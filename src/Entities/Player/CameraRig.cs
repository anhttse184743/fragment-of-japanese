using Godot;
using PhantomCamera;

namespace FragmentOfJapanese.Entities.Player;

/// <summary>
/// Chỉ phụ trách phần ĐỘNG của camera: xoay NGANG (yaw) quanh nhân vật theo kéo chuột / vuốt.
///
/// Mọi thứ TĨNH — khoảng cách lùi (spring_length), FOV, độ mượt (damping), offset... — lấy từ
/// node <c>PlayerPhantomCamera3D</c> chỉnh trong editor. Riêng PITCH (góc cúi) thì script KẸP lại
/// theo <see cref="MaxPitchDeg"/> để camera không chúi quá xuống đất.
///
/// Hai xử lý chống lỗi lúc vào game:
///  1. Pitch chỉ áp dụng SAU KHI SpringArm của Phantom Camera dựng xong (cờ <c>_has_follow_spring_arm</c>) —
///     nếu set sớm sẽ bị addon chặn rồi ghi đè (đó là lý do trước đây "vẫn vậy").
///  2. Lúc spawn TẮT follow_damping trong <see cref="SpawnSnapTime"/> giây để camera BÁM NGAY nhân vật,
///     tránh lerp từ vị trí camera ban đầu (gần mặt đất) lên chỗ spawn cao → hết cú "quét/chiếu xuống đất".
///
/// LƯU Ý: chỉ chạy khi CHẠY GAME (F5/F6), KHÔNG chạy trong editor.
/// </summary>
public partial class CameraRig : Node
{
    [Export] private Node3D _pcamNode;            // = %PlayerPhantomCamera3D

    [Export] public float Sensitivity   = 0.25f;  // độ / pixel kéo
    [Export] public bool  InvertDrag     = false; // đảo chiều kéo nếu thấy ngược
    [Export] public float MaxPitchDeg    = 14f;   // KẸP độ cúi camera (độ) — nhỏ hơn = nhìn ngang hơn
    [Export] public float SpawnSnapTime  = 0.6f;  // giây tắt damping lúc spawn để camera bám ngay (đỡ quét qua đất)
    [Export] public bool  Debug          = true;  // in log chẩn đoán ra Output

    private PhantomCamera3D _pcam;
    private float _yaw;
    private float _pitch;
    private bool  _applied;     // đã kẹp + áp pitch xong chưa (đợi SpringArm sẵn sàng)

    public override void _Ready()
    {
        SetProcessUnhandledInput(true);

        if (_pcamNode == null)
        {
            GD.PrintErr("[CameraRig] _pcamNode = NULL → chưa gán node PlayerPhantomCamera3D trong scene. Camera sẽ không xoay.");
            SetProcess(false);
            return;
        }

        _pcam = _pcamNode.AsPhantomCamera3D();

        // (2) Spawn snap: tắt damping để camera bám ngay nhân vật, rồi bật lại sau SpawnSnapTime.
        _pcamNode.Set("follow_damping", false);
        var timer = GetTree().CreateTimer(SpawnSnapTime);
        timer.Timeout += () =>
        {
            if (GodotObject.IsInstanceValid(_pcamNode))
                _pcamNode.Set("follow_damping", true);
        };
    }

    public override void _Process(double _delta)
    {
        if (_applied || _pcam == null) return;

        // (1) Chờ SpringArm dựng xong mới đọc/ghi được third-person rotation (tránh bị guard chặn).
        if (!_pcamNode.Get("_has_follow_spring_arm").AsBool()) return;

        var rot = _pcam.GetThirdPersonRotationDegrees();
        _yaw   = rot.Y;
        // Giữ DẤU góc cúi của editor nhưng KẸP độ lớn để camera không chúi quá xuống đất.
        _pitch = Mathf.Clamp(rot.X, -MaxPitchDeg, MaxPitchDeg);
        _pcam.SetThirdPersonRotationDegrees(new Vector3(_pitch, _yaw, 0f));
        _applied = true;

        if (Debug)
            GD.Print($"[CameraRig] Sẵn sàng. pitch editor={rot.X:0.0} → dùng={_pitch:0.0}, yaw={_yaw:0.0}");
    }

    public override void _UnhandledInput(InputEvent ev)
    {
        if (_pcam == null || !_applied) return;   // chờ áp pitch xong rồi mới cho xoay

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
