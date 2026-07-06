using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using FragmentOfJapanese.Autoloads;
using FragmentOfJapanese.Core;
using FragmentOfJapanese.Entities.Player;

namespace FragmentOfJapanese.Items;

/// <summary>1 ô trong túi: 1 loại vật phẩm + số lượng.</summary>
public class ItemStack
{
    public string    InventoryId { get; set; } // UUID từ server trả về
    public ItemEntry Item  { get; }
    public int       Count { get; set; }

    public ItemStack(ItemEntry item, int count, string inventoryId = "")
    {
        Item  = item;
        Count = count;
        InventoryId = inventoryId;
    }
}

/// <summary>
/// Túi đồ người chơi (autoload). Đồng bộ từ API thay vì user://
/// </summary>
public partial class Inventory : Node
{
    public static Inventory Instance { get; private set; }

    public const int MaxStack = 99;

    private readonly List<ItemStack> _stacks = new();

    public IReadOnlyList<ItemStack> Stacks => _stacks;

    public event Action Changed;
    public event Action<ItemEntry> ItemUsed;

    public override void _Ready()
    {
        Instance = this;
    }

    public async System.Threading.Tasks.Task SyncAsync()
    {
        if (string.IsNullOrEmpty(ApiClient.Instance.AccessToken)) return;

        var res = await ApiClient.Instance.GetAsync("/api/inventory");
        if (res.IsSuccessStatusCode)
        {
            var data = await ApiClient.Instance.ReadAsAsync<AccountManager.ApiResponse<List<InventoryItemDto>>>(res);
            if (data?.Data != null)
            {
                _stacks.Clear();
                foreach (var dto in data.Data)
                {
                    if (string.IsNullOrEmpty(dto.StringId)) continue;
                    var def = ItemDatabase.Instance?.Get(dto.StringId);
                    if (def != null)
                    {
                        _stacks.Add(new ItemStack(def, dto.Quantity, dto.InventoryId));
                    }
                }
                Changed?.Invoke();
            }
        }
    }

    public int  CountOf(string itemId)             => Find(itemId)?.Count ?? 0;
    public bool Has(string itemId, int amount = 1) => CountOf(itemId) >= amount;

    public void AddOptimistic(string itemId, int amount = 1, string inventoryId = "")
    {
        if (amount <= 0) return;

        var def = ItemDatabase.Instance?.Get(itemId);
        if (def == null) return;

        var stack = Find(itemId);
        if (stack == null) { stack = new ItemStack(def, 0, inventoryId); _stacks.Add(stack); }
        // Không kẹp trần ở client: số lượng thật do server quyết (SyncAsync sẽ ghi đè).
        // Nếu kẹp 99 ở đây, loot vượt 99 sẽ biến mất khỏi UI cho tới lần sync kế.
        stack.Count += amount;
        if (!string.IsNullOrEmpty(inventoryId)) stack.InventoryId = inventoryId;

        Changed?.Invoke();
    }

    /// <summary>Cộng vật phẩm (loot/gacha/redeem) — hiện ngay ở UI rồi lưu lên server, sau đó sync lại để có InventoryId.</summary>
    public async System.Threading.Tasks.Task GrantAsync(string itemId, int amount = 1)
    {
        if (amount <= 0) return;

        AddOptimistic(itemId, amount);   // hiện tức thì cho người chơi

        if (string.IsNullOrEmpty(ApiClient.Instance.AccessToken)) return;

        var res = await ApiClient.Instance.PostAsync("/api/inventory/grant",
            new { StringId = itemId, Quantity = amount });
        if (res.IsSuccessStatusCode)
            await SyncAsync();           // đồng bộ lại số lượng thật + gán InventoryId (để dùng được)
        else
            GD.PushWarning($"[Inventory] Grant '{itemId}' x{amount} thất bại: {(int)res.StatusCode}");
    }

    /// <summary>Đổi Cuộn Từ Vựng lấy 1 chìa (server trừ cuộn + cộng chìa nguyên tử). Trả (ok, lỗi).</summary>
    public async System.Threading.Tasks.Task<(bool ok, string error)> ExchangeScrollsAsync(string keyStringId)
    {
        if (string.IsNullOrEmpty(ApiClient.Instance.AccessToken)) return (false, "Bạn cần đăng nhập.");

        var res = await ApiClient.Instance.PostAsync("/api/inventory/exchange-scrolls", new { KeyStringId = keyStringId });
        if (res.IsSuccessStatusCode)
        {
            await SyncAsync();   // lấy số cuộn + chìa thật từ server
            return (true, null);
        }
        var err = await ApiClient.Instance.ReadAsAsync<AccountManager.ApiResponse<object>>(res);
        return (false, err?.Message ?? "Trao đổi thất bại.");
    }

    public void RemoveOptimistic(string itemId, int amount = 1)
    {
        var stack = Find(itemId);
        if (stack == null || stack.Count < amount) return;

        stack.Count -= amount;
        if (stack.Count <= 0) _stacks.Remove(stack);

        Changed?.Invoke();
    }

    public async System.Threading.Tasks.Task<bool> UseItemAsync(string itemId)
    {
        var stack = Find(itemId);
        if (stack == null || stack.Count <= 0 || string.IsNullOrEmpty(stack.InventoryId)) return false;

        var res = await ApiClient.Instance.PostAsync($"/api/inventory/{stack.InventoryId}/use", new { });
        if (res.IsSuccessStatusCode)
        {
            var item = stack.Item;
            RemoveOptimistic(itemId, 1);
            ItemUsed?.Invoke(item);
            
            // Sync lại wallet vì có thể dùng bình tiền/kinh nghiệm
            await Wallet.Instance.SyncAsync();
            var player = GetTree().GetFirstNodeInGroup("player") as Player;
            if (player != null) await player.SyncFromServerAsync();
            return true;
        }
        return false;
    }

    public void Clear()
    {
        _stacks.Clear();
        Changed?.Invoke();
    }

    private ItemStack Find(string itemId) => _stacks.Find(s => s.Item.Id == itemId);

    private class InventoryItemDto
    {
        public string InventoryId { get; set; }
        public string ItemId { get; set; }
        public string StringId { get; set; }
        public int Quantity { get; set; }
    }
}
