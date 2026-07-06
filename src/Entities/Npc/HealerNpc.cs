using FragmentOfJapanese.Ui;
using FragmentOfJapanese.Entities.Player;

namespace FragmentOfJapanese.Entities.Npc;

/// <summary>
/// Nữ tu sĩ (Healer). Hỗ trợ hồi phục thể lực cho người chơi.
/// </summary>
public partial class HealerNpc : Npc
{
	protected override DialogueNode DefaultDialogue() => new()
	{
		Speaker = string.IsNullOrEmpty(NpcName) ? "Tu sĩ" : NpcName,
		Lines = new[]
		{
			new DialogueLine("けがは ありませんか？かみのごかごが ありますように。", "Bạn có bị thương không? Nguyện cầu thần linh ban phước lành cho bạn."),
		},
		Choices = new[]
		{
			new DialogueChoice { Label = "Chào tạm biệt" }
		}
	};
}
