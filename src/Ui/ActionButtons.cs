using Godot;
using FragmentOfJapanese.Entities.Player;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// Cụm nút hành động góc dưới-phải (mobile). Nút to: ATK / JUMP. Nút nhỏ: VOICE / DRAW / FAST.
///   - ATK  → PlayerController.RequestAttack()
///   - JUMP → PlayerController.RequestJump()
///   - FAST → PlayerController.ToggleRun()  (bật/tắt chạy nhanh; sáng lên khi đang chạy)
///   - VOICE / DRAW → placeholder (đọc từ / viết kana) — nối sau.
///
/// GIAO DIỆN nằm trong <c>ActionButtons.tscn</c> (vị trí/màu/cỡ chữ các nút chỉnh trong editor).
/// Script CHỈ giữ logic + tham chiếu node qua <c>[Export]</c>; tự tìm Controller qua group "player".
/// </summary>
public partial class ActionButtons : Control
{
    [Export] private Button _jumpBtn;
    [Export] private Button _atkBtn;
    [Export] private Button _fastBtn;

    private PlayerController _controller;

    public override void _Ready()
    {
        if (_jumpBtn != null) _jumpBtn.ButtonDown += () => Ctrl()?.RequestJump();
        if (_atkBtn  != null) _atkBtn.ButtonDown  += () => Ctrl()?.RequestAttack();
        if (_fastBtn != null) _fastBtn.Pressed    += OnFast;
        // DrawBtn / VoiceBtn: placeholder trong scene — nối sau (viết kana / đọc từ).
    }

    private void OnFast()
    {
        var c = Ctrl();
        if (c == null || _fastBtn == null) return;
        c.ToggleRun();
        _fastBtn.Modulate = c.IsRunning ? new Color(1.5f, 1.5f, 1.5f) : Colors.White;   // sáng khi đang chạy
    }

    private PlayerController Ctrl()
    {
        if (_controller != null && GodotObject.IsInstanceValid(_controller)) return _controller;
        _controller = (GetTree().GetFirstNodeInGroup("player") as Node)?.GetNodeOrNull<PlayerController>("Controller");
        return _controller;
    }
}
