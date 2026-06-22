using Godot;
using System;
using System.Collections.Generic;

namespace FragmentOfJapanese.Cosmetics;

/// <summary>
/// Sinh texture hiệu ứng PROCEDURAL (đổi màu được) — không cần art ngoài.
/// Màu được "nướng" thẳng vào texture (alpha theo hình); Sprite3D chỉ cần Modulate alpha để mờ dần.
/// Có cache theo (hình + màu + size) để không sinh lại.
/// </summary>
public static class FxTextures
{
    private static readonly Dictionary<string, ImageTexture> _cache = new();

    /// <summary>Đốm tròn mềm (bụi, tia, hào quang nhỏ).</summary>
    public static ImageTexture SoftCircle(Color c, int size = 64) =>
        Get("c", c, size, (nx, ny) =>
        {
            float d = Mathf.Sqrt(nx * nx + ny * ny);
            float a = Mathf.Clamp(1f - d, 0f, 1f);
            return a * a;
        });

    /// <summary>Vệt chém hình lưỡi liềm (đòn đánh thường).</summary>
    public static ImageTexture Crescent(Color c, int size = 96) =>
        Get("x", c, size, (nx, ny) =>
        {
            float dOuter = Mathf.Sqrt(nx * nx + ny * ny);
            float dInner = Mathf.Sqrt((nx - 0.5f) * (nx - 0.5f) + ny * ny);
            if (dOuter > 1f || dInner < 0.62f) return 0f;
            float edge = Mathf.Min(1f - dOuter, dInner - 0.62f);
            return Mathf.Clamp(edge * 5f, 0f, 1f);
        });

    /// <summary>Vòng tròn rỗng (hào quang lên cấp).</summary>
    public static ImageTexture Ring(Color c, int size = 96) =>
        Get("r", c, size, (nx, ny) =>
        {
            float d = Mathf.Sqrt(nx * nx + ny * ny);
            return Mathf.Clamp(1f - Mathf.Abs(d - 0.8f) / 0.18f, 0f, 1f);
        });

    private static ImageTexture Get(string prefix, Color c, int size, Func<float, float, float> shape)
    {
        string key = $"{prefix}_{(int)(c.R * 255)}_{(int)(c.G * 255)}_{(int)(c.B * 255)}_{size}";
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var img  = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        float half = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f - half) / half;
                float ny = (y + 0.5f - half) / half;
                float a  = Mathf.Clamp(shape(nx, ny), 0f, 1f);
                img.SetPixel(x, y, new Color(c.R, c.G, c.B, a));
            }

        var tex = ImageTexture.CreateFromImage(img);
        _cache[key] = tex;
        return tex;
    }
}
