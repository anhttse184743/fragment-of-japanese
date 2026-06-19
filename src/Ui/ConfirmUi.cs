using System;
using Godot;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// Hộp thoại xác nhận chung (autoload). Gọi từ bất cứ đâu:
///   ConfirmUi.Instance.Ask("Bạn có muốn vào X không?", onConfirm);
/// Hiện nền mờ chặn click + panel giữa màn hình + 2 nút Đồng ý / Hủy.
/// Phím: Enter = Đồng ý, Esc = Hủy.
/// </summary>
public partial class ConfirmUi : CanvasLayer
{
	public static ConfirmUi Instance { get; private set; }

	private Control _root;
	private Label   _message;
	private Button  _yes;
	private Button  _no;
	private Action  _onConfirm;

	public override void _Ready()
	{
		Instance = this;
		Layer    = 50;   // trên cả túi đồ / cửa hàng (Layer 10)
		BuildUi();
		_root.Visible = false;
	}

	/// <summary>Hiện hộp xác nhận. onConfirm chạy khi người chơi bấm "Đồng ý".</summary>
	public void Ask(string message, Action onConfirm)
	{
		_onConfirm    = onConfirm;
		_message.Text = message;
		_root.Visible = true;
		_yes.GrabFocus();
	}

	public override void _UnhandledInput(InputEvent ev)
	{
		if (!_root.Visible) return;
		if (ev.IsActionPressed("ui_cancel"))      { OnNo();  GetViewport().SetInputAsHandled(); }
		else if (ev.IsActionPressed("ui_accept")) { OnYes(); GetViewport().SetInputAsHandled(); }
	}

	private void OnYes()
	{
		var cb = _onConfirm;
		Close();
		cb?.Invoke();
	}

	private void OnNo() => Close();

	private void Close()
	{
		_root.Visible = false;
		_onConfirm    = null;
	}

	private void BuildUi()
	{
		_root = new Control();
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.MouseFilter = Control.MouseFilterEnum.Stop;   // chặn click rơi xuống game
		AddChild(_root);

		var dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.55f) };
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(dim);

		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(center);

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(460, 0) };
		panel.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.PanelBg, 14, UiKit.Accent, 2, 24, 22));
		center.AddChild(panel);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 20);
		panel.AddChild(vbox);

		_message = new Label
		{
			Text                = "",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode        = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize   = new Vector2(412, 0),
		};
		_message.AddThemeFontSizeOverride("font_size", 22);
		_message.AddThemeColorOverride("font_color", Colors.White);
		vbox.AddChild(_message);

		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		row.AddThemeConstantOverride("separation", 16);
		vbox.AddChild(row);

		_yes = new Button { Text = "Đồng ý", CustomMinimumSize = new Vector2(160, 48) };
		UiKit.StyleButton(_yes, UiKit.BuyGreen, UiKit.BuyGreenHi, UiKit.BuyGreen);
		_yes.Connect(Button.SignalName.Pressed, Callable.From(OnYes));
		row.AddChild(_yes);

		_no = new Button { Text = "Hủy", CustomMinimumSize = new Vector2(160, 48) };
		UiKit.StyleButton(_no,
			new Color(0.36f, 0.22f, 0.24f),
			new Color(0.46f, 0.28f, 0.30f),
			new Color(0.36f, 0.22f, 0.24f));
		_no.Connect(Button.SignalName.Pressed, Callable.From(OnNo));
		row.AddChild(_no);
	}
}
