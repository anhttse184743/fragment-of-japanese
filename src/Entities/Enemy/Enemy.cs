using Godot;
using FragmentOfJapanese.Core;

namespace FragmentOfJapanese.Entities.Enemy;

public partial class Enemy : CharacterBody3D, IDamageable
{
    [Export] public string EnemyId   { get; set; } = "";
    [Export] public string EnemyName { get; set; } = "";
    [Export] public int    Hp        { get; set; } = 100;
    [Export] public int    MaxHp     { get; set; } = 100;
    [Export] public int    Attack    { get; set; } = 12;
    [Export] public int    Defense   { get; set; } = 2;
    [Export] public int    ExpReward { get; set; } = 20;

    [Signal] public delegate void DiedEventHandler(Enemy enemy);
    [Signal] public delegate void HpChangedEventHandler(int current, int max);

    public void TakeDamage(int amount)
    {
        int actual = Mathf.Max(1, amount - Defense);
        Hp = Mathf.Max(0, Hp - actual);
        EmitSignal(SignalName.HpChanged, Hp, MaxHp);
        if (Hp == 0)
        {
            EmitSignal(SignalName.Died, this);   // cho hệ khác xử lý (loot/exp...)
            QueueFree();                          // tạm: biến mất khi chết (chưa có anim death)
        }
    }
}
