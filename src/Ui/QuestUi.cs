using Godot;
using System.Collections.Generic;
using FragmentOfJapanese.Core;
using FragmentOfJapanese.Quests;
using PlayerNode = FragmentOfJapanese.Entities.Player.Player;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// UI nhiệm vụ — autoload dựng bằng code, tông nâu gỗ. Mở từ nút Quest trên HUD (Esc để đóng).
/// Liệt kê nhiệm vụ HÀNG NGÀY / HÀNG TUẦN từ <see cref="QuestManager"/>: tiến độ + phần thưởng + nút "Nhận".
/// "Nhận" gọi QuestManager.Claim (cộng Vàng/Ma Thạch/vật phẩm); EXP cộng cho người chơi tại <see cref="OnClaimed"/>.
/// </summary>
public partial class QuestUi : CanvasLayer
{
    public static QuestUi Instance { get; private set; }

    private Control       _root;
    private VBoxContainer _list;

    public override void _Ready()
    {
        Instance = this;
        Layer    = 12;            // trên shop (11) và túi đồ (10)

        BuildUi();
        _root.Visible = false;

        var qm = QuestManager.Instance;
        if (qm != null)
        {
            qm.Progressed  += _ => RefreshIfOpen();
            qm.Completed   += _ => RefreshIfOpen();
            qm.Claimed     += OnClaimed;
            qm.PeriodReset += RefreshIfOpen;
        }
    }

    public override void _UnhandledInput(InputEvent ev)
    {
        if (_root != null && _root.Visible && ev.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    public void Toggle() { if (_root.Visible) Close(); else Open(); }
    public void Open()   { _root.Visible = true; Refresh(); }
    public void Close()  { _root.Visible = false; }

    private void RefreshIfOpen() { if (_root != null && _root.Visible) Refresh(); }

    /// <summary>EXP thưởng nhiệm vụ — cộng cho người chơi (các thưởng khác đã do QuestManager phát).</summary>
    private void OnClaimed(QuestEntry q)
    {
        if (q?.Reward != null && q.Reward.Exp > 0)
            (GetTree().GetFirstNodeInGroup("player") as PlayerNode)?.GainExp(q.Reward.Exp);
        RefreshIfOpen();
    }

    // ───────────────────────── Build khung ─────────────────────────

    private void BuildUi()
    {
        _root = new Control { Name = "QuestRoot" };
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

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(720, 600) };
        panel.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.WoodPanel, 14, UiKit.WoodBorder, 3, 14, 12));
        center.AddChild(panel);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 10);
        panel.AddChild(col);

        // Header: tiêu đề + nút X
        var header = new HBoxContainer();
        var title = new Label { Text = "NHIỆM VỤ", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 26);
        title.AddThemeColorOverride("font_color", UiKit.Accent);
        header.AddChild(title);

        var x = new Button { Text = "X", CustomMinimumSize = new Vector2(42, 42) };
        UiKit.StyleButton(x, UiKit.WoodDark, new Color(0.55f, 0.25f, 0.22f), new Color(0.40f, 0.18f, 0.16f));
        x.AddThemeFontSizeOverride("font_size", 18);
        x.Pressed += Close;
        header.AddChild(x);
        col.AddChild(header);

        col.AddChild(new HSeparator());

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        col.AddChild(scroll);

        _list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_list);
    }

    // ───────────────────────── Nội dung ─────────────────────────

    private void Refresh()
    {
        if (_list == null) return;
        foreach (Node c in _list.GetChildren()) c.QueueFree();

        var qm = QuestManager.Instance;
        if (qm == null || qm.Quests.Count == 0)
        {
            var empty = new Label { Text = "Chưa có nhiệm vụ nào.", HorizontalAlignment = HorizontalAlignment.Center };
            empty.AddThemeColorOverride("font_color", UiKit.WoodTextDim);
            _list.AddChild(empty);
            return;
        }

        AddSection("HÀNG NGÀY", "daily");
        AddSection("HÀNG TUẦN", "weekly");
    }

    private void AddSection(string title, string period)
    {
        var quests = QuestManager.Instance.ByPeriod(period);
        if (quests.Count == 0) return;

        var head = new Label { Text = title };
        head.AddThemeFontSizeOverride("font_size", 17);
        head.AddThemeColorOverride("font_color", UiKit.Accent);
        _list.AddChild(head);

        foreach (var q in quests) _list.AddChild(MakeQuestRow(q));
    }

    private Control MakeQuestRow(QuestEntry q)
    {
        var  qm      = QuestManager.Instance;
        int  count   = qm.GetCount(q.Id);
        bool done    = qm.IsCompleted(q);
        bool claimed = qm.IsClaimed(q.Id);

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.WoodCard, 10, UiKit.WoodBorder, 2, 12, 10));

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        card.AddChild(row);

        // Trái: tên + mô tả + thanh tiến độ + dòng thưởng
        var left = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        left.AddThemeConstantOverride("separation", 4);

        var name = new Label { Text = q.NameVi };
        name.AddThemeFontSizeOverride("font_size", 16);
        name.AddThemeColorOverride("font_color", UiKit.WoodText);
        left.AddChild(name);

        if (!string.IsNullOrEmpty(q.DescriptionVi))
        {
            var desc = new Label { Text = q.DescriptionVi, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            desc.AddThemeFontSizeOverride("font_size", 12);
            desc.AddThemeColorOverride("font_color", UiKit.WoodTextDim);
            left.AddChild(desc);
        }

        var bar = new ProgressBar
        {
            MaxValue          = q.Target,
            Value             = count,
            ShowPercentage    = false,
            CustomMinimumSize = new Vector2(0, 16),
        };
        bar.AddThemeStyleboxOverride("background", UiKit.Box(UiKit.WoodDark, 6));
        bar.AddThemeStyleboxOverride("fill",       UiKit.Box(done ? UiKit.BuyGreen : UiKit.Accent, 6));
        left.AddChild(bar);

        var info = new Label { Text = $"{count}/{q.Target}   {RewardText(q.Reward)}" };
        info.AddThemeFontSizeOverride("font_size", 12);
        info.AddThemeColorOverride("font_color", UiKit.WoodTextDim);
        left.AddChild(info);

        row.AddChild(left);

        // Phải: nút Nhận
        var claim = new Button { CustomMinimumSize = new Vector2(112, 0) };
        if (claimed)
        {
            claim.Text     = "Đã nhận ✓";
            claim.Disabled = true;
            UiKit.StyleButton(claim, UiKit.WoodDark, UiKit.WoodDark, UiKit.WoodDark, UiKit.WoodDark);
        }
        else if (done)
        {
            claim.Text = "Nhận";
            UiKit.StyleButton(claim, UiKit.BuyGreen, UiKit.BuyGreenHi, UiKit.BuyGreen);
            claim.Pressed += () => { QuestManager.Instance?.Claim(q.Id); Refresh(); };
        }
        else
        {
            claim.Text     = "Chưa xong";
            claim.Disabled = true;
            UiKit.StyleButton(claim, UiKit.WoodDark, UiKit.WoodDark, UiKit.WoodDark, UiKit.WoodDark);
        }
        claim.AddThemeFontSizeOverride("font_size", 14);

        var claimWrap = new CenterContainer();
        claimWrap.AddChild(claim);
        row.AddChild(claimWrap);

        return card;
    }

    private static string RewardText(QuestReward r)
    {
        if (r == null) return "";
        var parts = new List<string>();
        if (r.Exp     != 0) parts.Add($"+{r.Exp} EXP");
        if (r.Gold    != 0) parts.Add($"+{r.Gold} Vàng");
        if (r.MaThach != 0) parts.Add($"+{r.MaThach} Ma Thạch");
        if (!string.IsNullOrEmpty(r.ItemId) && r.ItemCount > 0) parts.Add($"+{r.ItemCount} {r.ItemId}");
        return parts.Count > 0 ? "· " + string.Join("  ", parts) : "";
    }
}
