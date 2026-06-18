using Godot;

namespace FragmentOfJapanese.World;

/// <summary>
/// Base class cho mỗi dungeon.
/// Mỗi dungeon là 1 scene riêng biệt.
/// </summary>
public partial class Dungeon : Node2D
{
    [Export] public string DungeonId   { get; set; } = "";
    [Export] public string DungeonName { get; set; } = "";
    [Export] public int    FloorCount  { get; set; } = 1;

    public override void _Ready()
    {
        GD.Print($"[Dungeon] Entered: {DungeonId}");
    }
}
