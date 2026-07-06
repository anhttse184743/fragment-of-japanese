using System.Text.Json.Serialization;

namespace FragmentOfJapanese.Core;

/// <summary>Nhóm phân loại vật phẩm trong túi.</summary>
public enum ItemType
{
	Consumable,   // tiêu hao: thuốc, cuộn từ vựng — dùng được rồi mất
	Key,          // chìa khóa: vé để quay gacha
	Trade,        // đổi / bán: tai goblin... bán lấy Vàng hoặc đổi đồ với dân làng
	Equipment,    // trang bị: vũ khí / giáp (sắp ra mắt)
}

/// <summary>Độ hiếm vật phẩm — dùng cho gacha.</summary>
public enum Rarity { Common, Rare, Epic, Legendary }

/// <summary>
/// Định nghĩa 1 loại vật phẩm (đọc từ data/items.json).
/// Model thuần — giống <see cref="VocabularyEntry"/>, không phải Node.
/// </summary>
public class ItemEntry
{
	[JsonPropertyName("id")]             public string Id            { get; set; } = "";
	[JsonPropertyName("name_vi")]        public string NameVi        { get; set; } = "";
	[JsonPropertyName("name_en")]        public string NameEn        { get; set; } = "";
	[JsonPropertyName("name_ja")]        public string NameJa        { get; set; } = "";
	[JsonPropertyName("description_vi")] public string DescriptionVi { get; set; } = "";

	/// <summary>Nhóm phân loại — chuỗi trong JSON: consumable / key / trade / equipment.</summary>
	[JsonPropertyName("type")]           public string TypeId        { get; set; } = "consumable";

	/// <summary>Hiệu ứng khi dùng (chỉ cho Consumable): "heal", "learn"... ("" = không có).</summary>
	[JsonPropertyName("effect")]         public string Effect        { get; set; } = "";

	/// <summary>Giá trị hiệu ứng (vd heal 30 HP → value=30).</summary>
	[JsonPropertyName("value")]          public int    Value         { get; set; } = 0;

	/// <summary>Giá bán/đổi (Vàng).</summary>
	[JsonPropertyName("price")]          public int    Price         { get; set; } = 0;

	/// <summary>Đường dẫn icon (tùy chọn).</summary>
	[JsonPropertyName("icon")]           public string Icon          { get; set; } = "";

	/// <summary>Nhóm phân loại đã parse sang enum.</summary>
	[JsonIgnore]
	public ItemType Type => TypeId?.ToLowerInvariant() switch
	{
		"key"       => ItemType.Key,
		"trade"     => ItemType.Trade,
		"equipment" => ItemType.Equipment,
		_           => ItemType.Consumable,
	};

	/// <summary>Độ hiếm — chuỗi "common"/"rare"/"epic"/"legendary".</summary>
	[JsonPropertyName("rarity")] public string RarityId { get; set; } = "common";

	[JsonIgnore]
	public Rarity Rarity => RarityId?.ToLowerInvariant() switch
	{
		"rare"      => Rarity.Rare,
		"epic"      => Rarity.Epic,
		"legendary" => Rarity.Legendary,
		_           => Rarity.Common,
	};
}
