using Godot;
using PhantomCamera;

namespace FragmentOfJapanese.Entities.Player;

/// <summary>
/// Chỉ phụ trách phần ĐỘNG của camera: xoay NGANG (yaw) quanh nhân vật theo kéo chuột / vuốt.
///
/// Mọi thứ TĨNH — góc nhìn dọc (pitch), khoảng cách lùi (spring_length), FOV, độ mượt (damping),
/// offset... — đều lấy từ những gì bạn chỉnh TAY trên node <c>PlayerPhantomCamera3D</c> trong
/// Godot editor. Script này KHÔNG đè lên chúng.
///
/// Pitch được đọc 1 lần lúc đầu rồi GIỮ NGUYÊN (khóa) → giữ chất 2.5D xoay ngang.
/// LƯU Ý: script chỉ chạy khi CHẠY GAME (F5/F6), KHÔNG chạy trong editor.
/// </summary>
public partial class CameraRig : Node
{
    [Export] private Node3D _pcamNode;            // = %PlayerPhantomCamera3D

    [Export] public float Sensitivity = 0.25f;    // độ / pixel kéo
    [Export] public bool  InvertDrag  = false;    // đảo chiều kéo nếu thấy ngược
    [Export] public bool  Debug       = true;     // in log chẩn đoán ra Output

    private PhantomCamera3D _pcam;
    private float _yaw;
    private float _pitch;

    public override void _Ready()
    {
        // Bảo đảm node nhận sự kiện input (phòng trường hợp không auto-enable).
        SetProcessUnhandledInput(true);

        if (_pcamNode == null)
        {
            GD.PrintErr("[CameraRig] _pcamNode = NULL → chưa gán node PlayerPhantomCamera3D trong scene. Camera sẽ không xoay.");
            return;
        }

        _pcam = _pcamNode.AsPhantomCamera3D();
        CallDeferred(nameof(ReadInitialFromEditor));
    }

    private void ReadInitialFromEditor()
    {
        if (_pcam == null) return;
        var rot = _pcam.GetThirdPersonRotationDegrees();
        _pitch = rot.X;
        _yaw   = rot.Y;

        if (Debug)
            GD.Print($"[CameraRig] Sẵn sàng. FollowMode={_pcam.FollowMode} (cần ThirdPerson), pitch={_pitch:0.0}, yaw={_yaw:0.0}");
    }

    public override void _UnhandledInput(InputEvent ev)
    {
        if (_pcam == null) return;

        float dx;
        if (ev is InputEventScreenDrag drag)
            dx = drag.Relative.X;
        else if (ev is InputEventMouseMotion mm &&
                 (mm.ButtonMask & (MouseButtonMask.Left | MouseButtonMask.Right)) != 0)
            dx = mm.Relative.X;
        else
            return;

        _yaw += (InvertDrag ? dx : -dx) * Sensitivity;
        _pcam.SetThirdPersonRotationDegrees(new Vector3(_pitch, _yaw, 0f));

        if (Debug)
            GD.Print($"[CameraRig] kéo dx={dx:0.0} → yaw={_yaw:0.0}");
    }
}
