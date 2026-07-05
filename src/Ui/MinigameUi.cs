using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using FragmentOfJapanese.Autoloads;
using FragmentOfJapanese.Core;
using FragmentOfJapanese.Entities.Enemy;
using FragmentOfJapanese.Entities.Player;

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

    private int _currentQuestion = 0;
    private const int MaxQuestions = 5;
    private float _timeRemaining = 20f;
    private bool _isPlaying = false;
    private bool _isWaitingForCombat = false;
    
    private string _currentCorrectId;
    private List<VocabularyEntry> _pool = new();
    private Random _rand = new();
    private Dictionary<string, AudioStream> _audioCache = new();

    // Mode
    private string _currentMode = "hiragana"; // or "vocab"

    public override void _Ready()
    {
        Instance = this;
        if (_root != null) _root.Visible = false;

        if (_btnModeHiragana != null) _btnModeHiragana.Pressed += () => StartGame("hiragana");
        if (_btnModeVocab != null) _btnModeVocab.Pressed += () => StartGame("vocab");
        if (_btnClose != null) _btnClose.Pressed += Close;
        if (_btnPlayAudio != null) _btnPlayAudio.Pressed += PlayCurrentAudio;
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
            HandleWrongAnswer();
        }
    }

    public void Open()
    {
        if (_root == null) return;
        _root.Visible = true;
        _modeSelectionPanel.Visible = true;
        _questionPanel.Visible = false;
        _isPlaying = false;
        _isWaitingForCombat = false;
        SetHudVisible(false);
    }

    public void Close()
    {
        if (_root == null) return;
        _root.Visible = false;
        _isPlaying = false;
        _isWaitingForCombat = false;
        SetHudVisible(true);
    }

    private void StartGame(string mode)
    {
        _currentMode = mode;
        _currentQuestion = 0;
        _modeSelectionPanel.Visible = false;
        _questionPanel.Visible = true;

        if (mode == "hiragana") _pool = new List<VocabularyEntry>(JapaneseDB.Instance.Hiragana);
        else _pool = new List<VocabularyEntry>(JapaneseDB.Instance.VocabN5);

        // Filter pool to only include entries with existing audio
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
            // TODO: Give reward
            Close();
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
            var btn = CreateCardButton(entry);
            btn.Pressed += () => OnAnswerSelected(entry.Id);
            _cardsContainer.AddChild(btn);
        }
    }

    private Button CreateCardButton(VocabularyEntry entry)
    {
        string mainText = _currentMode == "hiragana" ? entry.Kana : (!string.IsNullOrEmpty(entry.Kanji) ? entry.Kanji : entry.Kana);
        
        var btn = new Button
        {
            CustomMinimumSize = new Vector2(180, 120),
            FocusMode = Control.FocusModeEnum.None,
            Text = $"{mainText}\n{entry.MeaningVi}",
            MouseFilter = Control.MouseFilterEnum.Stop
        };

        btn.AddThemeFontSizeOverride("font_size", 24);
        btn.AddThemeColorOverride("font_color", UiKit.WoodText);
        
        UiKit.StyleButton(btn, UiKit.WoodCard, UiKit.WoodBorder, UiKit.WoodDark, radius: 15);

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

    private void OnAnswerSelected(string selectedId)
    {
        if (!_isPlaying || _isWaitingForCombat) return;

        if (selectedId == _currentCorrectId)
        {
            // Đoán đúng
            _isPlaying = false;
            // TODO: Hiệu ứng đúng
            NextQuestion();
        }
        else
        {
            // Đoán sai
            HandleWrongAnswer();
        }
    }

    private void HandleWrongAnswer()
    {
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
        
        // Đặt vị trí gần player (cách khoảng 3m)
        Vector3 offset = new Vector3((_rand.Next(2) == 0 ? 1 : -1) * 3f, 0, (_rand.Next(2) == 0 ? 1 : -1) * 3f);
        goblin.GlobalPosition = player.GlobalPosition + offset;
        
        // Spawn vào level
        player.GetParent().AddChild(goblin);

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

        // Đóng minigame luôn
        Close();
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
