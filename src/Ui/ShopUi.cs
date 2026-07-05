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
    private const string GoldenKeyIconPath = "res://assets/sprites/items/golden_key.png";

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

    // Gacha luck labels (built dynamically inside gacha tab)
    private readonly List<(Label label, bool isGold)> _gachaLuckLabels = new();
    private string _pendingItemId = "";

    // Theo dõi thanh toán: sau khi mở link PayOS, tự động hỏi server tới khi cộng được (không cần thao tác).
    private bool _watchingPayment;

    // Panel chính — co giãn theo màn hình
    private PanelContainer _panel;

    // Custom confirm dialog
    private Control _customConfirm;
    private Label _customConfirmMsg;
    private Label _customConfirmQtyLabel;
    private int _pendingQty = 1;

    // Gacha rate dialog
    private Control _gachaRateDialog;
    private VBoxContainer _gachaRateList;

    // Ad reward popup
    private Control _adRewardDialog;
    private HBoxContainer _adChestContainer;
    private Button _adWatchButton;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    public override void _Ready()
    {
        Instance = this;

        EnsureInputAction();
        ApplyStyles();
        WireSignals();

        _panel = _shopRoot.GetNode<PanelContainer>("Center/Panel");
        
        // Ép các node gốc bám sát toàn màn hình để đảm bảo bảng luôn nằm chính giữa
        _shopRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var center = _shopRoot.GetNodeOrNull<Control>("Center");
        if (center != null)
        {
            center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        }

        ResizePanel();
        GetViewport().SizeChanged += ResizePanel;
        
        BuildCustomConfirm();
        BuildGachaRateDialog();
        BuildAdRewardDialog();

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
        if (Wallet.Instance != null) { await Wallet.Instance.SyncAsync(); RefreshWallet(); }

        // Xác nhận giao dịch PayOS đã trả (server hỏi PayOS API) → cộng Ma Thạch nếu có.
        if (Shop.Instance != null)
        {
            int credited = await Shop.Instance.ConfirmPendingPaymentsAsync();
            if (credited > 0 && Wallet.Instance != null)
            {
                await Wallet.Instance.SyncAsync();
                RefreshWallet();
                ShowFeedback("✓ Đã cộng Ma Thạch từ giao dịch!", true);
            }
        }
    }
    public void Close()  { _shopRoot.Visible = false; }

    /// <summary>Panel co theo viewport: ~94% bề ngang, ~92% chiều cao, có trần để không quá to trên desktop.</summary>
    private void ResizePanel()
    {
        if (_panel == null) return;
        var vp = GetViewport().GetVisibleRect().Size;
        float w = Mathf.Min(vp.X * 0.96f, 1600f);
        float h = Mathf.Min(vp.Y * 0.94f, 1000f);
        _panel.CustomMinimumSize = new Vector2(w, h);
        
        // Force the panel size explicitly to make sure CenterContainer recalculates
        _panel.Size = new Vector2(w, h);
    }

    // ── Wire signals từ scene ─────────────────────────────────────────────────
    private void WireSignals()
    {
        _closeButton.Pressed    += Close;
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
        panel?.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.24f, 0.17f, 0.13f, 0.98f), 20, new Color(0.4f, 0.28f, 0.2f, 1f), 3, 16, 16));

        // Sidebar
        var sidebar = _shopRoot.GetNode<PanelContainer>("Center/Panel/RootH/Sidebar");
        sidebar?.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.18f, 0.12f, 0.09f, 0.95f), 16, new Color(0.35f, 0.22f, 0.15f, 1f), 2, 12, 16));

        var shopTitle = _shopRoot.GetNodeOrNull<Label>("Center/Panel/RootH/Sidebar/SidebarVBox/ShopTitle");
        if (shopTitle != null) shopTitle.AddThemeColorOverride("font_color", UiKit.Accent);

        StyleTabButton(_tabFeatured);
        StyleTabButton(_tabItems);
        StyleTabButton(_tabGacha);
        StyleTabButton(_tabTopup);
        StyleTabButton(_tabRedeem);

        // Gold pill
        var goldPill = _shopRoot.GetNodeOrNull<PanelContainer>("Center/Panel/RootH/RightColumn/TopBar/GoldPill");
        goldPill?.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.12f, 0.08f, 0.05f, 0.8f), 16, UiKit.Gold, 1, 16, 8));
        if (_goldLabel  != null) _goldLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.9f, 0.8f));
        if (_goldLabel  != null) _goldLabel.AddThemeFontSizeOverride("font_size", 22);

        // Aether pill
        var aetherPill = _shopRoot.GetNodeOrNull<PanelContainer>("Center/Panel/RootH/RightColumn/TopBar/AetherPill");
        aetherPill?.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.12f, 0.08f, 0.05f, 0.8f), 16, UiKit.MaThach, 1, 16, 8));
        if (_aetherLabel != null) _aetherLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.9f, 0.8f));
        if (_aetherLabel != null) _aetherLabel.AddThemeFontSizeOverride("font_size", 22);

        // Load icon texture cho gold/aether pill
        LoadPillIcon("Center/Panel/RootH/RightColumn/TopBar/GoldPill/GoldPillH/GoldIcon",   UiKit.GoldIconPath);
        LoadPillIcon("Center/Panel/RootH/RightColumn/TopBar/AetherPill/AetherPillH/AetherIcon", UiKit.AetherIconPath);

        // Close button
        UiKit.StyleButton(_closeButton, new Color(0.7f, 0.25f, 0.25f), new Color(0.85f, 0.35f, 0.35f), new Color(0.55f, 0.15f, 0.15f), radius: 16);
        _closeButton.AddThemeFontSizeOverride("font_size", 26);
        _closeButton.CustomMinimumSize = new Vector2(56, 56);

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
        UiKit.StyleButton(btn, new Color(0.2f, 0.14f, 0.1f, 0f), new Color(0.35f, 0.25f, 0.18f, 0.8f), new Color(0.5f, 0.35f, 0.22f, 1f), radius: 12);
        btn.AddThemeColorOverride("font_color",         new Color(0.85f, 0.8f, 0.7f));
        btn.AddThemeColorOverride("font_hover_color",   Colors.White);
        btn.AddThemeColorOverride("font_pressed_color", Colors.White);
        btn.AddThemeFontSizeOverride("font_size", 20);
        btn.CustomMinimumSize = new Vector2(0, 64);
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
        _gachaLuckLabels.Clear();

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

        var poster = new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill, ClipContents = true };
        
        var tex = GD.Load<Texture2D>("res://assets/sprites/ui/shop_ui_advance/poster.png");
        if (tex != null)
        {
            var bg = new TextureRect 
            { 
                Texture = tex, 
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, 
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            };
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            poster.AddChild(bg);
        }
        
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
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 12);

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

        var card = new Button { CustomMinimumSize = new Vector2(144, 196), TooltipText = def?.NameVi ?? id };
        card.AddThemeStyleboxOverride("normal",  UiKit.Box(new Color(0.26f, 0.18f, 0.13f, 0.9f), 16, new Color(0.4f, 0.28f, 0.2f, 1f), 2, 8, 8));
        card.AddThemeStyleboxOverride("hover",   UiKit.Box(new Color(0.35f, 0.25f, 0.18f, 1f), 16, UiKit.Accent, 2, 8, 8));
        card.AddThemeStyleboxOverride("pressed", UiKit.Box(new Color(0.45f, 0.32f, 0.24f, 1f), 16, UiKit.Accent, 2, 8, 8));
        card.AddThemeStyleboxOverride("focus",   UiKit.Box(new Color(0, 0, 0, 0), 16));

        var v = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        v.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        v.AddThemeConstantOverride("separation", 8);
        v.AddChild(UiKit.ItemIcon(def, accent, 76));

        var name = new Label { Text = def?.NameVi ?? id, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        name.AddThemeFontSizeOverride("font_size", 16);
        name.AddThemeColorOverride("font_color", new Color(0.95f, 0.9f, 0.8f));
        v.AddChild(name);

        var price = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        price.AddThemeConstantOverride("separation", 6);
        AddIcon(price, isMa ? UiKit.AetherIconPath : UiKit.GoldIconPath, 24);
        var pl = new Label { Text = $"{listing.Price:N0}" };
        pl.AddThemeFontSizeOverride("font_size", 18);
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
        _gachaLuckLabels.Clear();

        var tabContainer = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        tabContainer.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());

        // Làm to và đẹp các nút Tab "Chìa Bạc" / "Chìa Vàng"
        tabContainer.AddThemeColorOverride("font_selected_color", Colors.White);
        tabContainer.AddThemeColorOverride("font_unselected_color", new Color(0.8f, 0.7f, 0.6f));
        tabContainer.AddThemeFontSizeOverride("font_size", 24);
        tabContainer.AddThemeConstantOverride("side_margin", 8);

        var selectedStyle = UiKit.Box(new Color(0.4f, 0.3f, 0.22f, 1f), 12, UiKit.Accent, 3, 32, 12);
        var unselectedStyle = UiKit.Box(new Color(0.25f, 0.18f, 0.13f, 1f), 12, new Color(0.35f, 0.25f, 0.18f, 1f), 2, 32, 12);
        
        tabContainer.AddThemeStyleboxOverride("tab_selected", selectedStyle);
        tabContainer.AddThemeStyleboxOverride("tab_unselected", unselectedStyle);
        tabContainer.AddThemeStyleboxOverride("tab_hovered", unselectedStyle); // Tránh bị đè style
        tabContainer.AddThemeStyleboxOverride("tab_focus", new StyleBoxEmpty());

        tabContainer.AddChild(BuildGachaBanner(false));
        tabContainer.AddChild(BuildGachaBanner(true));

        RefreshLuck();
        return tabContainer;
    }

    private Control BuildGachaBanner(bool isGold)
    {
        var v = new VBoxContainer();
        v.Name = isGold ? "Chìa Vàng" : "Chìa Bạc";
        v.AddThemeConstantOverride("separation", 12);

        var banner = new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill, ClipContents = true };
        
        var bannerTex = GD.Load<Texture2D>(isGold 
            ? "res://assets/sprites/ui/shop_ui_advance/gacha_banner/sakura banner.png" 
            : "res://assets/sprites/ui/shop_ui_advance/gacha_banner/daily banner.png");
            
        var bg = new TextureRect 
        { 
            Texture = bannerTex, 
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, 
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered
        };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        banner.AddChild(bg);

        var bv = new VBoxContainer();
        bv.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        
        var topRow = new HBoxContainer();
        topRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var rate = new Button { Text = "Tỉ lệ %", CustomMinimumSize = new Vector2(100, 44) };
        UiKit.StyleButton(rate, new Color(0.25f, 0.18f, 0.13f, 1f), new Color(0.35f, 0.25f, 0.18f, 1f), UiKit.Accent, radius: 12);
        rate.AddThemeFontSizeOverride("font_size", 18);
        rate.Pressed += () => { if (_gachaRateDialog != null) { PopulateGachaRateList(); _gachaRateDialog.Visible = true; } };
        topRow.AddChild(rate);
        bv.AddChild(topRow);

        var bc = new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        bv.AddChild(bc);
        
        banner.AddChild(bv);
        v.AddChild(banner);

        var bottom = new HBoxContainer();
        bottom.AddThemeConstantOverride("separation", 16);
        var luckLabel = new Label { VerticalAlignment = VerticalAlignment.Center, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        luckLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.9f, 0.8f));
        luckLabel.AddThemeFontSizeOverride("font_size", 20);
        _gachaLuckLabels.Add((luckLabel, isGold));

        bottom.AddChild(luckLabel);
        bottom.AddChild(MakePullButton("x1",  1, isGold));
        bottom.AddChild(MakePullButton("x10", 10, isGold));
        v.AddChild(bottom);

        return v;
    }

    private Button MakePullButton(string label, int count, bool isGold)
    {
        var b = new Button { CustomMinimumSize = new Vector2(180, 72), TooltipText = $"Tốn {count} Chìa Khóa {(isGold ? "Vàng" : "Bạc")}" };
        
        var btnTex = GD.Load<Texture2D>("res://assets/sprites/ui/shop_ui_advance/gacha_banner/gacha button.png");
        var sb = new StyleBoxTexture { Texture = btnTex };
        b.AddThemeStyleboxOverride("normal", sb);
        
        var sbHover = new StyleBoxTexture { Texture = btnTex, ModulateColor = new Color(1.2f, 1.2f, 1.2f) };
        b.AddThemeStyleboxOverride("hover", sbHover);
        b.AddThemeStyleboxOverride("pressed", sbHover);
        b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

        var h = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        h.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        h.AddThemeConstantOverride("separation", 12); // Tăng khoảng cách
        AddIcon(h, isGold ? GoldenKeyIconPath : KeyIconPath, 48); // Tăng kích cỡ icon
        
        var l = new Label { Text = label, VerticalAlignment = VerticalAlignment.Center };
        l.AddThemeFontSizeOverride("font_size", 28);
        l.AddThemeColorOverride("font_color", new Color(0.1f, 0.1f, 0.1f)); // Màu đen cho chữ dễ nhìn
        l.AddThemeConstantOverride("outline_size", 1); // Căn thêm viền mờ cho nét
        l.AddThemeColorOverride("font_outline_color", new Color(0.1f, 0.1f, 0.1f, 0.5f));
        
        h.AddChild(l);
        b.AddChild(h);
        IgnoreMouse(h);
        b.Pressed += () => OnPull(count, isGold);
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
            Text = "▶  Quà Tặng Quảng Cáo",
            CustomMinimumSize   = new Vector2(0, 72),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        UiKit.StyleButton(watch, new Color(0.2f, 0.5f, 0.3f, 1f), new Color(0.25f, 0.6f, 0.35f, 1f), new Color(0.15f, 0.4f, 0.25f, 1f), radius: 12);
        watch.AddThemeFontSizeOverride("font_size", 22);
        watch.Pressed += () => {
            RefreshAdDialog();
            _adRewardDialog.Visible = true;
        };
        adRow.AddChild(watch);

        var noAds = new Button { Text = removed ? "Đã gỡ QC ✓" : "Gỡ QC", CustomMinimumSize = new Vector2(200, 72), Disabled = removed };
        UiKit.StyleButton(noAds, new Color(0.25f, 0.18f, 0.13f, 1f), new Color(0.35f, 0.25f, 0.18f, 1f), UiKit.Accent, radius: 12);
        noAds.AddThemeColorOverride("font_color", new Color(0.95f, 0.9f, 0.8f));
        noAds.AddThemeFontSizeOverride("font_size", 22);
        noAds.Pressed += OnRemoveAds;
        adRow.AddChild(noAds);
        v.AddChild(adRow);

        v.AddChild(SectionLabel("Nạp Ma Thạch (thanh toán PayOS)"));

        var packsScroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        packsScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        var packsGrid = new GridContainer { Columns = 4 };
        packsGrid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        packsGrid.AddThemeConstantOverride("h_separation", 12);
        packsGrid.AddThemeConstantOverride("v_separation", 12);
        packsScroll.AddChild(packsGrid);
        v.AddChild(packsScroll);

        var loading = new Label { Text = "Đang tải gói nạp..." };
        loading.AddThemeColorOverride("font_color", UiKit.WoodTextDim);
        packsGrid.AddChild(loading);

        _ = FillPacks(packsGrid);

        // Nút xác nhận sau khi trả tiền (tự thử lại để chờ PayOS settle vài giây).
        var confirmBtn = new Button { Text = "✓ Tôi đã thanh toán xong → Cộng Ma Thạch", CustomMinimumSize = new Vector2(0, 60) };
        UiKit.StyleButton(confirmBtn, new Color(0.25f, 0.18f, 0.13f, 1f), UiKit.Fade(UiKit.MaThach, 0.3f), UiKit.MaThach, radius: 12);
        confirmBtn.AddThemeColorOverride("font_color", new Color(0.95f, 0.9f, 0.8f));
        confirmBtn.AddThemeFontSizeOverride("font_size", 20);
        confirmBtn.Pressed += () => OnConfirmPayment(confirmBtn);
        v.AddChild(confirmBtn);

        return v;
    }

    private async void OnConfirmPayment(Button btn)
    {
        if (Shop.Instance == null) return;
        btn.Disabled = true;
        int credited = 0;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            ShowFeedback(attempt == 0 ? "Đang kiểm tra giao dịch..." : $"Chờ PayOS xác nhận... (lần {attempt + 1})", true);
            credited = await Shop.Instance.ConfirmPendingPaymentsAsync();
            if (credited > 0) break;
            if (attempt < 2) await ToSignal(GetTree().CreateTimer(4.0), SceneTreeTimer.SignalName.Timeout);
        }

        if (credited > 0)
        {
            if (Wallet.Instance != null) { await Wallet.Instance.SyncAsync(); RefreshWallet(); }
            ShowFeedback($"✓ Đã cộng Ma Thạch từ {credited} giao dịch!", true);
        }
        else
        {
            ShowFeedback("Chưa thấy giao dịch đã trả. Nếu vừa trả xong, đợi vài giây rồi bấm lại.", false);
        }
        if (GodotObject.IsInstanceValid(btn)) btn.Disabled = false;
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
        var card = new Button { CustomMinimumSize = new Vector2(190, 180), TooltipText = p.Description };
        card.AddThemeStyleboxOverride("normal",  UiKit.Box(new Color(0.26f, 0.18f, 0.13f, 0.9f), 16, UiKit.MaThach, 2, 8, 8));
        card.AddThemeStyleboxOverride("hover",   UiKit.Box(UiKit.Fade(UiKit.MaThach, 0.18f), 16, UiKit.MaThach, 2, 8, 8));
        card.AddThemeStyleboxOverride("pressed", UiKit.Box(UiKit.Fade(UiKit.MaThach, 0.24f), 16, UiKit.MaThach, 2, 8, 8));

        var v = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        v.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        v.AddThemeConstantOverride("separation", 8);

        var amount = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        amount.AddThemeConstantOverride("separation", 6);
        AddIcon(amount, UiKit.AetherIconPath, 32);
        var al = new Label { Text = $"{p.CurrencyAmount:N0}" };
        al.AddThemeFontSizeOverride("font_size", 26);
        al.AddThemeColorOverride("font_color", UiKit.MaThach);
        amount.AddChild(al);
        v.AddChild(amount);

        if (p.BonusPercent > 0)
        {
            var bonus = new Label { Text = $"+{p.BonusPercent}%", HorizontalAlignment = HorizontalAlignment.Center };
            bonus.AddThemeFontSizeOverride("font_size", 16);
            bonus.AddThemeColorOverride("font_color", UiKit.BuyGreenHi);
            v.AddChild(bonus);
        }

        var name = new Label { Text = p.Name, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        name.AddThemeFontSizeOverride("font_size", 16);
        name.AddThemeColorOverride("font_color", new Color(0.8f, 0.75f, 0.7f));
        v.AddChild(name);

        var price = new Label { Text = $"{(p.PriceVnd ?? 0):N0}đ", HorizontalAlignment = HorizontalAlignment.Center };
        price.AddThemeFontSizeOverride("font_size", 20);
        price.AddThemeColorOverride("font_color", new Color(0.95f, 0.9f, 0.8f));
        v.AddChild(price);

        card.AddChild(v);
        IgnoreMouse(v);
        card.Pressed += () => OnBuyPack(p);
        return card;
    }

    private void OnBuyPack(Shop.PaymentPack p)
    {
        // Hỏi xác nhận TRƯỚC khi mở trang thanh toán (tránh bấm nhầm tốn tiền thật).
        string price = $"{(p.PriceVnd ?? 0):N0}đ";
        ShowPayConfirm(
            "XÁC NHẬN THANH TOÁN",
            $"Bạn có muốn mua gói\n\"{p.Name}\"?\n\nNhận {p.CurrencyAmount:N0} Ma Thạch\nGiá {price} (thanh toán qua PayOS)",
            () => DoBuyPack(p));
    }

    private async void DoBuyPack(Shop.PaymentPack p)
    {
        if (Shop.Instance == null) return;
        ShowFeedback("Đang tạo link thanh toán...", true);
        var (url, error) = await Shop.Instance.CreatePayOsLinkAsync(p.Id);
        if (!string.IsNullOrEmpty(url))
        {
            OS.ShellOpen(url);   // mở trình duyệt thanh toán
            StartPaymentWatch(); // tự động hỏi server tới khi cộng — người chơi không cần bấm gì
            ShowFeedback("Đã mở trang thanh toán. Trả xong quay lại là Ma Thạch tự cộng.", true);
        }
        else
        {
            ShowFeedback(error, false);
        }
    }

    // ── Tự động xác nhận thanh toán ───────────────────────────────────────────
    /// <summary>Bắt đầu poll ngầm sau khi mở link PayOS (ShopUi là autoload nên chạy kể cả khi đóng shop).</summary>
    private void StartPaymentWatch()
    {
        if (_watchingPayment) return;
        _watchingPayment = true;
        _ = WatchPaymentLoop();
    }

    private async System.Threading.Tasks.Task WatchPaymentLoop()
    {
        // Hỏi server (server hỏi PayOS API) mỗi 4s, tối đa ~3 phút. Trả xong là cộng ngay.
        for (int i = 0; i < 45 && _watchingPayment; i++)
        {
            await ToSignal(GetTree().CreateTimer(4.0), SceneTreeTimer.SignalName.Timeout);
            if (!_watchingPayment) return;
            if (await CheckPaymentNow() > 0) { _watchingPayment = false; return; }
        }
        _watchingPayment = false;
    }

    /// <summary>Một lần kiểm tra: xác nhận qua PayOS API + so số dư (bắt cả trường hợp webhook đã tự cộng).
    /// Trả > 0 nếu Ma Thạch tăng → dừng theo dõi.</summary>
    private async System.Threading.Tasks.Task<int> CheckPaymentNow()
    {
        if (Shop.Instance == null) return 0;
        int before = Wallet.Instance?.MaThach ?? 0;

        int credited = await Shop.Instance.ConfirmPendingPaymentsAsync();   // đường 1: server hỏi PayOS API
        if (Wallet.Instance != null) { await Wallet.Instance.SyncAsync(); RefreshWallet(); }   // đường 2: webhook đã cộng sẵn

        int delta = (Wallet.Instance?.MaThach ?? 0) - before;
        if (delta > 0) GlobalToast($"✓ Đã cộng {delta:N0} Ma Thạch!");
        return delta > 0 ? delta : credited;
    }

    /// <summary>Khi app/cửa sổ được focus lại (vừa thanh toán xong quay về) → kiểm tra ngay lập tức.</summary>
    public override void _Notification(int what)
    {
        if ((what == NotificationApplicationFocusIn || what == NotificationWMWindowFocusIn) && _watchingPayment)
            _ = CheckPaymentNow();
    }

    /// <summary>Toast phủ toàn màn hình (hiện kể cả khi shop đang đóng).</summary>
    private void GlobalToast(string msg)
    {
        var ctrl = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        ctrl.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(ctrl);
        UiKit.Toast(ctrl, msg, 2.5f);
        var t = GetTree().CreateTimer(3.0);
        t.Timeout += () => { if (GodotObject.IsInstanceValid(ctrl)) ctrl.QueueFree(); };
    }

    /// <summary>Hộp thoại xác nhận đơn giản (Đồng ý / Hủy). Dựng ngay tại thời điểm gọi.</summary>
    private void ShowPayConfirm(string title, string msg, System.Action onYes)
    {
        var overlay = new Control();
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _shopRoot.AddChild(overlay);

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.78f), MouseFilter = Control.MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(560, 0) };
        panel.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.24f, 0.17f, 0.13f, 0.98f), 24, UiKit.MaThach, 3, 32, 32));
        center.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 24);
        panel.AddChild(vbox);

        var t = new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Center };
        t.AddThemeFontSizeOverride("font_size", 28);
        t.AddThemeColorOverride("font_color", UiKit.Accent);
        vbox.AddChild(t);

        var m = new Label { Text = msg, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        m.AddThemeFontSizeOverride("font_size", 22);
        m.AddThemeColorOverride("font_color", new Color(0.95f, 0.9f, 0.8f));
        vbox.AddChild(m);

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 20);
        vbox.AddChild(row);

        var yes = new Button { Text = "ĐỒNG Ý", CustomMinimumSize = new Vector2(200, 64) };
        UiKit.StyleButton(yes, new Color(0.2f, 0.5f, 0.3f, 1f), new Color(0.25f, 0.6f, 0.35f, 1f), new Color(0.15f, 0.4f, 0.25f, 1f), radius: 16);
        yes.AddThemeFontSizeOverride("font_size", 22);
        yes.Pressed += () => { overlay.QueueFree(); onYes?.Invoke(); };
        row.AddChild(yes);

        var no = new Button { Text = "HỦY BỎ", CustomMinimumSize = new Vector2(200, 64) };
        UiKit.StyleButton(no, new Color(0.7f, 0.25f, 0.25f), new Color(0.85f, 0.35f, 0.35f), new Color(0.55f, 0.15f, 0.15f), radius: 16);
        no.AddThemeFontSizeOverride("font_size", 22);
        no.Pressed += () => overlay.QueueFree();
        row.AddChild(no);
    }

    // ── Tab Nhập Code ─────────────────────────────────────────────────────────
    private Control BuildRedeem()
    {
        var center = new CenterContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        var card   = new PanelContainer  { CustomMinimumSize = new Vector2(560, 0) };
        card.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.24f, 0.17f, 0.13f, 0.95f), 20, UiKit.Accent, 2, 32, 28));

        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 16);

        var title = new Label { Text = "NHẬP CODE", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 32);
        title.AddThemeColorOverride("font_color", UiKit.Accent);
        v.AddChild(title);

        var desc = new Label { Text = "Nhập mã quà tặng để nhận Vàng, Aetherstone hoặc vật phẩm.", HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        desc.AddThemeColorOverride("font_color", new Color(0.8f, 0.75f, 0.7f));
        desc.AddThemeFontSizeOverride("font_size", 18);
        v.AddChild(desc);

        var input = new LineEdit { PlaceholderText = "Ví dụ: WELCOME", CustomMinimumSize = new Vector2(0, 64), Alignment = HorizontalAlignment.Center };
        input.AddThemeStyleboxOverride("normal", UiKit.Box(new Color(0.18f, 0.12f, 0.09f, 1f), 12, new Color(0.4f, 0.28f, 0.2f, 1f), 2, 16, 8));
        input.AddThemeStyleboxOverride("focus",  UiKit.Box(new Color(0.18f, 0.12f, 0.09f, 1f), 12, UiKit.Accent,     2, 16, 8));
        input.AddThemeColorOverride("font_color",             new Color(0.95f, 0.9f, 0.8f));
        input.AddThemeColorOverride("font_placeholder_color", new Color(0.6f, 0.55f, 0.5f));
        input.AddThemeFontSizeOverride("font_size", 24);
        v.AddChild(input);

        var redeem = new Button { Text = "Đổi quà", CustomMinimumSize = new Vector2(0, 64) };
        UiKit.StyleButton(redeem, new Color(0.2f, 0.5f, 0.3f, 1f), new Color(0.25f, 0.6f, 0.35f, 1f), new Color(0.15f, 0.4f, 0.25f, 1f), radius: 16);
        redeem.AddThemeFontSizeOverride("font_size", 24);
        v.AddChild(redeem);

        var result = new Label { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(0, 44) };
        result.AddThemeFontSizeOverride("font_size", 18);
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
        var mgr = SkinManager.Instance;
        int cur = mgr?.PityCurrent ?? 0;
        int tgt = mgr?.PityTarget  ?? 60;
        foreach (var (lbl, isGold) in _gachaLuckLabels)
        {
            if (lbl != null && GodotObject.IsInstanceValid(lbl))
                lbl.Text = $"Bảo hiểm trúng thưởng: {cur}/{tgt}";
        }
    }

    private async void OnPull(int count, bool isGold)
    {
        var mgr = SkinManager.Instance;
        if (mgr == null) return;

        var outcome = await mgr.GachaPullAsync(count, isGold);
        if (outcome.Error != null) { ShowFeedback(outcome.Error, false); return; }

        _ = Inventory.Instance?.SyncAsync();   // chìa đã trừ ở server → làm mới túi
        RefreshWallet();
        RefreshLuck();
        if (outcome.GoldRefunded > 0) ShowFeedback($"Trùng skin → hoàn {outcome.GoldRefunded:N0} Vàng.", true);
        ShowSkinReveal(outcome.Results);
    }

    // ── Reveal skin ───────────────────────────────────────────────────────────
    private void ShowSkinReveal(List<SkinGachaResult> results, string titleOverride = null, bool skipShake = false)
    {
        if (results == null || results.Count == 0) return;
        
        Rarity best = Rarity.Common;
        var cards = new List<(Control root, Control front, Control back, Rarity rarity)>();
        foreach (var r in results)
        {
            var rar = ParseRarity(r.Rarity);
            if (rar > best) best = rar;
            cards.Add(MakeSkinRevealCardWithBack(r));
        }

        RunRevealAnimation(results.Count, best, cards, titleOverride, skipShake);
    }

    private (Control root, Control front, Control back, Rarity rarity) MakeSkinRevealCardWithBack(SkinGachaResult r)
    {
        Rarity rar = ParseRarity(r.Rarity);
        Color  rc  = UiKit.RarityColor(rar);
        
        var root = new Control { CustomMinimumSize = new Vector2(140, 172) };

        var front = new PanelContainer();
        front.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        front.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.Fade(rc, 0.18f), 12, rc, 3, 10, 10));

        var v = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        v.AddThemeConstantOverride("separation", 6);

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

        front.AddChild(v);
        front.Visible = false;

        var back = new PanelContainer();
        back.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        back.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.WoodDark, 12, UiKit.WoodBorder, 3));
        var backCenter = new CenterContainer();
        var qm = new Label { Text = "?" };
        qm.AddThemeFontSizeOverride("font_size", 60);
        qm.AddThemeColorOverride("font_color", UiKit.WoodBorder);
        backCenter.AddChild(qm);
        back.AddChild(backCenter);

        root.AddChild(back);
        root.AddChild(front);

        return (root, front, back, rar);
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

    private void ShowReveal(List<ItemEntry> results, string titleOverride = null, bool skipShake = false)
    {
        if (results == null || results.Count == 0) return;
        
        Rarity best = Rarity.Common;
        var cards = new List<(Control root, Control front, Control back, Rarity rarity)>();
        foreach (var def in results)
        {
            var rar = def?.Rarity ?? Rarity.Common;
            if (rar > best) best = rar;
            cards.Add(MakeRevealCardWithBack(def));
        }

        RunRevealAnimation(results.Count, best, cards, titleOverride, skipShake);
    }

    private void HideReveal()
    {
        _revealLayer.Visible     = false;
        _revealLayer.MouseFilter = Control.MouseFilterEnum.Ignore;
    }

    private (Control root, Control front, Control back, Rarity rarity) MakeRevealCardWithBack(ItemEntry def)
    {
        Rarity r  = def?.Rarity ?? Rarity.Common;
        Color  rc = UiKit.RarityColor(r);
        
        var root = new Control { CustomMinimumSize = new Vector2(170, 210) };

        var front = new PanelContainer();
        front.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        front.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.Fade(rc, 0.18f), 16, rc, 3, 10, 10));

        var v = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        v.AddThemeConstantOverride("separation", 8);
        v.AddChild(UiKit.ItemIcon(def, rc, 86));

        var name = new Label { Text = def?.NameVi ?? "?", HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        name.AddThemeFontSizeOverride("font_size", 18);
        name.AddThemeColorOverride("font_color", new Color(0.95f, 0.9f, 0.8f));
        v.AddChild(name);

        var rl = new Label { Text = UiKit.RarityName(r), HorizontalAlignment = HorizontalAlignment.Center };
        rl.AddThemeFontSizeOverride("font_size", 16);
        rl.AddThemeColorOverride("font_color", rc);
        v.AddChild(rl);

        front.AddChild(v);
        front.Visible = false;

        var back = new PanelContainer();
        back.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        back.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.WoodDark, 16, UiKit.WoodBorder, 3));
        var backCenter = new CenterContainer();
        var qm = new Label { Text = "?" };
        qm.AddThemeFontSizeOverride("font_size", 70);
        qm.AddThemeColorOverride("font_color", UiKit.WoodBorder);
        backCenter.AddChild(qm);
        back.AddChild(backCenter);

        root.AddChild(back);
        root.AddChild(front);

        return (root, front, back, r);
    }

    // ── Gacha Master Animation Coroutine ──────────────────────────────────────
    private async void RunRevealAnimation(int count, Rarity best, List<(Control root, Control front, Control back, Rarity rarity)> cards, string titleOverride = null, bool skipShake = false)
    {
        _revealLayer.Visible     = true;
        _revealLayer.MouseFilter = Control.MouseFilterEnum.Stop;
        
        foreach (Node c in _revealGrid.GetChildren()) c.QueueFree();
        _revealGrid.Columns = count > 1 ? 5 : 1;
        _revealTitle.Text   = titleOverride ?? (count > 1 ? $"Quay {count} lần!" : "Kết quả");
        
        _revealGrid.Modulate = new Color(1, 1, 1, 0);
        _revealTitle.Modulate = new Color(1, 1, 1, 0);
        _revealFlash.Color = new Color(0, 0, 0, 0); // Reset flash
        _revealLayer.Position = Vector2.Zero; // Reset shake

        Color bestColor = best == Rarity.Common ? Colors.White : UiKit.RarityColor(best);

        // --- PHASE 1: THE BUILD-UP (ORB & SHAKE) ---
        var orb = new PanelContainer();
        orb.AddThemeStyleboxOverride("panel", UiKit.Box(bestColor, 100)); 
        orb.CustomMinimumSize = new Vector2(0, 0);
        orb.PivotOffset = Vector2.Zero; 
        
        var orbCenter = new CenterContainer();
        orbCenter.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        orbCenter.AddChild(orb);
        _revealLayer.AddChild(orbCenter);

        var orbTw = CreateTween();
        orbTw.TweenProperty(orb, "custom_minimum_size", new Vector2(120, 120), 0.8f).SetTrans(Tween.TransitionType.Circ).SetEase(Tween.EaseType.In);
        orbTw.Parallel().TweenProperty(orb, "rotation", 10f, 0.8f).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
        
        if (!skipShake)
        {
            var shakeTw = CreateTween().SetLoops(15);
            shakeTw.TweenCallback(Callable.From(() => {
                _revealLayer.Position = new Vector2((float)GD.RandRange(-25.0, 25.0), (float)GD.RandRange(-25.0, 25.0));
            })).SetDelay(0.05f);

            await ToSignal(GetTree().CreateTimer(0.85f), "timeout");
            if (GodotObject.IsInstanceValid(shakeTw)) shakeTw.Kill();
        }
        else
        {
            await ToSignal(GetTree().CreateTimer(0.85f), "timeout");
        }
        
        _revealLayer.Position = Vector2.Zero;
        if (GodotObject.IsInstanceValid(orbCenter)) orbCenter.QueueFree();

        // --- PHASE 2: EXPLOSION (FLASH & PARTICLES) ---
        _revealFlash.Color = new Color(bestColor.R, bestColor.G, bestColor.B, 1f);
        var flashTw = CreateTween();
        flashTw.TweenProperty(_revealFlash, "color:a", 0f, 1.2f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

        var vpSize = GetViewport().GetVisibleRect().Size;
        for (int p = 0; p < 50; p++)
        {
            var pRect = new ColorRect { Color = (p % 3 == 0) ? Colors.White : bestColor };
            pRect.CustomMinimumSize = new Vector2(8, 8);
            float angle = (float)GD.RandRange(0.0, Mathf.Tau);
            float dist = (float)GD.RandRange(400.0, 1500.0);
            Vector2 endPos = vpSize / 2 + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
            
            pRect.Position = vpSize / 2;
            pRect.PivotOffset = new Vector2(4, 4);
            _revealLayer.AddChild(pRect);

            var pTw = CreateTween();
            pTw.SetParallel(true);
            pTw.TweenProperty(pRect, "position", endPos, (float)GD.RandRange(0.5, 1.5)).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            pTw.TweenProperty(pRect, "rotation", (float)GD.RandRange(-15.0, 15.0), 1.5f);
            pTw.TweenProperty(pRect, "modulate:a", 0f, 1f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            pTw.Chain().TweenCallback(Callable.From(() => { if (GodotObject.IsInstanceValid(pRect)) pRect.QueueFree(); }));
        }

        _revealTitle.Modulate = Colors.White;
        _revealTitle.Scale = new Vector2(2f, 2f);
        _revealTitle.PivotOffset = _revealTitle.Size / 2;
        CreateTween().TweenProperty(_revealTitle, "scale", Vector2.One, 0.5f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
        
        _revealGrid.Modulate = Colors.White;

        // --- PHASE 3: CARD FLIP SEQUENCE ---
        foreach (var c in cards) _revealGrid.AddChild(c.root);
        
        await ToSignal(GetTree(), "process_frame");
        await ToSignal(GetTree(), "process_frame");

        foreach (var c in cards)
        {
            if (GodotObject.IsInstanceValid(c.root))
            {
                c.root.PivotOffset = c.root.Size / 2;
                c.root.Scale = Vector2.Zero;
            }
        }

        // 1. Thẻ bay ra (mặt úp)
        for (int i = 0; i < cards.Count; i++)
        {
            if (!GodotObject.IsInstanceValid(cards[i].root)) continue;
            var tw = CreateTween();
            tw.TweenProperty(cards[i].root, "scale", Vector2.One, 0.3f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            await ToSignal(GetTree().CreateTimer(0.08f), "timeout");
        }

        await ToSignal(GetTree().CreateTimer(0.3f), "timeout");

        // 2. Từng thẻ lật mặt lên
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            if (!GodotObject.IsInstanceValid(c.root)) continue;
            
            bool isLegendary = c.rarity == Rarity.Legendary;
            bool isEpic = c.rarity == Rarity.Epic;
            
            if (isLegendary || isEpic)
            {
                if (!skipShake)
                {
                    // Rung thẻ trước khi lật đối với hàng xịn
                    var shakeC = CreateTween().SetLoops(6);
                    shakeC.TweenProperty(c.root, "rotation", 0.15f, 0.05f);
                    shakeC.TweenProperty(c.root, "rotation", -0.15f, 0.05f);
                    await ToSignal(GetTree().CreateTimer(isLegendary ? 0.6f : 0.3f), "timeout");
                    if (GodotObject.IsInstanceValid(shakeC)) shakeC.Kill();
                    c.root.Rotation = 0;
                }
                else
                {
                    // Khi mua hàng không cần rung, chỉ dừng 1 nhịp nhẹ
                    await ToSignal(GetTree().CreateTimer(isLegendary ? 0.3f : 0.15f), "timeout");
                }
            }

            var flipTw = CreateTween();
            flipTw.TweenProperty(c.root, "scale:x", 0f, 0.15f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            await ToSignal(flipTw, "finished");
            if (!GodotObject.IsInstanceValid(c.root)) continue;

            c.back.Visible = false;
            c.front.Visible = true;

            if (isLegendary || isEpic)
            {
                // Hào quang tỏa sáng phía sau thẻ
                var aura = new PanelContainer();
                aura.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.Fade(UiKit.RarityColor(c.rarity), 0.45f), 30));
                
                var sizeExt = isLegendary ? new Vector2(40, 40) : new Vector2(24, 24);
                aura.CustomMinimumSize = c.root.Size + sizeExt;
                aura.Position = -(sizeExt / 2);
                aura.PivotOffset = aura.CustomMinimumSize / 2;
                
                c.root.AddChild(aura);
                c.root.MoveChild(aura, 0); // Nằm dưới front & back
                
                var auraTw = CreateTween().SetLoops();
                auraTw.TweenProperty(aura, "rotation", Mathf.Tau, isLegendary ? 3f : 5f).AsRelative();
                
                var auraPulse = CreateTween().SetLoops();
                auraPulse.TweenProperty(aura, "modulate:a", 0.3f, 0.5f);
                auraPulse.TweenProperty(aura, "modulate:a", 1.0f, 0.5f);
            }

            var flipTw2 = CreateTween();
            flipTw2.TweenProperty(c.root, "scale:x", 1f, 0.15f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            flipTw2.TweenProperty(c.root, "scale", new Vector2(1.25f, 1.25f), 0.15f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            flipTw2.TweenProperty(c.root, "scale", Vector2.One, 0.15f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            
            await ToSignal(GetTree().CreateTimer(isLegendary ? 0.4f : (isEpic ? 0.2f : 0.1f)), "timeout");
        }
    }

    // ── Buy flow ──────────────────────────────────────────────────────────────
    private void RequestBuy(string itemId)
    {
        var listing = Shop.Instance?.GetListing(itemId);
        var def     = ItemDatabase.Instance?.Get(itemId);
        if (listing == null || def == null) return;
        _pendingItemId       = itemId;
        _pendingQty          = 1;
        UpdateConfirmText();
        _customConfirm.Visible = true;
    }

    private void UpdateConfirmText()
    {
        var listing = Shop.Instance?.GetListing(_pendingItemId);
        var def     = ItemDatabase.Instance?.Get(_pendingItemId);
        if (listing == null || def == null) return;
        
        string currencyName  = listing.CurrencyType == ShopCurrency.MaThach ? "Aetherstone" : "Vàng";
        int totalPrice = listing.Price * _pendingQty;
        _customConfirmMsg.Text = $"Bạn có chắc chắn muốn mua\n{def.NameVi}\nvới tổng giá {totalPrice:N0} {currencyName}?";
        
        if (_customConfirmQtyLabel != null)
            _customConfirmQtyLabel.Text = _pendingQty.ToString();
    }

    private void BuildCustomConfirm()
    {
        _customConfirm = new Control();
        _customConfirm.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _customConfirm.Visible = false;
        _shopRoot.AddChild(_customConfirm);

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.75f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        dim.MouseFilter = Control.MouseFilterEnum.Stop; // Chặn click xuống dưới
        _customConfirm.AddChild(dim);
        
        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _customConfirm.AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(540, 0) };
        panel.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.24f, 0.17f, 0.13f, 0.98f), 24, new Color(0.4f, 0.28f, 0.2f, 1f), 4, 32, 32));
        center.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 28);
        panel.AddChild(vbox);

        var title = new Label { Text = "XÁC NHẬN MUA", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 28);
        title.AddThemeColorOverride("font_color", UiKit.Accent);
        vbox.AddChild(title);

        _customConfirmMsg = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(480, 0),
        };
        _customConfirmMsg.AddThemeFontSizeOverride("font_size", 22);
        _customConfirmMsg.AddThemeColorOverride("font_color", new Color(0.95f, 0.9f, 0.8f));
        vbox.AddChild(_customConfirmMsg);

        var qtyRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        qtyRow.AddThemeConstantOverride("separation", 16);
        vbox.AddChild(qtyRow);

        var btnMinus10 = new Button { Text = "-10", CustomMinimumSize = new Vector2(64, 56) };
        UiKit.StyleButton(btnMinus10, new Color(0.3f, 0.22f, 0.16f, 1f), new Color(0.4f, 0.3f, 0.22f, 1f), UiKit.Accent, radius: 12);
        btnMinus10.AddThemeFontSizeOverride("font_size", 20);
        btnMinus10.Pressed += () => { _pendingQty = Mathf.Max(1, _pendingQty - 10); UpdateConfirmText(); };
        qtyRow.AddChild(btnMinus10);

        var btnMinus = new Button { Text = "-", CustomMinimumSize = new Vector2(64, 56) };
        UiKit.StyleButton(btnMinus, new Color(0.3f, 0.22f, 0.16f, 1f), new Color(0.4f, 0.3f, 0.22f, 1f), UiKit.Accent, radius: 12);
        btnMinus.AddThemeFontSizeOverride("font_size", 32);
        btnMinus.Pressed += () => { _pendingQty = Mathf.Max(1, _pendingQty - 1); UpdateConfirmText(); };
        qtyRow.AddChild(btnMinus);

        var qtyPanel = new PanelContainer { CustomMinimumSize = new Vector2(100, 56) };
        qtyPanel.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.18f, 0.12f, 0.09f, 1f), 12, new Color(0.4f, 0.28f, 0.2f, 1f), 2));
        _customConfirmQtyLabel = new Label { Text = "1", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        _customConfirmQtyLabel.AddThemeFontSizeOverride("font_size", 28);
        _customConfirmQtyLabel.AddThemeColorOverride("font_color", Colors.White);
        qtyPanel.AddChild(_customConfirmQtyLabel);
        qtyRow.AddChild(qtyPanel);

        var btnPlus = new Button { Text = "+", CustomMinimumSize = new Vector2(64, 56) };
        UiKit.StyleButton(btnPlus, new Color(0.3f, 0.22f, 0.16f, 1f), new Color(0.4f, 0.3f, 0.22f, 1f), UiKit.Accent, radius: 12);
        btnPlus.AddThemeFontSizeOverride("font_size", 32);
        btnPlus.Pressed += () => { _pendingQty = Mathf.Min(999, _pendingQty + 1); UpdateConfirmText(); };
        qtyRow.AddChild(btnPlus);

        var btnPlus10 = new Button { Text = "+10", CustomMinimumSize = new Vector2(64, 56) };
        UiKit.StyleButton(btnPlus10, new Color(0.3f, 0.22f, 0.16f, 1f), new Color(0.4f, 0.3f, 0.22f, 1f), UiKit.Accent, radius: 12);
        btnPlus10.AddThemeFontSizeOverride("font_size", 20);
        btnPlus10.Pressed += () => { _pendingQty = Mathf.Min(999, _pendingQty + 10); UpdateConfirmText(); };
        qtyRow.AddChild(btnPlus10);

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 24);
        vbox.AddChild(row);

        var yes = new Button { Text = "MUA NGAY", CustomMinimumSize = new Vector2(180, 64) };
        UiKit.StyleButton(yes, new Color(0.2f, 0.5f, 0.3f, 1f), new Color(0.25f, 0.6f, 0.35f, 1f), new Color(0.15f, 0.4f, 0.25f, 1f), radius: 16);
        yes.AddThemeFontSizeOverride("font_size", 22);
        yes.Pressed += () => { _customConfirm.Visible = false; OnBuyConfirmed(); };
        row.AddChild(yes);

        var no = new Button { Text = "HỦY BỎ", CustomMinimumSize = new Vector2(180, 64) };
        UiKit.StyleButton(no, new Color(0.7f, 0.25f, 0.25f), new Color(0.85f, 0.35f, 0.35f), new Color(0.55f, 0.15f, 0.15f), radius: 16);
        no.AddThemeFontSizeOverride("font_size", 22);
        no.Pressed += () => { _customConfirm.Visible = false; };
        row.AddChild(no);
    }

    private void BuildGachaRateDialog()
    {
        _gachaRateDialog = new Control { Visible = false };
        _gachaRateDialog.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _shopRoot.AddChild(_gachaRateDialog);

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.75f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        dim.MouseFilter = Control.MouseFilterEnum.Stop;
        _gachaRateDialog.AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _gachaRateDialog.AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(500, 700) };
        panel.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.24f, 0.17f, 0.13f, 0.98f), 24, new Color(0.4f, 0.28f, 0.2f, 1f), 4, 24, 24));
        center.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 20);
        panel.AddChild(vbox);

        var header = new HBoxContainer();
        var title = new Label { Text = "TỈ LỆ RƠI (DROP RATES)", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 26);
        title.AddThemeColorOverride("font_color", UiKit.Accent);
        header.AddChild(title);

        var close = new Button { Text = "X", CustomMinimumSize = new Vector2(50, 50) };
        UiKit.StyleButton(close, new Color(0.7f, 0.25f, 0.25f), new Color(0.85f, 0.35f, 0.35f), new Color(0.55f, 0.15f, 0.15f), radius: 12);
        close.AddThemeFontSizeOverride("font_size", 22);
        close.Pressed += () => _gachaRateDialog.Visible = false;
        header.AddChild(close);
        vbox.AddChild(header);

        vbox.AddChild(new HSeparator());

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        vbox.AddChild(scroll);

        _gachaRateList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _gachaRateList.AddThemeConstantOverride("separation", 16);
        scroll.AddChild(_gachaRateList);
    }

    private void PopulateGachaRateList()
    {
        if (_gachaRateList == null || ItemDatabase.Instance == null) return;
        foreach (Node c in _gachaRateList.GetChildren()) c.QueueFree();

        var itemsByRarity = new System.Collections.Generic.Dictionary<Rarity, System.Collections.Generic.List<ItemEntry>>();
        foreach (var r in new[] { Rarity.Legendary, Rarity.Epic, Rarity.Rare, Rarity.Common })
            itemsByRarity[r] = new System.Collections.Generic.List<ItemEntry>();

        foreach (var item in ItemDatabase.Instance.Items.Values)
            itemsByRarity[item.Rarity].Add(item);

        var rates = new System.Collections.Generic.Dictionary<Rarity, string>
        {
            { Rarity.Legendary, "3%" },
            { Rarity.Epic, "12%" },
            { Rarity.Rare, "35%" },
            { Rarity.Common, "50%" }
        };

        foreach (var rarity in new[] { Rarity.Legendary, Rarity.Epic, Rarity.Rare, Rarity.Common })
        {
            var list = itemsByRarity[rarity];
            if (list.Count == 0) continue;

            var rColor = UiKit.RarityColor(rarity);

            var rHead = new Label { Text = $"{UiKit.RarityName(rarity)} - {rates[rarity]}" };
            rHead.AddThemeFontSizeOverride("font_size", 24);
            rHead.AddThemeColorOverride("font_color", rColor);
            _gachaRateList.AddChild(rHead);

            var grid = new GridContainer { Columns = 1 };
            grid.AddThemeConstantOverride("h_separation", 16);
            grid.AddThemeConstantOverride("v_separation", 12);
            _gachaRateList.AddChild(grid);

            foreach (var item in list)
            {
                var h = new HBoxContainer();
                h.AddThemeConstantOverride("separation", 12);
                
                var icon = UiKit.ItemIcon(item, rColor, 56);
                h.AddChild(icon);

                var v = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
                var name = new Label { Text = item.NameVi };
                name.AddThemeFontSizeOverride("font_size", 20);
                name.AddThemeColorOverride("font_color", Colors.White);
                v.AddChild(name);
                h.AddChild(v);

                grid.AddChild(h);
            }
        }
    }

    private async void OnBuyConfirmed()
    {
        if (string.IsNullOrEmpty(_pendingItemId)) return;
        var    def    = ItemDatabase.Instance?.Get(_pendingItemId);
        string name   = def?.NameVi ?? _pendingItemId;
        int    qty    = _pendingQty;
        var    result = Shop.Instance != null ? await Shop.Instance.BuyAsync(_pendingItemId, qty) : Shop.BuyResult.InvalidItem;
        switch (result)
        {
            case Shop.BuyResult.Success:
                RefreshWallet();
                _ = Inventory.Instance?.SyncAsync();
                if (def != null)
                {
                    ShowReveal(new List<ItemEntry> { def }, $"Đã Mua x{qty}", skipShake: true);
                }
                else
                {
                    ShowFeedback($"Đã mua {qty} {name}.", ok: true);
                }
                break;
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
        RefreshAdDialog();
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

    private void BuildAdRewardDialog()
    {
        _adRewardDialog = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.75f),
            Visible = false
        };
        _adRewardDialog.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        
        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(1000, 480) };
        panel.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.WoodCard, 16, UiKit.WoodBorder, 4, 48, 32));
        
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 36);
        
        var title = new Label { Text = "THƯỞNG XEM QUẢNG CÁO", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 42);
        title.AddThemeColorOverride("font_color", UiKit.Accent);
        v.AddChild(title);

        _adChestContainer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _adChestContainer.AddThemeConstantOverride("separation", 24);
        
        for (int i = 1; i <= 5; i++)
        {
            var tex = GD.Load<Texture2D>($"res://assets/sprites/ui/shop_ui_advance/ad/ad {i}.png");
            var rect = new TextureRect
            {
                Texture = tex,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(150, 150)
            };
            _adChestContainer.AddChild(rect);
        }
        v.AddChild(_adChestContainer);
        
        var bottom = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        bottom.AddThemeConstantOverride("separation", 36);
        
        _adWatchButton = new Button { CustomMinimumSize = new Vector2(360, 80) };
        UiKit.StyleButton(_adWatchButton, new Color(0.2f, 0.5f, 0.3f, 1f), new Color(0.25f, 0.6f, 0.35f, 1f), new Color(0.15f, 0.4f, 0.25f, 1f), radius: 12);
        _adWatchButton.AddThemeFontSizeOverride("font_size", 30);
        _adWatchButton.Pressed += () => {
            _adRewardDialog.Visible = false;
            OnWatchRewarded();
        };
        bottom.AddChild(_adWatchButton);
        
        var closeBtn = new Button { Text = "Đóng", CustomMinimumSize = new Vector2(200, 80) };
        UiKit.StyleButton(closeBtn, UiKit.WoodDark, UiKit.WoodPanel, UiKit.WoodBorder, radius: 12);
        closeBtn.AddThemeFontSizeOverride("font_size", 30);
        closeBtn.Pressed += () => _adRewardDialog.Visible = false;
        bottom.AddChild(closeBtn);
        
        v.AddChild(bottom);
        panel.AddChild(v);
        center.AddChild(panel);
        _adRewardDialog.AddChild(center);
        _shopRoot.AddChild(_adRewardDialog);
    }

    private void RefreshAdDialog()
    {
        if (_adRewardDialog == null || _adChestContainer == null) return;
        
        var ad = AdManager.Instance;
        int used = ad?.RewardsToday ?? 0;
        int cap = ad?.DailyRewardCap ?? AdConfig.DailyRewardCap;
        bool canAd = ad?.CanWatchRewarded ?? false;
        
        for (int i = 0; i < 5; i++)
        {
            if (i < _adChestContainer.GetChildCount())
            {
                var rect = _adChestContainer.GetChild<TextureRect>(i);
                if (i < used) rect.Modulate = new Color(0.3f, 0.3f, 0.3f, 0.9f);
                else rect.Modulate = Colors.White;
            }
        }
        
        if (_adWatchButton != null)
        {
            _adWatchButton.Text = canAd ? $"▶ Xem QC ({used}/{cap})" : $"Hết lượt ({used}/{cap})";
            _adWatchButton.Disabled = !canAd;
        }
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
