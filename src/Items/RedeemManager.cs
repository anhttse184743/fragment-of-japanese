using Godot;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using FragmentOfJapanese.Autoloads;

namespace FragmentOfJapanese.Items;

public class RewardItem
{
    [JsonPropertyName("id")]    public string Id    { get; set; } = "";
    [JsonPropertyName("count")] public int    Count { get; set; } = 1;
}

public class RedeemReward
{
    [JsonPropertyName("code")]        public string           Code        { get; set; } = "";
    [JsonPropertyName("gold")]        public int              Gold        { get; set; } = 0;
    [JsonPropertyName("aetherstone")] public int              Aetherstone { get; set; } = 0;
    [JsonPropertyName("items")]       public List<RewardItem> Items       { get; set; } = new();
}

/// <summary>
/// Quản lý mã quà tặng (redeem code) — autoload.
/// Nạp danh sách mã từ data/codes.json; mỗi mã thưởng Vàng / Aetherstone / vật phẩm.
/// Mỗi mã chỉ dùng 1 lần — lưu user://redeemed_codes.json để không nhập lại sau khi tắt game.
/// </summary>
public partial class RedeemManager : Node
{
    public static RedeemManager Instance { get; private set; }

    public enum Result { Success, NotFound, AlreadyUsed }

    private const string DataPath = "res://data/codes.json";
    private const string SavePath = "user://redeemed_codes.json";

    private readonly Dictionary<string, RedeemReward> _codes = new();   // key = mã CHỮ HOA
    private readonly HashSet<string>                  _used  = new();

    public override void _Ready()
    {
        Instance = this;
        LoadCodes();
        LoadUsed();
    }

    /// <summary>Đổi mã. Trả kết quả; nếu Success thì <paramref name="reward"/> mô tả quà nhận được.</summary>
    public Result Redeem(string code, out string reward)
    {
        reward = "";
        string key = (code ?? "").Trim().ToUpperInvariant();
        if (key.Length == 0 || !_codes.TryGetValue(key, out var r)) return Result.NotFound;
        if (_used.Contains(key)) return Result.AlreadyUsed;

        var parts = new List<string>();
        if (r.Gold > 0)        { Wallet.Instance?.AddGold(r.Gold);           parts.Add($"{r.Gold:N0} Vàng"); }
        if (r.Aetherstone > 0) { Wallet.Instance?.AddMaThach(r.Aetherstone); parts.Add($"{r.Aetherstone:N0} Aetherstone"); }
        foreach (var it in r.Items)
        {
            if (string.IsNullOrEmpty(it.Id) || it.Count <= 0) continue;
            Inventory.Instance?.Add(it.Id, it.Count);
            var def = ItemDatabase.Instance?.Get(it.Id);
            parts.Add($"{it.Count}x {def?.NameVi ?? it.Id}");
        }

        _used.Add(key);
        SaveUsed();
        reward = parts.Count > 0 ? string.Join(", ", parts) : "(không có quà)";
        return Result.Success;
    }

    private void LoadCodes()
    {
        if (!FileAccess.FileExists(DataPath))
        {
            GD.PushWarning($"[RedeemManager] File not found: {DataPath}");
            return;
        }
        using var file = FileAccess.Open(DataPath, FileAccess.ModeFlags.Read);
        var list = JsonSerializer.Deserialize<List<RedeemReward>>(file.GetAsText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

        foreach (var r in list)
            if (!string.IsNullOrWhiteSpace(r.Code))
                _codes[r.Code.Trim().ToUpperInvariant()] = r;

        GD.Print($"[RedeemManager] Loaded {_codes.Count} codes.");
    }

    private void LoadUsed()
    {
        if (!FileAccess.FileExists(SavePath)) return;
        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        var list = JsonSerializer.Deserialize<List<string>>(file.GetAsText()) ?? new();
        foreach (var c in list) _used.Add(c);
    }

    private void SaveUsed()
    {
        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        file?.StoreString(JsonSerializer.Serialize(new List<string>(_used)));
    }
}
