using Godot;
using System.Threading.Tasks;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// Hộp thoại với hiệu ứng typewriter.
/// Nhấn nút Next để skip hoặc đóng.
/// </summary>
public partial class DialogueBox : Control
{
    [Export] private Label  _nameLabel;
    [Export] private Label  _textLabel;
    [Export] private Button _btnNext;

    [Export] public float TypewriterSpeed { get; set; } = 0.03f;

    private string _fullText  = "";
    private bool   _isTyping  = false;

    [Signal] public delegate void DialogueFinishedEventHandler();

    public override void _Ready()
    {
        _btnNext?.Connect(Button.SignalName.Pressed, Callable.From(OnNext));
        Hide();
    }

    public async Task Show(string speakerName, string text)
    {
        if (_nameLabel != null) _nameLabel.Text = speakerName;
        if (_textLabel != null) _textLabel.Text = "";
        _fullText = text;
        _isTyping = true;
        base.Show();

        foreach (char c in text)
        {
            if (!_isTyping) break; // skip triggered
            if (_textLabel != null) _textLabel.Text += c;
            await ToSignal(GetTree().CreateTimer(TypewriterSpeed), SceneTreeTimer.SignalName.Timeout);
        }

        if (_textLabel != null) _textLabel.Text = _fullText;
        _isTyping = false;
    }

    private void OnNext()
    {
        if (_isTyping)
        {
            _isTyping = false; // skip — vòng foreach sẽ break
        }
        else
        {
            Hide();
            EmitSignal(SignalName.DialogueFinished);
        }
    }
}
