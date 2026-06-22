using Godot;
using System.Collections.Generic;
using FragmentOfJapanese.Core;
using FragmentOfJapanese.Autoloads;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// Màn luyện viết kana: hiện 1 hiragana → vẽ trên <see cref="DrawingCanvas"/> → ngừng 2 giây tự chấm
/// bằng <see cref="StrokeRecognizer"/> (kiểm hình + thứ tự + hướng nét) → ✓/✗ + nét sai → chữ kế.
/// </summary>
public partial class KanaDrawUi : Control
{
    [Export] public float Threshold = 0.22f;

    private DrawingCanvas _canvas;
    private Label _prompt, _feedback;
    private readonly List<KanaStrokes> _set = new();
    private int _idx;

    public override void _Ready()
    {
        BuildUi();
        if (JapaneseDB.Instance != null)
            foreach (var k in JapaneseDB.Instance.HiraganaStrokes) _set.Add(k);

        if (_set.Count == 0) { _prompt.Text = "Chưa có mẫu nét (data/japanese/hiragana_strokes.json)"; return; }
        ShowChar();
    }

    private void ShowChar()
    {
        var k = _set[_idx % _set.Count];
        _prompt.Text = $"Vẽ:  {KanaGlyph(k.Romaji)}   ({k.Romaji})";
        SetFeedback("Ngừng vẽ 2 giây để chấm.", UiKit.TextDim);
        _canvas.Clear();
    }

    private void OnIdle()
    {
        var k = _set[_idx % _set.Count];
        var res = StrokeRecognizer.Match(_canvas.GetStrokes(), k.ToStrokes(), Threshold);
        SetFeedback(res.Pass ? "✓ Chuẩn!" : "✗ " + res.Message,
                    res.Pass ? UiKit.BuyGreenHi : new Color(0.9f, 0.4f, 0.4f));
        // (tùy chọn Pha sau) ghi tiến độ kana qua LearningTracker.
    }

    private string KanaGlyph(string romaji)
    {
        var db = JapaneseDB.Instance;
        if (db != null) foreach (var h in db.Hiragana) if (h.Romaji == romaji) return h.Kana;
        return romaji;
    }

    private void SetFeedback(string t, Color c)
    {
        _feedback.Text = t;
        _feedback.AddThemeColorOverride("font_color", c);
    }

    private void BuildUi()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);

        var bg = new ColorRect { Color = new Color(0.08f, 0.09f, 0.12f), MouseFilter = MouseFilterEnum.Ignore };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        center.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(center);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 14);
        center.AddChild(vb);

        _prompt = MkLabel("", 30, Colors.White);
        vb.AddChild(_prompt);

        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.13f, 0.14f, 0.18f), 12, UiKit.Accent, 2));
        vb.AddChild(panel);
        _canvas = new DrawingCanvas { CustomMinimumSize = new Vector2(380, 380) };
        _canvas.Idle += OnIdle;
        panel.AddChild(_canvas);

        _feedback = MkLabel("", 18, UiKit.TextDim);
        vb.AddChild(_feedback);

        var hb = new HBoxContainer();
        hb.AddThemeConstantOverride("separation", 12);
        hb.Alignment = BoxContainer.AlignmentMode.Center;
        vb.AddChild(hb);
        hb.AddChild(MkButton("✗ Xóa",  () => { _canvas.Clear(); SetFeedback("Ngừng vẽ 2 giây để chấm.", UiKit.TextDim); }));
        hb.AddChild(MkButton("→ Tiếp", () => { _idx++; ShowChar(); }));
    }

    private static Label MkLabel(string text, int size, Color color)
    {
        var l = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        return l;
    }

    private static Button MkButton(string text, System.Action onPressed)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(140, 48) };
        b.AddThemeFontSizeOverride("font_size", 18);
        UiKit.StyleButton(b, UiKit.CardBg, UiKit.Fade(UiKit.Accent, 0.5f), UiKit.Accent);
        b.Pressed += onPressed;
        return b;
    }
}
