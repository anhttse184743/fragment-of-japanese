using FragmentOfJapanese.Entities.Player;
using FragmentOfJapanese.Entities.Enemy;

namespace FragmentOfJapanese.Battle;

/// <summary>Tính toán damage cho 1 lượt đánh.</summary>
public class BattleTurn
{
    private readonly Player _player;
    private readonly Enemy  _enemy;

    public BattleTurn(Player player, Enemy enemy)
    {
        _player = player;
        _enemy  = enemy;
    }

    /// <returns>Damage thực tế gây ra cho enemy</returns>
    public int PlayerAttack()
    {
        int damage = _player.Data.Attack;
        _enemy.TakeDamage(damage);
        return damage;
    }

    /// <returns>Damage thực tế gây ra cho player</returns>
    public int EnemyCounterAttack()
    {
        int damage = _enemy.Attack;
        _player.TakeDamage(damage);
        return damage;
    }
}
