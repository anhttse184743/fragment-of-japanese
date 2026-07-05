using System;
using System.Text.Json.Serialization;
using Godot;

namespace FragmentOfJapanese.Cosmetics;

/// <summary>Các loại skin (mỗi loại 1 thứ đang mặc).</summary>
public enum SkinCategory
{
    Player,        // bộ frame nhân vật (+ tông màu)
    AttackSwing,   // vệt chém đòn đánh thường
    RunDust,       // bụi nảy ở chân khi chạy
    HitSpark,      // tia loé khi đòn trúng quái
    LevelUpAura,   // hào quang khi lên cấp
    Footstep,      // vệt chân để lại khi di chuyển
}

/// <summary>
/// Một skin (nạp từ data/skins.json). Hiệu ứng dùng <see cref="Color"/> (texture procedural đổi màu);
/// skin nhân vật dùng <see cref="Frames"/> (SpriteFrames) + Color làm tông màu (modulate).
/// </summary>
public sealed class SkinDef
{
    [JsonPropertyName("id")]       public string Id       { get; set; } = "";
    [JsonPropertyName("category")] public string Category { get; set; } = "";
    [JsonPropertyName("name")]     public string Name     { get; set; } = "";
    [JsonPropertyName("color")]    public string Color    { get; set; } = "#ffffff";
    [JsonPropertyName("frames")]   public string Frames   { get; set; } = "";

    // Chỉ skin Player: cỡ frame khác nhau (vd 256 vs 128px) → cần pixel_size/Y riêng. 0 = giữ mặc định của node.
    [JsonPropertyName("pixel_size")] public float PixelSize { get; set; } = 0f;
    [JsonPropertyName("visual_y")]   public float VisualY   { get; set; } = 0f;

    [JsonIgnore]
    public SkinCategory Cat =>
        Enum.TryParse<SkinCategory>(Category, true, out var c) ? c : SkinCategory.Player;

    [JsonIgnore]
    public Color ColorValue =>
        Godot.Color.FromHtml(string.IsNullOrEmpty(Color) ? "#ffffff" : Color);
}
