using Godot;
using System.Collections.Generic;
using FragmentOfJapanese.Core;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// UI câu hỏi quiz: hiển thị prompt + 4 đáp án.
/// Phát signal Answered(bool correct) sau khi chọn.
/// </summary>
public partial class QuizUi : Control
{
    private Label        _promptLabel;
    private List<Button> _buttons = new();
    private Label        _feedbackLabel;

    [Signal] public delegate void AnsweredEventHandler(bool correct);

    private QuizQuestion _current;

    public override void _Ready()
    {
        BuildUi();
        Hide();
        GetViewport().SizeChanged += () => Size = GetViewport().GetVisibleRect().Size;
    }

    private void BuildUi()
    {
        var viewRect = GetViewport().GetVisibleRect();
        Position = Vector2.Zero;
        Size     = viewRect.Size;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // Nền mờ
        var bg = new ColorRect { Color = new Color(0.05f, 0.04f, 0.03f, 0.85f) };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Stop;
        AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(650, 500) };
        panel.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.24f, 0.17f, 0.13f, 0.98f), 24, new Color(0.4f, 0.28f, 0.2f, 1f), 4, 32, 32));
        center.AddChild(panel);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 24);
        panel.AddChild(vb);

        // Header
        var title = new Label { Text = "📖 LUYỆN TẬP TỪ VỰNG", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 28);
        title.AddThemeColorOverride("font_color", UiKit.Accent);
        vb.AddChild(title);

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", UiKit.Fade(UiKit.Accent, 0.3f));
        vb.AddChild(sep);

        // Prompt
        _promptLabel = new Label { Text = "???", HorizontalAlignment = HorizontalAlignment.Center };
        _promptLabel.AddThemeFontSizeOverride("font_size", 72);
        _promptLabel.AddThemeColorOverride("font_color", UiKit.WoodText);
        vb.AddChild(_promptLabel);

        var grid = new GridContainer { Columns = 2, SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        grid.AddThemeConstantOverride("h_separation", 20);
        grid.AddThemeConstantOverride("v_separation", 20);
        vb.AddChild(grid);

        for (int i = 0; i < 4; i++)
        {
            var btn = new Button { CustomMinimumSize = new Vector2(270, 80) };
            UiKit.StyleButton(btn, UiKit.WoodCard, UiKit.Fade(UiKit.Accent, 0.3f), UiKit.Accent, radius: 16);
            btn.AddThemeColorOverride("font_color", UiKit.WoodText);
            btn.AddThemeColorOverride("font_hover_color", Colors.White);
            btn.AddThemeFontSizeOverride("font_size", 26);
            
            _buttons.Add(btn);
            grid.AddChild(btn);

            Button b = btn;
            b.Pressed += () => OnAnswerPressed(b.Text);
        }

        _feedbackLabel = new Label { Text = "", HorizontalAlignment = HorizontalAlignment.Center };
        _feedbackLabel.AddThemeFontSizeOverride("font_size", 22);
        vb.AddChild(_feedbackLabel);

        var closeBtn = new Button { Text = "Bỏ qua", CustomMinimumSize = new Vector2(160, 50), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        UiKit.StyleButton(closeBtn, new Color(0.7f, 0.25f, 0.25f), new Color(0.85f, 0.35f, 0.35f), new Color(0.55f, 0.15f, 0.15f), radius: 12);
        closeBtn.AddThemeFontSizeOverride("font_size", 20);
        closeBtn.Pressed += () => { Hide(); EmitSignal(SignalName.Answered, false); };
        vb.AddChild(closeBtn);
    }

    public void ShowQuestion(QuizQuestion question)
    {
        _current = question;
        if (_promptLabel != null) _promptLabel.Text = question.Prompt;
        if (_feedbackLabel != null) _feedbackLabel.Text = "";

        for (int i = 0; i < _buttons.Count; i++)
        {
            if (i < question.Choices.Count)
            {
                _buttons[i].Text = question.Choices[i];
                _buttons[i].Visible = true;
            }
            else
            {
                _buttons[i].Visible = false;
            }
        }

        SetButtonsDisabled(false);
        Show();
    }

    private async void OnAnswerPressed(string answer)
    {
        if (_current == null) return;
        SetButtonsDisabled(true);

        bool correct = answer == _current.CorrectAnswer;
        if (_feedbackLabel != null)
        {
            _feedbackLabel.Text = correct
                ? "✓ Chính xác!"
                : $"✗ Sai rồi! Đáp án đúng: {_current.CorrectAnswer}";
            _feedbackLabel.AddThemeColorOverride("font_color", correct ? UiKit.BuyGreenHi : new Color(1f, 0.5f, 0.5f));
        }

        // Chờ 1 giây để người chơi xem kết quả trước khi đóng/chuyển câu
        await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
        
        Hide();
        EmitSignal(SignalName.Answered, correct);
    }

    private void SetButtonsDisabled(bool disabled)
    {
        foreach (var btn in _buttons)
            if (btn != null) btn.Disabled = disabled;
    }
}
