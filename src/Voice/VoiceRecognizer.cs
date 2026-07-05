using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using FragmentOfJapanese.Autoloads;
using FragmentOfJapanese.Core;

namespace FragmentOfJapanese.Voice;

public partial class VoiceRecognizer : Node
{
	[Signal] public delegate void WordRecognizedEventHandler(string rawText, bool matched, string matchedId, float similarity);

	[Export] public string GroqApiKey = ""; 
	[Export] public bool MuteMicBus = false;

	private AudioEffectCapture _capture;
	private AudioStreamPlayer _micPlayer;
	private int _busIdx = -1;
	private bool _listening = false;
	private List<Vector2> _recordedFrames = new();
	private List<VocabularyEntry> _candidates = new();
	private HttpRequest _httpRequest;

	private const string API_URL = "https://api.groq.com/openai/v1/audio/transcriptions";

	public override void _Ready()
	{
		SetupMicBus();
		
		_httpRequest = new HttpRequest();
		AddChild(_httpRequest);
		_httpRequest.RequestCompleted += OnRequestCompleted;

		GD.Print("[Voice] Đã khởi tạo VoiceRecognizer (Groq API).");
	}

	private void SetupMicBus()
	{
		AudioServer.AddBus();
		_busIdx = AudioServer.BusCount - 1;
		AudioServer.SetBusName(_busIdx, "CloudCapture");
		_capture = new AudioEffectCapture();
		AudioServer.AddBusEffect(_busIdx, _capture);
		
		if (MuteMicBus) AudioServer.SetBusVolumeDb(_busIdx, -80f);

		_micPlayer = new AudioStreamPlayer();
		_micPlayer.Stream = new AudioStreamMicrophone();
		_micPlayer.Bus = "CloudCapture";
		AddChild(_micPlayer);
		_micPlayer.Play();
	}

	public void StartListening(int lesson)
	{
		if (JapaneseDB.Instance != null)
			_candidates = new List<VocabularyEntry>(JapaneseDB.Instance.GetHiraganaReadable(lesson));

		_capture.ClearBuffer();
		_recordedFrames.Clear();
		_listening = true;
		GD.Print($"[Voice] Bắt đầu ghi âm...");
	}

	public override void _Process(double delta)
	{
		if (!_listening || _capture == null) return;
		int avail = _capture.GetFramesAvailable();
		if (avail > 0)
		{
			_recordedFrames.AddRange(_capture.GetBuffer(avail));
		}
	}

	public void StopListening()
	{
		if (!_listening) return;
		_listening = false;
		
		// Flush buffer
		int avail = _capture.GetFramesAvailable();
		if (avail > 0) _recordedFrames.AddRange(_capture.GetBuffer(avail));

		if (string.IsNullOrEmpty(GroqApiKey))
		{
			GD.PushError("[Voice] Thiếu Groq API Key! Hãy nhập vào thuộc tính GroqApiKey của Node VoiceRecognizer.");
			return;
		}

		if (_recordedFrames.Count == 0)
		{
			GD.PushWarning("[Voice] Không thu được âm thanh nào.");
			return;
		}

		GD.Print($"[Voice] Đã ghi âm xong ({_recordedFrames.Count} frames). Đang mã hóa và gửi lên Groq...");
		byte[] wavBytes = GenerateWavBytes(_recordedFrames, (int)AudioServer.GetMixRate());
		SendToGroq(wavBytes);
	}

	private byte[] GenerateWavBytes(List<Vector2> frames, int sampleRate)
	{
		using var ms = new MemoryStream();
		using var bw = new BinaryWriter(ms);

		short numChannels = 1;
		short bitsPerSample = 16;
		int byteRate = sampleRate * numChannels * (bitsPerSample / 8);
		short blockAlign = (short)(numChannels * (bitsPerSample / 8));

		int dataChunkSize = frames.Count * blockAlign;
		int fileSize = 36 + dataChunkSize;

		// RIFF header
		bw.Write(Encoding.ASCII.GetBytes("RIFF"));
		bw.Write(fileSize);
		bw.Write(Encoding.ASCII.GetBytes("WAVE"));

		// fmt subchunk
		bw.Write(Encoding.ASCII.GetBytes("fmt "));
		bw.Write(16); // Subchunk1Size
		bw.Write((short)1); // AudioFormat (PCM)
		bw.Write(numChannels);
		bw.Write(sampleRate);
		bw.Write(byteRate);
		bw.Write(blockAlign);
		bw.Write(bitsPerSample);

		// data subchunk
		bw.Write(Encoding.ASCII.GetBytes("data"));
		bw.Write(dataChunkSize);

		// data payload (convert float Vector2 to 16-bit mono)
		foreach (var f in frames)
		{
			float mono = (f.X + f.Y) * 0.5f;
			mono = Mathf.Clamp(mono * 5.0f, -1f, 1f); // Gain x5
			short s = (short)(mono * 32767f);
			bw.Write(s);
		}

		return ms.ToArray();
	}

	private void SendToGroq(byte[] wavBytes)
	{
		string boundary = "----GodotBoundary" + GD.Randi().ToString();
		string[] headers = new string[]
		{
			$"Authorization: Bearer {GroqApiKey}",
			$"Content-Type: multipart/form-data; boundary={boundary}"
		};

		var body = new List<byte>();

		// Model param
		body.AddRange(Encoding.UTF8.GetBytes($"--{boundary}\r\n"));
		body.AddRange(Encoding.UTF8.GetBytes("Content-Disposition: form-data; name=\"model\"\r\n\r\n"));
		body.AddRange(Encoding.UTF8.GetBytes("whisper-large-v3\r\n"));

		// Language param
		body.AddRange(Encoding.UTF8.GetBytes($"--{boundary}\r\n"));
		body.AddRange(Encoding.UTF8.GetBytes("Content-Disposition: form-data; name=\"language\"\r\n\r\n"));
		body.AddRange(Encoding.UTF8.GetBytes("ja\r\n"));

		// File param
		body.AddRange(Encoding.UTF8.GetBytes($"--{boundary}\r\n"));
		body.AddRange(Encoding.UTF8.GetBytes("Content-Disposition: form-data; name=\"file\"; filename=\"audio.wav\"\r\n"));
		body.AddRange(Encoding.UTF8.GetBytes("Content-Type: audio/wav\r\n\r\n"));
		body.AddRange(wavBytes);
		body.AddRange(Encoding.UTF8.GetBytes("\r\n"));

		// End boundary
		body.AddRange(Encoding.UTF8.GetBytes($"--{boundary}--\r\n"));

		_httpRequest.RequestRaw(API_URL, headers, HttpClient.Method.Post, body.ToArray());
	}

	private void OnRequestCompleted(long result, long responseCode, string[] headers, byte[] body)
	{
		if (responseCode != 200)
		{
			GD.PushError($"[Voice] Lỗi API: Code {responseCode} - {Encoding.UTF8.GetString(body)}");
			return;
		}

		string json = Encoding.UTF8.GetString(body);
		string rawText = "";

		try
		{
			using var doc = JsonDocument.Parse(json);
			if (doc.RootElement.TryGetProperty("text", out var t))
			{
				rawText = t.GetString() ?? "";
			}
		}
		catch { }

		// Xóa khoảng trắng và dấu câu tiếng Nhật thông dụng
		rawText = rawText.Replace(" ", "").Replace("、", "").Replace("。", "").Replace("？", "").Replace("！", "");
		
		var (matched, id, sim) = Match(rawText);
		GD.Print($"[Voice] Groq nghe: '{rawText}' | matched={matched} id={id} sim={sim*100:0.0}%");
		EmitSignal(SignalName.WordRecognized, rawText, matched, id ?? "", sim);
	}

	private (bool, string, float) Match(string raw)
	{
		if (string.IsNullOrEmpty(raw)) return (false, null, 0f);
		
		float bestMatch = 0f;
		string bestId = null;
		
		foreach (var v in _candidates)
		{
			float kanaMatch = CalculateSimilarity(raw, v.Kana.Replace(" ", ""));
			float kanjiMatch = CalculateSimilarity(raw, v.Kanji.Replace(" ", ""));
			float romajiMatch = CalculateSimilarity(raw, v.Romaji.Replace(" ", ""));
			
			float maxForWord = Mathf.Max(kanaMatch, Mathf.Max(kanjiMatch, romajiMatch));
			if (maxForWord > bestMatch)
			{
				bestMatch = maxForWord;
				bestId = v.Id;
			}
		}
		
		return (bestMatch >= 0.7f, bestId, bestMatch);
	}

	private float CalculateSimilarity(string source, string target)
	{
		if (string.IsNullOrEmpty(source)) return string.IsNullOrEmpty(target) ? 1f : 0f;
		if (string.IsNullOrEmpty(target)) return 0f;
		
		int[,] d = new int[source.Length + 1, target.Length + 1];
		for (int i = 0; i <= source.Length; i++) d[i, 0] = i;
		for (int j = 0; j <= target.Length; j++) d[0, j] = j;
		
		for (int i = 1; i <= source.Length; i++)
		{
			for (int j = 1; j <= target.Length; j++)
			{
				int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;
				d[i, j] = Mathf.Min(Mathf.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
			}
		}
		
		int maxLength = Mathf.Max(source.Length, target.Length);
		return 1.0f - ((float)d[source.Length, target.Length] / maxLength);
	}

	public override void _ExitTree()
	{
		if (_busIdx >= 0 && _busIdx < AudioServer.BusCount)
			AudioServer.RemoveBus(_busIdx);
	}
}
