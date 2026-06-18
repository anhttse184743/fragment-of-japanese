using Godot;

namespace FragmentOfJapanese.World;

/// <summary>
/// Sinh heightmap từ FastNoiseLite, sau đó apply zone modifiers.
///
/// Per-zone modifiers:
///   Village    : phẳng thấp (~0.20)
///   Sand       : phẳng thấp + gợn nhẹ (~0.05-0.15)
///   RockWaste  : amplify peaks (~0.40-0.95)
///   GrassPlain : phẳng đều (~0.25-0.30)
///   Elevated   : terraced 4 tầng (~0.45-0.90)
///   Forest     : noise gốc (không đổi)
/// </summary>
public static class TerrainGenerator
{
    public static float[,] Generate(TerrainConfig cfg, ZoneMap zones = null)
    {
        var noise = new FastNoiseLite();
        noise.Seed              = (int)(cfg.Seed & 0x7FFFFFFF);
        noise.NoiseType         = FastNoiseLite.NoiseTypeEnum.Simplex;
        noise.FractalType       = FastNoiseLite.FractalTypeEnum.Fbm;
        noise.Frequency         = cfg.NoiseFrequency;
        noise.FractalOctaves    = cfg.NoiseOctaves;
        noise.FractalLacunarity = cfg.NoiseLacunarity;
        noise.FractalGain       = cfg.NoisePersistence;

        int w = cfg.GridWidth  + 1;
        int h = cfg.GridHeight + 1;
        var map = new float[w, h];

        float min = float.MaxValue;
        float max = float.MinValue;

        // 1. Sample raw noise
        for (int x = 0; x < w; x++)
        for (int z = 0; z < h; z++)
        {
            float v = noise.GetNoise2D(x, z);
            map[x, z] = v;
            if (v < min) min = v;
            if (v > max) max = v;
        }

        // 2. Normalize [0, 1]
        float range = max - min;
        if (range < 0.0001f) range = 1f;
        for (int x = 0; x < w; x++)
        for (int z = 0; z < h; z++)
            map[x, z] = (map[x, z] - min) / range;

        // 3. Apply zone modifiers
        if (zones != null)
            ApplyZoneHeights(map, cfg, zones);

        return map;
    }

    // -------------------------------------------------------------------------

    private static void ApplyZoneHeights(float[,] map, TerrainConfig cfg, ZoneMap zones)
    {
        int w = cfg.GridWidth  + 1;
        int h = cfg.GridHeight + 1;

        for (int x = 0; x < w; x++)
        for (int z = 0; z < h; z++)
        {
            float wx = x * cfg.CellSize;
            float wz = z * cfg.CellSize;
            var (zone, weight) = zones.GetDominantZone(wx, wz);
            if (weight < 0.005f) continue;

            float baseH  = map[x, z];
            float target = baseH;

            switch (zone)
            {
                case BiomeZone.Village:
                    target = 0.20f + baseH * 0.05f;     // gần như phẳng
                    break;
                case BiomeZone.Sand:
                    target = 0.05f + baseH * 0.15f;     // thấp, gợn dune
                    break;
                case BiomeZone.RockWaste:
                    target = 0.45f + baseH * 0.45f;     // cao, nhiều đá nhọn
                    break;
                case BiomeZone.GrassPlain:
                    target = 0.25f + baseH * 0.08f;     // phẳng đều, hơi gợn
                    break;
                case BiomeZone.Elevated:
                    // ĐỒI BẬC THANG — radial hill, cao ở giữa, thấp ở mép
                    // Distance từ tâm zone Elevated → hillT (1 ở tâm, 0 ở mép)
                    float dx     = wx - zones.ElevatedCenter.X;
                    float dz     = wz - zones.ElevatedCenter.Z;
                    float distEl = Mathf.Sqrt(dx * dx + dz * dz);
                    float hillT  = Mathf.Clamp(1f - distEl / ZoneMap.CornerRadius, 0f, 1f);

                    // 85% hill shape + 15% noise variation → terrace gần như concentric
                    float combined = hillT * 0.85f + baseH * 0.15f;

                    // Snap về 6 tầng → bậc thang rõ ràng
                    const int levels = 6;
                    float terrace = Mathf.Floor(combined * levels) / levels;

                    // Map: 0.25 (mép, thấp) → 1.0 (tâm, đỉnh)
                    target = 0.25f + terrace * 0.75f;
                    break;
            }

            map[x, z] = Mathf.Lerp(baseH, target, weight);
        }
    }
}
