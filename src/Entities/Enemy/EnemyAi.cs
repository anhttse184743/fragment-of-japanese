using Godot;

namespace FragmentOfJapanese.Entities.Enemy;

/// <summary>
/// AI đơn giản (2.5D): Patrol → Chase khi phát hiện player → Trigger battle khi đến gần.
/// Di chuyển trong không gian 3D trên mặt phẳng X,Z; có trọng lực để bám mặt đất.
/// Khoảng cách tính bằng MÉT (không phải pixel).
/// TODO: nâng cấp sang NavigationAgent3D (terrain đã bake sẵn NavigationRegion3D).
/// </summary>
public partial class EnemyAi : Node
{
    public enum AiState { Patrol, Chase, Battle }

    [Export] private Enemy _enemy;
    [Export] public float DetectRange { get; set; } = 12f;    // m
    [Export] public float BattleRange { get; set; } = 2f;     // m
    [Export] public float MoveSpeed   { get; set; } = 3.5f;   // m/s
    [Export] public float Gravity     { get; set; } = 18f;

    public AiState State { get; private set; } = AiState.Patrol;

    private Node3D  _target;                       // tham chiếu Player (CharacterBody3D)
    private AiState _lastState = AiState.Patrol;

    [Signal] public delegate void BattleTriggeredEventHandler(Enemy enemy);

    public override void _PhysicsProcess(double delta)
    {
        if (_enemy == null || _target == null) return;

        float dt   = (float)delta;
        float dist = _enemy.GlobalPosition.DistanceTo(_target.GlobalPosition);

        State = dist <= BattleRange ? AiState.Battle
              : dist <= DetectRange ? AiState.Chase
              : AiState.Patrol;

        var v = _enemy.Velocity;

        // Di chuyển ngang (X,Z) — chỉ khi đang đuổi theo player
        if (State == AiState.Chase)
        {
            var to = _target.GlobalPosition - _enemy.GlobalPosition;
            to.Y = 0f;
            var dir = to.Normalized();
            v.X = dir.X * MoveSpeed;
            v.Z = dir.Z * MoveSpeed;
        }
        else
        {
            v.X = 0f;
            v.Z = 0f;
        }

        // Trọng lực để bám mặt đất
        if (!_enemy.IsOnFloor()) v.Y -= Gravity * dt;
        else if (v.Y < 0f)       v.Y = 0f;

        _enemy.Velocity = v;
        _enemy.MoveAndSlide();

        // Phát battle 1 lần khi vừa vào tầm (tránh spam mỗi frame)
        if (State == AiState.Battle && _lastState != AiState.Battle)
            EmitSignal(SignalName.BattleTriggered, _enemy);
        _lastState = State;
    }

    public void SetTarget(Node3D target) => _target = target;
}
