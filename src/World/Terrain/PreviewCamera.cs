using Godot;

namespace FragmentOfJapanese.World;

/// <summary>
/// Camera bay tự do để preview map — xóa sau khi có camera game thật.
///
/// Điều khiển:
///   Chuột phải (giữ) + kéo → xoay nhìn
///   WASD                    → di chuyển ngang
///   Q / E                   → lên / xuống
///   Shift                   → tăng tốc x3
///   Scroll wheel            → tăng/giảm tốc độ bay
/// </summary>
public partial class PreviewCamera : Camera3D
{
    private float _speed    = 20f;   // m/s
    private float _sens     = 0.003f; // radian / pixel
    private bool  _looking  = false;

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) return;   // skip trong editor

        // Bắt sự kiện chuột trong viewport
        SetProcessUnhandledInput(true);
    }

    public override void _UnhandledInput(InputEvent ev)
    {
        // Bắt đầu / kết thúc xoay khi nhấn chuột phải
        if (ev is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Right)
        {
            _looking = mb.Pressed;
            Input.MouseMode = _looking
                ? Input.MouseModeEnum.Captured
                : Input.MouseModeEnum.Visible;
        }

        // Xoay camera khi di chuột (chỉ khi đang giữ chuột phải)
        if (_looking && ev is InputEventMouseMotion mm)
        {
            RotateY(-mm.Relative.X * _sens);
            RotateObjectLocal(Vector3.Right, -mm.Relative.Y * _sens);
        }

        // Scroll wheel → đổi tốc độ bay
        if (ev is InputEventMouseButton scroll)
        {
            if (scroll.ButtonIndex == MouseButton.WheelUp)
                _speed = Mathf.Clamp(_speed * 1.2f, 1f, 200f);
            else if (scroll.ButtonIndex == MouseButton.WheelDown)
                _speed = Mathf.Clamp(_speed / 1.2f, 1f, 200f);
        }
    }

    public override void _Process(double delta)
    {
        if (!_looking) return;

        float dt  = (float)delta;
        float spd = _speed * (Input.IsKeyPressed(Key.Shift) ? 3f : 1f);

        var dir = Vector3.Zero;
        if (Input.IsKeyPressed(Key.W)) dir -= Transform.Basis.Z;
        if (Input.IsKeyPressed(Key.S)) dir += Transform.Basis.Z;
        if (Input.IsKeyPressed(Key.A)) dir -= Transform.Basis.X;
        if (Input.IsKeyPressed(Key.D)) dir += Transform.Basis.X;
        if (Input.IsKeyPressed(Key.E)) dir += Vector3.Up;
        if (Input.IsKeyPressed(Key.Q)) dir -= Vector3.Up;

        if (dir != Vector3.Zero)
            Position += dir.Normalized() * spd * dt;
    }
}
