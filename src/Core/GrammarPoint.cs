using System.Text.Json.Serialization;

namespace FragmentOfJapanese.Core;

/// <summary>Một điểm ngữ pháp của một bài Minna (mẫu câu + giải thích VI + ví dụ Việt–Nhật).</summary>
public class GrammarPoint
{
    [JsonPropertyName("id")]             public string Id            { get; set; } = "";
    [JsonPropertyName("lesson")]         public int    Lesson        { get; set; }
    [JsonPropertyName("pattern")]        public string Pattern       { get; set; } = "";  // mẫu câu, vd 〜は〜です
    [JsonPropertyName("meaning_vi")]     public string MeaningVi     { get; set; } = "";  // nghĩa ngắn
    [JsonPropertyName("explanation_vi")] public string ExplanationVi { get; set; } = "";  // giải thích cách dùng
    [JsonPropertyName("example_ja")]     public string ExampleJa     { get; set; } = "";  // ví dụ tiếng Nhật (hiragana)
    [JsonPropertyName("example_vi")]     public string ExampleVi     { get; set; } = "";  // dịch ví dụ
}
