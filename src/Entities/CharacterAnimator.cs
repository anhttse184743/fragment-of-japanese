using Godot;

namespace FragmentOfJapanese.Entities;

/// <summary>
/// Directional sprite animator dùng CHUNG cho mọi nhân vật (player & quái).
/// Trạng thái idle/run/jump/attack × 4 hoặc 8 hướng; chọn hướng theo hướng đi so với camera.
/// BỎ QUA animation thiếu (vd quái không có jump) → không lỗi, giữ frame hiện tại.
///
/// Điều khiển từ ngoài: gán <see cref="Running"/> (chạy nhanh → anim nhanh hơn) và gọi
/// <see cref="TriggerAttack"/>. Tự đọc vận tốc từ <c>_body</c>; camera tự lấy nếu không gán.
/// </summary>
public partial class CharacterAnimator : Node
{
    [Export] private AnimatedSprite3D _sprite;
    [Export] private CharacterBody3D  _body;
    [Export] private Camera3D         _camera;   // để trống = tự lấy camera đang active

    [Export(PropertyHint.Enum, "4:4,8:8")] public int Directions = 4;
    [Export] public float AngleOffsetDeg    = 0f;
    [Export] public float RunFastSpeedScale = 1.6f;
    [Export] public float MoveThreshold     = 0.15f;

    /// <summary>Có đang chạy nhanh không (controller/AI set) → tăng tốc animation run.</summary>
    public bool Running { get; set; }

    private static readonly string[] Dir4 = { "down", "right", "up", "left" };
    private static readonly string[] Dir8 =
        { "down", "down_right", "right", "up_right", "up", "up_left", "left", "down_left" };

    private float  _facingYaw;
    private string _currentAnim = "";
    private bool   _attacking;

    public override void _Ready()
    {
        _camera ??= GetViewport()?.GetCamera3D();
        if (_sprite != null) _sprite.AnimationFinished += OnFinished;
    }

    public override void _ExitTree()
    {
        if (_sprite != null) _sprite.AnimationFinished -= OnFinished;
    }

    /// <summary>Phát animation attack (one-shot, khoá hướng tới khi xong).</summary>
    public void TriggerAttack()
    {
        if (_sprite == null) return;
        _attacking = true;
        _sprite.SpeedScale = 1f;
        Play("attack", force: true);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_sprite == null || _body == null) return;
        _camera ??= GetViewport()?.GetCamera3D();   // bám camera active (player's MainCamera3D)

        var v = _body.Velocity;
        var h = new Vector2(v.X, v.Z);
        if (h.Length() > MoveThreshold) _facingYaw = Mathf.Atan2(v.X, v.Z);

        if (_attacking) return;

        _sprite.SpeedScale = 1f;
        if (!_body.IsOnFloor())
            Play("jump");                              // quái thiếu jump → tự bỏ qua, giữ frame
        else if (h.Length() > MoveThreshold)
        {
            Play("run");
            if (Running) _sprite.SpeedScale = RunFastSpeedScale;
        }
        else
            Play("idle");
    }

    private void OnFinished()
    {
        if (_currentAnim.StartsWith("attack")) _attacking = false;
        _currentAnim = "";
    }

    private void Play(string state, bool force = false)
    {
        string anim = $"{state}_{DirSuffix()}";
        if (!force && anim == _currentAnim) return;
        _currentAnim = anim;
        if (_sprite.SpriteFrames != null && _sprite.SpriteFrames.HasAnimation(anim))
            _sprite.Play(anim);
    }

    private string DirSuffix()
    {
        int n = Directions <= 4 ? 4 : 8;
        float camYaw = _camera != null ? _camera.GlobalRotation.Y : 0f;
        float rel = Mathf.Wrap(_facingYaw - camYaw + Mathf.DegToRad(AngleOffsetDeg), 0f, Mathf.Tau);
        int i = Mathf.RoundToInt(rel / (Mathf.Tau / n)) % n;
        return n == 4 ? Dir4[i] : Dir8[i];
    }
}
