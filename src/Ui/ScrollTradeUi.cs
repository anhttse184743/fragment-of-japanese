using Godot;
using System;
using FragmentOfJapanese.Items;
using FragmentOfJapanese.Autoloads;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// Giao diện đổi Cuộn Từ Vựng lấy Chìa khóa Bạc/Vàng.
/// Mở thông qua NPC Thương Nhân.
/// </summary>
public partial class ScrollTradeUi : Control
{
    private Label _scrollCountLabel;

    public static void Open()
    {
        if (Engine.GetMainLoop() is not SceneTree tree) return;
        var layer = new CanvasLayer { Layer = 150, Name = "ScrollTradeLayer" };
        var ui = new ScrollTradeUi();
        layer.AddChild(ui);
        tree.Root.AddChild(layer);
    }

    public override void _Ready()
    {
        BuildUi();
        GetViewport().SizeChanged += () => Size = GetViewport().GetVisibleRect().Size;
        
        // Hiệu ứng Fade in
        Modulate = new Color(1, 1, 1, 0);
        CreateTween().TweenProperty(this, "modulate:a", 1f, 0.3f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);

        RefreshCounts();
    }

    private void BuildUi()
    {
        var viewRect = GetViewport().GetVisibleRect();
        Position = Vector2.Zero;
        Size = viewRect.Size;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // Nền mờ để focus vào bảng
        var bg = new ColorRect { Color = new Color(0.05f, 0.05f, 0.08f, 0.85f) };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Stop;
        AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(560, 480) };
        panel.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.18f, 0.12f, 0.09f, 0.98f), 24, UiKit.Accent, 3, 24, 32));
        center.AddChild(panel);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 24);
        panel.AddChild(vb);

        // Header
        var title = new Label { Text = "✨ TRAO ĐỔI VẬT PHẨM ✨", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 32);
        title.AddThemeColorOverride("font_color", UiKit.Accent);
        vb.AddChild(title);

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", UiKit.Fade(UiKit.Accent, 0.4f));
        vb.AddChild(sep);

        // Hiển thị số cuộn từ vựng đang có
        var countHb = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        countHb.AddThemeConstantOverride("separation", 12);
        
        var countText = new Label { Text = "Cuộn Từ Vựng hiện có:", VerticalAlignment = VerticalAlignment.Center };
        countText.AddThemeFontSizeOverride("font_size", 20);
        countText.AddThemeColorOverride("font_color", new Color(0.85f, 0.8f, 0.7f));
        countHb.AddChild(countText);

        // Icon cuộn từ vựng
        var defScroll = ItemDatabase.Instance?.Get("item_scroll");
        if (defScroll != null)
        {
            var iconBox = UiKit.ItemIcon(defScroll, UiKit.MaThach, 48);
            countHb.AddChild(iconBox);
        }

        _scrollCountLabel = new Label { Text = "0", VerticalAlignment = VerticalAlignment.Center };
        _scrollCountLabel.AddThemeFontSizeOverride("font_size", 24);
        _scrollCountLabel.AddThemeColorOverride("font_color", UiKit.BuyGreenHi);
        countHb.AddChild(_scrollCountLabel);
        
        vb.AddChild(countHb);

        // Khung lựa chọn giao dịch
        var tradeGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        tradeGrid.AddThemeConstantOverride("h_separation", 24);
        tradeGrid.AddThemeConstantOverride("v_separation", 24);
        vb.AddChild(tradeGrid);

        tradeGrid.AddChild(MakeTradeCard("Chìa Khóa Bạc", "item_silver_key", 3, new Color(0.7f, 0.75f, 0.8f)));
        tradeGrid.AddChild(MakeTradeCard("Chìa Khóa Vàng", "item_golden_key", 10, UiKit.Gold));

        var spacer = new Control { CustomMinimumSize = new Vector2(0, 16) };
        vb.AddChild(spacer);

        // Nút Đóng
        var btnClose = new Button { Text = "ĐÓNG", CustomMinimumSize = new Vector2(240, 56), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        UiKit.StyleButton(btnClose, new Color(0.7f, 0.25f, 0.25f), new Color(0.85f, 0.35f, 0.35f), new Color(0.55f, 0.15f, 0.15f), radius: 14);
        btnClose.AddThemeFontSizeOverride("font_size", 20);
        btnClose.Pressed += OnClose;
        vb.AddChild(btnClose);
    }

    private Control MakeTradeCard(string title, string keyId, int cost, Color color)
    {
        var card = new PanelContainer { CustomMinimumSize = new Vector2(240, 260) };
        card.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.24f, 0.17f, 0.13f, 0.9f), 16, color, 2, 16, 16));

        var vb = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        vb.AddThemeConstantOverride("separation", 16);
        card.AddChild(vb);

        // Tên chìa khóa
        var name = new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Center };
        name.AddThemeFontSizeOverride("font_size", 22);
        name.AddThemeColorOverride("font_color", color);
        vb.AddChild(name);

        // Icon chìa khóa
        var defKey = ItemDatabase.Instance?.Get(keyId);
        if (defKey != null)
        {
            var iconBox = UiKit.ItemIcon(defKey, color, 80);
            vb.AddChild(iconBox);
        }

        // Nút bấm đổi
        var btnTrade = new Button { CustomMinimumSize = new Vector2(0, 56) };
        UiKit.StyleButton(btnTrade, new Color(0.2f, 0.5f, 0.3f, 1f), new Color(0.25f, 0.6f, 0.35f, 1f), new Color(0.15f, 0.4f, 0.25f, 1f), radius: 12);
        
        var btnHb = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        btnHb.AddThemeConstantOverride("separation", 8);
        btnHb.SetAnchorsPreset(LayoutPreset.FullRect);

        var lbl = new Label { Text = $"Đổi ({cost}", VerticalAlignment = VerticalAlignment.Center };
        lbl.AddThemeFontSizeOverride("font_size", 20);
        btnHb.AddChild(lbl);

        var defScroll = ItemDatabase.Instance?.Get("item_scroll");
        if (defScroll != null)
        {
            var miniIcon = UiKit.ItemIcon(defScroll, UiKit.MaThach, 28);
            btnHb.AddChild(miniIcon);
        }

        var lbl2 = new Label { Text = ")", VerticalAlignment = VerticalAlignment.Center };
        lbl2.AddThemeFontSizeOverride("font_size", 20);
        btnHb.AddChild(lbl2);

        btnTrade.AddChild(btnHb);
        // Ngăn HBox chặn chuột của Button
        foreach (Node child in btnHb.GetChildren())
        {
            if (child is Control c) c.MouseFilter = MouseFilterEnum.Ignore;
        }
        btnHb.MouseFilter = MouseFilterEnum.Ignore;

        btnTrade.Pressed += () => OnTrade(keyId, cost);
        vb.AddChild(btnTrade);

        return card;
    }

    private void RefreshCounts()
    {
        if (Inventory.Instance != null && _scrollCountLabel != null)
        {
            int scrolls = Inventory.Instance.CountOf("item_scroll");
            _scrollCountLabel.Text = $"{scrolls:N0}";
        }
    }

    private async void OnTrade(string keyId, int cost)
    {
        if (Inventory.Instance == null) return;

        int scrolls = Inventory.Instance.CountOf("item_scroll");
        if (scrolls < cost)
        {
            UiKit.GlobalToast($"Không đủ cuộn! Cần {cost} cuộn từ vựng.", 2f);
            return;
        }

        // Server trừ cuộn + cộng chìa nguyên tử (chống đổi vô hạn).
        var (ok, error) = await Inventory.Instance.ExchangeScrollsAsync(keyId);
        UiKit.GlobalToast(ok ? "Trao đổi thành công!" : (error ?? "Trao đổi thất bại."), 2f);
        RefreshCounts();
    }

    private void OnClose()
    {
        if (GetParent() is CanvasLayer layer) 
            layer.QueueFree();
        else 
            QueueFree();
    }
}
