using Godot;

namespace FragmentOfJapanese.Entities.Npc;

/// <summary>
/// NPC tĩnh quay mặt MỘT hướng cố định trong thế giới; tùy VỊ TRÍ camera (đi vòng quanh NPC)
/// mà hiện idle 4 hướng — đứng trước thấy mặt, ra sau thấy lưng, sang bên thấy ngang. Sprite vẫn billboard —
/// script chỉ đổi animation idle_{down|right|up|left} cho khớp góc nhìn.
/// Quy ước hướng giống CharacterAnimator (player/quái) để dùng chung kiểu sheet.
/// </summary>
public partial class NpcDirectionalIdle : Node
{
    [Export] private AnimatedSprite3D _sprite;
    [Export] private Camera3D         _camera;     // Để trống = camera đang active

    /// <summary>Sử dụng góc quay (Rotation) của Node thay vì FacingDeg. Mặc định là true để xoay NPC trong Editor dễ dàng.</summary>
    [Export] public bool UseNodeRotation { get; set; } = true;

    /// <summary>Hướng mặt NPC trong thế giới (độ) nếu UseNodeRotation = false.</summary>
    [Export(PropertyHint.Range, "0,360,5")] public float FacingDeg { get; set; }

    private static readonly string[] Dir4 = { "down", "right", "up", "left" };
    private string _current = "";

    public override void _Ready() => _camera ??= GetViewport()?.GetCamera3D();

    public override void _Process(double delta)
    {
        if (_sprite?.SpriteFrames == null) return;
        _camera ??= GetViewport()?.GetCamera3D();
        if (_camera == null) return;

        var npc = GetParent() as Node3D;

        // Hướng TỪ NPC TỚI CAMERA (theo VỊ TRÍ, mặt phẳng ngang) → đi vòng quanh sẽ đổi mặt/lưng/ngang.
        Vector3 toCam = _camera.GlobalPosition - (npc?.GlobalPosition ?? _sprite.GlobalPosition);
        float angleToCam = Mathf.Atan2(toCam.X, toCam.Z);

        // Hướng mặt NPC: xoay node (mặc định) hoặc FacingDeg.
        float npcYaw = UseNodeRotation && npc != null ? npc.GlobalRotation.Y : Mathf.DegToRad(FacingDeg);

        float rel = Mathf.Wrap(angleToCam - npcYaw, 0f, Mathf.Tau);   // camera đứng ở phía nào so với mặt NPC
        int   i   = Mathf.RoundToInt(rel / (Mathf.Tau / 4f)) % 4;

        string anim = $"idle_{Dir4[i]}";
        if (anim == _current || !_sprite.SpriteFrames.HasAnimation(anim)) return;
        _current = anim;
        _sprite.Play(anim);
    }
}
