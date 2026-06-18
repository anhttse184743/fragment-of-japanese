using Godot;
using FragmentOfJapanese.Autoloads;
using FragmentOfJapanese.Core;
using FragmentOfJapanese.Items;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// UI túi đồ — autoload dựng bằng code. Mở/đóng bằng phím I (Esc để đóng).
/// Cùng phong cách với cửa hàng: thanh tiền tệ, tab phân loại, lưới card có icon +
/// viền màu theo nhóm, card đang chọn được làm nổi, bảng chi tiết + nút hành động.
///   - Tiêu hao  → "Dùng" (Inventory.UseItem)
///   - Đổi/Bán   → "Bán" (Inventory.Remove + Wallet.AddGold)
///   - Chìa khóa → "Quay Gacha" (sắp ra mắt) ; Trang bị → tab có placeholder "sắp ra mắt"
/// </summary>
public partial class InventoryUi : CanvasLayer
{
    public static InventoryUi Instance { get; private set; }

    private const int Columns = 4;

    private Control       _root;
    private Label         _goldLabel;
    private Label         _maThachLabel;
    private ScrollContainer _itemScroll;
    private GridContainer _grid;
    private Control       _equipBanner;
    private Label         _detailName;
    private Label         _detailDesc;
    private Button        _actionButton;

    private ItemType _currentTab = ItemType.Consumable;
    private string   _selectedId = "";
    private Button   _selectedCard;
    private Color    _selectedAccent;

    public override void _Ready()
    {
        Instance = this;
        Layer    = 10;

        EnsureInputAction();
        BuildUi();
        _root.Visible = false;

        if (Inventory.Instance != null) Inventory.Instance.Changed += Refresh;
        if (Wallet.Instance    != null) Wallet.Instance.Changed    += RefreshWallet;

        RefreshWallet();
        Refresh();
    }

    public override void _UnhandledInput(InputEvent ev)
    {
        if (ev.IsActionPressed("toggle_inventory"))
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
    public void Open()   { _root.Visible = true; RefreshWallet(); Refresh(); }
    public void Close()  { _root.Visible = false; }

    // ───────────────────────── Build UI ─────────────────────────

    private void BuildUi()
    {
        _root = new Control { Name = "InventoryRoot" };
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

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(820, 640) };
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

        var title = new Label { Text = "TÚI ĐỒ", VerticalAlignment = VerticalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 28);
        title.AddThemeColorOverride("font_color", UiKit.Accent);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        headerRow.AddChild(title);

        headerRow.AddChild(MakeWalletPill(false));
        headerRow.AddChild(MakeWalletPill(true));

        var closeBtn = new Button { Text = "Đóng", CustomMinimumSize = new Vector2(84, 38) };
        UiKit.StyleButton(closeBtn, new Color(0.42f, 0.20f, 0.22f), new Color(0.60f, 0.26f, 0.28f), new Color(0.36f, 0.16f, 0.18f));
        closeBtn.Pressed += Close;
        headerRow.AddChild(closeBtn);

        // ---- Tabs ----
        var tabBar = new HBoxContainer();
        tabBar.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(tabBar);

        var group = new ButtonGroup();
        AddTab(tabBar, group, "Tiêu hao",  ItemType.Consumable, first: true);
        AddTab(tabBar, group, "Chìa khóa", ItemType.Key);
        AddTab(tabBar, group, "Đổi / Bán", ItemType.Trade);
        AddTab(tabBar, group, "Trang bị",  ItemType.Equipment);

        // ---- Banner trang bị (chỉ tab Equipment) ----
        _equipBanner = BuildEquipBanner();
        vbox.AddChild(_equipBanner);

        // ---- Lưới card ----
        _itemScroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 300) };
        _itemScroll.SizeFlagsVertical    = Control.SizeFlags.ExpandFill;
        _itemScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        vbox.AddChild(_itemScroll);

        _grid = new GridContainer { Columns = Columns };
        _grid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _grid.AddThemeConstantOverride("h_separation", 12);
        _grid.AddThemeConstantOverride("v_separation", 12);
        _itemScroll.AddChild(_grid);

        // ---- Chi tiết ----
        var detail = new PanelContainer();
        detail.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.HeaderBg, 12, null, 0, 14, 12));
        vbox.AddChild(detail);

        var dv = new VBoxContainer();
        dv.AddThemeConstantOverride("separation", 6);
        detail.AddChild(dv);

        _detailName = new Label();
        _detailName.AddThemeFontSizeOverride("font_size", 20);
        _detailName.AddThemeColorOverride("font_color", UiKit.Accent);
        dv.AddChild(_detailName);

        _detailDesc = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _detailDesc.AddThemeColorOverride("font_color", UiKit.TextDim);
        _detailDesc.CustomMinimumSize = new Vector2(0, 40);
        dv.AddChild(_detailDesc);

        _actionButton = new Button { CustomMinimumSize = new Vector2(220, 40), Disabled = true };
        _actionButton.Pressed += OnActionPressed;
        dv.AddChild(_actionButton);
    }

    private void AddTab(HBoxContainer bar, ButtonGroup group, string text, ItemType type, bool first = false)
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
        btn.Pressed += () => SetTab(type);
        bar.AddChild(btn);
    }

    private Control MakeWalletPill(bool isMa)
    {
        Color col = isMa ? UiKit.MaThach : UiKit.Gold;

        var pill = new PanelContainer();
        pill.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.Fade(col, 0.16f), 14, col, 1, 12, 6));

        var h = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        h.AddThemeConstantOverride("separation", 6);

        if (!isMa && ResourceLoader.Exists(UiKit.GoldIconPath))
        {
            var tex = GD.Load<Texture2D>(UiKit.GoldIconPath);
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

    private Control BuildEquipBanner()
    {
        var wrap = new VBoxContainer { Visible = false };

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.CardBg, 12, UiKit.TypeColor(ItemType.Equipment), 2, 16, 12));

        var v = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        v.AddThemeConstantOverride("separation", 8);

        var t = new Label { Text = "Hệ thống Trang bị — SẮP RA MẮT", HorizontalAlignment = HorizontalAlignment.Center };
        t.AddThemeFontSizeOverride("font_size", 18);
        t.AddThemeColorOverride("font_color", UiKit.TypeColor(ItemType.Equipment));
        v.AddChild(t);

        var slots = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        slots.AddThemeConstantOverride("separation", 12);
        foreach (var n in new[] { "Vũ khí", "Giáp", "Phụ kiện" })
        {
            var s = new Button { Text = n, Disabled = true, CustomMinimumSize = new Vector2(120, 56) };
            UiKit.StyleButton(s, UiKit.CardBg, UiKit.CardBg, UiKit.CardBg);
            slots.AddChild(s);
        }
        v.AddChild(slots);

        card.AddChild(v);
        wrap.AddChild(card);
        return wrap;
    }

    // ───────────────────────── Tabs / Refresh ─────────────────────────

    private void SetTab(ItemType type)
    {
        _currentTab          = type;
        _selectedId          = "";
        _selectedCard        = null;
        _equipBanner.Visible = type == ItemType.Equipment;
        Refresh();
    }

    private void RefreshWallet()
    {
        var w = Wallet.Instance;
        if (_goldLabel    != null) _goldLabel.Text    = $"{(w?.Gold ?? 0):N0}";
        if (_maThachLabel != null) _maThachLabel.Text = $"Ma Thạch {(w?.MaThach ?? 0):N0}";
    }

    private void Refresh()
    {
        if (_grid == null) return;

        foreach (Node child in _grid.GetChildren())
            child.QueueFree();
        _selectedCard = null;

        int  shown    = 0;
        bool selAlive = false;

        var stacks = Inventory.Instance?.Stacks;
        if (stacks != null)
        {
            foreach (var stack in stacks)
            {
                if (stack.Item.Type != _currentTab) continue;

                Color accent = UiKit.TypeColor(stack.Item.Type);
                bool  sel    = stack.Item.Id == _selectedId;
                var   card   = MakeSlotCard(stack, accent, sel);
                if (sel) { _selectedCard = card; _selectedAccent = accent; selAlive = true; }
                _grid.AddChild(card);
                shown++;
            }
        }

        if (shown == 0)
        {
            var empty = new Label { Text = "Không có vật phẩm trong nhóm này." };
            empty.AddThemeColorOverride("font_color", UiKit.TextDim);
            _grid.AddChild(empty);
        }

        if (selAlive) UpdateDetail(_selectedId);
        else          ClearSelection();
    }

    private Button MakeSlotCard(ItemStack stack, Color accent, bool selected)
    {
        var item = stack.Item;

        var btn = new Button { CustomMinimumSize = new Vector2(176, 140), TooltipText = item.NameVi };
        ApplyCardStyle(btn, accent, selected);

        var v = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        v.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        v.AddThemeConstantOverride("separation", 4);

        var badge = new Label { Text = UiKit.TypeName(item.Type), HorizontalAlignment = HorizontalAlignment.Center };
        badge.AddThemeFontSizeOverride("font_size", 10);
        badge.AddThemeColorOverride("font_color", accent);
        v.AddChild(badge);

        v.AddChild(UiKit.ItemIcon(item, accent, 48));

        var name = new Label { Text = item.NameVi, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        name.AddThemeFontSizeOverride("font_size", 13);
        v.AddChild(name);

        var count = new Label { Text = $"×{stack.Count}", HorizontalAlignment = HorizontalAlignment.Center };
        count.AddThemeFontSizeOverride("font_size", 14);
        count.AddThemeColorOverride("font_color", UiKit.TextDim);
        v.AddChild(count);

        btn.AddChild(v);
        IgnoreMouse(v);                          // để click rơi xuống Button

        btn.Pressed += () => OnSlotSelected(item.Id, btn, accent);
        return btn;
    }

    private static void ApplyCardStyle(Button btn, Color accent, bool selected)
    {
        Color border = selected ? Colors.White : accent;
        int   bw     = selected ? 3 : 2;
        Color bg     = selected ? UiKit.Fade(accent, 0.20f) : UiKit.CardBg;

        btn.AddThemeStyleboxOverride("normal",  UiKit.Box(bg, 10, border, bw, 8, 8));
        btn.AddThemeStyleboxOverride("hover",   UiKit.Box(UiKit.Fade(accent, 0.12f), 10, border, bw, 8, 8));
        btn.AddThemeStyleboxOverride("pressed", UiKit.Box(UiKit.Fade(accent, 0.20f), 10, border, bw, 8, 8));
        btn.AddThemeStyleboxOverride("focus",   UiKit.Box(new Color(0, 0, 0, 0), 10));
    }

    private static void IgnoreMouse(Node node)
    {
        if (node is Control c) c.MouseFilter = Control.MouseFilterEnum.Ignore;
        foreach (var child in node.GetChildren())
            IgnoreMouse(child);
    }

    // ───────────────────────── Selection / Detail ─────────────────────────

    private void OnSlotSelected(string id, Button card, Color accent)
    {
        if (_selectedCard != null && GodotObject.IsInstanceValid(_selectedCard))
            ApplyCardStyle(_selectedCard, _selectedAccent, false);

        _selectedId     = id;
        _selectedCard   = card;
        _selectedAccent = accent;
        ApplyCardStyle(card, accent, true);

        UpdateDetail(id);
    }

    private void UpdateDetail(string itemId)
    {
        var def = ItemDatabase.Instance?.Get(itemId);
        if (def == null) { ClearSelection(); return; }

        int count = Inventory.Instance?.CountOf(itemId) ?? 0;
        _detailName.Text = $"{def.NameVi}   ×{count}";
        _detailDesc.Text = string.IsNullOrEmpty(def.DescriptionVi) ? "(không có mô tả)" : def.DescriptionVi;

        switch (def.Type)
        {
            case ItemType.Consumable:
                _actionButton.Text     = "Dùng";
                _actionButton.Disabled = string.IsNullOrEmpty(def.Effect) || count <= 0;
                UiKit.StyleButton(_actionButton, UiKit.BuyGreen, UiKit.BuyGreenHi, new Color(0.16f, 0.42f, 0.24f));
                break;
            case ItemType.Trade:
                _actionButton.Text     = $"Bán ({def.Price:N0} Vàng)";
                _actionButton.Disabled = count <= 0 || def.Price <= 0;
                UiKit.StyleButton(_actionButton, new Color(0.55f, 0.45f, 0.20f), new Color(0.68f, 0.55f, 0.24f), new Color(0.45f, 0.36f, 0.16f));
                break;
            case ItemType.Key:
                _actionButton.Text     = "Quay Gacha (sắp ra mắt)";
                _actionButton.Disabled = true;
                UiKit.StyleButton(_actionButton, UiKit.CardBg, UiKit.CardBg, UiKit.CardBg);
                break;
            case ItemType.Equipment:
                _actionButton.Text     = "Trang bị (sắp ra mắt)";
                _actionButton.Disabled = true;
                UiKit.StyleButton(_actionButton, UiKit.CardBg, UiKit.CardBg, UiKit.CardBg);
                break;
        }
    }

    private void ClearSelection()
    {
        _selectedId = "";
        if (_detailName != null) _detailName.Text = "";
        if (_detailDesc != null) _detailDesc.Text = "Chọn một vật phẩm để xem chi tiết.";
        if (_actionButton != null)
        {
            _actionButton.Text     = "—";
            _actionButton.Disabled = true;
            UiKit.StyleButton(_actionButton, UiKit.CardBg, UiKit.CardBg, UiKit.CardBg);
        }
    }

    private void OnActionPressed()
    {
        if (string.IsNullOrEmpty(_selectedId)) return;
        var def = ItemDatabase.Instance?.Get(_selectedId);
        if (def == null) return;

        switch (def.Type)
        {
            case ItemType.Consumable:
                Inventory.Instance?.UseItem(_selectedId);
                break;

            case ItemType.Trade:
                if (def.Price > 0 && Inventory.Instance != null && Inventory.Instance.Remove(_selectedId, 1))
                {
                    Wallet.Instance?.AddGold(def.Price);
                    GD.Print($"[InventoryUi] Bán '{def.NameVi}' +{def.Price} Vàng.");
                }
                break;

            // Key / Equipment: nút đang disabled (sắp ra mắt) → không xử lý.
        }
        // Inventory/Wallet phát Changed → Refresh + RefreshWallet tự chạy.
    }

    // ───────────────────────── Input action ─────────────────────────

    private static void EnsureInputAction()
    {
        if (!InputMap.HasAction("toggle_inventory"))
            InputMap.AddAction("toggle_inventory");

        foreach (var e in InputMap.ActionGetEvents("toggle_inventory"))
            if (e is InputEventKey k && k.PhysicalKeycode == Key.I)
                return;

        InputMap.ActionAddEvent("toggle_inventory", new InputEventKey { PhysicalKeycode = Key.I });
    }
}
