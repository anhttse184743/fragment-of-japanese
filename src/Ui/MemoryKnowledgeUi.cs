using Godot;
using System.Text;
using FragmentOfJapanese.Autoloads;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// Sổ tay kiến thức (Memory Knowledge) - Gắn vào NPC Hiragana/Sensei để ôn tập.
/// Hiển thị danh sách 25 bài học và chi tiết từ vựng/ngữ pháp/bài đọc.
/// Nằm trong file <c>scenes/ui/MemoryKnowledgeUi.tscn</c>.
/// </summary>
public partial class MemoryKnowledgeUi : Control
{
    [Export] private VBoxContainer _lessonListContainer;
    [Export] private RichTextLabel _contentLabel;
    [Export] private Button _closeBtn;
    [Export] private Label _lessonTitleLabel;

    public static void ShowUI()
    {
        var scene = GD.Load<PackedScene>("res://scenes/ui/MemoryKnowledgeUi.tscn");
        if (scene != null)
        {
            var inst = scene.Instantiate<MemoryKnowledgeUi>();
            if (Engine.GetMainLoop() is SceneTree tree)
            {
                var layer = new CanvasLayer { Layer = 150 };
                layer.AddChild(inst);
                tree.Root.AddChild(layer);
            }
        }
        else
        {
            GD.PrintErr("Không tìm thấy res://scenes/ui/MemoryKnowledgeUi.tscn");
        }
    }

    public override void _Ready()
    {
        if (_closeBtn != null)
        {
            _closeBtn.Pressed += Close;
            UiKit.StyleButton(_closeBtn, new Color(0.6f, 0.2f, 0.2f, 1f), new Color(0.7f, 0.3f, 0.3f, 1f), new Color(0.5f, 0.15f, 0.15f, 1f), radius: 12);
        }

        // Style the main panel
        var panel = GetNodeOrNull<Panel>("Panel");
        if (panel != null)
        {
            panel.AddThemeStyleboxOverride("panel",
                UiKit.Box(new Color(0.13f, 0.14f, 0.18f, 0.98f), 24, UiKit.WoodTextDim, 2, 24, 24));
        }

        if (_lessonTitleLabel != null)
        {
            _lessonTitleLabel.AutowrapMode = TextServer.AutowrapMode.Word;
        }

        PopulateLessonList();
    }

    private void PopulateLessonList()
    {
        if (_lessonListContainer == null) return;

        // Giả sử có 25 bài (1-25)
        for (int i = 1; i <= 25; i++)
        {
            int lessonNum = i;
            var btn = new Button { Text = $"BÀI {lessonNum}", CustomMinimumSize = new Vector2(0, 60) };
            
            // Highlight nếu có data hoặc tuỳ thuộc vào tiến trình (ví dụ: bài 1-3 đang học)
            var info = JapaneseDB.Instance.GetLesson(lessonNum);
            if (info != null)
            {
                UiKit.StyleButton(btn, UiKit.WoodCard, UiKit.Fade(UiKit.Accent, 0.4f), UiKit.Accent, radius: 8);
            }
            else
            {
                UiKit.StyleButton(btn, new Color(0.1f, 0.1f, 0.1f, 0.5f), new Color(0.2f, 0.2f, 0.2f, 0.8f), UiKit.Accent, radius: 8);
            }

            btn.FocusMode = FocusModeEnum.None;
            btn.Pressed += () => LoadLessonDetails(lessonNum);
            _lessonListContainer.AddChild(btn);
        }
    }

    private void LoadLessonDetails(int lesson)
    {
        var info = JapaneseDB.Instance.GetLesson(lesson);
        
        if (_lessonTitleLabel != null)
        {
            if (info != null)
                _lessonTitleLabel.Text = info.NameVi.ToUpper();
            else
                _lessonTitleLabel.Text = $"BÀI {lesson}";
        }

        if (_contentLabel == null) return;

        var sb = new StringBuilder();
        
        if (info == null)
        {
            sb.AppendLine("[i]Nội dung bài học này chưa được mở khóa hoặc chưa có dữ liệu.[/i]\n");
        }

        // TỪ VỰNG
        var vocab = JapaneseDB.Instance.GetByLesson(lesson);
        sb.AppendLine($"[color=#00BFFF][b]TỪ VỰNG ({vocab.Count})[/b][/color]");
        if (vocab.Count > 0)
        {
            foreach (var v in vocab)
            {
                string reading = string.IsNullOrEmpty(v.Kanji) ? v.Kana : $"{v.Kanji} ({v.Kana})";
                sb.AppendLine($"- [b]{reading}[/b]: {v.MeaningVi}");
            }
        }
        else
        {
            sb.AppendLine("Chưa có từ vựng.");
        }
        sb.AppendLine();

        // NGỮ PHÁP
        var grammar = JapaneseDB.Instance.GetGrammarByLesson(lesson);
        sb.AppendLine($"[color=#32CD32][b]NGỮ PHÁP ({grammar.Count})[/b][/color]");
        if (grammar.Count > 0)
        {
            foreach (var g in grammar)
            {
                sb.AppendLine($"[b]{g.Pattern}[/b]");
                sb.AppendLine($"[color=#CCCCCC]{g.MeaningVi}[/color]");
                sb.AppendLine($"[color=#AAAAAA][i]{g.ExplanationVi}[/i][/color]");
            }
        }
        else
        {
            sb.AppendLine("Chưa có ngữ pháp.");
        }
        sb.AppendLine();

        // BÀI ĐỌC
        var readings = JapaneseDB.Instance.GetReadingsByLesson(lesson);
        sb.AppendLine($"[color=#FF69B4][b]BÀI ĐỌC ({readings.Count})[/b][/color]");
        if (readings.Count > 0)
        {
            int i = 1;
            foreach (var r in readings)
            {
                string snippet = r.TextJa.Length > 15 ? r.TextJa.Substring(0, 15) + "..." : r.TextJa;
                sb.AppendLine($"[b]Đoạn {i++}:[/b] {snippet}");
            }
        }
        else
        {
            sb.AppendLine("Chưa có bài đọc.");
        }

        _contentLabel.Text = sb.ToString();
    }

    private void Close()
    {
        if (GetParent() is CanvasLayer layer)
            layer.QueueFree();
        else
            QueueFree();
    }
}
