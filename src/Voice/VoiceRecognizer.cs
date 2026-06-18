using Godot;
using System.Collections.Generic;
using System.Text.Json;
using Vosk;
using FragmentOfJapanese.Autoloads;
using FragmentOfJapanese.Core;

namespace FragmentOfJapanese.Voice;

/// <summary>
/// Spike nhận dạng giọng đọc tiếng Nhật bằng Vosk (offline).
/// Lấy danh sách từ của một bài (JapaneseDB.GetHiraganaReadable) làm "grammar" để khóa
/// bộ nhận dạng → người chơi đọc 1 từ trong bài, so khớp với cách đọc kana.
/// Chạy ở editor/desktop. Cần model ở data/vosk/ + libvosk.dll — xem data/vosk/README.md.
/// </summary>
public partial class VoiceRecognizer : Node
{
	[Signal] public delegate void WordRecognizedEventHandler(string rawText, bool matched, string matchedId);

	[Export] public bool  UseGrammar = true;   // tắt để test nhận dạng tự do (xem model phiên âm ra chữ gì)
	[Export] public bool  MuteMicBus = false;  // true = hạ volume cho khỏi nghe tiếng mình (capture vẫn chạy)
	[Export] public float MicGain    = 12f;    // khuếch đại mic yếu (đỉnh thô ~0.03 → cần ×10-20)

	private const string ModelResPath = "res://data/vosk/vosk-model-small-ja-0.22";
	private const float  VoskRate     = 16000f;

	private Model _model;
	private VoskRecognizer _rec;
	private AudioEffectCapture _capture;
	private AudioStreamPlayer _micPlayer;
	private int _busIdx = -1;
	private bool _listening;
	private double _resamplePos;
	private long _samplesFed;   // chẩn đoán: số mẫu đã đẩy vào Vosk
	private float _peak;        // chẩn đoán: biên độ đỉnh THÔ (trước gain); 0 = im lặng
	private List<VocabularyEntry> _candidates = new();

	public override void _Ready()
	{
		global::Vosk.Vosk.SetLogLevel(-1);

		string modelPath = ProjectSettings.GlobalizePath(ModelResPath);
		if (!System.IO.Directory.Exists(modelPath))
		{
			GD.PushError($"[Voice] Chưa thấy model Vosk: {modelPath}\n→ Tải vosk-model-small-ja-0.22 giải nén vào data/vosk/ (xem data/vosk/README.md).");
			return;
		}

		_model = new Model(modelPath);
		SetupMicBus();
		GD.Print("[Voice] Model + mic sẵn sàng.");
		GD.Print($"[Voice] Mic đang dùng: '{AudioServer.InputDevice}' | Các mic: {string.Join(" | ", AudioServer.GetInputDeviceList())}");
	}

	private void SetupMicBus()
	{
		AudioServer.AddBus();
		_busIdx = AudioServer.BusCount - 1;
		AudioServer.SetBusName(_busIdx, "VoskCapture");
		_capture = new AudioEffectCapture();
		AudioServer.AddBusEffect(_busIdx, _capture);
		// KHÔNG bus-mute: mute làm AudioEffectCapture nhận toàn 0.
		// Muốn khỏi nghe tiếng mình thì hạ volume (áp dụng SAU effect nên capture vẫn full tín hiệu).
		if (MuteMicBus) AudioServer.SetBusVolumeDb(_busIdx, -80f);

		_micPlayer = new AudioStreamPlayer();
		_micPlayer.Stream = new AudioStreamMicrophone();
		_micPlayer.Bus = "VoskCapture";
		AddChild(_micPlayer);
		_micPlayer.Play();
	}

	public void StartListening(int lesson)
	{
		if (_model == null) { GD.PushWarning("[Voice] Model chưa sẵn sàng."); return; }
		if (JapaneseDB.Instance == null) { GD.PushWarning("[Voice] JapaneseDB chưa nạp (autoload?)."); return; }

		_candidates = new List<VocabularyEntry>(JapaneseDB.Instance.GetHiraganaReadable(lesson));
		if (_candidates.Count == 0) { GD.PushWarning($"[Voice] Bài {lesson} không có từ hiragana."); return; }

		_rec?.Dispose();
		if (UseGrammar)
		{
			var words = new List<string>();
			foreach (var v in _candidates) words.Add(v.Kana);
			words.Add("[unk]");
			string grammar = JsonSerializer.Serialize(words);
			_rec = new VoskRecognizer(_model, VoskRate, grammar);
			GD.Print($"[Voice] Nghe Bài {lesson} — {_candidates.Count} từ. Grammar: {grammar}");
		}
		else
		{
			_rec = new VoskRecognizer(_model, VoskRate);
			GD.Print($"[Voice] Nghe Bài {lesson} — nhận dạng TỰ DO (không grammar).");
		}

		_capture.ClearBuffer();
		_resamplePos = 0;
		_samplesFed = 0;
		_peak = 0f;
		_listening = true;
	}

	public void StopListening()
	{
		if (!_listening || _rec == null) return;
		_listening = false;

		string json = _rec.FinalResult();
		string raw = ExtractText(json);
		GD.Print($"[Voice] Đã nạp {_samplesFed} mẫu (~{_samplesFed / 16000.0:0.0}s), đỉnh THÔ={_peak:0.000} (gain ×{MicGain}). [thô≈0=mic câm · ~0.0x=mic quá nhỏ · >0.2=tốt]");
		var (matched, id) = Match(raw);
		GD.Print($"[Voice] Thô='{raw}'  matched={matched} id={id}  (JSON {json})");
		EmitSignal(SignalName.WordRecognized, raw, matched, id ?? "");
	}

	public override void _Process(double delta)
	{
		if (!_listening || _rec == null || _capture == null) return;
		int avail = _capture.GetFramesAvailable();
		if (avail <= 0) return;

		byte[] pcm = ResampleToPcm16Mono(_capture.GetBuffer(avail));
		if (pcm.Length > 0)
		{
			_rec.AcceptWaveform(pcm, pcm.Length);
			_samplesFed += pcm.Length / 2;
		}
	}

	// Downmix mono + khuếch đại (MicGain) + hạ tần số mixRate→16k + float→int16 little-endian
	private byte[] ResampleToPcm16Mono(Vector2[] frames)
	{
		if (frames.Length == 0) return System.Array.Empty<byte>();
		double step = AudioServer.GetMixRate() / VoskRate;
		var outBytes = new List<byte>();
		while (_resamplePos < frames.Length)
		{
			Vector2 f = frames[(int)_resamplePos];
			float mono = (f.X + f.Y) * 0.5f;
			if (Mathf.Abs(mono) > _peak) _peak = Mathf.Abs(mono);   // đỉnh THÔ (trước gain) để chẩn đoán
			float amp = Mathf.Clamp(mono * MicGain, -1f, 1f);
			short s = (short)(amp * 32767f);
			outBytes.Add((byte)(s & 0xFF));
			outBytes.Add((byte)((s >> 8) & 0xFF));
			_resamplePos += step;
		}
		_resamplePos -= frames.Length;
		return outBytes.ToArray();
	}

	private static string ExtractText(string json)
	{
		try
		{
			using var doc = JsonDocument.Parse(json);
			if (doc.RootElement.TryGetProperty("text", out var t))
				return (t.GetString() ?? "").Replace(" ", "");
		}
		catch { }
		return "";
	}

	private (bool, string) Match(string raw)
	{
		if (string.IsNullOrEmpty(raw) || raw == "[unk]") return (false, null);
		foreach (var v in _candidates)
			if (raw == v.Kana.Replace(" ", "") || raw == v.Kanji.Replace(" ", "") || raw == v.Romaji.Replace(" ", ""))
				return (true, v.Id);
		return (false, null);
	}

	public override void _ExitTree()
	{
		_rec?.Dispose();
		_model?.Dispose();
		if (_busIdx >= 0 && _busIdx < AudioServer.BusCount)
			AudioServer.RemoveBus(_busIdx);
	}
}
