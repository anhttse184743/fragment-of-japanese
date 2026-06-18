using Godot;
using FragmentOfJapanese.Autoloads;

namespace FragmentOfJapanese.Entities.Npc;

/// <summary>
/// NPC dạy từ vựng. Tương tác → mở QuizUI học từ mới.
/// </summary>
public partial class SenseiNpc : Npc
{
    /// <summary>Tags để lọc từ vựng phù hợp với NPC này</summary>
    [Export] public string[] VocabTags { get; set; } = { "noun" };

    public override void Interact()
    {
        var vocab = VocabTags.Length > 0
            ? JapaneseDB.Instance.GetByTag(VocabTags[0])
            : new System.Collections.Generic.List<Core.VocabularyEntry>(JapaneseDB.Instance.VocabN5);

        GD.Print($"[SenseiNpc] Teaching {vocab.Count} words.");
        // TODO: Emit signal để QuizUI mở với vocab list này
    }
}
