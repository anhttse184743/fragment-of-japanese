using Godot;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using FragmentOfJapanese.Autoloads;

namespace FragmentOfJapanese.Items;

public enum ShopCurrency { Gold, MaThach }

/// <summary>1 mặt hàng trong cửa hàng: item nào, mua bằng tiền gì, giá bao nhiêu.</summary>
public class ShopListing
{
    [JsonPropertyName("item_id")]  public string ItemId   { get; set; } = "";
    [JsonPropertyName("currency")] public string Currency { get; set; } = "gold";
    [JsonPropertyName("price")]    public int    Price    { get; set; } = 0;

    [JsonIgnore]
    public ShopCurrency CurrencyType =>
        Currency?.ToLowerInvariant() == "mathach" ? ShopCurrency.MaThach : ShopCurrency.Gold;
}

/// <summary>
/// Cửa hàng (autoload) — nạp danh mục từ data/shop.json và xử lý MUA.
/// Hiện chỉ MUA (trừ tiền + thêm vào túi); bán lại để sau.
/// </summary>
public partial class Shop : Node
{
    public static Shop Instance { get; private set; }

    public enum BuyResult { Success, NotEnoughMoney, InvalidItem }

    private const string DataPath = "res://data/shop.json";

    private readonly List<ShopListing> _listings = new();
    public IReadOnlyList<ShopListing> Listings => _listings;

    public override void _Ready()
    {
        Instance = this;
        LoadAll();
    }

    private void LoadAll()
    {
        if (!FileAccess.FileExists(DataPath))
        {
            GD.PushWarning($"[Shop] File not found: {DataPath}");
            return;
        }

        using var file = FileAccess.Open(DataPath, FileAccess.ModeFlags.Read);
        var list = JsonSerializer.Deserialize<List<ShopListing>>(file.GetAsText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

        _listings.Clear();
        _listings.AddRange(list);
        GD.Print($"[Shop] Loaded {_listings.Count} listings.");
    }

    public ShopListing GetListing(string itemId) => _listings.Find(l => l.ItemId == itemId);

    /// <summary>Mua <paramref name="qty"/> đơn vị: trừ tiền trong ví rồi thêm vào túi.</summary>
    public BuyResult Buy(string itemId, int qty = 1)
    {
        if (qty <= 0) return BuyResult.InvalidItem;

        var listing = GetListing(itemId);
        if (listing == null || ItemDatabase.Instance?.Get(itemId) == null)
            return BuyResult.InvalidItem;

        var wallet = Wallet.Instance;
        if (wallet == null) return BuyResult.NotEnoughMoney;

        int total = listing.Price * qty;
        bool paid = listing.CurrencyType == ShopCurrency.MaThach
            ? wallet.SpendMaThach(total)
            : wallet.SpendGold(total);

        if (!paid) return BuyResult.NotEnoughMoney;

        Inventory.Instance?.Add(itemId, qty);
        GD.Print($"[Shop] Mua {qty}x {itemId} (-{total} {listing.Currency}).");
        return BuyResult.Success;
    }
}
