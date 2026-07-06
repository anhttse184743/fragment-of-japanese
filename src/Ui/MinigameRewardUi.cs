using Godot;
using System;
using FragmentOfJapanese.Items;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// Màn hình tổng kết hiện ra sau khi chiến thắng Mini-game (Nghe/Nói).
/// Tương tự DungeonRewardUi, thưởng 2-3 cuộn ma lực (cuộn từ vựng).
/// </summary>
public partial class MinigameRewardUi : Control
{
    private Action _onClose;
    private int _scrolls;
    private int _gold;
    private bool _isVictory;
    private string _titleText;

    public static void ShowReward(string titleText, bool isVictory, Action onClose)
    {
        if (Engine.GetMainLoop() is not SceneTree tree) return;
        
        var layer = new CanvasLayer { Layer = 200, Name = "MinigameRewardLayer" };
        
        int scrolls = isVictory ? new Random().Next(2, 4) : 0; 
        int gold = isVictory ? 200 : 50;
        
        if (Inventory.Instance != null && scrolls > 0)
        {
            _ = Inventory.Instance.GrantAsync("item_scroll", scrolls);
        }
        if (Wallet.Instance != null)
        {
            Wallet.Instance.AddGold(gold);
            _ = Wallet.Instance.SyncAsync();
        }

        var ui = new MinigameRewardUi(titleText, scrolls, gold, isVictory, onClose);
        layer.AddChild(ui);
        tree.Root.AddChild(layer);
    }

    private MinigameRewardUi(string titleText, int scrolls, int gold, bool isVictory, Action onClose)
    {
        _titleText = titleText;
        _scrolls = scrolls;
        _gold = gold;
        _isVictory = isVictory;
        _onClose = onClose;
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always; // Cho phép chạy ngay cả khi game đang Paused
        BuildUi();
        GetViewport().SizeChanged += () => Size = GetViewport().GetVisibleRect().Size;
        
        // Hiệu ứng Fade in cho cảm giác "chiến thắng"
        Modulate = new Color(1, 1, 1, 0);
        CreateTween().TweenProperty(this, "modulate:a", 1f, 0.4f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    private void BuildUi()
    {
        var viewRect = GetViewport().GetVisibleRect();
        Position = Vector2.Zero;
        Size = viewRect.Size;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // Nền mờ toàn màn hình
        var bg = new ColorRect { Color = new Color(0.04f, 0.05f, 0.08f, 0.90f) };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Stop;
        AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(550, 480) };
        panel.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.18f, 0.12f, 0.09f, 0.98f), 24, UiKit.Gold, 4, 32, 40));
        center.AddChild(panel);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 28);
        panel.AddChild(vb);

        // Header "Chiến Lợi Phẩm"
        string headerText = _isVictory ? $"✨ {_titleText} ✨" : $"💦 {_titleText} (Khích lệ) 💦";
        var title = new Label { Text = headerText, HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 36);
        title.AddThemeColorOverride("font_color", _isVictory ? UiKit.Gold : new Color(0.7f, 0.7f, 0.7f));
        vb.AddChild(title);

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", UiKit.Fade(UiKit.Gold, 0.4f));
        vb.AddChild(sep);
        
        var subTitle = new Label { Text = "Phần thưởng nhận được:", HorizontalAlignment = HorizontalAlignment.Center };
        subTitle.AddThemeFontSizeOverride("font_size", 20);
        subTitle.AddThemeColorOverride("font_color", UiKit.WoodTextDim);
        vb.AddChild(subTitle);

        // Khu vực Stats (Grid)
        var statGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        statGrid.AddThemeConstantOverride("h_separation", 24);
        statGrid.AddThemeConstantOverride("v_separation", 16);
        vb.AddChild(statGrid);

        AddStatRow(statGrid, "💰 Vàng:", $"+{_gold}");
        if (_scrolls > 0)
        {
            AddStatRow(statGrid, "📜 Cuộn Ma Lực:", $"+{_scrolls}");
        }

        var spacer = new Control { CustomMinimumSize = new Vector2(0, 16) };
        vb.AddChild(spacer);

        // Nút Tiếp tục
        var btnContinue = new Button { Text = "TIẾP TỤC", CustomMinimumSize = new Vector2(280, 60), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        UiKit.StyleButton(btnContinue, new Color(0.2f, 0.5f, 0.3f, 1f), new Color(0.25f, 0.6f, 0.35f, 1f), new Color(0.15f, 0.4f, 0.25f, 1f), radius: 14);
        btnContinue.AddThemeFontSizeOverride("font_size", 22);
        btnContinue.Pressed += OnContinue;
        vb.AddChild(btnContinue);
    }

    private void AddStatRow(GridContainer grid, string label, string value)
    {
        var lbl = new Label { Text = label, HorizontalAlignment = HorizontalAlignment.Right };
        lbl.AddThemeFontSizeOverride("font_size", 24);
        lbl.AddThemeColorOverride("font_color", UiKit.WoodTextDim);
        grid.AddChild(lbl);

        var val = new Label { Text = value, HorizontalAlignment = HorizontalAlignment.Left };
        val.AddThemeFontSizeOverride("font_size", 26);
        val.AddThemeColorOverride("font_color", UiKit.BuyGreenHi);
        grid.AddChild(val);
    }

    private void OnContinue()
    {
        // Gọi callback đóng minigame
        _onClose?.Invoke();
        
        // Tự hủy
        if (GetParent() is CanvasLayer layer) 
            layer.QueueFree();
        else 
            QueueFree();
    }
}
