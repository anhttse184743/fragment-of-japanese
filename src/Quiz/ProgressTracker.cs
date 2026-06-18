using System.Collections.Generic;

namespace FragmentOfJapanese.Quiz;

/// <summary>
/// Theo dõi tiến trình học của từng từ vựng.
/// Cơ bản: đếm đúng/sai. Có thể mở rộng thành SM-2 algorithm sau.
/// </summary>
public class ProgressTracker
{
    private readonly Dictionary<string, WordProgress> _data = new();

    public void Record(string vocabId, bool correct)
    {
        if (!_data.TryGetValue(vocabId, out var prog))
        {
            prog = new WordProgress { VocabId = vocabId };
            _data[vocabId] = prog;
        }

        if (correct) prog.CorrectCount++;
        else         prog.WrongCount++;

        prog.LastSeen = System.DateTime.UtcNow;
    }

    public WordProgress Get(string vocabId)
        => _data.TryGetValue(vocabId, out var p) ? p : new WordProgress { VocabId = vocabId };

    /// <summary>Coi là đã học khi trả lời đúng >= 3 lần liên tiếp</summary>
    public bool IsLearned(string vocabId) => Get(vocabId).CorrectCount >= 3;

    /// <summary>Lấy danh sách từ cần ôn lại (sai nhiều hơn đúng)</summary>
    public List<string> GetWeakVocabs()
    {
        var result = new List<string>();
        foreach (var kv in _data)
            if (kv.Value.WrongCount > kv.Value.CorrectCount)
                result.Add(kv.Key);
        return result;
    }
}

public class WordProgress
{
    public string          VocabId      { get; set; } = "";
    public int             CorrectCount { get; set; } = 0;
    public int             WrongCount   { get; set; } = 0;
    public System.DateTime LastSeen     { get; set; } = System.DateTime.UtcNow;
}
