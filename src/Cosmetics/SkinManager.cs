using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace FragmentOfJapanese.Cosmetics;

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

    /// <summary>Phát khi đổi skin một loại (category, skin mới).</summary>
    public event Action<SkinCategory, SkinDef> SkinChanged;

    public override void _Ready()
    {
        Instance = this;
        LoadDefs();
        InitDefaults();
        LoadEquipped();
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
}
