using Godot;
using System.Collections.Generic;
using FragmentOfJapanese.Ads;
using FragmentOfJapanese.Autoloads;
using FragmentOfJapanese.Cosmetics;
using FragmentOfJapanese.Core;
using FragmentOfJapanese.Items;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// UI cửa hàng — Scene-based (ShopUi.tscn). Mở/đóng bằng phím P (Esc để đóng).
/// Tất cả node được wire từ scene qua [Export]. Script chỉ chứa logic thuần.
/// </summary>
public partial class ShopUi : CanvasLayer
{
    public static ShopUi Instance { get; private set; }

    private enum Tab { Featured, Items, Gacha, Topup, Redeem }

    private const string KeyIconPath = "res://assets/sprites/items/silver_key.png";

    // ── Nodes wired từ scene ──────────────────────────────────────────────────
    [Export] private Control            _shopRoot;
    [Export] private Label              _goldLabel;
    [Export] private Label              _aetherLabel;
    [Export] private MarginContainer    _contentArea;
    [Export] private Label              _feedbackLabel;
    [Export] private ConfirmationDialog _confirmDialog;

    // Sidebar tab buttons
    [Export] private Button _tabFeatured;
    [Export] private Button _tabItems;
    [Export] private Button _tabGacha;
    [Export] private Button _tabTopup;
    [Export] private Button _tabRedeem;

    // Close button
    [Export] private Button _closeButton;

    // Gacha reveal nodes
    [Export] private Control       _revealLayer;
    [Export] private Label         _revealTitle;
    [Export] private GridContainer _revealGrid;
    [Export] private ColorRect     _revealFlash;
    [Export] private Button        _revealBackdrop;

    // Gacha luck label (built dynamically inside gacha tab)
    private Label _luckLabel;
    private string _pendingItemId = "";

    // Panel chính — co giãn theo màn hình
    private PanelContainer _panel;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    public override void _Ready()
    {
        Instance = this;

        EnsureInputAction();
        ApplyStyles();
        WireSignals();

        // Layout co giãn theo màn hình (mobile/desktop): panel chiếm phần lớn viewport, có trần.
        _panel = _shopRoot.GetNode<PanelContainer>("Center/Panel");
        ResizePanel();
        GetViewport().SizeChanged += ResizePanel;

        _shopRoot.Visible = false;

        if (Wallet.Instance    != null) Wallet.Instance.Changed          += RefreshWallet;
        if (Gacha.Instance     != null) Gacha.Instance.Changed           += RefreshLuck;
        if (AdManager.Instance != null) AdManager.Instance.RewardGranted += OnAdReward;

        RefreshWallet();
        SetTab(Tab.Featured);
    }

    public override void _UnhandledInput(InputEvent ev)
    {
        if (_revealLayer != null && _revealLayer.Visible) return;

        if (ev.IsActionPressed("toggle_shop"))
        {
            Toggle();
            GetViewport().SetInputAsHandled();
        }
        else if (_shopRoot.Visible && ev.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    public void Toggle() { if (_shopRoot.Visible) Close(); else Open(); }
    public async void Open()
    {
        _shopRoot.Visible = true;
        ResizePanel();
        RefreshWallet();
        if (Wallet.Instance != null) { await Wallet.Instance.SyncAsync(); RefreshWallet(); }   // cập nhật sau thanh toán
    }
    public void Close()  { _shopRoot.Visible = false; }

    /// <summary>Panel co theo viewport: ~94% bề ngang, ~92% chiều cao, có trần để không quá to trên desktop.</summary>
    private void ResizePanel()
    {
        if (_panel == null) return;
        var vp = GetViewport().GetVisibleRect().Size;
        float w = Mathf.Min(vp.X * 0.94f, 1480f);
        float h = Mathf.Min(vp.Y * 0.92f, 920f);
        _panel.CustomMinimumSize = new Vector2(w, h);
    }

    // ── Wire signals từ scene ─────────────────────────────────────────────────
    private void WireSignals()
    {
        _closeButton.Pressed    += Close;
        _confirmDialog.Confirmed += OnBuyConfirmed;
        _revealBackdrop.Pressed  += HideReveal;

        var group = new ButtonGroup();
        WireTabButton(_tabFeatured, group, Tab.Featured, first: true);
        WireTabButton(_tabItems,    group, Tab.Items);
        WireTabButton(_tabGacha,    group, Tab.Gacha);
        WireTabButton(_tabTopup,    group, Tab.Topup);
        WireTabButton(_tabRedeem,   group, Tab.Redeem);
    }

    private void WireTabButton(Button btn, ButtonGroup group, Tab tab, bool first = false)
    {
        btn.ButtonGroup   = group;
        btn.ButtonPressed = first;
        btn.Pressed       += () => SetTab(tab);
    }

    // ── Áp dụng style UiKit lên các node scene ───────────────────────────────
    private void ApplyStyles()
    {
        // Panel chính
        var panel = _shopRoot.GetNode<PanelContainer>("Center/Panel");
        panel?.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.WoodPanel, 14, UiKit.WoodBorder, 3, 10, 10));

        // Sidebar
        var sidebar = _shopRoot.GetNode<PanelContainer>("Center/Panel/RootH/Sidebar");
        sidebar?.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.WoodDark, 10, UiKit.WoodBorder, 2, 8, 10));

        var shopTitle = _shopRoot.GetNodeOrNull<Label>("Center/Panel/RootH/Sidebar/SidebarVBox/ShopTitle");
        if (shopTitle != null) shopTitle.AddThemeColorOverride("font_color", UiKit.Accent);

        StyleTabButton(_tabFeatured);
        StyleTabButton(_tabItems);
        StyleTabButton(_tabGacha);
        StyleTabButton(_tabTopup);
        StyleTabButton(_tabRedeem);

        // Gold pill
        var goldPill = _shopRoot.GetNodeOrNull<PanelContainer>("Center/Panel/RootH/RightColumn/TopBar/GoldPill");
        goldPill?.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.WoodDark, 14, UiKit.Gold, 1, 12, 5));
        if (_goldLabel  != null) _goldLabel.AddThemeColorOverride("font_color", UiKit.WoodText);

        // Aether pill
        var aetherPill = _shopRoot.GetNodeOrNull<PanelContainer>("Center/Panel/RootH/RightColumn/TopBar/AetherPill");
        aetherPill?.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.WoodDark, 14, UiKit.MaThach, 1, 12, 5));
        if (_aetherLabel != null) _aetherLabel.AddThemeColorOverride("font_color", UiKit.WoodText);

        // Load icon texture cho gold/aether pill
        LoadPillIcon("Center/Panel/RootH/RightColumn/TopBar/GoldPill/GoldPillH/GoldIcon",   UiKit.GoldIconPath);
        LoadPillIcon("Center/Panel/RootH/RightColumn/TopBar/AetherPill/AetherPillH/AetherIcon", UiKit.AetherIconPath);

        // Close button
        UiKit.StyleButton(_closeButton, UiKit.WoodDark, new Color(0.55f, 0.25f, 0.22f), new Color(0.40f, 0.18f, 0.16f));
        _closeButton.AddThemeFontSizeOverride("font_size", 18);

        // Reveal backdrop
        var clear = UiKit.Box(new Color(0, 0, 0, 0));
        _revealBackdrop?.AddThemeStyleboxOverride("normal",  clear);
        _revealBackdrop?.AddThemeStyleboxOverride("hover",   clear);
        _revealBackdrop?.AddThemeStyleboxOverride("pressed", clear);
        _revealBackdrop?.AddThemeStyleboxOverride("focus",   clear);

        if (_revealTitle  != null) _revealTitle.AddThemeColorOverride("font_color", UiKit.Accent);
        if (_revealFlash  != null) _revealFlash.Color = new Color(1, 1, 1, 0);
    }

    private void StyleTabButton(Button btn)
    {
        if (btn == null) return;
        UiKit.StyleButton(btn, UiKit.WoodDark, new Color(0.40f, 0.28f, 0.17f), UiKit.Accent);
        btn.AddThemeColorOverride("font_color",         UiKit.WoodText);
        btn.AddThemeColorOverride("font_hover_color",   UiKit.WoodText);
        btn.AddThemeColorOverride("font_pressed_color", new Color(0.22f, 0.14f, 0.07f));
        btn.AddThemeFontSizeOverride("font_size", 15);
    }

    private void LoadPillIcon(string nodePath, string texPath)
    {
        var icon = _shopRoot.GetNodeOrNull<TextureRect>(nodePath);
        if (icon == null || !ResourceLoader.Exists(texPath)) return;
        icon.Texture = GD.Load<Texture2D>(texPath);
    }

    // ── Tabs ──────────────────────────────────────────────────────────────────
    private void SetTab(Tab tab)
    {
        if (_contentArea == null) return;
        foreach (Node c in _contentArea.GetChildren()) c.QueueFree();
        if (_feedbackLabel != null) _feedbackLabel.Text = "";
        _luckLabel = null;

        Control view = tab switch
        {
            Tab.Featured => BuildFeatured(),
            Tab.Items    => BuildItemsGrid(),
            Tab.Gacha    => BuildGacha(),
            Tab.Topup    => BuildTopup(),
            Tab.Redeem   => BuildRedeem(),
            _            => new Control(),
        };
        _contentArea.AddChild(view);
    }

    private void RefreshWallet()
    {
        var w = Wallet.Instance;
        if (_goldLabel   != null) _goldLabel.Text   = $"{(w?.Gold    ?? 0):N0}";
        if (_aetherLabel != null) _aetherLabel.Text = $"{(w?.MaThach ?? 0):N0}";
    }

    // ── Tab Nổi bật ──────────────────────────────────────────────────────────
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

    // ── Tab Vật phẩm ─────────────────────────────────────────────────────────
    private Control BuildItemsGrid()
    {
        var scroll = new ScrollContainer();
        scroll.SizeFlagsHorizontal  = Control.SizeFlags.ExpandFill;
        scroll.SizeFlagsVertical    = Control.SizeFlags.ExpandFill;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;

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
        var   def    = ItemDatabase.Instance?.Get(listing.ItemId);
        Color accent = UiKit.TypeColor(def?.Type ?? ItemType.Consumable);
        string id    = listing.ItemId;
        bool  isMa   = listing.CurrencyType == ShopCurrency.MaThach;
        Color curCol = isMa ? UiKit.MaThach : UiKit.Gold;

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

    // ── Tab Gacha ─────────────────────────────────────────────────────────────
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
        rate.Pressed += () => ShowFeedback("Hiếm 35% · Sử thi 12% · Huyền thoại 3% · còn lại Thường — quay trúng skin trùng được hoàn Vàng", true);
        topRow.AddChild(rate);
        bv.AddChild(topRow);

        var bc = new CenterContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        var bl = new Label { Text = "GACHA HIỆU ỨNG", HorizontalAlignment = HorizontalAlignment.Center };
        bl.AddThemeFontSizeOverride("font_size", 24);
        bl.AddThemeColorOverride("font_color", UiKit.WoodTextDim);
        bc.AddChild(bl);
        bv.AddChild(bc);
        banner.AddChild(bv);
        v.AddChild(banner);

        var bottom = new HBoxContainer();
        bottom.AddThemeConstantOverride("separation", 10);
        _luckLabel = new Label { VerticalAlignment = VerticalAlignment.Center, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _luckLabel.AddThemeColorOverride("font_color", UiKit.WoodText);
        _luckLabel.AddThemeFontSizeOverride("font_size", 16);
        bottom.AddChild(_luckLabel);
        bottom.AddChild(MakePullButton("x1",  1));
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

    // ── Tab Nạp ───────────────────────────────────────────────────────────────
    private Control BuildTopup()
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 12);
        v.AddChild(SectionLabel("Xem quảng cáo nhận thưởng"));

        var ad      = AdManager.Instance;
        bool canAd  = ad?.CanWatchRewarded ?? false;
        int  used   = ad?.RewardsToday     ?? 0;
        int  cap    = ad?.DailyRewardCap   ?? AdConfig.DailyRewardCap;
        bool removed = ad?.AdsRemoved      ?? false;

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

        var noAds = new Button { Text = removed ? "Đã gỡ QC ✓" : "Gỡ QC", CustomMinimumSize = new Vector2(150, 60), Disabled = removed };
        UiKit.StyleButton(noAds, UiKit.WoodCard, UiKit.Fade(UiKit.Accent, 0.18f), UiKit.Accent);
        noAds.AddThemeColorOverride("font_color", UiKit.WoodText);
        noAds.Pressed += OnRemoveAds;
        adRow.AddChild(noAds);
        v.AddChild(adRow);

        v.AddChild(SectionLabel("Nạp Ma Thạch (thanh toán PayOS)"));

        var packsScroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        packsScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        var packsGrid = new GridContainer { Columns = 4 };
        packsGrid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        packsGrid.AddThemeConstantOverride("h_separation", 8);
        packsGrid.AddThemeConstantOverride("v_separation", 8);
        packsScroll.AddChild(packsGrid);
        v.AddChild(packsScroll);

        var loading = new Label { Text = "Đang tải gói nạp..." };
        loading.AddThemeColorOverride("font_color", UiKit.WoodTextDim);
        packsGrid.AddChild(loading);

        _ = FillPacks(packsGrid);
        return v;
    }

    private async System.Threading.Tasks.Task FillPacks(GridContainer grid)
    {
        var packs = Shop.Instance != null ? await Shop.Instance.GetPacksAsync() : new List<Shop.PaymentPack>();
        if (!GodotObject.IsInstanceValid(grid)) return;
        foreach (Node c in grid.GetChildren()) c.QueueFree();

        if (packs.Count == 0)
        {
            var empty = new Label { Text = "Chưa có gói nạp (hoặc chưa đăng nhập)." };
            empty.AddThemeColorOverride("font_color", UiKit.WoodTextDim);
            grid.AddChild(empty);
            return;
        }

        foreach (var p in packs)
            grid.AddChild(MakePackCard(p));
    }

    private Control MakePackCard(Shop.PaymentPack p)
    {
        var card = new Button { CustomMinimumSize = new Vector2(150, 132), TooltipText = p.Description };
        card.AddThemeStyleboxOverride("normal",  UiKit.Box(UiKit.WoodCard, 10, UiKit.MaThach, 2, 8, 8));
        card.AddThemeStyleboxOverride("hover",   UiKit.Box(UiKit.Fade(UiKit.MaThach, 0.18f), 10, UiKit.MaThach, 2, 8, 8));
        card.AddThemeStyleboxOverride("pressed", UiKit.Box(UiKit.Fade(UiKit.MaThach, 0.24f), 10, UiKit.MaThach, 2, 8, 8));

        var v = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        v.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        v.AddThemeConstantOverride("separation", 4);

        var amount = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        amount.AddThemeConstantOverride("separation", 4);
        AddIcon(amount, UiKit.AetherIconPath, 22);
        var al = new Label { Text = $"{p.CurrencyAmount:N0}" };
        al.AddThemeFontSizeOverride("font_size", 18);
        al.AddThemeColorOverride("font_color", UiKit.MaThach);
        amount.AddChild(al);
        v.AddChild(amount);

        if (p.BonusPercent > 0)
        {
            var bonus = new Label { Text = $"+{p.BonusPercent}%", HorizontalAlignment = HorizontalAlignment.Center };
            bonus.AddThemeFontSizeOverride("font_size", 12);
            bonus.AddThemeColorOverride("font_color", UiKit.BuyGreenHi);
            v.AddChild(bonus);
        }

        var name = new Label { Text = p.Name, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        name.AddThemeFontSizeOverride("font_size", 12);
        name.AddThemeColorOverride("font_color", UiKit.WoodTextDim);
        v.AddChild(name);

        var price = new Label { Text = $"{(p.PriceVnd ?? 0):N0}đ", HorizontalAlignment = HorizontalAlignment.Center };
        price.AddThemeFontSizeOverride("font_size", 15);
        price.AddThemeColorOverride("font_color", UiKit.WoodText);
        v.AddChild(price);

        card.AddChild(v);
        IgnoreMouse(v);
        card.Pressed += () => OnBuyPack(p);
        return card;
    }

    private async void OnBuyPack(Shop.PaymentPack p)
    {
        if (Shop.Instance == null) return;
        ShowFeedback("Đang tạo link thanh toán...", true);
        var (url, error) = await Shop.Instance.CreatePayOsLinkAsync(p.Id);
        if (!string.IsNullOrEmpty(url))
        {
            OS.ShellOpen(url);   // mở trình duyệt thanh toán; webhook PayOS cộng Ma Thạch khi xong
            ShowFeedback("Đã mở trang thanh toán. Hoàn tất rồi quay lại — Ma Thạch sẽ tự cộng.", true);
        }
        else
        {
            ShowFeedback(error, false);
        }
    }

    // ── Tab Nhập Code ─────────────────────────────────────────────────────────
    private Control BuildRedeem()
    {
        var center = new CenterContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        var card   = new PanelContainer  { CustomMinimumSize = new Vector2(440, 0) };
        card.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.WoodCard, 12, UiKit.Accent, 2, 24, 22));

        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 12);

        var title = new Label { Text = "NHẬP CODE", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", UiKit.Accent);
        v.AddChild(title);

        var desc = new Label { Text = "Nhập mã quà tặng để nhận Vàng, Aetherstone hoặc vật phẩm.", HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        desc.AddThemeColorOverride("font_color", UiKit.WoodTextDim);
        v.AddChild(desc);

        var input = new LineEdit { PlaceholderText = "Ví dụ: WELCOME", CustomMinimumSize = new Vector2(0, 46), Alignment = HorizontalAlignment.Center };
        input.AddThemeStyleboxOverride("normal", UiKit.Box(UiKit.WoodDark, 8, UiKit.WoodBorder, 2, 10, 6));
        input.AddThemeStyleboxOverride("focus",  UiKit.Box(UiKit.WoodDark, 8, UiKit.Accent,     2, 10, 6));
        input.AddThemeColorOverride("font_color",             UiKit.WoodText);
        input.AddThemeColorOverride("font_placeholder_color", UiKit.WoodTextDim);
        input.AddThemeFontSizeOverride("font_size", 18);
        v.AddChild(input);

        var redeem = new Button { Text = "Đổi quà", CustomMinimumSize = new Vector2(0, 46) };
        UiKit.StyleButton(redeem, UiKit.BuyGreen, UiKit.BuyGreenHi, new Color(0.16f, 0.42f, 0.24f));
        redeem.AddThemeFontSizeOverride("font_size", 18);
        v.AddChild(redeem);

        var result = new Label { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(0, 44) };
        result.AddThemeFontSizeOverride("font_size", 15);
        v.AddChild(result);

        async void DoRedeem()
        {
            var mgr = RedeemManager.Instance;
            if (mgr == null) return;
            redeem.Disabled = true;
            result.Text = "Đang đổi mã...";
            result.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f));
            var (ok, message) = await mgr.RedeemAsync(input.Text);
            result.Text = message;
            result.AddThemeColorOverride("font_color", ok ? new Color(0.55f, 0.95f, 0.55f) : new Color(1f, 0.6f, 0.55f));
            if (ok) { input.Text = ""; RefreshWallet(); }
            redeem.Disabled = false;
        }

        redeem.Pressed      += DoRedeem;
        input.TextSubmitted += _ => DoRedeem();

        card.AddChild(v);
        center.AddChild(card);
        return center;
    }

    // ── Gacha ─────────────────────────────────────────────────────────────────
    private void RefreshLuck()
    {
        if (_luckLabel != null && GodotObject.IsInstanceValid(_luckLabel))
            _luckLabel.Text = $"Chìa Khóa Bạc: {Inventory.Instance?.CountOf("item_silver_key") ?? 0}";
    }

    private async void OnPull(int count)
    {
        var mgr = SkinManager.Instance;
        if (mgr == null) return;

        var outcome = await mgr.GachaPullAsync(count);
        if (outcome.Error != null) { ShowFeedback(outcome.Error, false); return; }

        _ = Inventory.Instance?.SyncAsync();   // chìa đã trừ ở server → làm mới túi
        RefreshWallet();
        RefreshLuck();
        if (outcome.GoldRefunded > 0) ShowFeedback($"Trùng skin → hoàn {outcome.GoldRefunded:N0} Vàng.", true);
        ShowSkinReveal(outcome.Results);
    }

    // ── Reveal skin ───────────────────────────────────────────────────────────
    private void ShowSkinReveal(List<SkinGachaResult> results)
    {
        if (results == null || results.Count == 0) return;
        _revealLayer.Visible     = true;
        _revealLayer.MouseFilter = Control.MouseFilterEnum.Stop;
        foreach (Node c in _revealGrid.GetChildren()) c.QueueFree();
        _revealGrid.Columns = results.Count > 1 ? 5 : 1;
        _revealTitle.Text   = results.Count > 1 ? $"Quay {results.Count} lần!" : "Kết quả";

        Rarity best = Rarity.Common;
        foreach (var r in results) { var rr = ParseRarity(r.Rarity); if (rr > best) best = rr; }
        _revealFlash.Color = UiKit.Fade(best == Rarity.Common ? Colors.White : UiKit.RarityColor(best), 0.6f);
        CreateTween().TweenProperty(_revealFlash, "color:a", 0f, 0.4);

        int i = 0;
        foreach (var r in results)
        {
            var card = MakeSkinRevealCard(r);
            card.Modulate = new Color(1, 1, 1, 0);
            _revealGrid.AddChild(card);
            var tw = CreateTween();
            tw.TweenInterval(0.12 + i * 0.07);
            tw.TweenProperty(card, "modulate:a", 1f, 0.25);
            i++;
        }
    }

    private Control MakeSkinRevealCard(SkinGachaResult r)
    {
        Rarity rar = ParseRarity(r.Rarity);
        Color  rc  = UiKit.RarityColor(rar);
        var card = new PanelContainer { CustomMinimumSize = new Vector2(140, 172) };
        card.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.Fade(rc, 0.18f), 12, rc, 3, 10, 10));

        var v = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        v.AddThemeConstantOverride("separation", 6);

        // Ô màu đại diện hiệu ứng (skin procedural đổi màu)
        var swatch = new ColorRect { CustomMinimumSize = new Vector2(64, 64), Color = SkinSwatchColor(r.SkinId) };
        var sw = new CenterContainer();
        sw.AddChild(swatch);
        v.AddChild(sw);

        var name = new Label { Text = r.Name, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        name.AddThemeFontSizeOverride("font_size", 13);
        name.AddThemeColorOverride("font_color", UiKit.WoodText);
        v.AddChild(name);

        var tag = new Label { Text = r.IsNew ? "MỚI!" : "Trùng", HorizontalAlignment = HorizontalAlignment.Center };
        tag.AddThemeFontSizeOverride("font_size", 12);
        tag.AddThemeColorOverride("font_color", r.IsNew ? UiKit.BuyGreenHi : UiKit.WoodTextDim);
        v.AddChild(tag);

        card.AddChild(v);
        return card;
    }

    private static Color SkinSwatchColor(string skinId)
    {
        var def = SkinManager.Instance?.Get(skinId);
        return def?.ColorValue ?? Colors.White;
    }

    private static Rarity ParseRarity(string s) => (s ?? "").ToLowerInvariant() switch
    {
        "rare"      => Rarity.Rare,
        "epic"      => Rarity.Epic,
        "legendary" => Rarity.Legendary,
        _           => Rarity.Common,
    };

    private void ShowReveal(List<ItemEntry> results)
    {
        _revealLayer.Visible     = true;
        _revealLayer.MouseFilter = Control.MouseFilterEnum.Stop;
        foreach (Node c in _revealGrid.GetChildren()) c.QueueFree();
        _revealGrid.Columns  = results.Count > 1 ? 5 : 1;
        _revealTitle.Text    = results.Count > 1 ? $"Quay {results.Count} lần!" : "Kết quả";

        Rarity best = Rarity.Common;
        foreach (var d in results) if (d != null && d.Rarity > best) best = d.Rarity;
        _revealFlash.Color = UiKit.Fade(best == Rarity.Common ? Colors.White : UiKit.RarityColor(best), 0.6f);
        CreateTween().TweenProperty(_revealFlash, "color:a", 0f, 0.4);

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
        _revealLayer.Visible     = false;
        _revealLayer.MouseFilter = Control.MouseFilterEnum.Ignore;
    }

    private Control MakeRevealCard(ItemEntry def)
    {
        Rarity r  = def?.Rarity ?? Rarity.Common;
        Color  rc = UiKit.RarityColor(r);
        var card  = new PanelContainer { CustomMinimumSize = new Vector2(140, 172) };
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

    // ── Buy flow ──────────────────────────────────────────────────────────────
    private void RequestBuy(string itemId)
    {
        var listing = Shop.Instance?.GetListing(itemId);
        var def     = ItemDatabase.Instance?.Get(itemId);
        if (listing == null || def == null) return;
        _pendingItemId       = itemId;
        _confirmDialog.DialogText = $"Mua {def.NameVi} với giá {listing.Price:N0} {(listing.CurrencyType == ShopCurrency.MaThach ? "Aetherstone" : "Vàng")}?";
        _confirmDialog.PopupCentered();
    }

    private async void OnBuyConfirmed()
    {
        if (string.IsNullOrEmpty(_pendingItemId)) return;
        var    def    = ItemDatabase.Instance?.Get(_pendingItemId);
        string name   = def?.NameVi ?? _pendingItemId;
        var    result = Shop.Instance != null ? await Shop.Instance.BuyAsync(_pendingItemId) : Shop.BuyResult.InvalidItem;
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
        if (_feedbackLabel == null) return;
        _feedbackLabel.Text = msg;
        _feedbackLabel.AddThemeColorOverride("font_color", ok ? new Color(0.55f, 0.95f, 0.55f) : new Color(1f, 0.6f, 0.55f));
    }

    // ── Ads ───────────────────────────────────────────────────────────────────
    private void OnWatchRewarded()
    {
        var ad = AdManager.Instance;
        if (ad == null)           { ShowFeedback("Hệ thống quảng cáo chưa sẵn sàng", false); return; }
        if (!ad.CanWatchRewarded) { ShowFeedback("Bạn đã hết lượt xem hôm nay", false);      return; }
        ad.WatchRewardedForGold();
    }

    private void OnAdReward(int gold)
    {
        if (_shopRoot == null || !_shopRoot.Visible) return;
        SetTab(Tab.Topup);
        ShowFeedback($"+{gold} Vàng từ quảng cáo!", true);
    }

    private void OnRemoveAds()
    {
        var ad = AdManager.Instance;
        if (ad == null) return;
        ad.SetAdsRemoved(true);
        if (_shopRoot.Visible) SetTab(Tab.Topup);
        ShowFeedback("Đã gỡ quảng cáo (banner + interstitial). Rewarded vẫn xem được.", true);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
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
        foreach (var child in node.GetChildren()) IgnoreMouse(child);
    }

    private static void EnsureInputAction()
    {
        if (!InputMap.HasAction("toggle_shop")) InputMap.AddAction("toggle_shop");
        foreach (var e in InputMap.ActionGetEvents("toggle_shop"))
            if (e is InputEventKey k && k.PhysicalKeycode == Key.P) return;
        InputMap.ActionAddEvent("toggle_shop", new InputEventKey { PhysicalKeycode = Key.P });
    }
}
