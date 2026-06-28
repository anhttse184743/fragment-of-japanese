using Godot;
using System;
using System.Text.Json;
using FragmentOfJapanese.Core;
using FragmentOfJapanese.Items;

namespace FragmentOfJapanese.Entities.Player;

public partial class Player : CharacterBody3D, IDamageable
{
	[Export] public PlayerData Data { get; set; }

	private const string SavePath = "user://player.json";

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
		LoadData();
		AddToGroup("player");

		if (Inventory.Instance != null) Inventory.Instance.ItemUsed += OnItemUsed;

		_ = SyncFromServerAsync();
	}

	/// <summary>Áp hiệu ứng khi dùng vật phẩm tiêu hao (máu/mana là giá trị runtime → xử lý ở client).</summary>
	private void OnItemUsed(ItemEntry item)
	{
		switch (item.Effect)
		{
			case "heal": Heal(item.Value); break;
			case "mana": RestoreMana(item.Value); break;
		}
	}

	public async System.Threading.Tasks.Task SyncFromServerAsync()
	{
		if (string.IsNullOrEmpty(Autoloads.ApiClient.Instance.AccessToken)) return;
		var res = await Autoloads.ApiClient.Instance.GetAsync("/api/player/profile");
		if (res.IsSuccessStatusCode)
		{
			var data = await Autoloads.ApiClient.Instance.ReadAsAsync<Autoloads.AccountManager.ApiResponse<PlayerProfileDto>>(res);
			if (data?.Data != null)
			{
				Data.Level = data.Data.Level;
				Data.Exp = data.Data.Exp;
				EmitSignal(SignalName.ExpChanged, Data.Exp, Data.MaxExp, Data.Level);
			}
		}
	}

	private class PlayerProfileDto
	{
		public int Level { get; set; }
		public int Exp { get; set; }
	}

	public override void _ExitTree()
	{
		if (Inventory.Instance != null) Inventory.Instance.ItemUsed -= OnItemUsed;
		SaveData();
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
		_ = AwardAsync(amount, 0);   // bền hóa exp qua server (có trần, server tự lên cấp)
	}

	private void LevelUp()
	{
		while (Data.Exp >= Data.MaxExp)
		{
			Data.Level++;
			Data.Exp   -= Data.MaxExp;
			Data.MaxExp = (int)(Data.MaxExp * 1.25f);
			EmitSignal(SignalName.LeveledUp);
		}
	}

	/// <summary>Gửi phần thưởng exp/gold lên server (có trần) rồi đồng bộ lại số dư.</summary>
	public async System.Threading.Tasks.Task AwardAsync(int exp, int gold)
	{
		if (exp <= 0 && gold <= 0) return;
		if (string.IsNullOrEmpty(Autoloads.ApiClient.Instance.AccessToken)) return;

		var res = await Autoloads.ApiClient.Instance.PostAsync("/api/player/reward", new { Exp = exp, Gold = gold });
		if (res.IsSuccessStatusCode)
			_ = FragmentOfJapanese.Items.Wallet.Instance?.SyncAsync();   // gold đổi → làm mới ví
	}

	public void SaveData()
	{
		if (Data == null) return;
		try
		{
			var json = JsonSerializer.Serialize(new
			{
				Data.PlayerName,
				Data.Level, Data.Exp, Data.MaxExp,
				Data.Hp, Data.MaxHp,
				Data.Mana, Data.MaxMana,
				Data.Stamina, Data.MaxStamina,
				Data.Attack, Data.Defense,
			});
			using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
			f?.StoreString(json);
		}
		catch (Exception e) { GD.PushWarning($"[Player] Save lỗi: {e.Message}"); }
	}

	private void LoadData()
	{
		if (!FileAccess.FileExists(SavePath)) return;
		try
		{
			using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
			var doc = JsonDocument.Parse(f?.GetAsText() ?? "{}");
			var r = doc.RootElement;
			Data ??= new PlayerData();
			if (r.TryGetProperty("PlayerName", out var v)) Data.PlayerName = v.GetString() ?? Data.PlayerName;
			if (r.TryGetProperty("Level",      out v))     Data.Level      = v.GetInt32();
			if (r.TryGetProperty("Exp",        out v))     Data.Exp        = v.GetInt32();
			if (r.TryGetProperty("MaxExp",     out v))     Data.MaxExp     = v.GetInt32();
			if (r.TryGetProperty("Hp",         out v))     Data.Hp         = v.GetInt32();
			if (r.TryGetProperty("MaxHp",      out v))     Data.MaxHp      = v.GetInt32();
			if (r.TryGetProperty("Mana",       out v))     Data.Mana       = v.GetInt32();
			if (r.TryGetProperty("MaxMana",    out v))     Data.MaxMana    = v.GetInt32();
			if (r.TryGetProperty("Stamina",    out v))     Data.Stamina    = v.GetInt32();
			if (r.TryGetProperty("MaxStamina", out v))     Data.MaxStamina = v.GetInt32();
			if (r.TryGetProperty("Attack",     out v))     Data.Attack     = v.GetInt32();
			if (r.TryGetProperty("Defense",    out v))     Data.Defense    = v.GetInt32();
		}
		catch (Exception e) { GD.PushWarning($"[Player] Load lỗi: {e.Message}"); }
	}
}
