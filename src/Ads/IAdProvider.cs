using System;

namespace FragmentOfJapanese.Ads;

/// <summary>
/// Lớp trừu tượng cho nguồn quảng cáo. Tách <see cref="AdManager"/> khỏi SDK cụ thể:
///   - <c>MockAdProvider</c>  : giả lập trong editor/desktop (test trọn luồng, không cần plugin).
///   - <c>AdMobProvider</c>   : quảng cáo thật qua plugin AdMob (chỉ build khi bật cờ ADMOB_ENABLED + chạy Android).
/// Nhờ vậy game vẫn chạy/biên dịch bình thường khi CHƯA cài plugin.
/// </summary>
public interface IAdProvider
{
    /// <summary>True nếu là quảng cáo thật (AdMob); false nếu giả lập.</summary>
    bool IsRealAds { get; }

    /// <summary>True nếu đã nạp sẵn 1 quảng cáo-thưởng, bấm là hiện ngay. (Mock luôn true.)</summary>
    bool IsRewardedReady { get; }

    /// <summary>Yêu cầu nạp trước 1 quảng cáo-thưởng (gọi lại khi lần trước nạp lỗi).</summary>
    void PreloadRewarded();

    /// <summary>Khởi tạo SDK + nạp trước (preload) interstitial/rewarded.</summary>
    void Initialize();

    void ShowBanner();
    void HideBanner();

    /// <summary><paramref name="onClosed"/> chạy khi quảng cáo đóng (hoặc không có sẵn).</summary>
    void ShowInterstitial(Action onClosed);

    /// <summary><paramref name="onEarned"/>(true) khi người chơi xem hết và nhận thưởng; (false) nếu bỏ qua/thất bại.</summary>
    void ShowRewarded(Action<bool> onEarned);
}
