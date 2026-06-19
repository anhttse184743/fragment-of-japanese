using Godot;
using FragmentOfJapanese.Entities;

namespace FragmentOfJapanese.Entities.Enemy;

/// <summary>
/// AI quái real-time (2.5D): Idle → Chase (đi/chạy về phía player) → Attack khi đến tầm.
///   - Đi bộ khi gần, CHẠY khi xa (đều chậm hơn player một chút).
///   - Nhảy TÙY TÌNH HUỐNG: khi kẹt tường hoặc mục tiêu ở cao hơn (dù không có animation nhảy).
///   - Đến tầm thì vung đòn theo nhịp (AttackCooldown) → AttackZone gây sát thương.
/// Tự tìm player trong group "player". Mọi thông số chỉnh trong Inspector.
/// Khoảng cách = MÉT. TODO: nâng cấp NavigationAgent3D (terrain đã bake NavigationRegion3D).
/// </summary>
public partial class EnemyAi : Node
{
    public enum AiState { Idle, Chase, Attack }

    [Export] private Enemy             _enemy;
    [Export] private CharacterAnimator _animator;
    [Export] private AttackZone        _attackZone;

    [Export] public float DetectRange    { get; set; } = 12f;   // m — bắt đầu đuổi
    [Export] public float AttackRange     { get; set; } = 1.6f;  // m — vào tầm đánh
    [Export] public float WalkSpeed       { get; set; } = 3.2f;  // chậm hơn player (4.5)
    [Export] public float RunSpeed        { get; set; } = 6.0f;  // chậm hơn player (9.0)
    [Export] public float RunDistance     { get; set; } = 5f;    // xa hơn mức này thì chạy
    [Export] public float Gravity         { get; set; } = 18f;
    [Export] public float JumpSpeed       { get; set; } = 5.5f;
    [Export] public float JumpHeightDiff  { get; set; } = 1.0f;  // mục tiêu cao hơn ngần này thì nhảy
    [Export] public float AttackCooldown  { get; set; } = 1.2f;  // giây giữa các đòn

    public AiState State { get; private set; } = AiState.Idle;

    [Signal] public delegate void BattleTriggeredEventHandler(Enemy enemy);  // giữ cho tương thích

    private Node3D _target;
    private float  _atkTimer;

    public override void _Ready() => _target = GetTree().GetFirstNodeInGroup("player") as Node3D;

    public void SetTarget(Node3D target) => _target = target;

    public override void _PhysicsProcess(double delta)
    {
        if (_enemy == null) return;
        float dt = (float)delta;

        _target ??= GetTree().GetFirstNodeInGroup("player") as Node3D;
        float dist = _target != null ? _enemy.GlobalPosition.DistanceTo(_target.GlobalPosition) : float.MaxValue;

        State = dist <= AttackRange ? AiState.Attack
              : dist <= DetectRange ? AiState.Chase
              :                       AiState.Idle;

        var v = _enemy.Velocity;
        bool running = false;

        // Di chuyển ngang khi đang đuổi
        if (State == AiState.Chase && _target != null)
        {
            var to = _target.GlobalPosition - _enemy.GlobalPosition;
            to.Y = 0f;
            var dir = to.Normalized();
            running = dist > RunDistance;
            float spd = running ? RunSpeed : WalkSpeed;
            v.X = dir.X * spd;
            v.Z = dir.Z * spd;
        }
        else
        {
            v.X = 0f;
            v.Z = 0f;
        }

        // Trọng lực + nhảy tùy tình huống
        bool onFloor = _enemy.IsOnFloor();
        if (!onFloor)
            v.Y -= Gravity * dt;
        else
        {
            v.Y = 0f;
            if (State == AiState.Chase)
            {
                bool blocked = _enemy.IsOnWall();   // kẹt vật cản
                bool higher  = _target != null &&
                               (_target.GlobalPosition.Y - _enemy.GlobalPosition.Y) > JumpHeightDiff;
                if (blocked || higher) v.Y = JumpSpeed;
            }
        }

        _enemy.Velocity = v;
        _enemy.MoveAndSlide();

        if (_animator != null) _animator.Running = running;

        // Vung đòn theo nhịp khi trong tầm
        _atkTimer -= dt;
        if (State == AiState.Attack && _atkTimer <= 0f)
        {
            _atkTimer = AttackCooldown;
            _animator?.TriggerAttack();
            _attackZone?.Attack();
        }
    }
}
