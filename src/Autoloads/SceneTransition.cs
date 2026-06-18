using Godot;

namespace FragmentOfJapanese.Autoloads;

public partial class SceneTransition : Node
{
    public static SceneTransition Instance { get; private set; }

    private ColorRect _overlay;
    private Tween     _tween;

    public override void _Ready()
    {
        Instance = this;

        // Tạo overlay fade bằng code — không cần scene riêng
        _overlay = new ColorRect
        {
            Color = new Color(0, 0, 0, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, _overlay);
    }

    public async void GoTo(string scenePath)
    {
        await FadeTo(1f, 0.25f);
        GetTree().ChangeSceneToFile(scenePath);
        await FadeTo(0f, 0.25f);
    }

    private async System.Threading.Tasks.Task FadeTo(float alpha, float duration)
    {
        _tween?.Kill();
        _tween = GetTree().CreateTween();
        _tween.TweenProperty(_overlay, "color:a", alpha, duration);
        await ToSignal(_tween, Tween.SignalName.Finished);
    }
}
