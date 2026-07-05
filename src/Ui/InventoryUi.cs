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
    private enum SkinTab { Player, AttackSwing, HitSpark, LevelUpAura, Movement }
    private SkinTab       _currentSkinTab = SkinTab.Player;
    private string        _selectedSkinId = "";
    public string         SelectedSkinId => _selectedSkinId;
    private HBoxContainer _skinCatBar;

    private SkinDropSlot _dropRunDust;
    private SkinDropSlot _dropFootstep;
    private HBoxContainer _multiEquipBox;

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

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(1100, 680) };
        var mainBox = new StyleBoxFlat
        {
            BgColor = new Color(0.14f, 0.15f, 0.18f, 0.98f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            BorderColor = new Color(0.28f, 0.30f, 0.35f, 1f),
            CornerRadiusTopLeft = 20, CornerRadiusTopRight = 20, CornerRadiusBottomRight = 20, CornerRadiusBottomLeft = 20,
            ShadowColor = new Color(0, 0, 0, 0.5f), ShadowSize = 12
        };
        panel.AddThemeStyleboxOverride("panel", mainBox);
        center.AddChild(panel);

        var outer = new MarginContainer();
        outer.AddThemeConstantOverride("margin_left",   20);
        outer.AddThemeConstantOverride("margin_right",  20);
        outer.AddThemeConstantOverride("margin_top",    20);
        outer.AddThemeConstantOverride("margin_bottom", 20);
        panel.AddChild(outer);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 16);
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

        var xBtn = new Button { Text = "X", CustomMinimumSize = new Vector2(54, 54) };
        var xNormal = new StyleBoxFlat { BgColor = new Color(0.75f, 0.25f, 0.25f, 0.9f), CornerRadiusTopLeft = 16, CornerRadiusTopRight = 16, CornerRadiusBottomRight = 16, CornerRadiusBottomLeft = 16 };
        var xHover = new StyleBoxFlat { BgColor = new Color(0.85f, 0.35f, 0.35f, 1f), CornerRadiusTopLeft = 16, CornerRadiusTopRight = 16, CornerRadiusBottomRight = 16, CornerRadiusBottomLeft = 16 };
        var xPressed = new StyleBoxFlat { BgColor = new Color(0.55f, 0.15f, 0.15f, 1f), CornerRadiusTopLeft = 16, CornerRadiusTopRight = 16, CornerRadiusBottomRight = 16, CornerRadiusBottomLeft = 16 };
        xBtn.AddThemeStyleboxOverride("normal", xNormal);
        xBtn.AddThemeStyleboxOverride("hover", xHover);
        xBtn.AddThemeStyleboxOverride("pressed", xPressed);
        xBtn.AddThemeStyleboxOverride("focus", xNormal);
        xBtn.AddThemeFontSizeOverride("font_size", 24);
        xBtn.Pressed += Close;
        topRow.AddChild(xBtn);

        var sep = new HSeparator();
        sep.Modulate = new Color(1, 1, 1, 0.1f);
        vbox.AddChild(sep);

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
        _grid.AddThemeConstantOverride("h_separation", 12);
        _grid.AddThemeConstantOverride("v_separation", 12);
        _itemScroll.AddChild(_grid);

        body.AddChild(BuildDetailPanel());
    }

    private Control BuildDetailPanel()
    {
        var detail = new PanelContainer { CustomMinimumSize = new Vector2(360, 0) };
        detail.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var detailBox = new StyleBoxFlat
        {
            BgColor = new Color(0.11f, 0.12f, 0.15f, 0.95f),
            CornerRadiusTopLeft = 20, CornerRadiusTopRight = 20, CornerRadiusBottomRight = 20, CornerRadiusBottomLeft = 20,
            ContentMarginLeft = 20, ContentMarginRight = 20, ContentMarginTop = 24, ContentMarginBottom = 24
        };
        detail.AddThemeStyleboxOverride("panel", detailBox);

        var dv = new VBoxContainer();
        dv.AddThemeConstantOverride("separation", 16);
        detail.AddChild(dv);

        _detailImage = new CenterContainer { CustomMinimumSize = new Vector2(0, 160) };
        dv.AddChild(_detailImage);

        _detailName = new Label { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _detailName.AddThemeFontSizeOverride("font_size", 26);
        _detailName.AddThemeColorOverride("font_color", new Color(0.96f, 0.85f, 0.45f));
        _detailName.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.6f));
        dv.AddChild(_detailName);

        _detailMeta = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _detailMeta.AddThemeFontSizeOverride("font_size", 16);
        _detailMeta.AddThemeColorOverride("font_color", new Color(0.75f, 0.8f, 0.9f));
        dv.AddChild(_detailMeta);

        var sep = new HSeparator();
        sep.Modulate = new Color(1, 1, 1, 0.15f);
        dv.AddChild(sep);

        _detailDesc = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _detailDesc.AddThemeColorOverride("font_color", new Color(0.85f, 0.88f, 0.92f));
        _detailDesc.AddThemeFontSizeOverride("font_size", 18);
        dv.AddChild(_detailDesc);

        dv.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });   // đẩy nút xuống đáy

        _useButton = new Button { Text = "Dùng", CustomMinimumSize = new Vector2(0, 56), Visible = false };
        var useNormal = new StyleBoxFlat { BgColor = new Color(0.25f, 0.65f, 0.35f, 1f), CornerRadiusTopLeft = 16, CornerRadiusTopRight = 16, CornerRadiusBottomRight = 16, CornerRadiusBottomLeft = 16 };
        var useHover = new StyleBoxFlat { BgColor = new Color(0.3f, 0.75f, 0.4f, 1f), CornerRadiusTopLeft = 16, CornerRadiusTopRight = 16, CornerRadiusBottomRight = 16, CornerRadiusBottomLeft = 16 };
        var usePressed = new StyleBoxFlat { BgColor = new Color(0.15f, 0.45f, 0.2f, 1f), CornerRadiusTopLeft = 16, CornerRadiusTopRight = 16, CornerRadiusBottomRight = 16, CornerRadiusBottomLeft = 16 };
        _useButton.AddThemeStyleboxOverride("normal", useNormal);
        _useButton.AddThemeStyleboxOverride("hover", useHover);
        _useButton.AddThemeStyleboxOverride("pressed", usePressed);
        _useButton.AddThemeStyleboxOverride("focus", useNormal);
        _useButton.AddThemeFontSizeOverride("font_size", 22);
        _useButton.Pressed += OnUsePressed;
        dv.AddChild(_useButton);

        _multiEquipBox = new HBoxContainer { Visible = false };
        _multiEquipBox.AddThemeConstantOverride("separation", 16);
        _multiEquipBox.Alignment = BoxContainer.AlignmentMode.Center;
        dv.AddChild(_multiEquipBox);

        _dropRunDust = new SkinDropSlot { TargetCategory = SkinCategory.RunDust, OnDropAction = EquipMovementSkin, CustomMinimumSize = new Vector2(100, 120), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _dropFootstep = new SkinDropSlot { TargetCategory = SkinCategory.Footstep, OnDropAction = EquipMovementSkin, CustomMinimumSize = new Vector2(100, 120), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };

        _multiEquipBox.AddChild(_dropRunDust);
        _multiEquipBox.AddChild(_dropFootstep);

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
            CustomMinimumSize = new Vector2(110, 52),
        };
        ApplyModernTabStyle(btn);
        btn.Pressed += () => SetTab(type);
        bar.AddChild(btn);
    }

    private static void ApplyModernTabStyle(Button btn)
    {
        var normal = new StyleBoxFlat { BgColor = new Color(0.18f, 0.19f, 0.24f, 0.6f), CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomRight = 12, CornerRadiusBottomLeft = 12 };
        var hover = new StyleBoxFlat { BgColor = new Color(0.25f, 0.27f, 0.33f, 0.8f), CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomRight = 12, CornerRadiusBottomLeft = 12 };
        var pressed = new StyleBoxFlat { BgColor = new Color(0.3f, 0.55f, 0.9f, 1f), CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomRight = 12, CornerRadiusBottomLeft = 12 };

        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", pressed);
        btn.AddThemeStyleboxOverride("focus", normal);

        btn.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.9f));
        btn.AddThemeColorOverride("font_hover_color", Colors.White);
        btn.AddThemeColorOverride("font_pressed_color", Colors.White);
        btn.AddThemeFontSizeOverride("font_size", 18);
    }

    private PanelContainer MakeCurrency(string iconPath, out Label label)
    {
        var pill = new PanelContainer();
        var pillBox = new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0.5f),
            CornerRadiusTopLeft = 20, CornerRadiusTopRight = 20,
            CornerRadiusBottomRight = 20, CornerRadiusBottomLeft = 20,
            ContentMarginLeft = 12, ContentMarginTop = 4,
            ContentMarginRight = 12, ContentMarginBottom = 4
        };
        pill.AddThemeStyleboxOverride("panel", pillBox);

        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 6);
        pill.AddChild(h);

        if (ResourceLoader.Exists(iconPath))
        {
            var tex = GD.Load<Texture2D>(iconPath);
            if (tex != null)
                h.AddChild(new TextureRect
                {
                    Texture           = tex,
                    CustomMinimumSize = new Vector2(32, 32),
                    StretchMode       = TextureRect.StretchModeEnum.KeepAspectCentered,
                    ExpandMode        = TextureRect.ExpandModeEnum.IgnoreSize,
                });
        }

        label = new Label { VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", 20);
        label.AddThemeColorOverride("font_color", Colors.White);
        h.AddChild(label);
        return pill;
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
            empty.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.7f));
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
            CustomMinimumSize   = new Vector2(116, 116),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText         = item.NameVi,
        };
        ApplySlotStyle(btn, selected);

        // Ảnh item — căn giữa, phủ toàn ô
        var icon = UiKit.ItemIcon(item, UiKit.TypeColor(item.Type), 72);
        icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        // Số lượng — góc dưới phải
        var overlay = new VBoxContainer();
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });

        var bottom = new HBoxContainer();
        bottom.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        var qtyPill = new PanelContainer();
        var pillStyle = new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0.7f), CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomRight = 8, CornerRadiusBottomLeft = 8, ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 4, ContentMarginBottom = 4 };
        qtyPill.AddThemeStyleboxOverride("panel", pillStyle);
        var qty = new Label { Text = $"{stack.Count}" };
        qty.AddThemeFontSizeOverride("font_size", 16);
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
        Color border = selected ? new Color(0.4f, 0.65f, 0.95f) : new Color(0.28f, 0.3f, 0.35f);
        int   bw     = selected ? 3 : 2;
        Color bg     = selected ? new Color(0.2f, 0.3f, 0.5f, 0.8f) : new Color(0.18f, 0.19f, 0.24f, 0.9f);

        var normal = new StyleBoxFlat { BgColor = bg, BorderColor = border, BorderWidthLeft = bw, BorderWidthTop = bw, BorderWidthRight = bw, BorderWidthBottom = bw, CornerRadiusTopLeft = 16, CornerRadiusTopRight = 16, CornerRadiusBottomRight = 16, CornerRadiusBottomLeft = 16 };
        var hover = new StyleBoxFlat { BgColor = new Color(0.22f, 0.24f, 0.3f, 1f), BorderColor = border, BorderWidthLeft = bw, BorderWidthTop = bw, BorderWidthRight = bw, BorderWidthBottom = bw, CornerRadiusTopLeft = 16, CornerRadiusTopRight = 16, CornerRadiusBottomRight = 16, CornerRadiusBottomLeft = 16 };

        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", normal);
        btn.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
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
        if (_multiEquipBox != null) _multiEquipBox.Visible = false;
    }

    private async void OnUsePressed()
    {
        if (_skinsMode)
        {
            if (!string.IsNullOrEmpty(_selectedSkinId))
            {
                SkinCategory cat = GetRealCategory(_currentSkinTab);
                SkinManager.Instance?.Equip(cat, _selectedSkinId);
                RefreshSkins();
            }
            return;
        }

        if (!string.IsNullOrEmpty(_selectedId))
        {
            _useButton.Disabled = true;
            bool ok = await Inventory.Instance?.UseItemAsync(_selectedId);
            _useButton.Disabled = false;
            if (!ok) UiKit.Toast(_root, "Không dùng được vật phẩm này lúc này.", 2f);
        }
    }

    // ───────────────────────── Tab Skin ─────────────────────────

    private void AddSkinTab(HBoxContainer bar, ButtonGroup group, string text)
    {
        var btn = new Button
        {
            Text              = text,
            ToggleMode        = true,
            ButtonGroup       = group,
            CustomMinimumSize = new Vector2(110, 52),
        };
        ApplyModernTabStyle(btn);
        btn.Pressed += EnterSkinsMode;
        bar.AddChild(btn);
    }

    private SkinCategory GetRealCategory(SkinTab tab) => tab switch {
        SkinTab.Player => SkinCategory.Player,
        SkinTab.AttackSwing => SkinCategory.AttackSwing,
        SkinTab.HitSpark => SkinCategory.HitSpark,
        SkinTab.LevelUpAura => SkinCategory.LevelUpAura,
        _ => SkinCategory.Player
    };

    private static string TabName(SkinTab t) => t switch {
        SkinTab.Player => "Nhân vật",
        SkinTab.AttackSwing => "Đòn đánh",
        SkinTab.HitSpark => "Tia trúng",
        SkinTab.LevelUpAura => "Hào quang",
        SkinTab.Movement => "Di chuyển",
        _ => ""
    };

    private void EquipMovementSkin(SkinCategory targetCat, string skinId)
    {
        if (string.IsNullOrEmpty(skinId)) return;
        var mgr = SkinManager.Instance;
        if (mgr == null) return;

        SkinCategory otherCat = targetCat == SkinCategory.RunDust ? SkinCategory.Footstep : SkinCategory.RunDust;
        if (mgr.GetEquippedId(otherCat) == skinId)
        {
            mgr.Equip(otherCat, "");
        }
        mgr.Equip(targetCat, skinId);
        RefreshSkins();
    }

    private HBoxContainer BuildSkinCatBar()
    {
        var bar = new HBoxContainer();
        bar.AddThemeConstantOverride("separation", 6);

        var group = new ButtonGroup();
        bool first = true;
        foreach (SkinTab tab in System.Enum.GetValues<SkinTab>())
        {
            var t = tab;
            var btn = new Button
            {
                Text                = TabName(tab),
                ToggleMode          = true,
                ButtonGroup         = group,
                ButtonPressed       = first,
                CustomMinimumSize   = new Vector2(0, 44),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            ApplyModernTabStyle(btn);
            btn.AddThemeFontSizeOverride("font_size", 16);
            btn.Pressed += () =>
            {
                _currentSkinTab = t;
                if (t == SkinTab.Movement)
                    _selectedSkinId = "";
                else
                    _selectedSkinId = SkinManager.Instance?.GetEquippedId(GetRealCategory(t)) ?? "";
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
        if (_currentSkinTab == SkinTab.Movement)
            _selectedSkinId = "";
        else
            _selectedSkinId = SkinManager.Instance?.GetEquippedId(GetRealCategory(_currentSkinTab)) ?? "";
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
        
        System.Collections.Generic.IEnumerable<SkinDef> catalog;
        if (_currentSkinTab == SkinTab.Movement)
            catalog = System.Linq.Enumerable.Concat(mgr.Catalog(SkinCategory.RunDust), mgr.Catalog(SkinCategory.Footstep));
        else
            catalog = mgr.Catalog(GetRealCategory(_currentSkinTab));

        bool selAlive = false;
        foreach (var skin in catalog)
        {
            bool sel  = skin.Id == _selectedSkinId;
            bool equipped;
            if (_currentSkinTab == SkinTab.Movement)
                equipped = mgr.GetEquippedId(SkinCategory.RunDust) == skin.Id || mgr.GetEquippedId(SkinCategory.Footstep) == skin.Id;
            else
                equipped = mgr.GetEquippedId(GetRealCategory(_currentSkinTab)) == skin.Id;

            var  slot = MakeSkinSlot(skin, sel, equipped, mgr.IsOwned(skin.Id));
            if (sel) { _selectedSlot = slot; selAlive = true; }
            _grid.AddChild(slot);
        }

        if (selAlive) UpdateSkinDetail(_selectedSkinId);
        else          ClearSelection();
    }

    private Button MakeSkinSlot(SkinDef skin, bool selected, bool equipped, bool owned)
    {
        var btn = new DraggableSkinSlot
        {
            SkinId              = skin.Id,
            CustomMinimumSize   = new Vector2(116, 116),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText         = owned ? skin.Name : $"{skin.Name} (chưa mở khóa)",
        };
        ApplySlotStyle(btn, selected);

        var v = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        v.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        v.AddThemeConstantOverride("separation", 4);

        var swHolder = new CenterContainer();
        bool hasTex = !string.IsNullOrEmpty(skin.Frames) && skin.Frames.EndsWith(".png") && ResourceLoader.Exists(skin.Frames);
        if (hasTex)
        {
            var tex = GD.Load<Texture2D>(skin.Frames);
            btn.DragIcon = tex;
            var tr = new TextureRect { Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(54, 54) };
            if (!owned) tr.Modulate = new Color(1, 1, 1, 0.28f);
            swHolder.AddChild(tr);
        }
        else
        {
            var swatch   = new PanelContainer { CustomMinimumSize = new Vector2(54, 40) };
            var swatchStyle = new StyleBoxFlat { BgColor = skin.ColorValue, CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomRight = 8, CornerRadiusBottomLeft = 8, BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2, BorderColor = new Color(0.243f, 0.255f, 0.345f) };
            swatch.AddThemeStyleboxOverride("panel", swatchStyle);
            if (!owned) swatch.Modulate = new Color(1, 1, 1, 0.28f);   // khóa → mờ đi
            swHolder.AddChild(swatch);
        }
        v.AddChild(swHolder);

        var name = new Label { Text = skin.Name, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        name.AddThemeFontSizeOverride("font_size", 14);
        name.AddThemeColorOverride("font_color", owned ? new Color(0.9f, 0.9f, 0.95f) : new Color(0.6f, 0.6f, 0.7f));
        v.AddChild(name);

        btn.AddChild(v);
        IgnoreMouse(v);

        if (!owned)
        {
            var lockTag = new Label { Text = "🔒", HorizontalAlignment = HorizontalAlignment.Left, MouseFilter = Control.MouseFilterEnum.Ignore };
            lockTag.SetAnchorsPreset(Control.LayoutPreset.TopWide);
            lockTag.OffsetTop  = 4;
            lockTag.OffsetLeft = 8;
            lockTag.AddThemeFontSizeOverride("font_size", 18);
            btn.AddChild(lockTag);
        }

        if (equipped)
        {
            var tag = new Label { Text = "✓", HorizontalAlignment = HorizontalAlignment.Right, MouseFilter = Control.MouseFilterEnum.Ignore };
            tag.SetAnchorsPreset(Control.LayoutPreset.TopWide);
            tag.OffsetTop   = 4;
            tag.OffsetRight = -8;
            tag.AddThemeFontSizeOverride("font_size", 20);
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

        bool owned = mgr.IsOwned(id);

        foreach (Node c in _detailImage.GetChildren()) c.QueueFree();

        bool hasTex = !string.IsNullOrEmpty(skin.Frames) && skin.Frames.EndsWith(".png") && ResourceLoader.Exists(skin.Frames);
        if (hasTex)
        {
            var tex = GD.Load<Texture2D>(skin.Frames);
            var tr = new TextureRect { Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(132, 132) };
            if (!owned) tr.Modulate = new Color(1, 1, 1, 0.3f);
            _detailImage.AddChild(tr);
        }
        else
        {
            var swatch = new PanelContainer { CustomMinimumSize = new Vector2(132, 96) };
            var swatchStyle = new StyleBoxFlat { BgColor = skin.ColorValue, CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomRight = 12, CornerRadiusBottomLeft = 12, BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2, BorderColor = new Color(0.243f, 0.255f, 0.345f) };
            swatch.AddThemeStyleboxOverride("panel", swatchStyle);
            if (!owned) swatch.Modulate = new Color(1, 1, 1, 0.3f);
            _detailImage.AddChild(swatch);
        }

        _detailName.Text = skin.Name;

        if (_currentSkinTab == SkinTab.Movement)
        {
            _useButton.Visible = false;
            _multiEquipBox.Visible = true;
            
            bool eqRunDust = mgr.GetEquippedId(SkinCategory.RunDust) == id;
            bool eqFootstep = mgr.GetEquippedId(SkinCategory.Footstep) == id;

            _detailMeta.Text = "Di chuyển" + (eqRunDust || eqFootstep ? "  •  Đang dùng" : owned ? "" : "  •  🔒 Chưa mở khóa");
            _detailDesc.Text = owned ? "Kéo thả hiệu ứng này vào một trong hai ô bên dưới để trang bị." : "Quay Gacha Hiệu Ứng trong Cửa hàng để mở khóa skin này.";

            UpdateDropSlot(_dropRunDust, mgr.GetEquippedId(SkinCategory.RunDust), "Gia tốc");
            UpdateDropSlot(_dropFootstep, mgr.GetEquippedId(SkinCategory.Footstep), "Dấu chân");
        }
        else
        {
            _useButton.Visible = true;
            if (_multiEquipBox != null) _multiEquipBox.Visible = false;

            SkinCategory cat = GetRealCategory(_currentSkinTab);
            bool equipped = mgr.GetEquippedId(cat) == id;
            _detailMeta.Text = SkinCatName(cat) + (equipped ? "  •  Đang dùng" : owned ? "" : "  •  🔒 Chưa mở khóa");
            _detailDesc.Text = owned ? $"Skin {SkinCatName(cat).ToLower()}." : "Quay Gacha Hiệu Ứng trong Cửa hàng để mở khóa skin này.";

            _useButton.Text     = !owned ? "Chưa mở khóa" : equipped ? "Đang dùng" : "Trang bị";
            _useButton.Disabled = equipped || !owned;
        }
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

    private void UpdateDropSlot(SkinDropSlot slot, string equippedId, string labelText)
    {
        foreach (Node child in slot.GetChildren()) child.QueueFree();

        var vbox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        vbox.AddThemeConstantOverride("separation", 8);
        vbox.MouseFilter = Control.MouseFilterEnum.Ignore;

        var lbl = new Label { Text = labelText, HorizontalAlignment = HorizontalAlignment.Center };
        lbl.AddThemeFontSizeOverride("font_size", 14);
        lbl.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.8f));
        lbl.MouseFilter = Control.MouseFilterEnum.Ignore;
        vbox.AddChild(lbl);

        var mgr = SkinManager.Instance;
        var skin = mgr?.Get(equippedId);

        if (skin != null)
        {
            bool hasTex = !string.IsNullOrEmpty(skin.Frames) && skin.Frames.EndsWith(".png") && ResourceLoader.Exists(skin.Frames);
            if (hasTex)
            {
                var tex = GD.Load<Texture2D>(skin.Frames);
                var tr = new TextureRect { Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(48, 48) };
                tr.MouseFilter = Control.MouseFilterEnum.Ignore;
                vbox.AddChild(tr);
            }
            else
            {
                var swatch = new PanelContainer { CustomMinimumSize = new Vector2(48, 48) };
                var swatchStyle = new StyleBoxFlat { BgColor = skin.ColorValue, CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomRight = 8, CornerRadiusBottomLeft = 8, BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2, BorderColor = new Color(0.243f, 0.255f, 0.345f) };
                swatch.AddThemeStyleboxOverride("panel", swatchStyle);
                swatch.MouseFilter = Control.MouseFilterEnum.Ignore;
                vbox.AddChild(swatch);
            }
        }

        var hint = new Label { Text = "(Kéo vào đây)", HorizontalAlignment = HorizontalAlignment.Center };
        hint.AddThemeFontSizeOverride("font_size", 12);
        hint.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.6f));
        hint.MouseFilter = Control.MouseFilterEnum.Ignore;
        vbox.AddChild(hint);

        slot.AddChild(vbox);
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

public partial class DraggableSkinSlot : Button
{
    public string SkinId { get; set; }
    public Texture2D DragIcon { get; set; }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (string.IsNullOrEmpty(SkinId)) return default;
        if (DragIcon != null)
        {
            var preview = new TextureRect { Texture = DragIcon, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(64, 64) };
            preview.Modulate = new Color(1, 1, 1, 0.7f);
            SetDragPreview(preview);
        }
        return Variant.CreateFrom(SkinId);
    }
}

public partial class SkinDropSlot : PanelContainer
{
    public SkinCategory TargetCategory { get; set; }
    public System.Action<SkinCategory, string> OnDropAction { get; set; }

    public override void _Ready()
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.13f, 0.18f),
            BorderColor = new Color(0.3f, 0.3f, 0.4f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomRight = 12, CornerRadiusBottomLeft = 12,
        };
        AddThemeStyleboxOverride("panel", style);
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return data.VariantType == Variant.Type.String;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (data.VariantType == Variant.Type.String)
        {
            string skinId = data.AsString();
            OnDropAction?.Invoke(TargetCategory, skinId);
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        bool isClick = false;
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            isClick = true;
        else if (@event is InputEventScreenTouch tc && tc.Pressed)
            isClick = true;

        if (isClick && InventoryUi.Instance != null && !string.IsNullOrEmpty(InventoryUi.Instance.SelectedSkinId))
        {
            OnDropAction?.Invoke(TargetCategory, InventoryUi.Instance.SelectedSkinId);
        }
    }
}
