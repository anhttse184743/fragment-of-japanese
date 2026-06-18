using Godot;

namespace FragmentOfJapanese.Entities.Npc;

public partial class Npc : StaticBody3D
{
    [Export] public string NpcId   { get; set; } = "";
    [Export] public string NpcName { get; set; } = "";

    public virtual void Interact()
    {
        GD.Print($"[NPC] {NpcName} interacted.");
    }
}
