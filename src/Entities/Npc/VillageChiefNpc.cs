using FragmentOfJapanese.Ui;

namespace FragmentOfJapanese.Entities.Npc;

/// <summary>
/// Trưởng làng. Dùng hành động <see cref="DialogueAction.OpenShop"/> (mở cửa hàng) — đã có ở lớp base.
/// Có thể soạn thoại trong Inspector; nếu để trống dùng thoại mặc định bên dưới.
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
            new DialogueChoice { Label = "Xem cửa hàng", Action = () => ShopUi.Instance?.Open() },
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
