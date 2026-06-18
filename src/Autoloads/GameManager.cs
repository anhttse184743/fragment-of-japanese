using Godot;

namespace FragmentOfJapanese.Autoloads;

public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; }

    public enum GamePhase
    {
        MainMenu,
        WorldMap,
        Zone,
        Dungeon,
        Battle,
        Dialogue,
        Quiz,
    }

    public GamePhase CurrentPhase { get; private set; } = GamePhase.MainMenu;

    public override void _Ready()
    {
        Instance = this;
    }

    public void SetPhase(GamePhase phase)
    {
        CurrentPhase = phase;
        GD.Print($"[GameManager] Phase → {phase}");
    }
}
