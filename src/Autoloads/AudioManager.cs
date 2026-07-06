using Godot;

namespace FragmentOfJapanese.Autoloads;

public partial class AudioManager : Node
{
    public static AudioManager Instance { get; private set; }

    private AudioStreamPlayer _bgmPlayer;
    private AudioStreamPlayer _sfxPlayer;

    public override void _Ready()
    {
        Instance = this;

        _bgmPlayer = new AudioStreamPlayer();
        _sfxPlayer = new AudioStreamPlayer();
        AddChild(_bgmPlayer);
        AddChild(_sfxPlayer);

        var bgm = GD.Load<AudioStream>("res://assets/audio/bgm/bgm.mp3");
        if (bgm != null)
        {
            PlayBgm(bgm);
        }
    }

    public void PlayBgm(AudioStream stream, bool loop = true)
    {
        if (stream is AudioStreamMP3 mp3) mp3.Loop = loop;
        else if (stream is AudioStreamOggVorbis ogg) ogg.Loop = loop;
        else if (stream is AudioStreamWav wav) wav.LoopMode = loop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled;

        if (_bgmPlayer.Stream == stream && _bgmPlayer.Playing) return;
        _bgmPlayer.Stream = stream;
        _bgmPlayer.Play();
    }

    public void PlaySfx(AudioStream stream)
    {
        _sfxPlayer.Stream = stream;
        _sfxPlayer.Play();
    }

    public void StopBgm() => _bgmPlayer.Stop();

    public void SetBgmVolume(float linear) =>
        _bgmPlayer.VolumeDb = Mathf.LinearToDb(Mathf.Clamp(linear, 0f, 1f));

    public float GetBgmVolume() => Mathf.DbToLinear(_bgmPlayer.VolumeDb);

    public void SetSfxVolume(float linear) =>
        _sfxPlayer.VolumeDb = Mathf.LinearToDb(Mathf.Clamp(linear, 0f, 1f));

    public float GetSfxVolume() => Mathf.DbToLinear(_sfxPlayer.VolumeDb);
}
