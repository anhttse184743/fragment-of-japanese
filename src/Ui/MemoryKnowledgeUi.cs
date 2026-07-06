using Godot;
using System.Linq;
using System.Text;
using FragmentOfJapanese.Autoloads;
using FragmentOfJapanese.Core;
using FragmentOfJapanese.Learning;

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

        var lt = LearningTracker.Instance;

        // Chỉ mở bài ĐÃ MỞ KHÓA (đã học tới); bài chưa tới → khóa, không xem được.
        for (int i = 1; i <= 25; i++)
        {
            int lessonNum = i;
            bool unlocked = lt == null ? lessonNum == 1 : lt.IsUnlocked(lessonNum);

            var btn = new Button
            {
                Text = unlocked ? $"BÀI {lessonNum}" : $"🔒 BÀI {lessonNum}",
                CustomMinimumSize = new Vector2(0, 60),
                Disabled = !unlocked,
                FocusMode = FocusModeEnum.None,
            };

            if (unlocked)
                UiKit.StyleButton(btn, UiKit.WoodCard, UiKit.Fade(UiKit.Accent, 0.4f), UiKit.Accent, radius: 8);
            else
                UiKit.StyleButton(btn, new Color(0.1f, 0.1f, 0.1f, 0.5f), new Color(0.2f, 0.2f, 0.2f, 0.8f), UiKit.Accent, radius: 8);

            if (unlocked) btn.Pressed += () => LoadLessonDetails(lessonNum);
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

        var lt = LearningTracker.Instance;
        // Chỉ hiện mục ĐÃ HỌC (đã ghi nhận qua LearningTracker). Chưa học → không hiện.
        bool Learned(ItemKind k, string id) => lt != null && !lt.IsNew(k, id);

        var sb = new StringBuilder();

        // TỪ VỰNG (đã học)
        var vocabAll = JapaneseDB.Instance.GetByLesson(lesson);
        var vocab = vocabAll.Where(v => Learned(ItemKind.Vocab, v.Id)).ToList();
        sb.AppendLine($"[color=#00BFFF][b]TỪ VỰNG (đã học {vocab.Count}/{vocabAll.Count})[/b][/color]");
        if (vocab.Count > 0)
        {
            foreach (var v in vocab)
            {
                string reading = string.IsNullOrEmpty(v.Kanji) ? JapaneseDB.ToHiragana(v.Kana) : $"{v.Kanji} ({JapaneseDB.ToHiragana(v.Kana)})";
                sb.AppendLine($"- [b]{reading}[/b]: {v.MeaningVi}");
            }
        }
        else
        {
            sb.AppendLine("[i]Chưa học từ nào trong bài này.[/i]");
        }
        sb.AppendLine();

        // NGỮ PHÁP (đã học)
        var grammarAll = JapaneseDB.Instance.GetGrammarByLesson(lesson);
        var grammar = grammarAll.Where(g => Learned(ItemKind.Grammar, g.Id)).ToList();
        sb.AppendLine($"[color=#32CD32][b]NGỮ PHÁP (đã học {grammar.Count}/{grammarAll.Count})[/b][/color]");
        if (grammar.Count > 0)
        {
            foreach (var g in grammar)
            {
                sb.AppendLine($"[b]{JapaneseDB.ToHiragana(g.Pattern)}[/b]");
                sb.AppendLine($"[color=#CCCCCC]{g.MeaningVi}[/color]");
                sb.AppendLine($"[color=#AAAAAA][i]{g.ExplanationVi}[/i][/color]");
            }
        }
        else
        {
            sb.AppendLine("[i]Chưa học ngữ pháp nào trong bài này.[/i]");
        }
        sb.AppendLine();

        // BÀI ĐỌC (đã học)
        var readingsAll = JapaneseDB.Instance.GetReadingsByLesson(lesson);
        var readings = readingsAll.Where(r => Learned(ItemKind.Reading, r.Id)).ToList();
        sb.AppendLine($"[color=#FF69B4][b]BÀI ĐỌC (đã học {readings.Count}/{readingsAll.Count})[/b][/color]");
        if (readings.Count > 0)
        {
            int i = 1;
            foreach (var r in readings)
            {
                string hiraganaText = JapaneseDB.ToHiragana(r.TextJa);
                string snippet = hiraganaText.Length > 15 ? hiraganaText.Substring(0, 15) + "..." : hiraganaText;
                sb.AppendLine($"[b]Đoạn {i++}:[/b] {snippet}");
            }
        }
        else
        {
            sb.AppendLine("[i]Chưa đọc đoạn nào trong bài này.[/i]");
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
