using Godot;

namespace FragmentOfJapanese.Voice;

/// <summary>Harness test: giữ phím V để đọc 1 từ của bài, thả ra để nhận kết quả.</summary>
public partial class VoiceTest : Node
{
	[Export] public int Lesson = 1;

	private VoiceRecognizer _voice;

	public override void _Ready()
	{
		_voice = new VoiceRecognizer();
		AddChild(_voice);
		_voice.WordRecognized += OnWord;
		GD.Print($"[VoiceTest] Giữ phím V để đọc 1 từ Bài {Lesson}, thả ra để nhận kết quả.");
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (e is InputEventKey k && k.Keycode == Key.V && !k.Echo)
		{
			if (k.Pressed) _voice.StartListening(Lesson);
			else           _voice.StopListening();
		}
	}

	private void OnWord(string raw, bool matched, string id)
	{
		if (matched) GD.Print($"[VoiceTest] ✅ ĐÚNG: nghe '{raw}' → {id}");
		else         GD.Print($"[VoiceTest] ❌ chưa khớp. Model nghe: '{raw}'");
	}
}
