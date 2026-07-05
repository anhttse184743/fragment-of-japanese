using Godot;
using System;
using FragmentOfJapanese.Autoloads;
using FragmentOfJapanese.Ui;

namespace FragmentOfJapanese.Entities.Npc;

/// <summary>
/// NPC Người Cây: Cho phép mở bảng chữ cái Hiragana để nghe, và có các option khác.
/// </summary>
public partial class TreantNpc : Npc
{
    protected override DialogueNode DefaultDialogue() => new()
    {
        Speaker = string.IsNullOrEmpty(NpcName) ? "Người Cây" : NpcName,
        Lines = new[]
        {
            new DialogueLine("この森へようこそ...", "Chào mừng cậu đã đến với khu rừng này..."),
            new DialogueLine("聞くことはとても大切なスキルです。耳を鍛えて、世界を感じてください...", "Nghe là một kỹ năng vô cùng quan trọng. Hãy luyện tập đôi tai của cậu để cảm nhận thế giới..."),
        },
        Choices = new[]
        {
            new DialogueChoice 
            { 
                Label = "Luyện nghe bảng chữ cái", 
                Action = () => HiraganaChartUi.Instance?.Open() 
            },
            new DialogueChoice 
            { 
                Label = "Thử thách Minigame", 
                Action = () => MinigameUi.Instance?.Open() 
            },
            new DialogueChoice { Label = "Tạm biệt" },
        },
    };
}
