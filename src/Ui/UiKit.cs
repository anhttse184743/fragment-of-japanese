using Godot;
using FragmentOfJapanese.Core;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// Helper style/màu dùng chung cho UI dựng-bằng-code (shop, túi đồ...).
/// Gom màu + StyleBoxFlat + style nút + icon vật phẩm về 1 nơi để giao diện đồng nhất.
/// </summary>
public static class UiKit
{
    public const string GoldIconPath = "res://assets/sprites/items/gold.png";

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

        if (def != null && !string.IsNullOrEmpty(def.Icon) && ResourceLoader.Exists(def.Icon))
        {
            var tex = GD.Load<Texture2D>(def.Icon);
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
}
