using Godot;
using FragmentOfJapanese.Entities.Player;
using FragmentOfJapanese.Ui;
using EnemyEntity = FragmentOfJapanese.Entities.Enemy.Enemy;

namespace FragmentOfJapanese.World;

/// <summary>
/// Gắn vào root của World.tscn. Lắng nghe Player.Died và hồi sinh tại PlayerSpawn
/// (xử lý chết ở World — trường hợp chết trong dungeon do DungeonController xử lý).
/// </summary>
public partial class WorldController : Node3D
{
    // Loot rơi khi hạ quái NGOÀI map (không có DungeonController). EXP do AttackZone lo (tránh trùng).
    [Export] public string LootItemId  { get; set; } = "item_goblin_ear";
    [Export] public int    LootDropMin { get; set; } = 1;
    [Export] public int    LootDropMax { get; set; } = 2;

    private Player _player;
    private Node3D _spawnNode;
    private readonly RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        // Spawner dùng CallDeferred nên Player chưa tồn tại ngay _Ready.
        // _Process chờ đến khi Player vào scene rồi nối signal.
        // PlayerSpawn là anh/em (sibling) cùng cha với WorldController — phải tìm qua parent.
        _spawnNode = GetParent()?.GetNodeOrNull<Node3D>("PlayerSpawn");

        _rng.Randomize();
        // Quái đặt sẵn (đã trong cây) + quái Spawner sinh sau (qua NodeAdded) đều nối Died → rơi loot.
        HookEnemies(GetTree().Root);
        GetTree().NodeAdded += OnNodeAdded;

        // Đồng bộ lại toàn bộ trạng thái khi vào World (phòng khi tải lại scene / vào thẳng gameplay).
        _ = FragmentOfJapanese.Autoloads.GameSync.SyncAllAsync();
    }

    public override void _ExitTree()
    {
        if (GetTree() != null) GetTree().NodeAdded -= OnNodeAdded;
    }

    private void HookEnemies(Node root)
    {
        if (root is EnemyEntity e) e.Died += OnEnemyKilled;
        foreach (var c in root.GetChildren()) HookEnemies(c);
    }

    private void OnNodeAdded(Node n)
    {
        if (n is EnemyEntity e) e.Died += OnEnemyKilled;
    }

    private void OnEnemyKilled(EnemyEntity enemy)
    {
        int n = _rng.RandiRange(LootDropMin, LootDropMax);
        if (n > 0) _ = FragmentOfJapanese.Items.Inventory.Instance?.GrantAsync(LootItemId, n);
        FragmentOfJapanese.Quests.QuestManager.Instance?.Report("kill", "goblin");
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
