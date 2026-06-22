using Godot;
using FragmentOfJapanese.Autoloads;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// Menu cài đặt — bao gồm chọn ngôn ngữ (vi / en / ja).
/// </summary>
public partial class SettingsMenu : Control
{
    /// <summary>Scene để quay về khi bấm Quay lại. TopBar gán World; MainMenu gán MainMenu.</summary>
    public static string BackScene { get; set; } = "res://scenes/ui/MainMenu.tscn";

    [Export] private OptionButton _languageDropdown;
    [Export] private HSlider      _bgmSlider;
    [Export] private HSlider      _sfxSlider;
    [Export] private Button       _btnBack;

    public override void _Ready()
    {
        // Điền danh sách ngôn ngữ
        _languageDropdown?.Clear();
        foreach (var name in LocaleManager.LocaleDisplayNames)
            _languageDropdown?.AddItem(name);

        if (_languageDropdown != null)
        {
            _languageDropdown.Selected = LocaleManager.Instance.CurrentLocaleIndex;
            _languageDropdown.Connect(
                OptionButton.SignalName.ItemSelected,
                Callable.From<long>(OnLanguageChanged));
        }

        _btnBack?.Connect(Button.SignalName.Pressed, Callable.From(OnBack));
    }

    private void OnLanguageChanged(long index)
        => LocaleManager.Instance.SetLocale(LocaleManager.SupportedLocales[index]);

    private void OnBack()
        => SceneTransition.Instance.GoTo(BackScene);
}
