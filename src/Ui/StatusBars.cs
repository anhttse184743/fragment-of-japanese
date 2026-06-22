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

    public override void _Ready() => CallDeferred(nameof(Bind));

    private void Bind()
    {
        if (GetTree().GetFirstNodeInGroup("player") is not Player player)
        {
            GD.PrintErr("[StatusBars] Không tìm thấy player (group \"player\").");
            return;
        }

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
