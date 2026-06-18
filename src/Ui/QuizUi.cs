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
    [Export] private Label  _promptLabel;
    [Export] private Button _btnA, _btnB, _btnC, _btnD;
    [Export] private Label  _feedbackLabel;

    [Signal] public delegate void AnsweredEventHandler(bool correct);

    private QuizQuestion    _current;
    private List<Button>    _buttons;

    public override void _Ready()
    {
        _buttons = new() { _btnA, _btnB, _btnC, _btnD };
        foreach (var btn in _buttons)
            btn?.Connect(Button.SignalName.Pressed, Callable.From(() => OnAnswerPressed(btn.Text)));
        Hide();
    }

    public void ShowQuestion(QuizQuestion question)
    {
        _current = question;
        if (_promptLabel != null) _promptLabel.Text = question.Prompt;
        if (_feedbackLabel != null) _feedbackLabel.Text = "";

        for (int i = 0; i < _buttons.Count && i < question.Choices.Count; i++)
            if (_buttons[i] != null) _buttons[i].Text = question.Choices[i];

        SetButtonsDisabled(false);
        Show();
    }

    private void OnAnswerPressed(string answer)
    {
        if (_current == null) return;
        SetButtonsDisabled(true);

        bool correct = answer == _current.CorrectAnswer;
        if (_feedbackLabel != null)
            _feedbackLabel.Text = correct
                ? "✓ Chính xác!"
                : $"✗ Đáp án đúng: {_current.CorrectAnswer}";

        EmitSignal(SignalName.Answered, correct);
    }

    private void SetButtonsDisabled(bool disabled)
    {
        foreach (var btn in _buttons)
            if (btn != null) btn.Disabled = disabled;
    }
}
