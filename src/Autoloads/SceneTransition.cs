using Godot;
using FragmentOfJapanese.World;

namespace FragmentOfJapanese.Autoloads;

public partial class SceneTransition : Node
{
    public static SceneTransition Instance { get; private set; }

    /// <summary>Đường dẫn scene VỪA RỜI (đặt trong <see cref="GoTo"/>). Khi tới map mới, người chơi được
    /// đặt tại cổng của map mới có <c>ScenePath</c> trỏ VỀ map này → đi cổng nào về cổng nấy.</summary>
    public static string FromScenePath { get; private set; }

    /// <summary>Đặt true TRƯỚC <see cref="GoTo"/> để lần chuyển tới KHÔNG đặt người chơi tại cổng
    /// (dùng spawn mặc định) — vd khi gục về làng. Tự reset sau mỗi lần chuyển.</summary>
    public static bool ForceDefaultArrival { get; set; }

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
    /// Sau khi sang map mới, đặt người chơi tại cổng quay-về (nếu có) — làm khi màn còn đen.
    /// </summary>
    public async void GoTo(string scenePath, bool allowAd = true)
    {
        if (_transitioning) { GD.Print("[SceneTransition] Đang chuyển cảnh — bỏ qua yêu cầu chồng."); return; }
        if (string.IsNullOrEmpty(scenePath)) { GD.PushError("[SceneTransition] scenePath rỗng."); return; }
        if (!ResourceLoader.Exists(scenePath)) { GD.PushError($"[SceneTransition] Không thấy scene: {scenePath}"); return; }

        _transitioning = true;
        string from           = GetTree().CurrentScene?.SceneFilePath;   // map đang rời
        FromScenePath         = from;
        bool   defaultArrival = ForceDefaultArrival;
        ForceDefaultArrival   = false;

        try
        {
            if (GetTree().Paused) GetTree().Paused = false;   // không để pause kẹt qua màn

            await FadeTo(1f, 0.25f);
            // Không hiện quảng cáo khi chuyển cảnh (gây khó chịu) — chỉ dùng rewarded "xem QC nhận thưởng".

            var err = GetTree().ChangeSceneToFile(scenePath);
            if (err != Error.Ok) GD.PushError($"[SceneTransition] Mở scene lỗi ({err}): {scenePath}");

            // Đặt người chơi tại cổng quay-về KHI MÀN CÒN ĐEN (tránh thấy nhân vật nhảy vị trí).
            if (!defaultArrival)
                await PlaceAtReturnPortal(from);

            await FadeTo(0f, 0.25f);
        }
        finally
        {
            if (GodotObject.IsInstanceValid(_overlay)) _overlay.Color = new Color(0, 0, 0, 0);
            _transitioning = false;
        }
    }

    /// <summary>
    /// Đặt người chơi tại cổng (ở scene MỚI) có ScenePath trỏ VỀ <paramref name="fromScene"/>.
    /// Chờ scene + người chơi sẵn sàng (PlayerSpawn tạo trễ). Map đích không có cổng quay-về → giữ vị trí mặc định.
    /// </summary>
    private async System.Threading.Tasks.Task PlaceAtReturnPortal(string fromScene)
    {
        if (string.IsNullOrEmpty(fromScene)) return;

        Node3D portal = null, player = null;
        for (int i = 0; i < 40; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            portal ??= FindReturnPortal(fromScene);
            player ??= GetTree().GetFirstNodeInGroup("player") as Node3D;
            if (portal != null && player != null) break;
            if (i >= 5 && portal == null) return;   // map đích rõ ràng không có cổng quay-về → thôi
        }
        if (portal == null || player == null) return;

        player.GlobalPosition = portal.GlobalPosition;          // đứng NGAY tại cổng
        if (player is CharacterBody3D body) body.Velocity = Vector3.Zero;
    }

    private Node3D FindReturnPortal(string fromScene)
    {
        foreach (var n in GetTree().GetNodesInGroup("portal"))
            if (n is Portal p && ScenePathsEqual(p.ScenePath, fromScene))
                return p;
        return null;
    }

    /// <summary>So path scene: khớp tuyệt đối, hoặc cùng tên file (phòng res:// vs uid://).</summary>
    private static bool ScenePathsEqual(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        if (a == b) return true;
        return System.IO.Path.GetFileName(a) == System.IO.Path.GetFileName(b);
    }

    private async System.Threading.Tasks.Task FadeTo(float alpha, float duration)
    {
        _tween?.Kill();
        _tween = CreateTween();   // tween thuộc node autoload (ProcessMode mặc định) — GoTo gọi khi đã bỏ pause
        _tween.TweenProperty(_overlay, "color:a", alpha, duration);
        await ToSignal(_tween, Tween.SignalName.Finished);
    }
}
