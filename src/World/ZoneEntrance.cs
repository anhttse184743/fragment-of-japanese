using Godot;
using FragmentOfJapanese.Autoloads;

namespace FragmentOfJapanese.World;

/// <summary>
/// Area3D đánh dấu lối vào một Zone. Khi player tap/click vào → chuyển scene tương ứng.
/// Cần InputRayPickable + có CollisionShape3D trong scene để nhận được click.
/// </summary>
public partial class ZoneEntrance : Area3D
{
    [Export] public string ZoneId     { get; set; } = "";
    [Export] public string ScenePath  { get; set; } = "";
    [Export] public bool   IsUnlocked { get; set; } = false;

    public override void _Ready()
    {
        InputRayPickable = true;
        InputEvent += OnInputEvent;
    }

    private void OnInputEvent(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIdx)
    {
        if (!IsUnlocked) return;

        if (@event is InputEventScreenTouch touch && touch.Pressed)
            SceneTransition.Instance.GoTo(ScenePath);
        else if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            SceneTransition.Instance.GoTo(ScenePath);
    }
}
