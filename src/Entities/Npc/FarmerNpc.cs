using FragmentOfJapanese.Ui;

namespace FragmentOfJapanese.Entities.Npc;

/// <summary>
/// Nông dân.
/// </summary>
public partial class FarmerNpc : Npc
{
	protected override DialogueNode DefaultDialogue() => new()
	{
		Speaker = string.IsNullOrEmpty(NpcName) ? "Nông dân" : NpcName,
		Lines = new[]
		{
			new DialogueLine("おはようございます！きょうも はたけしごとを がんばります。", "Chào bạn! Hôm nay tôi lại tiếp tục chăm chỉ làm ruộng đây."),
			new DialogueLine("あなたも ぼうけん がんばってくださいね！", "Bạn cũng hãy cố gắng với chuyến phiêu lưu của mình nhé!"),
		},
		Choices = new[]
		{
			new DialogueChoice { Label = "Chào tạm biệt" }
		}
	};
}
