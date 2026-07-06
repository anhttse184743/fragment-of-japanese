using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using FragmentOfJapanese.Autoloads;
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

    // Khung lấy từ ChallengeUi.tscn (chỉnh nền/panel/tiêu đề/đồng hồ/feedback trong editor).
    [Export] private Control       _root;
    [Export] private VBoxContainer _content;
    [Export] private Label         _title;
    [Export] private Label         _timer;
    [Export] private Label         _feedback;
    [Export] private PackedScene   _answerBtnScene;   // mẫu nút đáp án (AnswerButton.tscn)

    private readonly QuizEngine _engine = new();
    private readonly RandomNumberGenerator _rng = new();

    private Action<bool, double> _onDone;
    private double _remaining;
    private ulong  _startMs;
    private bool   _active;
    private bool   _timed;

    public override void _Ready()
    {
        Instance = this;        // Layer 128 + ProcessMode=Always đặt trong ChallengeUi.tscn
        _rng.Randomize();
        
        if (_root != null) 
        {
            _root.Visible = false;
            if (_root is PanelContainer pc)
            {
                pc.AddThemeStyleboxOverride("panel", 
                    UiKit.Box(new Color(0.24f, 0.17f, 0.13f, 0.98f), 24, new Color(0.4f, 0.28f, 0.2f, 1f), 4, 32, 32));
            }
        }
        
        if (_content != null)
        {
            _content.AddThemeConstantOverride("separation", 24);
        }
        
        if (_title != null)
        {
            _title.AddThemeFontSizeOverride("font_size", 32);
            _title.AddThemeColorOverride("font_color", UiKit.Accent);
        }

        if (_timer != null)
        {
            _timer.AddThemeFontSizeOverride("font_size", 46);
            _timer.AddThemeColorOverride("font_color", UiKit.Gold);
        }
        
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

        if (showMeaning)
        {
            var newWord = MkLabel("✨ TỪ MỚI", 24, UiKit.Gold);
            _content.AddChild(newWord);
            
            _content.AddChild(MkLabel(JapaneseDB.ToHiragana(target.Kana), 72, UiKit.WoodText));
            _content.AddChild(MkLabel($"Nghĩa: {target.MeaningVi}", 26, UiKit.Accent));
        }
        else
        {
            _content.AddChild(MkLabel(JapaneseDB.ToHiragana(target.Kana), 72, UiKit.WoodText));
        }
        
        var spacer = new Control { CustomMinimumSize = new Vector2(0, 12) };
        _content.AddChild(spacer);
        
        var warningLbl = MkLabel("⚠️ Trả lời sai có thể khiến quái vật sống lại!", 20, new Color(0.95f, 0.4f, 0.4f));
        _content.AddChild(warningLbl);
        
        var spacer2 = new Control { CustomMinimumSize = new Vector2(0, 16) };
        _content.AddChild(spacer2);

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
            if (!Begin("✨ KHÁM PHÁ NGỮ PHÁP", 0, false, onDone)) { onDone?.Invoke(false, 0); return; }
            
            _content.AddChild(MkLabel(JapaneseDB.ToHiragana(g.Pattern), 56, UiKit.WoodText));
            _content.AddChild(MkLabel(g.MeaningVi, 26, UiKit.Gold));
            
            var sep = new HSeparator();
            sep.AddThemeConstantOverride("separation", 16);
            sep.Modulate = new Color(1, 1, 1, 0.2f);
            _content.AddChild(sep);
            
            var expl = WrapLabel(g.ExplanationVi, UiKit.WoodTextDim);
            expl.AddThemeFontSizeOverride("font_size", 20);
            _content.AddChild(expl);
            
            var sep2 = new HSeparator();
            sep2.AddThemeConstantOverride("separation", 16);
            sep2.Modulate = new Color(1, 1, 1, 0.2f);
            _content.AddChild(sep2);
            
            var exJa = WrapLabel($"Ví dụ:\n{JapaneseDB.ToHiragana(g.ExampleJa)}", UiKit.Accent);
            exJa.AddThemeFontSizeOverride("font_size", 22);
            _content.AddChild(exJa);
            
            var exVi = WrapLabel(g.ExampleVi, UiKit.WoodTextDim);
            exVi.AddThemeFontSizeOverride("font_size", 18);
            _content.AddChild(exVi);
            
            var spacer = new Control { CustomMinimumSize = new Vector2(0, 16) };
            _content.AddChild(spacer);
            
            var btn = MkButton("⚔ Khắc Cốt Ghi Tâm!");
            btn.CustomMinimumSize = new Vector2(400, 70);
            btn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            btn.AddThemeFontSizeOverride("font_size", 26);
            UiKit.StyleButton(btn, new Color(0.6f, 0.25f, 0.25f, 1f), new Color(0.7f, 0.35f, 0.35f, 1f), new Color(0.5f, 0.2f, 0.2f, 1f), radius: 14);
            
            btn.Pressed += () => Finish(true);
            _content.AddChild(btn);
            
            Show();
            return;
        }

        var parts = (JapaneseDB.ToHiragana(g.ExampleJa) ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);

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
        
        var hiraganaChoices = BuildGrammarChoices(g, pool).Select(c => JapaneseDB.ToHiragana(c)).ToList();
        string hiraganaPattern = JapaneseDB.ToHiragana(g.Pattern);
        
        AddChoiceGrid(hiraganaChoices, ans => Finish(ans == hiraganaPattern));
        Show();
    }

    // ───────── READING (đoạn văn — KHÔNG hiện tiếng Việt ở mọi trường hợp) ─────────
    public void RunReading(ReadingPassage r, double timeoutSec, Action<bool, double> onDone)
    {
        if (r == null || r.Choices == null || r.Choices.Count == 0) { onDone?.Invoke(false, 0); return; }
        if (!Begin("📜  ĐỌC HIỂU", timeoutSec, true, onDone)) { onDone?.Invoke(false, 0); return; }

        var hiraganaChoices = r.Choices.Select(c => JapaneseDB.ToHiragana(c)).ToList();
        string correct = r.Answer >= 0 && r.Answer < hiraganaChoices.Count ? hiraganaChoices[r.Answer] : "";
        _content.AddChild(WrapLabel(JapaneseDB.ToHiragana(r.TextJa), Colors.White));   // chỉ tiếng Nhật
        _content.AddChild(WrapLabel(JapaneseDB.ToHiragana(r.QuestionJa), UiKit.Gold)); // câu hỏi (không VI)
        AddChoiceGrid(hiraganaChoices, ans => Finish(ans == correct));
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
            b.CustomMinimumSize = new Vector2(0, 56);
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
        int secs = Mathf.Max(0, Mathf.CeilToInt((float)_remaining));
        
        if (_timer != null)
        {
            _timer.Text = $"⏱ {secs}s";
            
            // Tăng kịch tính khi sắp hết giờ (dưới 5 giây)
            if (_remaining < 5.0)
            {
                _timer.AddThemeColorOverride("font_color", new Color(1f, 0.25f, 0.25f, 1f));
                // Hiệu ứng rung đập nhịp tim nhẹ
                float pulse = 46 + Mathf.Sin((float)_remaining * 12f) * 6f;
                _timer.AddThemeFontSizeOverride("font_size", (int)pulse);
            }
            else
            {
                _timer.AddThemeColorOverride("font_color", UiKit.Gold);
                _timer.AddThemeFontSizeOverride("font_size", 46);
            }
        }
        
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
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        grid.AddThemeConstantOverride("h_separation", 20);
        grid.AddThemeConstantOverride("v_separation", 20);
        foreach (var c in choices)
        {
            var b = MkButton(c);
            b.CustomMinimumSize = new Vector2(280, 70);
            string ans = c;
            b.Pressed += () => { if (_active) onPick(ans); };
            grid.AddChild(b);
        }
        _content.AddChild(grid);
    }

    private Button MkButton(string text)
    {
        var b = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        b.AddThemeFontSizeOverride("font_size", 24);
        b.AddThemeColorOverride("font_color", UiKit.WoodText);
        b.AddThemeColorOverride("font_hover_color", Colors.White);
        UiKit.StyleButton(b, UiKit.WoodCard, UiKit.Fade(UiKit.Accent, 0.3f), UiKit.Accent, radius: 14);
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

}
