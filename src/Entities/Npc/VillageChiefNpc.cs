using FragmentOfJapanese.Ui;

namespace FragmentOfJapanese.Entities.Npc;

/// <summary>
/// Trưởng làng. Mở cửa hàng (mua đồ) hoặc màn đổi chiến lợi phẩm lấy Vàng.
/// </summary>
public partial class VillageChiefNpc : Npc
{
	protected override DialogueNode DefaultDialogue() => new()
	{
		Speaker = string.IsNullOrEmpty(NpcName) ? "Trưởng làng" : NpcName,
		Lines = new[]
		{
			new DialogueLine("ようこそ、たびびとさん！わたしは このむらの そんちょうです。", "Chào lữ khách! Ta là trưởng làng nơi đây."),
			new DialogueLine("なにか てつだいましょうか？", "Ngươi cần ta giúp gì không?"),
		},
		Choices = new[]
		{
			new DialogueChoice
			{
				Label = "Hỏi bí quyết học tập",
				Next  = new DialogueNode
				{
					Speaker = NpcName,
					Lines   = new[] {
						new DialogueLine("にほんごの べんきょうは まいにち すこしずつ やるのが だいじです。", "Học tiếng Nhật quan trọng nhất là mỗi ngày học một chút."),
						new DialogueLine("まものと たたかいながら、ことばの ちからを みにつけてください。", "Hãy vừa chiến đấu với quái vật, vừa rèn luyện sức mạnh của ngôn từ nhé.")
					},
				},
			},
			new DialogueChoice
			{
				Label = "Hỏi chuyện trong làng",
				Next  = new DialogueNode
				{
					Speaker = NpcName,
					Lines   = new[] { new DialogueLine("さいきん まものが おおいです。そとでは きを つけてね。", "Dạo này quái vật nhiều hơn. Ra ngoài hãy cẩn thận nhé.") },
				},
			},
			new DialogueChoice { Label = "Rời đi" },
		},
	};
}
