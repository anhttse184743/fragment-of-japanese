using System;
using System.Collections.Generic;
using System.Linq;

namespace FragmentOfJapanese.Core;

public class QuizEngine
{
    private readonly Random _rng = new();

    public QuizQuestion Generate(
        VocabularyEntry target,
        IReadOnlyList<VocabularyEntry> pool,
        QuizType type = QuizType.KanaToMeaning)
    {
        var q = new QuizQuestion { Type = type, Source = target };

        switch (type)
        {
            case QuizType.KanaToMeaning:
                q.Prompt        = target.Kana;
                q.CorrectAnswer = target.MeaningVi;
                q.Choices       = BuildChoices(target.MeaningVi, pool.Select(v => v.MeaningVi).ToList());
                break;

            case QuizType.MeaningToKana:
                q.Prompt        = target.MeaningVi;
                q.CorrectAnswer = target.Kana;
                q.Choices       = BuildChoices(target.Kana, pool.Select(v => v.Kana).ToList());
                break;

            case QuizType.KanjiToKana:
                q.Prompt        = target.Kanji;
                q.CorrectAnswer = target.Kana;
                q.Choices       = BuildChoices(target.Kana, pool.Select(v => v.Kana).ToList());
                break;

            case QuizType.KanaToRomaji:
                q.Prompt        = target.Kana;
                q.CorrectAnswer = target.Romaji;
                q.Choices       = BuildChoices(target.Romaji, pool.Select(v => v.Romaji).ToList());
                break;
        }

        return q;
    }

    private List<string> BuildChoices(string correct, List<string> pool)
    {
        var wrong = pool
            .Where(x => x != correct && !string.IsNullOrEmpty(x))
            .OrderBy(_ => _rng.Next())
            .Take(3)
            .ToList();

        var choices = new List<string> { correct };
        choices.AddRange(wrong);
        return choices.OrderBy(_ => _rng.Next()).ToList();
    }
}
