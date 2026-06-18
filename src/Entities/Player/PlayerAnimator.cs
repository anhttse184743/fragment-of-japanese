using Godot;

namespace FragmentOfJapanese.Entities.Player;

/// <summary>
/// Máy trạng thái hoạt ảnh cho sprite 2.5D nhiều hướng.
///
/// Trạng thái: idle / run / jump / attack. Mỗi trạng thái có 1 animation cho mỗi hướng,
/// đặt tên theo quy ước  <c>{state}_{dir}</c>  ví dụ: idle_down, run_left, jump_up, attack_right.
///   - 4 hướng: down, right, up, left
///   - 8 hướng: down, down_right, right, up_right, up, up_left, left, down_left
///
/// Hướng được tính theo hướng nhân vật ĐANG ĐI so với camera (orbit camera → hướng đổi).
/// "Run nhanh" (PlayerController.IsRunning) → tăng SpeedScale = hoạt ảnh chạy nhanh hơn.
/// jump / attack là one-shot (không loop); attack khoá hướng tới khi đánh xong.
///
/// CHỊU ĐƯỢC KHI CHƯA CÓ ART: nếu SpriteFrames chưa có animation tương ứng thì bỏ qua, không lỗi.
/// Gán <see cref="_sprite"/> = AnimatedSprite3D, <see cref="_player"/>, <see cref="_camera"/>,
/// <see cref="_controller"/> trong Inspector.
/// </summary>
public partial class PlayerAnimator : Node
{
    [Export] private AnimatedSprite3D _sprite;
    [Export] private CharacterBody3D  _player;
    [Export] private Camera3D         _camera;
    [Export] private PlayerController _controller;

    [Export(PropertyHint.Enum, "4:4,8:8")] public int Directions = 4;
    [Export] public float AngleOffsetDeg     = 0f;     // xoay 90° nếu hướng bị lệch so với art
    [Export] public float RunFastSpeedScale  = 1.6f;   // run nhanh → anim nhanh hơn bao nhiêu lần
    [Export] public float MoveThreshold      = 0.15f;  // tốc độ tối thiểu coi là "đang đi"

    private static readonly string[] Dir4 = { "down", "right", "up", "left" };
    private static readonly string[] Dir8 =
        { "down", "down_right", "right", "up_right", "up", "up_left", "left", "down_left" };

    private float  _facingYaw;
    private string _currentAnim = "";
    private bool   _attacking;

    public override void _Ready()
    {
        if (_controller != null)
            _controller.AttackPressed += OnAttack;
        if (_sprite != null)
            _sprite.AnimationFinished += OnSpriteAnimationFinished;
    }

    public override void _ExitTree()
    {
        if (_controller != null)
            _controller.AttackPressed -= OnAttack;
        if (_sprite != null)
            _sprite.AnimationFinished -= OnSpriteAnimationFinished;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_sprite == null || _player == null) return;

        // Cập nhật hướng theo vận tốc (giữ hướng cũ khi đứng yên)
        var vel   = _player.Velocity;
        var horiz = new Vector2(vel.X, vel.Z);
        if (horiz.Length() > MoveThreshold)
            _facingYaw = Mathf.Atan2(vel.X, vel.Z);

        if (_attacking) return;   // đang đánh: khoá, để animation chạy hết

        _sprite.SpeedScale = 1f;

        if (!_player.IsOnFloor())
            Play("jump");
        else if (horiz.Length() > MoveThreshold)
        {
            Play("run");
            if (_controller != null && _controller.IsRunning)
                _sprite.SpeedScale = RunFastSpeedScale;   // chạy nhanh = hoạt ảnh nhanh
        }
        else
            Play("idle");
    }

    private void OnAttack()
    {
        if (_sprite == null) return;
        _attacking = true;
        _sprite.SpeedScale = 1f;
        Play("attack", force: true);
    }

    private void OnSpriteAnimationFinished()
    {
        // attack/jump không loop → khi xong, mở khoá để quay lại idle/run frame kế
        if (_currentAnim.StartsWith("attack"))
            _attacking = false;
        _currentAnim = "";
    }

    /// <summary>Phát animation {state}_{dir}; chỉ đổi khi tên khác để không restart liên tục.</summary>
    private void Play(string state, bool force = false)
    {
        string anim = $"{state}_{DirSuffix()}";
        if (!force && anim == _currentAnim) return;
        _currentAnim = anim;

        if (_sprite.SpriteFrames != null && _sprite.SpriteFrames.HasAnimation(anim))
            _sprite.Play(anim);
        // chưa import animation đó → bỏ qua êm, không lỗi
    }

    private string DirSuffix()
    {
        int n = Directions <= 4 ? 4 : 8;
        float camYaw = _camera != null ? _camera.GlobalRotation.Y : 0f;
        float rel = Mathf.Wrap(_facingYaw - camYaw + Mathf.DegToRad(AngleOffsetDeg), 0f, Mathf.Tau);
        int index = Mathf.RoundToInt(rel / (Mathf.Tau / n)) % n;
        return n == 4 ? Dir4[index] : Dir8[index];
    }
}
