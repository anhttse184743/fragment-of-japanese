using System;
using System.Collections.Generic;
using Godot;
using FragmentOfJapanese.Ui;
using FragmentOfJapanese.World;

namespace FragmentOfJapanese.Entities.Npc;

/// <summary>
/// NPC nền: thân tĩnh (chặn người chơi) + một <see cref="Interactable"/> con làm vùng [E].
/// Bấm [E] trong tầm → mở hội thoại qua <see cref="DialogueUi"/>.
///
/// Nội dung thoại: nếu gán <see cref="Dialogue"/> (Resource, soạn trong Inspector) thì dùng cái đó;
/// nếu để trống thì dùng <see cref="DefaultDialogue"/> (code) — lớp con override để có nội dung riêng.
/// </summary>
public partial class Npc : StaticBody3D
{
    [Export] public string NpcId   { get; set; } = "";
    [Export] public string NpcName { get; set; } = "";

    /// <summary>Hội thoại soạn trong Inspector. Để trống = dùng thoại mặc định trong code.</summary>
    [Export] public DialogueRes Dialogue { get; set; }

    /// <summary>Vùng [E] (Area3D con). Gán trong Inspector.</summary>
    [Export] private Interactable _interactable;

    /// <summary>Ảnh chân dung cho hộp thoại. Để trống = tự lấy frame idle đầu của sprite NPC (node "Visual").</summary>
    [Export] public Texture2D PortraitImage { get; set; }

    public override void _Ready()
    {
        if (_interactable != null)
            _interactable.Interacted += OnInteracted;
    }

    public override void _ExitTree()
    {
        if (_interactable != null)
            _interactable.Interacted -= OnInteracted;
    }

    /// <summary>Bấm [E] trong tầm → mở hội thoại.</summary>
    protected virtual void OnInteracted()
    {
        var node = Dialogue != null ? Convert(Dialogue) : DefaultDialogue();
        // Chân dung: Inspector (res/NPC) ưu tiên; nếu trống → tự lấy frame đầu của sprite NPC.
        node.Portrait ??= PortraitImage ?? AutoPortraitFromSprite();
        DialogueUi.Instance?.Start(node);
    }

    /// <summary>Tự tạo chân dung từ frame đầu của AnimatedSprite3D con (ưu tiên node "Visual").</summary>
    private Texture2D AutoPortraitFromSprite()
    {
        AnimatedSprite3D spr = GetNodeOrNull<AnimatedSprite3D>("Visual");
        if (spr == null)
            foreach (var ch in GetChildren())
                if (ch is AnimatedSprite3D a) { spr = a; break; }

        var frames = spr?.SpriteFrames;
        if (frames == null) return null;

        string anim = spr.Animation.ToString();
        if (string.IsNullOrEmpty(anim) || !frames.HasAnimation(anim))
        {
            var names = frames.GetAnimationNames();
            if (names.Length == 0) return null;
            anim = names[0];
        }
        return frames.GetFrameCount(anim) > 0 ? frames.GetFrameTexture(anim, 0) : null;
    }

    /// <summary>Thoại mặc định khi KHÔNG gán <see cref="Dialogue"/>. Lớp con override để thêm nội dung.</summary>
    protected virtual DialogueNode DefaultDialogue() => new()
    {
        Speaker = NpcName,
        Lines   = new[] { new DialogueLine("...") },
    };

    /// <summary>Map enum hành động (Inspector) → code. Lớp con override để thêm hành động riêng.</summary>
    protected virtual Action ResolveAction(DialogueAction action) => action switch
    {
        DialogueAction.OpenShop     => () => ShopUi.Instance?.Open(),
        DialogueAction.OpenKanaDraw => () => KanaDrawUi.Open(),
        _                           => null,
    };

    /// <summary>Đổi <see cref="DialogueRes"/> (Inspector) → <see cref="DialogueNode"/> (runtime).</summary>
    private DialogueNode Convert(DialogueRes res)
    {
        var choices = new List<DialogueChoice>();
        foreach (var c in res.Choices ?? Array.Empty<DialogueChoiceRes>())
        {
            if (c == null) continue;
            Action act = ResolveAction(c.Action);

            if (c.ReplyLines is { Length: > 0 })
                // có lời đáp → rẽ nhánh hiện lời đáp, hết lời đáp thì chạy act (nếu có)
                choices.Add(new DialogueChoice
                {
                    Label = c.Label,
                    Next  = new DialogueNode { Speaker = res.Speaker, Lines = ToLines(c.ReplyLines), OnEnd = act },
                });
            else
                // không lời đáp → chọn xong chạy act rồi đóng (act null = chỉ đóng)
                choices.Add(new DialogueChoice { Label = c.Label, Action = act });
        }

        return new DialogueNode
        {
            Speaker  = string.IsNullOrEmpty(res.Speaker) ? NpcName : res.Speaker,
            Lines    = ToLines(res.Lines),
            Choices  = choices.ToArray(),
            Portrait = res.Portrait,   // rỗng → OnInteracted tự điền (PortraitImage / sprite NPC)
        };
    }

    /// <summary>Đổi mảng dòng song ngữ (Inspector) → runtime <see cref="DialogueLine"/>[].</summary>
    private static DialogueLine[] ToLines(DialogueLineRes[] src)
    {
        if (src == null) return Array.Empty<DialogueLine>();
        var list = new List<DialogueLine>(src.Length);
        foreach (var l in src)
            if (l != null) list.Add(new DialogueLine { Ja = l.Japanese, Vi = l.Vietnamese });
        return list.ToArray();
    }
}
