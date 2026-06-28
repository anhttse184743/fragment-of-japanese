using Godot;
using System;
using System.Collections.Generic;
using FragmentOfJapanese.Autoloads;
using FragmentOfJapanese.Core;

namespace FragmentOfJapanese.Items;

/// <summary>
/// Hệ thống gacha (autoload).
/// Đồng bộ từ server.
/// </summary>
public partial class Gacha : Node
{
    public static Gacha Instance { get; private set; }

    public const string TicketId = "item_silver_key";   // vé quay
    public const int    PityMax  = 90;

    public int Pity { get; private set; }
    public event Action Changed;

    public override void _Ready() => Instance = this;

    public bool CanPull(int count) => Wallet.Instance?.MaThach >= 160 * count; // 160 gem/pull for now

    /// <summary>Quay <paramref name="count"/> lần.</summary>
    public async System.Threading.Tasks.Task<List<ItemEntry>> PullAsync(int count)
    {
        if (count <= 0) return null;
        if (!CanPull(count)) return null;

        var res = await ApiClient.Instance.PostAsync("/api/gacha/pull", new { PullCount = count });
        if (res.IsSuccessStatusCode)
        {
            var data = await ApiClient.Instance.ReadAsAsync<AccountManager.ApiResponse<GachaResultDto>>(res);
            if (data?.Data != null)
            {
                var results = new List<ItemEntry>();
                foreach (var item in data.Data.Items)
                {
                    Pity = item.PityCount;
                    if (!string.IsNullOrEmpty(item.StringId))
                    {
                        var def = ItemDatabase.Instance?.Get(item.StringId);
                        if (def != null) results.Add(def);
                    }
                }
                
                // Sync inventory & wallet after gacha
                await Inventory.Instance.SyncAsync();
                await Wallet.Instance.SyncAsync();
                
                Changed?.Invoke();
                return results;
            }
        }
        return null;
    }

    private class GachaResultDto
    {
        public List<GachaItemDto> Items { get; set; } = new();
        public int TotalCostPremium { get; set; }
        public int RemainingPremiumCurrency { get; set; }
    }

    private class GachaItemDto
    {
        public string ItemId { get; set; }
        public string StringId { get; set; }
        public string ItemName { get; set; }
        public string Rarity { get; set; }
        public string IconUrl { get; set; }
        public int PityCount { get; set; }
    }
}
