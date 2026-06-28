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
/// • Đồng bộ tiến trình từ server qua API.
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

    private readonly Dictionary<string, ItemProgress> _progress = new();
    private readonly Random _rng = new();
    private int _unlocked = 1;

    [Signal] public delegate void ItemMasteredEventHandler(string kind, string id);
    [Signal] public delegate void LessonUnlockedEventHandler(int lesson);

    public override void _Ready()
    {
        Instance = this;
    }

    public async System.Threading.Tasks.Task SyncAsync()
    {
        if (string.IsNullOrEmpty(ApiClient.Instance.AccessToken)) return;

        var res = await ApiClient.Instance.GetAsync("/api/learning");
        if (res.IsSuccessStatusCode)
        {
            var data = await ApiClient.Instance.ReadAsAsync<AccountManager.ApiResponse<List<LearningProgressDto>>>(res);
            if (data?.Data != null)
            {
                _progress.Clear();
                foreach (var dto in data.Data)
                {
                    string k = (dto.ItemType ?? "").ToLowerInvariant();
                    string kindStr = k == "vocab" ? "vocab" : k == "grammar" ? "grammar" : "reading";
                    string key = kindStr + ":" + dto.ItemId;
                    _progress[key] = new ItemProgress
                    {
                        Key = key,
                        Box = dto.LeitnerBox,
                        NextReviewAt = dto.NextReviewAt,
                        Seen = dto.CorrectCount + dto.WrongCount,
                        Correct = dto.CorrectCount,
                        Wrong = dto.WrongCount
                    };
                }
                RecalculateUnlock();
            }
        }
    }

    private void RecalculateUnlock()
    {
        _unlocked = 1;
        CheckUnlock();
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
        
        // Sync to server (fire and forget)
        _ = ApiClient.Instance.PostAsync("/api/learning/answer", new 
        { 
            ItemType = KindStr(kind),
            ItemId = id,
            IsCorrect = correct,
            IsGuess = guess
        });
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
        var due  = _progress.Values.Where(p => p.NextReviewAt != null && ((DateTimeOffset)p.NextReviewAt).ToUnixTimeSeconds() <= now).OrderBy(p => p.NextReviewAt).ToList();
        
        // Fallback for missing NextReviewAt due to optimistic update
        var dueFallback = _progress.Values.Where(p => p.NextReviewAt == null && p.Due <= now).OrderBy(p => p.Due).ToList();
        if (due.Count == 0 && dueFallback.Count > 0) due = dueFallback;

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
        var due = _progress.Values.Where(p => (p.NextReviewAt != null && ((DateTimeOffset)p.NextReviewAt).ToUnixTimeSeconds() <= now) || (p.NextReviewAt == null && p.Due <= now)).OrderBy(p => p.NextReviewAt ?? DateTimeOffset.FromUnixTimeSeconds(p.Due))
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
        var due  = _progress.Values.Where(p => (p.NextReviewAt != null && ((DateTimeOffset)p.NextReviewAt).ToUnixTimeSeconds() <= now) || (p.NextReviewAt == null && p.Due <= now)).OrderBy(p => p.NextReviewAt ?? DateTimeOffset.FromUnixTimeSeconds(p.Due))
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

    private class LearningProgressDto
    {
        public string ItemType { get; set; }
        public string ItemId { get; set; }          // khớp LearningProgressResponse.ItemId
        public int LeitnerBox { get; set; }
        public DateTime? NextReviewAt { get; set; }
        public int CorrectCount { get; set; }
        public int WrongCount { get; set; }
    }
}
