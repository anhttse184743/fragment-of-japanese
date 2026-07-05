using Godot;
using FragmentOfJapanese.Battle;
using FragmentOfJapanese.Entities.Player;
using FragmentOfJapanese.Entities.Enemy;
using FragmentOfJapanese.Core;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// Giao diện trận chiến vẽ chữ đè lên màn hình (UI Overlay).
/// Người chơi vẽ chữ lên Canvas, sau 2 giây ngừng vẽ sẽ chấm điểm và gửi về DrawBattleManager.
/// </summary>
public partial class DrawBattleUi : Control
{
    [Export] public float Threshold = 0.55f; // Đã tăng từ 0.35f lên 0.55f để chấm dễ hơn rất nhiều

    private DrawBattleManager _manager;
    private Player _player;
    private Enemy  _enemy;
    
    private DrawingCanvas _canvas;
    private Label _prompt, _feedback, _timerLabel;
    private ProgressBar _playerHp, _enemyHp;
    private KanaStrokes _targetStrokes;
    
    private float _timeLeft;
    private bool _isTimerRunning;
    private int _lastSec = -1;

    private PanelContainer _drawPanel;
    private PanelContainer _resultPanel;
    private Label _resultLabel;

    public static DrawBattleUi Open(DrawBattleManager manager, Player player, Enemy enemy)
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null) return null;
        
        var layer = new CanvasLayer { Layer = 60, Name = "DrawBattleOverlay" };
        var ui = new DrawBattleUi(manager, player, enemy);
        layer.AddChild(ui);
        tree.Root.AddChild(layer);
        
        return ui;
    }

    public DrawBattleUi(DrawBattleManager manager, Player player, Enemy enemy)
    {
        _manager = manager;
        _player = player;
        _enemy = enemy;
    }

    public override void _Ready()
    {
        BuildUi();
        UpdateHealthBars(true);
    }

    public override void _Process(double delta)
    {
        if (_isTimerRunning && _timeLeft > 0)
        {
            _timeLeft -= (float)delta;
            int newSec = Mathf.CeilToInt(_timeLeft);
            
            if (newSec != _lastSec)
            {
                _lastSec = newSec;
                _timerLabel.Text = $"Thời gian: {newSec}s";
                
                if (newSec <= 3)
                    _timerLabel.AddThemeColorOverride("font_color", Colors.Red);
            }
        }
    }

    public void ShowPrompt(string text, float timeLimit)
    {
        _prompt.Text = text;
        _timeLeft = timeLimit;
        _lastSec = -1;
        _isTimerRunning = true;
        _timerLabel.AddThemeColorOverride("font_color", Colors.White);
        _canvas.Clear();
        _feedback.Text = "Ngừng vẽ 2 giây để chấm điểm.";
        _feedback.AddThemeColorOverride("font_color", UiKit.TextDim);
    }

    public void SetTargetStrokes(KanaStrokes strokes)
    {
        _targetStrokes = strokes;
    }

    public void ShowFeedback(string text, Color color)
    {
        _isTimerRunning = false;
        _feedback.Text = text;
        _feedback.AddThemeColorOverride("font_color", color);
    }

    public void ShowResult(bool win)
    {
        _isTimerRunning = false;
        _resultPanel.Visible = true;
        
        _resultLabel.Text = win ? "🌟 CHIẾN THẮNG! 🌟" : "💀 THẤT BẠI... 💀";
        var color = win ? Colors.Gold : Colors.Crimson;
        
        _resultLabel.AddThemeColorOverride("font_color", color);
        
        var sb = _resultPanel.GetThemeStylebox("panel") as StyleBoxFlat;
        if (sb != null) sb.BorderColor = color;

        if (_drawPanel != null) _drawPanel.Visible = false;
        
        // Animation nảy lên (pop-up bounce)
        _resultPanel.Scale = new Vector2(0.3f, 0.3f);
        _resultPanel.PivotOffset = _resultPanel.CustomMinimumSize / 2;
        CreateTween().TweenProperty(_resultPanel, "scale", Vector2.One, 0.6f)
            .SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
    }

    public void StopTimer()
    {
        _isTimerRunning = false;
    }

    public void UpdateHealthBars(bool instant = false)
    {
        if (_playerHp != null && _player != null)
        {
            _playerHp.MaxValue = _player.Data.MaxHp;
            if (instant) 
                _playerHp.Value = _player.Data.Hp;
            else 
                CreateTween().TweenProperty(_playerHp, "value", _player.Data.Hp, 0.4f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }

        if (_enemyHp != null && _enemy != null)
        {
            _enemyHp.MaxValue = _enemy.MaxHp;
            if (instant) 
                _enemyHp.Value = _enemy.Hp;
            else 
                CreateTween().TweenProperty(_enemyHp, "value", _enemy.Hp, 0.4f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }
    }

    public void Close()
    {
        if (GetParent() is CanvasLayer layer) layer.QueueFree();
        else QueueFree();
    }

    private void OnIdle()
    {
        if (!_isTimerRunning || _targetStrokes == null) return;
        
        var result = StrokeRecognizer.Match(_canvas.GetStrokes(), _targetStrokes.ToStrokes(), Threshold);
        
        // Disable timer immediately
        _isTimerRunning = false;
        
        // Report to manager
        _manager.OnDrawEvaluated(result);
    }

    private void BuildUi()
    {
        var viewRect = GetViewport().GetVisibleRect();
        Position = Vector2.Zero;
        Size = viewRect.Size;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // Nền mờ toàn màn hình cực nhẹ (0.1)
        var bg = new ColorRect { Color = new Color(0.08f, 0.09f, 0.12f, 0.1f) };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Ignore; 
        AddChild(bg);

        // --- PHẦN 1: GÓC TRÊN CÙNG (TRẬN CHIẾN & THANH MÁU) ---
        var topMargin = new MarginContainer();
        topMargin.SetAnchorsPreset(LayoutPreset.TopWide);
        topMargin.AddThemeConstantOverride("margin_top", 16);
        topMargin.AddThemeConstantOverride("margin_left", 16);
        topMargin.AddThemeConstantOverride("margin_right", 16);
        AddChild(topMargin);

        var topPanel = new PanelContainer();
        topPanel.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.1f, 0.12f, 0.16f, 0.85f), 16, UiKit.Accent, 2));
        topMargin.AddChild(topPanel);

        var topPadding = new MarginContainer();
        topPadding.AddThemeConstantOverride("margin_top", 12);
        topPadding.AddThemeConstantOverride("margin_bottom", 16);
        topPadding.AddThemeConstantOverride("margin_left", 20);
        topPadding.AddThemeConstantOverride("margin_right", 20);
        topPanel.AddChild(topPadding);

        var topVb = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        topVb.AddThemeConstantOverride("separation", 16);
        topPadding.AddChild(topVb);

        var title = MkLabel("⚔ TRẬN CHIẾN ⚔", 26, Colors.White);
        title.AddThemeColorOverride("font_outline_color", UiKit.Accent);
        title.AddThemeConstantOverride("outline_size", 2);
        topVb.AddChild(title);

        var statsHb = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        statsHb.AddThemeConstantOverride("separation", 24);
        topVb.AddChild(statsHb);

        // Player Stats (Trái)
        var playerVb = new VBoxContainer();
        playerVb.AddChild(MkLabel("👤 Người chơi", 16, new Color(0.6f, 0.9f, 1f)));
        _playerHp = new ProgressBar { CustomMinimumSize = new Vector2(240, 28), ShowPercentage = true };
        _playerHp.AddThemeStyleboxOverride("background", UiKit.Box(new Color(0.1f, 0.25f, 0.15f, 1f), 8));
        _playerHp.AddThemeStyleboxOverride("fill", UiKit.Box(new Color(0.2f, 0.8f, 0.3f, 1f), 8));
        playerVb.AddChild(_playerHp);
        statsHb.AddChild(playerVb);

        // Enemy Stats (Phải)
        var enemyVb = new VBoxContainer();
        enemyVb.AddChild(MkLabel($"👹 {_enemy.EnemyName}", 16, new Color(1f, 0.6f, 0.6f)));
        _enemyHp = new ProgressBar { CustomMinimumSize = new Vector2(240, 28), ShowPercentage = true };
        _enemyHp.AddThemeStyleboxOverride("background", UiKit.Box(new Color(0.3f, 0.1f, 0.1f, 1f), 8));
        _enemyHp.AddThemeStyleboxOverride("fill", UiKit.Box(new Color(0.9f, 0.2f, 0.2f, 1f), 8));
        enemyVb.AddChild(_enemyHp);
        statsHb.AddChild(enemyVb);


        // --- PHẦN 2: GÓC DƯỚI CÙNG (VẼ CHỮ) ---
        var bottomMargin = new MarginContainer();
        bottomMargin.SetAnchorsPreset(LayoutPreset.BottomWide);
        bottomMargin.GrowVertical = GrowDirection.Begin;
        bottomMargin.AddThemeConstantOverride("margin_bottom", 40);
        AddChild(bottomMargin);

        // Làm panel mờ bọc khu vực vẽ
        _drawPanel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        _drawPanel.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.13f, 0.14f, 0.18f, 0.85f), 12, UiKit.Accent, 2));
        bottomMargin.AddChild(_drawPanel);

        var drawMargin = new MarginContainer();
        drawMargin.AddThemeConstantOverride("margin_left", 20);
        drawMargin.AddThemeConstantOverride("margin_right", 20);
        drawMargin.AddThemeConstantOverride("margin_top", 16);
        drawMargin.AddThemeConstantOverride("margin_bottom", 16);
        _drawPanel.AddChild(drawMargin);

        var drawVb = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        drawVb.AddThemeConstantOverride("separation", 14);
        drawMargin.AddChild(drawVb);

        _prompt = MkLabel("Đang chờ...", 24, Colors.White);
        drawVb.AddChild(_prompt);

        _timerLabel = MkLabel("Thời gian: 10s", 20, Colors.White);
        drawVb.AddChild(_timerLabel);

        var canvasPanel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        canvasPanel.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.08f, 0.09f, 0.12f, 1f), 12));
        drawVb.AddChild(canvasPanel);
        
        _canvas = new DrawingCanvas { CustomMinimumSize = new Vector2(300, 300) };
        _canvas.Idle += OnIdle;
        canvasPanel.AddChild(_canvas);

        _feedback = MkLabel("", 18, UiKit.TextDim);
        drawVb.AddChild(_feedback);

        var hb = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        hb.AddThemeConstantOverride("separation", 12);
        drawVb.AddChild(hb);
        hb.AddChild(MkButton("✗ Xóa nét", () => { _canvas.Clear(); }));

        // --- PHẦN 3: THÔNG BÁO KẾT QUẢ KHI XONG TRẬN ---
        var centerContainer = new CenterContainer();
        centerContainer.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(centerContainer);

        _resultPanel = new PanelContainer { Visible = false, CustomMinimumSize = new Vector2(500, 160) };
        
        var resStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.07f, 0.1f, 0.98f),
            CornerRadiusTopLeft = 24, CornerRadiusTopRight = 24,
            CornerRadiusBottomLeft = 24, CornerRadiusBottomRight = 24,
            BorderWidthTop = 4, BorderWidthBottom = 4, BorderWidthLeft = 4, BorderWidthRight = 4,
            BorderColor = Colors.Gold,
            ShadowColor = new Color(0, 0, 0, 0.6f),
            ShadowSize = 25
        };
        _resultPanel.AddThemeStyleboxOverride("panel", resStyle);
        centerContainer.AddChild(_resultPanel);

        var resVb = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _resultPanel.AddChild(resVb);

        _resultLabel = MkLabel("", 52, Colors.White);
        _resultLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _resultLabel.AddThemeConstantOverride("outline_size", 8);
        resVb.AddChild(_resultLabel);
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
