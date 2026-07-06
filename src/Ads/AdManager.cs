using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Godot;
using FragmentOfJapanese.Items;

namespace FragmentOfJapanese.Ads;

/// <summary>
/// Trung tâm quảng cáo (autoload). Là <see cref="CanvasLayer"/> để overlay giả-lập nằm trên cùng.
///
/// API gọi từ bất cứ đâu:
///   AdManager.Instance.ShowBanner() / HideBanner()
///   await AdManager.Instance.MaybeShowInterstitialAsync()   // tự giới hạn tần suất
///   AdManager.Instance.WatchRewardedForGold()               // xem QC nhận Vàng (có cap/ngày)
///   AdManager.Instance.SetAdsRemoved(true)                  // "Gỡ QC" (banner+interstitial)
///
/// - Chọn provider tự động: AdMob thật (Android + cờ ADMOB_ENABLED) hoặc giả lập (editor/desktop).
/// - Tự lưu trạng thái "đã gỡ QC" + số lần xem QC-thưởng trong ngày vào user://ads.save.json
///   (giống QuestManager — KHÔNG dùng SaveSystem để tránh ghi đè blob chung).
/// </summary>
public partial class AdManager : CanvasLayer
{
    public static AdManager Instance { get; private set; }

    private const string SavePath = "user://ads.save.json";

    private IAdProvider _provider;

    // ── Trạng thái có lưu ──
    public bool AdsRemoved   { get; private set; }
    public int  RewardsToday { get; private set; }
    private string _lastRewardDate = "";

    // ── Gating interstitial ──
    private int    _sceneCount;
    private double _lastInterstitialMs = -1_000_000;

    // ── Sự kiện cho UI ──
    public event Action<int>    RewardGranted;      // (cũ) số Vàng vừa nhận
    public event Action<string> AdChestClaimed;     // mô tả phần thưởng rương vừa mở
    public event Action         AdsRemovedChanged;

    public int  DailyRewardCap   => AdConfig.DailyRewardCap;
    public bool CanWatchRewarded => !_lastRewardDate.Equals(Today) || RewardsToday < AdConfig.DailyRewardCap;
    public bool IsRealAds        => _provider?.IsRealAds ?? false;
    /// <summary>Đã nạp sẵn quảng cáo-thưởng chưa (bấm là hiện ngay). Mock luôn true.</summary>
    public bool IsRewardedReady  => _provider?.IsRewardedReady ?? false;
    /// <summary>Thử nạp trước quảng cáo-thưởng (khi chưa sẵn sàng, để lần bấm sau có ad).</summary>
    public void PreloadRewarded() => _provider?.PreloadRewarded();

    private static string Today => DateTime.Now.ToString("yyyy-MM-dd");

    public override void _Ready()
    {
        Instance = this;
        Layer    = 60;            // trên ConfirmUi (50) — overlay QC giả phủ mọi thứ

        Load();
        ResetDailyIfNeeded();

#if ADMOB_ENABLED
        _provider = (OS.GetName() is "Android" or "iOS")
            ? new AdMobProvider(this)
            : new MockAdProvider(this);
#else
        _provider = new MockAdProvider(this);
#endif
        _provider.Initialize();
        GD.Print($"[Ads] Provider = {(_provider.IsRealAds ? "AdMob (thật)" : "Mock (giả lập)")}, AdsRemoved={AdsRemoved}");
    }

    // ───────────────────────── Banner ─────────────────────────
    public void ShowBanner()
    {
        if (AdsRemoved) return;
        _provider?.ShowBanner();
    }

    public void HideBanner() => _provider?.HideBanner();

    // ───────────────────────── Interstitial ─────────────────────────
    /// <summary>Hiện interstitial ngay (nếu chưa gỡ QC). onClosed luôn được gọi.</summary>
    public void ShowInterstitial(Action onClosed = null)
    {
        if (AdsRemoved || _provider == null) { onClosed?.Invoke(); return; }
        _provider.ShowInterstitial(onClosed);
    }

    /// <summary>
    /// Hiện interstitial CÓ KIỂM SOÁT TẦN SUẤT (mỗi N lần chuyển cảnh + cooldown).
    /// Dùng cho chuyển cảnh: <c>await AdManager.Instance.MaybeShowInterstitialAsync();</c>
    /// </summary>
    public async Task MaybeShowInterstitialAsync()
    {
        if (!ShouldShowInterstitial()) return;

        var tcs = new TaskCompletionSource();
        _provider.ShowInterstitial(() => tcs.TrySetResult());
        await tcs.Task;
    }

    private bool ShouldShowInterstitial()
    {
        _sceneCount++;
        if (AdsRemoved || _provider == null) return false;
        if (_sceneCount % AdConfig.InterstitialEveryNScenes != 0) return false;

        double now = Time.GetTicksMsec();
        if (now - _lastInterstitialMs < AdConfig.InterstitialCooldownSec * 1000.0) return false;

        _lastInterstitialMs = now;
        return true;
    }

    // ───────────────────────── Rewarded ─────────────────────────
    /// <summary>Xem quảng cáo-thưởng. onEarned(true) nếu xem hết. KHÔNG tự cộng tiền — dùng cho phần thưởng tùy biến.</summary>
    public void ShowRewarded(Action<bool> onEarned)
    {
        ResetDailyIfNeeded();
        if (_provider == null) { onEarned?.Invoke(false); return; }
        if (!CanWatchRewarded)
        {
            GD.Print("[Ads] Đã hết lượt xem QC-thưởng hôm nay.");
            onEarned?.Invoke(false);
            return;
        }

        _provider.ShowRewarded(earned =>
        {
            if (earned)
            {
                RewardsToday++;
                Save();
            }
            onEarned?.Invoke(earned);
        });
    }

    /// <summary>Tiện ích: xem quảng cáo-thưởng → cộng thẳng Vàng vào ví + phát <see cref="RewardGranted"/>.</summary>
    public void WatchRewardedForGold()
        => ShowRewarded(earned =>
        {
            if (!earned) return;
            int gold = AdConfig.RewardGoldPerAd;
            _ = Wallet.Instance?.RewardGoldAsync(gold);   // qua server (có trần) + đồng bộ
            RewardGranted?.Invoke(gold);
        });

    /// <summary>Xem quảng cáo-thưởng → mở RƯƠNG kế tiếp (server tính thưởng theo rương, cấp Vàng/Ma Thạch/vật phẩm).</summary>
    public void WatchRewardedForChest()
    {
        ResetDailyIfNeeded();
        if (_provider == null) return;
        if (!CanWatchRewarded) { GD.Print("[Ads] Hết lượt rương hôm nay."); return; }
        _provider.ShowRewarded(earned => { if (earned) _ = ClaimChestAsync(); });
    }

    private async Task ClaimChestAsync()
    {
        var api = FragmentOfJapanese.Autoloads.ApiClient.Instance;
        if (string.IsNullOrEmpty(api.AccessToken)) return;

        var res = await api.PostAsync("/api/player/ad-reward", new { });
        var data = await api.ReadAsAsync<FragmentOfJapanese.Autoloads.AccountManager.ApiResponse<AdRewardDto>>(res);
        if (!res.IsSuccessStatusCode || data?.Data == null)
        {
            GD.PushWarning($"[Ads] Mở rương thất bại: {data?.Message}");
            return;
        }

        RewardsToday    = data.Data.Used;
        _lastRewardDate = Today;
        Save();

        _ = Wallet.Instance?.SyncAsync();       // Vàng / Ma Thạch
        _ = Inventory.Instance?.SyncAsync();    // vật phẩm
        AdChestClaimed?.Invoke(data.Data.Summary ?? "");
    }

    /// <summary>Nạp số rương đã mở hôm nay từ server (để UI hiện đúng x/5).</summary>
    public async Task SyncAdStatusAsync()
    {
        var api = FragmentOfJapanese.Autoloads.ApiClient.Instance;
        if (string.IsNullOrEmpty(api.AccessToken)) return;
        var res = await api.GetAsync("/api/player/ad-reward");
        if (!res.IsSuccessStatusCode) return;
        var data = await api.ReadAsAsync<FragmentOfJapanese.Autoloads.AccountManager.ApiResponse<AdStatusDto>>(res);
        if (data?.Data == null) return;
        RewardsToday    = data.Data.Used;
        _lastRewardDate = Today;
        Save();
    }

    private class AdRewardDto { public int Used { get; set; } public int Cap { get; set; } public string Summary { get; set; } }
    private class AdStatusDto { public int Used { get; set; } public int Cap { get; set; } }

    // ───────────────────────── Gỡ quảng cáo ─────────────────────────
    /// <summary>Bật/tắt "đã gỡ QC" (tắt banner + interstitial; rewarded vẫn xem được).</summary>
    public void SetAdsRemoved(bool removed)
    {
        if (AdsRemoved == removed) return;
        AdsRemoved = removed;
        if (removed) HideBanner();
        Save();
        AdsRemovedChanged?.Invoke();

        // Lưu entitlement lên server (bền khi cài lại / đổi máy).
        if (removed && !string.IsNullOrEmpty(FragmentOfJapanese.Autoloads.ApiClient.Instance.AccessToken))
            _ = FragmentOfJapanese.Autoloads.ApiClient.Instance.PostAsync("/api/player/ads-removed", new { });
    }

    /// <summary>Nạp trạng thái "đã gỡ QC" từ server (server là nguồn chân lý cho entitlement này).</summary>
    public async System.Threading.Tasks.Task SyncAsync()
    {
        if (string.IsNullOrEmpty(FragmentOfJapanese.Autoloads.ApiClient.Instance.AccessToken)) return;

        await SyncAdStatusAsync();   // số rương đã mở hôm nay (server là nguồn chân lý)

        var res = await FragmentOfJapanese.Autoloads.ApiClient.Instance.GetAsync("/api/player/profile");
        if (!res.IsSuccessStatusCode) return;

        var data = await FragmentOfJapanese.Autoloads.ApiClient.Instance
            .ReadAsAsync<FragmentOfJapanese.Autoloads.AccountManager.ApiResponse<AdsProfileDto>>(res);
        if (data?.Data != null && data.Data.AdsRemoved && !AdsRemoved)
        {
            AdsRemoved = true;
            HideBanner();
            Save();
            AdsRemovedChanged?.Invoke();
        }
    }

    private class AdsProfileDto { public bool AdsRemoved { get; set; } }

    // ───────────────────────── Lưu / nạp ─────────────────────────
    private void ResetDailyIfNeeded()
    {
        if (_lastRewardDate == Today) return;
        _lastRewardDate = Today;
        RewardsToday    = 0;
        Save();
    }

    private void Save()
    {
        var blob = new SaveBlob
        {
            AdsRemoved     = AdsRemoved,
            RewardsToday   = RewardsToday,
            LastRewardDate = _lastRewardDate,
        };
        using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        f?.StoreString(JsonSerializer.Serialize(blob, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void Load()
    {
        if (!FileAccess.FileExists(SavePath)) return;
        using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        var blob = JsonSerializer.Deserialize<SaveBlob>(f.GetAsText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (blob == null) return;
        AdsRemoved      = blob.AdsRemoved;
        RewardsToday    = blob.RewardsToday;
        _lastRewardDate = blob.LastRewardDate ?? "";
    }

    private class SaveBlob
    {
        [JsonPropertyName("ads_removed")]      public bool   AdsRemoved     { get; set; }
        [JsonPropertyName("rewards_today")]    public int    RewardsToday   { get; set; }
        [JsonPropertyName("last_reward_date")] public string LastRewardDate { get; set; } = "";
    }
}
