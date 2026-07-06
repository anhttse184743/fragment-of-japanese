using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using FragmentOfJapanese.Core;
using FragmentOfJapanese.Autoloads;
using FragmentOfJapanese.Entities.Enemy;
using FragmentOfJapanese.Entities.Player;
using FragmentOfJapanese.Voice;
using FragmentOfJapanese.Learning;

namespace FragmentOfJapanese.Ui;

public partial class VoiceMinigameUi : CanvasLayer
{
    private Panel _root;
    private Container _modeSelectionPanel;
    private Container _gamePanel;
    private Label _instructionLabel;
    private Button _btnClose;
    private GridContainer _lessonGrid;

    private string _currentMode = "hiragana";
    private int _round = 1;
    private const int MaxRounds = 5;
    private int _mistakeCount = 0;

    private Container _topPanel;
    private Label _lblTurn;
    private Label _lblWord;
    private Label _lblResult;

    private Enemy _currentGoblin;
    private Label3D _goblinText;
    private VoiceRecognizer _voice;
    private List<VocabularyEntry> _pool = new();
    private VocabularyEntry _currentWord;
    private Random _rand = new();

    private bool _isPlaying = false;
    private Button _actionVoiceBtn;
    
    // Scene layout variables
    private Camera3D _cinematicCamera;
    private Vector3 _originalPlayerPos;
    private Vector3 _originalPlayerRot;
    private Vector3 _minigameCenter;
    private Transform3D? _customGoblinTransform;
    
    public Node3D TargetNpc { get; set; }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always; // Keep running when game is paused

        _voice = new VoiceRecognizer();
        // Đọc key từ biến môi trường GROQ_API_KEY (không hardcode secret vào source).
        _voice.GroqApiKey = System.Environment.GetEnvironmentVariable("GROQ_API_KEY") ?? "";
        AddChild(_voice);
        _voice.WordRecognized += OnWordRecognized;

        BuildUi();
        PopulateLessonList();
        
        GetTree().Paused = true;
    }

    private void BuildUi()
    {
        _root = new Panel();
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0, 0, 0, 0.85f)));
        AddChild(_root);

        var centerContainer = new CenterContainer();
        centerContainer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(centerContainer);

        // --- Màn hình chọn bài ---
        _modeSelectionPanel = new VBoxContainer();
        _modeSelectionPanel.AddThemeConstantOverride("separation", 20);
        centerContainer.AddChild(_modeSelectionPanel);

        _btnClose = new Button { Text = "X", CustomMinimumSize = new Vector2(60, 60) };
        _btnClose.AddThemeFontSizeOverride("font_size", 32);
        _btnClose.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _btnClose.Position = new Vector2(GetViewport().GetVisibleRect().Size.X - 80, 20);
        UiKit.StyleButton(_btnClose, new Color(0.8f, 0.2f, 0.2f), new Color(0.9f, 0.3f, 0.3f), new Color(0.6f, 0.1f, 0.1f), radius: 12);
        _btnClose.Pressed += Close;
        AddChild(_btnClose);

        // --- Màn hình in-game ---
        var margin = new MarginContainer { Visible = false };
        AddChild(margin);
        margin.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        margin.AddThemeConstantOverride("margin_bottom", 50);
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_right", 20);

        _gamePanel = new VBoxContainer();
        margin.AddChild(_gamePanel);

        _instructionLabel = new Label 
        { 
            Text = "Giữ nút VOICE góc dưới bên phải (hoặc phím V) để đọc tiếng Nhật!", 
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _instructionLabel.AddThemeFontSizeOverride("font_size", 28);

        // --- Màn hình kết quả (Tương tự Minigame nghe) ---
        var resultCenter = new CenterContainer { Visible = false };
        resultCenter.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(resultCenter);

        _lblResult = new Label 
        { 
            Text = "CHIẾN THẮNG!", 
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _lblResult.AddThemeFontSizeOverride("font_size", 100);
        _lblResult.AddThemeColorOverride("font_outline_color", Colors.Black);
        _lblResult.AddThemeConstantOverride("outline_size", 20);
        resultCenter.AddChild(_lblResult);

        // --- Top Panel (Lượt và Chữ) ---
        var topWrapper = new MarginContainer { Visible = false };
        topWrapper.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        topWrapper.AddThemeConstantOverride("margin_top", 40);
        AddChild(topWrapper);
        _topPanel = topWrapper; // Sử dụng _topPanel làm reference để ẩn/hiện

        var panelBg = new PanelContainer();
        panelBg.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        panelBg.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0, 0, 0, 0.7f), radius: 20));
        topWrapper.AddChild(panelBg);
        
        var topMargin = new MarginContainer();
        topMargin.AddThemeConstantOverride("margin_top", 15);
        topMargin.AddThemeConstantOverride("margin_bottom", 15);
        topMargin.AddThemeConstantOverride("margin_left", 80);
        topMargin.AddThemeConstantOverride("margin_right", 80);
        panelBg.AddChild(topMargin);

        var topVbox = new VBoxContainer();
        topVbox.AddThemeConstantOverride("separation", 10);
        topVbox.Alignment = BoxContainer.AlignmentMode.Center;
        topMargin.AddChild(topVbox);

        _lblTurn = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _lblTurn.AddThemeFontSizeOverride("font_size", 24);
        _lblTurn.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.8f));
        topVbox.AddChild(_lblTurn);

        _lblWord = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _lblWord.AddThemeFontSizeOverride("font_size", 60);
        _lblWord.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.4f));
        topVbox.AddChild(_lblWord);
        
        topVbox.AddChild(_instructionLabel);
    }

    private void PopulateLessonList()
    {
        var title = new Label { Text = "LUYỆN PHÁT ÂM", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 36);
        _modeSelectionPanel.AddChild(title);

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(700, 400) };
        _lessonGrid = new GridContainer { Columns = 3 };
        _lessonGrid.AddThemeConstantOverride("h_separation", 15);
        _lessonGrid.AddThemeConstantOverride("v_separation", 15);
        scroll.AddChild(_lessonGrid);
        _modeSelectionPanel.AddChild(scroll);

        int unlocked = LearningTracker.Instance != null ? LearningTracker.Instance.UnlockedLesson : 1;

        for (int i = 1; i <= 25; i++)
        {
            int lessonNum = i;
            var btn = new Button { Text = $"Bài {lessonNum}", CustomMinimumSize = new Vector2(200, 80) };
            btn.AddThemeFontSizeOverride("font_size", 26);
            btn.FocusMode = Control.FocusModeEnum.None;
            
            if (lessonNum <= unlocked)
            {
                UiKit.StyleButton(btn, UiKit.WoodCard, UiKit.Fade(UiKit.Accent, 0.4f), UiKit.Accent, radius: 12);
                btn.Pressed += () => StartGame(lessonNum.ToString());
            }
            else
            {
                btn.Disabled = true;
                UiKit.StyleButton(btn, new Color(0.1f, 0.1f, 0.1f, 0.5f), new Color(0.1f, 0.1f, 0.1f, 0.5f), new Color(0.1f, 0.1f, 0.1f, 0.5f), new Color(0.1f, 0.1f, 0.1f, 0.5f), radius: 12);
            }
            _lessonGrid.AddChild(btn);
        }

        var btnHiragana = new Button { Text = "Bảng chữ cái Hiragana", CustomMinimumSize = new Vector2(630, 70) };
        btnHiragana.AddThemeFontSizeOverride("font_size", 28);
        btnHiragana.FocusMode = Control.FocusModeEnum.None;
        btnHiragana.Pressed += () => StartGame("hiragana");
        UiKit.StyleButton(btnHiragana, new Color(0.8f, 0.5f, 0.2f), new Color(0.9f, 0.6f, 0.3f), new Color(0.6f, 0.4f, 0.1f), radius: 12);
        _modeSelectionPanel.AddChild(btnHiragana);

        var btnReview = new Button { Text = $"Ôn Tập Tổng Hợp (Bài 1 - {unlocked})", CustomMinimumSize = new Vector2(630, 70) };
        btnReview.AddThemeFontSizeOverride("font_size", 28);
        btnReview.FocusMode = Control.FocusModeEnum.None;
        btnReview.Pressed += () => StartGame("review");
        UiKit.StyleButton(btnReview, new Color(0.2f, 0.6f, 0.2f), new Color(0.3f, 0.7f, 0.3f), new Color(0.1f, 0.5f, 0.1f), radius: 12);
        _modeSelectionPanel.AddChild(btnReview);
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
        _round = 1;
        _isPlaying = true;
        
        int unlocked = LearningTracker.Instance != null ? LearningTracker.Instance.UnlockedLesson : 1;

        if (mode == "review")
        {
            _pool = JapaneseDB.Instance.VocabN5.Where(v => v.Lesson <= unlocked).ToList();
        }
        else if (mode == "hiragana")
        {
            _pool = new List<VocabularyEntry>(JapaneseDB.Instance.Hiragana);
        }
        else if (int.TryParse(mode, out int lessonNum))
        {
            _pool = JapaneseDB.Instance.VocabN5.Where(v => v.Lesson == lessonNum).ToList();
        }

        _mistakeCount = 0;
        _lblResult.GetParent<Control>().Visible = false;
        _topPanel.Visible = true;
        _root.Visible = false; // Hide background UI so player sees the game world clearly
        _modeSelectionPanel.Visible = false; // Hide the lesson list!
        
        // Hiện màn hình game (lấy node cha MarginContainer)
        _gamePanel.GetParent<MarginContainer>().Visible = true;
        _btnClose.Visible = true;
        SetHudElementsVisible(false); // Hide the main game HUD partially

        var voiceBtn = GetTree().Root.FindChild("VoiceBtn", true, false) as Button;
        if (voiceBtn != null)
        {
            _actionVoiceBtn = voiceBtn;
            _actionVoiceBtn.Visible = true; // Hiện nút Voice
            _actionVoiceBtn.ProcessMode = ProcessModeEnum.Always; // Allow it to run when paused
            _actionVoiceBtn.ButtonDown += StartRecording;
            _actionVoiceBtn.ButtonUp += StopRecording;
        }

        // Tái dùng 'player' đã lấy ở đầu hàm (Player kế thừa Node3D) — tránh khai báo trùng.
        if (player != null)
        {
            _originalPlayerPos = player.GlobalPosition;
            _originalPlayerRot = player.GlobalRotation;
            _minigameCenter = _originalPlayerPos; // Center of our scene

            _cinematicCamera = new Camera3D();
            player.GetParent().AddChild(_cinematicCamera);

            var pPos = TargetNpc?.GetNodeOrNull<Node3D>("PlayerPos");
            var gPos = TargetNpc?.GetNodeOrNull<Node3D>("GoblinPos");
            var cPos = TargetNpc?.GetNodeOrNull<Node3D>("CameraPos");

            if (pPos != null && gPos != null && cPos != null)
            {
                player.GlobalTransform = pPos.GlobalTransform;
                _cinematicCamera.GlobalTransform = cPos.GlobalTransform;
                _customGoblinTransform = gPos.GlobalTransform;
            }
            else
            {
                // Fallback math if markers not found (looking South)
                player.GlobalPosition = _minigameCenter + new Vector3(-1.5f, 0, 0);
                player.LookAt(_minigameCenter + new Vector3(1.5f, 0, 0), Vector3.Up);

                _cinematicCamera.GlobalPosition = _minigameCenter + new Vector3(0, 1.2f, -3.5f);
                _cinematicCamera.LookAt(_minigameCenter + new Vector3(0, 0.8f, 0), Vector3.Up);
                
                _customGoblinTransform = null;
            }
            
            _cinematicCamera.MakeCurrent();

            if (TargetNpc != null)
            {
                TargetNpc.Visible = false; // Ẩn NPC kích hoạt minigame
            }
        }

        SpawnGoblinRound();
    }

    private void SpawnGoblinRound()
    {
        if (_round > MaxRounds)
        {
            // End of Minigame
            ShowResult(_mistakeCount <= 1);
            return;
        }

        if (_pool.Count == 0)
        {
            Close();
            return;
        }

        _currentWord = _pool[_rand.Next(_pool.Count)];
        
        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (player == null) { Close(); return; }

        var goblinScene = ResourceLoader.Load<PackedScene>("res://scenes/entities/Goblin.tscn");
        if (goblinScene == null) { Close(); return; }

        _currentGoblin = goblinScene.Instantiate<Enemy>();
        player.GetParent().AddChild(_currentGoblin);

        if (_customGoblinTransform.HasValue)
        {
            _currentGoblin.GlobalTransform = _customGoblinTransform.Value;
        }
        else
        {
            // Fallback
            _currentGoblin.GlobalPosition = _minigameCenter + new Vector3(1.5f, 0, 0);
        }

        // Ép Player và Goblin quay mặt vào nhau
        var anim = player.GetNodeOrNull<PlayerAnimator>("IdleLogic");
        if (anim != null) anim.FaceTarget(_currentGoblin.GlobalPosition);
        else player.LookAt(_currentGoblin.GlobalPosition, Vector3.Up);
        
        // Xoay Goblin nhìn về phía Player
        _currentGoblin.LookAt(player.GlobalPosition, Vector3.Up);

        // Bỏ Text trên đầu Goblin vì đã mang lên TopPanel
        _lblTurn.Text = $"LƯỢT {_round} / {MaxRounds}";
        _lblWord.Text = JapaneseDB.ToHiragana(_currentWord.Kana);
        
        // Nghĩa tiếng Việt gợi ý trên màn hình
        _instructionLabel.Text = $"Nghĩa: {_currentWord.MeaningVi}";
        _instructionLabel.Modulate = Colors.White;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!_isPlaying || _currentGoblin == null || !IsInstanceValid(_currentGoblin)) return;

        if (e is InputEventKey k && k.Keycode == Key.V && !k.Echo)
        {
            if (k.Pressed) StartRecording();
            else           StopRecording();
        }
    }

    private void StartRecording()
    {
        if (!_isPlaying || _currentGoblin == null || !IsInstanceValid(_currentGoblin)) return;
        _instructionLabel.Text = "Đang nghe...";
        _instructionLabel.Modulate = Colors.Green;
        _voice.StartListening(_pool);
    }

    private void StopRecording()
    {
        if (!_isPlaying || _currentGoblin == null || !IsInstanceValid(_currentGoblin)) return;
        _instructionLabel.Text = "Đang xử lý...";
        _instructionLabel.Modulate = Colors.Orange;
        _voice.StopListening();
    }

    private void OnWordRecognized(string raw, bool matched, string id, float similarity)
    {
        if (!_isPlaying || _currentGoblin == null || !IsInstanceValid(_currentGoblin)) return;

        _instructionLabel.Modulate = Colors.White;

        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (player == null) return;

        if (matched && id == _currentWord.Id)
        {
            _instructionLabel.Text = $"Chính xác! Bạn nói: {raw}";
            _instructionLabel.Modulate = Colors.Green;
            
            // Lượt của Player: Lao tới đánh Goblin
            PlayDashAnimation(player, _currentGoblin, () => 
            {
                // Goblin chớp đỏ và chết
                var mat = _currentGoblin.GetNodeOrNull<AnimatedSprite3D>("Visual")?.MaterialOverride as ShaderMaterial;
                if (mat != null) mat.SetShaderParameter("flash_color", new Color(1, 0, 0, 1));
                
                _currentGoblin.QueueFree();
                NextRound();
            });
        }
        else
        {
            _mistakeCount++;
            _instructionLabel.Text = $"Sai rồi! Bạn nói: {raw}\nMáy chủ mong đợi: {_currentWord.Kana}";
            _instructionLabel.Modulate = Colors.Red;
            
            // Lượt của Goblin: Lao tới đánh Player
            PlayDashAnimation(_currentGoblin, player, () => 
            {
                player.TakeDamage(_currentGoblin.Attack);
                _currentGoblin.QueueFree();
                NextRound();
            });
        }
    }

    private void PlayDashAnimation(Node3D attacker, Node3D target, Action onComplete)
    {
        var tween = CreateTween();
        var startPos = attacker.GlobalPosition;
        var dashTarget = startPos.Lerp(target.GlobalPosition, 0.6f); // Lao tới 60% quãng đường

        // Lao tới nhanh
        tween.TweenProperty(attacker, "global_position", dashTarget, 0.15f)
             .SetTrans(Tween.TransitionType.Circ)
             .SetEase(Tween.EaseType.Out);
        
        // Gọi callback (vd: trừ máu, diệt địch) ngay khi chạm
        tween.TweenCallback(Callable.From(onComplete));

        // Rút về từ từ
        tween.TweenProperty(attacker, "global_position", startPos, 0.3f)
             .SetTrans(Tween.TransitionType.Quad)
             .SetEase(Tween.EaseType.InOut);
    }

    private void ShowResult(bool isVictory)
    {
        _isPlaying = false;

        // Thắng mini game nói → báo tiến độ quest (daily_speak / weekly_speak).
        if (isVictory) Quests.QuestManager.Instance?.Report("minigame", "speak");

        // Hiện background mờ và giấu câu hỏi
        _root.Visible = true;
        _gamePanel.GetParent<MarginContainer>().Visible = false;
        _topPanel.Visible = false;
        
        if (_lblResult != null)
        {
            _lblResult.GetParent<Control>().Visible = true; // Hiện CenterContainer
            _lblResult.Text = isVictory ? "CHIẾN THẮNG!" : "THẤT BẠI!";
            _lblResult.AddThemeColorOverride("font_color", isVictory ? new Color(1, 0.8f, 0.2f) : new Color(1, 0.3f, 0.3f));
            
            // Animation
            _lblResult.PivotOffset = new Vector2(400, 70); // Ước tính tâm của chữ 100px
            _lblResult.Scale = Vector2.Zero;
            var t = CreateTween();
            t.TweenProperty(_lblResult, "scale", Vector2.One, 0.5f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            
            // Tắt sau 2s
            t.TweenInterval(2.0f);
            t.TweenCallback(Callable.From(() => 
            {
                _lblResult.GetParent<Control>().Visible = false;
                _root.Visible = false;
                MinigameRewardUi.ShowReward("LUYỆN PHÁT ÂM", isVictory, Close);
            }));
        }
        else
        {
            Close();
        }
    }

    private void NextRound()
    {
        _round++;
        // Chờ 2 giây trước khi spawn lượt mới
        var timer = GetTree().CreateTimer(2.0f);
        timer.Connect("timeout", Callable.From(() => 
        {
            if (_isPlaying) SpawnGoblinRound();
        }));
    }

    private void Close()
    {
        if (_cinematicCamera != null && IsInstanceValid(_cinematicCamera))
        {
            _cinematicCamera.QueueFree();
            _cinematicCamera = null;
        }

        var player = GetTree().GetFirstNodeInGroup("player") as Node3D;
        if (player != null)
        {
            // Restore position
            player.GlobalPosition = _originalPlayerPos;
            player.GlobalRotation = _originalPlayerRot;

            if (TargetNpc != null)
            {
                TargetNpc.Visible = true;
            }
        }
        
        if (_actionVoiceBtn != null)
        {
            _actionVoiceBtn.ButtonDown -= StartRecording;
            _actionVoiceBtn.ButtonUp -= StopRecording;
            _actionVoiceBtn.Visible = false; // Trả lại trạng thái ẩn ban đầu
            _actionVoiceBtn.ProcessMode = ProcessModeEnum.Inherit;
            _actionVoiceBtn = null;
        }

        SetHudElementsVisible(true);
        GetTree().Paused = false;
        QueueFree();
    }

    private void SetHudElementsVisible(bool visible)
    {
        var hudNodes = GetTree().GetNodesInGroup("hud");
        foreach (var node in hudNodes)
        {
            if (node.Name == "VirtualJoystick")
            {
                if (node is CanvasLayer cl) cl.Visible = visible;
                else if (node is Control c) c.Visible = visible;
            }
            if (node.Name == "GameHud" && node is Node gameHud)
            {
                var statusBars = gameHud.GetNodeOrNull<Control>("StatusBars");
                var topBar = gameHud.GetNodeOrNull<Control>("TopBar");
                var ads = gameHud.GetNodeOrNull<Control>("Ads");
                var actionButtons = gameHud.GetNodeOrNull<Control>("ActionButtons");

                if (statusBars != null) statusBars.Visible = visible;
                if (topBar != null) topBar.Visible = visible;
                if (ads != null) ads.Visible = visible;

                if (actionButtons != null)
                {
                    var atk = actionButtons.GetNodeOrNull<Control>("AtkBtn");
                    var jump = actionButtons.GetNodeOrNull<Control>("JumpBtn");
                    var fast = actionButtons.GetNodeOrNull<Control>("FastBtn");
                    var draw = actionButtons.GetNodeOrNull<Control>("DrawBtn");
                    
                    if (atk != null) atk.Visible = visible;
                    if (jump != null) jump.Visible = visible;
                    if (fast != null) fast.Visible = visible;
                    if (draw != null) draw.Visible = visible;
                }
            }
        }
    }
}
