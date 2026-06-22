using Godot;

namespace FragmentOfJapanese.Entities.Npc;

/// <summary>
/// NPC tĩnh quay mặt MỘT hướng cố định trong thế giới; tùy góc camera mà hiện idle 4 hướng
/// (đi vòng quanh NPC sẽ thấy trước / ngang / sau). Sprite vẫn billboard (phẳng về camera) —
/// script chỉ đổi animation idle_{down|right|up|left} cho khớp góc nhìn.
/// Quy ước hướng giống CharacterAnimator (player/quái) để dùng chung kiểu sheet.
/// </summary>
public partial class NpcDirectionalIdle : Node
{
    [Export] private AnimatedSprite3D _sprite;
    [Export] private Camera3D         _camera;     // để trống = camera đang active

    /// <summary>Hướng mặt NPC trong thế giới (độ). Xoay nếu NPC quay nhầm hướng (vd 180 = quay ngược lại).</summary>
    [Export(PropertyHint.Range, "0,360,5")] public float FacingDeg { get; set; }

    private static readonly string[] Dir4 = { "down", "right", "up", "left" };
    private string _current = "";

    public override void _Ready() => _camera ??= GetViewport()?.GetCamera3D();

    public override void _Process(double delta)
    {
        if (_sprite?.SpriteFrames == null) return;
        _camera ??= GetViewport()?.GetCamera3D();

        float camYaw = _camera != null ? _camera.GlobalRotation.Y : 0f;
        float rel    = Mathf.Wrap(Mathf.DegToRad(FacingDeg) - camYaw, 0f, Mathf.Tau);
        int   i      = Mathf.RoundToInt(rel / (Mathf.Tau / 4f)) % 4;

        string anim = $"idle_{Dir4[i]}";
        if (anim == _current || !_sprite.SpriteFrames.HasAnimation(anim)) return;
        _current = anim;
        _sprite.Play(anim);
    }
}
