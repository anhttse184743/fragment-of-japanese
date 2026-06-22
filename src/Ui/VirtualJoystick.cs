using Godot;
using FragmentOfJapanese.Entities.Player;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// Joystick ảo cảm ứng (mobile) cho di chuyển. Tự vẽ — không cần ảnh. Đặt góc dưới-trái.
/// Kéo núm → set <c>PlayerController.TouchInput</c> (hướng + độ lớn 0..1 = analog, đi nhanh/chậm theo độ đẩy).
///
/// - KHÓA theo đúng ngón đã chạm → đa chạm OK (ngón kia xoay camera không kéo nhầm joystick).
/// - Remap deadzone → tốc độ tăng mượt từ 0, không nhảy bậc.
/// - Tự "nuốt" input trong vùng → không xoay camera / không đánh nhầm.
/// - Chạy được bằng chuột trên desktop để test. Tự tìm player qua group "player".
/// Kéo scene này vào World là xong.
/// </summary>
public partial class VirtualJoystick : Control
{
    [Export] public float Radius   = 90f;     // bán kính kéo tối đa (px)
    [Export] public float DeadZone = 0.2f;    // vùng chết (0..1)
    [Export] public Color BaseColor = new(1f, 1f, 1f, 0.20f);
    [Export] public Color RingColor = new(1f, 1f, 1f, 0.55f);
    [Export] public Color KnobColor = new(1f, 1f, 1f, 0.75f);

    public Vector2 Output { get; private set; }

    private int  _touchId = -1;   // index ngón đang giữ joystick (-1 = không)
    private bool _mouse;          // chuột đang giữ (desktop test)
    private Vector2 _knob;
    private PlayerController _controller;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ResolveController();
        QueueRedraw();
    }

    private Vector2 Center => Size * 0.5f;

    public override void _Draw()
    {
        DrawCircle(Center, Radius, BaseColor);
        DrawArc(Center, Radius, 0f, Mathf.Tau, 48, RingColor, 3f, true);
        DrawCircle(Center + _knob, Radius * 0.42f, KnobColor);
    }

    public override void _Input(InputEvent ev)
    {
        switch (ev)
        {
            // ----- Cảm ứng: khóa theo đúng ngón -----
            case InputEventScreenTouch t when t.Pressed && _touchId == -1 && InArea(t.Position):
                _touchId = t.Index; Begin(t.Position); Consume(); break;
            case InputEventScreenTouch t when !t.Pressed && t.Index == _touchId:
                _touchId = -1; End(); Consume(); break;
            case InputEventScreenDrag d when d.Index == _touchId:
                MoveKnob(d.Position); Consume(); break;

            // ----- Chuột (desktop) -----
            case InputEventMouseButton mb when mb.ButtonIndex == MouseButton.Left:
                if (mb.Pressed && !_mouse && InArea(mb.Position)) { _mouse = true; Begin(mb.Position); Consume(); }
                else if (!mb.Pressed && _mouse)                   { _mouse = false; End(); Consume(); }
                break;
            case InputEventMouseMotion m when _mouse:
                MoveKnob(m.Position); Consume(); break;
        }
    }

    private void Begin(Vector2 pos) => MoveKnob(pos);

    private void End()
    {
        _knob = Vector2.Zero;
        SetOutput(Vector2.Zero);
        QueueRedraw();
    }

    private void MoveKnob(Vector2 globalPos)
    {
        _knob = (globalPos - (GlobalPosition + Center)).LimitLength(Radius);
        QueueRedraw();

        var dir   = _knob / Radius;          // hướng + độ lớn 0..1
        float mag = dir.Length();
        if (mag < DeadZone)
            SetOutput(Vector2.Zero);
        else
            SetOutput(dir.Normalized() * Mathf.Min(1f, (mag - DeadZone) / (1f - DeadZone)));  // remap → analog mượt
    }

    private void SetOutput(Vector2 v)
    {
        Output = v;
        ResolveController();
        if (_controller != null) _controller.TouchInput = v;
    }

    private void Consume() => GetViewport().SetInputAsHandled();

    private bool InArea(Vector2 globalPos)
        => globalPos.DistanceTo(GlobalPosition + Center) <= Radius * 1.6f;

    private void ResolveController()
        => _controller ??= (GetTree().GetFirstNodeInGroup("player") as Node)
                           ?.GetNodeOrNull<PlayerController>("Controller");
}
