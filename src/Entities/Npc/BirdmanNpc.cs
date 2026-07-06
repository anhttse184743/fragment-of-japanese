using Godot;
using FragmentOfJapanese.Core;
using FragmentOfJapanese.Ui;

namespace FragmentOfJapanese.Entities.Npc;

public partial class BirdmanNpc : Npc
{
    public override void _Ready()
    {
        base._Ready();
        NpcId = "birdman";
        NpcName = "Người Chim";
    }

    protected override DialogueNode DefaultDialogue() => new DialogueNode
    {
        Speaker = NpcName,
        Lines = new[]
        {
            new DialogueLine { Vi = "Chào bạn! Cổ họng bạn hôm nay thế nào? Có muốn luyện phát âm một chút không?", Ja = "こんにちは！発音の練習をしませんか？" }
        },
        Choices = new[]
        {
            new DialogueChoice
            {
                Label = "Bắt đầu luyện tập",
                Action = () => 
                {
                    var voiceScene = ResourceLoader.Load<PackedScene>("res://scenes/ui/VoiceMinigameUi.tscn");
                    if (voiceScene != null)
                    {
                        var ui = voiceScene.Instantiate<VoiceMinigameUi>();
                        ui.TargetNpc = this;
                        GetTree().Root.AddChild(ui);
                    }
                }
            },
            new DialogueChoice
            {
                Label = "Để khi khác",
                Action = null // Close dialogue
            }
        }
    };
}
