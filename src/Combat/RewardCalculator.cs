using System.Collections.Generic;
using Godot;

namespace FragmentOfJapanese.Combat;

/// <summary>
/// Tính thưởng cho một lượt ải. Thứ tự giá trị: quái &lt; quiz &lt; đoạn văn &lt; grammar.
/// Có TRẦN mỗi lượt + hệ số portal để "không quá cao". Pha 3 sẽ tinh chỉnh thêm.
/// </summary>
public static class RewardCalculator
{
    public const int BaseGoldPerEnemy = 12;
    public const int RunCap           = 600;   // trần Vàng mỗi lượt ải

    public static float KindMult(GameKind k) => k switch
    {
        GameKind.Quiz    => 1.3f,
        GameKind.Reading => 1.6f,
        GameKind.Grammar => 2.0f,
        _                => 1.0f,   // quái thường
    };

    /// <summary>Tổng Vàng = Σ base × kindMult × portalCoeff, chặn trần.</summary>
    public static int RunGold(IEnumerable<GameKind> defeated, float portalCoeff)
    {
        float sum = 0f;
        foreach (var k in defeated) sum += BaseGoldPerEnemy * KindMult(k) * portalCoeff;
        return Mathf.Min(RunCap, Mathf.RoundToInt(sum));
    }
}
