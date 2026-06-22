using Godot;

namespace FragmentOfJapanese.World;

/// <summary>
/// Tường sương mù bao quanh World. [Tool] → hiện trong editor, chỉnh Inspector → tự cập nhật.
/// Không cần thêm node con tay — script tự tạo/xóa khi rebuild.
/// </summary>
[Tool]
public partial class WorldBoundary : Node3D
{
    private float _radius       = 220f;
    private float _wallHeight   = 120f;
    private float _fogThickness = 40f;
    private Color _fogColor     = new Color(0.88f, 0.94f, 1.0f, 0.60f);

    [Export] public float Radius
    {
        get => _radius;
        set { _radius = value; Rebuild(); }
    }

    [Export] public float WallHeight
    {
        get => _wallHeight;
        set { _wallHeight = value; Rebuild(); }
    }

    [Export] public float FogThickness
    {
        get => _fogThickness;
        set { _fogThickness = value; Rebuild(); }
    }

    [Export] public Color FogColor
    {
        get => _fogColor;
        set { _fogColor = value; Rebuild(); }
    }

    public override void _Ready() => Rebuild();

    private void Rebuild()
    {
        if (!IsInsideTree()) return;

        // Xóa tường cũ
        foreach (var child in GetChildren())
            child.QueueFree();

        float span  = (_radius + _fogThickness) * 2f;
        float midY  = _wallHeight * 0.5f - 20f;

        var configs = new (Vector3 pos, Vector3 size)[]
        {
            (new Vector3( _radius + _fogThickness * 0.5f, midY, 0), new Vector3(_fogThickness, _wallHeight, span)),
            (new Vector3(-_radius - _fogThickness * 0.5f, midY, 0), new Vector3(_fogThickness, _wallHeight, span)),
            (new Vector3(0, midY,  _radius + _fogThickness * 0.5f), new Vector3(span, _wallHeight, _fogThickness)),
            (new Vector3(0, midY, -_radius - _fogThickness * 0.5f), new Vector3(span, _wallHeight, _fogThickness)),
        };

        var fogMat = new StandardMaterial3D
        {
            AlbedoColor  = _fogColor,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode  = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode     = BaseMaterial3D.CullModeEnum.Disabled,
        };

        foreach (var (pos, size) in configs)
        {
            // Tường va chạm vô hình (chỉ cần khi chơi thật)
            if (!Engine.IsEditorHint())
            {
                var body = new StaticBody3D { Position = pos };
                body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
                AddChild(body);
            }

            // Lớp sương mù — hiện cả trong editor lẫn khi chơi
            AddChild(new MeshInstance3D
            {
                Mesh             = new BoxMesh { Size = size },
                Position         = pos,
                MaterialOverride = fogMat,
                CastShadow       = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }
    }
}
