using Godot;
using System;
using FragmentOfJapanese.World;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// Màn hình tổng kết (Victory Screen) hiện ra sau khi dọn sạch ải.
/// Khởi tạo động hoàn toàn bằng code, sử dụng UiKit cho đồng bộ theme.
/// </summary>
public partial class DungeonRewardUi : Control
{
    private DungeonController _dungeon;
    private int _gold;
    private int _ears;
    private int _kills;

    public static void ShowReward(DungeonController dungeon, int gold, int ears, int kills)
    {
        if (Engine.GetMainLoop() is not SceneTree tree) return;
        
        var layer = new CanvasLayer { Layer = 150, Name = "DungeonRewardLayer" };
        var ui = new DungeonRewardUi(dungeon, gold, ears, kills);
        layer.AddChild(ui);
        tree.Root.AddChild(layer);
    }

    private DungeonRewardUi(DungeonController dungeon, int gold, int ears, int kills)
    {
        _dungeon = dungeon;
        _gold = gold;
        _ears = ears;
        _kills = kills;
    }

    public override void _Ready()
    {
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

        // Header "Thắng Lợi"
        var title = new Label { Text = "⚔️ THẮNG LỢI ⚔️", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 42);
        title.AddThemeColorOverride("font_color", UiKit.Gold);
        vb.AddChild(title);

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", UiKit.Fade(UiKit.Gold, 0.4f));
        vb.AddChild(sep);
        
        var subTitle = new Label { Text = "Chiến lợi phẩm thu được:", HorizontalAlignment = HorizontalAlignment.Center };
        subTitle.AddThemeFontSizeOverride("font_size", 20);
        subTitle.AddThemeColorOverride("font_color", UiKit.WoodTextDim);
        vb.AddChild(subTitle);

        // Khu vực Stats (Grid)
        var statGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        statGrid.AddThemeConstantOverride("h_separation", 24);
        statGrid.AddThemeConstantOverride("v_separation", 16);
        vb.AddChild(statGrid);

        AddStatRow(statGrid, "💀 Quái hạ gục:", $"{_kills} con");
        AddStatRow(statGrid, "🪙 Vàng thu được:", $"+{_gold}");
        AddStatRow(statGrid, "👂 Tai Goblin:", $"+{_ears}");

        var spacer = new Control { CustomMinimumSize = new Vector2(0, 16) };
        vb.AddChild(spacer);

        // Nút Về Làng
        var btnBack = new Button { Text = "QUAY VỀ LÀNG", CustomMinimumSize = new Vector2(280, 60), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        UiKit.StyleButton(btnBack, new Color(0.2f, 0.5f, 0.3f, 1f), new Color(0.25f, 0.6f, 0.35f, 1f), new Color(0.15f, 0.4f, 0.25f, 1f), radius: 14);
        btnBack.AddThemeFontSizeOverride("font_size", 22);
        btnBack.Pressed += OnLeave;
        vb.AddChild(btnBack);
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

    private void OnLeave()
    {
        if (_dungeon != null && GodotObject.IsInstanceValid(_dungeon))
        {
            _dungeon.Leave();
        }
        
        if (GetParent() is CanvasLayer layer) 
            layer.QueueFree();
        else 
            QueueFree();
    }
}
