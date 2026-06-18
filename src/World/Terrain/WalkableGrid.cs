using Godot;

namespace FragmentOfJapanese.World;

/// <summary>
/// Lưới logic game — mỗi ô biết: đi được không, biome gì, độ cao bao nhiêu.
/// Chỉ là array C# thuần, không có Node hay visual.
/// Dùng để: spawn enemy, đặt object, fog of war, zone trigger, v.v.
/// </summary>
public class WalkableGrid
{
    public int   Width    { get; }
    public int   Height   { get; }
    public float CellSize { get; }

    public bool[,]  IsWalkable { get; }   // ô này có đi được không
    public float[,] HeightData { get; }   // độ cao thực (meters)
    public byte[,]  BiomeType  { get; }   // loại biome (xem hằng số bên dưới)

    // Biome constants
    public const byte BIOME_SAND  = 0;
    public const byte BIOME_GRASS = 1;
    public const byte BIOME_ROCK  = 2;
    public const byte BIOME_SNOW  = 3;

    public WalkableGrid(float[,] heightMap, TerrainConfig cfg)
    {
        Width    = cfg.GridWidth;
        Height   = cfg.GridHeight;
        CellSize = cfg.CellSize;

        IsWalkable = new bool[Width, Height];
        HeightData = new float[Width, Height];
        BiomeType  = new byte[Width, Height];

        for (int x = 0; x < Width; x++)
        for (int z = 0; z < Height; z++)
        {
            float h = heightMap[x, z];
            HeightData[x, z] = h * cfg.MaxHeight;
            BiomeType[x, z]  = ClassifyBiome(h, cfg);

            float slope = CalcMaxSlope(heightMap, x, z);
            IsWalkable[x, z] = slope < cfg.WalkableMaxSlope;
        }
    }

    // -------------------------------------------------------------------------
    // Coordinate conversion
    // -------------------------------------------------------------------------

    public (int x, int z) WorldToGrid(Vector3 worldPos)
    {
        int gx = Mathf.Clamp((int)(worldPos.X / CellSize), 0, Width  - 1);
        int gz = Mathf.Clamp((int)(worldPos.Z / CellSize), 0, Height - 1);
        return (gx, gz);
    }

    public Vector3 GridToWorld(int gx, int gz)
        => new Vector3(gx * CellSize, HeightData[gx, gz], gz * CellSize);

    // -------------------------------------------------------------------------
    // Queries
    // -------------------------------------------------------------------------

    public bool CanSpawnAt(Vector3 worldPos)
    {
        var (x, z) = WorldToGrid(worldPos);
        return IsWalkable[x, z];
    }

    public byte GetBiome(Vector3 worldPos)
    {
        var (x, z) = WorldToGrid(worldPos);
        return BiomeType[x, z];
    }

    public float GetHeight(Vector3 worldPos)
    {
        var (x, z) = WorldToGrid(worldPos);
        return HeightData[x, z];
    }

    /// <summary>Tìm ngẫu nhiên 1 ô đi được. Trả null nếu không tìm được sau 100 lần thử.</summary>
    public Vector3? RandomWalkablePos(RandomNumberGenerator rng)
    {
        for (int i = 0; i < 100; i++)
        {
            int x = rng.RandiRange(0, Width  - 1);
            int z = rng.RandiRange(0, Height - 1);
            if (IsWalkable[x, z])
                return GridToWorld(x, z);
        }
        return null;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static byte ClassifyBiome(float h, TerrainConfig cfg)
    {
        if (h < cfg.SandLevel)  return BIOME_SAND;
        if (h < cfg.GrassLevel) return BIOME_GRASS;
        if (h < cfg.SnowLevel)  return BIOME_ROCK;
        return BIOME_SNOW;
    }

    /// <summary>Slope = height diff tối đa so với 4 ô lân cận (normalized).</summary>
    private static float CalcMaxSlope(float[,] hmap, int x, int z)
    {
        int maxX = hmap.GetLength(0) - 1;
        int maxZ = hmap.GetLength(1) - 1;
        float c  = hmap[x, z];
        float s  = 0f;

        if (x > 0)    s = Mathf.Max(s, Mathf.Abs(hmap[x-1, z] - c));
        if (x < maxX) s = Mathf.Max(s, Mathf.Abs(hmap[x+1, z] - c));
        if (z > 0)    s = Mathf.Max(s, Mathf.Abs(hmap[x, z-1] - c));
        if (z < maxZ) s = Mathf.Max(s, Mathf.Abs(hmap[x, z+1] - c));

        return s;
    }
}
