using Godot;
using System;

namespace FragmentOfJapanese.World;

/// <summary>
/// 5 zone biome chính trên 1 map vuông liên tục:
///   - Forest       (mặc định bao quanh) — rừng cây dày
///   - Village      (tâm map)            — phẳng, chỗ đặt công trình
///   - Sand         (NW corner)          — sa mạc cát
///   - RockWaste    (NE corner)          — đất + đá hoang
///   - GrassPlain   (SW corner)          — đồng bằng cỏ phẳng
///   - Elevated     (SE corner)          — cao nguyên phân tầng
///
/// Mỗi corner là 1 hình tròn radius 75m, blend mượt với forest qua 18m.
/// </summary>
public enum BiomeZone : byte
{
    Forest     = 0,  // mặc định
    Village    = 1,
    Sand       = 2,
    RockWaste  = 3,
    GrassPlain = 4,
    Elevated   = 5,
}

public class ZoneMap
{
    // Bán kính từng vùng
    public const float VillageRadius = 38f;
    public const float CornerRadius  = 75f;

    // Khoảng cách từ tâm map đến tâm zone góc
    public const float CornerOffset  = 180f;

    // Độ rộng dải blend mượt giữa zone và forest
    public const float BlendWidth    = 18f;

    // World coords (heightmap grid origin = 0,0 → center map = halfW, halfH)
    private readonly float _cX;
    private readonly float _cZ;

    public Vector3 VillageCenter    { get; }
    public Vector3 SandCenter       { get; }
    public Vector3 RockWasteCenter  { get; }
    public Vector3 GrassPlainCenter { get; }
    public Vector3 ElevatedCenter   { get; }

    public ZoneMap(TerrainConfig cfg)
    {
        _cX = cfg.GridWidth  * cfg.CellSize * 0.5f;
        _cZ = cfg.GridHeight * cfg.CellSize * 0.5f;

        VillageCenter    = new(_cX,                  0, _cZ);
        SandCenter       = new(_cX - CornerOffset,   0, _cZ - CornerOffset);   // NW
        RockWasteCenter  = new(_cX + CornerOffset,   0, _cZ - CornerOffset);   // NE
        GrassPlainCenter = new(_cX - CornerOffset,   0, _cZ + CornerOffset);   // SW
        ElevatedCenter   = new(_cX + CornerOffset,   0, _cZ + CornerOffset);   // SE
    }

    /// <summary>Zone identity tại world XZ (không blend, chỉ trả về 1 giá trị).</summary>
    public BiomeZone GetZone(float wx, float wz)
    {
        if (Dist(wx, wz, _cX, _cZ) < VillageRadius)
            return BiomeZone.Village;
        if (Dist(wx, wz, SandCenter.X, SandCenter.Z) < CornerRadius)
            return BiomeZone.Sand;
        if (Dist(wx, wz, RockWasteCenter.X, RockWasteCenter.Z) < CornerRadius)
            return BiomeZone.RockWaste;
        if (Dist(wx, wz, GrassPlainCenter.X, GrassPlainCenter.Z) < CornerRadius)
            return BiomeZone.GrassPlain;
        if (Dist(wx, wz, ElevatedCenter.X, ElevatedCenter.Z) < CornerRadius)
            return BiomeZone.Elevated;
        return BiomeZone.Forest;
    }

    /// <summary>
    /// Trọng số influence của zone tại XZ: 1=trong tâm, 0=ngoài blend.
    /// Smoothstep cho transition mượt.
    /// </summary>
    public float GetZoneWeight(float wx, float wz, BiomeZone zone)
    {
        Vector3 center;
        float   radius;
        switch (zone)
        {
            case BiomeZone.Village:    center = VillageCenter;    radius = VillageRadius; break;
            case BiomeZone.Sand:       center = SandCenter;       radius = CornerRadius;  break;
            case BiomeZone.RockWaste:  center = RockWasteCenter;  radius = CornerRadius;  break;
            case BiomeZone.GrassPlain: center = GrassPlainCenter; radius = CornerRadius;  break;
            case BiomeZone.Elevated:   center = ElevatedCenter;   radius = CornerRadius;  break;
            default: return 0f;
        }

        float d = Dist(wx, wz, center.X, center.Z);
        if (d <= radius - BlendWidth) return 1f;
        if (d >= radius)              return 0f;
        return 1f - Mathf.SmoothStep(0f, 1f, (d - (radius - BlendWidth)) / BlendWidth);
    }

    /// <summary>Zone có influence cao nhất tại XZ.</summary>
    public (BiomeZone zone, float weight) GetDominantZone(float wx, float wz)
    {
        float wV = GetZoneWeight(wx, wz, BiomeZone.Village);
        float wS = GetZoneWeight(wx, wz, BiomeZone.Sand);
        float wR = GetZoneWeight(wx, wz, BiomeZone.RockWaste);
        float wG = GetZoneWeight(wx, wz, BiomeZone.GrassPlain);
        float wE = GetZoneWeight(wx, wz, BiomeZone.Elevated);

        float maxW = Math.Max(wV, Math.Max(wS, Math.Max(wR, Math.Max(wG, wE))));
        if (maxW < 0.001f) return (BiomeZone.Forest, 0f);
        if (maxW == wV)    return (BiomeZone.Village, wV);
        if (maxW == wS)    return (BiomeZone.Sand, wS);
        if (maxW == wR)    return (BiomeZone.RockWaste, wR);
        if (maxW == wG)    return (BiomeZone.GrassPlain, wG);
        return (BiomeZone.Elevated, wE);
    }

    private static float Dist(float x1, float z1, float x2, float z2)
    {
        float dx = x1 - x2, dz = z1 - z2;
        return MathF.Sqrt(dx * dx + dz * dz);
    }
}
