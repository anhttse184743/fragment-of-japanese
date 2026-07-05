using System;
using Godot;
using FragmentOfJapanese.Ui;

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
    [Export] private CharacterBody3D _player;       // = %Player
    [Export] private Camera3D        _camera;       // = %MainCamera3D (lấy yaw cho camera-relative)
    [Export] private AttackZone      _attackZone;   // vùng tấn công trước mặt (debug: vùng đỏ)

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
    private float _runStaminaTimer;
    private float _regenDelayTimer;
    private float _regenTickTimer;

    public override void _Ready()
    {
        EnsureInputActions();
        _camera?.MakeCurrent();
        if (_player != null)
        {
            _player.FloorSnapLength  = 0.5f;   // bám đất gồ ghề, đỡ nảy lên → camera đỡ giật dọc
            _player.FloorStopOnSlope = true;
        }
    }

    // ───── API cho nút cảm ứng mobile (gọi sau) ─────
    public void ToggleRun()    => IsRunning = !IsRunning;
    public void RequestJump()  => _jumpRequested = true;
    public void RequestAttack() { AttackPressed?.Invoke(); _attackZone?.Attack(); }

    public override void _PhysicsProcess(double delta)
    {
        if (_player == null) return;

        float dt = (float)delta;
        var velocity = _player.Velocity;

        bool locked = DialogueUi.Active;   // đang thoại → khoá điều khiển (vẫn rơi do trọng lực)

        // Toggle chạy nhanh: Shift nhấn 1 lần (hoặc nút mobile gọi ToggleRun)
        if (!locked && Input.IsActionJustPressed("run_toggle"))
            IsRunning = !IsRunning;

        bool onFloor = _player.IsOnFloor();

        // Trọng lực — đứng trên đất thì CẮT vận tốc rơi tích lũy (hết giật dọc khi đi qua đất gồ ghề)
        if (onFloor)
        {
            if (velocity.Y < 0f) velocity.Y = 0f;
        }
        else
            velocity.Y -= Gravity * dt;

        // Nhảy (chỉ khi đang trên mặt đất)
        bool wantJump = !locked && (Input.IsActionJustPressed("jump") || _jumpRequested);
        _jumpRequested = false;
        if (wantJump && onFloor)
            velocity.Y = JumpSpeed;

        // Input ngang: bàn phím hoặc joystick cảm ứng
        Vector2 input = locked
            ? Vector2.Zero
            : Input.GetVector("move_left", "move_right", "move_up", "move_down");
        if (input == Vector2.Zero && TouchInput != Vector2.Zero && !locked)
            input = TouchInput;

        bool isMoving = input != Vector2.Zero;
        Player p = _player as Player;

        if (p != null)
        {
            if (IsRunning && isMoving)
            {
                _regenDelayTimer = 0f;
                _runStaminaTimer += dt;
                if (_runStaminaTimer >= 0.5f)
                {
                    _runStaminaTimer -= 0.5f;
                    if (!p.UseStamina(1))
                        IsRunning = false;
                }
            }
            else
            {
                _runStaminaTimer = 0f;
                _regenDelayTimer += dt;
                if (_regenDelayTimer >= 3.0f)
                {
                    _regenTickTimer += dt;
                    if (_regenTickTimer >= 0.5f)
                    {
                        _regenTickTimer -= 0.5f;
                        p.RestoreStamina(1);
                    }
                }
            }

            if (p.Data.Stamina <= 0)
                IsRunning = false;
        }

        float speed = IsRunning ? RunSpeed : MoveSpeed;

        if (input != Vector2.Zero)
        {
            // "tiến" (W) = ra xa camera → xoay input theo yaw camera
            var move = new Vector3(input.X, 0f, input.Y);
            if (move.Length() > 1f) move = move.Normalized();   // chặn chéo bàn phím; GIỮ độ lớn analog joystick
            float camYaw = _camera != null ? _camera.GlobalRotation.Y : 0f;
            move = move.Rotated(Vector3.Up, camYaw);
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

    public override void _UnhandledInput(InputEvent ev)
    {
        // Đánh ở _UnhandledInput để UI (joystick) "nuốt" được sự kiện → không đánh nhầm khi chạm joystick.
        if (DialogueUi.Active) return;     // đang thoại → không đánh
        if (ev.IsActionPressed("attack"))
        {
            AttackPressed?.Invoke();   // → animation
            _attackZone?.Attack();     // → sát thương vùng trước mặt
        }
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
        AddMouseButton("attack", MouseButton.Left);   // chuột trái = đánh
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

    private static void AddMouseButton(string action, MouseButton button)
    {
        if (!InputMap.HasAction(action))
            InputMap.AddAction(action);

        foreach (var e in InputMap.ActionGetEvents(action))
            if (e is InputEventMouseButton mb && mb.ButtonIndex == button)
                return; // đã có

        InputMap.ActionAddEvent(action, new InputEventMouseButton { ButtonIndex = button });
    }
}
