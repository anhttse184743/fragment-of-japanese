using System.Text.Json.Serialization;

namespace FragmentOfJapanese.Core;

/// <summary>Metadata một bài học (topic) theo giáo trình Minna no Nihongo.</summary>
public class LessonInfo
{
    [JsonPropertyName("id")]         public string Id        { get; set; } = "";
    [JsonPropertyName("lesson")]     public int    Lesson    { get; set; }
    [JsonPropertyName("name_vi")]    public string NameVi    { get; set; } = "";
    [JsonPropertyName("grammar_vi")] public string GrammarVi { get; set; } = "";
}
