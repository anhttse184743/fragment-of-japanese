using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FragmentOfJapanese.Core;

/// <summary>
/// Đoạn đọc hiểu ngắn của một bài: văn bản Nhật (hiragana) + dịch Việt,
/// kèm 1 câu hỏi (Việt–Nhật) và 4 đáp án (Answer = chỉ số đáp án đúng, 0-based).
/// </summary>
public class ReadingPassage
{
    [JsonPropertyName("id")]          public string       Id         { get; set; } = "";
    [JsonPropertyName("lesson")]      public int          Lesson     { get; set; }
    [JsonPropertyName("text_ja")]     public string       TextJa     { get; set; } = "";
    [JsonPropertyName("text_vi")]     public string       TextVi     { get; set; } = "";
    [JsonPropertyName("question_ja")] public string       QuestionJa { get; set; } = "";
    [JsonPropertyName("question_vi")] public string       QuestionVi { get; set; } = "";
    [JsonPropertyName("choices")]     public List<string> Choices    { get; set; } = new();
    [JsonPropertyName("answer")]      public int          Answer     { get; set; }
}
