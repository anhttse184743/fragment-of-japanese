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

		_ = InitFromServerAsync();
	}

	private async System.Threading.Tasks.Task InitFromServerAsync()
	{
		await SyncFromServerAsync();   // Level/Exp/MaxExp
		await LoadVitalsAsync();       // Máu/Mana/Thể lực đã lưu (bền qua phiên)
	}

	// ───── Máu/Mana/Thể lực bền qua phiên (lưu server) ─────
	private int _lastSavedHp = -1, _lastSavedMana = -1, _lastSavedStamina = -1;
	private float _vitalsSaveTimer;

	private class VitalsDto { public int? Hp { get; set; } public int? Mana { get; set; } public int? Stamina { get; set; } }

	/// <summary>Nạp Máu/Mana/Thể lực đã lưu; null = giữ đầy (mặc định).</summary>
	private async System.Threading.Tasks.Task LoadVitalsAsync()
	{
		if (Data == null || string.IsNullOrEmpty(Autoloads.ApiClient.Instance.AccessToken)) return;
		var res = await Autoloads.ApiClient.Instance.GetAsync("/api/player/vitals");
		if (!res.IsSuccessStatusCode) return;
		var data = await Autoloads.ApiClient.Instance.ReadAsAsync<Autoloads.AccountManager.ApiResponse<VitalsDto>>(res);
		if (data?.Data == null) return;

		if (data.Data.Hp.HasValue)      Data.Hp      = Mathf.Clamp(data.Data.Hp.Value,      1, Data.MaxHp);
		if (data.Data.Mana.HasValue)    Data.Mana    = Mathf.Clamp(data.Data.Mana.Value,    0, Data.MaxMana);
		if (data.Data.Stamina.HasValue) Data.Stamina = Mathf.Clamp(data.Data.Stamina.Value, 0, Data.MaxStamina);

		_lastSavedHp = Data.Hp; _lastSavedMana = Data.Mana; _lastSavedStamina = Data.Stamina;
		EmitSignal(SignalName.HpChanged,      Data.Hp,      Data.MaxHp);
		EmitSignal(SignalName.ManaChanged,    Data.Mana,    Data.MaxMana);
		EmitSignal(SignalName.StaminaChanged, Data.Stamina, Data.MaxStamina);
	}

	/// <summary>Lưu Máu/Mana/Thể lực hiện tại lên server (bền qua phiên).</summary>
	public async System.Threading.Tasks.Task SaveVitalsAsync()
	{
		if (Data == null || string.IsNullOrEmpty(Autoloads.ApiClient.Instance.AccessToken)) return;
		_lastSavedHp = Data.Hp; _lastSavedMana = Data.Mana; _lastSavedStamina = Data.Stamina;
		await Autoloads.ApiClient.Instance.PostAsync("/api/player/vitals",
			new { Hp = Data.Hp, Mana = Data.Mana, Stamina = Data.Stamina });
	}

	private void OnItemUsed(ItemEntry item)
	{
		switch (item.Effect)
		{
			case "heal":          Heal(item.Value);          break;
			case "mana":
			case "heal_mana":     RestoreMana(item.Value);    break;
			case "heal_stamina":  RestoreStamina(item.Value); break;
			case "teleport":      OpenFastTravel();           break;
		}
	}

	/// <summary>Mở giao diện Fast Travel để dịch chuyển giữa các map.</summary>
	private void OpenFastTravel()
	{
		var ui = new FragmentOfJapanese.Ui.FastTravelUi();
		GetTree().Root.AddChild(ui);
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
				Data.Level  = data.Data.Level;
				Data.Exp    = data.Data.Exp;
				if (data.Data.MaxExp > 0) Data.MaxExp = data.Data.MaxExp;   // server là nguồn sự thật
				EmitSignal(SignalName.ExpChanged, Data.Exp, Data.MaxExp, Data.Level);
			}
		}
	}

	private class PlayerProfileDto
	{
		public int Level { get; set; }
		public int Exp { get; set; }
		public int MaxExp { get; set; }
	}

	public override void _ExitTree()
	{
		if (Inventory.Instance != null) Inventory.Instance.ItemUsed -= OnItemUsed;
		SaveData();
		_ = SaveVitalsAsync();   // best-effort lưu máu/mana/thể lực khi rời scene
	}

	private float _sinceDamage;
	private float _regenTimer;
	private float _manaRegenTimer;

	public override void _Process(double delta)
	{
		if (Data == null) return;

		// Lưu Máu/Mana/Thể lực định kỳ (5s) nếu có thay đổi → bền qua phiên, không hồi đầy khi relog.
		_vitalsSaveTimer += (float)delta;
		if (_vitalsSaveTimer >= 5f)
		{
			_vitalsSaveTimer = 0f;
			if (Data.Hp != _lastSavedHp || Data.Mana != _lastSavedMana || Data.Stamina != _lastSavedStamina)
				_ = SaveVitalsAsync();
		}

		if (Data.Hp <= 0) return;

		// Hồi máu khi không bị đánh đủ lâu
		if (Data.Hp < Data.MaxHp)
		{
			_sinceDamage += (float)delta;
			if (_sinceDamage >= RegenDelay)
			{
				_regenTimer += (float)delta;
				if (_regenTimer >= RegenTick)
				{
					_regenTimer = 0f;
					Heal(RegenAmount);
				}
			}
		}

		// Hồi Mana mỗi 30s (2-5 mana)
		if (Data.Mana < Data.MaxMana)
		{
			_manaRegenTimer += (float)delta;
			if (_manaRegenTimer >= 30f)
			{
				_manaRegenTimer = 0f;
				RestoreMana((int)(GD.Randi() % 4) + 2); // 2..5
			}
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

	/// <summary>Cộng EXP: server quyết định lên cấp (nguồn sự thật duy nhất), client chỉ hiển thị lại sau sync.</summary>
	public void GainExp(int amount)
	{
		if (amount <= 0) return;
		int before = Data.Level;
		_ = AwardAsync(amount, 0, before);   // server cộng + lên cấp → SyncFromServer lấy Level/Exp/MaxExp thật
	}

	/// <summary>Gửi phần thưởng exp/gold lên server (có trần) rồi đồng bộ lại cấp/EXP + ví.</summary>
	public async System.Threading.Tasks.Task AwardAsync(int exp, int gold, int levelBefore = -1)
	{
		if (exp <= 0 && gold <= 0) return;
		if (string.IsNullOrEmpty(Autoloads.ApiClient.Instance.AccessToken)) return;

		var res = await Autoloads.ApiClient.Instance.PostAsync("/api/player/reward", new { Exp = exp, Gold = gold });
		if (!res.IsSuccessStatusCode) return;

		if (exp > 0)
		{
			await SyncFromServerAsync();                                  // Level/Exp/MaxExp thật từ server
			if (levelBefore >= 0 && Data.Level > levelBefore) EmitSignal(SignalName.LeveledUp);
		}
		if (gold > 0)
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
