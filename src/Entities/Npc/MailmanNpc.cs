using FragmentOfJapanese.Ui;

namespace FragmentOfJapanese.Entities.Npc;

/// <summary>
/// Người đưa thư (Mailman/Guide). Hướng dẫn người chơi về các mini-game và Dungeon.
/// </summary>
public partial class MailmanNpc : Npc
{
    public override void _Ready()
    {
        base._Ready();
        if (string.IsNullOrEmpty(NpcName)) NpcName = "Người đưa thư";
    }

	protected override DialogueNode DefaultDialogue()
	{
		var minigameNghe = new DialogueNode
		{
			Speaker = NpcName,
			Lines = new[] { new DialogueLine("Trong Mini-game Luyện Nghe, bạn sẽ nghe âm thanh phát âm tiếng Nhật và chọn đáp án chính xác. Nếu trả lời đúng, bạn sẽ nhận được Vàng và Cuộn Từ Vựng!", "Luyện nghe giúp cải thiện khả năng nhận diện âm thanh.") },
		};

		var minigameNoi = new DialogueNode
		{
			Speaker = NpcName,
			Lines = new[] { new DialogueLine("Với Mini-game Luyện Nói, bạn cần sử dụng micro để đọc to chữ cái đang hiển thị. Hãy phát âm thật to và rõ ràng để hệ thống ghi nhận nhé!", "Giúp bạn tự tin phát âm tiếng Nhật hơn.") },
		};

		var minigameVe = new DialogueNode
		{
			Speaker = NpcName,
			Lines = new[] { new DialogueLine("Trong Trận chiến Vẽ, bạn sẽ chiến đấu với quái vật bằng cách vẽ đúng các nét chữ Hiragana trong thời gian cho phép. Vẽ đúng sẽ gây sát thương, vẽ sai hoặc hết giờ sẽ bị quái vật đánh lại đó!", "Ghi nhớ cách viết tiếng Nhật rất quan trọng khi chiến đấu.") },
		};

		var dungeon = new DialogueNode
		{
			Speaker = NpcName,
			Lines = new[] { 
                new DialogueLine("Tại Hầm Ngục, bạn sẽ liên tục đối mặt với kẻ thù mạnh mẽ qua nhiều màn chơi. Vượt qua chúng, bạn sẽ nhận được các rương báu. Bạn có thể mở Rương Miễn Phí, hoặc dùng Chìa Khóa Bạc, Vàng để mở Rương Cao Cấp lấy trang bị!", "Nơi đây cực kỳ nguy hiểm nhưng phần thưởng rất xứng đáng.") 
            },
		};

		return new DialogueNode
		{
			Speaker = NpcName,
			Lines = new[]
			{
				new DialogueLine("Chào bạn! Tôi mang theo rất nhiều bức thư hướng dẫn về thế giới này. Bạn muốn tìm hiểu về hoạt động nào?", "Xin chào lữ khách! Bạn cần tôi hướng dẫn gì không?")
			},
			Choices = new[]
			{
				new DialogueChoice { Label = "Luyện Nghe", Next = minigameNghe },
				new DialogueChoice { Label = "Luyện Nói (Phát âm)", Next = minigameNoi },
				new DialogueChoice { Label = "Trận Chiến Vẽ", Next = minigameVe },
				new DialogueChoice { Label = "Hầm ngục (Dungeon)", Next = dungeon },
				new DialogueChoice { Label = "Không cần, cảm ơn" }
			}
		};
	}
}
