using Godot;
using System.Collections.Generic;
using FragmentOfJapanese.Autoloads;
using FragmentOfJapanese.Items;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// Màn đổi vật phẩm với trưởng làng.
/// </summary>
public partial class TradeUi : Control
{

    private VBoxContainer _listContainer;
    private Label         _feedbackLabel;

    public static void Open()
    {
        if (Engine.GetMainLoop() is not SceneTree tree) return;
        var old = tree.Root.GetNodeOrNull("TradeOverlay");
        old?.QueueFree();

        var layer = new CanvasLayer { Layer = 62, Name = "TradeOverlay" };
        layer.AddChild(new TradeUi());
        tree.Root.AddChild(layer);
    }

    public override void _Ready()
    {
        BuildUi();
        RefreshList();
        GetViewport().SizeChanged += () => Size = GetViewport().GetVisibleRect().Size;
    }

    // ─── Xây giao diện ───────────────────────────────────────────────────────
    private void BuildUi()
    {
        var viewRect = GetViewport().GetVisibleRect();
        Position = Vector2.Zero;
        Size     = viewRect.Size;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // Nền mờ toàn màn
        var bg = new ColorRect { Color = new Color(0.04f, 0.05f, 0.08f, 0.88f) };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Stop;
        AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(800, 650) };
        panel.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.24f, 0.17f, 0.13f, 0.98f), 24, new Color(0.4f, 0.28f, 0.2f, 1f), 4, 32, 32));
        center.AddChild(panel);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 24);
        panel.AddChild(vb);

        // Header
        var headerBox = new HBoxContainer();
        var title = MkLabel("THU MUA CHIẾN LỢI PHẨM", 34, UiKit.Accent);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        title.HorizontalAlignment = HorizontalAlignment.Left;
        headerBox.AddChild(title);

        var closeBtn = new Button { Text = "X", CustomMinimumSize = new Vector2(56, 56) };
        UiKit.StyleButton(closeBtn, new Color(0.7f, 0.25f, 0.25f), new Color(0.85f, 0.35f, 0.35f), new Color(0.55f, 0.15f, 0.15f), radius: 16);
        closeBtn.AddThemeFontSizeOverride("font_size", 24);
        closeBtn.Pressed += Close;
        headerBox.AddChild(closeBtn);
        
        vb.AddChild(headerBox);

        var sub = MkLabel("Đổi các vật phẩm thu thập được từ quái vật để lấy Vàng.", 20, UiKit.WoodTextDim);
        sub.HorizontalAlignment = HorizontalAlignment.Left;
        vb.AddChild(sub);

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", UiKit.Fade(UiKit.Accent, 0.3f));
        vb.AddChild(sep);

        // Danh sách
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 420) };
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        vb.AddChild(scroll);

        _listContainer = new VBoxContainer();
        _listContainer.AddThemeConstantOverride("separation", 16);
        _listContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_listContainer);

        // Feedback
        _feedbackLabel = MkLabel("", 20, UiKit.BuyGreenHi);
        vb.AddChild(_feedbackLabel);
    }

    // ─── Làm mới danh sách ───────────────────────────────────────────────────
    private void RefreshList()
    {
        foreach (Node child in _listContainer.GetChildren())
            child.QueueFree();

        var inv = Inventory.Instance;
        if (inv == null)
        {
            _listContainer.AddChild(MkLabel("Không thể đọc túi đồ.", 20, UiKit.TextDim));
            return;
        }

        // Chỉ đổi Tai Goblin lấy Vàng (giá theo DB sell_price). Cuộn từ vựng đổi chìa ở ScrollTradeUi.
        bool hasAny = false;
        const string earId = "item_goblin_ear";
        int earQty = inv.CountOf(earId);
        if (earQty > 0)
        {
            int goldPer = Shop.Instance?.GetSellPrice(earId) ?? 20;
            if (goldPer <= 0) goldPer = 20;
            string nameVi = ItemDatabase.Instance?.Get(earId)?.NameVi ?? "Tai Goblin";
            hasAny = true;
            _listContainer.AddChild(MakeRow(earId, nameVi, goldPer, earQty));
        }

        if (!hasAny)
        {
            var noItem = MkLabel("Bạn không có vật phẩm nào để đổi lúc này.", 22, UiKit.WoodTextDim);
            noItem.CustomMinimumSize = new Vector2(0, 300);
            noItem.VerticalAlignment = VerticalAlignment.Center;
            _listContainer.AddChild(noItem);
        }
    }

    // ─── Dòng item ───────────────────────────────────────────────────────────
    private Control MakeRow(string itemId, string nameVi, int goldPer, int maxQty)
    {
        var row = new PanelContainer();
        row.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.18f, 0.12f, 0.09f, 1f), 16, new Color(0.4f, 0.28f, 0.2f, 1f), 2, 20, 20));

        var hb = new HBoxContainer();
        hb.AddThemeConstantOverride("separation", 24);
        row.AddChild(hb);

        // Icon vật phẩm
        var icon = UiKit.ItemIcon(ItemDatabase.Instance?.Get(itemId), UiKit.Gold, 96);
        hb.AddChild(icon);

        // Thông tin
        var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        info.AddThemeConstantOverride("separation", 8);
        hb.AddChild(info);

        var nameLbl = MkLabel(nameVi, 26, UiKit.WoodText);
        nameLbl.HorizontalAlignment = HorizontalAlignment.Left;
        info.AddChild(nameLbl);

        var rateLbl = MkLabel($"Giá: {goldPer} Vàng / cái", 18, UiKit.Accent);
        rateLbl.HorizontalAlignment = HorizontalAlignment.Left;
        info.AddChild(rateLbl);
        
        var stockLbl = MkLabel($"Bạn đang có: {maxQty}", 18, UiKit.WoodTextDim);
        stockLbl.HorizontalAlignment = HorizontalAlignment.Left;
        info.AddChild(stockLbl);

        // Vùng thao tác
        var actionCol = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        actionCol.AddThemeConstantOverride("separation", 16);
        hb.AddChild(actionCol);

        var qtyRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        qtyRow.AddThemeConstantOverride("separation", 12);
        actionCol.AddChild(qtyRow);

        int currentQty = maxQty; // Default to max

        var qtyLbl = MkLabel(currentQty.ToString(), 26, Colors.White);
        
        var qtyPanel = new PanelContainer { CustomMinimumSize = new Vector2(90, 50) };
        qtyPanel.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.12f, 0.08f, 0.06f, 1f), 8, UiKit.WoodBorder, 2));
        var qtyCenter = new CenterContainer();
        qtyCenter.AddChild(qtyLbl);
        qtyPanel.AddChild(qtyCenter);

        var btnTrade = MkButton($"ĐỔI ({currentQty * goldPer} Vàng)", null);
        btnTrade.CustomMinimumSize = new Vector2(280, 56);
        UiKit.StyleButton(btnTrade, new Color(0.2f, 0.5f, 0.3f, 1f), new Color(0.25f, 0.6f, 0.35f, 1f), new Color(0.15f, 0.4f, 0.25f, 1f), radius: 12);
        btnTrade.AddThemeFontSizeOverride("font_size", 22);

        void UpdateQty(int newQty)
        {
            currentQty = Mathf.Clamp(newQty, 1, maxQty);
            qtyLbl.Text = currentQty.ToString();
            btnTrade.Text = $"ĐỔI ({currentQty * goldPer} Vàng)";
        }

        qtyRow.AddChild(MakeSmallBtn("-10", () => UpdateQty(currentQty - 10)));
        qtyRow.AddChild(MakeSmallBtn("-", () => UpdateQty(currentQty - 1)));
        qtyRow.AddChild(qtyPanel);
        qtyRow.AddChild(MakeSmallBtn("+", () => UpdateQty(currentQty + 1)));
        qtyRow.AddChild(MakeSmallBtn("+10", () => UpdateQty(currentQty + 10)));

        btnTrade.Pressed += () => OnTrade(itemId, nameVi, goldPer, currentQty);
        actionCol.AddChild(btnTrade);

        return row;
    }

    private Button MakeSmallBtn(string text, System.Action onPressed)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(45, 50) };
        UiKit.StyleButton(b, new Color(0.3f, 0.22f, 0.16f, 1f), new Color(0.4f, 0.3f, 0.22f, 1f), UiKit.Accent, radius: 12);
        b.AddThemeFontSizeOverride("font_size", 20);
        b.Pressed += onPressed;
        return b;
    }

    // ─── Logic đổi ───────────────────────────────────────────────────────────
    private async void OnTrade(string itemId, string nameVi, int goldPer, int qty)
    {
        var inv    = Inventory.Instance;
        var wallet = Wallet.Instance;
        if (inv == null || wallet == null) return;

        int totalGold = goldPer * qty;

        // Xóa khỏi túi đồ qua API
        var res = await ApiClient.Instance.PostAsync($"/api/inventory/sell",
            new { StringId = itemId, Quantity = qty });

        if (res.IsSuccessStatusCode)
        {
            // Cộng vàng local (optimistic) rồi sync
            wallet.AddGold(totalGold);
            inv.RemoveOptimistic(itemId, qty);
            _ = wallet.SyncAsync();
            _ = inv.SyncAsync();

            SetFeedback($"✓ Đã bán {qty} {nameVi} lấy {totalGold} Vàng!", UiKit.BuyGreenHi);
            RefreshList();
        }
        else
        {
            SetFeedback("✗ Không đổi được. Thử lại sau.", new Color(0.9f, 0.4f, 0.4f));
        }
    }

    private void SetFeedback(string msg, Color c)
    {
        _feedbackLabel.Text = msg;
        _feedbackLabel.AddThemeColorOverride("font_color", c);
    }

    private void Close()
    {
        if (GetParent() is CanvasLayer layer) layer.QueueFree();
        else QueueFree();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    private static Label MkLabel(string text, int size, Color color)
    {
        var l = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        return l;
    }

    private static Button MkButton(string text, System.Action onPressed)
    {
        var b = new Button { Text = text };
        b.AddThemeFontSizeOverride("font_size", 16);
        UiKit.StyleButton(b, UiKit.WoodCard, UiKit.Fade(UiKit.Accent, 0.5f), UiKit.Accent);
        if (onPressed != null) b.Pressed += onPressed;
        return b;
    }
}

