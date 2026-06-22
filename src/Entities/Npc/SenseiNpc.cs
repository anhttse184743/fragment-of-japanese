using System;
using Godot;
using FragmentOfJapanese.Autoloads;
using FragmentOfJapanese.Ui;

namespace FragmentOfJapanese.Entities.Npc;

/// <summary>
/// NPC dạy từ vựng. Thêm hành động <see cref="DialogueAction.OpenLearning"/> (mở màn học từ).
/// Có thể soạn thoại trong Inspector (dùng Action = OpenLearning); nếu để trống dùng thoại mặc định.
/// </summary>
public partial class SenseiNpc : Npc
{
    /// <summary>Tags để lọc từ vựng phù hợp với NPC này.</summary>
    [Export] public string[] VocabTags { get; set; } = { "noun" };

    protected override Action ResolveAction(DialogueAction action) => action switch
    {
        DialogueAction.OpenLearning => OpenLearning,
        _                           => base.ResolveAction(action),
    };

    protected override DialogueNode DefaultDialogue() => new()
    {
        Speaker = string.IsNullOrEmpty(NpcName) ? "Sensei" : NpcName,
        Lines = new[]
        {
            new DialogueLine("こんにちは！わたしは ことばの せんせいです。", "Xin chào! Ta là thầy dạy chữ."),
            new DialogueLine("きょうは なにを しますか？", "Hôm nay con muốn làm gì?"),
        },
        Choices = new[]
        {
            new DialogueChoice { Label = "Học từ mới", Action = OpenLearning },
            new DialogueChoice
            {
                Label = "Nghe lời khuyên",
                Next  = new DialogueNode
                {
                    Speaker = NpcName,
                    Lines   = new[] { new DialogueLine("まいにち すこしずつ れんしゅうしましょう。がんばってね！", "Mỗi ngày luyện một chút nhé. Cố lên!") },
                },
            },
            new DialogueChoice { Label = "Tạm biệt" },   // không Next/Action → đóng thoại
        },
    };

    private void OpenLearning()
    {
        var vocab = VocabTags.Length > 0
            ? JapaneseDB.Instance.GetByTag(VocabTags[0])
            : new System.Collections.Generic.List<Core.VocabularyEntry>(JapaneseDB.Instance.VocabN5);

        GD.Print($"[SenseiNpc] Mở màn học từ: {vocab.Count} từ.");
        // TODO: mở QuizUi / màn học từ với danh sách vocab này.
    }
}
