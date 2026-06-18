using Godot;
using System.Text.Json;

namespace FragmentOfJapanese.Autoloads;

public partial class SaveSystem : Node
{
    public static SaveSystem Instance { get; private set; }
    private const string SavePath = "user://save.json";

    public override void _Ready()
    {
        Instance = this;
    }

    public void Save(object data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        file?.StoreString(json);
        GD.Print("[SaveSystem] Game saved.");
    }

    public string Load()
    {
        if (!FileAccess.FileExists(SavePath)) return null;
        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        return file?.GetAsText();
    }

    public bool HasSave() => FileAccess.FileExists(SavePath);
}
