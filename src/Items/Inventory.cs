using Godot;
using System;
using System.Collections.Generic;
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
/// Túi đồ người chơi (autoload). Lưu vật phẩm dạng stack — mỗi loại 1 stack,
/// tối đa <see cref="MaxStack"/> đơn vị.
///
/// API: Add / Remove / UseItem / Has / CountOf / Clear.
/// Phát <see cref="Changed"/> mỗi khi thay đổi → UI tự refresh.
/// Hiệu ứng vật phẩm (heal, learn...) KHÔNG xử lý ở đây mà phát qua
/// <see cref="ItemUsed"/> để game logic (battle/world) tự áp dụng → giữ tách lớp.
/// </summary>
public partial class Inventory : Node
{
    public static Inventory Instance { get; private set; }

    public const int MaxStack = 99;

    private readonly List<ItemStack> _stacks = new();   // giữ thứ tự thêm vào cho UI ổn định

    public IReadOnlyList<ItemStack> Stacks => _stacks;

    /// <summary>Nội dung túi thay đổi (thêm/bớt/dùng/clear).</summary>
    public event Action Changed;

    /// <summary>Vừa dùng 1 vật phẩm — consumer áp dụng hiệu ứng (effect/value).</summary>
    public event Action<ItemEntry> ItemUsed;

    public override void _Ready()
    {
        Instance = this;
        SeedDemoItems();
    }

    public int  CountOf(string itemId)              => Find(itemId)?.Count ?? 0;
    public bool Has(string itemId, int amount = 1)  => CountOf(itemId) >= amount;

    /// <summary>Thêm <paramref name="amount"/> vật phẩm vào túi.</summary>
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
        if (stack == null)
        {
            stack = new ItemStack(def, 0);
            _stacks.Add(stack);
        }
        stack.Count = Mathf.Min(stack.Count + amount, MaxStack);

        Changed?.Invoke();
        return true;
    }

    /// <summary>Bớt <paramref name="amount"/> vật phẩm. False nếu không đủ.</summary>
    public bool Remove(string itemId, int amount = 1)
    {
        var stack = Find(itemId);
        if (stack == null || stack.Count < amount) return false;

        stack.Count -= amount;
        if (stack.Count <= 0) _stacks.Remove(stack);

        Changed?.Invoke();
        return true;
    }

    /// <summary>Dùng 1 vật phẩm: bớt 1 đơn vị rồi phát <see cref="ItemUsed"/>.</summary>
    public bool UseItem(string itemId)
    {
        var stack = Find(itemId);
        if (stack == null || stack.Count <= 0) return false;

        var item = stack.Item;
        Remove(itemId, 1);                  // đã phát Changed

        GD.Print($"[Inventory] Dùng '{item.NameVi}' (effect={item.Effect}, value={item.Value}).");
        ItemUsed?.Invoke(item);
        return true;
    }

    public void Clear()
    {
        _stacks.Clear();
        Changed?.Invoke();
    }

    private ItemStack Find(string itemId) => _stacks.Find(s => s.Item.Id == itemId);

    // ───── Save / Load: serialize sang {itemId: count} ─────
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
            foreach (var kv in data)
                Add(kv.Key, kv.Value);
        Changed?.Invoke();
    }

    // DEMO: vật phẩm khởi đầu để xem UI hoạt động ngay. Xóa khi có loot/shop thật.
    private void SeedDemoItems()
    {
        Add("item_potion",        5);   // tiêu hao
        Add("item_hi_potion",     2);
        Add("item_scroll",        3);
        Add("item_silver_key",    4);   // chìa khóa (gacha)
        Add("item_golden_key",    1);
        Add("item_goblin_ear",   12);   // đổi / bán
        Add("item_wolf_pelt",     3);
        Add("item_wooden_sword",  1);   // trang bị (sắp ra mắt)
        Add("item_leather_armor", 1);
    }
}
