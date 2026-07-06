// ───────────────────────────────────────────────────────────────────────────
//  QUẢNG CÁO THẬT qua plugin AdMob (Poing Studios — godot-admob-plugin, hỗ trợ C#).
//
//  Toàn bộ file chỉ được biên dịch khi định nghĩa hằng ADMOB_ENABLED.
//  → Khi CHƯA cài plugin, file này bị bỏ qua nên project vẫn build/chạy bình thường.
//
//  CÁCH BẬT (sau khi cài plugin + Android Build Template — xem docs/ADMOB_SETUP.md):
//    Thêm vào "FRAGMENT OF JAPANESE.csproj":
//      <PropertyGroup Condition=" '$(GodotTargetPlatform)' == 'android' ">
//        <DefineConstants>$(DefineConstants);ADMOB_ENABLED</DefineConstants>
//      </PropertyGroup>
//
//  Tên class/callback bám theo tài liệu plugin. Nếu phiên bản plugin bạn cài đặt
//  tên hơi khác (vd FullScreenContentCallback), chỉ cần sửa trong DUY NHẤT file này.
// ───────────────────────────────────────────────────────────────────────────
#if ADMOB_ENABLED
using System;
using Godot;
using PoingStudios.AdMob.Api;
using PoingStudios.AdMob.Api.Core;
using PoingStudios.AdMob.Api.Listeners;

namespace FragmentOfJapanese.Ads;

public sealed class AdMobProvider : IAdProvider
{
    private readonly AdManager _host;

    private AdView         _banner;
    private InterstitialAd _interstitial;
    private RewardedAd     _rewarded;

    public AdMobProvider(AdManager host) => _host = host;

    public bool IsRealAds => true;

    public void Initialize()
    {
        // Khai thiết bị test (nếu có) → máy bạn ra quảng cáo test, xem an toàn không bị ban.
        var cfg = new RequestConfiguration();
        cfg.TestDeviceIds.Add(RequestConfiguration.DeviceIdEmulator);   // máy ảo luôn ra ad test
        foreach (var id in AdConfig.TestDeviceIds)
            if (!string.IsNullOrEmpty(id)) cfg.TestDeviceIds.Add(id);
        MobileAds.SetRequestConfiguration(cfg);

        MobileAds.Initialize();
        LoadRewarded();
        GD.Print($"[Ads] AdMob khởi tạo (ID THẬT). Test devices: {AdConfig.TestDeviceIds.Length}.");
    }

    // ───────── Banner ─────────
    public void ShowBanner()
    {
        if (_banner != null) return;
        _banner = new AdView(AdConfig.BannerId, AdSize.Banner, AdPosition.Top);
        _banner.LoadAd(new AdRequest());   // AdView tự hiện khi nạp xong
    }

    public void HideBanner()
    {
        _banner?.Destroy();
        _banner = null;
    }

    // ───────── Interstitial ─────────
    public void ShowInterstitial(Action onClosed)
    {
        if (_interstitial == null)
        {
            onClosed?.Invoke();   // chưa nạp kịp → bỏ qua mượt, nạp cho lần sau
            LoadInterstitial();
            return;
        }

        _interstitial.FullScreenContentCallback = new FullScreenContentCallback
        {
            OnAdDismissedFullScreenContent    = () => { _interstitial = null; LoadInterstitial(); onClosed?.Invoke(); },
            OnAdFailedToShowFullScreenContent = _  => { _interstitial = null; LoadInterstitial(); onClosed?.Invoke(); },
        };
        _interstitial.Show();
    }

    // ───────── Rewarded ─────────
    public void ShowRewarded(Action<bool> onEarned)
    {
        if (_rewarded == null)
        {
            onEarned?.Invoke(false);
            LoadRewarded();
            return;
        }

        bool earned = false;
        _rewarded.FullScreenContentCallback = new FullScreenContentCallback
        {
            OnAdDismissedFullScreenContent    = () => { _rewarded = null; LoadRewarded(); onEarned?.Invoke(earned); },
            OnAdFailedToShowFullScreenContent = _  => { _rewarded = null; LoadRewarded(); onEarned?.Invoke(false); },
        };
        _rewarded.Show(new OnUserEarnedRewardListener
        {
            OnUserEarnedReward = reward =>
            {
                earned = true;
                GD.Print($"[Ads] Rewarded earned: {reward.Amount} {reward.Type}");
            }
        });
    }

    // ───────── Preload ─────────
    private void LoadInterstitial()
    {
        new InterstitialAdLoader().Load(AdConfig.InterstitialId, new AdRequest(),
            new InterstitialAdLoadCallback
            {
                OnAdLoaded       = ad  => _interstitial = ad,
                OnAdFailedToLoad = err => { _interstitial = null; GD.PushWarning($"[Ads] Interstitial nạp lỗi: {err}"); },
            });
    }

    private void LoadRewarded()
    {
        new RewardedAdLoader().Load(AdConfig.RewardedId, new AdRequest(),
            new RewardedAdLoadCallback
            {
                OnAdLoaded       = ad  => _rewarded = ad,
                OnAdFailedToLoad = err => { _rewarded = null; GD.PushWarning($"[Ads] Rewarded nạp lỗi: {err}"); },
            });
    }
}
#endif
