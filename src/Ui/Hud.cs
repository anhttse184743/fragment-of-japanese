using Godot;

namespace FragmentOfJapanese.Ui;

public partial class Hud : Control
{
    [Export] private ProgressBar _hpBar;
    [Export] private ProgressBar _expBar;
    [Export] private Label       _levelLabel;
    [Export] private Label       _hpLabel;

    public void UpdateHp(int current, int max)
    {
        if (_hpBar != null)  { _hpBar.MaxValue = max; _hpBar.Value = current; }
        if (_hpLabel != null)  _hpLabel.Text = $"{current}/{max}";
    }

    public void UpdateExp(int current, int max, int level)
    {
        if (_expBar != null)   { _expBar.MaxValue = max; _expBar.Value = current; }
        if (_levelLabel != null) _levelLabel.Text = $"Lv.{level}";
    }
}
