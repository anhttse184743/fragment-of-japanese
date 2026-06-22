using Godot;
using FragmentOfJapanese.Autoloads;
using FragmentOfJapanese.Cosmetics;
using FragmentOfJapanese.Core;
using FragmentOfJapanese.Items;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// UI túi đồ — autoload dựng bằng code, tông nâu da. Mở/đóng bằng phím I (Esc để đóng).
/// Bố cục: thanh trên = tab phân loại + tiền tệ (Vàng + Aetherstone) + nút X.
/// Bên trái = lưới ô vật phẩm (ảnh item + số lượng ở góc). Bên phải = bảng thông tin
/// (ảnh + tên + mô tả + nút "Dùng" chỉ hiện với vật phẩm dùng được).
/// </summary>
public partial class InventoryUi : CanvasLayer
{
    public static InventoryUi Instance { get; private set; }

    private const int Columns = 5;

    private Control         _root;
    private Label           _goldLabel;
    private Label           _aetherLabel;
    private ScrollContainer _itemScroll;
    private GridContainer   _grid;
    private CenterContainer _detailImage;
    private Label           _detailName;
    private Label           _detailMeta;
    private Label           _detailDesc;
    private Button          _useButton;

    private ItemType _currentTab = ItemType.Consumable;
    private string   _selectedId = "";
    private Button   _selectedSlot;

    // Tab Skin (ngoài 4 tab vật phẩm)
    private bool          _skinsMode;
    private SkinCategory  _skinCategory = SkinCategory.Player;
    private string        _selectedSkinId = "";
    private HBoxContainer _skinCatBar;

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

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(900, 600) };
        panel.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.BrownPanel, 14, UiKit.BrownBorder, 3));
        center.AddChild(panel);

        var outer = new MarginContainer();
        outer.AddThemeConstantOverride("margin_left",   16);
        outer.AddThemeConstantOverride("margin_right",  16);
        outer.AddThemeConstantOverride("margin_top",    14);
        outer.AddThemeConstantOverride("margin_bottom", 14);
        panel.AddChild(outer);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 10);
        outer.AddChild(vbox);

        // ===== Thanh trên: tab + tiền tệ + X =====
        var topRow = new HBoxContainer();
        topRow.AddThemeConstantOverride("separation", 10);
        vbox.AddChild(topRow);

        var group = new ButtonGroup();
        AddTab(topRow, group, "Tiêu hao",  ItemType.Consumable, first: true);
        AddTab(topRow, group, "Chìa khóa", ItemType.Key);
        AddTab(topRow, group, "Đổi/Bán",   ItemType.Trade);
        AddTab(topRow, group, "Trang bị",  ItemType.Equipment);
        AddSkinTab(topRow, group, "Skin");

        topRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        topRow.AddChild(MakeCurrency(UiKit.GoldIconPath,   out _goldLabel));
        topRow.AddChild(MakeCurrency(UiKit.AetherIconPath, out _aetherLabel));
        topRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        var xBtn = new Button { Text = "X", CustomMinimumSize = new Vector2(42, 42) };
        UiKit.StyleButton(xBtn, UiKit.BrownDark, new Color(0.55f, 0.25f, 0.22f), new Color(0.40f, 0.18f, 0.16f));
        xBtn.AddThemeFontSizeOverride("font_size", 18);
        xBtn.Pressed += Close;
        topRow.AddChild(xBtn);

        vbox.AddChild(new HSeparator());

        // ===== Thân: lưới (trái) + chi tiết (phải) =====
        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", 12);
        body.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        vbox.AddChild(body);

        var leftCol = new VBoxContainer();
        leftCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        leftCol.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        leftCol.AddThemeConstantOverride("separation", 8);
        body.AddChild(leftCol);

        _skinCatBar = BuildSkinCatBar();
        _skinCatBar.Visible = false;     // chỉ hiện ở tab Skin
        leftCol.AddChild(_skinCatBar);

        _itemScroll = new ScrollContainer();
        _itemScroll.SizeFlagsHorizontal   = Control.SizeFlags.ExpandFill;
        _itemScroll.SizeFlagsVertical     = Control.SizeFlags.ExpandFill;
        _itemScroll.HorizontalScrollMode  = ScrollContainer.ScrollMode.Disabled;
        leftCol.AddChild(_itemScroll);

        _grid = new GridContainer { Columns = Columns };
        _grid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _grid.AddThemeConstantOverride("h_separation", 8);
        _grid.AddThemeConstantOverride("v_separation", 8);
        _itemScroll.AddChild(_grid);

        body.AddChild(BuildDetailPanel());
    }

    private Control BuildDetailPanel()
    {
        var detail = new PanelContainer { CustomMinimumSize = new Vector2(280, 0) };
        detail.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        detail.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.BrownDark, 12, UiKit.BrownBorder, 2, 14, 14));

        var dv = new VBoxContainer();
        dv.AddThemeConstantOverride("separation", 8);
        detail.AddChild(dv);

        _detailImage = new CenterContainer { CustomMinimumSize = new Vector2(0, 150) };
        dv.AddChild(_detailImage);

        _detailName = new Label { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _detailName.AddThemeFontSizeOverride("font_size", 20);
        _detailName.AddThemeColorOverride("font_color", UiKit.BrownText);
        dv.AddChild(_detailName);

        _detailMeta = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _detailMeta.AddThemeFontSizeOverride("font_size", 13);
        _detailMeta.AddThemeColorOverride("font_color", UiKit.Accent);
        dv.AddChild(_detailMeta);

        _detailDesc = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _detailDesc.AddThemeColorOverride("font_color", UiKit.BrownTextDim);
        dv.AddChild(_detailDesc);

        dv.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });   // đẩy nút xuống đáy

        _useButton = new Button { Text = "Dùng", CustomMinimumSize = new Vector2(0, 44), Visible = false };
        UiKit.StyleButton(_useButton, UiKit.BuyGreen, UiKit.BuyGreenHi, new Color(0.16f, 0.42f, 0.24f));
        _useButton.AddThemeFontSizeOverride("font_size", 18);
        _useButton.Pressed += OnUsePressed;
        dv.AddChild(_useButton);

        return detail;
    }

    private void AddTab(HBoxContainer bar, ButtonGroup group, string text, ItemType type, bool first = false)
    {
        var btn = new Button
        {
            Text              = text,
            ToggleMode        = true,
            ButtonGroup       = group,
            ButtonPressed     = first,
            CustomMinimumSize = new Vector2(88, 38),
        };
        UiKit.StyleButton(btn, UiKit.BrownDark, new Color(0.50f, 0.37f, 0.24f), UiKit.Accent);
        btn.AddThemeColorOverride("font_color",         UiKit.BrownText);
        btn.AddThemeColorOverride("font_hover_color",   UiKit.BrownText);
        btn.AddThemeColorOverride("font_pressed_color", new Color(0.22f, 0.14f, 0.07f));   // chữ tối trên nền vàng (tab đang chọn)
        btn.AddThemeFontSizeOverride("font_size", 14);
        btn.Pressed += () => SetTab(type);
        bar.AddChild(btn);
    }

    private HBoxContainer MakeCurrency(string iconPath, out Label label)
    {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 6);

        if (ResourceLoader.Exists(iconPath))
        {
            var tex = GD.Load<Texture2D>(iconPath);
            if (tex != null)
                h.AddChild(new TextureRect
                {
                    Texture           = tex,
                    CustomMinimumSize = new Vector2(30, 30),
                    StretchMode       = TextureRect.StretchModeEnum.KeepAspectCentered,
                    ExpandMode        = TextureRect.ExpandModeEnum.IgnoreSize,
                });
        }

        label = new Label { VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", 18);
        label.AddThemeColorOverride("font_color", UiKit.BrownText);
        h.AddChild(label);
        return h;
    }

    // ───────────────────────── Tabs / Refresh ─────────────────────────

    private void SetTab(ItemType type)
    {
        _skinsMode    = false;
        if (_skinCatBar != null) _skinCatBar.Visible = false;
        _currentTab   = type;
        _selectedId   = "";
        _selectedSlot = null;
        Refresh();
    }

    private void RefreshWallet()
    {
        var w = Wallet.Instance;
        if (_goldLabel   != null) _goldLabel.Text   = $"{(w?.Gold ?? 0):N0}";
        if (_aetherLabel != null) _aetherLabel.Text = $"{(w?.MaThach ?? 0):N0}";
    }

    private void Refresh()
    {
        if (_grid == null) return;
        if (_skinsMode) { RefreshSkins(); return; }

        foreach (Node child in _grid.GetChildren())
            child.QueueFree();
        _selectedSlot = null;

        int  shown    = 0;
        bool selAlive = false;

        var stacks = Inventory.Instance?.Stacks;
        if (stacks != null)
        {
            foreach (var stack in stacks)
            {
                if (stack.Item.Type != _currentTab) continue;

                bool sel  = stack.Item.Id == _selectedId;
                var  slot = MakeSlot(stack, sel);
                if (sel) { _selectedSlot = slot; selAlive = true; }
                _grid.AddChild(slot);
                shown++;
            }
        }

        if (shown == 0)
        {
            var empty = new Label { Text = "Không có vật phẩm trong nhóm này." };
            empty.AddThemeColorOverride("font_color", UiKit.BrownTextDim);
            _grid.AddChild(empty);
        }

        if (selAlive) UpdateDetail(_selectedId);
        else          ClearSelection();
    }

    private Button MakeSlot(ItemStack stack, bool selected)
    {
        var item = stack.Item;

        var btn = new Button
        {
            CustomMinimumSize   = new Vector2(96, 96),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText         = item.NameVi,
        };
        ApplySlotStyle(btn, selected);

        // Ảnh item — căn giữa, phủ toàn ô
        var icon = UiKit.ItemIcon(item, UiKit.TypeColor(item.Type), 60);
        icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        // Số lượng — góc dưới phải
        var overlay = new VBoxContainer();
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });

        var bottom = new HBoxContainer();
        bottom.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        var qtyPill = new PanelContainer();
        qtyPill.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.12f, 0.08f, 0.04f, 0.82f), 6, null, 0, 6, 1));
        var qty = new Label { Text = $"{stack.Count}" };
        qty.AddThemeFontSizeOverride("font_size", 14);
        qty.AddThemeColorOverride("font_color", Colors.White);
        qtyPill.AddChild(qty);
        bottom.AddChild(qtyPill);
        overlay.AddChild(bottom);

        btn.AddChild(icon);
        btn.AddChild(overlay);

        IgnoreMouse(icon);
        IgnoreMouse(overlay);

        btn.Pressed += () => OnSlotSelected(item.Id, btn);
        return btn;
    }

    private static void ApplySlotStyle(Button btn, bool selected)
    {
        Color border = selected ? UiKit.Accent : UiKit.BrownBorder;
        int   bw     = selected ? 3 : 2;
        Color bg     = selected ? UiKit.Fade(UiKit.Accent, 0.22f) : UiKit.BrownSlot;

        btn.AddThemeStyleboxOverride("normal",  UiKit.Box(bg, 8, border, bw));
        btn.AddThemeStyleboxOverride("hover",   UiKit.Box(UiKit.Fade(UiKit.Accent, 0.12f), 8, border, bw));
        btn.AddThemeStyleboxOverride("pressed", UiKit.Box(UiKit.Fade(UiKit.Accent, 0.22f), 8, border, bw));
        btn.AddThemeStyleboxOverride("focus",   UiKit.Box(new Color(0, 0, 0, 0), 8));
    }

    private static void IgnoreMouse(Node node)
    {
        if (node is Control c) c.MouseFilter = Control.MouseFilterEnum.Ignore;
        foreach (var child in node.GetChildren())
            IgnoreMouse(child);
    }

    // ───────────────────────── Selection / Detail ─────────────────────────

    private void OnSlotSelected(string id, Button slot)
    {
        if (_selectedSlot != null && GodotObject.IsInstanceValid(_selectedSlot))
            ApplySlotStyle(_selectedSlot, false);

        _selectedId   = id;
        _selectedSlot = slot;
        ApplySlotStyle(slot, true);

        UpdateDetail(id);
    }

    private void UpdateDetail(string itemId)
    {
        var def = ItemDatabase.Instance?.Get(itemId);
        if (def == null) { ClearSelection(); return; }

        int count = Inventory.Instance?.CountOf(itemId) ?? 0;

        foreach (Node c in _detailImage.GetChildren()) c.QueueFree();
        _detailImage.AddChild(UiKit.ItemIcon(def, UiKit.TypeColor(def.Type), 132));

        _detailName.Text = def.NameVi;
        _detailMeta.Text = $"{UiKit.TypeName(def.Type)}  •  Số lượng: {count}";
        _detailDesc.Text = string.IsNullOrEmpty(def.DescriptionVi) ? "(không có mô tả)" : def.DescriptionVi;

        // Chỉ vật phẩm tiêu hao (có effect) mới hiện nút Dùng
        _useButton.Text     = "Dùng";
        _useButton.Disabled = false;
        _useButton.Visible  = def.Type == ItemType.Consumable && !string.IsNullOrEmpty(def.Effect) && count > 0;
    }

    private void ClearSelection()
    {
        _selectedId = "";
        if (_detailImage != null)
            foreach (Node c in _detailImage.GetChildren()) c.QueueFree();
        if (_detailName != null) _detailName.Text = "";
        if (_detailMeta != null) _detailMeta.Text = "";
        if (_detailDesc != null) _detailDesc.Text = "Chọn một vật phẩm để xem thông tin.";
        if (_useButton  != null) _useButton.Visible = false;
    }

    private void OnUsePressed()
    {
        if (_skinsMode)
        {
            if (!string.IsNullOrEmpty(_selectedSkinId))
            {
                SkinManager.Instance?.Equip(_skinCategory, _selectedSkinId);
                RefreshSkins();
            }
            return;
        }

        if (!string.IsNullOrEmpty(_selectedId))
            Inventory.Instance?.UseItem(_selectedId);   // Changed → Refresh tự chạy
    }

    // ───────────────────────── Tab Skin ─────────────────────────

    private void AddSkinTab(HBoxContainer bar, ButtonGroup group, string text)
    {
        var btn = new Button
        {
            Text              = text,
            ToggleMode        = true,
            ButtonGroup       = group,
            CustomMinimumSize = new Vector2(88, 38),
        };
        UiKit.StyleButton(btn, UiKit.BrownDark, new Color(0.50f, 0.37f, 0.24f), UiKit.Accent);
        btn.AddThemeColorOverride("font_color",         UiKit.BrownText);
        btn.AddThemeColorOverride("font_hover_color",   UiKit.BrownText);
        btn.AddThemeColorOverride("font_pressed_color", new Color(0.22f, 0.14f, 0.07f));
        btn.AddThemeFontSizeOverride("font_size", 14);
        btn.Pressed += EnterSkinsMode;
        bar.AddChild(btn);
    }

    private HBoxContainer BuildSkinCatBar()
    {
        var bar = new HBoxContainer();
        bar.AddThemeConstantOverride("separation", 6);

        var group = new ButtonGroup();
        bool first = true;
        foreach (SkinCategory cat in System.Enum.GetValues<SkinCategory>())
        {
            var c = cat;
            var btn = new Button
            {
                Text                = SkinCatName(cat),
                ToggleMode          = true,
                ButtonGroup         = group,
                ButtonPressed       = first,
                CustomMinimumSize   = new Vector2(0, 32),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            UiKit.StyleButton(btn, UiKit.BrownSlot, new Color(0.50f, 0.37f, 0.24f), UiKit.Accent);
            btn.AddThemeColorOverride("font_color",         UiKit.BrownText);
            btn.AddThemeColorOverride("font_pressed_color", new Color(0.22f, 0.14f, 0.07f));
            btn.AddThemeFontSizeOverride("font_size", 13);
            btn.Pressed += () =>
            {
                _skinCategory   = c;
                _selectedSkinId = SkinManager.Instance?.GetEquippedId(c) ?? "";
                RefreshSkins();
            };
            bar.AddChild(btn);
            first = false;
        }
        return bar;
    }

    private void EnterSkinsMode()
    {
        _skinsMode      = true;
        _selectedId     = "";
        _selectedSlot   = null;
        _selectedSkinId = SkinManager.Instance?.GetEquippedId(_skinCategory) ?? "";
        if (_skinCatBar != null) _skinCatBar.Visible = true;
        RefreshSkins();
    }

    private void RefreshSkins()
    {
        if (_grid == null) return;
        foreach (Node child in _grid.GetChildren()) child.QueueFree();
        _selectedSlot = null;

        var mgr = SkinManager.Instance;
        if (mgr == null) return;
        string equipped = mgr.GetEquippedId(_skinCategory);

        bool selAlive = false;
        foreach (var skin in mgr.Catalog(_skinCategory))
        {
            bool sel  = skin.Id == _selectedSkinId;
            var  slot = MakeSkinSlot(skin, sel, skin.Id == equipped);
            if (sel) { _selectedSlot = slot; selAlive = true; }
            _grid.AddChild(slot);
        }

        if (selAlive) UpdateSkinDetail(_selectedSkinId);
        else          ClearSelection();
    }

    private Button MakeSkinSlot(SkinDef skin, bool selected, bool equipped)
    {
        var btn = new Button
        {
            CustomMinimumSize   = new Vector2(96, 96),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText         = skin.Name,
        };
        ApplySlotStyle(btn, selected);

        var v = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        v.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        v.AddThemeConstantOverride("separation", 4);

        var swHolder = new CenterContainer();
        var swatch   = new PanelContainer { CustomMinimumSize = new Vector2(54, 40) };
        swatch.AddThemeStyleboxOverride("panel", UiKit.Box(skin.ColorValue, 8, UiKit.BrownBorder, 2));
        swHolder.AddChild(swatch);
        v.AddChild(swHolder);

        var name = new Label { Text = skin.Name, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        name.AddThemeFontSizeOverride("font_size", 12);
        name.AddThemeColorOverride("font_color", UiKit.BrownText);
        v.AddChild(name);

        btn.AddChild(v);
        IgnoreMouse(v);

        if (equipped)
        {
            var tag = new Label { Text = "✓", HorizontalAlignment = HorizontalAlignment.Right, MouseFilter = Control.MouseFilterEnum.Ignore };
            tag.SetAnchorsPreset(Control.LayoutPreset.TopWide);
            tag.OffsetTop   = 2;
            tag.OffsetRight = -6;
            tag.AddThemeFontSizeOverride("font_size", 16);
            tag.AddThemeColorOverride("font_color", new Color(0.55f, 0.95f, 0.55f));
            btn.AddChild(tag);
        }

        btn.Pressed += () => OnSkinSelected(skin.Id, btn);
        return btn;
    }

    private void OnSkinSelected(string id, Button slot)
    {
        if (_selectedSlot != null && GodotObject.IsInstanceValid(_selectedSlot))
            ApplySlotStyle(_selectedSlot, false);

        _selectedSkinId = id;
        _selectedSlot   = slot;
        ApplySlotStyle(slot, true);
        UpdateSkinDetail(id);
    }

    private void UpdateSkinDetail(string id)
    {
        var mgr  = SkinManager.Instance;
        var skin = mgr?.Get(id);
        if (skin == null) { ClearSelection(); return; }

        bool equipped = mgr.GetEquippedId(_skinCategory) == id;

        foreach (Node c in _detailImage.GetChildren()) c.QueueFree();
        var swatch = new PanelContainer { CustomMinimumSize = new Vector2(132, 96) };
        swatch.AddThemeStyleboxOverride("panel", UiKit.Box(skin.ColorValue, 12, UiKit.BrownBorder, 2));
        _detailImage.AddChild(swatch);

        _detailName.Text = skin.Name;
        _detailMeta.Text = SkinCatName(_skinCategory) + (equipped ? "  •  Đang dùng" : "");
        _detailDesc.Text = $"Skin {SkinCatName(_skinCategory).ToLower()}.";

        _useButton.Text     = equipped ? "Đang dùng" : "Trang bị";
        _useButton.Disabled = equipped;
        _useButton.Visible  = true;
    }

    private static string SkinCatName(SkinCategory c) => c switch
    {
        SkinCategory.Player      => "Nhân vật",
        SkinCategory.AttackSwing => "Đòn đánh",
        SkinCategory.RunDust     => "Bụi chạy",
        SkinCategory.HitSpark    => "Tia trúng",
        SkinCategory.LevelUpAura => "Hào quang",
        SkinCategory.Footstep    => "Vệt chân",
        _                        => "",
    };

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
