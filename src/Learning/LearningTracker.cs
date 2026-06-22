using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using FragmentOfJapanese.Autoloads;
using FragmentOfJapanese.Core;

namespace FragmentOfJapanese.Learning;

/// <summary>
/// Theo dõi học tập + ôn bài (spaced repetition) + KHÓA BÀI TUẦN TỰ — autoload.
/// • Ghi mỗi lần trả lời (đúng/sai + thời gian) cho từng từ/ngữ pháp/đoạn đọc.
/// • Phát hiện "chọn bừa": trả lời nhanh hơn GuessThresholdMs trên mục chưa thuộc → không lên bậc.
/// • Đánh giá: đã thuộc (box≥4) / đang học / hay sai (struggling) / chưa học.
/// • KHÓA BÀI: chỉ học MỚI ở "bài hiện tại"; phải thuộc HẾT (UnlockRatio) mới mở bài kế tiếp.
///   Ôn lại (bài cũ đến hạn) thì lấy từ MỌI bài đã mở — đó không phải "học tùm lum".
/// • Tự lưu user://learning.save.json.
/// </summary>
public partial class LearningTracker : Node
{
    public static LearningTracker Instance { get; private set; }

    [Export] public float NewItemRatio     = 0.35f;   // xác suất chọn bài MỚI (vs ôn bài cũ)
    [Export] public int   GuessThresholdMs = 1200;    // trả lời nhanh hơn = nghi chọn bừa
    [Export] public float UnlockRatio      = 1.0f;    // tỉ lệ mục phải THUỘC để mở bài kế (1.0 = học hết)

    private const int MasteredBox = 4;
    private const int MaxBox      = 5;
    private static readonly long[] Intervals = { 0, 60, 600, 86400, 259200, 604800 }; // 0s,1ph,10ph,1d,3d,7d
    private const string SavePath = "user://learning.save.json";

    private readonly Dictionary<string, ItemProgress> _progress = new();
    private readonly Random _rng = new();
    private int _unlocked = 1;

    [Signal] public delegate void ItemMasteredEventHandler(string kind, string id);
    [Signal] public delegate void LessonUnlockedEventHandler(int lesson);

    public override void _Ready()
    {
        Instance = this;
        Load();
    }

    private static string KindStr(ItemKind k) => k == ItemKind.Vocab ? "vocab" : k == ItemKind.Grammar ? "grammar" : "reading";
    private static string Key(ItemKind k, string id) => KindStr(k) + ":" + id;
    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    // ───────── Ghi nhận một lần trả lời ─────────
    public void Record(ItemKind kind, string id, bool correct, double responseMs = -1)
    {
        string key = Key(kind, id);
        if (!_progress.TryGetValue(key, out var p)) { p = new ItemProgress { Key = key }; _progress[key] = p; }

        bool wasMastered = p.Box >= MasteredBox;
        bool fast  = responseMs >= 0 && responseMs < GuessThresholdMs;
        bool guess = fast && !wasMastered;

        p.Seen++;
        p.LastSeen = Now;
        if (guess) p.Guesses++;

        if (correct)
        {
            p.Correct++;
            if (!guess) { p.Streak++; if (p.Box < MaxBox) p.Box++; }
            else          p.Streak = 0;
        }
        else { p.Wrong++; p.Streak = 0; p.Box = 0; }

        p.Due = Now + Intervals[Math.Clamp(p.Box, 0, Intervals.Length - 1)];

        if (!wasMastered && p.Box >= MasteredBox)
        {
            GD.Print($"[Learn] ĐÃ THUỘC: {key}");
            EmitSignal(SignalName.ItemMastered, KindStr(kind), id);
        }
        CheckUnlock();
        Save();
    }

    // ───────── Khóa bài tuần tự ─────────
    public int  UnlockedLesson           => _unlocked;
    public bool IsUnlocked(int lesson)   => lesson >= 1 && lesson <= _unlocked;

    /// <summary>Bài đã "học hết" = tỉ lệ mục đã thuộc ≥ UnlockRatio (mặc định 100%).</summary>
    public bool IsLessonComplete(int lesson)
    {
        var s = GetLessonStats(lesson);
        return s.Total > 0 && (double)s.Mastered / s.Total >= UnlockRatio;
    }

    /// <summary>Bài đang học = bài đã mở khóa thấp nhất mà chưa hoàn thành.</summary>
    public int CurrentLesson
    {
        get
        {
            for (int l = 1; l <= _unlocked; l++)
                if (!IsLessonComplete(l)) return l;
            return _unlocked;
        }
    }

    private void CheckUnlock()
    {
        int max = MaxLesson();
        while (_unlocked < max && IsLessonComplete(_unlocked))
        {
            _unlocked++;
            GD.Print($"[Learn] 🔓 MỞ KHÓA Bài {_unlocked}");
            EmitSignal(SignalName.LessonUnlocked, _unlocked);
        }
    }

    private int MaxLesson()
    {
        var db = JapaneseDB.Instance;
        if (db != null && db.Lessons.Count > 0) return db.Lessons.Max(l => l.Lesson);
        return 25;
    }

    // ───────── Truy vấn ─────────
    public ItemProgress Get(ItemKind kind, string id) => _progress.TryGetValue(Key(kind, id), out var p) ? p : null;
    public bool IsNew(ItemKind kind, string id)        => !_progress.ContainsKey(Key(kind, id));
    public bool IsMastered(ItemKind kind, string id)   { var p = Get(kind, id); return p != null && p.Box >= MasteredBox; }
    public bool IsStruggling(ItemKind kind, string id) { var p = Get(kind, id); return p != null && p.Seen >= 3 && p.Accuracy < 0.5 && p.Box <= 1; }

    // ───────── Bộ lịch: bài MỚI (chỉ bài hiện tại) + ÔN bài cũ đến hạn (mọi bài đã mở) ─────────
    public ReviewItem NextItem() => NextItem(CurrentLesson);

    public ReviewItem NextItem(int lesson)
    {
        long now = Now;
        var due  = _progress.Values.Where(p => p.Due <= now).OrderBy(p => p.Due).ToList();
        var news = IsUnlocked(lesson) ? NewItemsOf(lesson) : new List<(ItemKind, string)>();

        if (due.Count == 0 && news.Count == 0) return null;
        bool pickNew = news.Count > 0 && (due.Count == 0 || _rng.NextDouble() < NewItemRatio);
        if (pickNew) { var (k, id) = news[0]; return new ReviewItem(k, id, true); }
        return ToReviewItem(due[0].Key, false);
    }

    /// <summary>Chọn 1 mục theo LOẠI (vd Vocab cho đòn kết liễu): mới của bài hiện tại + ôn bài cũ cùng loại.</summary>
    public ReviewItem NextItem(ItemKind kind)
    {
        long now = Now;
        var due = _progress.Values.Where(p => p.Due <= now).OrderBy(p => p.Due)
                            .Select(p => ToReviewItem(p.Key, false))
                            .Where(r => r.Kind == kind).ToList();
        var news = NewItemsOf(CurrentLesson).Where(t => t.Item1 == kind)
                            .Select(t => new ReviewItem(t.Item1, t.Item2, true)).ToList();

        if (due.Count == 0 && news.Count == 0) return null;
        bool pickNew = news.Count > 0 && (due.Count == 0 || _rng.NextDouble() < NewItemRatio);
        return pickNew ? news[0] : due[0];
    }

    public List<ReviewItem> BuildSession(int count) => BuildSession(CurrentLesson, count);

    public List<ReviewItem> BuildSession(int lesson, int count)
    {
        long now = Now;
        var due  = _progress.Values.Where(p => p.Due <= now).OrderBy(p => p.Due)
                             .Select(p => ToReviewItem(p.Key, false)).ToList();
        var news = (IsUnlocked(lesson) ? NewItemsOf(lesson) : new List<(ItemKind, string)>())
                             .Select(t => new ReviewItem(t.Item1, t.Item2, true)).ToList();

        var result = new List<ReviewItem>();
        int di = 0, ni = 0;
        while (result.Count < count && (di < due.Count || ni < news.Count))
        {
            bool pickNew = ni < news.Count && (di >= due.Count || _rng.NextDouble() < NewItemRatio);
            if (pickNew)             result.Add(news[ni++]);
            else if (di < due.Count) result.Add(due[di++]);
        }
        return result;
    }

    // ───────── Thống kê theo bài ─────────
    public LessonStats GetLessonStats(int lesson)
    {
        var s = new LessonStats { Lesson = lesson };
        foreach (var (kind, id) in LessonItems(lesson))
        {
            s.Total++;
            var p = Get(kind, id);
            if      (p == null)                                       s.New++;
            else if (p.Box >= MasteredBox)                            s.Mastered++;
            else if (p.Seen >= 3 && p.Accuracy < 0.5 && p.Box <= 1)   s.Struggling++;
            else                                                     s.Learning++;
        }
        return s;
    }

    // ───────── Helpers ─────────
    private List<(ItemKind, string)> LessonItems(int lesson)
    {
        var list = new List<(ItemKind, string)>();
        var db = JapaneseDB.Instance;
        if (db == null) return list;
        foreach (var v in db.GetByLesson(lesson))         list.Add((ItemKind.Vocab,   v.Id));
        foreach (var g in db.GetGrammarByLesson(lesson))  list.Add((ItemKind.Grammar, g.Id));
        foreach (var r in db.GetReadingsByLesson(lesson)) list.Add((ItemKind.Reading, r.Id));
        return list;
    }

    private List<(ItemKind, string)> NewItemsOf(int lesson)
        => LessonItems(lesson).Where(t => !_progress.ContainsKey(Key(t.Item1, t.Item2))).ToList();

    private static ReviewItem ToReviewItem(string key, bool isNew)
    {
        int i = key.IndexOf(':');
        string ks = key.Substring(0, i), id = key.Substring(i + 1);
        ItemKind k = ks == "vocab" ? ItemKind.Vocab : ks == "grammar" ? ItemKind.Grammar : ItemKind.Reading;
        return new ReviewItem(k, id, isNew);
    }

    // ───────── Lưu / nạp ─────────
    private void Save()
    {
        var blob = new SaveBlob { Unlocked = _unlocked, Progress = _progress };
        using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        f?.StoreString(JsonSerializer.Serialize(blob));
    }

    private void Load()
    {
        if (!FileAccess.FileExists(SavePath)) return;
        using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        var blob = JsonSerializer.Deserialize<SaveBlob>(f.GetAsText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (blob == null) return;
        _unlocked = Math.Max(1, blob.Unlocked);
        if (blob.Progress != null) foreach (var kv in blob.Progress) _progress[kv.Key] = kv.Value;
        GD.Print($"[Learn] Loaded {_progress.Count} mục — mở khóa tới Bài {_unlocked}.");
    }

    private class SaveBlob
    {
        [JsonPropertyName("unlocked")] public int Unlocked { get; set; } = 1;
        [JsonPropertyName("progress")] public Dictionary<string, ItemProgress> Progress { get; set; } = new();
    }
}
