using Godot;
using System.Collections.Generic;
using System.Text.Json;
using FragmentOfJapanese.Core;

namespace FragmentOfJapanese.Autoloads;

/// <summary>
/// Kho định nghĩa vật phẩm — nạp data/items.json 1 lần lúc khởi động.
/// Tra cứu theo id. Song song với <see cref="JapaneseDB"/> (vocab).
/// </summary>
public partial class ItemDatabase : Node
{
    public static ItemDatabase Instance { get; private set; }

    private const string DataPath = "res://data/items.json";

    private readonly Dictionary<string, ItemEntry> _items = new();

    public IReadOnlyDictionary<string, ItemEntry> Items => _items;

    public override void _Ready()
    {
        Instance = this;
        LoadAll();
    }

    private void LoadAll()
    {
        if (!FileAccess.FileExists(DataPath))
        {
            GD.PushWarning($"[ItemDatabase] File not found: {DataPath}");
            return;
        }

        using var file = FileAccess.Open(DataPath, FileAccess.ModeFlags.Read);
        var list = JsonSerializer.Deserialize<List<ItemEntry>>(file.GetAsText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

        foreach (var item in list)
            _items[item.Id] = item;

        GD.Print($"[ItemDatabase] Loaded {_items.Count} items.");
    }

    /// <summary>Lấy định nghĩa vật phẩm theo id, hoặc null nếu không có.</summary>
    public ItemEntry Get(string id) => _items.TryGetValue(id, out var item) ? item : null;
}
