using System.Text.Json.Serialization;

namespace FragmentOfJapanese.Learning;

/// <summary>Loại mục học được theo dõi.</summary>
public enum ItemKind { Vocab, Grammar, Reading }

/// <summary>
/// Tiến độ học của MỘT mục (1 từ / 1 điểm ngữ pháp / 1 đoạn đọc).
/// Box = bậc Leitner (0..5): trả lời đúng (có suy nghĩ) → lên bậc, lâu mới gặp lại;
/// sai → về bậc 0, gặp lại ngay. Guesses = số lần nghi "chọn bừa" (trả lời quá nhanh).
/// </summary>
public class ItemProgress
{
    [JsonPropertyName("key")]     public string Key      { get; set; } = "";   // "vocab:l1_watashi"
    [JsonPropertyName("seen")]    public int    Seen     { get; set; }
    [JsonPropertyName("correct")] public int    Correct  { get; set; }
    [JsonPropertyName("wrong")]   public int    Wrong    { get; set; }
    [JsonPropertyName("streak")]  public int    Streak   { get; set; }          // đúng liên tiếp (có suy nghĩ)
    [JsonPropertyName("box")]     public int    Box      { get; set; }          // bậc Leitner 0..5
    [JsonPropertyName("guesses")] public int    Guesses  { get; set; }          // số lần nghi chọn bừa
    [JsonPropertyName("last")]    public long   LastSeen { get; set; }          // unix giây
    [JsonPropertyName("due")]     public long   Due      { get; set; }          // unix giây — khi nào nên ôn lại
    [JsonIgnore]                  public System.DateTime? NextReviewAt { get; set; }

    [JsonIgnore] public double Accuracy => Seen > 0 ? (double)Correct / Seen : 0.0;
}

/// <summary>Một mục bộ lịch chọn ra để học/ôn (IsNew = bài mới chưa từng gặp).</summary>
public class ReviewItem
{
    public ItemKind Kind;
    public string   Id;
    public bool     IsNew;
    public ReviewItem(ItemKind kind, string id, bool isNew) { Kind = kind; Id = id; IsNew = isNew; }
}

/// <summary>Thống kê gộp của một bài.</summary>
public class LessonStats
{
    public int Lesson, Total, New, Learning, Struggling, Mastered;
}
