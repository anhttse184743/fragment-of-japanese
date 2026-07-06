using Godot;
using FragmentOfJapanese.Autoloads;
using System.Collections.Generic;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// Giao diện dịch chuyển nhanh khi sử dụng Đá Dịch Chuyển.
/// Hiển thị danh sách các cổng trong map World để người chơi chọn.
/// </summary>
public partial class FastTravelUi : CanvasLayer
{
    private class Destination
    {
        public string Name { get; set; }
        public string TargetPortalName { get; set; }
    }

    private readonly List<Destination> _destinations = new()
    {
        new Destination { Name = "Về Làng (Điểm Hồi Sinh)", TargetPortalName = "" },
        new Destination { Name = "Cổng Thánh Tích", TargetPortalName = "Relics_Portal" },
        new Destination { Name = "Cổng Đỉnh Núi", TargetPortalName = "Peak_Portal" },
        new Destination { Name = "Cổng Khu Rừng", TargetPortalName = "Forest_Portal" },
        new Destination { Name = "Cổng Lâu Đài", TargetPortalName = "Castle_Portal" }
    };

    public override void _Ready()
    {
        Layer = 20; // Nằm trên cả InventoryUi (Layer 10)

        var root = new Control();
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.75f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(400, 0) };
        var boxStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.14f, 0.15f, 0.18f, 0.98f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            BorderColor = new Color(0.28f, 0.30f, 0.35f, 1f),
            CornerRadiusTopLeft = 20, CornerRadiusTopRight = 20, CornerRadiusBottomRight = 20, CornerRadiusBottomLeft = 20,
            ShadowColor = new Color(0, 0, 0, 0.5f), ShadowSize = 12
        };
        panel.AddThemeStyleboxOverride("panel", boxStyle);
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 16);
        margin.AddChild(vbox);

        var title = new Label { Text = "Dịch Chuyển Nhanh", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 28);
        title.AddThemeColorOverride("font_color", new Color(0.96f, 0.85f, 0.45f));
        vbox.AddChild(title);

        var sep = new HSeparator();
        sep.Modulate = new Color(1, 1, 1, 0.1f);
        vbox.AddChild(sep);

        foreach (var dest in _destinations)
        {
            var btn = new Button { Text = dest.Name, CustomMinimumSize = new Vector2(0, 50) };
            ApplyButtonStyle(btn, new Color(0.2f, 0.3f, 0.5f, 1f));
            btn.Pressed += () => OnDestinationSelected(dest.TargetPortalName);
            vbox.AddChild(btn);
        }

        var cancelBtn = new Button { Text = "Hủy (Mất vật phẩm)", CustomMinimumSize = new Vector2(0, 50) };
        ApplyButtonStyle(cancelBtn, new Color(0.5f, 0.2f, 0.2f, 1f));
        cancelBtn.Pressed += Close;
        vbox.AddChild(cancelBtn);
    }

    private void ApplyButtonStyle(Button btn, Color baseColor)
    {
        var normal = new StyleBoxFlat { BgColor = baseColor, CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomRight = 12, CornerRadiusBottomLeft = 12 };
        var hover = new StyleBoxFlat { BgColor = baseColor.Lightened(0.2f), CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomRight = 12, CornerRadiusBottomLeft = 12 };
        var pressed = new StyleBoxFlat { BgColor = baseColor.Darkened(0.2f), CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomRight = 12, CornerRadiusBottomLeft = 12 };

        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", pressed);
        btn.AddThemeStyleboxOverride("focus", normal);
        btn.AddThemeFontSizeOverride("font_size", 20);
    }

    private void OnDestinationSelected(string targetPortal)
    {
        if (InventoryUi.Instance != null && InventoryUi.Instance.Visible)
            InventoryUi.Instance.Close();

        if (string.IsNullOrEmpty(targetPortal))
        {
            SceneTransition.ForceDefaultArrival = true;
            SceneTransition.TargetPortalName = null;
        }
        else
        {
            SceneTransition.ForceDefaultArrival = false;
            SceneTransition.TargetPortalName = targetPortal;
        }

        SceneTransition.Instance.GoTo("res://scenes/world/World.tscn");
        
        Close();
    }

    private void Close()
    {
        QueueFree();
    }
}
