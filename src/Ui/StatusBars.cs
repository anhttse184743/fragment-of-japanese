using Godot;
using FragmentOfJapanese.Entities.Player;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// Khối chỉ số góc trên-trái: Level + thanh HP / Mana / Thể lực (+ thanh EXP mảnh).
/// Tự tìm player (group "player") rồi nối signal HpChanged / ManaChanged / StaminaChanged / ExpChanged.
/// Là 1 UI riêng — chỉnh sửa/đặt lại vị trí trong StatusBars.tscn mà không đụng phần còn lại của HUD.
/// </summary>
public partial class StatusBars : Control
{
    [Export] private Label              _levelLabel;
    [Export] private TextureProgressBar _expBar;
    [Export] private TextureProgressBar _hpBar;
    [Export] private Label              _hpLabel;
    [Export] private TextureProgressBar _manaBar;
    [Export] private Label              _manaLabel;
    [Export] private TextureProgressBar _staminaBar;
    [Export] private Label              _staminaLabel;

    private bool   _bound;
    private bool   _warned;
    private double _waited;

    // Player có thể được SPAWN sau HUD (PlayerSpawn) → thử lại mỗi frame tới khi thấy rồi mới bind.
    public override void _Ready() => SetProcess(true);

    public override void _Process(double delta)
    {
        if (_bound) return;

        if (GetTree().GetFirstNodeInGroup("player") is Player player)
        {
            Bind(player);
            _bound = true;
            SetProcess(false);
            return;
        }

        _waited += delta;
        if (_waited > 5d && !_warned)   // cảnh báo 1 lần nếu chờ lâu mà vẫn chưa có player
        {
            _warned = true;
            GD.PrintErr("[StatusBars] 5s rồi vẫn chưa thấy player — kiểm tra PlayerSpawn có spawn Player.tscn không.");
        }
    }

    private void Bind(Player player)
    {
        player.HpChanged      += UpdateHp;
        player.ManaChanged    += UpdateMana;
        player.StaminaChanged += UpdateStamina;
        player.ExpChanged     += UpdateExp;

        var d = player.Data;          // hiển thị giá trị ban đầu
        if (d != null)
        {
            UpdateHp(d.Hp, d.MaxHp);
            UpdateMana(d.Mana, d.MaxMana);
            UpdateStamina(d.Stamina, d.MaxStamina);
            UpdateExp(d.Exp, d.MaxExp, d.Level);
        }
    }

    // Icon đã cho biết là chỉ số gì → label chỉ cần "hiện tại/tối đa".
    public void UpdateHp(int current, int max)
    {
        if (_hpBar != null)   { _hpBar.MaxValue = max; _hpBar.Value = current; }
        if (_hpLabel != null)   _hpLabel.Text = $"{current}/{max}";
    }

    public void UpdateMana(int current, int max)
    {
        if (_manaBar != null)   { _manaBar.MaxValue = max; _manaBar.Value = current; }
        if (_manaLabel != null)   _manaLabel.Text = $"{current}/{max}";
    }

    public void UpdateStamina(int current, int max)
    {
        if (_staminaBar != null)   { _staminaBar.MaxValue = max; _staminaBar.Value = current; }
        if (_staminaLabel != null)   _staminaLabel.Text = $"{current}/{max}";
    }

    public void UpdateExp(int current, int max, int level)
    {
        if (_expBar != null)    { _expBar.MaxValue = max; _expBar.Value = current; }
        if (_levelLabel != null)  _levelLabel.Text = $"{level}";   // "LV" đã in sẵn trên khung
    }
}
