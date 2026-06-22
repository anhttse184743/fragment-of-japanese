using Godot;
using FragmentOfJapanese.Entities;

namespace FragmentOfJapanese.Entities.Enemy;

/// <summary>
/// AI quái real-time (2.5D) kiểu "tổ":
///   - Patrol: đi dạo lững thững quanh ĐIỂM SPAWN (tổ) trong <see cref="WanderRadius"/>, có nghỉ.
///   - Chase/Attack: phát hiện player trong <see cref="DetectRange"/> → đuổi & đánh.
///   - Return: nếu rời tổ quá <see cref="LeashRadius"/> (đuổi xa / lạc) → bỏ player, chạy về tổ.
/// Tổ = vị trí lúc spawn (ghi ở khung hình đầu, sau khi Spawner đặt vị trí rải).
/// Tự tìm player trong group "player". Khoảng cách = MÉT, đo trên mặt phẳng (bỏ Y).
/// </summary>
public partial class EnemyAi : Node
{
    public enum AiState { Patrol, Chase, Attack, Return }

    [Export] private Enemy             _enemy;
    [Export] private CharacterAnimator _animator;
    [Export] private AttackZone        _attackZone;

    [Export] public float DetectRange    { get; set; } = 12f;   // m — bắt đầu đuổi
    [Export] public float AttackRange     { get; set; } = 1.6f;  // m — vào tầm đánh
    [Export] public float WalkSpeed       { get; set; } = 3.2f;
    [Export] public float RunSpeed        { get; set; } = 6.0f;
    [Export] public float RunDistance     { get; set; } = 5f;    // xa hơn mức này thì chạy
    [Export] public float Gravity         { get; set; } = 18f;
    [Export] public float JumpSpeed       { get; set; } = 5.5f;
    [Export] public float JumpHeightDiff  { get; set; } = 1.0f;
    [Export] public float AttackCooldown  { get; set; } = 1.2f;

    // ─── Tổ / tuần tra / leash ───
    [Export] public float WanderRadius   { get; set; } = 6f;    // đi dạo quanh tổ trong bán kính này
    [Export] public float LeashRadius    { get; set; } = 15f;   // rời tổ quá mức này khi đuổi → quay về
    [Export] public float WanderSpeed    { get; set; } = 1.8f;  // đi dạo chậm thong thả
    [Export] public float WanderPauseMin { get; set; } = 1.5f;  // nghỉ ngắn nhất tại điểm dạo
    [Export] public float WanderPauseMax { get; set; } = 3.5f;

    public AiState State { get; private set; } = AiState.Patrol;

    [Signal] public delegate void BattleTriggeredEventHandler(Enemy enemy);  // giữ cho tương thích

    private Node3D _target;
    private float  _atkTimer;

    private Vector3 _home;
    private bool    _homeSet;
    private Vector3 _wanderTarget;
    private float   _wanderPause;
    private bool    _returning;
    private readonly RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        _rng.Randomize();
        _target = GetTree().GetFirstNodeInGroup("player") as Node3D;
    }

    public void SetTarget(Node3D target) => _target = target;

    public override void _PhysicsProcess(double delta)
    {
        if (_enemy == null) return;
        float dt = (float)delta;

        // Tổ = vị trí spawn (đặt sau khi Spawner rải vị trí → ghi ở frame đầu)
        if (!_homeSet) { _home = Flat(_enemy.GlobalPosition); _homeSet = true; PickWanderTarget(); }

        _target ??= GetTree().GetFirstNodeInGroup("player") as Node3D;
        Vector3 here = Flat(_enemy.GlobalPosition);
        float distPlayer = _target != null ? here.DistanceTo(Flat(_target.GlobalPosition)) : float.MaxValue;
        float distHome   = here.DistanceTo(_home);

        // Bật/tắt chế độ quay về tổ
        if (_returning)
        {
            if (distHome <= WanderRadius * 0.4f) { _returning = false; PickWanderTarget(); }
        }
        else if (distHome > LeashRadius)
        {
            _returning = true;   // đuổi / lạc quá xa tổ → bỏ player, về tổ
        }

        bool    canAttack  = false;
        bool    running    = false;
        bool    wantMove   = false;
        float   speed      = WalkSpeed;
        Vector3 moveTarget = _enemy.GlobalPosition;

        if (_returning)
        {
            State      = AiState.Return;
            moveTarget = _home;
            running    = true;
            speed      = RunSpeed;
            wantMove   = true;
        }
        else if (_target != null && distPlayer <= DetectRange)
        {
            if (distPlayer <= AttackRange)
            {
                State     = AiState.Attack;
                canAttack = true;
            }
            else
            {
                State      = AiState.Chase;
                moveTarget = _target.GlobalPosition;
                running    = distPlayer > RunDistance;
                speed      = running ? RunSpeed : WalkSpeed;
                wantMove   = true;
            }
        }
        else
        {
            State = AiState.Patrol;
            Patrol(dt, ref wantMove, ref moveTarget, ref speed);
        }

        // Vận tốc ngang
        var v = _enemy.Velocity;
        if (wantMove)
        {
            var dir = (Flat(moveTarget) - here).Normalized();
            v.X = dir.X * speed;
            v.Z = dir.Z * speed;
        }
        else { v.X = 0f; v.Z = 0f; }

        // Trọng lực + nhảy tùy tình huống (chỉ khi đuổi / về tổ)
        bool onFloor = _enemy.IsOnFloor();
        if (!onFloor) v.Y -= Gravity * dt;
        else
        {
            v.Y = 0f;
            if (State == AiState.Chase || State == AiState.Return)
            {
                bool blocked = _enemy.IsOnWall();
                bool higher  = State == AiState.Chase && _target != null &&
                               (_target.GlobalPosition.Y - _enemy.GlobalPosition.Y) > JumpHeightDiff;
                if (blocked || higher) v.Y = JumpSpeed;
            }
        }

        _enemy.Velocity = v;
        _enemy.MoveAndSlide();

        if (_animator != null) _animator.Running = running;

        // Vung đòn theo nhịp
        _atkTimer -= dt;
        if (canAttack && _atkTimer <= 0f)
        {
            _atkTimer = AttackCooldown;
            _animator?.TriggerAttack();
            _attackZone?.Attack();
        }
    }

    /// <summary>Đi dạo quanh tổ: tới điểm ngẫu nhiên → nghỉ → chọn điểm mới.</summary>
    private void Patrol(float dt, ref bool wantMove, ref Vector3 moveTarget, ref float speed)
    {
        if (_wanderPause > 0f)
        {
            _wanderPause -= dt;
            if (_wanderPause <= 0f) PickWanderTarget();   // hết nghỉ → điểm mới
            wantMove = false;
            return;
        }

        if (Flat(_enemy.GlobalPosition).DistanceTo(_wanderTarget) < 0.6f)
        {
            _wanderPause = _rng.RandfRange(WanderPauseMin, WanderPauseMax);   // tới nơi → nghỉ
            wantMove = false;
        }
        else
        {
            moveTarget = _wanderTarget;
            speed      = WanderSpeed;
            wantMove   = true;
        }
    }

    private void PickWanderTarget()
    {
        float a = _rng.RandfRange(0f, Mathf.Tau);
        float r = _rng.RandfRange(WanderRadius * 0.25f, WanderRadius);
        _wanderTarget = _home + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        _wanderPause  = 0f;
    }

    private static Vector3 Flat(Vector3 v) => new(v.X, 0f, v.Z);
}
