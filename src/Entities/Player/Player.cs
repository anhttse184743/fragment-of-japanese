using Godot;
using FragmentOfJapanese.Core;

namespace FragmentOfJapanese.Entities.Player;

public partial class Player : CharacterBody3D, IDamageable
{
	[Export] public PlayerData Data { get; set; }

	[Signal] public delegate void HpChangedEventHandler(int current, int max);
	[Signal] public delegate void ManaChangedEventHandler(int current, int max);
	[Signal] public delegate void ExpChangedEventHandler(int current, int max, int level);
	[Signal] public delegate void DiedEventHandler();

	public override void _Ready()
	{
		Data ??= new PlayerData();
		AddToGroup("player");   // để quái tự tìm được mục tiêu
	}

	public void TakeDamage(int amount)
	{
		int actual = Mathf.Max(1, amount - Data.Defense);
		Data.Hp = Mathf.Max(0, Data.Hp - actual);
		EmitSignal(SignalName.HpChanged, Data.Hp, Data.MaxHp);
		if (Data.Hp == 0) EmitSignal(SignalName.Died);
	}

	public void Heal(int amount)
	{
		Data.Hp = Mathf.Min(Data.MaxHp, Data.Hp + amount);
		EmitSignal(SignalName.HpChanged, Data.Hp, Data.MaxHp);
	}

	/// <summary>Tiêu mana nếu đủ; trả về true nếu dùng được (cho phép thuật/skill sau này).</summary>
	public bool UseMana(int amount)
	{
		if (Data.Mana < amount) return false;
		Data.Mana -= amount;
		EmitSignal(SignalName.ManaChanged, Data.Mana, Data.MaxMana);
		return true;
	}

	public void RestoreMana(int amount)
	{
		Data.Mana = Mathf.Min(Data.MaxMana, Data.Mana + amount);
		EmitSignal(SignalName.ManaChanged, Data.Mana, Data.MaxMana);
	}

	public void GainExp(int amount)
	{
		Data.Exp += amount;
		if (Data.Exp >= Data.MaxExp) LevelUp();
		EmitSignal(SignalName.ExpChanged, Data.Exp, Data.MaxExp, Data.Level);
	}

	private void LevelUp()
	{
		Data.Level++;
		Data.Exp    -= Data.MaxExp;
		Data.MaxExp  = (int)(Data.MaxExp * 1.25f);
		Data.MaxHp  += 15;
		Data.Hp      = Data.MaxHp;
		Data.Attack += 3;
		GD.Print($"[Player] Level up → {Data.Level}");
	}
}
