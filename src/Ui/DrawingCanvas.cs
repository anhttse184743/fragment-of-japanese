using Godot;
using System.Collections.Generic;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// Canvas vẽ chữ: bắt chạm + chuột (mẫu như VirtualJoystick), mỗi lần chạm xuống→nhấc = 1 nét.
/// Vẽ nét + lưới hướng dẫn bằng _Draw. Ngừng vẽ <see cref="IdleSeconds"/> giây → phát <see cref="Idle"/>
/// để tự chấm. GetStrokes() trả nét (toạ độ local); Clear() xoá để vẽ chữ mới.
/// </summary>
public partial class DrawingCanvas : Control
{
    [Export] public float IdleSeconds = 2.0f;
    [Export] public float StrokeWidth = 7f;
    [Export] public Color InkColor    = new(0.96f, 0.96f, 1f);
    [Export] public Color GridColor   = new(1f, 1f, 1f, 0.10f);
    [Export] public Color FrameColor  = new(1f, 1f, 1f, 0.28f);

    [Signal] public delegate void IdleEventHandler();

    private readonly List<List<Vector2>> _strokes = new();
    private List<Vector2> _current;
    private int   _touchId = -1;
    private bool  _mouse;
    private float _idle;
    private bool  _submitted;

    public bool HasStrokes => _strokes.Count > 0;

    public override void _Ready()
    {
        MouseFilter  = MouseFilterEnum.Stop;
        ClipContents = true;
    }

    public void Clear()
    {
        _strokes.Clear();
        _current   = null;
        _idle      = 0f;
        _submitted = false;
        _touchId   = -1;
        _mouse     = false;
        QueueRedraw();
    }

    /// <summary>Nét người chơi (toạ độ local canvas), không gồm nét đang vẽ dở.</summary>
    public Vector2[][] GetStrokes()
    {
        var outS = new Vector2[_strokes.Count][];
        for (int s = 0; s < _strokes.Count; s++) outS[s] = _strokes[s].ToArray();
        return outS;
    }

    public override void _Process(double delta)
    {
        if (_submitted || _current != null || _strokes.Count == 0) return;
        _idle += (float)delta;
        if (_idle >= IdleSeconds)
        {
            _submitted = true;
            EmitSignal(SignalName.Idle);
        }
    }

    public override void _Input(InputEvent ev)
    {
        switch (ev)
        {
            case InputEventScreenTouch t when t.Pressed && _touchId == -1 && InRect(t.Position):
                _touchId = t.Index; Begin(Local(t.Position)); Consume(); break;
            case InputEventScreenTouch t when !t.Pressed && t.Index == _touchId:
                _touchId = -1; End(); Consume(); break;
            case InputEventScreenDrag d when d.Index == _touchId:
                Add(Local(d.Position)); Consume(); break;

            case InputEventMouseButton mb when mb.ButtonIndex == MouseButton.Left:
                if (mb.Pressed && !_mouse && InRect(mb.Position)) { _mouse = true; Begin(Local(mb.Position)); Consume(); }
                else if (!mb.Pressed && _mouse)                   { _mouse = false; End(); Consume(); }
                break;
            case InputEventMouseMotion m when _mouse:
                Add(Local(m.Position)); Consume(); break;
        }
    }

    private void Begin(Vector2 p)
    {
        if (_submitted) return;            // đã chấm → chờ Clear (UI sẽ xoá khi sang chữ mới)
        _idle = 0f;
        _current = new List<Vector2> { p };
        QueueRedraw();
    }

    private void Add(Vector2 p)
    {
        if (_current == null) return;
        _idle = 0f;
        if (_current.Count == 0 || _current[^1].DistanceTo(p) >= 2f) _current.Add(p);
        QueueRedraw();
    }

    private void End()
    {
        if (_current != null && _current.Count > 0) _strokes.Add(_current);
        _current = null;
        _idle = 0f;
        QueueRedraw();
    }

    private bool    InRect(Vector2 globalPos) => GetGlobalRect().HasPoint(globalPos);
    private Vector2 Local(Vector2 globalPos)  => globalPos - GlobalPosition;
    private void    Consume()                 => GetViewport().SetInputAsHandled();

    public override void _Draw()
    {
        var sz = Size;
        DrawRect(new Rect2(Vector2.Zero, sz), FrameColor, false, 2f);
        DrawLine(new Vector2(sz.X * 0.5f, 0), new Vector2(sz.X * 0.5f, sz.Y), GridColor, 1f);
        DrawLine(new Vector2(0, sz.Y * 0.5f), new Vector2(sz.X, sz.Y * 0.5f), GridColor, 1f);

        foreach (var st in _strokes) DrawStroke(st);
        if (_current != null) DrawStroke(_current);
    }

    private void DrawStroke(List<Vector2> st)
    {
        if (st.Count == 1)      DrawCircle(st[0], StrokeWidth * 0.5f, InkColor);
        else if (st.Count >= 2) DrawPolyline(st.ToArray(), InkColor, StrokeWidth, true);
    }
}
