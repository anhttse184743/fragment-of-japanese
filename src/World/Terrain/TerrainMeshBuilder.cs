using Godot;
using Godot.Collections;

namespace FragmentOfJapanese.World;

/// <summary>
/// Sinh ArrayMesh low-poly (flat normals) + skirt sâu + HeightMapShape3D.
///
/// Vertex COLOR là blend weight 4 biome:
///   r = grass, g = dirt, b = rock, a = sand
///
/// Khi có ZoneMap → override colors theo zone:
///   Sand zone       → COLOR = (0, 0, 0, 1) — cát thuần
///   RockWaste zone  → COLOR = (0, 0.4, 0.6, 0) — đất + đá
///   Village/Plain   → COLOR = (1, 0, 0, 0) — cỏ thuần
///   Elevated zone   → blend grass→rock theo height
///   Forest (default)→ COLOR = BiomeWeights(h) — như cũ
/// </summary>
public static class TerrainMeshBuilder
{
    private const float SkirtDepth = -12f;

    public static ArrayMesh Build(float[,] heightMap, TerrainConfig cfg, ZoneMap zones = null)
    {
        int gw = cfg.GridWidth;
        int gh = cfg.GridHeight;

        int surfaceVerts = gw * gh * 6;
        int skirtVerts   = 2 * (gw + gh) * 6;
        int total        = surfaceVerts + skirtVerts;

        var vertices = new Vector3[total];
        var normals  = new Vector3[total];
        var uvs      = new Vector2[total];
        var colors   = new Color[total];

        int idx = 0;

        // === Mặt trên ===
        for (int x = 0; x < gw; x++)
        for (int z = 0; z < gh; z++)
        {
            Vector3 v00 = GetV(x,   z,   heightMap, cfg);
            Vector3 v10 = GetV(x+1, z,   heightMap, cfg);
            Vector3 v01 = GetV(x,   z+1, heightMap, cfg);
            Vector3 v11 = GetV(x+1, z+1, heightMap, cfg);

            // Triangle 1: v00, v10, v11
            float h1   = (heightMap[x,z] + heightMap[x+1,z] + heightMap[x+1,z+1]) / 3f;
            float cx1  = (v00.X + v10.X + v11.X) / 3f;
            float cz1  = (v00.Z + v10.Z + v11.Z) / 3f;
            Color w1   = ComputeColor(h1, cx1, cz1, cfg, zones);
            AddTri(vertices, normals, uvs, colors, ref idx, v00, v10, v11, w1, useWorldUv: true);

            // Triangle 2: v00, v11, v01
            float h2   = (heightMap[x,z] + heightMap[x+1,z+1] + heightMap[x,z+1]) / 3f;
            float cx2  = (v00.X + v11.X + v01.X) / 3f;
            float cz2  = (v00.Z + v11.Z + v01.Z) / 3f;
            Color w2   = ComputeColor(h2, cx2, cz2, cfg, zones);
            AddTri(vertices, normals, uvs, colors, ref idx, v00, v11, v01, w2, useWorldUv: true);
        }

        // === Skirt — pure dirt COLOR=(0,1,0,0) ===
        Color dirt = new Color(0f, 1f, 0f, 0f);

        for (int x = 0; x < gw; x++)
        {
            var tl = GetV(x,   0, heightMap, cfg);
            var tr = GetV(x+1, 0, heightMap, cfg);
            var bl = new Vector3(tl.X, SkirtDepth, tl.Z);
            var br = new Vector3(tr.X, SkirtDepth, tr.Z);
            AddTri(vertices, normals, uvs, colors, ref idx, tl, br, bl, dirt, useWorldUv: false);
            AddTri(vertices, normals, uvs, colors, ref idx, tl, tr, br, dirt, useWorldUv: false);
        }
        for (int x = 0; x < gw; x++)
        {
            var tl = GetV(x,   gh, heightMap, cfg);
            var tr = GetV(x+1, gh, heightMap, cfg);
            var bl = new Vector3(tl.X, SkirtDepth, tl.Z);
            var br = new Vector3(tr.X, SkirtDepth, tr.Z);
            AddTri(vertices, normals, uvs, colors, ref idx, tl, bl, br, dirt, useWorldUv: false);
            AddTri(vertices, normals, uvs, colors, ref idx, tl, br, tr, dirt, useWorldUv: false);
        }
        for (int z = 0; z < gh; z++)
        {
            var tl = GetV(0, z,   heightMap, cfg);
            var tr = GetV(0, z+1, heightMap, cfg);
            var bl = new Vector3(tl.X, SkirtDepth, tl.Z);
            var br = new Vector3(tr.X, SkirtDepth, tr.Z);
            AddTri(vertices, normals, uvs, colors, ref idx, tl, bl, br, dirt, useWorldUv: false);
            AddTri(vertices, normals, uvs, colors, ref idx, tl, br, tr, dirt, useWorldUv: false);
        }
        for (int z = 0; z < gh; z++)
        {
            var tl = GetV(gw, z,   heightMap, cfg);
            var tr = GetV(gw, z+1, heightMap, cfg);
            var bl = new Vector3(tl.X, SkirtDepth, tl.Z);
            var br = new Vector3(tr.X, SkirtDepth, tr.Z);
            AddTri(vertices, normals, uvs, colors, ref idx, tl, br, bl, dirt, useWorldUv: false);
            AddTri(vertices, normals, uvs, colors, ref idx, tl, tr, br, dirt, useWorldUv: false);
        }

        var arrays = new Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.TexUV]  = uvs;
        arrays[(int)Mesh.ArrayType.Color]  = colors;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    // -------------------------------------------------------------------------

    private static Vector3 GetV(int x, int z, float[,] hmap, TerrainConfig cfg)
        => new Vector3(x * cfg.CellSize, hmap[x, z] * cfg.MaxHeight, z * cfg.CellSize);

    private static void AddTri(
        Vector3[] verts, Vector3[] norms, Vector2[] uvs, Color[] cols,
        ref int idx,
        Vector3 a, Vector3 b, Vector3 c,
        Color weight, bool useWorldUv)
    {
        Vector3 n = (b - a).Cross(c - a).Normalized();
        Vector2 ua = useWorldUv ? new Vector2(a.X, a.Z) : new Vector2(a.X + a.Z, a.Y);
        Vector2 ub = useWorldUv ? new Vector2(b.X, b.Z) : new Vector2(b.X + b.Z, b.Y);
        Vector2 uc = useWorldUv ? new Vector2(c.X, c.Z) : new Vector2(c.X + c.Z, c.Y);

        verts[idx] = a; norms[idx] = n; uvs[idx] = ua; cols[idx] = weight; idx++;
        verts[idx] = b; norms[idx] = n; uvs[idx] = ub; cols[idx] = weight; idx++;
        verts[idx] = c; norms[idx] = n; uvs[idx] = uc; cols[idx] = weight; idx++;
    }

    // -------------------------------------------------------------------------
    // Color computation — blend zone với forest default
    // -------------------------------------------------------------------------

    private static Color ComputeColor(float h, float wx, float wz, TerrainConfig cfg, ZoneMap zones)
    {
        Color forest = BiomeWeights(h, cfg);
        if (zones == null) return forest;

        var (zone, w) = zones.GetDominantZone(wx, wz);
        if (w < 0.005f) return forest;

        // Color theo zone (r=grass, g=dirt, b=rock, a=sand)
        Color zc = zone switch
        {
            BiomeZone.Village    => new Color(1.0f, 0.0f, 0.0f, 0.0f),  // cỏ thuần
            BiomeZone.Sand       => new Color(0.0f, 0.0f, 0.0f, 1.0f),  // cát thuần
            BiomeZone.RockWaste  => new Color(0.0f, 0.45f, 0.55f, 0.0f), // đất + đá
            BiomeZone.GrassPlain => new Color(1.0f, 0.0f, 0.0f, 0.0f),  // cỏ thuần
            BiomeZone.Elevated   => h > 0.75f
                                    ? new Color(0.15f, 0.15f, 0.70f, 0.0f) // đỉnh = đá xám
                                    : h > 0.50f
                                        ? new Color(0.55f, 0.35f, 0.10f, 0.0f) // giữa = cỏ + đất
                                        : new Color(1.0f, 0.0f, 0.0f, 0.0f),    // chân = cỏ thuần
            _ => forest,
        };

        return Lerp(forest, zc, w);
    }

    /// <summary>Blend weight biome theo height (forest default).</summary>
    private static Color BiomeWeights(float h, TerrainConfig cfg)
    {
        const float blendW = 0.05f;
        float sand = 0, grass = 0, rock = 0;

        if (h <= cfg.SandLevel)
        {
            sand = 1f;
        }
        else if (h <= cfg.SandLevel + blendW)
        {
            float t = Mathf.SmoothStep(0f, 1f, (h - cfg.SandLevel) / blendW);
            sand = 1f - t; grass = t;
        }
        else if (h <= cfg.RockLevel - blendW)
        {
            grass = 1f;
        }
        else if (h <= cfg.RockLevel)
        {
            float t = Mathf.SmoothStep(0f, 1f, (h - (cfg.RockLevel - blendW)) / blendW);
            grass = 1f - t; rock = t;
        }
        else
        {
            rock = 1f;
        }

        // Forest dùng kênh g (dirt) cho vùng thấp thay vì a (sand)
        // → forest có chuyển dirt→grass tự nhiên, KHÔNG dùng sand texture
        return new Color(grass, sand, rock, 0f);
    }

    private static Color Lerp(Color a, Color b, float t)
        => new Color(
            Mathf.Lerp(a.R, b.R, t),
            Mathf.Lerp(a.G, b.G, t),
            Mathf.Lerp(a.B, b.B, t),
            Mathf.Lerp(a.A, b.A, t));

    // -------------------------------------------------------------------------

    public static HeightMapShape3D BuildCollision(float[,] heightMap, TerrainConfig cfg)
    {
        int w = cfg.GridWidth  + 1;
        int d = cfg.GridHeight + 1;
        var data = new float[w * d];
        for (int z = 0; z < d; z++)
        for (int x = 0; x < w; x++)
            data[z * w + x] = heightMap[x, z] * cfg.MaxHeight;

        return new HeightMapShape3D { MapWidth = w, MapDepth = d, MapData = data };
    }
}
