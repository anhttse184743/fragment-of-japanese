using Godot;

namespace FragmentOfJapanese.World;

/// <summary>
/// Player 3D đơn giản để xác định kích cỡ map.
/// - Capsule cao 1.75m (như người thật)
/// - WASD di chuyển, Space nhảy
/// - Gravity giữ trên terrain
/// - Tốc độ 4.5 m/s (chuẩn RPG mobile)
///
/// Camera tự follow phía sau ở góc 3rd person.
/// </summary>
public partial class PreviewPlayer : CharacterBody3D
{
    [Export] public float WalkSpeed = 4.5f;     // m/s — chuẩn RPG
    [Export] public float RunSpeed  = 9.0f;     // Shift × 2
    [Export] public float JumpSpeed = 6.0f;
    [Export] public float Gravity   = 18.0f;

    private Camera3D _cam;
    private float    _camYaw   = 0f;
    private float    _camPitch = -0.4f; // hơi nhìn xuống

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) return;   // skip trong editor

        // Hình dạng player
        var mesh = new CapsuleMesh
        {
            Radius = 0.35f,
            Height = 1.75f,
        };
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.95f, 0.35f, 0.25f),  // đỏ cam nổi bật
            Roughness   = 0.6f,
        };
        mesh.Material = mat;

        var visual = new MeshInstance3D { Mesh = mesh };
        visual.Position = new Vector3(0, 1.75f * 0.5f, 0); // gốc capsule ở giữa, dời lên
        AddChild(visual);

        // Collider khớp với capsule
        var collider = new CollisionShape3D
        {
            Shape    = new CapsuleShape3D { Radius = 0.35f, Height = 1.75f },
            Position = new Vector3(0, 1.75f * 0.5f, 0),
        };
        AddChild(collider);

        // Camera 3rd person
        _cam = new Camera3D
        {
            Fov     = 65f,
            Current = true,
        };
        AddChild(_cam);
        UpdateCameraPosition();

        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _UnhandledInput(InputEvent ev)
    {
        // Xoay camera khi di chuột
        if (ev is InputEventMouseMotion mm && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            _camYaw   -= mm.Relative.X * 0.003f;
            _camPitch -= mm.Relative.Y * 0.003f;
            _camPitch  = Mathf.Clamp(_camPitch, -1.2f, 0.3f);
        }

        // ESC = thả chuột để click khác
        if (ev is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
        {
            Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
                ? Input.MouseModeEnum.Visible
                : Input.MouseModeEnum.Captured;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        var velocity = Velocity;

        // Gravity
        if (!IsOnFloor())
            velocity.Y -= Gravity * dt;
        else if (Input.IsKeyPressed(Key.Space))
            velocity.Y = JumpSpeed;

        // Di chuyển theo hướng camera nhìn (chỉ XZ)
        float spd = Input.IsKeyPressed(Key.Shift) ? RunSpeed : WalkSpeed;
        var input = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W)) input.Y -= 1;
        if (Input.IsKeyPressed(Key.S)) input.Y += 1;
        if (Input.IsKeyPressed(Key.A)) input.X -= 1;
        if (Input.IsKeyPressed(Key.D)) input.X += 1;

        if (input != Vector2.Zero)
        {
            input = input.Normalized();
            // Forward = camera yaw direction (XZ plane)
            var fwd   = new Vector3(-Mathf.Sin(_camYaw), 0, -Mathf.Cos(_camYaw));
            var right = new Vector3( Mathf.Cos(_camYaw), 0, -Mathf.Sin(_camYaw));
            var move  = (fwd * -input.Y + right * input.X) * spd;
            velocity.X = move.X;
            velocity.Z = move.Z;
        }
        else
        {
            velocity.X = 0;
            velocity.Z = 0;
        }

        Velocity = velocity;
        MoveAndSlide();

        UpdateCameraPosition();
    }

    private void UpdateCameraPosition()
    {
        if (_cam == null) return;

        // 3rd person: camera ở phía sau + cao hơn
        float dist = 6f;
        float h    = 2.5f;
        var offset = new Vector3(
            Mathf.Sin(_camYaw) * Mathf.Cos(_camPitch) * dist,
            -Mathf.Sin(_camPitch) * dist + h,
            Mathf.Cos(_camYaw) * Mathf.Cos(_camPitch) * dist
        );
        _cam.Position = offset;
        _cam.LookAt(GlobalPosition + new Vector3(0, 1.2f, 0), Vector3.Up);
    }
}
