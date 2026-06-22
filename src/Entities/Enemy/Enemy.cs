using Godot;
using FragmentOfJapanese.Core;
using FragmentOfJapanese.Combat;

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

    // ─── Cổng kết liễu (mini-game học) — DungeonController gán lúc spawn ───
    [Export] public GameKind Challenge   { get; set; } = GameKind.None;
    [Export] public int      HealCharges { get; set; } = 3;     // số lần hồi khi trả lời sai
    [Export] public float    HealPctMin  { get; set; } = 0.6f;  // hồi tới 60..80% máu tối đa
    [Export] public float    HealPctMax  { get; set; } = 0.8f;

    // ─── Hồi máu khi "an toàn" ───
    [Export] public float RegenDelay  { get; set; } = 50f;
    [Export] public int   RegenAmount { get; set; } = 10;
    [Export] public float RegenTick   { get; set; } = 1f;

    [Signal] public delegate void DiedEventHandler(Enemy enemy);
    [Signal] public delegate void HpChangedEventHandler(int current, int max);
    /// <summary>Đòn đánh đã hạ quái xuống ngưỡng tử nhưng quái còn game + lượt hồi → chờ challenge.</summary>
    [Signal] public delegate void LethalReachedEventHandler(Enemy enemy);

    private float _sinceDamage;
    private float _regenTimer;
    private bool  _resolving;   // đang chờ kết quả challenge → khóa sát thương + hồi máu
    private readonly RandomNumberGenerator _rng = new();

    public bool HasPendingChallenge => _resolving;

    public override void _Ready() => _rng.Randomize();

    public override void _Process(double delta)
    {
        if (_resolving) return;                       // đóng băng hồi máu khi chờ challenge
        if (Hp <= 0 || Hp >= MaxHp) return;

        _sinceDamage += (float)delta;
        if (_sinceDamage < RegenDelay) return;

        _regenTimer += (float)delta;
        if (_regenTimer >= RegenTick)
        {
            _regenTimer = 0f;
            Hp = Mathf.Min(MaxHp, Hp + RegenAmount);
            EmitSignal(SignalName.HpChanged, Hp, MaxHp);
        }
    }

    public void TakeDamage(int amount)
    {
        if (_resolving) return;                       // đang chờ challenge → bất tử tạm thời

        int actual = Mathf.Max(1, amount - Defense);
        Hp = Mathf.Max(0, Hp - actual);
        _sinceDamage = 0f;
        _regenTimer  = 0f;
        EmitSignal(SignalName.HpChanged, Hp, MaxHp);

        if (Hp > 0) return;

        // Tới ngưỡng tử: còn game + còn lượt hồi → giữ lại chờ challenge; ngược lại chết luôn.
        if (Challenge != GameKind.None && HealCharges > 0)
        {
            Hp = 1;
            _resolving = true;
            EmitSignal(SignalName.HpChanged, Hp, MaxHp);
            EmitSignal(SignalName.LethalReached, this);
        }
        else
        {
            Die();
        }
    }

    /// <summary>DungeonController gọi sau khi người chơi trả lời challenge.</summary>
    public void ResolveChallenge(bool success)
    {
        if (!_resolving) return;
        _resolving = false;

        if (success)
        {
            Die();
        }
        else
        {
            HealCharges--;
            float pct = _rng.RandfRange(HealPctMin, HealPctMax);
            Hp = Mathf.Clamp((int)(MaxHp * pct), 1, MaxHp);
            _sinceDamage = 0f;
            _regenTimer  = 0f;
            EmitSignal(SignalName.HpChanged, Hp, MaxHp);
        }
    }

    private void Die()
    {
        Hp = 0;
        EmitSignal(SignalName.HpChanged, 0, MaxHp);
        EmitSignal(SignalName.Died, this);
        QueueFree();
    }
}
