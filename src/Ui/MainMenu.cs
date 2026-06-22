using Godot;
using FragmentOfJapanese.Autoloads;

namespace FragmentOfJapanese.Ui;

public partial class MainMenu : Control
{
    [Export] private Button _btnNewGame;
    [Export] private Button _btnContinue;
    [Export] private Button _btnSettings;
    [Export] private Button _btnQuit;

    public override void _Ready()
    {
        _btnNewGame?.Connect(Button.SignalName.Pressed,  Callable.From(OnNewGame));
        _btnContinue?.Connect(Button.SignalName.Pressed, Callable.From(OnContinue));
        _btnSettings?.Connect(Button.SignalName.Pressed, Callable.From(OnSettings));
        _btnQuit?.Connect(Button.SignalName.Pressed,     Callable.From(OnQuit));

        // Ẩn nút Continue nếu chưa có save
        if (_btnContinue != null)
            _btnContinue.Disabled = !SaveSystem.Instance.HasSave();

        // Banner quảng cáo hiển thị ở menu (ẩn khi vào chơi)
        Ads.AdManager.Instance?.ShowBanner();
    }

    private void OnNewGame()
    {
        Ads.AdManager.Instance?.HideBanner();
        SceneTransition.Instance.GoTo("res://scenes/world/WorldMap.tscn");
    }
    private void OnContinue() { /* TODO: Load save rồi GoTo */ }
    private void OnSettings() => SceneTransition.Instance.GoTo("res://scenes/ui/SettingsMenu.tscn");
    private void OnQuit()     => GetTree().Quit();
}
