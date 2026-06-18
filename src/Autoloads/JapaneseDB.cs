using Godot;
using System.Collections.Generic;
using System.Text.Json;
using FragmentOfJapanese.Core;

namespace FragmentOfJapanese.Autoloads;

public partial class JapaneseDB : Node
{
	public static JapaneseDB Instance { get; private set; }

	private List<VocabularyEntry> _vocabN5  = new();
	private List<VocabularyEntry> _hiragana = new();
	private List<VocabularyEntry> _katakana = new();
	private List<LessonInfo>      _lessons  = new();

	public IReadOnlyList<VocabularyEntry> VocabN5  => _vocabN5;
	public IReadOnlyList<VocabularyEntry> Hiragana => _hiragana;
	public IReadOnlyList<VocabularyEntry> Katakana => _katakana;
	public IReadOnlyList<LessonInfo>      Lessons  => _lessons;

	public override void _Ready()
	{
		Instance = this;
		LoadAll();
	}

	private void LoadAll()
	{
		_vocabN5  = LoadJson<VocabularyEntry>("res://data/japanese/vocab_n5.json");
		_hiragana = LoadJson<VocabularyEntry>("res://data/japanese/hiragana.json");
		_katakana = LoadJson<VocabularyEntry>("res://data/japanese/katakana.json");
		_lessons  = LoadJson<LessonInfo>("res://data/japanese/lessons.json");
		GD.Print($"[JapaneseDB] Loaded — N5: {_vocabN5.Count}, Hiragana: {_hiragana.Count}, Katakana: {_katakana.Count}, Lessons: {_lessons.Count}");
	}

	private static List<T> LoadJson<T>(string path)
	{
		if (!FileAccess.FileExists(path))
		{
			GD.PushWarning($"[JapaneseDB] File not found: {path}");
			return new();
		}
		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		var json = file.GetAsText();
		return JsonSerializer.Deserialize<List<T>>(json,
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
	}

	public List<VocabularyEntry> GetByTag(string tag)
		=> _vocabN5.FindAll(v => v.Tags.Contains(tag));

	public List<VocabularyEntry> GetByJlpt(string jlpt)
		=> _vocabN5.FindAll(v => v.Jlpt == jlpt);

	/// <summary>Các từ vựng thuộc một bài Minna (topic).</summary>
	public List<VocabularyEntry> GetByLesson(int lesson)
		=> _vocabN5.FindAll(v => v.Lesson == lesson);

	/// <summary>Metadata của một bài (tên, ngữ pháp); null nếu chưa có.</summary>
	public LessonInfo GetLesson(int lesson)
		=> _lessons.Find(l => l.Lesson == lesson);

	/// <summary>True nếu chuỗi chỉ gồm hiragana (đọc được ở giai đoạn mới học hiragana).</summary>
	public static bool IsHiraganaOnly(string kana)
	{
		if (string.IsNullOrEmpty(kana)) return false;
		foreach (var c in kana)
			if (c < '぀' || c > 'ゟ')
				return false;
		return true;
	}

	/// <summary>Từ trong một bài có cách đọc thuần hiragana (loại từ katakana) — dùng cho giai đoạn hiragana.</summary>
	public List<VocabularyEntry> GetHiraganaReadable(int lesson)
		=> _vocabN5.FindAll(v => v.Lesson == lesson && IsHiraganaOnly(v.Kana));
}
