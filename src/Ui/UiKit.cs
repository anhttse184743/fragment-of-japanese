using Godot;
using FragmentOfJapanese.Core;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// Helper style/màu dùng chung cho UI dựng-bằng-code (shop, túi đồ...).
/// Gom màu + StyleBoxFlat + style nút + icon vật phẩm về 1 nơi để giao diện đồng nhất.
/// </summary>
public static class UiKit
{
    public const string GoldIconPath  = "res://assets/sprites/items/gold.png";
    public const string AetherIconPath = "res://assets/sprites/items/aetherstone.png";
    public const string NoIconPath     = "res://assets/sprites/items/no_icon.png";

    // ----- Bảng màu -----
    public static readonly Color Gold     = new(1.00f, 0.82f, 0.30f);
    public static readonly Color MaThach  = new(0.62f, 0.50f, 1.00f);
    public static readonly Color PanelBg  = new(0.11f, 0.12f, 0.16f, 0.98f);
    public static readonly Color CardBg   = new(0.16f, 0.17f, 0.22f);
    public static readonly Color HeaderBg = new(0.07f, 0.08f, 0.11f);
    public static readonly Color Accent   = new(0.85f, 0.68f, 0.35f);   // vàng đồng
    public static readonly Color BuyGreen = new(0.22f, 0.55f, 0.32f);
    public static readonly Color BuyGreenHi = new(0.28f, 0.68f, 0.40f);
    public static readonly Color TextDim  = new(0.62f, 0.64f, 0.72f);

    // ----- Tông nâu da (túi đồ) -----
    public static readonly Color BrownPanel   = new(0.64f, 0.50f, 0.36f);   // nâu da chủ đạo
    public static readonly Color BrownDark    = new(0.38f, 0.27f, 0.17f);   // header / bảng chi tiết
    public static readonly Color BrownSlot    = new(0.50f, 0.38f, 0.26f);   // ô vật phẩm
    public static readonly Color BrownBorder  = new(0.28f, 0.19f, 0.11f);
    public static readonly Color BrownText    = new(0.97f, 0.93f, 0.85f);   // kem
    public static readonly Color BrownTextDim = new(0.86f, 0.79f, 0.68f);

    // ----- Tông nâu gỗ (shop) -----
    public static readonly Color WoodPanel   = new(0.38f, 0.26f, 0.15f);   // gỗ chủ đạo
    public static readonly Color WoodDark    = new(0.24f, 0.16f, 0.09f);   // sidebar / thanh / pill
    public static readonly Color WoodCard    = new(0.47f, 0.34f, 0.21f);   // card / banner
    public static readonly Color WoodBorder  = new(0.16f, 0.10f, 0.05f);
    public static readonly Color WoodText    = new(0.97f, 0.91f, 0.79f);   // kem
    public static readonly Color WoodTextDim = new(0.80f, 0.71f, 0.57f);

    /// <summary>Màu nhấn theo nhóm vật phẩm — tạo sự đa dạng thị giác.</summary>
    public static Color TypeColor(ItemType t) => t switch
    {
        ItemType.Consumable => new Color(0.36f, 0.72f, 0.46f),
        ItemType.Key        => new Color(0.88f, 0.72f, 0.32f),
        ItemType.Trade      => new Color(0.42f, 0.62f, 0.88f),
        ItemType.Equipment  => new Color(0.78f, 0.46f, 0.86f),
        _                   => new Color(0.50f, 0.50f, 0.55f),
    };

    public static string TypeName(ItemType t) => t switch
    {
        ItemType.Consumable => "TIÊU HAO",
        ItemType.Key        => "CHÌA KHÓA",
        ItemType.Trade      => "ĐỔI / BÁN",
        ItemType.Equipment  => "TRANG BỊ",
        _                   => "",
    };

    public static Color RarityColor(Rarity r) => r switch
    {
        Rarity.Rare      => new Color(0.35f, 0.62f, 0.95f),
        Rarity.Epic      => new Color(0.72f, 0.42f, 0.92f),
        Rarity.Legendary => new Color(1.00f, 0.74f, 0.25f),
        _                => new Color(0.78f, 0.78f, 0.82f),
    };

    public static string RarityName(Rarity r) => r switch
    {
        Rarity.Rare      => "HIẾM",
        Rarity.Epic      => "SỬ THI",
        Rarity.Legendary => "HUYỀN THOẠI",
        _                => "THƯỜNG",
    };

    /// <summary>StyleBoxFlat bo góc, tùy chọn viền + content margin.</summary>
    public static StyleBoxFlat Box(Color bg, int radius = 10, Color? border = null, int borderW = 0,
                                   int marginH = 0, int marginV = 0)
    {
        var sb = new StyleBoxFlat { BgColor = bg };
        sb.SetCornerRadiusAll(radius);
        if (border.HasValue && borderW > 0)
        {
            sb.BorderColor = border.Value;
            sb.SetBorderWidthAll(borderW);
        }
        if (marginH > 0) { sb.ContentMarginLeft = marginH; sb.ContentMarginRight  = marginH; }
        if (marginV > 0) { sb.ContentMarginTop  = marginV; sb.ContentMarginBottom = marginV; }
        return sb;
    }

    /// <summary>Tô màu 4 trạng thái cho 1 Button (bo góc, bỏ viền focus).</summary>
    public static Button StyleButton(Button b, Color normal, Color hover, Color pressed, Color? disabled = null)
    {
        b.AddThemeStyleboxOverride("normal",        Box(normal, 8));
        b.AddThemeStyleboxOverride("hover",         Box(hover, 8));
        b.AddThemeStyleboxOverride("pressed",       Box(pressed, 8));
        b.AddThemeStyleboxOverride("hover_pressed", Box(pressed, 8));
        b.AddThemeStyleboxOverride("disabled",      Box(disabled ?? new Color(0.20f, 0.21f, 0.25f), 8));
        b.AddThemeStyleboxOverride("focus",         Box(new Color(0, 0, 0, 0), 8));
        b.AddThemeColorOverride("font_color",          Colors.White);
        b.AddThemeColorOverride("font_hover_color",    Colors.White);
        b.AddThemeColorOverride("font_pressed_color",  Colors.White);
        b.AddThemeColorOverride("font_disabled_color", new Color(0.55f, 0.55f, 0.60f));
        return b;
    }

    /// <summary>Icon vật phẩm: dùng sprite nếu có, không thì ô vuông màu nhóm + chữ cái đầu tên.</summary>
    public static Control ItemIcon(ItemEntry def, Color accent, int size = 64)
    {
        var holder = new CenterContainer { CustomMinimumSize = new Vector2(0, size + 2) };

        // Dùng icon riêng nếu có, không thì fallback no_icon.png
        string path = def != null && !string.IsNullOrEmpty(def.Icon) ? def.Icon : NoIconPath;
        if (ResourceLoader.Exists(path))
        {
            var tex = GD.Load<Texture2D>(path);
            if (tex != null)
            {
                holder.AddChild(new TextureRect
                {
                    Texture           = tex,
                    CustomMinimumSize = new Vector2(size, size),
                    StretchMode       = TextureRect.StretchModeEnum.KeepAspectCentered,
                    ExpandMode        = TextureRect.ExpandModeEnum.IgnoreSize,
                });
                return holder;
            }
        }

        // Fallback cuối (khi cả no_icon.png cũng thiếu): ô màu + chữ cái đầu
        var box = new PanelContainer { CustomMinimumSize = new Vector2(size, size) };
        box.AddThemeStyleboxOverride("panel", Box(Fade(accent, 0.22f), 10, accent, 2));

        var letter = new Label
        {
            Text                = string.IsNullOrEmpty(def?.NameVi) ? "?" : def.NameVi.Substring(0, 1).ToUpper(),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
        };
        letter.AddThemeFontSizeOverride("font_size", (int)(size * 0.45f));
        letter.AddThemeColorOverride("font_color", Colors.White);
        box.AddChild(letter);
        holder.AddChild(box);
        return holder;
    }

    /// <summary>Trả về bản sao màu với alpha mới (cho nền pill/placeholder).</summary>
    public static Color Fade(Color c, float alpha)
    {
        c.A = alpha;
        return c;
    }

    /// <summary>
    /// Hiện thông báo ngắn (toast) căn giữa, hơi trên đáy màn hình rồi tự mờ dần.
    /// Tiện cho nút placeholder ("sắp có") — gọi <c>UiKit.Toast(this, "...")</c> từ một Control phủ-toàn-màn.
    /// </summary>
    public static void Toast(Control host, string msg, float seconds = 1.4f)
    {
        if (host == null || !GodotObject.IsInstanceValid(host)) return;

        var label = new Label
        {
            Text                = msg,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            MouseFilter         = Control.MouseFilterEnum.Ignore,
        };
        label.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        label.OffsetTop    = -180;
        label.OffsetBottom = -130;
        label.AddThemeColorOverride("font_color", Accent);
        label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
        label.AddThemeConstantOverride("outline_size", 6);
        label.AddThemeFontSizeOverride("font_size", 18);
        host.AddChild(label);

        var tw = label.CreateTween();
        tw.TweenInterval(seconds);
        tw.TweenProperty(label, "modulate:a", 0f, 0.4);
        tw.TweenCallback(Callable.From(label.QueueFree));
    }
}
