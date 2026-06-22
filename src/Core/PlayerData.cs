using Godot;
using Godot.Collections;

namespace FragmentOfJapanese.Core;

[GlobalClass]
public partial class PlayerData : Resource
{
    [Export] public string PlayerName  { get; set; } = "Tabi";
    [Export] public int    Level       { get; set; } = 1;
    [Export] public int    Exp         { get; set; } = 0;
    [Export] public int    MaxExp      { get; set; } = 100;
    [Export] public int    Hp          { get; set; } = 150;
    [Export] public int    MaxHp       { get; set; } = 150;
    [Export] public int    Mana        { get; set; } = 100;
    [Export] public int    MaxMana     { get; set; } = 100;
    [Export] public int    Stamina     { get; set; } = 100;   // thể lực — dùng để chạy/né/skill (wire sau)
    [Export] public int    MaxStamina  { get; set; } = 100;
    [Export] public int    Attack      { get; set; } = 10;
    [Export] public int    Defense     { get; set; } = 5;

    /// <summary>IDs của từ vựng đã học — dùng bởi ProgressTracker</summary>
    [Export] public Array<string> LearnedVocabIds { get; set; } = new();
}
