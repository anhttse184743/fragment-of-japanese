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
    }

    public void PlayBgm(AudioStream stream, bool loop = true)
    {
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
}
