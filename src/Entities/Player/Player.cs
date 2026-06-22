using Godot;
using FragmentOfJapanese.Core;

namespace FragmentOfJapanese.Entities.Player;

public partial class Player : CharacterBody3D, IDamageable
{
	[Export] public PlayerData Data { get; set; }

	// Hồi máu khi không bị đánh đủ lâu
	[Export] public float RegenDelay  { get; set; } = 60f;   // giây không bị đánh thì bắt đầu hồi
	[Export] public int   RegenAmount { get; set; } = 2;     // +máu mỗi nhịp
	[Export] public float RegenTick   { get; set; } = 1f;    // nhịp hồi (giây)

	[Signal] public delegate void HpChangedEventHandler(int current, int max);
	[Signal] public delegate void ManaChangedEventHandler(int current, int max);
	[Signal] public delegate void StaminaChangedEventHandler(int current, int max);
	[Signal] public delegate void ExpChangedEventHandler(int current, int max, int level);
	[Signal] public delegate void LeveledUpEventHandler();
	[Signal] public delegate void DiedEventHandler();

	public override void _Ready()
	{
		Data ??= new PlayerData();
		AddToGroup("player");   // để quái tự tìm được mục tiêu
	}

	private float _sinceDamage;
	private float _regenTimer;

	public override void _Process(double delta)
	{
		// Không bị đánh đủ lâu → hồi máu dần tới khi đầy
		if (Data == null || Data.Hp <= 0 || Data.Hp >= Data.MaxHp) return;

		_sinceDamage += (float)delta;
		if (_sinceDamage < RegenDelay) return;

		_regenTimer += (float)delta;
		if (_regenTimer >= RegenTick)
		{
			_regenTimer = 0f;
			Heal(RegenAmount);
		}
	}

	public void TakeDamage(int amount)
	{
		int actual = Mathf.Max(1, amount - Data.Defense);
		Data.Hp = Mathf.Max(0, Data.Hp - actual);
		_sinceDamage = 0f;   // bị đánh → reset bộ đếm hồi máu
		_regenTimer  = 0f;
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

	/// <summary>Tiêu thể lực nếu đủ; trả về true nếu dùng được (cho chạy/né/skill sau này).</summary>
	public bool UseStamina(int amount)
	{
		if (Data.Stamina < amount) return false;
		Data.Stamina -= amount;
		EmitSignal(SignalName.StaminaChanged, Data.Stamina, Data.MaxStamina);
		return true;
	}

	public void RestoreStamina(int amount)
	{
		Data.Stamina = Mathf.Min(Data.MaxStamina, Data.Stamina + amount);
		EmitSignal(SignalName.StaminaChanged, Data.Stamina, Data.MaxStamina);
	}

	/// <summary>Hồi đầy máu/mana/thể lực — dùng khi hồi sinh, tránh vòng lặp chết khi về World.</summary>
	public void RestoreFull()
	{
		if (Data == null) return;
		Data.Hp      = Data.MaxHp;
		Data.Mana    = Data.MaxMana;
		Data.Stamina = Data.MaxStamina;
		_sinceDamage = 0f;
		_regenTimer  = 0f;
		EmitSignal(SignalName.HpChanged,      Data.Hp,      Data.MaxHp);
		EmitSignal(SignalName.ManaChanged,    Data.Mana,    Data.MaxMana);
		EmitSignal(SignalName.StaminaChanged, Data.Stamina, Data.MaxStamina);
	}

	/// <summary>Mất một TỈ LỆ máu hiện tại (phạt trả lời sai đoạn văn). Bỏ qua phòng thủ.</summary>
	public void LosePercent(float pct)
	{
		if (Data == null) return;
		int loss = Mathf.Max(1, (int)(Data.Hp * pct));
		Data.Hp = Mathf.Max(0, Data.Hp - loss);
		_sinceDamage = 0f;
		_regenTimer  = 0f;
		EmitSignal(SignalName.HpChanged, Data.Hp, Data.MaxHp);
		if (Data.Hp == 0) EmitSignal(SignalName.Died);
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
		Data.MaxHp      += 15;
		Data.Hp          = Data.MaxHp;
		Data.MaxStamina += 10;
		Data.Stamina     = Data.MaxStamina;
		Data.Attack     += 3;
		EmitSignal(SignalName.HpChanged, Data.Hp, Data.MaxHp);              // HUD cập nhật máu sau khi hồi đầy
		EmitSignal(SignalName.StaminaChanged, Data.Stamina, Data.MaxStamina);
		EmitSignal(SignalName.LeveledUp);
		GD.Print($"[Player] Level up → {Data.Level}");
	}
}
