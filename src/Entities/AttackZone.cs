using Godot;
using FragmentOfJapanese.Core;

namespace FragmentOfJapanese.Entities;

/// <summary>
/// Vùng tấn công cận chiến phía TRƯỚC nhân vật (2.5D). Tự xoay theo hướng di chuyển của chủ.
/// Gọi <see cref="Attack"/> để gây sát thương cho mọi <see cref="IDamageable"/> trong vùng (trừ chủ).
///
/// Có vùng ĐỎ MỜ để debug (bật/tắt <see cref="ShowDebug"/>) — hiện khi chạy game (F6).
/// Mọi thông số (sát thương, tầm, rộng...) chỉnh được trong Inspector.
/// </summary>
public partial class AttackZone : Node3D
{
    [Export] public CharacterBody3D OwnerBody;          // chủ sở hữu — không tự đánh trúng mình
    [Export] public int   Damage        = 10;
    [Export] public float Range         = 1.6f;     // chiều sâu vùng về phía trước (m)
    [Export] public float Width         = 1.4f;
    [Export] public float Height        = 1.6f;
    [Export] public float ForwardOffset = 0.4f;     // đẩy vùng ra trước, tránh ôm vào thân
    [Export(PropertyHint.Layers3DPhysics)] public uint TargetMask = 1;  // layer của mục tiêu
    [Export] public bool  ShowDebug     = true;     // vùng đỏ mờ để quan sát
    [Export] public float TurnThreshold = 0.15f;    // tốc độ tối thiểu để xoay hướng

    private Area3D _area;
    private float  _facingYaw;

    public override void _Ready()
    {
        var size   = new Vector3(Width, Height, Range);
        var center = new Vector3(0f, Height * 0.5f, ForwardOffset + Range * 0.5f);

        _area = new Area3D { CollisionLayer = 0, CollisionMask = TargetMask, Monitoring = true, Monitorable = false };
        _area.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = center });
        AddChild(_area);

        if (ShowDebug)
        {
            var mat = new StandardMaterial3D
            {
                AlbedoColor  = new Color(1f, 0f, 0f, 0.22f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode  = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode     = BaseMaterial3D.CullModeEnum.Disabled,
            };
            AddChild(new MeshInstance3D
            {
                Mesh             = new BoxMesh { Size = size },
                Position         = center,
                MaterialOverride = mat,
            });
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (OwnerBody == null) return;
        var v = OwnerBody.Velocity;
        if (new Vector2(v.X, v.Z).Length() > TurnThreshold)
        {
            _facingYaw = Mathf.Atan2(v.X, v.Z);
            Rotation = new Vector3(0f, _facingYaw, 0f);
        }
    }

    /// <summary>Gây sát thương cho mọi mục tiêu trong vùng (trừ chủ). Gọi khi nhân vật vung đòn.</summary>
    public void Attack()
    {
        if (_area == null) return;
        foreach (var body in _area.GetOverlappingBodies())
        {
            if (body == OwnerBody) continue;
            if (body is IDamageable d) d.TakeDamage(Damage);
        }
    }
}
