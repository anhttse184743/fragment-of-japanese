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

    /// <summary>Ảnh chân dung hiện bên trái (do NPC gán; rỗng = không có chân dung).</summary>
    public Texture2D Portrait { get; set; }
}

/// <summary>
/// Hộp thoại NPC (autoload = scene <c>res://scenes/ui/DialogueUi.tscn</c>). Gọi:  <c>DialogueUi.Instance.Start(node)</c>.
///  - Hiện ô thoại dưới màn hình + ẩn HUD (mọi node trong group "hud") khi đang thoại.
///  - Typewriter; CLICK / Enter / Space để TUA NHANH dòng đang gõ, lần nữa = sang dòng kế.
///  - Sau dòng cuối: hiện lựa chọn 1/2/3 (click hoặc bấm số). Lựa chọn có thể:
///      Next   → sang nhánh thoại khác
///      Action → chạy (vd <see cref="ShopUi"/> trao đổi) rồi đóng
///      (rỗng) → đóng thoại.
///  - Khi mở: chặn di chuyển/đánh của người chơi qua cờ tĩnh <see cref="Active"/>.
///
/// GIAO DIỆN nằm trong <c>DialogueUi.tscn</c> (chỉnh layout/font/màu trong editor) — file này CHỈ giữ logic
/// + tham chiếu node qua <c>[Export]</c>. Nút lựa chọn tạo lúc chạy nên màu của chúng vẫn ở đây (ChoiceX).
/// </summary>
public partial class DialogueUi : CanvasLayer
{
    public static DialogueUi Instance { get; private set; }

    /// <summary>Đang có hội thoại mở — PlayerController đọc cờ này để khoá điều khiển.</summary>
    public static bool Active { get; private set; }

    [Export] public float CharInterval { get; set; } = 0.02f;   // giây / ký tự (nhỏ = gõ nhanh)

    // ── Tham chiếu node (gắn trong DialogueUi.tscn) ──
    [Export] private Control        _root;
    [Export] private Button         _advance;        // nền phủ màn, bắt click "tua / tiếp"
    [Export] private PanelContainer _portraitFrame;  // khung chân dung (ẩn nếu NPC không có ảnh)
    [Export] private TextureRect    _portrait;
    [Export] private Label          _nameLabel;
    [Export] private Label          _textLabel;      // tiếng Nhật (gõ typewriter)
    [Export] private Label          _textViLabel;    // tiếng Việt (bản dịch)
    [Export] private Label          _hint;
    [Export] private VBoxContainer  _choiceBox;

    // Màu nút lựa chọn tạo lúc chạy (phần tĩnh chỉnh trong .tscn).
    private static readonly Color ChoiceBg     = new(0.16f, 0.15f, 0.23f);
    private static readonly Color ChoiceBorder = new(0.86f, 0.70f, 0.38f);
    private static readonly Color ChoiceText   = new(0.98f, 0.96f, 0.92f);
    private static readonly Color ChoiceHi     = new(1.00f, 0.84f, 0.45f);

    private enum Phase { Typing, LineDone, Choosing }

    private DialogueNode _node;
    private int          _lineIdx;
    private string       _full = "";
    private int          _shown;
    private float        _timer;
    private Phase        _phase;

    public override void _Ready()
    {
        Instance = this;
        if (_advance != null) _advance.Pressed += Advance;
        if (_root != null) _root.Visible = false;
    }

    // ───────────────────────── API ─────────────────────────

    public void Start(DialogueNode node)
    {
        if (node == null || _root == null) return;
        if (!_root.Visible)               // mở lần đầu
        {
            _root.Visible = true;
            Active        = true;
            SetHudVisible(false);
        }
        SetPortrait(node.Portrait);       // 1 NPC = 1 chân dung suốt đoạn thoại
        GoTo(node);
    }

    public void Close()
    {
        if (_root != null) _root.Visible = false;
        Active = false;
        _node  = null;
        ClearChoices();
        SetHudVisible(true);
    }

    private void SetPortrait(Texture2D tex)
    {
        if (_portrait == null) return;
        _portrait.Texture = tex;
        if (_portraitFrame != null) _portraitFrame.Visible = tex != null;
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
        if (_advance != null) _advance.Disabled = false;
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
        _phase = Phase.Choosing;
        if (_advance != null) _advance.Disabled = true;   // không cho "click-tiếp" khi đang chọn
        if (_hint != null) _hint.Visible = false;
        ClearChoices();

        for (int i = 0; i < _node.Choices.Length; i++)
        {
            int idx = i;
            var btn = new Button
            {
                Text              = $"{i + 1}.  {_node.Choices[i].Label}",
                CustomMinimumSize = new Vector2(340, 46),
                Alignment         = HorizontalAlignment.Left,
            };
            UiKit.StyleButton(btn, ChoiceBg, UiKit.Fade(ChoiceBorder, 0.28f), UiKit.Fade(ChoiceBorder, 0.42f));
            btn.AddThemeColorOverride("font_color",         ChoiceText);
            btn.AddThemeColorOverride("font_hover_color",   ChoiceHi);
            btn.AddThemeColorOverride("font_pressed_color", ChoiceHi);
            btn.AddThemeFontSizeOverride("font_size", 17);
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
