using Godot;
using System;
using System.Collections.Generic;
using FragmentOfJapanese.Core;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// Overlay mini-game học khi kết liễu quái (autoload). Đóng băng game (pause tree) khi hỏi,
/// đếm giờ, trả (đúng/sai + thời gian) qua callback. ProcessMode=Always để chạy lúc pause.
/// Nội dung dựng ĐỘNG vào _content: Quiz (vocab) + Grammar (thẻ học / trắc nghiệm / ghép câu).
/// LUÔN bỏ pause + TimeScale=1 khi đóng (không kẹt pause).
/// </summary>
public partial class ChallengeUi : CanvasLayer
{
    public static ChallengeUi Instance { get; private set; }

    private Control        _root;
    private VBoxContainer  _content;
    private Label          _title, _timer, _feedback;
    private readonly QuizEngine _engine = new();
    private readonly RandomNumberGenerator _rng = new();

    private Action<bool, double> _onDone;
    private double _remaining;
    private ulong  _startMs;
    private bool   _active;
    private bool   _timed;

    public override void _Ready()
    {
        Instance    = this;
        Layer       = 128;
        ProcessMode = ProcessModeEnum.Always;
        _rng.Randomize();
        BuildShell();
        _root.Visible = false;
        SetProcess(false);
    }

    // ───────── QUIZ (từ vựng) ─────────
    public void RunQuiz(VocabularyEntry target, IReadOnlyList<VocabularyEntry> pool, bool showMeaning,
                        double timeoutSec, Action<bool, double> onDone)
    {
        if (target == null) { onDone?.Invoke(false, 0); return; }
        if (!Begin("⚔  KẾT LIỄU", timeoutSec, true, onDone)) { onDone?.Invoke(false, 0); return; }

        var q = _engine.Generate(target, pool, QuizType.KanaToMeaning);
        string correct = q.CorrectAnswer;

        _content.AddChild(MkLabel(target.Kana, 50, Colors.White));
        if (showMeaning) _content.AddChild(MkLabel($"nghĩa: {target.MeaningVi}", 18, UiKit.TextDim));
        AddChoiceGrid(q.Choices, ans => Finish(ans == correct));
        Show();
    }

    // ───────── GRAMMAR (3 chế độ theo Seen) ─────────
    public void RunGrammar(GrammarPoint g, int stage, IReadOnlyList<GrammarPoint> pool,
                           double timeoutSec, Action<bool, double> onDone)
    {
        if (g == null) { onDone?.Invoke(false, 0); return; }

        // stage 0: THẺ HỌC — xem + mở khóa, diệt 1 phát (luôn đúng)
        if (stage <= 0)
        {
            if (!Begin("📖  NGỮ PHÁP MỚI", 0, false, onDone)) { onDone?.Invoke(false, 0); return; }
            _content.AddChild(MkLabel(g.Pattern, 32, Colors.White));
            _content.AddChild(MkLabel(g.MeaningVi, 18, UiKit.Gold));
            _content.AddChild(WrapLabel(g.ExplanationVi, UiKit.BrownTextDim));
            _content.AddChild(WrapLabel($"例: {g.ExampleJa}\n     {g.ExampleVi}", UiKit.TextDim));
            var btn = MkButton("⚔  Khắc cốt — Diệt!");
            btn.Pressed += () => Finish(true);
            _content.AddChild(btn);
            Show();
            return;
        }

        var parts = (g.ExampleJa ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // stage ≥ 2 và câu tách được ≥ 2 phần: GHÉP CÂU
        if (stage >= 2 && parts.Length >= 2)
        {
            if (!Begin("📖  GHÉP CÂU", timeoutSec, true, onDone)) { onDone?.Invoke(false, 0); return; }
            _content.AddChild(MkLabel($"Ghép câu — nghĩa: {g.ExampleVi}", 18, UiKit.TextDim));
            BuildArrange(parts);
            Show();
            return;
        }

        // stage 1 (hoặc fallback): TRẮC NGHIỆM chọn mẫu câu
        if (!Begin("📖  NGỮ PHÁP", timeoutSec, true, onDone)) { onDone?.Invoke(false, 0); return; }
        _content.AddChild(WrapLabel($"Mẫu câu nào diễn đạt: \"{g.MeaningVi}\"?", Colors.White));
        AddChoiceGrid(BuildGrammarChoices(g, pool), ans => Finish(ans == g.Pattern));
        Show();
    }

    // ───────── READING (đoạn văn — KHÔNG hiện tiếng Việt ở mọi trường hợp) ─────────
    public void RunReading(ReadingPassage r, double timeoutSec, Action<bool, double> onDone)
    {
        if (r == null || r.Choices == null || r.Choices.Count == 0) { onDone?.Invoke(false, 0); return; }
        if (!Begin("📜  ĐỌC HIỂU", timeoutSec, true, onDone)) { onDone?.Invoke(false, 0); return; }

        string correct = r.Answer >= 0 && r.Answer < r.Choices.Count ? r.Choices[r.Answer] : "";
        _content.AddChild(WrapLabel(r.TextJa, Colors.White));   // chỉ tiếng Nhật
        _content.AddChild(WrapLabel(r.QuestionJa, UiKit.Gold)); // câu hỏi (không VI)
        AddChoiceGrid(r.Choices, ans => Finish(ans == correct));
        Show();
    }

    // ───────── DRAW (vẽ chữ kana) ─────────
    public void RunDraw(KanaStrokes target, string glyph, double timeoutSec, Action<bool, double> onDone)
    {
        if (target == null) { onDone?.Invoke(false, 0); return; }
        if (!Begin("✍  VẼ CHỮ", timeoutSec, true, onDone)) { onDone?.Invoke(false, 0); return; }

        _content.AddChild(MkLabel($"Vẽ chữ:  {glyph}   ({target.Romaji})", 26, Colors.White));
        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        panel.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.13f, 0.14f, 0.18f), 12, UiKit.Accent, 2));
        var canvas = new DrawingCanvas { CustomMinimumSize = new Vector2(320, 320) };
        panel.AddChild(canvas);
        _content.AddChild(panel);

        canvas.Idle += () => Finish(StrokeRecognizer.Match(canvas.GetStrokes(), target.ToStrokes()).Pass);
        Show();
    }

    private List<string> BuildGrammarChoices(GrammarPoint g, IReadOnlyList<GrammarPoint> pool)
    {
        var choices = new List<string> { g.Pattern };
        var seen = new HashSet<string> { g.Pattern };
        if (pool != null)
        {
            var idx = new List<int>();
            for (int i = 0; i < pool.Count; i++) idx.Add(i);
            Shuffle(idx);
            foreach (var i in idx)
            {
                if (seen.Add(pool[i].Pattern)) choices.Add(pool[i].Pattern);
                if (choices.Count == 4) break;
            }
        }
        Shuffle(choices);
        return choices;
    }

    private void BuildArrange(string[] parts)
    {
        var built = new List<string>();
        var assembled = MkLabel("…", 24, Colors.White);
        assembled.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        var flow = new HFlowContainer();
        flow.AddThemeConstantOverride("h_separation", 8);
        flow.AddThemeConstantOverride("v_separation", 8);

        var order = new List<int>();
        for (int i = 0; i < parts.Length; i++) order.Add(i);
        Shuffle(order);

        var buttons = new List<Button>();
        foreach (var i in order)
        {
            var b = MkButton(parts[i]);
            b.CustomMinimumSize = new Vector2(0, 50);
            string part = parts[i];
            b.Pressed += () =>
            {
                if (!_active) return;
                b.Disabled = true;
                built.Add(part);
                assembled.Text = string.Join(" ", built);
                if (built.Count == parts.Length)
                    Finish(string.Join(" ", built) == string.Join(" ", parts));
            };
            buttons.Add(b);
            flow.AddChild(b);
        }

        var reset = MkButton("↺ Làm lại");
        reset.Pressed += () =>
        {
            if (!_active) return;
            built.Clear();
            assembled.Text = "…";
            foreach (var b in buttons) b.Disabled = false;
        };

        _content.AddChild(assembled);
        _content.AddChild(flow);
        _content.AddChild(reset);
    }

    // ───────── khung chung ─────────
    private bool Begin(string title, double timeoutSec, bool timed, Action<bool, double> onDone)
    {
        if (_active) return false;
        _active    = true;
        _onDone    = onDone;
        _timed     = timed;
        _remaining = timeoutSec;
        _startMs   = Time.GetTicksMsec();
        _title.Text    = title;
        _feedback.Text = "";
        _timer.Visible = timed;
        ClearContent();
        return true;
    }

    private void Show()
    {
        _root.Visible = true;
        SetProcess(_timed);
        GetTree().Paused = true;     // ĐÓNG BĂNG
    }

    public override void _Process(double delta)
    {
        if (!_active || !_timed) return;
        _remaining -= delta;
        _timer.Text = $"⏱ {Mathf.Max(0, Mathf.CeilToInt((float)_remaining))}s";
        if (_remaining <= 0) Finish(false);
    }

    private async void Finish(bool correct)
    {
        if (!_active) return;
        _active = false;
        SetProcess(false);
        double ms = Time.GetTicksMsec() - _startMs;

        _feedback.Text = correct ? "✓ Chính xác!" : "✗ Sai rồi!";
        _feedback.AddThemeColorOverride("font_color", correct ? UiKit.BuyGreenHi : new Color(0.9f, 0.4f, 0.4f));

        await ToSignal(GetTree().CreateTimer(0.9, true, false, true), SceneTreeTimer.SignalName.Timeout);

        _root.Visible    = false;
        GetTree().Paused = false;    // LUÔN bỏ băng
        Engine.TimeScale = 1.0;

        var cb = _onDone; _onDone = null;
        cb?.Invoke(correct, ms);
    }

    // ───────── widget helpers ─────────
    private void ClearContent()
    {
        foreach (var c in _content.GetChildren()) { _content.RemoveChild(c); c.QueueFree(); }
    }

    private void AddChoiceGrid(IReadOnlyList<string> choices, Action<string> onPick)
    {
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 12);
        foreach (var c in choices)
        {
            var b = MkButton(c);
            b.CustomMinimumSize = new Vector2(250, 56);
            string ans = c;
            b.Pressed += () => { if (_active) onPick(ans); };
            grid.AddChild(b);
        }
        _content.AddChild(grid);
    }

    private Button MkButton(string text)
    {
        var b = new Button { Text = text };
        b.AddThemeFontSizeOverride("font_size", 20);
        UiKit.StyleButton(b, UiKit.CardBg, UiKit.Fade(UiKit.Accent, 0.5f), UiKit.Accent);
        return b;
    }

    private static Label MkLabel(string text, int size, Color color)
    {
        var l = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        return l;
    }

    private static Label WrapLabel(string text, Color color)
    {
        var l = MkLabel(text, 17, color);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.CustomMinimumSize = new Vector2(560, 0);
        return l;
    }

    private void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = (int)(_rng.Randi() % (uint)(i + 1));
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void BuildShell()
    {
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f), MouseFilter = Control.MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(center);

        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.PanelBg, 16, UiKit.Accent, 2));
        center.AddChild(panel);

        var margin = new MarginContainer();
        foreach (var s in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(s, 28);
        panel.AddChild(margin);

        var vb = new VBoxContainer { CustomMinimumSize = new Vector2(580, 0) };
        vb.AddThemeConstantOverride("separation", 14);
        margin.AddChild(vb);

        _title = MkLabel("", 20, UiKit.Accent);
        vb.AddChild(_title);

        _content = new VBoxContainer();
        _content.AddThemeConstantOverride("separation", 12);
        vb.AddChild(_content);

        _timer = MkLabel("", 18, UiKit.Gold);
        vb.AddChild(_timer);

        _feedback = MkLabel("", 18, UiKit.TextDim);
        vb.AddChild(_feedback);
    }
}
