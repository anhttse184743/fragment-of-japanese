using Godot;
using System;
using System.Threading.Tasks;
using FragmentOfJapanese.Autoloads;

namespace FragmentOfJapanese.Items;

/// <summary>
/// Ví tiền người chơi (autoload). Đồng bộ dữ liệu qua Backend API thay vì lưu cục bộ.
/// </summary>
public partial class Wallet : Node
{
    public static Wallet Instance { get; private set; }

    public int Gold { get; private set; }
    public int MaThach { get; private set; }

    public event Action Changed;

    public override void _Ready()
    {
        Instance = this;
    }

    /// <summary>
    /// Đồng bộ số dư từ Backend (dùng endpoint GetProfile).
    /// </summary>
    public async Task SyncAsync()
    {
        if (string.IsNullOrEmpty(ApiClient.Instance.AccessToken)) return;

        var res = await ApiClient.Instance.GetAsync("/api/player/profile");
        if (res.IsSuccessStatusCode)
        {
            var data = await ApiClient.Instance.ReadAsAsync<AccountManager.ApiResponse<PlayerProfileDto>>(res);
            if (data?.Data != null)
            {
                Gold = data.Data.Gold;
                MaThach = data.Data.PremiumCurrency;
                Changed?.Invoke();
            }
        }
    }

    // Các hàm AddGold / SpendGold hiện tại sẽ chỉ update UI tạm thời,
    // logic thực sự thay đổi tiền tệ sẽ diễn ra qua các lệnh Backend (mua đồ, nhận thưởng).
    // Tuy nhiên, có thể giữ các hàm này để update UI "lạc quan" (Optimistic UI) nếu cần.
    
    public void AddGold(int amount)
    {
        UpdateOptimistic(amount, 0);
    }

    public bool SpendGold(int amount)
    {
        if (amount < 0 || Gold < amount) return false;
        UpdateOptimistic(-amount, 0);
        return true;
    }

    public void AddMaThach(int amount)
    {
        UpdateOptimistic(0, amount);
    }

    public bool SpendMaThach(int amount)
    {
        if (amount < 0 || MaThach < amount) return false;
        UpdateOptimistic(0, -amount);
        return true;
    }
    
    /// <summary>
    /// CHỈ cập nhật UI tạm thời (optimistic). KHÔNG tự đẩy lên server —
    /// số dư thật do server quyết (mua/gacha/quest/thưởng/redeem), client gọi SyncAsync để lấy lại.
    /// </summary>
    public void UpdateOptimistic(int goldChange, int maThachChange)
    {
        Gold = Mathf.Max(0, Gold + goldChange);
        MaThach = Mathf.Max(0, MaThach + maThachChange);
        Changed?.Invoke();
    }

    /// <summary>Thưởng Vàng từ gameplay (loot/quảng cáo) — đi qua endpoint server có kẹp trần, rồi đồng bộ lại.</summary>
    public async Task RewardGoldAsync(int gold)
    {
        if (gold <= 0) return;
        UpdateOptimistic(gold, 0);   // hiện ngay
        if (string.IsNullOrEmpty(ApiClient.Instance.AccessToken)) return;

        var res = await ApiClient.Instance.PostAsync("/api/player/reward", new { Exp = 0, Gold = gold });
        if (res.IsSuccessStatusCode) await SyncAsync();   // lấy số thật (đã kẹp trần) từ server
        else GD.PushWarning($"[Wallet] RewardGold {gold} thất bại: {(int)res.StatusCode}");
    }

    private class PlayerProfileDto
    {
        public int Gold { get; set; }
        public int PremiumCurrency { get; set; }
    }
}
