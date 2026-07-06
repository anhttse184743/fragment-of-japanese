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

    private class ItemPriceDto
    {
        public string StringId  { get; set; }
        public int    BuyPrice  { get; set; }
        public int    SellPrice { get; set; }
    }

    // Giá bán lại (Vàng/cái) theo DB — nạp cùng SyncPricesAsync.
    private readonly Dictionary<string, int> _sellPrices = new();

    /// <summary>Giá bán lại của item (0 = không bán được).</summary>
    public int GetSellPrice(string itemId) => _sellPrices.TryGetValue(itemId, out var v) ? v : 0;
    /// <summary>Các item bán lại được (sell_price > 0).</summary>
    public IEnumerable<string> SellableIds => _sellPrices.Keys;

    /// <summary>Lấy giá bán THẬT từ DB (server là nơi trừ tiền) và ghi đè giá trong shop.json,
    /// để giá hiển thị = giá thực trừ. shop.json chỉ còn quyết định bán item nào + bằng tiền gì.</summary>
    public async System.Threading.Tasks.Task SyncPricesAsync()
    {
        if (string.IsNullOrEmpty(ApiClient.Instance.AccessToken)) return;

        var res = await ApiClient.Instance.GetAsync("/api/items");
        if (!res.IsSuccessStatusCode) return;

        var data = await ApiClient.Instance.ReadAsAsync<AccountManager.ApiResponse<List<ItemPriceDto>>>(res);
        if (data?.Data == null) return;

        _sellPrices.Clear();
        foreach (var dto in data.Data)
        {
            if (string.IsNullOrEmpty(dto.StringId)) continue;
            var listing = GetListing(dto.StringId);
            if (listing != null) listing.Price = dto.BuyPrice;          // giá mua = DB
            if (dto.SellPrice > 0) _sellPrices[dto.StringId] = dto.SellPrice;   // giá bán lại = DB
        }
        GD.Print($"[Shop] Đồng bộ giá từ DB: {data.Data.Count} item, {_sellPrices.Count} bán được.");
    }

    /// <summary>Mua <paramref name="qty"/> đơn vị: gọi API mua, nếu thành công thì trừ tiền ví và thêm vào túi.</summary>
    public async System.Threading.Tasks.Task<BuyResult> BuyAsync(string itemId, int qty = 1)
    {
        if (qty <= 0) return BuyResult.InvalidItem;

        var listing = GetListing(itemId);
        if (listing == null || ItemDatabase.Instance?.Get(itemId) == null)
            return BuyResult.InvalidItem;

        var wallet = Wallet.Instance;
        if (wallet == null) return BuyResult.NotEnoughMoney;

        int total = listing.Price * qty;
        bool hasEnough = listing.CurrencyType == ShopCurrency.MaThach
            ? wallet.MaThach >= total
            : wallet.Gold >= total;

        if (!hasEnough) return BuyResult.NotEnoughMoney;

        var res = await ApiClient.Instance.PostAsync("/api/shop/buy-item", new 
        { 
            StringId = itemId, 
            Quantity = qty, 
            Currency = listing.CurrencyType == ShopCurrency.MaThach ? "MaThach" : "Gold" 
        });

        if (res.IsSuccessStatusCode)
        {
            if (listing.CurrencyType == ShopCurrency.MaThach)
                wallet.SpendMaThach(total);
            else
                wallet.SpendGold(total);

            Inventory.Instance?.AddOptimistic(itemId, qty);
            GD.Print($"[Shop] Mua {qty}x {itemId} (-{total} {listing.Currency}).");
            
            _ = Inventory.Instance?.SyncAsync();
            _ = Wallet.Instance?.SyncAsync();
            return BuyResult.Success;
        }

        return BuyResult.InvalidItem; // Hoặc thêm enum Failed
    }

    // ───────── Nạp Ma Thạch (PayOS) ─────────

    /// <summary>1 gói nạp lấy từ shop_products trên server.</summary>
    public class PaymentPack
    {
        public string Id          { get; set; }   // Guid dạng chuỗi
        public string Name        { get; set; }
        public string Description { get; set; }
        public decimal? PriceVnd  { get; set; }
        public int CurrencyAmount { get; set; }
        public int BonusPercent   { get; set; }
    }

    private class ProductDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
        public decimal? PriceVnd { get; set; }
        public int CurrencyAmount { get; set; }
        public int BonusPercent { get; set; }
    }

    private class CreatePayResponseDto
    {
        public string CheckoutUrl { get; set; }
        public int AmountVnd { get; set; }
    }

    /// <summary>Lấy danh sách gói nạp từ server (DB shop_products).</summary>
    public async System.Threading.Tasks.Task<List<PaymentPack>> GetPacksAsync()
    {
        var packs = new List<PaymentPack>();
        if (string.IsNullOrEmpty(ApiClient.Instance.AccessToken)) return packs;

        var res = await ApiClient.Instance.GetAsync("/api/shop/products");
        if (!res.IsSuccessStatusCode) return packs;

        var data = await ApiClient.Instance.ReadAsAsync<AccountManager.ApiResponse<List<ProductDto>>>(res);
        if (data?.Data == null) return packs;

        foreach (var p in data.Data)
            packs.Add(new PaymentPack
            {
                Id = p.Id, Name = p.Name, Description = p.Description,
                PriceVnd = p.PriceVnd, CurrencyAmount = p.CurrencyAmount, BonusPercent = p.BonusPercent
            });
        return packs;
    }

    private class ConfirmDto { public int Credited { get; set; } }

    /// <summary>Xác nhận các đơn PayOS đã trả nhưng chưa cộng (server hỏi PayOS API). Trả số đơn vừa cộng.</summary>
    public async System.Threading.Tasks.Task<int> ConfirmPendingPaymentsAsync()
    {
        if (string.IsNullOrEmpty(ApiClient.Instance.AccessToken)) return 0;
        var res = await ApiClient.Instance.PostAsync("/api/payment/payos/confirm", new { });
        if (!res.IsSuccessStatusCode) return 0;
        var data = await ApiClient.Instance.ReadAsAsync<AccountManager.ApiResponse<ConfirmDto>>(res);
        return data?.Data?.Credited ?? 0;
    }

    /// <summary>Tạo link thanh toán PayOS cho 1 gói. Trả (url, lỗi). url rỗng nếu lỗi.</summary>
    public async System.Threading.Tasks.Task<(string url, string error)> CreatePayOsLinkAsync(string productId)
    {
        if (string.IsNullOrEmpty(ApiClient.Instance.AccessToken)) return ("", "Bạn cần đăng nhập.");

        var res  = await ApiClient.Instance.PostAsync("/api/payment/payos/create", new { ProductId = productId });
        var data = await ApiClient.Instance.ReadAsAsync<AccountManager.ApiResponse<CreatePayResponseDto>>(res);

        if (!res.IsSuccessStatusCode || data == null || !data.Success || data.Data == null || string.IsNullOrEmpty(data.Data.CheckoutUrl))
            return ("", data?.Message ?? "Không tạo được link thanh toán (kiểm tra cấu hình PayOS).");

        return (data.Data.CheckoutUrl, null);
    }
}
