using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;
using FragmentOfJapanese.Autoloads;

namespace FragmentOfJapanese.Items;

/// <summary>
/// Đổi mã quà tặng — server-authoritative. Mã + lần đã đổi nằm ở DB (bảng redeem_codes /
/// code_redemptions), nên không thể dùng lại bằng cách cài lại game, và phần thưởng do server cộng.
/// </summary>
public partial class RedeemManager : Node
{
    public static RedeemManager Instance { get; private set; }

    public override void _Ready() => Instance = this;

    private class RedeemResponseDto
    {
        public string Message { get; set; } = "";
        public int Gold { get; set; }
        public int PremiumCurrency { get; set; }
        public string ItemStringId { get; set; }
        public int ItemQuantity { get; set; }
    }

    /// <summary>Đổi mã qua server. Trả (thành công, thông điệp để hiển thị).</summary>
    public async Task<(bool ok, string message)> RedeemAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return (false, "Hãy nhập mã.");
        if (string.IsNullOrEmpty(ApiClient.Instance.AccessToken)) return (false, "Bạn cần đăng nhập trước.");

        var res = await ApiClient.Instance.PostAsync("/api/redeem", new { Code = code.Trim() });
        var data = await ApiClient.Instance.ReadAsAsync<AccountManager.ApiResponse<RedeemResponseDto>>(res);

        if (!res.IsSuccessStatusCode || data == null || !data.Success)
            return (false, data?.Message ?? "Đổi mã thất bại.");

        // Phần thưởng đã được server cộng → đồng bộ lại ví + túi để hiện đúng.
        _ = Wallet.Instance?.SyncAsync();
        _ = Inventory.Instance?.SyncAsync();

        var parts = new List<string>();
        var r = data.Data;
        if (r != null)
        {
            if (r.Gold > 0)            parts.Add($"{r.Gold:N0} Vàng");
            if (r.PremiumCurrency > 0) parts.Add($"{r.PremiumCurrency:N0} Ma Thạch");
            if (!string.IsNullOrEmpty(r.ItemStringId) && r.ItemQuantity > 0)
            {
                var def = ItemDatabase.Instance?.Get(r.ItemStringId);
                parts.Add($"{r.ItemQuantity}x {def?.NameVi ?? r.ItemStringId}");
            }
        }
        string reward = parts.Count > 0 ? string.Join(", ", parts) : "phần thưởng";
        return (true, $"Đổi mã thành công! Nhận: {reward}.");
    }
}
