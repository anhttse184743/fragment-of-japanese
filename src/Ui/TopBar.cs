using Godot;
using FragmentOfJapanese.Autoloads;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// Hàng nút góc trên-phải (icon): SHOP / BAG (túi đồ) / QUEST (nhiệm vụ) / OPTS (cài đặt).
/// Mỗi nút là 1 icon trong assets/sprites/ui (flat, expand_icon). SHOP/BAG/QUEST mở các
/// autoload tương ứng (y như phím P / I). OPTS mở màn cài đặt.
/// Là 1 UI riêng: sửa icon/bố cục trong TopBar.tscn.
/// </summary>
public partial class TopBar : Control
{
    [Export] private Button _shopBtn;
    [Export] private Button _bagBtn;
    [Export] private Button _questBtn;
    [Export] private Button _optsBtn;

    public override void _Ready()
    {
        if (_shopBtn  != null) _shopBtn.Pressed  += () => ShopUi.Instance?.Toggle();
        if (_bagBtn   != null) _bagBtn.Pressed   += () => InventoryUi.Instance?.Toggle();
        if (_questBtn != null) _questBtn.Pressed += () => QuestUi.Instance?.Toggle();
        if (_optsBtn  != null) _optsBtn.Pressed  += OnOpts;

        StyleTopBtn(_shopBtn);
        StyleTopBtn(_bagBtn);
        StyleTopBtn(_questBtn);
        StyleTopBtn(_optsBtn);
    }

    private void StyleTopBtn(Button btn)
    {
        if (btn == null) return;
        
        btn.FocusMode = FocusModeEnum.None;
        
        var normalBg = new Color(0.18f, 0.12f, 0.09f, 0.85f);
        var hoverBg = UiKit.Fade(UiKit.Accent, 0.5f);
        var pressedBg = UiKit.Accent;
        
        UiKit.StyleButton(btn, normalBg, hoverBg, pressedBg, radius: 16);
        
        // Cập nhật tâm xoay/thu phóng khi kích thước nút thay đổi
        btn.Resized += () => btn.PivotOffset = btn.Size / 2f;
        
        // Hiệu ứng "lún xuống" khi nhấn
        btn.ButtonDown += () => 
        {
            btn.PivotOffset = btn.Size / 2f; // Dự phòng
            var t = btn.CreateTween();
            t.TweenProperty(btn, "scale", new Vector2(0.85f, 0.85f), 0.05f);
        };
        
        btn.ButtonUp += () => 
        {
            var t = btn.CreateTween();
            t.TweenProperty(btn, "scale", Vector2.One, 0.15f)
             .SetTrans(Tween.TransitionType.Back)
             .SetEase(Tween.EaseType.Out);
        };
    }

    private void OnOpts()
    {
        SettingsMenu.ShowSettings();
    }
}
