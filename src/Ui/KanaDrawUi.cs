using Godot;
using System.Collections.Generic;
using FragmentOfJapanese.Core;
using FragmentOfJapanese.Autoloads;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// Màn luyện viết kana: hiện 1 hiragana → vẽ trên <see cref="DrawingCanvas"/> → ngừng 2 giây tự chấm
/// bằng <see cref="StrokeRecognizer"/> (kiểm hình + thứ tự + hướng nét) → ✓/✗ + nét sai → chữ kế.
/// Chạy qua TOÀN BỘ bảng hiragana (JapaneseDB.Hiragana); chữ nào có mẫu nét (HiraganaStrokes) thì chấm,
/// chưa có mẫu thì chỉ cho luyện. Mở dạng overlay bằng <see cref="Open"/> (vd từ thoại NPC pháp sư).
/// </summary>
public partial class KanaDrawUi : Control
{
    [Export] public float Threshold = 0.35f;

    private DrawingCanvas _canvas;
    private Label _prompt, _feedback;
    private readonly List<(string kana, string romaji, KanaStrokes strokes)> _set = new();
    private int _idx;

    /// <summary>Mở màn luyện viết kana dạng overlay (CanvasLayer phủ màn).</summary>
    public static void Open()
    {
        if (Engine.GetMainLoop() is not SceneTree tree) return;
        var layer = new CanvasLayer { Layer = 60, Name = "KanaDrawOverlay" };
        layer.AddChild(new KanaDrawUi());
        tree.Root.AddChild(layer);
    }

    public override void _Ready()
    {
        BuildUi();

        var db = JapaneseDB.Instance;
        if (db != null)
        {
            // map romaji → mẫu nét (chỉ một phần bảng có mẫu)
            var byRomaji = new Dictionary<string, KanaStrokes>();
            foreach (var ks in db.HiraganaStrokes) byRomaji[ks.Romaji] = ks;
            // chạy qua TOÀN BỘ bảng hiragana
            foreach (var h in db.Hiragana)
                _set.Add((h.Kana, h.Romaji, byRomaji.TryGetValue(h.Romaji, out var s) ? s : null));
        }

        if (_set.Count == 0) { _prompt.Text = "Chưa có bảng kana (JapaneseDB)."; return; }
        ShowChar();
    }

    private void ShowChar()
    {
        var e = _set[_idx % _set.Count];
        _prompt.Text = $"Vẽ:  {e.kana}   ({e.romaji})      [{(_idx % _set.Count) + 1}/{_set.Count}]";
        SetFeedback(e.strokes != null ? "Ngừng vẽ 2 giây để chấm."
                                      : "Chữ này chưa có mẫu chấm — cứ luyện rồi bấm → Tiếp.",
                    UiKit.TextDim);
        _canvas.Clear();
    }

    private void OnIdle()
    {
        var e = _set[_idx % _set.Count];
        if (e.strokes == null) return;   // chưa có mẫu nét → không chấm
        var res = StrokeRecognizer.Match(_canvas.GetStrokes(), e.strokes.ToStrokes(), Threshold);
        int score = Mathf.Clamp(100 - (int)(res.Score * 250), 0, 100);

        // Lưu tiến trình vẽ kana lên server (AttemptCount/BestScore/IsMastered do server tính).
        if (!string.IsNullOrEmpty(ApiClient.Instance.AccessToken))
            _ = ApiClient.Instance.PostAsync("/api/kana/practice",
                new { Kana = e.kana, KanaType = "Hiragana", Score = score });

        if (res.Pass)
            SetFeedback($"✓ Chuẩn!  ({score}/100)", UiKit.BuyGreenHi);
        else
            SetFeedback($"✗ {res.Message}  (độ khớp: {score}/100 — cần ≥ {Mathf.Clamp(100 - (int)(Threshold * 250), 0, 100)})", new Color(0.9f, 0.4f, 0.4f));
    }

    private void Close()
    {
        if (GetParent() is CanvasLayer layer) layer.QueueFree();   // overlay do Open() tạo
        else QueueFree();
    }

    private void SetFeedback(string t, Color c)
    {
        _feedback.Text = t;
        _feedback.AddThemeColorOverride("font_color", c);
    }

    private void BuildUi()
    {
        // CanvasLayer không có kích thước rõ ràng → phải đặt Size thủ công từ viewport
        var viewRect = GetViewport().GetVisibleRect();
        Position = Vector2.Zero;
        Size     = viewRect.Size;
        // Sau khi có Size, FullRect anchor mới hoạt động đúng cho các con
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var bg = new ColorRect { Color = new Color(0.08f, 0.09f, 0.12f, 0.97f) };   // modal: bắt click
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        center.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(center);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 14);
        center.AddChild(vb);

        var title = MkLabel("✍  Luyện viết Hiragana", 22, UiKit.Accent);
        vb.AddChild(title);

        _prompt = MkLabel("", 30, Colors.White);
        vb.AddChild(_prompt);

        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        panel.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.13f, 0.14f, 0.18f), 12, UiKit.Accent, 2));
        vb.AddChild(panel);
        _canvas = new DrawingCanvas { CustomMinimumSize = new Vector2(380, 380) };
        _canvas.Idle += OnIdle;
        panel.AddChild(_canvas);

        _feedback = MkLabel("", 18, UiKit.TextDim);
        vb.AddChild(_feedback);

        var hb = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        hb.AddThemeConstantOverride("separation", 12);
        vb.AddChild(hb);
        hb.AddChild(MkButton("✗ Xóa",  () => { _canvas.Clear(); SetFeedback("Ngừng vẽ 2 giây để chấm.", UiKit.TextDim); }));
        hb.AddChild(MkButton("→ Tiếp", () => { _idx++; ShowChar(); }));
        hb.AddChild(MkButton("✕ Đóng", Close));
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
