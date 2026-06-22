using Godot;
using FragmentOfJapanese.Entities.Player;

namespace FragmentOfJapanese.Ui;

/// <summary>
/// Cụm nút hành động góc dưới-phải (mobile). Nút to: ATK / JUMP. Nút nhỏ: VOICE / DRAW / FAST.
///   - ATK  → PlayerController.RequestAttack()
///   - JUMP → PlayerController.RequestJump()
///   - FAST → PlayerController.ToggleRun()  (bật/tắt chạy nhanh; sáng lên khi đang chạy)
///   - VOICE / DRAW → placeholder (đọc từ / viết kana) — nối sau.
/// Tự dựng nút bằng code (bo tròn) + tự tìm Controller qua group "player". Là 1 UI riêng.
/// </summary>
public partial class ActionButtons : Control
{
    private PlayerController _controller;
    private Button _fastBtn;

    public override void _Ready()
    {
        // Nút to (size 96): JUMP sát góc, ATK bên trái nó
        var jump = MakeRound("JUMP", 96,  40,  40, new Color(0.30f, 0.52f, 0.78f));
        var atk  = MakeRound("ATK",  96, 150,  40, new Color(0.74f, 0.30f, 0.30f));

        // Nút nhỏ (size 60): hàng phía trên
        _fastBtn  = MakeRound("FAST",  60,  58, 156, new Color(0.72f, 0.60f, 0.24f));
        var draw  = MakeRound("DRAW",  60, 150, 156, new Color(0.45f, 0.45f, 0.55f), placeholder: true);
        var voice = MakeRound("VOICE", 60, 224, 156, new Color(0.45f, 0.45f, 0.55f), placeholder: true);

        jump.ButtonDown  += () => Ctrl()?.RequestJump();
        atk.ButtonDown   += () => Ctrl()?.RequestAttack();
        _fastBtn.Pressed += OnFast;
        voice.Pressed    += () => UiKit.Toast(this, "Đọc (voice): sắp có");
        draw.Pressed     += () => UiKit.Toast(this, "Viết (draw): sắp có");
    }

    private void OnFast()
    {
        var c = Ctrl();
        if (c == null) return;
        c.ToggleRun();
        _fastBtn.Modulate = c.IsRunning ? new Color(1.5f, 1.5f, 1.5f) : Colors.White;   // sáng khi đang chạy
    }

    /// <summary>Nút bo tròn neo góc dưới-phải. marginRight/Bottom = khoảng cách từ góc tới cạnh phải/đáy nút.</summary>
    private Button MakeRound(string text, int size, int marginRight, int marginBottom, Color col, bool placeholder = false)
    {
        var b = new Button { Text = text, FocusMode = FocusModeEnum.None };
        b.SetAnchorsPreset(LayoutPreset.BottomRight);
        b.OffsetRight  = -marginRight;
        b.OffsetBottom = -marginBottom;
        b.OffsetLeft   = -marginRight - size;
        b.OffsetTop    = -marginBottom - size;

        int   r       = size / 2;                       // bo tròn = nửa cạnh → hình tròn
        Color normal  = placeholder ? UiKit.Fade(col, 0.6f) : col;
        b.AddThemeStyleboxOverride("normal",        UiKit.Box(normal, r));
        b.AddThemeStyleboxOverride("hover",         UiKit.Box(normal.Lightened(0.12f), r));
        b.AddThemeStyleboxOverride("pressed",       UiKit.Box(normal.Darkened(0.18f), r));
        b.AddThemeStyleboxOverride("hover_pressed", UiKit.Box(normal.Darkened(0.18f), r));
        b.AddThemeStyleboxOverride("focus",         UiKit.Box(new Color(0, 0, 0, 0), r));
        b.AddThemeColorOverride("font_color",         Colors.White);
        b.AddThemeColorOverride("font_hover_color",   Colors.White);
        b.AddThemeColorOverride("font_pressed_color", Colors.White);
        b.AddThemeFontSizeOverride("font_size", size >= 90 ? 18 : 13);

        AddChild(b);
        return b;
    }

    private PlayerController Ctrl()
    {
        if (_controller != null && GodotObject.IsInstanceValid(_controller)) return _controller;
        _controller = (GetTree().GetFirstNodeInGroup("player") as Node)?.GetNodeOrNull<PlayerController>("Controller");
        return _controller;
    }
}
