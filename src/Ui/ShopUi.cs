using Godot;
using System.Collections.Generic;
using FragmentOfJapanese.Ads;
using FragmentOfJapanese.Autoloads;
using FragmentOfJapanese.Core;
using FragmentOfJapanese.Items;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// UI cửa hàng — autoload dựng bằng code, tông nâu gỗ. Mở/đóng bằng phím P (Esc để đóng).
/// Sidebar trái (SHOP + tab dọc) | thanh trên (Vàng + Aetherstone + X) | nội dung đổi theo tab.
///   - Nổi bật : poster + hàng item (mua được)
///   - Vật phẩm: lưới 6 cột (mua được, có hộp xác nhận)
///   - Gacha   : banner + Luck/pity + quay x1/x10 (tốn Chìa Khóa Bạc) → màn lật kết quả có hiệu ứng
///   - Nạp     : xem quảng cáo + gói game + gói gold/pay (sắp ra mắt)
/// </summary>
public partial class ShopUi : CanvasLayer
{
    public static ShopUi Instance { get; private set; }

    private enum Tab { Featured, Items, Gacha, Topup, Redeem }

    private const string KeyIconPath = "res://assets/sprites/items/silver_key.png";

    private Control            _root;
    private Label              _goldLabel;
    private Label              _aetherLabel;
    private MarginContainer    _content;
    private Label              _feedback;
    private ConfirmationDialog _confirm;

    private string _pendingItemId = "";

    // Gacha
    private Label         _luckLabel;
    private Control       _reveal;
    private ColorRect     _flash;
    private Label         _revealTitle;
    private GridContainer _revealGrid;

    public override void _Ready()
    {
        Instance = this;
        Layer    = 11;            // trên cả túi đồ (10)

        EnsureInputAction();
        BuildUi();
        _root.Visible = false;

        if (Wallet.Instance != null) Wallet.Instance.Changed += RefreshWallet;
        if (Gacha.Instance  != null) Gacha.Instance.Changed  += RefreshLuck;
        if (AdManager.Instance != null) AdManager.Instance.RewardGranted += OnAdReward;

        RefreshWallet();
        SetTab(Tab.Featured);
    }

    public override void _UnhandledInput(InputEvent ev)
    {
        if (_reveal != null && _reveal.Visible) return;   // đang xem kết quả gacha

        if (ev.IsActionPressed("toggle_shop"))
        {
            Toggle();
            GetViewport().SetInputAsHandled();
        }
        else if (_root.Visible && ev.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    public void Toggle() { if (_root.Visible) Close(); else Open(); }
    public void Open()   { _root.Visible = true; RefreshWallet(); }
    public void Close()  { _root.Visible = false; }

    // ───────────────────────── Build khung ─────────────────────────

    private void BuildUi()
    {
        _root = new Control { Name = "ShopRoot" };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.MouseFilter = Control.MouseFilterEnum.Stop;
        AddChild(_root);

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.72f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        dim.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(940, 600) };
        panel.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.WoodPanel, 14, UiKit.WoodBorder, 3, 10, 10));
        center.AddChild(panel);

        var rootH = new HBoxContainer();
        rootH.AddThemeConstantOverride("separation", 10);
        panel.AddChild(rootH);

        rootH.AddChild(BuildSidebar());

        var right = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 8);
        rootH.AddChild(right);

        right.AddChild(BuildTopBar());

        _content = new MarginContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _content.AddThemeConstantOverride("margin_top", 2);
        right.AddChild(_content);

        _feedback = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _feedback.AddThemeFontSizeOverride("font_size", 15);
        right.AddChild(_feedback);

        _confirm = new ConfirmationDialog { Title = "Xác nhận", OkButtonText = "Mua", CancelButtonText = "Hủy" };
        _confirm.Confirmed += OnBuyConfirmed;
        _root.AddChild(_confirm);

        BuildReveal();
    }

    private Control BuildSidebar()
    {
        var side = new PanelContainer { CustomMinimumSize = new Vector2(146, 0) };
        side.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        side.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.WoodDark, 10, UiKit.WoodBorder, 2, 8, 10));

        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 8);
        side.AddChild(v);

        var shop = new Label { Text = "SHOP", HorizontalAlignment = HorizontalAlignment.Center };
        shop.AddThemeFontSizeOverride("font_size", 24);
        shop.AddThemeColorOverride("font_color", UiKit.Accent);
        v.AddChild(shop);

        v.AddChild(new HSeparator());

        var group = new ButtonGroup();
        AddSideTab(v, group, "Nổi bật",  Tab.Featured, first: true);
        AddSideTab(v, group, "Vật phẩm", Tab.Items);
        AddSideTab(v, group, "Gacha",    Tab.Gacha);
        AddSideTab(v, group, "Nạp",      Tab.Topup);
        AddSideTab(v, group, "Nhập Code", Tab.Redeem);

        v.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        return side;
    }

    private void AddSideTab(VBoxContainer bar, ButtonGroup group, string text, Tab tab, bool first = false)
    {
        var btn = new Button
        {
            Text                = text,
            ToggleMode          = true,
            ButtonGroup         = group,
            ButtonPressed       = first,
            CustomMinimumSize   = new Vector2(0, 42),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        UiKit.StyleButton(btn, UiKit.WoodDark, new Color(0.40f, 0.28f, 0.17f), UiKit.Accent);
        btn.AddThemeColorOverride("font_color",         UiKit.WoodText);
        btn.AddThemeColorOverride("font_hover_color",   UiKit.WoodText);
        btn.AddThemeColorOverride("font_pressed_color", new Color(0.22f, 0.14f, 0.07f));
        btn.AddThemeFontSizeOverride("font_size", 15);
        btn.Pressed += () => SetTab(tab);
        bar.AddChild(btn);
    }

    private Control BuildTopBar()
    {
        var bar = new HBoxContainer();
        bar.AddThemeConstantOverride("separation", 10);

        bar.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        bar.AddChild(MakeCurrencyPill(false));
        bar.AddChild(MakeCurrencyPill(true));
        bar.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        var x = new Button { Text = "X", CustomMinimumSize = new Vector2(42, 42) };
        UiKit.StyleButton(x, UiKit.WoodDark, new Color(0.55f, 0.25f, 0.22f), new Color(0.40f, 0.18f, 0.16f));
        x.AddThemeFontSizeOverride("font_size", 18);
        x.Pressed += Close;
        bar.AddChild(x);

        return bar;
    }

    private Control MakeCurrencyPill(bool isMa)
    {
        Color  col      = isMa ? UiKit.MaThach : UiKit.Gold;
        string iconPath = isMa ? UiKit.AetherIconPath : UiKit.GoldIconPath;

        var pill = new PanelContainer();
        pill.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.WoodDark, 14, col, 1, 12, 5));

        var h = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        h.AddThemeConstantOverride("separation", 6);
        AddIcon(h, iconPath, 24);

        var l = new Label { VerticalAlignment = VerticalAlignment.Center };
        l.AddThemeFontSizeOverride("font_size", 16);
        l.AddThemeColorOverride("font_color", UiKit.WoodText);
        h.AddChild(l);
        pill.AddChild(h);

        if (isMa) _aetherLabel = l; else _goldLabel = l;
        return pill;
    }

    // ───────────────────────── Tabs ─────────────────────────

    private void SetTab(Tab tab)
    {
        if (_content == null) return;
        foreach (Node c in _content.GetChildren()) c.QueueFree();
        _feedback.Text = "";

        Control view = tab switch
        {
            Tab.Featured => BuildFeatured(),
            Tab.Items    => BuildItemsGrid(),
            Tab.Gacha    => BuildGacha(),
            Tab.Topup    => BuildTopup(),
            Tab.Redeem   => BuildRedeem(),
            _            => new Control(),
        };
        _content.AddChild(view);
    }

    private void RefreshWallet()
    {
        var w = Wallet.Instance;
        if (_goldLabel   != null) _goldLabel.Text   = $"{(w?.Gold ?? 0):N0}";
        if (_aetherLabel != null) _aetherLabel.Text = $"{(w?.MaThach ?? 0):N0}";
    }

    // ---- Tab Nổi bật ----
    private Control BuildFeatured()
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 12);

        var poster = new PanelContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        poster.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.WoodCard, 12, UiKit.Accent, 2));
        var pc = new CenterContainer();
        var pl = new Label { Text = "BANNER / KHUYẾN MÃI" };
        pl.AddThemeFontSizeOverride("font_size", 26);
        pl.AddThemeColorOverride("font_color", UiKit.WoodTextDim);
        pc.AddChild(pl);
        poster.AddChild(pc);
        v.AddChild(poster);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var listings = Shop.Instance?.Listings;
        if (listings != null)
        {
            int n = 0;
            foreach (var l in listings)
            {
                if (n++ >= 6) break;
                var card = MakeBuyCard(l);
                card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                row.AddChild(card);
            }
        }
        v.AddChild(row);
        return v;
    }

    // ---- Tab Vật phẩm ----
    private Control BuildItemsGrid()
    {
        var scroll = new ScrollContainer();
        scroll.SizeFlagsHorizontal   = Control.SizeFlags.ExpandFill;
        scroll.SizeFlagsVertical     = Control.SizeFlags.ExpandFill;
        scroll.HorizontalScrollMode  = ScrollContainer.ScrollMode.Disabled;

        var grid = new GridContainer { Columns = 6 };
        grid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 8);

        var listings = Shop.Instance?.Listings;
        if (listings != null)
            foreach (var l in listings)
            {
                var card = MakeBuyCard(l);
                card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                grid.AddChild(card);
            }

        scroll.AddChild(grid);
        return scroll;
    }

    private Button MakeBuyCard(ShopListing listing)
    {
        var    def    = ItemDatabase.Instance?.Get(listing.ItemId);
        Color  accent = UiKit.TypeColor(def?.Type ?? ItemType.Consumable);
        string id     = listing.ItemId;
        bool   isMa   = listing.CurrencyType == ShopCurrency.MaThach;
        Color  curCol = isMa ? UiKit.MaThach : UiKit.Gold;

        var card = new Button { CustomMinimumSize = new Vector2(108, 150), TooltipText = def?.NameVi ?? id };
        card.AddThemeStyleboxOverride("normal",  UiKit.Box(UiKit.WoodCard, 10, UiKit.WoodBorder, 2, 6, 6));
        card.AddThemeStyleboxOverride("hover",   UiKit.Box(UiKit.Fade(UiKit.Accent, 0.16f), 10, UiKit.Accent, 2, 6, 6));
        card.AddThemeStyleboxOverride("pressed", UiKit.Box(UiKit.Fade(UiKit.Accent, 0.22f), 10, UiKit.Accent, 2, 6, 6));
        card.AddThemeStyleboxOverride("focus",   UiKit.Box(new Color(0, 0, 0, 0), 10));

        var v = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        v.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        v.AddThemeConstantOverride("separation", 4);

        v.AddChild(UiKit.ItemIcon(def, accent, 52));

        var name = new Label { Text = def?.NameVi ?? id, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        name.AddThemeFontSizeOverride("font_size", 12);
        name.AddThemeColorOverride("font_color", UiKit.WoodText);
        v.AddChild(name);

        var price = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        price.AddThemeConstantOverride("separation", 4);
        AddIcon(price, isMa ? UiKit.AetherIconPath : UiKit.GoldIconPath, 16);
        var pl = new Label { Text = $"{listing.Price:N0}" };
        pl.AddThemeFontSizeOverride("font_size", 13);
        pl.AddThemeColorOverride("font_color", curCol);
        price.AddChild(pl);
        v.AddChild(price);

        card.AddChild(v);
        IgnoreMouse(v);

        card.Pressed += () => RequestBuy(id);
        return card;
    }

    // ---- Tab Gacha ----
    private Control BuildGacha()
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 12);

        var banner = new PanelContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        banner.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.WoodCard, 12, UiKit.Accent, 2, 12, 12));

        var bv = new VBoxContainer();
        var topRow = new HBoxContainer();
        topRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var rate = new Button { Text = "Tỉ lệ %", CustomMinimumSize = new Vector2(86, 34) };
        UiKit.StyleButton(rate, UiKit.WoodDark, new Color(0.40f, 0.28f, 0.17f), UiKit.Accent);
        rate.Pressed += () => ShowFeedback("Thường 74% · Hiếm 20% · Sử thi 5% · Huyền thoại 1%", true);
        topRow.AddChild(rate);
        bv.AddChild(topRow);

        var bc = new CenterContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        var bl = new Label { Text = "BANNER GACHA", HorizontalAlignment = HorizontalAlignment.Center };
        bl.AddThemeFontSizeOverride("font_size", 24);
        bl.AddThemeColorOverride("font_color", UiKit.WoodTextDim);
        bc.AddChild(bl);
        bv.AddChild(bc);
        banner.AddChild(bv);
        v.AddChild(banner);

        var bottom = new HBoxContainer();
        bottom.AddThemeConstantOverride("separation", 10);
        _luckLabel = new Label { VerticalAlignment = VerticalAlignment.Center };
        _luckLabel.AddThemeColorOverride("font_color", UiKit.WoodText);
        _luckLabel.AddThemeFontSizeOverride("font_size", 16);
        _luckLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        bottom.AddChild(_luckLabel);
        bottom.AddChild(MakePullButton("x1", 1));
        bottom.AddChild(MakePullButton("x10", 10));
        v.AddChild(bottom);

        RefreshLuck();
        return v;
    }

    private Button MakePullButton(string label, int count)
    {
        var b = new Button { CustomMinimumSize = new Vector2(132, 56), TooltipText = $"Tốn {count} Chìa Khóa Bạc" };
        UiKit.StyleButton(b, UiKit.WoodCard, UiKit.Fade(UiKit.Accent, 0.2f), UiKit.Accent);

        var h = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        h.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        h.AddThemeConstantOverride("separation", 6);
        AddIcon(h, KeyIconPath, 26);
        var l = new Label { Text = label };
        l.AddThemeFontSizeOverride("font_size", 17);
        l.AddThemeColorOverride("font_color", UiKit.WoodText);
        h.AddChild(l);
        b.AddChild(h);
        IgnoreMouse(h);

        b.Pressed += () => OnPull(count);
        return b;
    }

    // ---- Tab Nạp (sắp ra mắt) ----
    private Control BuildTopup()
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 12);

        v.AddChild(SectionLabel("Xem quảng cáo nhận thưởng"));

        var ad       = AdManager.Instance;
        bool canAd   = ad?.CanWatchRewarded ?? false;
        int  used    = ad?.RewardsToday     ?? 0;
        int  cap     = ad?.DailyRewardCap   ?? AdConfig.DailyRewardCap;
        bool removed = ad?.AdsRemoved       ?? false;

        var adRow = new HBoxContainer();
        adRow.AddThemeConstantOverride("separation", 8);

        var watch = new Button
        {
            Text = canAd
                ? $"▶  Xem QC  +{AdConfig.RewardGoldPerAd} Vàng    ({used}/{cap})"
                : $"Hết lượt hôm nay    ({used}/{cap})",
            CustomMinimumSize   = new Vector2(0, 60),
            Disabled            = !canAd,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        UiKit.StyleButton(watch, UiKit.BuyGreen, UiKit.BuyGreenHi, UiKit.BuyGreen);
        watch.Pressed += OnWatchRewarded;
        adRow.AddChild(watch);

        var noAds = new Button
        {
            Text              = removed ? "Đã gỡ QC ✓" : "Gỡ QC",
            CustomMinimumSize = new Vector2(150, 60),
            Disabled          = removed,
        };
        UiKit.StyleButton(noAds, UiKit.WoodCard, UiKit.Fade(UiKit.Accent, 0.18f), UiKit.Accent);
        noAds.AddThemeColorOverride("font_color", UiKit.WoodText);
        noAds.Pressed += OnRemoveAds;
        adRow.AddChild(noAds);

        v.AddChild(adRow);

        v.AddChild(SectionLabel("Gói game"));
        var gpRow = new HBoxContainer();
        gpRow.AddThemeConstantOverride("separation", 8);
        for (int i = 0; i < 3; i++)
        {
            var b = MakePackButton("Gói game", new Vector2(0, 70));
            b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            gpRow.AddChild(b);
        }
        v.AddChild(gpRow);

        v.AddChild(SectionLabel("Mua Vàng / Nạp Aetherstone"));
        var payRow = new HBoxContainer();
        payRow.AddThemeConstantOverride("separation", 8);
        for (int i = 0; i < 3; i++)
        {
            var b = MakePackButton("Gold", new Vector2(0, 68));
            b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            payRow.AddChild(b);
        }
        for (int i = 0; i < 3; i++)
        {
            var b = MakePackButton("Nạp", new Vector2(0, 68));
            b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            payRow.AddChild(b);
        }
        v.AddChild(payRow);

        return v;
    }

    // ---- Tab Nhập Code ----
    private Control BuildRedeem()
    {
        var center = new CenterContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };

        var card = new PanelContainer { CustomMinimumSize = new Vector2(440, 0) };
        card.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.WoodCard, 12, UiKit.Accent, 2, 24, 22));

        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 12);

        var title = new Label { Text = "NHẬP CODE", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", UiKit.Accent);
        v.AddChild(title);

        var desc = new Label
        {
            Text                = "Nhập mã quà tặng để nhận Vàng, Aetherstone hoặc vật phẩm.",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode        = TextServer.AutowrapMode.WordSmart,
        };
        desc.AddThemeColorOverride("font_color", UiKit.WoodTextDim);
        v.AddChild(desc);

        var input = new LineEdit
        {
            PlaceholderText   = "Ví dụ: WELCOME",
            CustomMinimumSize = new Vector2(0, 46),
            Alignment         = HorizontalAlignment.Center,
        };
        input.AddThemeStyleboxOverride("normal", UiKit.Box(UiKit.WoodDark, 8, UiKit.WoodBorder, 2, 10, 6));
        input.AddThemeStyleboxOverride("focus",  UiKit.Box(UiKit.WoodDark, 8, UiKit.Accent, 2, 10, 6));
        input.AddThemeColorOverride("font_color", UiKit.WoodText);
        input.AddThemeColorOverride("font_placeholder_color", UiKit.WoodTextDim);
        input.AddThemeFontSizeOverride("font_size", 18);
        v.AddChild(input);

        var redeem = new Button { Text = "Đổi quà", CustomMinimumSize = new Vector2(0, 46) };
        UiKit.StyleButton(redeem, UiKit.BuyGreen, UiKit.BuyGreenHi, new Color(0.16f, 0.42f, 0.24f));
        redeem.AddThemeFontSizeOverride("font_size", 18);
        v.AddChild(redeem);

        var result = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode        = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize   = new Vector2(0, 44),
        };
        result.AddThemeFontSizeOverride("font_size", 15);
        v.AddChild(result);

        void DoRedeem()
        {
            var mgr = RedeemManager.Instance;
            if (mgr == null) return;
            var status = mgr.Redeem(input.Text, out string reward);
            switch (status)
            {
                case RedeemManager.Result.Success:
                    result.Text = $"Thành công! Nhận: {reward}";
                    result.AddThemeColorOverride("font_color", new Color(0.55f, 0.95f, 0.55f));
                    input.Text = "";
                    RefreshWallet();
                    break;
                case RedeemManager.Result.AlreadyUsed:
                    result.Text = "Mã này đã được sử dụng.";
                    result.AddThemeColorOverride("font_color", new Color(1f, 0.6f, 0.55f));
                    break;
                default:
                    result.Text = "Mã không hợp lệ.";
                    result.AddThemeColorOverride("font_color", new Color(1f, 0.6f, 0.55f));
                    break;
            }
        }

        redeem.Pressed      += DoRedeem;
        input.TextSubmitted += _ => DoRedeem();

        card.AddChild(v);
        center.AddChild(card);
        return center;
    }

    private Label SectionLabel(string text)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", 15);
        l.AddThemeColorOverride("font_color", UiKit.Accent);
        return l;
    }

    private Button MakePackButton(string text, Vector2 size)
    {
        var b = new Button { Text = text, CustomMinimumSize = size };
        UiKit.StyleButton(b, UiKit.WoodCard, UiKit.Fade(UiKit.Accent, 0.18f), UiKit.Accent);
        b.AddThemeColorOverride("font_color", UiKit.WoodText);
        b.Pressed += () => ShowFeedback("Nạp / quảng cáo — sắp ra mắt (cần tích hợp thanh toán)", false);
        return b;
    }

    // ---- Quảng cáo (tab Nạp) ----
    private void OnWatchRewarded()
    {
        var ad = AdManager.Instance;
        if (ad == null)           { ShowFeedback("Hệ thống quảng cáo chưa sẵn sàng", false); return; }
        if (!ad.CanWatchRewarded) { ShowFeedback("Bạn đã hết lượt xem hôm nay", false);     return; }
        ad.WatchRewardedForGold();   // xem xong → cộng Vàng + phát RewardGranted (→ OnAdReward)
    }

    private void OnAdReward(int gold)
    {
        if (_root == null || !_root.Visible) return;
        SetTab(Tab.Topup);                                  // làm mới (used/cap, trạng thái nút)
        ShowFeedback($"+{gold} Vàng từ quảng cáo!", true);
    }

    private void OnRemoveAds()
    {
        var ad = AdManager.Instance;
        if (ad == null) return;
        ad.SetAdsRemoved(true);
        if (_root.Visible) SetTab(Tab.Topup);
        ShowFeedback("Đã gỡ quảng cáo (banner + interstitial). Rewarded vẫn xem được.", true);
    }

    // ───────────────────────── Buy flow ─────────────────────────

    private void RequestBuy(string itemId)
    {
        var listing = Shop.Instance?.GetListing(itemId);
        var def     = ItemDatabase.Instance?.Get(itemId);
        if (listing == null || def == null) return;

        _pendingItemId      = itemId;
        _confirm.DialogText = $"Mua {def.NameVi} với giá {listing.Price:N0} {(listing.CurrencyType == ShopCurrency.MaThach ? "Aetherstone" : "Vàng")}?";
        _confirm.PopupCentered();
    }

    private void OnBuyConfirmed()
    {
        if (string.IsNullOrEmpty(_pendingItemId)) return;

        var    def    = ItemDatabase.Instance?.Get(_pendingItemId);
        string name   = def?.NameVi ?? _pendingItemId;
        var    result = Shop.Instance?.Buy(_pendingItemId) ?? Shop.BuyResult.InvalidItem;

        switch (result)
        {
            case Shop.BuyResult.Success:        ShowFeedback($"Đã mua {name}.", ok: true);  break;
            case Shop.BuyResult.NotEnoughMoney: ShowFeedback("Không đủ tiền.",  ok: false); break;
            default:                            ShowFeedback("Không mua được.", ok: false); break;
        }
        _pendingItemId = "";
    }

    private void ShowFeedback(string msg, bool ok)
    {
        if (_feedback == null) return;
        _feedback.Text = msg;
        _feedback.AddThemeColorOverride("font_color", ok ? new Color(0.55f, 0.95f, 0.55f) : new Color(1f, 0.6f, 0.55f));
    }

    // ───────────────────────── Gacha quay + hiệu ứng ─────────────────────────

    private void RefreshLuck()
    {
        if (_luckLabel != null && GodotObject.IsInstanceValid(_luckLabel))
            _luckLabel.Text = $"Luck: {Gacha.Instance?.Pity ?? 0} / {Gacha.PityMax}";
    }

    private void OnPull(int count)
    {
        var results = Gacha.Instance?.Pull(count);
        if (results == null || results.Count == 0)
        {
            ShowFeedback($"Cần {count} Chìa Khóa Bạc để quay.", false);
            return;
        }
        RefreshLuck();
        RefreshWallet();
        ShowReveal(results);
    }

    private void BuildReveal()
    {
        _reveal = new Control { Visible = false };
        _reveal.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _reveal.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(_reveal);

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.85f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        dim.MouseFilter = Control.MouseFilterEnum.Ignore;
        _reveal.AddChild(dim);

        var box = new CenterContainer();
        box.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        box.MouseFilter = Control.MouseFilterEnum.Ignore;
        _reveal.AddChild(box);

        var v = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        v.AddThemeConstantOverride("separation", 16);
        box.AddChild(v);

        _revealTitle = new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        _revealTitle.AddThemeFontSizeOverride("font_size", 30);
        _revealTitle.AddThemeColorOverride("font_color", UiKit.Accent);
        v.AddChild(_revealTitle);

        _revealGrid = new GridContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _revealGrid.AddThemeConstantOverride("h_separation", 12);
        _revealGrid.AddThemeConstantOverride("v_separation", 12);
        v.AddChild(_revealGrid);

        var hint = new Label { Text = "Chạm để tiếp tục", HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        hint.AddThemeColorOverride("font_color", UiKit.WoodTextDim);
        v.AddChild(hint);

        _flash = new ColorRect { Color = new Color(1, 1, 1, 0) };
        _flash.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _flash.MouseFilter = Control.MouseFilterEnum.Ignore;
        _reveal.AddChild(_flash);

        var backdrop = new Button();
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var clear = UiKit.Box(new Color(0, 0, 0, 0));
        backdrop.AddThemeStyleboxOverride("normal",  clear);
        backdrop.AddThemeStyleboxOverride("hover",   clear);
        backdrop.AddThemeStyleboxOverride("pressed", clear);
        backdrop.AddThemeStyleboxOverride("focus",   clear);
        backdrop.Pressed += HideReveal;
        _reveal.AddChild(backdrop);
    }

    private void ShowReveal(List<ItemEntry> results)
    {
        _reveal.Visible     = true;
        _reveal.MouseFilter = Control.MouseFilterEnum.Stop;

        foreach (Node c in _revealGrid.GetChildren()) c.QueueFree();
        _revealGrid.Columns = results.Count > 1 ? 5 : 1;
        _revealTitle.Text   = results.Count > 1 ? $"Quay {results.Count} lần!" : "Kết quả";

        // Chớp sáng theo độ hiếm cao nhất
        Rarity best = Rarity.Common;
        foreach (var d in results)
            if (d != null && d.Rarity > best) best = d.Rarity;
        _flash.Color = UiKit.Fade(best == Rarity.Common ? Colors.White : UiKit.RarityColor(best), 0.6f);
        CreateTween().TweenProperty(_flash, "color:a", 0f, 0.4);

        // Card hiện lần lượt (fade-in so le)
        int i = 0;
        foreach (var def in results)
        {
            var card = MakeRevealCard(def);
            card.Modulate = new Color(1, 1, 1, 0);
            _revealGrid.AddChild(card);

            var tw = CreateTween();
            tw.TweenInterval(0.12 + i * 0.07);
            tw.TweenProperty(card, "modulate:a", 1f, 0.25);
            i++;
        }
    }

    private void HideReveal()
    {
        _reveal.Visible     = false;
        _reveal.MouseFilter = Control.MouseFilterEnum.Ignore;
    }

    private Control MakeRevealCard(ItemEntry def)
    {
        Rarity r  = def?.Rarity ?? Rarity.Common;
        Color  rc = UiKit.RarityColor(r);

        var card = new PanelContainer { CustomMinimumSize = new Vector2(140, 172) };
        card.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.Fade(rc, 0.18f), 12, rc, 3, 10, 10));

        var v = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        v.AddThemeConstantOverride("separation", 6);

        v.AddChild(UiKit.ItemIcon(def, rc, 72));

        var name = new Label { Text = def?.NameVi ?? "?", HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        name.AddThemeFontSizeOverride("font_size", 13);
        name.AddThemeColorOverride("font_color", UiKit.WoodText);
        v.AddChild(name);

        var rl = new Label { Text = UiKit.RarityName(r), HorizontalAlignment = HorizontalAlignment.Center };
        rl.AddThemeFontSizeOverride("font_size", 12);
        rl.AddThemeColorOverride("font_color", rc);
        v.AddChild(rl);

        card.AddChild(v);
        return card;
    }

    // ───────────────────────── Helpers ─────────────────────────

    private static void AddIcon(BoxContainer parent, string path, int size)
    {
        if (!ResourceLoader.Exists(path)) return;
        var tex = GD.Load<Texture2D>(path);
        if (tex == null) return;
        parent.AddChild(new TextureRect
        {
            Texture           = tex,
            CustomMinimumSize = new Vector2(size, size),
            StretchMode       = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode        = TextureRect.ExpandModeEnum.IgnoreSize,
        });
    }

    private static void IgnoreMouse(Node node)
    {
        if (node is Control c) c.MouseFilter = Control.MouseFilterEnum.Ignore;
        foreach (var child in node.GetChildren())
            IgnoreMouse(child);
    }

    private static void EnsureInputAction()
    {
        if (!InputMap.HasAction("toggle_shop"))
            InputMap.AddAction("toggle_shop");

        foreach (var e in InputMap.ActionGetEvents("toggle_shop"))
            if (e is InputEventKey k && k.PhysicalKeycode == Key.P)
                return;

        InputMap.ActionAddEvent("toggle_shop", new InputEventKey { PhysicalKeycode = Key.P });
    }
}
