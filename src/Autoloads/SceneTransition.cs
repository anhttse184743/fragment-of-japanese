using Godot;

namespace FragmentOfJapanese.Autoloads;

public partial class SceneTransition : Node
{
    public static SceneTransition Instance { get; private set; }

    /// <summary>
    /// Vị trí người chơi cần đứng khi World load lại. Portal gán trước khi GoTo arena;
    /// Spawner đọc + xóa sau khi tạo Player. Null = dùng vị trí PlayerSpawn mặc định (khi chết).
    /// </summary>
    public static Vector3? PlayerStartPosition { get; set; }

    private ColorRect _overlay;
    private Tween     _tween;
    private bool      _transitioning;   // chặn gọi GoTo chồng → tránh kẹt/đổi scene đôi

    public override void _Ready()
    {
        Instance = this;

        // Overlay fade dựng bằng code — không cần scene riêng. ProcessMode=Always để fade chạy kể cả khi pause.
        _overlay = new ColorRect
        {
            Color       = new Color(0, 0, 0, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ProcessMode = Node.ProcessModeEnum.Always,
        };
        _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, _overlay);
    }

    /// <summary>
    /// Chuyển cảnh có fade. Chống gọi chồng; LUÔN gỡ màn đen ở cuối (không kẹt scene).
    /// </summary>
    public async void GoTo(string scenePath, bool allowAd = true)
    {
        if (_transitioning) { GD.Print("[SceneTransition] Đang chuyển cảnh — bỏ qua yêu cầu chồng."); return; }
        if (string.IsNullOrEmpty(scenePath)) { GD.PushError("[SceneTransition] scenePath rỗng."); return; }
        if (!ResourceLoader.Exists(scenePath)) { GD.PushError($"[SceneTransition] Không thấy scene: {scenePath}"); return; }

        _transitioning = true;
        try
        {
            if (GetTree().Paused) GetTree().Paused = false;   // không để pause kẹt qua màn

            await FadeTo(1f, 0.25f);
            if (allowAd && Ads.AdManager.Instance != null)
                await Ads.AdManager.Instance.MaybeShowInterstitialAsync();

            var err = GetTree().ChangeSceneToFile(scenePath);
            if (err != Error.Ok) GD.PushError($"[SceneTransition] Mở scene lỗi ({err}): {scenePath}");

            await FadeTo(0f, 0.25f);
        }
        finally
        {
            if (GodotObject.IsInstanceValid(_overlay)) _overlay.Color = new Color(0, 0, 0, 0);
            _transitioning = false;
        }
    }

    private async System.Threading.Tasks.Task FadeTo(float alpha, float duration)
    {
        _tween?.Kill();
        _tween = CreateTween();   // tween thuộc node autoload (ProcessMode mặc định) — GoTo gọi khi đã bỏ pause
        _tween.TweenProperty(_overlay, "color:a", alpha, duration);
        await ToSignal(_tween, Tween.SignalName.Finished);
    }
}
