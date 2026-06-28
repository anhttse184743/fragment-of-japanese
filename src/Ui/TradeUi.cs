using Godot;
using System.Collections.Generic;
using FragmentOfJapanese.Autoloads;
using FragmentOfJapanese.Items;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// Màn đổi vật phẩm với trưởng làng. Hiện danh sách vật phẩm Trade trong túi đồ,
/// mỗi thứ kèm tỷ lệ đổi → vàng. Bấm "Đổi" để gửi API bán và cộng vàng.
/// Mở dạng overlay qua <see cref="Open"/>.
/// </summary>
public partial class TradeUi : Control
{
    // Tỷ lệ đổi: 1 vật phẩm = bao nhiêu vàng
    private static readonly Dictionary<string, (string nameVi, int goldPer)> _rates = new()
    {
        { "item_goblin_ear", ("Tai Goblin", 25) },
    };

    private VBoxContainer _listContainer;
    private Label         _feedbackLabel;

    public static void Open()
    {
        if (Engine.GetMainLoop() is not SceneTree tree) return;
        // Đóng overlay cũ nếu đã mở
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
        AddChild(bg);

        // Panel trung tâm
        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        center.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(480, 0) };
        panel.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.14f, 0.10f, 0.06f), 14, UiKit.Accent, 2, 20, 18));
        center.AddChild(panel);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 14);
        panel.AddChild(vb);

        // Tiêu đề
        var title = MkLabel("🪙  Đổi Chiến Lợi Phẩm", 22, UiKit.Accent);
        vb.AddChild(title);

        var sub = MkLabel("Đổi vật phẩm trade lấy Vàng từ trưởng làng.", 15, UiKit.WoodTextDim);
        vb.AddChild(sub);

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", UiKit.Fade(UiKit.Accent, 0.3f));
        vb.AddChild(sep);

        // Danh sách
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 280) };
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        vb.AddChild(scroll);

        _listContainer = new VBoxContainer();
        _listContainer.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_listContainer);

        // Feedback
        _feedbackLabel = MkLabel("", 16, UiKit.BuyGreenHi);
        vb.AddChild(_feedbackLabel);

        // Nút đóng
        var closeBtn = MkButton("✕  Đóng", Close);
        vb.AddChild(closeBtn);
    }

    // ─── Làm mới danh sách ───────────────────────────────────────────────────
    private void RefreshList()
    {
        foreach (Node child in _listContainer.GetChildren())
            child.QueueFree();

        var inv = Inventory.Instance;
        if (inv == null)
        {
            _listContainer.AddChild(MkLabel("Không thể đọc túi đồ.", 16, UiKit.TextDim));
            return;
        }

        bool hasAny = false;
        foreach (var (itemId, (nameVi, goldPer)) in _rates)
        {
            int qty = inv.CountOf(itemId);
            if (qty <= 0) continue;
            hasAny = true;
            _listContainer.AddChild(MakeRow(itemId, nameVi, goldPer, qty));
        }

        if (!hasAny)
            _listContainer.AddChild(MkLabel("Bạn không có vật phẩm nào để đổi.", 16, UiKit.TextDim));
    }

    // ─── Dòng item ───────────────────────────────────────────────────────────
    private Control MakeRow(string itemId, string nameVi, int goldPer, int qty)
    {
        var row = new PanelContainer();
        row.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.20f, 0.14f, 0.08f), 10, UiKit.Fade(UiKit.Accent, 0.25f), 1, 12, 8));

        var hb = new HBoxContainer();
        hb.AddThemeConstantOverride("separation", 10);
        row.AddChild(hb);

        // Icon vật phẩm
        var icon = UiKit.ItemIcon(ItemDatabase.Instance?.Get(itemId), UiKit.Gold, 44);
        hb.AddChild(icon);

        // Thông tin
        var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        info.AddThemeConstantOverride("separation", 2);
        hb.AddChild(info);

        var nameLbl = MkLabel($"{nameVi}  ×{qty}", 17, UiKit.WoodText);
        nameLbl.HorizontalAlignment = HorizontalAlignment.Left;
        info.AddChild(nameLbl);

        int total = goldPer * qty;
        var rateLbl = MkLabel($"Đổi hết → 🪙 {total} Vàng  ({goldPer}/cái)", 14, UiKit.WoodTextDim);
        rateLbl.HorizontalAlignment = HorizontalAlignment.Left;
        info.AddChild(rateLbl);

        // Nút đổi hết
        var tradeBtn = MkButton($"Đổi hết ({qty})", () => OnTrade(itemId, nameVi, goldPer, qty));
        tradeBtn.CustomMinimumSize = new Vector2(120, 44);
        hb.AddChild(tradeBtn);

        return row;
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

            SetFeedback($"✓ Đổi {qty}× {nameVi} → +{totalGold} Vàng!", UiKit.BuyGreenHi);
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
        b.Pressed += onPressed;
        return b;
    }
}
