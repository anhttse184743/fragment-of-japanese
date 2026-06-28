using FragmentOfJapanese.Ui;

namespace FragmentOfJapanese.Entities.Npc;

/// <summary>
/// NPC "Pháp sư bí ẩn": rủ người chơi luyện viết chữ.
///   - Chọn "Vâng" → đáp 1 câu rồi mở màn luyện viết kana (<see cref="KanaDrawUi"/>) qua <see cref="DialogueAction.OpenKanaDraw"/>.
///   - Chọn "Thôi" → đóng thoại như thường.
/// Soạn thoại riêng thì gán <c>Dialogue</c> (DialogueRes) trong Inspector — sẽ ghi đè thoại mặc định này.
/// </summary>
public partial class MysteriousWizardNpc : Npc
{
    protected override DialogueNode DefaultDialogue()
    {
        var openDraw = ResolveAction(DialogueAction.OpenKanaDraw);
        string name = string.IsNullOrEmpty(NpcName) ? "Pháp sư bí ẩn" : NpcName;

        return new DialogueNode
        {
            Speaker = name,
            Lines   = new[]
            {
                new DialogueLine("ほう… 文字を 書く 練習を したいか？", "Hô… Ngươi muốn luyện viết chữ à?"),
            },
            Choices = new[]
            {
                new DialogueChoice
                {
                    Label = "Vâng, dạy ta viết",
                    Next  = new DialogueNode
                    {
                        Speaker = name,
                        Lines   = new[] { new DialogueLine("よし、筆を 取れ！", "Tốt! Cầm bút lên.") },
                        OnEnd   = openDraw,   // hết lời đáp → mở màn luyện viết
                    },
                },
                new DialogueChoice { Label = "Thôi, để sau" },   // Action rỗng → đóng thoại
            },
        };
    }
}
