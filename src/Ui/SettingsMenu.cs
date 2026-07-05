using Godot;
using FragmentOfJapanese.Autoloads;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// Menu cài đặt dạng Overlay (Lớp phủ) hiện trực tiếp trên màn hình đang chơi.
/// </summary>
public partial class SettingsMenu : Control
{
    private static SettingsMenu _instance;

    public static void ShowSettings()
    {
        if (Engine.GetMainLoop() is not SceneTree tree) return;
        if (_instance != null && GodotObject.IsInstanceValid(_instance)) return;

        var layer = new CanvasLayer { Layer = 150, Name = "SettingsLayer" };
        _instance = new SettingsMenu();
        layer.AddChild(_instance);
        tree.Root.AddChild(layer);
    }

    public override void _Ready()
    {
        BuildUi();
        GetViewport().SizeChanged += () => Size = GetViewport().GetVisibleRect().Size;
        
        Modulate = new Color(1, 1, 1, 0);
        CreateTween().TweenProperty(this, "modulate:a", 1f, 0.25f);
    }

    private void BuildUi()
    {
        var viewRect = GetViewport().GetVisibleRect();
        Position = Vector2.Zero;
        Size = viewRect.Size;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var bg = new ColorRect { Color = new Color(0.04f, 0.05f, 0.08f, 0.75f) };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Stop;
        AddChild(bg);
        
        bg.GuiInput += (e) => 
        {
            if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                Close();
        };

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        center.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(400, 380) };
        panel.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.18f, 0.12f, 0.09f, 0.98f), 24, UiKit.WoodTextDim, 2, 24, 24));
        center.AddChild(panel);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 24);
        panel.AddChild(vb);

        var title = new Label { Text = "CÀI ĐẶT", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 36);
        title.AddThemeColorOverride("font_color", UiKit.Accent);
        vb.AddChild(title);

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", UiKit.Fade(UiKit.WoodTextDim, 0.3f));
        vb.AddChild(sep);

        vb.AddChild(MkAudioRow("🎵 Nhạc nền (BGM)"));
        vb.AddChild(MkAudioRow("🔊 Hiệu ứng (SFX)"));

        var spacer = new Control { CustomMinimumSize = new Vector2(0, 16) };
        vb.AddChild(spacer);

        var btnLogout = new Button { Text = "ĐĂNG XUẤT", CustomMinimumSize = new Vector2(280, 60), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        btnLogout.FocusMode = FocusModeEnum.None;
        UiKit.StyleButton(btnLogout, new Color(0.6f, 0.2f, 0.2f, 1f), new Color(0.7f, 0.3f, 0.3f, 1f), new Color(0.5f, 0.15f, 0.15f, 1f), radius: 12);
        btnLogout.AddThemeFontSizeOverride("font_size", 22);
        btnLogout.Pressed += OnLogout;
        vb.AddChild(btnLogout);
        
        var btnClose = new Button { Text = "ĐÓNG", CustomMinimumSize = new Vector2(280, 50), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        btnClose.FocusMode = FocusModeEnum.None;
        UiKit.StyleButton(btnClose, UiKit.CardBg, UiKit.Fade(UiKit.Accent, 0.3f), UiKit.Accent, radius: 12);
        btnClose.AddThemeFontSizeOverride("font_size", 20);
        btnClose.Pressed += Close;
        vb.AddChild(btnClose);
    }

    private Control MkAudioRow(string labelText)
    {
        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 8);
        var lbl = new Label { Text = labelText };
        lbl.AddThemeFontSizeOverride("font_size", 20);
        lbl.AddThemeColorOverride("font_color", UiKit.WoodText);
        vb.AddChild(lbl);

        var slider = new HSlider();
        slider.CustomMinimumSize = new Vector2(320, 20);
        slider.Value = 50; 
        vb.AddChild(slider);
        return vb;
    }

    private void OnLogout()
    {
        AccountManager.Instance?.Logout();
        
        if (SceneTransition.Instance != null)
            SceneTransition.Instance.GoTo("res://scenes/ui/AuthScreen.tscn");
        else
            GetTree().ChangeSceneToFile("res://scenes/ui/AuthScreen.tscn");
            
        Close();
    }

    private void Close()
    {
        CreateTween().TweenProperty(this, "modulate:a", 0f, 0.15f)
            .Finished += () => 
            {
                if (GetParent() is CanvasLayer layer) layer.QueueFree();
                else QueueFree();
            };
    }
}
