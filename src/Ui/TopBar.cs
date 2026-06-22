using Godot;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// Hàng nút góc trên-phải: SHOP / BAG (túi đồ) / OPTS (cài đặt).
/// SHOP + BAG mở các autoload đã có (ShopUi / InventoryUi) — y như bấm phím P / I.
/// OPTS hiện là placeholder (chưa có menu cài đặt trong game) — user nối sau.
/// Là 1 UI riêng: sửa nhãn/bố cục trong TopBar.tscn.
/// </summary>
public partial class TopBar : Control
{
    [Export] private Button _shopBtn;
    [Export] private Button _bagBtn;
    [Export] private Button _optsBtn;

    public override void _Ready()
    {
        if (_shopBtn != null) _shopBtn.Pressed += () => ShopUi.Instance?.Toggle();
        if (_bagBtn  != null) _bagBtn.Pressed  += () => InventoryUi.Instance?.Toggle();
        if (_optsBtn != null) _optsBtn.Pressed += OnOpts;
    }

    private void OnOpts()
    {
        GD.Print("[TopBar] OPTS — menu cài đặt trong game chưa làm (placeholder).");
        UiKit.Toast(this, "Cài đặt: sắp có");
    }
}
