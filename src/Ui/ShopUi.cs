using Godot;
using FragmentOfJapanese.Autoloads;
using FragmentOfJapanese.Core;
using FragmentOfJapanese.Items;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// UI cửa hàng — autoload dựng bằng code. Mở/đóng bằng phím P (Esc để đóng).
/// 3 tab: Vật phẩm (card lưới, mua bằng Vàng/Ma Thạch) | Nạp Ma Thạch | Gacha
/// (hai tab sau là placeholder "sắp ra mắt"). Có hộp xác nhận trước khi trừ tiền.
/// </summary>
public partial class ShopUi : CanvasLayer
{
    public static ShopUi Instance { get; private set; }

    private enum Tab { Items, TopUp, Gacha }

    private const string GoldIconPath = "res://assets/sprites/items/gold.png";

    private Control            _root;
    private Label              _goldLabel;
    private Label              _maThachLabel;
    private ScrollContainer    _itemScroll;
    private GridContainer      _grid;
    private Control            _soonTopUp;
    private Control            _soonGacha;
    private Label              _feedback;
    private ConfirmationDialog _confirm;

    private string _pendingItemId = "";

    public override void _Ready()
    {
        Instance = this;
        Layer    = 11;            // trên cả túi đồ (10)

        EnsureInputAction();
        BuildUi();
        _root.Visible = false;

        if (Wallet.Instance    != null) Wallet.Instance.Changed    += RefreshWallet;
        if (Inventory.Instance != null) Inventory.Instance.Changed += BuildList;

        RefreshWallet();
        BuildList();
        SetTab(Tab.Items);
    }

    public override void _UnhandledInput(InputEvent ev)
    {
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
    public void Open()   { _root.Visible = true; RefreshWallet(); BuildList(); }
    public void Close()  { _root.Visible = false; }

    // ───────────────────────── Build UI ─────────────────────────

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

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(900, 650) };
        panel.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.PanelBg, 16, UiKit.Accent, 2));
        center.AddChild(panel);

        var outer = new MarginContainer();
        outer.AddThemeConstantOverride("margin_left",   18);
        outer.AddThemeConstantOverride("margin_right",  18);
        outer.AddThemeConstantOverride("margin_top",    16);
        outer.AddThemeConstantOverride("margin_bottom", 16);
        panel.AddChild(outer);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        outer.AddChild(vbox);

        // ---- Header ----
        var header = new PanelContainer();
        header.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.HeaderBg, 12, null, 0, 14, 8));
        vbox.AddChild(header);

        var headerRow = new HBoxContainer();
        headerRow.AddThemeConstantOverride("separation", 12);
        header.AddChild(headerRow);

        var title = new Label { Text = "CỬA HÀNG", VerticalAlignment = VerticalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 28);
        title.AddThemeColorOverride("font_color", UiKit.Accent);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        headerRow.AddChild(title);

        headerRow.AddChild(MakeWalletPill(false));   // Vàng
        headerRow.AddChild(MakeWalletPill(true));    // Ma Thạch

        var closeBtn = new Button { Text = "Đóng", CustomMinimumSize = new Vector2(84, 38) };
        UiKit.StyleButton(closeBtn, new Color(0.42f, 0.20f, 0.22f), new Color(0.60f, 0.26f, 0.28f), new Color(0.36f, 0.16f, 0.18f));
        closeBtn.Pressed += Close;
        headerRow.AddChild(closeBtn);

        // ---- Tabs ----
        var tabBar = new HBoxContainer();
        tabBar.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(tabBar);

        var group = new ButtonGroup();
        AddTab(tabBar, group, "Vật phẩm",     Tab.Items, first: true);
        AddTab(tabBar, group, "Nạp Ma Thạch", Tab.TopUp);
        AddTab(tabBar, group, "Gacha",        Tab.Gacha);

        // ---- Tab Vật phẩm: lưới card ----
        _itemScroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 416) };
        _itemScroll.SizeFlagsVertical    = Control.SizeFlags.ExpandFill;
        _itemScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        vbox.AddChild(_itemScroll);

        _grid = new GridContainer { Columns = 3 };
        _grid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _grid.AddThemeConstantOverride("h_separation", 14);
        _grid.AddThemeConstantOverride("v_separation", 14);
        _itemScroll.AddChild(_grid);

        // ---- Tab Nạp / Gacha: placeholder ----
        _soonTopUp = MakeSoonBox("Nạp Ma Thạch", "Cần tích hợp thanh toán (Google Play / App Store) + backend xác thực.");
        vbox.AddChild(_soonTopUp);
        _soonGacha = MakeSoonBox("Gacha", "Quay thưởng bằng chìa khóa — rarity, tỉ lệ, pity.");
        vbox.AddChild(_soonGacha);

        // ---- Feedback ----
        _feedback = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _feedback.AddThemeFontSizeOverride("font_size", 15);
        vbox.AddChild(_feedback);

        // ---- Confirm dialog ----
        _confirm = new ConfirmationDialog { Title = "Xác nhận", OkButtonText = "Mua", CancelButtonText = "Hủy" };
        _confirm.Confirmed += OnBuyConfirmed;
        _root.AddChild(_confirm);
    }

    private void AddTab(HBoxContainer bar, ButtonGroup group, string text, Tab tab, bool first = false)
    {
        var btn = new Button
        {
            Text                = text,
            ToggleMode          = true,
            ButtonGroup         = group,
            ButtonPressed       = first,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize   = new Vector2(0, 42),
        };
        UiKit.StyleButton(btn, new Color(0.17f, 0.18f, 0.23f), new Color(0.23f, 0.25f, 0.32f), new Color(0.30f, 0.40f, 0.58f));
        btn.AddThemeFontSizeOverride("font_size", 16);
        btn.Pressed += () => SetTab(tab);
        bar.AddChild(btn);
    }

    private Control MakeWalletPill(bool isMa)
    {
        Color col = isMa ? UiKit.MaThach : UiKit.Gold;

        var pill = new PanelContainer();
        pill.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.Fade(col, 0.16f), 14, col, 1, 12, 6));

        var h = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        h.AddThemeConstantOverride("separation", 6);

        if (!isMa && ResourceLoader.Exists(GoldIconPath))
        {
            var tex = GD.Load<Texture2D>(GoldIconPath);
            if (tex != null)
                h.AddChild(new TextureRect
                {
                    Texture           = tex,
                    CustomMinimumSize = new Vector2(22, 22),
                    StretchMode       = TextureRect.StretchModeEnum.KeepAspectCentered,
                    ExpandMode        = TextureRect.ExpandModeEnum.IgnoreSize,
                });
        }

        var label = new Label { VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", 16);
        label.AddThemeColorOverride("font_color", col);
        h.AddChild(label);

        pill.AddChild(h);
        if (isMa) _maThachLabel = label; else _goldLabel = label;
        return pill;
    }

    private Control MakeSoonBox(string title, string desc)
    {
        var center = new CenterContainer { Visible = false, CustomMinimumSize = new Vector2(0, 416) };
        center.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

        var card = new PanelContainer { CustomMinimumSize = new Vector2(440, 210) };
        card.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.CardBg, 14, UiKit.Accent, 2, 28, 24));

        var v = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        v.AddThemeConstantOverride("separation", 10);

        var t = new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Center };
        t.AddThemeFontSizeOverride("font_size", 24);
        t.AddThemeColorOverride("font_color", UiKit.Accent);

        var soon = new Label { Text = "SẮP RA MẮT", HorizontalAlignment = HorizontalAlignment.Center };
        soon.AddThemeFontSizeOverride("font_size", 14);
        soon.AddThemeColorOverride("font_color", UiKit.TextDim);

        var d = new Label { Text = desc, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        d.AddThemeColorOverride("font_color", UiKit.TextDim);
        d.CustomMinimumSize = new Vector2(380, 0);

        v.AddChild(t);
        v.AddChild(soon);
        v.AddChild(d);
        card.AddChild(v);
        center.AddChild(card);
        return center;
    }

    // ───────────────────────── Tabs / Refresh ─────────────────────────

    private void SetTab(Tab tab)
    {
        _itemScroll.Visible = tab == Tab.Items;
        _soonTopUp.Visible  = tab == Tab.TopUp;
        _soonGacha.Visible  = tab == Tab.Gacha;
        _feedback.Text      = "";
    }

    private void RefreshWallet()
    {
        var w = Wallet.Instance;
        if (_goldLabel    != null) _goldLabel.Text    = $"{(w?.Gold ?? 0):N0}";
        if (_maThachLabel != null) _maThachLabel.Text = $"Ma Thạch {(w?.MaThach ?? 0):N0}";
    }

    private void BuildList()
    {
        if (_grid == null) return;

        foreach (Node child in _grid.GetChildren())
            child.QueueFree();

        var listings = Shop.Instance?.Listings;
        if (listings == null || listings.Count == 0)
        {
            _grid.AddChild(new Label { Text = "Cửa hàng trống." });
            return;
        }

        foreach (var listing in listings)
            _grid.AddChild(MakeCard(listing));
    }

    private Control MakeCard(ShopListing listing)
    {
        var      def    = ItemDatabase.Instance?.Get(listing.ItemId);
        ItemType type   = def?.Type ?? ItemType.Consumable;
        Color    accent = UiKit.TypeColor(type);
        string   id     = listing.ItemId;

        var card = new PanelContainer { CustomMinimumSize = new Vector2(258, 0) };
        card.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.CardBg, 12, accent, 2, 12, 12));

        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 8);
        card.AddChild(v);

        // Badge nhóm
        var badge = new Label { Text = UiKit.TypeName(type), HorizontalAlignment = HorizontalAlignment.Center };
        badge.AddThemeFontSizeOverride("font_size", 11);
        badge.AddThemeColorOverride("font_color", accent);
        v.AddChild(badge);

        // Icon (hoặc placeholder màu nhóm)
        v.AddChild(MakeIcon(def, accent));

        // Tên
        var name = new Label
        {
            Text                = def?.NameVi ?? id,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode        = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize   = new Vector2(0, 42),
        };
        name.AddThemeFontSizeOverride("font_size", 16);
        v.AddChild(name);

        // Đang có
        int owned = Inventory.Instance?.CountOf(id) ?? 0;
        var ownedL = new Label { Text = $"Đang có: {owned}", HorizontalAlignment = HorizontalAlignment.Center };
        ownedL.AddThemeFontSizeOverride("font_size", 12);
        ownedL.AddThemeColorOverride("font_color", UiKit.TextDim);
        v.AddChild(ownedL);

        // Pill giá
        v.AddChild(MakePricePill(listing));

        // Nút mua
        var buy = new Button { Text = "Mua", CustomMinimumSize = new Vector2(0, 38) };
        UiKit.StyleButton(buy, UiKit.BuyGreen, UiKit.BuyGreenHi, new Color(0.16f, 0.42f, 0.24f));
        buy.AddThemeFontSizeOverride("font_size", 15);
        buy.Pressed += () => RequestBuy(id);
        v.AddChild(buy);

        return card;
    }

    private Control MakeIcon(ItemEntry def, Color accent) => UiKit.ItemIcon(def, accent, 64);

    private Control MakePricePill(ShopListing listing)
    {
        bool  isMa = listing.CurrencyType == ShopCurrency.MaThach;
        Color col  = isMa ? UiKit.MaThach : UiKit.Gold;

        var pill = new PanelContainer();
        pill.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.Fade(col, 0.16f), 12, col, 1, 10, 5));

        var h = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        h.AddThemeConstantOverride("separation", 6);

        if (!isMa && ResourceLoader.Exists(GoldIconPath))
        {
            var tex = GD.Load<Texture2D>(GoldIconPath);
            if (tex != null)
                h.AddChild(new TextureRect
                {
                    Texture           = tex,
                    CustomMinimumSize = new Vector2(18, 18),
                    StretchMode       = TextureRect.StretchModeEnum.KeepAspectCentered,
                    ExpandMode        = TextureRect.ExpandModeEnum.IgnoreSize,
                });
        }

        var label = new Label { Text = $"{listing.Price:N0} {(isMa ? "Ma Thạch" : "Vàng")}" };
        label.AddThemeFontSizeOverride("font_size", 15);
        label.AddThemeColorOverride("font_color", col);
        h.AddChild(label);

        pill.AddChild(h);

        var wrap = new CenterContainer();
        wrap.AddChild(pill);
        return wrap;
    }

    // ───────────────────────── Buy flow ─────────────────────────

    private void RequestBuy(string itemId)
    {
        var listing = Shop.Instance?.GetListing(itemId);
        var def     = ItemDatabase.Instance?.Get(itemId);
        if (listing == null || def == null) return;

        _pendingItemId      = itemId;
        _confirm.DialogText = $"Mua {def.NameVi} với giá {listing.Price:N0} {(listing.CurrencyType == ShopCurrency.MaThach ? "Ma Thạch" : "Vàng")}?";
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
        // Buy thành công → Wallet/Inventory phát Changed → RefreshWallet + BuildList tự chạy.
    }

    private void ShowFeedback(string msg, bool ok)
    {
        if (_feedback == null) return;
        _feedback.Text = msg;
        _feedback.AddThemeColorOverride("font_color", ok ? new Color(0.45f, 0.90f, 0.50f) : new Color(1f, 0.50f, 0.50f));
    }

    // ───────────────────────── Input action ─────────────────────────

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
