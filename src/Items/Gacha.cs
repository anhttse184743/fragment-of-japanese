using Godot;
using System;
using System.Collections.Generic;
using FragmentOfJapanese.Autoloads;
using FragmentOfJapanese.Core;

namespace FragmentOfJapanese.Items;

/// <summary>
/// Hệ thống gacha (autoload). Quay bằng Chìa Khóa Bạc — 1 vé / lần quay.
/// Tỉ lệ mỗi lần: Thường 74% · Hiếm 20% · Sử thi 5% · Huyền thoại 1%.
/// Pity ("Luck"): sau <see cref="PityMax"/> lần không ra Huyền thoại sẽ ÉP ra, reset khi trúng.
/// Phần thưởng được cộng thẳng vào túi; phát <see cref="Changed"/> khi pity đổi.
/// </summary>
public partial class Gacha : Node
{
    public static Gacha Instance { get; private set; }

    public const string TicketId = "item_silver_key";   // vé quay
    public const int    PityMax  = 100;

    public int Pity { get; private set; }
    public event Action Changed;

    private readonly Random _rng = new();

    // Túi phần thưởng theo độ hiếm (id phải khớp rarity trong items.json)
    private static readonly Dictionary<Rarity, string[]> Pool = new()
    {
        { Rarity.Common,    new[] { "item_potion", "item_goblin_ear", "item_wolf_pelt" } },
        { Rarity.Rare,      new[] { "item_hi_potion", "item_scroll", "item_wooden_sword" } },
        { Rarity.Epic,      new[] { "item_leather_armor" } },
        { Rarity.Legendary, new[] { "item_golden_key", "item_ancient_relic" } },
    };

    public override void _Ready() => Instance = this;

    public bool CanPull(int count) => Inventory.Instance?.Has(TicketId, count) ?? false;

    /// <summary>Quay <paramref name="count"/> lần. Trả null nếu không đủ vé.</summary>
    public List<ItemEntry> Pull(int count)
    {
        if (count <= 0) return null;
        if (Inventory.Instance == null || !Inventory.Instance.Has(TicketId, count)) return null;

        Inventory.Instance.Remove(TicketId, count);

        var results = new List<ItemEntry>();
        for (int i = 0; i < count; i++)
        {
            var def = RollOne();
            if (def != null) results.Add(def);
        }

        Changed?.Invoke();
        return results;
    }

    private ItemEntry RollOne()
    {
        Pity++;
        Rarity r = Pity >= PityMax ? Rarity.Legendary : RollRarity();
        if (r == Rarity.Legendary) Pity = 0;

        var    bucket = Pool[r];
        string id     = bucket[_rng.Next(bucket.Length)];
        Inventory.Instance?.Add(id, 1);
        return ItemDatabase.Instance?.Get(id);
    }

    private Rarity RollRarity()
    {
        int roll = _rng.Next(100);                 // 0..99
        if (roll < 1)  return Rarity.Legendary;    // 1%
        if (roll < 6)  return Rarity.Epic;         // 5%
        if (roll < 26) return Rarity.Rare;         // 20%
        return Rarity.Common;                       // 74%
    }
}
