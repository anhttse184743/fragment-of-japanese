using Godot;
using FragmentOfJapanese.Entities.Player;
using FragmentOfJapanese.Ui;

namespace FragmentOfJapanese.World;

/// <summary>
/// Gắn vào root của World.tscn. Lắng nghe Player.Died và hồi sinh tại PlayerSpawn
/// (xử lý chết ở World — trường hợp chết trong dungeon do DungeonController xử lý).
/// </summary>
public partial class WorldController : Node3D
{
    private Player _player;
    private Node3D _spawnNode;

    public override void _Ready()
    {
        // Spawner dùng CallDeferred nên Player chưa tồn tại ngay _Ready.
        // _Process chờ đến khi Player vào scene rồi nối signal.
        // PlayerSpawn là anh/em (sibling) cùng cha với WorldController — phải tìm qua parent.
        _spawnNode = GetParent()?.GetNodeOrNull<Node3D>("PlayerSpawn");
    }

    public override void _Process(double delta)
    {
        if (_player == null)
        {
            _player = GetTree().GetFirstNodeInGroup("player") as Player;
            if (_player == null) return;
            _player.Died += OnPlayerDied;
        }

        // Dự phòng: lỡ rớt xuống dưới -100 thì coi như chết.
        if (_player.GlobalPosition.Y < -100f)
            _player.TakeDamage(_player.Data?.MaxHp ?? 9999);
    }

    private void OnPlayerDied()
    {
        _player.RestoreFull();
        _player.GlobalPosition = _spawnNode?.GlobalPosition ?? Vector3.Zero;

        ShowDeathBanner();
        GD.Print("[World] Player chết → hồi sinh tại spawn.");
    }

    private void ShowDeathBanner()
    {
        var layer = new CanvasLayer { Layer = 200, ProcessMode = ProcessModeEnum.Always };
        var ctrl  = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        ctrl.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(ctrl);
        GetTree().Root.AddChild(layer);
        UiKit.Toast(ctrl, "💀 Bạn đã gục...", 2.0f);
        var t = GetTree().CreateTimer(2.6, true, false, true);
        t.Timeout += () => { if (GodotObject.IsInstanceValid(layer)) layer.QueueFree(); };
    }
}
