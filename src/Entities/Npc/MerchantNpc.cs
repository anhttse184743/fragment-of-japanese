using FragmentOfJapanese.Ui;

namespace FragmentOfJapanese.Entities.Npc;

/// <summary>
/// NPC Thương nhân: cho phép đổi Cuộn Từ Vựng lấy Chìa khóa Bạc/Vàng.
/// </summary>
public partial class MerchantNpc : Npc
{
    public override void _Ready()
    {
        base._Ready();
        if (string.IsNullOrEmpty(NpcName)) NpcName = "Thương nhân";
    }

    protected override DialogueNode DefaultDialogue()
    {
        var openTrade = ResolveAction(DialogueAction.OpenScrollTrade);

        return new DialogueNode
        {
            Speaker = NpcName,
            Lines   = new[]
            {
                new DialogueLine("Ta là thương nhân của làng. Bạn muốn mua sắm, trao đổi vật phẩm hay bán chiến lợi phẩm?", "Tôi có thể giúp bạn quy đổi tài nguyên.")
            },
            Choices = new[]
            {
                new DialogueChoice { Label = "Xem cửa hàng", Action = () => ShopUi.Instance?.Open() },
                new DialogueChoice { Label = "Bán chiến lợi phẩm", Action = () => TradeUi.Open() },
                new DialogueChoice
                {
                    Label = "Đổi chìa khóa",
                    Next  = new DialogueNode
                    {
                        Speaker = NpcName,
                        Lines   = new[] { new DialogueLine("Hãy xem ngươi có gì nào...", "Xin mời xem!") },
                        OnEnd   = openTrade,
                    },
                },
                new DialogueChoice { Label = "Để sau" },
            },
        };
    }
}
