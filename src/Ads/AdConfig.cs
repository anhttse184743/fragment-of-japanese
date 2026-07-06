namespace FragmentOfJapanese.Ads;

/// <summary>
/// Cấu hình tập trung cho hệ thống quảng cáo (AdMob).
///
/// Hiện đang dùng **ID quảng cáo THỬ NGHIỆM của Google** — luôn hiển thị quảng cáo test
/// trên mọi thiết bị, an toàn khi phát triển (không vi phạm chính sách AdMob).
/// Khi phát hành thật: thay các ID dưới bằng ID thật trong AdMob console.
///
/// LƯU Ý: App ID dùng dấu '~', còn Ad Unit ID dùng dấu '/'.
/// App ID phải đặt trong AndroidManifest (xem docs/ADMOB_SETUP.md).
/// </summary>
public static class AdConfig
{
    // ───────── ID THẬT (AdMob của FOJ) ─────────
    /// <summary>App ID thật (meta-data com.google.android.gms.ads.APPLICATION_ID trong Manifest).</summary>
    public const string AndroidAppId = "ca-app-pub-1042984115395141~5427628638";

    /// <summary>Rewarded THẬT — dùng cho rương xem quảng cáo nhận thưởng.</summary>
    public const string AndroidRewardedId      = "ca-app-pub-1042984115395141/8385834009";

    // Banner/Interstitial hiện KHÔNG dùng trong game (đã bỏ) → giữ ID test của Google
    // để không request quảng cáo thật ở các định dạng chưa tạo. Nếu sau này dùng, tạo đơn vị
    // riêng trong AdMob rồi thay 2 dòng dưới.
    public const string AndroidBannerId       = "ca-app-pub-3940256099942544/6300978111";
    public const string AndroidInterstitialId = "ca-app-pub-3940256099942544/1033173712";

    // ───────── Thiết bị TEST (tự xem an toàn, không bị ban) ─────────
    /// <summary>
    /// Mã thiết bị test: khi có, điện thoại của bạn sẽ nhận QUẢNG CÁO TEST dù dùng ID thật
    /// → tự xem/bấm thoải mái, không tính doanh thu, KHÔNG bị ban.
    /// Cách lấy: chạy game 1 lần trên máy, xem logcat dòng
    ///   "Use ... setTestDeviceIds(Arrays.asList(\"ABC123...\")) to get test ads"
    /// rồi dán mã "ABC123..." vào mảng dưới.
    /// </summary>
    public static readonly string[] TestDeviceIds =
    {
        // "DÁN_MÃ_THIẾT_BỊ_CỦA_BẠN_VÀO_ĐÂY",
    };

    // ───────── Tinh chỉnh gameplay/kinh tế ─────────
    /// <summary>Số lần xem quảng cáo-thưởng tối đa mỗi ngày (khớp mockup "1–5" trong tab Nạp).</summary>
    public const int DailyRewardCap = 5;

    /// <summary>Vàng nhận được mỗi lần xem quảng cáo-thưởng.</summary>
    public const int RewardGoldPerAd = 100;

    /// <summary>Khoảng cách tối thiểu (giây) giữa 2 interstitial — tránh làm phiền.</summary>
    public const float InterstitialCooldownSec = 90f;

    /// <summary>Chỉ hiện interstitial mỗi N lần chuyển cảnh đủ điều kiện.</summary>
    public const int InterstitialEveryNScenes = 2;

    // ───────── Truy cập theo nền tảng (sẵn cho iOS sau này) ─────────
    public static string BannerId       => AndroidBannerId;
    public static string InterstitialId => AndroidInterstitialId;
    public static string RewardedId      => AndroidRewardedId;
}
