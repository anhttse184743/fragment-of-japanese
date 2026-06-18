using System.Collections.Generic;

namespace FragmentOfJapanese.Core;

public enum QuizType
{
    KanaToMeaning,   // みず → ? (chọn nghĩa)
    MeaningToKana,   // nước → ? (chọn kana)
    KanjiToKana,     // 水 → ?   (chọn kana)
    KanaToRomaji,    // みず → ? (chọn romaji)
}

public class QuizQuestion
{
    public string              Prompt        { get; set; } = "";
    public string              CorrectAnswer { get; set; } = "";
    public List<string>        Choices       { get; set; } = new(); // 4 đáp án đã shuffle
    public QuizType            Type          { get; set; } = QuizType.KanaToMeaning;
    public VocabularyEntry     Source        { get; set; }
}
