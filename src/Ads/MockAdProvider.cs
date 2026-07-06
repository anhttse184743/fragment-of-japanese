using System;
using System.Threading.Tasks;
using Godot;
using FragmentOfJapanese.Ui;

namespace FragmentOfJapanese.Ads;

/// <summary>
/// Giả lập quảng cáo cho EDITOR / DESKTOP (khi chưa có plugin AdMob, hoặc chạy PC).
/// Hiện overlay phủ màn để thấy & test trọn luồng: banner, interstitial, rewarded —
/// kể cả phần "xem hết mới nhận thưởng" và "bỏ qua thì mất thưởng".
/// Trên Android (có plugin + cờ ADMOB_ENABLED) sẽ dùng <c>AdMobProvider</c> thay cho lớp này.
/// </summary>
public sealed class MockAdProvider : IAdProvider
{
    private readonly AdManager _host;
    private Control _banner;

    public MockAdProvider(AdManager host) => _host = host;

    public bool IsRealAds => false;

    public bool IsRewardedReady => true;   // giả lập luôn sẵn sàng

    public void PreloadRewarded() { }      // giả lập không cần nạp

    public void Initialize()
        => GD.Print("[Ads] Dùng GIẢ LẬP quảng cáo (chưa có plugin AdMob / đang chạy desktop).");

    // ───────────────────────── Banner ─────────────────────────
    public void ShowBanner()
    {
        if (_banner != null && GodotObject.IsInstanceValid(_banner)) { _banner.Visible = true; return; }
        _banner = BuildBanner();
        _host.AddChild(_banner);
    }

    public void HideBanner()
    {
        if (_banner != null && GodotObject.IsInstanceValid(_banner)) _banner.Visible = false;
    }

    // ───────────────────────── Interstitial ─────────────────────────
    public void ShowInterstitial(Action onClosed) => _ = RunInterstitial(onClosed);

    private async Task RunInterstitial(Action onClosed)
    {
        var (overlay, info, buttons) = BuildFullscreen("QUẢNG CÁO", "Interstitial (thử nghiệm)");
        _host.AddChild(overlay);

        for (int t = 3; t > 0; t--) { info.Text = $"Quảng cáo demo — đóng sau {t}s…"; await Wait(1); }
        info.Text = "Cảm ơn bạn đã xem!";

        await ClickButton(buttons, "Đóng", UiKit.BuyGreen);
        overlay.QueueFree();
        onClosed?.Invoke();
    }

    // ───────────────────────── Rewarded ─────────────────────────
    public void ShowRewarded(Action<bool> onEarned) => _ = RunRewarded(onEarned);

    private async Task RunRewarded(Action<bool> onEarned)
    {
        var (overlay, info, buttons) = BuildFullscreen("QUẢNG CÁO THƯỞNG", "Rewarded (thử nghiệm)");

        bool skipped = false;
        var skip = MakeButton("Bỏ qua (mất thưởng)", new Color(0.40f, 0.25f, 0.27f));
        skip.Pressed += () => skipped = true;
        buttons.AddChild(skip);
        _host.AddChild(overlay);

        for (int t = 4; t > 0 && !skipped; t--)
        {
            info.Text = $"Xem hết để nhận {AdConfig.RewardGoldPerAd} Vàng… {t}s";
            await Wait(1);
        }

        if (skipped)
        {
            overlay.QueueFree();
            onEarned?.Invoke(false);
            return;
        }

        skip.QueueFree();
        info.Text = $"Hoàn tất! Bạn nhận được +{AdConfig.RewardGoldPerAd} Vàng";
        await ClickButton(buttons, "Nhận thưởng", UiKit.BuyGreen);
        overlay.QueueFree();
        onEarned?.Invoke(true);
    }

    // ───────────────────────── Helpers ─────────────────────────
    private SignalAwaiter Wait(float seconds)
        => _host.ToSignal(_host.GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    /// <summary>Thêm nút vào hàng nút, chờ tới khi người chơi bấm.</summary>
    private static async Task ClickButton(HBoxContainer row, string text, Color col)
    {
        var tcs = new TaskCompletionSource();
        var b = MakeButton(text, col);
        b.Pressed += () => tcs.TrySetResult();
        row.AddChild(b);
        await tcs.Task;
    }

    private static Button MakeButton(string text, Color col)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(170, 48) };
        UiKit.StyleButton(b, col, col.Lightened(0.12f), col.Darkened(0.08f));
        return b;
    }

    /// <summary>Overlay phủ màn: nền tối + thẻ quảng cáo giả + label trạng thái + hàng nút.</summary>
    private static (Control overlay, Label info, HBoxContainer buttons) BuildFullscreen(string title, string tag)
    {
        var root = new Control();
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.MouseFilter = Control.MouseFilterEnum.Stop;   // chặn click rơi xuống game

        var dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.88f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

        var card = new PanelContainer { CustomMinimumSize = new Vector2(560, 0) };
        card.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.PanelBg, 16, UiKit.Accent, 2, 28, 26));
        center.AddChild(card);

        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 18);
        card.AddChild(v);

        var tagLabel = new Label { Text = tag, HorizontalAlignment = HorizontalAlignment.Center };
        tagLabel.AddThemeFontSizeOverride("font_size", 14);
        tagLabel.AddThemeColorOverride("font_color", UiKit.TextDim);
        v.AddChild(tagLabel);

        var titleLabel = new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Center };
        titleLabel.AddThemeFontSizeOverride("font_size", 30);
        titleLabel.AddThemeColorOverride("font_color", UiKit.Accent);
        v.AddChild(titleLabel);

        // "creative" giả — khung màu + chữ Ad, có nhịp đập nhẹ cho sinh động
        var creative = new PanelContainer { CustomMinimumSize = new Vector2(420, 190) };
        creative.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.20f, 0.34f, 0.52f), 12));
        var adWord = new Label
        {
            Text = "Ad",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
        };
        adWord.AddThemeFontSizeOverride("font_size", 64);
        adWord.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.92f));
        creative.AddChild(adWord);
        v.AddChild(creative);
        var pulse = creative.CreateTween().SetLoops();
        pulse.TweenProperty(creative, "modulate:a", 0.65f, 0.7).SetTrans(Tween.TransitionType.Sine);
        pulse.TweenProperty(creative, "modulate:a", 1.0f, 0.7).SetTrans(Tween.TransitionType.Sine);

        var info = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        info.AddThemeFontSizeOverride("font_size", 18);
        info.AddThemeColorOverride("font_color", Colors.White);
        v.AddChild(info);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 14);
        v.AddChild(buttons);

        return (root, info, buttons);
    }

    private static Control BuildBanner()
    {
        var holder = new Control { Name = "MockBanner" };
        holder.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        holder.OffsetTop = 0;
        holder.OffsetBottom = 60;
        holder.MouseFilter = Control.MouseFilterEnum.Ignore;

        var panel = new PanelContainer();
        panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        panel.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.12f, 0.13f, 0.18f, 0.96f), 0, UiKit.Accent, 1));
        holder.AddChild(panel);

        var label = new Label
        {
            Text = "▮ Quảng cáo thử nghiệm — Banner 320×50",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
        };
        label.AddThemeFontSizeOverride("font_size", 16);
        label.AddThemeColorOverride("font_color", UiKit.TextDim);
        panel.AddChild(label);

        return holder;
    }
}
