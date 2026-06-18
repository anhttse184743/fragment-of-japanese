using System;
using Godot;

namespace FragmentOfJapanese.Entities.Player;

/// <summary>
/// Điều khiển nhân vật 2.5D trong không gian 3D:
///   - Di chuyển camera-relative (đẩy "tiến" = ra xa camera).
///   - Nhảy (Space).
///   - Chạy nhanh kiểu TOGGLE: Shift nhấn 1 lần bật, nhấn lại tắt (không phải giữ).
///   - Tấn công (J) → bắn event <see cref="AttackPressed"/> cho PlayerAnimator.
///
/// Phần HÌNH ẢNH (hướng + animation) do <see cref="PlayerAnimator"/> lo.
/// Keyboard dùng được ngay; mobile gọi <see cref="ToggleRun"/> / <see cref="RequestJump"/> /
/// <see cref="RequestAttack"/> từ nút cảm ứng (làm sau).
/// </summary>
public partial class PlayerController : Node
{
    [Export] private CharacterBody3D _player;   // = %Player
    [Export] private Camera3D        _camera;   // = %MainCamera3D (lấy yaw cho camera-relative)

    [Export] public float MoveSpeed = 4.5f;     // m/s đi bộ
    [Export] public float RunSpeed  = 9.0f;     // m/s chạy nhanh
    [Export] public float JumpSpeed = 6.0f;     // lực nhảy
    [Export] public float Gravity   = 18.0f;

    /// <summary>Vector input cảm ứng (-1..1) cho virtual joystick mobile. (0,0) = dùng keyboard.</summary>
    public Vector2 TouchInput { get; set; } = Vector2.Zero;

    /// <summary>Đang ở chế độ chạy nhanh hay không (toggle).</summary>
    public bool IsRunning { get; private set; }

    /// <summary>Bắn khi người chơi bấm tấn công — PlayerAnimator nghe để phát anim attack.</summary>
    public event Action AttackPressed;

    private bool _jumpRequested;

    public override void _Ready() => EnsureInputActions();

    // ───── API cho nút cảm ứng mobile (gọi sau) ─────
    public void ToggleRun()    => IsRunning = !IsRunning;
    public void RequestJump()  => _jumpRequested = true;
    public void RequestAttack() => AttackPressed?.Invoke();

    public override void _PhysicsProcess(double delta)
    {
        if (_player == null) return;

        float dt = (float)delta;
        var velocity = _player.Velocity;

        // Toggle chạy nhanh: Shift nhấn 1 lần (hoặc nút mobile gọi ToggleRun)
        if (Input.IsActionJustPressed("run_toggle"))
            IsRunning = !IsRunning;

        // Tấn công
        if (Input.IsActionJustPressed("attack"))
            AttackPressed?.Invoke();

        bool onFloor = _player.IsOnFloor();

        // Trọng lực
        if (!onFloor)
            velocity.Y -= Gravity * dt;

        // Nhảy (chỉ khi đang trên mặt đất)
        bool wantJump = Input.IsActionJustPressed("jump") || _jumpRequested;
        _jumpRequested = false;
        if (wantJump && onFloor)
            velocity.Y = JumpSpeed;

        // Input ngang: bàn phím hoặc joystick cảm ứng
        Vector2 input = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        if (input == Vector2.Zero && TouchInput != Vector2.Zero)
            input = TouchInput;

        float speed = IsRunning ? RunSpeed : MoveSpeed;

        if (input != Vector2.Zero)
        {
            // "tiến" (W) = ra xa camera → xoay input theo yaw camera
            var move = new Vector3(input.X, 0f, input.Y);
            float camYaw = _camera != null ? _camera.GlobalRotation.Y : 0f;
            move = move.Rotated(Vector3.Up, camYaw).Normalized();
            velocity.X = move.X * speed;
            velocity.Z = move.Z * speed;
        }
        else
        {
            velocity.X = Mathf.MoveToward(velocity.X, 0f, speed);
            velocity.Z = Mathf.MoveToward(velocity.Z, 0f, speed);
        }

        _player.Velocity = velocity;
        _player.MoveAndSlide();
    }

    /// <summary>Đăng ký input action nếu project chưa khai báo — để test bàn phím ngay.</summary>
    private static void EnsureInputActions()
    {
        AddKey("move_up",    Key.W);
        AddKey("move_down",  Key.S);
        AddKey("move_left",  Key.A);
        AddKey("move_right", Key.D);
        AddKey("jump",       Key.Space);
        AddKey("run_toggle", Key.Shift);
        AddKey("attack",     Key.J);
    }

    private static void AddKey(string action, Key key)
    {
        if (!InputMap.HasAction(action))
            InputMap.AddAction(action);

        foreach (var e in InputMap.ActionGetEvents(action))
            if (e is InputEventKey k && k.PhysicalKeycode == key)
                return; // đã có, không add trùng

        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
    }
}
