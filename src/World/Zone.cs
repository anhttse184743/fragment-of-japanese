using Godot;

namespace FragmentOfJapanese.World;

/// <summary>
/// Base class cho mỗi khu vực (Village, Forest, Mountains...).
/// Mỗi zone là 1 scene riêng biệt.
/// </summary>
public partial class Zone : Node2D
{
    [Export] public string ZoneId   { get; set; } = "";
    [Export] public string ZoneName { get; set; } = "";

    public override void _Ready()
    {
        GD.Print($"[Zone] Entered: {ZoneId}");
    }
}
