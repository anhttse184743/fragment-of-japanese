using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using FragmentOfJapanese.Autoloads;

namespace FragmentOfJapanese.Cosmetics;

/// <summary>Kết quả 1 skin khi quay (cho UI reveal).</summary>
public class SkinGachaResult
{
    public string SkinId   { get; set; } = "";
    public string Category { get; set; } = "";
    public string Name     { get; set; } = "";
    public string Rarity   { get; set; } = "";
    public bool   IsNew    { get; set; }
}

/// <summary>Kết quả tổng của 1 lượt quay skin.</summary>
public class SkinPullOutcome
{
    public List<SkinGachaResult> Results = new();
    public int    KeysLeft;
    public int    GoldRefunded;
    public string Error;   // null nếu thành công
}

/// <summary>
/// Quản lý skin (autoload). Nạp catalog từ data/skins.json; lưu skin ĐANG MẶC riêng vào
/// user://skins.save.json (giống QuestManager). Hiện tại MỞ SẴN tất cả skin (test) — sau
/// có thể nối gacha/shop bằng cách thêm danh sách "đã sở hữu".
/// Đổi skin → phát <see cref="SkinChanged"/> để PlayerCosmetics / UI cập nhật.
/// </summary>
public partial class SkinManager : Node
{
    public static SkinManager Instance { get; private set; }

    private const string DefsPath = "res://data/skins.json";
    private const string SavePath = "user://skins.save.json";

    private readonly List<SkinDef> _all = new();
    private readonly Dictionary<SkinCategory, string> _equipped = new();
    private readonly HashSet<string> _owned = new();   // skin đã mở khóa (gồm mặc định mỗi loại)

    /// <summary>Phát khi đổi skin một loại (category, skin mới).</summary>
    public event Action<SkinCategory, SkinDef> SkinChanged;
    /// <summary>Phát khi danh sách skin sở hữu thay đổi (sau sync/gacha) → UI túi cập nhật.</summary>
    public event Action OwnedChanged;

    public override void _Ready()
    {
        Instance = this;
        LoadDefs();
        InitDefaults();
        LoadEquipped();
    }

    // ───────── Sở hữu ─────────

    /// <summary>Skin mặc định mỗi loại = phần tử ĐẦU của loại trong catalog (luôn sở hữu).</summary>
    public bool IsDefault(string id)
    {
        var def = Get(id);
        if (def == null) return false;
        var first = _all.Find(s => s.Cat == def.Cat);
        return first != null && first.Id == id;
    }

    public bool IsOwned(string id) => IsDefault(id) || _owned.Contains(id);

    /// <summary>Nạp danh sách skin đã mở khóa từ server.</summary>
    public async Task SyncAsync()
    {
        if (string.IsNullOrEmpty(ApiClient.Instance.AccessToken)) return;

        var res = await ApiClient.Instance.GetAsync("/api/skins");
        if (!res.IsSuccessStatusCode) return;

        var data = await ApiClient.Instance.ReadAsAsync<AccountManager.ApiResponse<List<SkinDto>>>(res);
        if (data?.Data == null) return;

        _owned.Clear();
        foreach (var s in data.Data)
            if (s.Owned && !string.IsNullOrEmpty(s.SkinId)) _owned.Add(s.SkinId);

        // Nếu skin đang mặc bị khóa (vd dữ liệu cũ) → trả về mặc định loại đó.
        foreach (SkinCategory cat in Enum.GetValues<SkinCategory>())
        {
            var id = GetEquippedId(cat);
            if (!string.IsNullOrEmpty(id) && !IsOwned(id))
            {
                var first = _all.Find(s => s.Cat == cat);
                if (first != null) Equip(cat, first.Id);
            }
        }
        OwnedChanged?.Invoke();
    }

    /// <summary>Quay gacha skin bằng Chìa Khóa Bạc (server-authoritative).</summary>
    public async Task<SkinPullOutcome> GachaPullAsync(int count)
    {
        var outcome = new SkinPullOutcome();
        if (string.IsNullOrEmpty(ApiClient.Instance.AccessToken)) { outcome.Error = "Bạn cần đăng nhập."; return outcome; }

        var res  = await ApiClient.Instance.PostAsync("/api/skins/gacha-pull", new { PullCount = count });
        var data = await ApiClient.Instance.ReadAsAsync<AccountManager.ApiResponse<SkinGachaResponseDto>>(res);

        if (!res.IsSuccessStatusCode || data == null || !data.Success || data.Data == null)
        {
            outcome.Error = data?.Message ?? "Quay thất bại.";
            return outcome;
        }

        foreach (var r in data.Data.Results)
        {
            if (r.IsNew && !string.IsNullOrEmpty(r.SkinId)) _owned.Add(r.SkinId);
            outcome.Results.Add(new SkinGachaResult
            {
                SkinId = r.SkinId, Category = r.Category, Name = r.Name, Rarity = r.Rarity ?? "common", IsNew = r.IsNew
            });
        }
        outcome.KeysLeft     = data.Data.KeysLeft;
        outcome.GoldRefunded = data.Data.GoldRefunded;
        OwnedChanged?.Invoke();
        return outcome;
    }

    // ───────── Truy vấn ─────────

    public IEnumerable<SkinDef> Catalog(SkinCategory cat)
    {
        foreach (var s in _all)
            if (s.Cat == cat) yield return s;
    }

    public SkinDef Get(string id) => _all.Find(s => s.Id == id);

    public string GetEquippedId(SkinCategory cat) =>
        _equipped.TryGetValue(cat, out var id) ? id : "";

    public SkinDef GetEquipped(SkinCategory cat)
    {
        var def = _all.Find(s => s.Cat == cat && s.Id == GetEquippedId(cat));
        return def ?? _all.Find(s => s.Cat == cat);   // fallback: skin đầu của loại
    }

    // ───────── Đổi skin ─────────

    public void Equip(SkinCategory cat, string id)
    {
        var def = _all.Find(s => s.Cat == cat && s.Id == id);
        if (def == null) return;
        if (!IsOwned(id)) { GD.Print($"[Skin] Chưa mở khóa: {def.Name}"); return; }   // chỉ mặc skin đã sở hữu
        _equipped[cat] = id;
        SaveEquipped();
        SkinChanged?.Invoke(cat, def);
        GD.Print($"[Skin] Mặc {cat} = {def.Name}");
    }

    // ───────── Nạp / lưu ─────────

    private void InitDefaults()
    {
        foreach (SkinCategory cat in Enum.GetValues<SkinCategory>())
            if (!_equipped.ContainsKey(cat))
            {
                var first = _all.Find(s => s.Cat == cat);
                if (first != null) _equipped[cat] = first.Id;
            }
    }

    private void LoadDefs()
    {
        if (!FileAccess.FileExists(DefsPath)) { GD.PushWarning($"[Skin] Không thấy {DefsPath}"); return; }
        using var f = FileAccess.Open(DefsPath, FileAccess.ModeFlags.Read);
        var list = JsonSerializer.Deserialize<List<SkinDef>>(f.GetAsText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (list != null) _all.AddRange(list);
        GD.Print($"[Skin] Loaded {_all.Count} skins.");
    }

    private void SaveEquipped()
    {
        var blob = new Dictionary<string, string>();
        foreach (var kv in _equipped) blob[kv.Key.ToString()] = kv.Value;
        using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        f?.StoreString(JsonSerializer.Serialize(blob, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void LoadEquipped()
    {
        if (!FileAccess.FileExists(SavePath)) return;
        using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        var blob = JsonSerializer.Deserialize<Dictionary<string, string>>(f.GetAsText());
        if (blob == null) return;
        foreach (var kv in blob)
            if (Enum.TryParse<SkinCategory>(kv.Key, out var cat) && _all.Exists(s => s.Id == kv.Value))
                _equipped[cat] = kv.Value;
    }

    // ───────── DTO khớp server ─────────

    private class SkinDto
    {
        public string SkinId   { get; set; }
        public string Category { get; set; }
        public string Name     { get; set; }
        public string Rarity   { get; set; }
        public bool   IsDefault { get; set; }
        public bool   Owned    { get; set; }
    }

    private class SkinGachaResultDto
    {
        public string SkinId   { get; set; }
        public string Category { get; set; }
        public string Name     { get; set; }
        public string Rarity   { get; set; }
        public bool   IsNew    { get; set; }
    }

    private class SkinGachaResponseDto
    {
        public List<SkinGachaResultDto> Results { get; set; } = new();
        public int KeysLeft     { get; set; }
        public int GoldRefunded { get; set; }
    }
}
