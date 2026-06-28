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
    }

    private void OnOpts()
    {
        SettingsMenu.BackScene = "res://scenes/world/World.tscn";
        SceneTransition.Instance?.GoTo("res://scenes/ui/SettingsMenu.tscn");
    }
}
