using Godot;
using FragmentOfJapanese.Entities.Player;
using FragmentOfJapanese.Entities.Enemy;

namespace FragmentOfJapanese.Battle;

/// <summary>
/// Điều phối trận chiến turn-based.
/// Mỗi lượt player = 1 câu quiz (đúng → tấn công, sai → bị phản đòn).
/// </summary>
public partial class BattleManager : Node
{
    public enum BattleState { Idle, PlayerTurn, EnemyTurn, Victory, Defeat }

    private Player _player;
    private Enemy  _enemy;

    public BattleState State { get; private set; } = BattleState.Idle;

    [Signal] public delegate void BattleStartedEventHandler();
    [Signal] public delegate void BattleEndedEventHandler(bool playerWon);
    [Signal] public delegate void TurnResolvedEventHandler(bool playerAttacked, int damage);

    public void StartBattle(Player player, Enemy enemy)
    {
        _player = player;
        _enemy  = enemy;
        State   = BattleState.PlayerTurn;
        GD.Print($"[BattleManager] Battle start: Player vs {enemy.EnemyName}");
        EmitSignal(SignalName.BattleStarted);
    }

    /// <summary>Gọi sau khi player trả lời quiz</summary>
    public void OnQuizAnswered(bool correct)
    {
        if (State != BattleState.PlayerTurn) return;

        var turn = new BattleTurn(_player, _enemy);
        int damage;

        if (correct)
        {
            damage = turn.PlayerAttack();
            EmitSignal(SignalName.TurnResolved, true, damage);
        }
        else
        {
            damage = turn.EnemyCounterAttack();
            EmitSignal(SignalName.TurnResolved, false, damage);
        }

        CheckEnd();
    }

    private void CheckEnd()
    {
        if (_enemy.Hp <= 0)
        {
            State = BattleState.Victory;
            _player.GainExp(_enemy.ExpReward);
            GD.Print("[BattleManager] Victory!");
            EmitSignal(SignalName.BattleEnded, true);
        }
        else if (_player.Data.Hp <= 0)
        {
            State = BattleState.Defeat;
            GD.Print("[BattleManager] Defeat.");
            EmitSignal(SignalName.BattleEnded, false);
        }
        else
        {
            State = BattleState.PlayerTurn;
        }
    }
}
