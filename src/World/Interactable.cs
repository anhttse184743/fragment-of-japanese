using Godot;
using FragmentOfJapanese.Entities.Player;

namespace FragmentOfJapanese.World;

/// <summary>
/// Area3D tổng quát cho vật tương tác (cổng, rương, NPC...).
/// Khi player vào tầm: hiện Label3D "[E] ..." nổi trên vật + nút bấm góc dưới màn hình (mobile).
/// Bấm E (keyboard) hoặc tap nút → phát signal Interacted.
/// </summary>
public partial class Interactable : Area3D
{
	[Signal] public delegate void InteractedEventHandler();

	[Export] public string  Label        { get; set; } = "";
	[Export] public string  ActionText   { get; set; } = "Tương tác";
	[Export] public Vector3 PromptOffset { get; set; } = new(0f, 2.2f, 0f);

	private bool      _playerInRange;
	private Label3D   _prompt;
	private CanvasLayer _uiLayer;

	public override void _Ready()
	{
		EnsureInteractAction();

		_prompt = BuildPrompt();
		AddChild(_prompt);

		BodyEntered += OnBodyEntered;
		BodyExited  += OnBodyExited;
	}

	public override void _ExitTree()
	{
		HideMobileButton();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_playerInRange && @event.IsActionPressed("interact"))
			EmitSignal(SignalName.Interacted);
	}

	private void OnBodyEntered(Node3D body)
	{
		if (body is not Player) return;
		_playerInRange = true;
		if (_prompt != null) _prompt.Visible = true;
		ShowMobileButton();
	}

	private void OnBodyExited(Node3D body)
	{
		if (body is not Player) return;
		_playerInRange = false;
		if (_prompt != null) _prompt.Visible = false;
		HideMobileButton();
	}

	private void ShowMobileButton()
	{
		HideMobileButton();   // đảm bảo không tạo 2 lần

		_uiLayer = new CanvasLayer { Layer = 50 };

		var btn = new Button
		{
			Text              = ActionText,
			CustomMinimumSize = new Vector2(240, 70),
			FocusMode         = Control.FocusModeEnum.None,
		};
		// Góc dưới-PHẢI, ngay TRÊN cụm nút ATK/JUMP/FAST (tránh đè joystick bên trái).
		btn.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
		btn.OffsetRight  = -40;
		btn.OffsetLeft   = -280;
		btn.OffsetBottom = -226;
		btn.OffsetTop    = -296;

		btn.AddThemeFontSizeOverride("font_size", 22);

		var bg = new StyleBoxFlat
		{
			BgColor          = new Color(0.08f, 0.08f, 0.10f, 0.88f),
			CornerRadiusTopLeft     = 14,
			CornerRadiusTopRight    = 14,
			CornerRadiusBottomLeft  = 14,
			CornerRadiusBottomRight = 14,
			BorderColor         = new Color(0.85f, 0.85f, 1.0f, 0.6f),
			BorderWidthTop      = 2,
			BorderWidthBottom   = 2,
			BorderWidthLeft     = 2,
			BorderWidthRight    = 2,
		};
		var bgHov = (StyleBoxFlat)bg.Duplicate();
		bgHov.BgColor = new Color(0.16f, 0.16f, 0.24f, 0.95f);

		btn.AddThemeStyleboxOverride("normal",  bg);
		btn.AddThemeStyleboxOverride("hover",   bgHov);
		btn.AddThemeStyleboxOverride("pressed", bgHov);
		btn.AddThemeColorOverride("font_color", Colors.White);

		btn.Pressed += () => EmitSignal(SignalName.Interacted);

		// Nhãn nhỏ "[E]" góc trên nút để máy tính biết phím tắt
		var hint = new Label
		{
			Text     = "[E]",
			Position = new Vector2(8, 4),
		};
		hint.AddThemeFontSizeOverride("font_size", 12);
		hint.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.9f));
		btn.AddChild(hint);

		_uiLayer.AddChild(btn);
		GetTree().Root.AddChild(_uiLayer);
	}

	private void HideMobileButton()
	{
		if (_uiLayer == null) return;
		if (GodotObject.IsInstanceValid(_uiLayer)) _uiLayer.QueueFree();
		_uiLayer = null;
	}

	private Label3D BuildPrompt() => new()
	{
		Text            = $"[E]  {ActionText}",
		Position        = PromptOffset,
		Billboard       = BaseMaterial3D.BillboardModeEnum.Enabled,
		FontSize        = 48,
		OutlineSize     = 12,
		OutlineModulate = new Color(0f, 0f, 0f, 0.9f),
		PixelSize       = 0.01f,
		NoDepthTest     = true,
		Visible         = false,
	};

	private static void EnsureInteractAction()
	{
		if (!InputMap.HasAction("interact"))
			InputMap.AddAction("interact");

		foreach (var e in InputMap.ActionGetEvents("interact"))
			if (e is InputEventKey k && k.PhysicalKeycode == Key.E)
				return;

		InputMap.ActionAddEvent("interact", new InputEventKey { PhysicalKeycode = Key.E });
	}
}
