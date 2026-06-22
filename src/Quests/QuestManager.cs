using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using FragmentOfJapanese.Core;
using FragmentOfJapanese.Items;

namespace FragmentOfJapanese.Quests;

/// <summary>
/// Quản lý nhiệm vụ hàng ngày / hàng tuần (autoload).
/// - Nạp định nghĩa từ data/quests.json; lưu tiến độ riêng vào user://quests.save.json
///   (KHÔNG dùng SaveSystem vì nó ghi 1 file blob duy nhất sẽ đè mất).
/// - Hệ thống khác báo tiến độ qua <see cref="Report"/>; nhận thưởng qua <see cref="Claim"/>.
/// - Tự reset khi sang ngày mới (daily) / tuần ISO mới (weekly).
/// EXP phát qua event <see cref="Claimed"/> để Player/HUD tự cộng (không có PlayerData toàn cục).
/// </summary>
public partial class QuestManager : Node
{
    public static QuestManager Instance { get; private set; }

    private const string DefsPath = "res://data/quests.json";
    private const string SavePath = "user://quests.save.json";

    private readonly List<QuestEntry> _quests = new();
    private readonly Dictionary<string, QuestState> _state = new();
    private string _lastDailyReset  = "";
    private string _lastWeeklyReset = "";

    public IReadOnlyList<QuestEntry> Quests => _quests;

    public event Action<QuestEntry> Progressed;
    public event Action<QuestEntry> Completed;
    public event Action<QuestEntry> Claimed;     // sau khi đã phát thưởng (đọc q.Reward.Exp để cộng EXP)
    public event Action             PeriodReset; // daily/weekly vừa reset

    public override void _Ready()
    {
        Instance = this;
        LoadDefs();
        LoadProgress();
        CheckResets();
    }

    // ───────── Truy vấn ─────────
    public int  GetCount(string id)        => _state.TryGetValue(id, out var s) ? s.Count : 0;
    public bool IsCompleted(QuestEntry q)  => GetCount(q.Id) >= q.Target;
    public bool IsClaimed(string id)       => _state.TryGetValue(id, out var s) && s.Claimed;
    public List<QuestEntry> ByPeriod(string period) => _quests.FindAll(q => q.Period == period);

    /// <summary>Hệ thống khác gọi để báo tiến độ. type vd "learn"/"battle"/"voice"; key tùy chọn.</summary>
    public void Report(string type, string key = "", int amount = 1)
    {
        if (amount <= 0) return;
        bool dirty = false;

        foreach (var q in _quests)
        {
            if (q.Type != type) continue;
            if (!string.IsNullOrEmpty(q.TargetKey) && q.TargetKey != key) continue;

            var s = StateOf(q.Id);
            if (s.Count >= q.Target) continue;   // đã xong

            s.Count = Math.Min(s.Count + amount, q.Target);
            dirty = true;
            Progressed?.Invoke(q);
            if (s.Count >= q.Target)
            {
                GD.Print($"[Quest] HOÀN THÀNH: {q.NameVi}");
                Completed?.Invoke(q);
            }
        }

        if (dirty) SaveProgress();
    }

    /// <summary>Nhận thưởng nhiệm vụ đã hoàn thành. False nếu chưa xong / đã nhận.</summary>
    public bool Claim(string id)
    {
        var q = _quests.Find(x => x.Id == id);
        if (q == null || !IsCompleted(q) || IsClaimed(id)) return false;

        var r = q.Reward;
        if (r.Gold    != 0) Wallet.Instance?.AddGold(r.Gold);
        if (r.MaThach != 0) Wallet.Instance?.AddMaThach(r.MaThach);
        if (!string.IsNullOrEmpty(r.ItemId) && r.ItemCount > 0)
            Inventory.Instance?.Add(r.ItemId, r.ItemCount);

        StateOf(id).Claimed = true;
        SaveProgress();
        GD.Print($"[Quest] Nhận '{q.NameVi}': +{r.Exp} exp, +{r.Gold} Vàng, +{r.MaThach} Ma Thạch"
               + (r.ItemCount > 0 ? $", +{r.ItemCount} {r.ItemId}" : ""));
        Claimed?.Invoke(q);   // Player/HUD đọc q.Reward.Exp để cộng EXP
        return true;
    }

    // ───────── Reset theo ngày / tuần ─────────
    private void CheckResets()
    {
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        string week  = IsoWeekKey(DateTime.Now);
        bool dirty = false;

        if (_lastDailyReset != today)
        {
            ResetPeriod("daily");
            _lastDailyReset = today;
            dirty = true;
            GD.Print($"[Quest] Reset nhiệm vụ HÀNG NGÀY ({today}).");
        }
        if (_lastWeeklyReset != week)
        {
            ResetPeriod("weekly");
            _lastWeeklyReset = week;
            dirty = true;
            GD.Print($"[Quest] Reset nhiệm vụ HÀNG TUẦN ({week}).");
        }

        if (dirty) { SaveProgress(); PeriodReset?.Invoke(); }
    }

    private void ResetPeriod(string period)
    {
        foreach (var q in _quests)
            if (q.Period == period && _state.TryGetValue(q.Id, out var s))
            { s.Count = 0; s.Claimed = false; }
    }

    private static string IsoWeekKey(DateTime dt)
        => $"{System.Globalization.ISOWeek.GetYear(dt)}-W{System.Globalization.ISOWeek.GetWeekOfYear(dt):00}";

    private QuestState StateOf(string id)
    {
        if (!_state.TryGetValue(id, out var s)) { s = new QuestState(); _state[id] = s; }
        return s;
    }

    // ───────── Nạp / lưu ─────────
    private void LoadDefs()
    {
        if (!FileAccess.FileExists(DefsPath)) { GD.PushWarning($"[Quest] Không thấy {DefsPath}"); return; }
        using var f = FileAccess.Open(DefsPath, FileAccess.ModeFlags.Read);
        var list = JsonSerializer.Deserialize<List<QuestEntry>>(f.GetAsText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (list != null) _quests.AddRange(list);
        foreach (var q in _quests) StateOf(q.Id);
        GD.Print($"[Quest] Loaded {_quests.Count} nhiệm vụ.");
    }

    private void SaveProgress()
    {
        var blob = new SaveBlob { LastDailyReset = _lastDailyReset, LastWeeklyReset = _lastWeeklyReset };
        foreach (var kv in _state) blob.Progress[kv.Key] = kv.Value;
        using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        f?.StoreString(JsonSerializer.Serialize(blob, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void LoadProgress()
    {
        if (!FileAccess.FileExists(SavePath)) return;
        using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        var blob = JsonSerializer.Deserialize<SaveBlob>(f.GetAsText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (blob == null) return;
        _lastDailyReset  = blob.LastDailyReset  ?? "";
        _lastWeeklyReset = blob.LastWeeklyReset ?? "";
        if (blob.Progress != null)
            foreach (var kv in blob.Progress) _state[kv.Key] = kv.Value;
    }

    private class QuestState
    {
        [JsonPropertyName("count")]   public int  Count   { get; set; }
        [JsonPropertyName("claimed")] public bool Claimed { get; set; }
    }

    private class SaveBlob
    {
        [JsonPropertyName("last_daily_reset")]  public string LastDailyReset  { get; set; } = "";
        [JsonPropertyName("last_weekly_reset")] public string LastWeeklyReset { get; set; } = "";
        [JsonPropertyName("progress")]          public Dictionary<string, QuestState> Progress { get; set; } = new();
    }
}
