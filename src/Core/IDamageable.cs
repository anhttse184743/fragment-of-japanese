namespace FragmentOfJapanese.Core;

/// <summary>Bất cứ thứ gì có thể nhận sát thương (player, quái...). Dùng bởi AttackZone.</summary>
public interface IDamageable
{
    void TakeDamage(int amount);
}
