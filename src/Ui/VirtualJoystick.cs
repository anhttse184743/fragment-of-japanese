using Godot;
using FragmentOfJapanese.Entities.Player;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// Joystick ảo cảm ứng (mobile) cho di chuyển. Đặt góc dưới-trái.
/// Kéo núm → set <c>PlayerController.TouchInput</c> (hướng + độ lớn 0..1 = analog, đi nhanh/chậm theo độ đẩy).
///
/// - KHÓA theo đúng ngón đã chạm → đa chạm OK (ngón kia xoay camera không kéo nhầm joystick).
/// - Remap deadzone → tốc độ tăng mượt từ 0, không nhảy bậc.
/// - Tự "nuốt" input trong vùng → không xoay camera / không đánh nhầm.
/// - Chạy được bằng chuột trên desktop để test. Tự tìm player qua group "player".
///
/// GIAO DIỆN nằm trong <c>VirtualJoystick.tscn</c>: node <b>Base</b> (vòng nền + viền) và <b>Knob</b> (núm) —
/// đổi màu/kích thước/ảnh trong editor. Bán kính kéo (<see cref="Radius"/>) tự lấy theo NỬA bề ngang node Base,
/// nên cứ resize Base trong scene là vùng kéo đổi theo. Kéo scene này vào World là xong.
/// </summary>
public partial class VirtualJoystick : Control
{
    [Export] public float Radius   = 90f;     // bán kính kéo tối đa (px) — tự ghi đè theo node Base nếu có
    [Export] public float DeadZone = 0.2f;    // vùng chết (0..1)

    [Export] private Control _baseNode;        // vòng nền (chỉnh màu/kích thước/ảnh trong scene)
    [Export] private Control _knobCircle;      // núm kéo

    public Vector2 Output { get; private set; }

    private int  _touchId = -1;   // index ngón đang giữ joystick (-1 = không)
    private bool _mouse;          // chuột đang giữ (desktop test)
    private Vector2 _knob;        // độ lệch núm so với tâm (đã kẹp trong Radius)
    private PlayerController _controller;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        
        if (_baseNode != null)
        {
            // Dựa vào kích thước thực tế của Base trong scene và tăng lên 1 chút (20%) cho vừa vặn hơn
            float baseSide = Mathf.Max(_baseNode.Size.X, _baseNode.Size.Y) * 1.2f;
            if (baseSide < 10) baseSide = Mathf.Min(Size.X, Size.Y) * 0.8f; // Dự phòng
            
            // Gỡ neo để không bị parent kéo giãn thành hình bầu dục
            _baseNode.SetAnchorsPreset(LayoutPreset.TopLeft);
            _baseNode.Size = new Vector2(baseSide, baseSide);
            _baseNode.Position = (Size - _baseNode.Size) * 0.5f; // Căn giữa
            Radius = baseSide * 0.5f;
            
            // Cập nhật bo góc để luôn là hình tròn
            if (_baseNode is Panel pBase && pBase.GetThemeStylebox("panel") is StyleBoxFlat style)
            {
                var newStyle = (StyleBoxFlat)style.Duplicate();
                int rad = Mathf.RoundToInt(baseSide * 0.5f);
                newStyle.CornerRadiusTopLeft = rad;
                newStyle.CornerRadiusTopRight = rad;
                newStyle.CornerRadiusBottomRight = rad;
                newStyle.CornerRadiusBottomLeft = rad;
                pBase.AddThemeStyleboxOverride("panel", newStyle);
            }
        }
        
        if (_knobCircle != null)
        {
            _knobCircle.SetAnchorsPreset(LayoutPreset.TopLeft);
            // Nếu núm đang bị méo, lấy cạnh lớn nhất để vuông lại
            float knobSide = Mathf.Max(_knobCircle.Size.X, _knobCircle.Size.Y);
            _knobCircle.Size = new Vector2(knobSide, knobSide);
            
            if (_knobCircle is Panel pKnob && pKnob.GetThemeStylebox("panel") is StyleBoxFlat style)
            {
                var newStyle = (StyleBoxFlat)style.Duplicate();
                int rad = Mathf.RoundToInt(knobSide * 0.5f);
                newStyle.CornerRadiusTopLeft = rad;
                newStyle.CornerRadiusTopRight = rad;
                newStyle.CornerRadiusBottomRight = rad;
                newStyle.CornerRadiusBottomLeft = rad;
                pKnob.AddThemeStyleboxOverride("panel", newStyle);
            }
        }

        ResolveController();
        
        // Cần đảm bảo _knob = 0 và gọi PlaceKnob ngay, nếu dùng ResetKnob() sẽ có tween khi khởi tạo.
        _knob = Vector2.Zero;
        PlaceKnob();
    }

    private Vector2 Center => Size * 0.5f;

    public override void _Input(InputEvent ev)
    {
        if (!IsVisibleInTree()) return;

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
        SetOutput(Vector2.Zero);
        ResetKnob();
    }

    private void MoveKnob(Vector2 globalPos)
    {
        _knob = (globalPos - (GlobalPosition + Center)).LimitLength(Radius);
        PlaceKnob();

        var dir   = _knob / Radius;          // hướng + độ lớn 0..1
        float mag = dir.Length();
        if (mag < DeadZone)
            SetOutput(Vector2.Zero);
        else
            SetOutput(dir.Normalized() * Mathf.Min(1f, (mag - DeadZone) / (1f - DeadZone)));  // remap → analog mượt
    }

    private void ResetKnob()
    {
        if (_knob == Vector2.Zero) 
        {
            PlaceKnob();
            return;
        }
        
        var t = CreateTween();
        t.TweenMethod(Callable.From<Vector2>(v => { _knob = v; PlaceKnob(); }), _knob, Vector2.Zero, 0.15f)
         .SetTrans(Tween.TransitionType.Back)
         .SetEase(Tween.EaseType.Out);
    }

    /// <summary>Đặt node núm vào tâm + độ lệch hiện tại (toạ độ cục bộ trong Control).</summary>
    private void PlaceKnob()
    {
        if (_knobCircle == null) return;
        _knobCircle.Position = Center + _knob - _knobCircle.Size * 0.5f;
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
