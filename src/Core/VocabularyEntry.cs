using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FragmentOfJapanese.Core;

public class VocabularyEntry
{
    [JsonPropertyName("id")]         public string Id         { get; set; } = "";
    [JsonPropertyName("kanji")]      public string Kanji      { get; set; } = "";
    [JsonPropertyName("kana")]       public string Kana       { get; set; } = "";
    [JsonPropertyName("romaji")]     public string Romaji     { get; set; } = "";
    [JsonPropertyName("meaning_vi")] public string MeaningVi  { get; set; } = "";
    [JsonPropertyName("meaning_en")] public string MeaningEn  { get; set; } = "";
    [JsonPropertyName("example")]    public string Example    { get; set; } = "";
    [JsonPropertyName("example_vi")] public string ExampleVi  { get; set; } = "";
    [JsonPropertyName("jlpt")]       public string Jlpt       { get; set; } = "N5";
    [JsonPropertyName("lesson")]     public int    Lesson     { get; set; } = 0;
    [JsonPropertyName("tags")]       public List<string> Tags { get; set; } = new();
}
