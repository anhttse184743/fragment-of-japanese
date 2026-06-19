using Godot;
using FragmentOfJapanese.Entities.Player;

namespace FragmentOfJapanese.World;

/// <summary>
/// Area3D tổng quát cho vật tương tác (cổng, rương, biển báo... 2.5D — node 3D,
/// hình ảnh là sprite billboard).
///
/// Khi player vào tầm: hiện một bảng nổi "[E] {ActionText}" phía trên vật (kiểu
/// proximity prompt của Roblox). Bấm E trong tầm → phát signal <see cref="Interacted"/>.
/// Cần có CollisionShape3D con để xác định vùng tương tác.
/// </summary>
public partial class Interactable : Area3D
{
	[Signal] public delegate void InteractedEventHandler();

	[Export] public string  Label        { get; set; } = "";
	[Export] public string  ActionText   { get; set; } = "Tương tác";        // động từ hiện trên bảng E
	[Export] public Vector3 PromptOffset { get; set; } = new(0f, 2.2f, 0f);  // vị trí bảng so với gốc vật

	private bool    _playerInRange = false;
	private Label3D _prompt;

	public override void _Ready()
	{
		EnsureInteractAction();

		_prompt = BuildPrompt();
		AddChild(_prompt);

		BodyEntered += OnBodyEntered;
		BodyExited  += OnBodyExited;
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
	}

	private void OnBodyExited(Node3D body)
	{
		if (body is not Player) return;
		_playerInRange = false;
		if (_prompt != null) _prompt.Visible = false;
	}

	/// <summary>Bảng nổi "[E] ..." — billboard luôn quay về camera, vẽ đè lên cảnh.</summary>
	private Label3D BuildPrompt() => new()
	{
		Text            = $"[E] {ActionText}",
		Position        = PromptOffset,
		Billboard       = BaseMaterial3D.BillboardModeEnum.Enabled,
		FontSize        = 48,
		OutlineSize     = 12,
		OutlineModulate = new Color(0f, 0f, 0f, 0.9f),
		PixelSize       = 0.01f,
		NoDepthTest     = true,
		Visible         = false,
	};

	/// <summary>Đăng ký phím E cho action "interact" nếu project chưa khai báo.</summary>
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
