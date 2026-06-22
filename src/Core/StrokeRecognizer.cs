using Godot;
using System;
using System.Collections.Generic;

namespace FragmentOfJapanese.Core;

/// <summary>
/// So khớp nét vẽ tay với mẫu (template). Chuẩn hóa theo bbox chung (giữ tỉ lệ), resample mỗi nét
/// về N điểm đều, so nét i ↔ nét i theo thứ tự → kiểm HÌNH + THỨ TỰ + HƯỚNG cùng lúc.
/// Đạt khi số nét khớp VÀ mọi nét trong ngưỡng. Ngưỡng nới vì mẫu là gần đúng (chỉnh sau).
/// </summary>
public static class StrokeRecognizer
{
    public const int Samples = 16;

    public class Result
    {
        public bool   Pass;
        public int    WrongStroke = -1;   // nét sai đầu tiên (0-based); -1 nếu không
        public float  Score;              // khoảng cách nét tệ nhất (càng nhỏ càng tốt)
        public string Message = "";
    }

    public static Result Match(IReadOnlyList<Vector2[]> drawn, Vector2[][] template, float threshold = 0.22f)
    {
        var r = new Result();
        if (template == null || template.Length == 0) { r.Message = "Chưa có mẫu nét."; return r; }

        int dn = drawn?.Count ?? 0;
        if (dn != template.Length)
        {
            r.WrongStroke = Math.Min(dn, template.Length);
            r.Message = $"Số nét chưa đúng: bạn vẽ {dn}, cần {template.Length}.";
            return r;
        }

        var dNorm = Normalize(drawn);
        var tNorm = Normalize(template);

        float worst = 0f; int worstIdx = -1;
        for (int s = 0; s < tNorm.Length; s++)
        {
            float d = AvgDist(Resample(dNorm[s], Samples), Resample(tNorm[s], Samples));
            if (d > worst) { worst = d; worstIdx = s; }
        }

        r.Score = worst;
        r.Pass  = worst <= threshold;
        r.Message = r.Pass ? "Chuẩn!" : $"Nét {worstIdx + 1} chưa khớp (hình/thứ tự/hướng).";
        if (!r.Pass) r.WrongStroke = worstIdx;
        return r;
    }

    // Chuẩn hóa toàn bộ nét theo bbox chung → [0,1], giữ tỉ lệ.
    private static Vector2[][] Normalize(IReadOnlyList<Vector2[]> strokes)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var st in strokes)
            foreach (var p in st)
            {
                if (p.X < minX) minX = p.X;  if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y;  if (p.Y > maxY) maxY = p.Y;
            }
        float scale  = 1f / Mathf.Max(Mathf.Max(maxX - minX, maxY - minY), 0.0001f);
        var   origin = new Vector2(minX, minY);

        var outStrokes = new Vector2[strokes.Count][];
        for (int s = 0; s < strokes.Count; s++)
        {
            var st  = strokes[s];
            var arr = new Vector2[st.Length];
            for (int i = 0; i < st.Length; i++) arr[i] = (st[i] - origin) * scale;
            outStrokes[s] = arr;
        }
        return outStrokes;
    }

    // Lấy mẫu lại 1 nét thành n điểm đều theo độ dài cung (giữ hướng).
    private static Vector2[] Resample(Vector2[] pts, int n)
    {
        if (pts.Length == 0) return new Vector2[n];
        if (pts.Length == 1) return Fill(pts[0], n);

        float total = 0f;
        for (int i = 1; i < pts.Length; i++) total += pts[i].DistanceTo(pts[i - 1]);
        if (total < 1e-5f) return Fill(pts[0], n);

        float step = total / (n - 1);
        var outPts = new List<Vector2> { pts[0] };
        float acc = 0f;
        Vector2 prev = pts[0];
        int idx = 1;
        while (outPts.Count < n && idx < pts.Length)
        {
            float segLen = pts[idx].DistanceTo(prev);
            if (segLen > 1e-6f && acc + segLen >= step)
            {
                float t = (step - acc) / segLen;
                var np = prev.Lerp(pts[idx], t);
                outPts.Add(np);
                prev = np;
                acc = 0f;
            }
            else { acc += segLen; prev = pts[idx]; idx++; }
        }
        while (outPts.Count < n) outPts.Add(pts[^1]);
        return outPts.ToArray();
    }

    private static Vector2[] Fill(Vector2 p, int n)
    {
        var o = new Vector2[n];
        for (int i = 0; i < n; i++) o[i] = p;
        return o;
    }

    private static float AvgDist(Vector2[] a, Vector2[] b)
    {
        int n = Mathf.Min(a.Length, b.Length);
        if (n == 0) return 1f;
        float sum = 0f;
        for (int i = 0; i < n; i++) sum += a[i].DistanceTo(b[i]);
        return sum / n;
    }
}
