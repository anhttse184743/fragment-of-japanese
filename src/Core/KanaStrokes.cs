using Godot;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FragmentOfJapanese.Core;

/// <summary>
/// Mẫu nét của một chữ kana: danh sách nét theo ĐÚNG thứ tự; mỗi nét là dãy điểm [x,y] (0..1,
/// gốc trên-trái) theo ĐÚNG hướng vẽ. Dùng cho game vẽ chữ (so khớp hình + thứ tự + hướng).
/// </summary>
public class KanaStrokes
{
    [JsonPropertyName("id")]      public string Id     { get; set; } = "";
    [JsonPropertyName("romaji")]  public string Romaji { get; set; } = "";
    [JsonPropertyName("strokes")] public List<List<float[]>> Strokes { get; set; } = new();

    /// <summary>Chuyển sang mảng nét Vector2 để recognizer/canvas dùng.</summary>
    public Vector2[][] ToStrokes()
    {
        var result = new Vector2[Strokes.Count][];
        for (int s = 0; s < Strokes.Count; s++)
        {
            var pts = Strokes[s];
            var arr = new Vector2[pts.Count];
            for (int i = 0; i < pts.Count; i++)
            {
                float x = pts[i].Length > 0 ? pts[i][0] : 0f;
                float y = pts[i].Length > 1 ? pts[i][1] : 0f;
                arr[i] = new Vector2(x, y);
            }
            result[s] = arr;
        }
        return result;
    }
}
