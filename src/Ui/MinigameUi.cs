using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using FragmentOfJapanese.Autoloads;
using FragmentOfJapanese.Core;
using FragmentOfJapanese.Entities;
using FragmentOfJapanese.Entities.Enemy;
using FragmentOfJapanese.Entities.Player;
using FragmentOfJapanese.Learning;

namespace FragmentOfJapanese.Ui;

public partial class MinigameUi : CanvasLayer
{
    public static MinigameUi Instance { get; private set; }

    [Export] private Control _root;
    [Export] private Control _modeSelectionPanel;
    [Export] private Control _questionPanel;
    
    // Mode Selection
    [Export] private Button _btnModeHiragana;
    [Export] private Button _btnModeVocab;
    [Export] private Button _btnClose;

    // Question
    [Export] private Label _lblProgress;
    [Export] private ProgressBar _timerBar;
    [Export] private Button _btnPlayAudio;
    [Export] private GridContainer _cardsContainer;
    [Export] private AudioStreamPlayer _audioPlayer;
    [Export] private Label _lblResult;

    private int _currentQuestion = 0;
    private const int MaxQuestions = 5;
    private float _timeRemaining = 20f;
    private bool _isPlaying = false;
    private bool _isWaitingForCombat = false;
    
    private string _currentCorrectId;
    private List<VocabularyEntry> _pool = new();
    private Random _rand = new();
    private Dictionary<string, AudioStream> _audioCache = new();
    private int _mistakeCount = 0;

    // Mode
    private string _currentMode = "hiragana"; // or "vocab"

    private Enemy _penaltyGoblin;   // goblin phạt hiện tại (dọn khi thua/đóng để không kẹt lại thế giới)

    public override void _Ready()
    {
        Instance = this;
        if (_root != null) _root.Visible = false;

        PopulateLessonList();
        
        if (_btnClose != null) _btnClose.Pressed += Close;
        if (_btnPlayAudio != null) _btnPlayAudio.Pressed += PlayCurrentAudio;
    }

    private GridContainer _lessonGrid;
    private Button _btnReview;

    private void PopulateLessonList()
    {
        if (_btnModeHiragana != null) { _btnModeHiragana.QueueFree(); _btnModeHiragana = null; }
        if (_btnModeVocab != null) { _btnModeVocab.QueueFree(); _btnModeVocab = null; }

        var vbox = _btnClose?.GetParent() as Container;
        if (vbox == null) return;

        var title = new Label { Text = "CHỌN BÀI HỌC", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 36);
        vbox.AddChild(title);
        vbox.MoveChild(title, 0);

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(700, 400) };
        _lessonGrid = new GridContainer { Columns = 3 };
        _lessonGrid.AddThemeConstantOverride("h_separation", 15);
        _lessonGrid.AddThemeConstantOverride("v_separation", 15);
        scroll.AddChild(_lessonGrid);
        vbox.AddChild(scroll);
        vbox.MoveChild(scroll, 1);

        for (int i = 1; i <= 25; i++)
        {
            int lessonNum = i;
            var btn = new Button { Text = $"Bài {lessonNum}", CustomMinimumSize = new Vector2(200, 80) };
            btn.AddThemeFontSizeOverride("font_size", 26);
            btn.FocusMode = Control.FocusModeEnum.None;
            btn.Pressed += () => StartGame(lessonNum.ToString());
            _lessonGrid.AddChild(btn);
        }

        _btnModeHiragana = new Button { Text = "Bảng chữ cái Hiragana", CustomMinimumSize = new Vector2(630, 70) };
        _btnModeHiragana.AddThemeFontSizeOverride("font_size", 28);
        _btnModeHiragana.FocusMode = Control.FocusModeEnum.None;
        _btnModeHiragana.Pressed += () => StartGame("hiragana");
        UiKit.StyleButton(_btnModeHiragana, new Color(0.8f, 0.5f, 0.2f), new Color(0.9f, 0.6f, 0.3f), new Color(0.6f, 0.4f, 0.1f), radius: 12);
        vbox.AddChild(_btnModeHiragana);
        vbox.MoveChild(_btnModeHiragana, 2);

        _btnReview = new Button { Text = "Ôn Tập Tổng Hợp", CustomMinimumSize = new Vector2(630, 70) };
        _btnReview.AddThemeFontSizeOverride("font_size", 28);
        _btnReview.FocusMode = Control.FocusModeEnum.None;
        _btnReview.Pressed += () => StartGame("review");
        vbox.AddChild(_btnReview);
        vbox.MoveChild(_btnReview, 3);
    }

    public override void _Process(double delta)
    {
        if (!_isPlaying || _isWaitingForCombat || _timeRemaining <= 0) return;

        _timeRemaining -= (float)delta;
        if (_timerBar != null)
        {
            _timerBar.Value = _timeRemaining;
        }

        if (_timeRemaining <= 0)
        {
            if (LearningTracker.Instance != null && _currentMode != "hiragana" && !string.IsNullOrEmpty(_currentCorrectId))
            {
                LearningTracker.Instance.Record(ItemKind.Vocab, _currentCorrectId, false, 20000);
            }
            HandleWrongAnswer();
        }
    }

    public void Open()
    {
        if (_root == null) return;
        _root.Visible = true;
        _modeSelectionPanel.Visible = true;
        _questionPanel.Visible = false;
        if (_lblResult != null) _lblResult.Visible = false;
        _isPlaying = false;
        _isWaitingForCombat = false;
        SetHudVisible(false);
        UpdateLessonUI();
    }

    private void UpdateLessonUI()
    {
        if (_lessonGrid == null) return;
        int unlocked = LearningTracker.Instance != null ? LearningTracker.Instance.UnlockedLesson : 1;
        for (int i = 0; i < _lessonGrid.GetChildCount(); i++)
        {
            if (_lessonGrid.GetChild(i) is Button btn)
            {
                int lessonNum = i + 1;
                if (lessonNum <= unlocked)
                {
                    btn.Disabled = false;
                    UiKit.StyleButton(btn, UiKit.WoodCard, UiKit.Fade(UiKit.Accent, 0.4f), UiKit.Accent, radius: 12);
                }
                else
                {
                    btn.Disabled = true;
                    UiKit.StyleButton(btn, new Color(0.1f, 0.1f, 0.1f, 0.5f), new Color(0.1f, 0.1f, 0.1f, 0.5f), new Color(0.1f, 0.1f, 0.1f, 0.5f), new Color(0.1f, 0.1f, 0.1f, 0.5f), radius: 12);
                }
            }
        }
        if (_btnReview != null)
        {
            _btnReview.Text = $"Ôn Tập Tổng Hợp (Bài 1 - {unlocked})";
            UiKit.StyleButton(_btnReview, new Color(0.2f, 0.6f, 0.2f), new Color(0.3f, 0.7f, 0.3f), new Color(0.1f, 0.5f, 0.1f), radius: 12);
        }
    }

    public void Close()
    {
        if (_root == null) return;
        _root.Visible = false;
        _isPlaying = false;
        _isWaitingForCombat = false;

        // An toàn: gỡ listener + dọn goblin phạt còn sót (nếu đóng giữa lúc combat).
        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (player != null) player.Died -= OnPlayerDied;
        if (IsInstanceValid(_penaltyGoblin)) _penaltyGoblin.QueueFree();
        _penaltyGoblin = null;

        SetHudVisible(true);
    }

    private void StartGame(string mode)
    {
        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (player != null && !player.UseMana(5))
        {
            UiKit.GlobalToast("Không đủ Mana! Cần 5 Mana.", 2.5f);
            return;
        }

        _currentMode = mode;
        _currentQuestion = 0;
        _mistakeCount = 0;
        _modeSelectionPanel.Visible = false;
        _questionPanel.Visible = true;

        if (mode == "review")
        {
            int unlocked = LearningTracker.Instance != null ? LearningTracker.Instance.UnlockedLesson : 1;
            _pool = JapaneseDB.Instance.VocabN5
                .Where(v => v.Lesson <= unlocked)
                .ToList();
        }
        else if (mode == "hiragana")
        {
            _pool = new List<VocabularyEntry>(JapaneseDB.Instance.Hiragana);
        }
        else if (int.TryParse(mode, out int lessonNum))
        {
            _pool = JapaneseDB.Instance.VocabN5
                .Where(v => v.Lesson == lessonNum)
                .ToList();
        }
        else 
        {
            _pool = JapaneseDB.Instance.VocabN5.ToList();
        }

        // Lọc pool chỉ giữ lại những từ có sẵn audio
        string pathPrefix = mode == "hiragana" ? "res://assets/audio/hiragana/" : "res://assets/audio/vocab/";
        _pool = _pool.Where(e => ResourceLoader.Exists($"{pathPrefix}{e.Id}.mp3")).ToList();

        NextQuestion();
    }

    private void NextQuestion()
    {
        _currentQuestion++;
        if (_currentQuestion > MaxQuestions)
        {
            // WIN
            GD.Print("[Minigame] Hoàn thành 5 thử thách!");
            ShowResult(true);
            return;
        }

        _lblProgress.Text = $"Câu hỏi {_currentQuestion}/{MaxQuestions}";
        _timeRemaining = 20f;
        if (_timerBar != null)
        {
            _timerBar.MaxValue = 20f;
            _timerBar.Value = 20f;
        }

        GenerateQuestion();
        _isPlaying = true;
        PlayCurrentAudio();
    }

    private void GenerateQuestion()
    {
        if (_pool.Count < 4)
        {
            GD.PrintErr("[Minigame] Không đủ dữ liệu để tạo câu hỏi!");
            Close();
            return;
        }

        // Chọn 4 từ ngẫu nhiên
        var shuffled = _pool.OrderBy(x => _rand.Next()).Take(4).ToList();
        var correctEntry = shuffled[_rand.Next(4)];
        _currentCorrectId = correctEntry.Id;

        // Xóa các card cũ
        foreach (Node child in _cardsContainer.GetChildren())
        {
            child.QueueFree();
        }

        // Tạo 4 thẻ bài
        for (int i = 0; i < 4; i++)
        {
            var entry = shuffled[i];
            var btn = CreateCardButton();
            btn.Pressed += () => OnAnswerSelected(btn);
            _cardsContainer.AddChild(btn);

            string mainText = JapaneseDB.ToHiragana(entry.Kana); // Chuyển toàn bộ Katakana sang Hiragana
            string finalTxt = $"{mainText}\n\n{entry.MeaningVi}";
            btn.SetMeta("final_txt", finalTxt);
            btn.SetMeta("entry_id", entry.Id);

            var t = CreateTween();
            float delay = i * 0.15f;
            
            t.TweenProperty(btn, "scale:x", 0.0f, 0.2f).SetDelay(delay).SetTrans(Tween.TransitionType.Sine);
            t.TweenCallback(Callable.From(() => {
                btn.Text = mainText;
                btn.AddThemeFontSizeOverride("font_size", 64);
                UiKit.StyleButton(btn, UiKit.WoodCard, UiKit.WoodBorder, UiKit.WoodDark, radius: 24);
                btn.Disabled = false;
            }));
            t.TweenProperty(btn, "scale:x", 1.0f, 0.2f).SetTrans(Tween.TransitionType.Sine);
        }
    }

    private Button CreateCardButton()
    {
        var btn = new Button
        {
            CustomMinimumSize = new Vector2(220, 300),
            FocusMode = Control.FocusModeEnum.None,
            Text = "?",
            MouseFilter = Control.MouseFilterEnum.Stop,
            PivotOffset = new Vector2(110, 150),
            Disabled = true
        };

        btn.AddThemeFontSizeOverride("font_size", 64);
        btn.AddThemeColorOverride("font_color", UiKit.WoodText);
        
        UiKit.StyleButton(btn, UiKit.WoodDark, UiKit.WoodBorder, UiKit.WoodCard, radius: 24);

        return btn;
    }

    private void PlayCurrentAudio()
    {
        if (_audioPlayer == null || string.IsNullOrEmpty(_currentCorrectId)) return;

        if (_audioCache.TryGetValue(_currentCorrectId, out var stream))
        {
            _audioPlayer.Stream = stream;
            _audioPlayer.Play();
            return;
        }

        string pathPrefix = _currentMode == "hiragana" ? "res://assets/audio/hiragana/" : "res://assets/audio/vocab/";
        string path = $"{pathPrefix}{_currentCorrectId}.mp3";

        if (ResourceLoader.Exists(path))
        {
            var loadedStream = ResourceLoader.Load<AudioStream>(path);
            _audioCache[_currentCorrectId] = loadedStream;
            _audioPlayer.Stream = loadedStream;
            _audioPlayer.Play();
        }
    }

    private void OnAnswerSelected(Button selectedBtn)
    {
        if (!_isPlaying || _isWaitingForCombat) return;

        _isPlaying = false;
        
        string selectedId = selectedBtn.GetMeta("entry_id").AsString();
        bool isCorrect = selectedId == _currentCorrectId;

        if (LearningTracker.Instance != null)
        {
            double responseMs = (20f - _timeRemaining) * 1000.0;
            LearningTracker.Instance.Record(ItemKind.Vocab, _currentCorrectId, isCorrect, responseMs);
        }

        // Reveal text and colors
        foreach (Node child in _cardsContainer.GetChildren())
        {
            if (child is Button b)
            {
                b.MouseFilter = Control.MouseFilterEnum.Ignore; // Vô hiệu hóa chuột thay vì Disabled để giữ màu
                b.Text = b.GetMeta("final_txt").AsString();
                b.AddThemeFontSizeOverride("font_size", 36);
                
                string id = b.GetMeta("entry_id").AsString();
                if (id == _currentCorrectId)
                {
                    // Green for correct
                    UiKit.StyleButton(b, new Color(0.15f, 0.65f, 0.25f), new Color(0.15f, 0.65f, 0.25f), new Color(0.15f, 0.65f, 0.25f), radius: 24);
                }
                else if (b == selectedBtn && !isCorrect)
                {
                    // Red for incorrect
                    UiKit.StyleButton(b, new Color(0.85f, 0.25f, 0.25f), new Color(0.85f, 0.25f, 0.25f), new Color(0.85f, 0.25f, 0.25f), radius: 24);
                }
            }
        }

        // Wait before proceeding
        var t = CreateTween();
        t.TweenInterval(1.5f);
        t.TweenCallback(Callable.From(() => 
        {
            if (isCorrect)
            {
                NextQuestion();
            }
            else
            {
                HandleWrongAnswer();
            }
        }));
    }

    private void HandleWrongAnswer()
    {
        _mistakeCount++;
        _isPlaying = false;
        _isWaitingForCombat = true;
        _root.Visible = false; // Ẩn UI để đánh quái
        SetHudVisible(true);   // Hiện lại joystick để đánh

        SpawnGoblinPenalty();
    }

    private void SpawnGoblinPenalty()
    {
        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (player == null)
        {
            // Lỗi không có player
            Close();
            return;
        }

        var goblinScene = ResourceLoader.Load<PackedScene>("res://scenes/entities/Goblin.tscn");
        if (goblinScene == null) return;

        var goblin = goblinScene.Instantiate<Enemy>();
        _penaltyGoblin = goblin;

        // Tăng sức mạnh quái vật dựa trên số lần sai
        int extraDamage = (_mistakeCount - 1) * 8; // Sai lần 1: dmg gốc. Sai lần 2: +8 dmg. Sai lần 3: +16 dmg...
        goblin.Attack += extraDamage; 
        var zone = goblin.GetNodeOrNull<AttackZone>("AttackZone");
        if (zone != null) zone.Damage += extraDamage;
        
        // Spawn vào level
        player.GetParent().AddChild(goblin);

        // Đặt vị trí gần player (cách khoảng 3m)
        Vector3 offset = new Vector3((_rand.Next(2) == 0 ? 1 : -1) * 3f, 0, (_rand.Next(2) == 0 ? 1 : -1) * 3f);
        goblin.GlobalPosition = player.GlobalPosition + offset;

        // Lắng nghe sự kiện chết
        goblin.Died += OnGoblinDefeated;
        player.Died += OnPlayerDied;
    }

    private void OnGoblinDefeated(Enemy enemy)
    {
        if (!_isWaitingForCombat) return;

        // Xóa listener player chết để tránh lỗi
        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (player != null) player.Died -= OnPlayerDied;

        _penaltyGoblin = null;   // goblin này đã bị hạ (tự QueueFree khi chết)
        _isWaitingForCombat = false;
        _root.Visible = true;
        SetHudVisible(false);

        // Chờ xíu rồi qua câu tiếp
        var t = CreateTween();
        t.TweenInterval(1.0f);
        t.TweenCallback(Callable.From(NextQuestion));
    }

    private void OnPlayerDied()
    {
        if (!_isWaitingForCombat) return;

        // Người chơi đã chết
        _isWaitingForCombat = false;
        
        // Xóa listener
        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (player != null) player.Died -= OnPlayerDied;

        // Dọn goblin phạt để nó không kẹt lại tấn công trong thế giới sau khi minigame đóng.
        if (IsInstanceValid(_penaltyGoblin)) _penaltyGoblin.QueueFree();
        _penaltyGoblin = null;

        // Đóng minigame luôn
        ShowResult(false);
    }



    private void ShowResult(bool isVictory)
    {
        _isPlaying = false;
        _isWaitingForCombat = false;

        // Thắng mini game nghe → báo tiến độ quest (daily_listen / weekly_listen).
        if (isVictory) Quests.QuestManager.Instance?.Report("minigame", "listen");

        // Hiện background mờ và giấu câu hỏi
        _questionPanel.Visible = false;
        _root.Visible = true;
        
        if (_lblResult != null)
        {
            _lblResult.Visible = true;
            _lblResult.Text = isVictory ? "CHIẾN THẮNG!" : "THẤT BẠI!";
            _lblResult.AddThemeColorOverride("font_color", isVictory ? new Color(1, 0.8f, 0.2f) : new Color(1, 0.3f, 0.3f));
            
            // Animation
            _lblResult.PivotOffset = _lblResult.Size / 2;
            _lblResult.Scale = Vector2.Zero;
            var t = CreateTween();
            t.TweenProperty(_lblResult, "scale", Vector2.One, 0.5f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            
            // Tắt sau 2s
            t.TweenInterval(2.0f);
            t.TweenCallback(Callable.From(() => 
            {
                _lblResult.Visible = false;
                _root.Visible = false;
                MinigameRewardUi.ShowReward("LUYỆN NGHE", isVictory, Close);
            }));
        }
        else
        {
            Close();
        }
    }

    private void SetHudVisible(bool visible)
    {
        foreach (Node n in GetTree().GetNodesInGroup("hud"))
        {
            if (n is CanvasLayer cl) cl.Visible = visible;
            else if (n is CanvasItem ci) ci.Visible = visible;
        }
    }
}
