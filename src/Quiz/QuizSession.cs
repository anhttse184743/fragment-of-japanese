using System;
using System.Collections.Generic;
using FragmentOfJapanese.Core;

namespace FragmentOfJapanese.Quiz;

/// <summary>
/// Một phiên quiz: bao gồm danh sách câu hỏi, theo dõi tiến trình.
/// </summary>
public class QuizSession
{
    public string Topic         { get; set; } = "";
    public int    TotalQuestions { get; set; } = 5;
    public int    CurrentIndex  { get; private set; } = 0;
    public int    CorrectCount  { get; private set; } = 0;

    private readonly List<QuizQuestion> _questions = new();
    private readonly QuizEngine         _engine    = new();

    public QuizQuestion Current    => CurrentIndex < _questions.Count ? _questions[CurrentIndex] : null;
    public bool          IsFinished => CurrentIndex >= _questions.Count;
    public float         Accuracy   => TotalQuestions > 0 ? (float)CorrectCount / TotalQuestions : 0f;

    public void Build(IReadOnlyList<VocabularyEntry> pool, QuizType type = QuizType.KanaToMeaning)
    {
        _questions.Clear();
        CurrentIndex = 0;
        CorrectCount = 0;

        var rng      = new Random();
        var shuffled = new List<VocabularyEntry>(pool);
        shuffled.Sort((_, _) => rng.Next(-1, 2));

        int count = Math.Min(TotalQuestions, shuffled.Count);
        for (int i = 0; i < count; i++)
            _questions.Add(_engine.Generate(shuffled[i], pool, type));
    }

    /// <returns>True nếu đáp án đúng</returns>
    public bool Answer(string answer)
    {
        if (IsFinished || Current == null) return false;
        bool correct = Current.CorrectAnswer == answer;
        if (correct) CorrectCount++;
        CurrentIndex++;
        return correct;
    }
}
