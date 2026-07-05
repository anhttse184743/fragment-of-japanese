using Godot;

namespace FragmentOfJapanese.Voice;

/// <summary>
/// Màn hình Test cho Groq Whisper API. 
/// Giữ phím V để ghi âm, thả ra để gửi lên API.
/// </summary>
public partial class VoiceTest : Node
{
	[Export] public int Lesson = 1;

	private VoiceRecognizer _voice;

	public override void _Ready()
	{
		_voice = new VoiceRecognizer();
		// BẠN PHẢI ĐIỀN API KEY CỦA GROQ VÀO ĐÂY TRƯỚC KHI CHẠY
		_voice.GroqApiKey = ""; 
		
		AddChild(_voice);
		_voice.WordRecognized += OnWord;
		
		GD.Print($"[VoiceTest] Giữ phím V để đọc 1 từ Bài {Lesson}, thả ra để gửi lên Groq Cloud.");
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (e is InputEventKey k && k.Keycode == Key.V && !k.Echo)
		{
			if (k.Pressed) _voice.StartListening(Lesson);
			else           _voice.StopListening();
		}
	}

	private void OnWord(string raw, bool matched, string id, float similarity)
	{
		if (matched) GD.Print($"[VoiceTest] ✅ ĐÚNG ({similarity*100:0.0}%): nghe '{raw}' → {id}");
		else         GD.Print($"[VoiceTest] ❌ SAI ({similarity*100:0.0}%). Máy chủ nghe: '{raw}'");
	}
}
