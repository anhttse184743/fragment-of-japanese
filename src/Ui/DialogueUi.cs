using System;
using Godot;

namespace FragmentOfJapanese.Ui;

/// <summary>Một lựa chọn trong hội thoại (hiện dạng "1. ...", "2. ...").</summary>
public sealed class DialogueChoice
{
    public string Label { get; init; } = "";

    /// <summary>Chọn → sang nhánh thoại này (ưu tiên trước <see cref="Action"/>).</summary>
    public DialogueNode Next { get; init; }

    /// <summary>Chọn → chạy hành động (vd mở cửa hàng) rồi đóng thoại.</summary>
    public Action Action { get; init; }
}

/// <summary>Một dòng thoại song ngữ: Nhật (gõ typewriter, to) + Việt (bản dịch, nhỏ).</summary>
public sealed class DialogueLine
{
    public string Ja { get; init; } = "";
    public string Vi { get; init; } = "";
    public DialogueLine() { }
    public DialogueLine(string ja, string vi = "") { Ja = ja; Vi = vi; }
}

/// <summary>Một "nút" hội thoại: vài dòng thoại + (tùy chọn) lựa chọn ở cuối.</summary>
public sealed class DialogueNode
{
    public string         Speaker { get; init; } = "";
    public DialogueLine[] Lines   { get; init; } = Array.Empty<DialogueLine>();

    /// <summary>Hiện sau dòng cuối. Rỗng = hết thoại (đóng).</summary>
    public DialogueChoice[] Choices { get; init; } = Array.Empty<DialogueChoice>();

    /// <summary>Chạy khi hết dòng (chỉ khi KHÔNG có Choices) rồi đóng — vd mở shop sau lời đáp.</summary>
    public Action OnEnd { get; init; }
}

/// <summary>
/// Hộp thoại NPC (autoload). Gọi:  <c>DialogueUi.Instance.Start(node)</c>.
///  - Hiện ô thoại dưới màn hình + ẩn HUD (mọi node trong group "hud") khi đang thoại.
///  - Typewriter; CLICK / Enter / Space để TUA NHANH dòng đang gõ, lần nữa = sang dòng kế.
///  - Sau dòng cuối: hiện lựa chọn 1/2/3 (click hoặc bấm số). Lựa chọn có thể:
///      Next   → sang nhánh thoại khác
///      Action → chạy (vd <see cref="ShopUi"/> trao đổi) rồi đóng
///      (rỗng) → đóng thoại.
///  - Khi mở: chặn di chuyển/đánh của người chơi qua cờ tĩnh <see cref="Active"/>.
/// </summary>
public partial class DialogueUi : CanvasLayer
{
    public static DialogueUi Instance { get; private set; }

    /// <summary>Đang có hội thoại mở — PlayerController đọc cờ này để khoá điều khiển.</summary>
    public static bool Active { get; private set; }

    [Export] public float CharInterval { get; set; } = 0.02f;   // giây / ký tự

    private enum Phase { Typing, LineDone, Choosing }

    private Control       _root;
    private Button        _advance;     // nền phủ màn, bắt click "tua / tiếp"
    private Label         _nameLabel;
    private Label         _textLabel;     // tiếng Nhật (gõ typewriter)
    private Label         _textViLabel;   // tiếng Việt (bản dịch)
    private Label         _hint;
    private VBoxContainer _choiceBox;

    private DialogueNode _node;
    private int          _lineIdx;
    private string       _full = "";
    private int          _shown;
    private float        _timer;
    private Phase        _phase;

    public override void _Ready()
    {
        Instance = this;
        Layer    = 40;        // trên HUD/shop (11), dưới ConfirmUi (50)
        BuildUi();
        _root.Visible = false;
    }

    // ───────────────────────── API ─────────────────────────

    public void Start(DialogueNode node)
    {
        if (node == null) return;
        if (!_root.Visible)               // mở lần đầu
        {
            _root.Visible = true;
            Active        = true;
            SetHudVisible(false);
        }
        GoTo(node);
    }

    public void Close()
    {
        _root.Visible = false;
        Active        = false;
        _node         = null;
        ClearChoices();
        SetHudVisible(true);
    }

    // ───────────────────────── Luồng thoại ─────────────────────────

    private void GoTo(DialogueNode node)
    {
        _node    = node;
        _lineIdx = 0;
        if (_nameLabel != null) _nameLabel.Text = node.Speaker;
        ClearChoices();
        ShowLine();
    }

    private void ShowLine()
    {
        var line = _lineIdx < _node.Lines.Length ? _node.Lines[_lineIdx] : null;
        // ưu tiên gõ tiếng Nhật; dòng chỉ có tiếng Việt thì gõ tiếng Việt
        string main = line == null ? "" : (!string.IsNullOrEmpty(line.Ja) ? line.Ja : line.Vi);
        string sub  = line != null && !string.IsNullOrEmpty(line.Ja) ? line.Vi : "";

        _full  = main ?? "";
        _shown = 0;
        _timer = 0f;
        _phase = Phase.Typing;
        if (_textLabel != null) _textLabel.Text = "";
        if (_textViLabel != null)
        {
            _textViLabel.Text    = sub;
            _textViLabel.Visible = !string.IsNullOrEmpty(sub);
        }
        if (_hint != null) _hint.Visible = false;
        _advance.Disabled = false;
    }

    public override void _Process(double delta)
    {
        if (!Active || _phase != Phase.Typing) return;

        _timer += (float)delta;
        while (_timer >= CharInterval && _shown < _full.Length)
        {
            _shown++;
            _timer -= CharInterval;
        }
        if (_textLabel != null) _textLabel.Text = _full.Substring(0, _shown);
        if (_shown >= _full.Length) FinishTyping();
    }

    private void FinishTyping()
    {
        _shown = _full.Length;
        if (_textLabel != null) _textLabel.Text = _full;
        _phase = Phase.LineDone;
        if (_hint != null) _hint.Visible = true;
    }

    /// <summary>Click / Enter: tua nhanh dòng đang gõ, hoặc sang dòng kế / hiện lựa chọn.</summary>
    private void Advance()
    {
        if (_node == null) return;
        if (_phase == Phase.Typing) { FinishTyping(); return; }
        if (_phase != Phase.LineDone) return;

        _lineIdx++;
        if (_lineIdx < _node.Lines.Length) { ShowLine(); return; }

        if (_node.Choices is { Length: > 0 }) { ShowChoices(); return; }

        // hết dòng, không lựa chọn → chạy OnEnd (nếu có) rồi đóng
        var end = _node.OnEnd;
        Close();
        end?.Invoke();
    }

    private void ShowChoices()
    {
        _phase            = Phase.Choosing;
        _advance.Disabled = true;          // không cho "click-tiếp" khi đang chọn
        if (_hint != null) _hint.Visible = false;
        ClearChoices();

        for (int i = 0; i < _node.Choices.Length; i++)
        {
            int idx = i;
            var btn = new Button
            {
                Text              = $"{i + 1}.  {_node.Choices[i].Label}",
                CustomMinimumSize = new Vector2(320, 44),
                Alignment         = HorizontalAlignment.Left,
            };
            UiKit.StyleButton(btn, UiKit.WoodCard, UiKit.Fade(UiKit.Accent, 0.22f), UiKit.Accent);
            btn.AddThemeColorOverride("font_color", UiKit.WoodText);
            btn.Pressed += () => Choose(idx);
            _choiceBox.AddChild(btn);
            if (i == 0) btn.GrabFocus();
        }
        _choiceBox.Visible = true;
    }

    private void Choose(int i)
    {
        if (_node?.Choices == null || i < 0 || i >= _node.Choices.Length) return;
        var c = _node.Choices[i];

        if (c.Next != null) { GoTo(c.Next); return; }

        // Action / đóng: đóng thoại TRƯỚC rồi mới chạy (để UI khác mở sạch sẽ)
        var act = c.Action;
        Close();
        act?.Invoke();
    }

    private void ClearChoices()
    {
        if (_choiceBox == null) return;
        foreach (Node n in _choiceBox.GetChildren()) n.QueueFree();
        _choiceBox.Visible = false;
    }

    // ───────────────────────── Input ─────────────────────────

    public override void _Input(InputEvent ev)
    {
        if (!Active) return;

        // nuốt phím E để không mở lại thoại khi đang thoại
        if (ev.IsActionPressed("interact")) { GetViewport().SetInputAsHandled(); return; }

        if (ev.IsActionPressed("ui_cancel")) { Close(); GetViewport().SetInputAsHandled(); return; }

        if (_phase == Phase.Choosing)
        {
            if (ev is InputEventKey { Pressed: true, Echo: false } k)
            {
                int pick = k.Keycode switch
                {
                    Key.Key1 or Key.Kp1 => 0,
                    Key.Key2 or Key.Kp2 => 1,
                    Key.Key3 or Key.Kp3 => 2,
                    Key.Key4 or Key.Kp4 => 3,
                    _                   => -1,
                };
                if (pick >= 0 && pick < _node.Choices.Length)
                {
                    Choose(pick);
                    GetViewport().SetInputAsHandled();
                }
            }
            return;   // khi đang chọn: để nút focus tự xử Enter
        }

        if (ev.IsActionPressed("ui_accept")) { Advance(); GetViewport().SetInputAsHandled(); }
    }

    // ───────────────────────── Dựng UI ─────────────────────────

    private void BuildUi()
    {
        _root = new Control { Name = "DialogueRoot" };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(_root);

        // nền phủ toàn màn, bắt click "tua / tiếp" — nằm DƯỚI ô thoại & lựa chọn
        _advance = new Button { FocusMode = Control.FocusModeEnum.None };
        _advance.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var clear = UiKit.Box(new Color(0, 0, 0, 0));
        _advance.AddThemeStyleboxOverride("normal",  clear);
        _advance.AddThemeStyleboxOverride("hover",   clear);
        _advance.AddThemeStyleboxOverride("pressed", clear);
        _advance.AddThemeStyleboxOverride("focus",   clear);
        _advance.Pressed += Advance;
        _root.AddChild(_advance);

        // cụm dưới màn hình: [lựa chọn] trên [ô thoại]
        var anchor = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        anchor.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        anchor.OffsetTop    = -320;
        anchor.OffsetBottom = -28;
        anchor.OffsetLeft   = 40;
        anchor.OffsetRight  = -40;
        _root.AddChild(anchor);

        var col = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Alignment   = BoxContainer.AlignmentMode.End,   // dồn xuống đáy (ô thoại sát đáy)
        };
        col.AddThemeConstantOverride("separation", 10);
        anchor.AddChild(col);

        _choiceBox = new VBoxContainer { Visible = false, SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
        _choiceBox.AddThemeConstantOverride("separation", 6);
        col.AddChild(_choiceBox);

        // panel + nội dung để Ignore chuột → click trên ô thoại vẫn rơi xuống _advance (tua/tiếp)
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 150),
            MouseFilter       = Control.MouseFilterEnum.Ignore,
        };
        panel.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.WoodPanel, 14, UiKit.WoodBorder, 3, 22, 18));
        col.AddChild(panel);

        var pv = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        pv.AddThemeConstantOverride("separation", 8);
        panel.AddChild(pv);

        _nameLabel = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        _nameLabel.AddThemeFontSizeOverride("font_size", 22);
        _nameLabel.AddThemeColorOverride("font_color", UiKit.Accent);
        pv.AddChild(_nameLabel);

        _textLabel = new Label
        {
            AutowrapMode      = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 46),
            VerticalAlignment = VerticalAlignment.Top,
            MouseFilter       = Control.MouseFilterEnum.Ignore,
        };
        _textLabel.AddThemeFontSizeOverride("font_size", 24);     // tiếng Nhật: to hơn
        _textLabel.AddThemeColorOverride("font_color", UiKit.WoodText);
        pv.AddChild(_textLabel);

        _textViLabel = new Label
        {
            AutowrapMode      = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 28),
            VerticalAlignment = VerticalAlignment.Top,
            MouseFilter       = Control.MouseFilterEnum.Ignore,
        };
        _textViLabel.AddThemeFontSizeOverride("font_size", 17);   // tiếng Việt: nhỏ, mờ
        _textViLabel.AddThemeColorOverride("font_color", UiKit.WoodTextDim);
        pv.AddChild(_textViLabel);

        _hint = new Label
        {
            Text                = "▼  Nhấp để tiếp",
            HorizontalAlignment = HorizontalAlignment.Right,
            Visible             = false,
            MouseFilter         = Control.MouseFilterEnum.Ignore,
        };
        _hint.AddThemeFontSizeOverride("font_size", 14);
        _hint.AddThemeColorOverride("font_color", UiKit.WoodTextDim);
        pv.AddChild(_hint);
    }

    /// <summary>Ẩn/hiện mọi UI thuộc group "hud" (HUD, joystick...) khi vào/ra thoại.</summary>
    private void SetHudVisible(bool visible)
    {
        foreach (Node n in GetTree().GetNodesInGroup("hud"))
        {
            if      (n is CanvasItem  ci) ci.Visible = visible;
            else if (n is CanvasLayer cl) cl.Visible = visible;
        }
    }
}
