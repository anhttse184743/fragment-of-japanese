using Godot;
using FragmentOfJapanese.Autoloads;
using FragmentOfJapanese.Entities.Player;

namespace FragmentOfJapanese.World;

/// <summary>
/// Area3D trong Zone. Khi player bước vào → chuyển đến Dungeon scene tương ứng.
/// </summary>
public partial class DungeonEntrance : Area3D
{
    [Export] public string DungeonId { get; set; } = "";
    [Export] public string ScenePath { get; set; } = "";

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body is Player)
            SceneTransition.Instance.GoTo(ScenePath);
    }
}
