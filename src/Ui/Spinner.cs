using Godot;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// Vòng quay "đang tải" vẽ bằng code — một cung tròn tự xoay. Dùng cho nút khi đang xử lý.
/// Đặt làm con của nút (neo giữa), bật/tắt qua <c>Visible</c>.
/// </summary>
public partial class Spinner : Control
{
    [Export] public Color Color     { get; set; } = new(0.16f, 0.11f, 0.04f);
    [Export] public float Thickness { get; set; } = 3f;
    [Export] public float Speed     { get; set; } = 5f;

    private float _angle;

    public override void _Process(double delta)
    {
        if (!Visible) return;
        _angle    += (float)delta * Speed;
        PivotOffset = Size / 2f;
        Rotation    = _angle;
    }

    public override void _Draw()
    {
        var center = Size / 2f;
        float r = Mathf.Min(center.X, center.Y) - Thickness;
        if (r <= 0f) return;
        DrawArc(center, r, 0f, Mathf.Tau * 0.72f, 24, Color, Thickness, true);
    }
}
