using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using FragmentOfJapanese.Autoloads;
using FragmentOfJapanese.Core;

namespace FragmentOfJapanese.Items;

/// <summary>1 ô trong túi: 1 loại vật phẩm + số lượng.</summary>
public class ItemStack
{
    public ItemEntry Item  { get; }
    public int       Count { get; set; }

    public ItemStack(ItemEntry item, int count)
    {
        Item  = item;
        Count = count;
    }
}

/// <summary>
/// Túi đồ người chơi (autoload). Tự lưu user://inventory.json khi thay đổi và khi thoát game.
/// </summary>
public partial class Inventory : Node
{
    public static Inventory Instance { get; private set; }

    public const int MaxStack = 99;

    private readonly List<ItemStack> _stacks = new();

    public IReadOnlyList<ItemStack> Stacks => _stacks;

    public event Action Changed;
    public event Action<ItemEntry> ItemUsed;

    private const string SavePath = "user://inventory.json";
    private bool _loaded;

    public override void _Ready()
    {
        Instance = this;
        Load();   // ItemDatabase đứng trước Inventory trong autoload → đã sẵn sàng
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) Save();
    }

    public int  CountOf(string itemId)             => Find(itemId)?.Count ?? 0;
    public bool Has(string itemId, int amount = 1) => CountOf(itemId) >= amount;

    public bool Add(string itemId, int amount = 1)
    {
        if (amount <= 0) return false;

        var def = ItemDatabase.Instance?.Get(itemId);
        if (def == null)
        {
            GD.PushWarning($"[Inventory] Không có item id '{itemId}' trong ItemDatabase.");
            return false;
        }

        var stack = Find(itemId);
        if (stack == null) { stack = new ItemStack(def, 0); _stacks.Add(stack); }
        stack.Count = Mathf.Min(stack.Count + amount, MaxStack);

        Changed?.Invoke();
        if (_loaded) Save();
        return true;
    }

    public bool Remove(string itemId, int amount = 1)
    {
        var stack = Find(itemId);
        if (stack == null || stack.Count < amount) return false;

        stack.Count -= amount;
        if (stack.Count <= 0) _stacks.Remove(stack);

        Changed?.Invoke();
        if (_loaded) Save();
        return true;
    }

    public bool UseItem(string itemId)
    {
        var stack = Find(itemId);
        if (stack == null || stack.Count <= 0) return false;

        var item = stack.Item;
        Remove(itemId, 1);
        ItemUsed?.Invoke(item);
        return true;
    }

    public void Clear()
    {
        _stacks.Clear();
        Changed?.Invoke();
        if (_loaded) Save();
    }

    private ItemStack Find(string itemId) => _stacks.Find(s => s.Item.Id == itemId);

    public Dictionary<string, int> ToSaveData()
    {
        var data = new Dictionary<string, int>();
        foreach (var s in _stacks) data[s.Item.Id] = s.Count;
        return data;
    }

    public void LoadFromData(Dictionary<string, int> data)
    {
        _stacks.Clear();
        if (data != null)
            foreach (var kv in data) Add(kv.Key, kv.Value);
        Changed?.Invoke();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(ToSaveData());
            using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
            f?.StoreString(json);
        }
        catch (Exception e) { GD.PushWarning($"[Inventory] Save lỗi: {e.Message}"); }
    }

    private void Load()
    {
        _loaded = true;
        if (!FileAccess.FileExists(SavePath)) return;
        try
        {
            using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
            var data = JsonSerializer.Deserialize<Dictionary<string, int>>(f?.GetAsText() ?? "{}");
            LoadFromData(data);
        }
        catch (Exception e) { GD.PushWarning($"[Inventory] Load lỗi: {e.Message}"); }
    }
}
