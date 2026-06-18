using Godot;
using FragmentOfJapanese.Entities.Player;

namespace FragmentOfJapanese.World;

/// <summary>
/// Area3D tổng quát dùng cho NPC, rương, biển báo... (2.5D — node 3D, hình ảnh là sprite billboard).
/// Phát signal Interacted khi player ở trong vùng và nhấn phím tương tác (E).
/// </summary>
public partial class Interactable : Area3D
{
    [Signal] public delegate void InteractedEventHandler();

    [Export] public string Label { get; set; } = "";

    private bool _playerInRange = false;

    public override void _Ready()
    {
        EnsureInteractAction();
        BodyEntered += body => { if (body is Player) _playerInRange = true; };
        BodyExited  += body => { if (body is Player) _playerInRange = false; };
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_playerInRange && @event.IsActionPressed("interact"))
            EmitSignal(SignalName.Interacted);
    }

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
