using Godot;

namespace FragmentOfJapanese.World;

/// <summary>
/// Tham số sinh địa hình cho từng zone.
/// Thay đổi các giá trị này để ra địa hình rừng, sa mạc, núi, làng...
/// </summary>
public class TerrainConfig
{
    // --- Kích thước lưới ---
    public int   GridWidth  = 64;
    public int   GridHeight = 64;
    public float CellSize   = 2.0f;   // meters mỗi ô
    public float MaxHeight  = 15.0f;  // chiều cao tối đa (meters)

    // --- FastNoiseLite ---
    public float NoiseFrequency   = 0.05f;
    public int   NoiseOctaves     = 4;
    public float NoiseLacunarity  = 2.0f;
    public float NoisePersistence = 0.5f;
    public long  Seed             = 0;

    // --- Ngưỡng biome (normalized 0–1) ---
    // Chỉ 3 biome: sand/dirt → grass → rock
    // SnowLevel để 2.0 (vô hiệu) — không có tuyết
    public float SandLevel  = 0.20f;  // dưới = cát/đất
    public float GrassLevel = 0.60f;  // dưới = cỏ
    public float RockLevel  = 0.95f;  // dưới = đá
    public float SnowLevel  = 2.00f;  // vô hiệu hóa tuyết

    // --- WalkableGrid ---
    public float WalkableMaxSlope = 0.35f; // height diff normalized tối đa giữa 2 ô kề nhau

    // --- Mật độ props (tỉ lệ mỗi ô) ---
    public float TreeDensity     = 0.05f;
    public float MushroomDensity = 0.02f;
    public float GrassDensity    = 0.12f;
    public float FlowerDensity   = 0.03f;
    public float RockDensity     = 0.04f;   // đá nhỏ
    public float BoulderDensity  = 0.005f;  // tảng đá lớn (boulder)

    // =========================================================
    // Preset factory methods theo zone
    // =========================================================

    // =========================================================
    // PRESET CHÍNH — sinh 1 map vuông liên tục, không phân zone
    // =========================================================

    /// <summary>
    /// Map thống nhất — 1 terrain vuông liên tục 490×490m.
    /// Đồi nhỏ nhấp nhô, không núi, không tuyết.
    /// Grid 140×140, cell 3.5m → ~117K vertices (đủ nhẹ cho mobile).
    /// </summary>
    public static TerrainConfig Unified(long seed = 0) => new()
    {
        GridWidth  = 140, GridHeight = 140,
        CellSize   = 3.5f, MaxHeight  = 12.0f,        // Elevated peak ~12m (đồi 7× player)
        // Noise multi-octave — đồng bằng + đồi nhỏ
        NoiseFrequency   = 0.012f,
        NoiseOctaves     = 4,
        NoiseLacunarity  = 2.1f,
        NoisePersistence = 0.45f,
        // Biome forest default (height-based)
        // Forest chỉ blend dirt→grass→rock với pure grass plateau (xem BiomeWeights)
        SandLevel  = 0.05f,
        GrassLevel = 0.50f,    // không dùng trong logic mới
        RockLevel  = 0.88f,
        SnowLevel  = 2.00f,    // tắt
        WalkableMaxSlope = 0.50f,    // tăng để Elevated terrace vẫn walkable
        // Base densities — sẽ × multiplier theo zone
        TreeDensity     = 0.05f,
        MushroomDensity = 0.015f,
        GrassDensity    = 0.30f,
        FlowerDensity   = 0.04f,
        RockDensity     = 0.020f,
        BoulderDensity  = 0.008f,
        Seed = seed
    };

    // =========================================================
    // PRESETS CŨ — giữ lại để tham chiếu, KHÔNG dùng hiện tại
    // =========================================================

    /// <summary>Làng trung tâm — gần như bằng phẳng, cỏ xanh nhiều</summary>
    public static TerrainConfig OriginVillage(long seed = 0) => new()
    {
        GridWidth  = 50, GridHeight = 50,
        CellSize   = 2.0f, MaxHeight = 2.5f,           // rất thoải
        NoiseFrequency = 0.02f, NoiseOctaves = 2,
        NoiseLacunarity = 2.0f, NoisePersistence = 0.35f,
        SandLevel = 0.10f, GrassLevel = 0.75f, RockLevel = 0.90f, SnowLevel = 0.98f,
        WalkableMaxSlope = 0.25f,
        TreeDensity = 0.02f, MushroomDensity = 0.01f,
        GrassDensity = 0.08f, FlowerDensity = 0.04f, RockDensity = 0.01f,
        Seed = seed
    };

    /// <summary>Echoing Forest — rừng rậm, đồi nhỏ nhẹ, nhiều cây</summary>
    public static TerrainConfig EchoingForest(long seed = 0) => new()
    {
        GridWidth  = 75, GridHeight = 75,
        CellSize   = 2.0f, MaxHeight = 5.0f,           // đồi nhỏ
        NoiseFrequency = 0.04f, NoiseOctaves = 4,
        NoiseLacunarity = 2.0f, NoisePersistence = 0.5f,
        SandLevel = 0.05f, GrassLevel = 0.65f, RockLevel = 0.85f, SnowLevel = 0.97f,
        WalkableMaxSlope = 0.35f,
        TreeDensity = 0.12f, MushroomDensity = 0.05f,
        GrassDensity = 0.20f, FlowerDensity = 0.06f, RockDensity = 0.02f,
        Seed = seed
    };

    /// <summary>Thunder Peak — gò đá, nhấp nhô, ít cây, nhiều đá</summary>
    public static TerrainConfig ThunderPeak(long seed = 0) => new()
    {
        GridWidth  = 75, GridHeight = 75,
        CellSize   = 2.0f, MaxHeight = 6.0f,           // gò đá — KHÔNG còn núi
        NoiseFrequency = 0.05f, NoiseOctaves = 4,
        NoiseLacunarity = 2.0f, NoisePersistence = 0.5f,
        SandLevel = 0.05f, GrassLevel = 0.35f, RockLevel = 0.60f, SnowLevel = 0.85f,
        WalkableMaxSlope = 0.40f,
        TreeDensity = 0.02f, MushroomDensity = 0.01f,
        GrassDensity = 0.04f, FlowerDensity = 0.01f, RockDensity = 0.10f,
        Seed = seed
    };

    /// <summary>Ancient Relics — đồng bằng cổ, nhiều cát và đá nhỏ</summary>
    public static TerrainConfig AncientRelics(long seed = 0) => new()
    {
        GridWidth  = 75, GridHeight = 75,
        CellSize   = 2.0f, MaxHeight = 4.0f,
        NoiseFrequency = 0.03f, NoiseOctaves = 3,
        NoiseLacunarity = 2.0f, NoisePersistence = 0.45f,
        SandLevel = 0.35f, GrassLevel = 0.60f, RockLevel = 0.82f, SnowLevel = 0.96f,
        WalkableMaxSlope = 0.30f,
        TreeDensity = 0.02f, MushroomDensity = 0.01f,
        GrassDensity = 0.05f, FlowerDensity = 0.02f, RockDensity = 0.08f,
        Seed = seed
    };

    /// <summary>Magic Castle — cao nguyên nhẹ, cỏ thưa và đá</summary>
    public static TerrainConfig MagicCastle(long seed = 0) => new()
    {
        GridWidth  = 75, GridHeight = 75,
        CellSize   = 2.0f, MaxHeight = 5.0f,
        NoiseFrequency = 0.035f, NoiseOctaves = 3,
        NoiseLacunarity = 2.0f, NoisePersistence = 0.45f,
        SandLevel = 0.05f, GrassLevel = 0.40f, RockLevel = 0.65f, SnowLevel = 0.85f,
        WalkableMaxSlope = 0.35f,
        TreeDensity = 0.03f, MushroomDensity = 0.01f,
        GrassDensity = 0.05f, FlowerDensity = 0.01f, RockDensity = 0.06f,
        Seed = seed
    };

    /// <summary>Connector góc chéo — 90×90m, thoải</summary>
    public static TerrainConfig Connector(long seed = 0) => new()
    {
        GridWidth  = 45, GridHeight = 45,
        CellSize   = 2.0f, MaxHeight = 2.5f,
        NoiseFrequency = 0.025f, NoiseOctaves = 2,
        NoiseLacunarity = 2.0f, NoisePersistence = 0.4f,
        SandLevel = 0.10f, GrassLevel = 0.70f, RockLevel = 0.88f, SnowLevel = 0.98f,
        WalkableMaxSlope = 0.22f,
        TreeDensity = 0.03f, MushroomDensity = 0.01f,
        GrassDensity = 0.10f, FlowerDensity = 0.02f, RockDensity = 0.01f,
        Seed = seed
    };

    /// <summary>Connector Bắc/Nam — 100×90m (lấp khoảng trống N và S của làng)</summary>
    public static TerrainConfig ConnectorNS(long seed = 0) => new()
    {
        GridWidth  = 50, GridHeight = 45,   // 100m × 90m
        CellSize   = 2.0f, MaxHeight = 2.5f,
        NoiseFrequency = 0.025f, NoiseOctaves = 2,
        NoiseLacunarity = 2.0f, NoisePersistence = 0.4f,
        SandLevel = 0.10f, GrassLevel = 0.70f, RockLevel = 0.88f, SnowLevel = 0.98f,
        WalkableMaxSlope = 0.22f,
        TreeDensity = 0.03f, MushroomDensity = 0.01f,
        GrassDensity = 0.10f, FlowerDensity = 0.02f, RockDensity = 0.01f,
        Seed = seed
    };

    /// <summary>Connector Đông/Tây — 90×100m (lấp khoảng trống E và W của làng)</summary>
    public static TerrainConfig ConnectorEW(long seed = 0) => new()
    {
        GridWidth  = 45, GridHeight = 50,   // 90m × 100m
        CellSize   = 2.0f, MaxHeight = 2.5f,
        NoiseFrequency = 0.025f, NoiseOctaves = 2,
        NoiseLacunarity = 2.0f, NoisePersistence = 0.4f,
        SandLevel = 0.10f, GrassLevel = 0.70f, RockLevel = 0.88f, SnowLevel = 0.98f,
        WalkableMaxSlope = 0.22f,
        TreeDensity = 0.03f, MushroomDensity = 0.01f,
        GrassDensity = 0.10f, FlowerDensity = 0.02f, RockDensity = 0.01f,
        Seed = seed
    };

    /// <summary>Đường nối hẹp (không dùng thường xuyên)</summary>
    public static TerrainConfig Path(long seed = 0) => new()
    {
        GridWidth  = 100, GridHeight = 20,
        CellSize   = 2.0f, MaxHeight = 2.0f,
        NoiseFrequency = 0.02f, NoiseOctaves = 2,
        NoiseLacunarity = 2.0f, NoisePersistence = 0.4f,
        SandLevel = 0.10f, GrassLevel = 0.72f, RockLevel = 0.88f, SnowLevel = 0.98f,
        WalkableMaxSlope = 0.18f,
        TreeDensity = 0.02f, MushroomDensity = 0.01f,
        GrassDensity = 0.08f, FlowerDensity = 0.02f, RockDensity = 0.01f,
        Seed = seed
    };

    public static TerrainConfig FromZoneType(string zoneType, long seed = 0) => zoneType switch
    {
        "Unified"       => Unified(seed),
        "OriginVillage" => OriginVillage(seed),
        "EchoingForest" => EchoingForest(seed),
        "ThunderPeak"   => ThunderPeak(seed),
        "AncientRelics" => AncientRelics(seed),
        "MagicCastle"   => MagicCastle(seed),
        "Path"          => Path(seed),
        "Connector"     => Connector(seed),
        "ConnectorNS"   => ConnectorNS(seed),
        "ConnectorEW"   => ConnectorEW(seed),
        _               => Unified(seed)
    };
}
