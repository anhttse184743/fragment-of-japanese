using Godot;

namespace FragmentOfJapanese.Autoloads;

/// <summary>
/// [KẾ HOẠCH] Quản lý ngôn ngữ hiển thị trong game.
/// Hỗ trợ: Tiếng Việt (vi), English (en), 日本語 (ja)
/// </summary>
public partial class LocaleManager : Node
{
    public static LocaleManager Instance { get; private set; }

    public static readonly string[] SupportedLocales      = { "vi", "en", "ja" };
    public static readonly string[] LocaleDisplayNames    = { "Tiếng Việt", "English", "日本語" };

    public string CurrentLocale => TranslationServer.GetLocale();

    public int CurrentLocaleIndex =>
        System.Array.IndexOf(SupportedLocales, CurrentLocale);

    public override void _Ready()
    {
        Instance = this;
        TranslationServer.SetLocale("vi"); // Mặc định: Tiếng Việt
    }

    public void SetLocale(string locale)
    {
        if (System.Array.IndexOf(SupportedLocales, locale) < 0) return;
        TranslationServer.SetLocale(locale);
        GD.Print($"[LocaleManager] Locale set → {locale}");
        // TODO: Lưu vào SaveSystem
    }
}
