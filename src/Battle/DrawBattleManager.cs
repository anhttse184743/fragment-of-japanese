using Godot;
using FragmentOfJapanese.Entities.Player;
using FragmentOfJapanese.Entities.Enemy;
using FragmentOfJapanese.Ui;
using FragmentOfJapanese.Core;
using FragmentOfJapanese.Autoloads;

namespace FragmentOfJapanese.Battle;

public partial class DrawBattleManager : Node
{
    private Player _player;
    private Enemy  _enemy;
    private BattleTurn _turnCalc;

    private Timer _timer;
    private DrawBattleUi _ui;
    private JapaneseDB _db;

    private System.Collections.Generic.List<Node> _hiddenHuds = new();

    private Vector3 _originalPlayerPos;
    private Camera3D _originalCamera;
    private Camera3D _battleCamera;
    private Node3D _battleArena;
    private PlayerController _playerController;
    private bool _turnResolved;   // chặn xử lý 2 lần khi vẽ-xong trùng đúng lúc hết giờ

    public static void StartBattleFromWorld()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null) return;

        var player = tree.GetFirstNodeInGroup("player") as Player;
        if (player == null)
        {
            GD.PrintErr("[DrawBattle] Cannot find player in group 'player'.");
            return;
        }

        if (!player.UseMana(5))
        {
            UiKit.GlobalToast("Không đủ Mana! Cần 5 Mana.", 2.5f);
            return;
        }

        // Tải 3D Arena và các vị trí
        var root = tree.CurrentScene;
        var arena = root.FindChild("BattleArena", true, false) as Node3D;
        if (arena == null)
        {
            GD.PrintErr("[DrawBattle] Lỗi: Không tìm thấy node 'BattleArena' trong map.");
            return;
        }

        // Spawn Goblin
        var enemyScene = ResourceLoader.Load<PackedScene>("res://scenes/entities/Goblin.tscn");
        if (enemyScene == null)
        {
            GD.PrintErr("[DrawBattle] Lỗi: Không tìm thấy res://scenes/entities/Goblin.tscn");
            return;
        }

        var enemy = enemyScene.Instantiate<Enemy>();
        // Set fixed stats for mini-game
        enemy.MaxHp = 100;
        enemy.Hp = 100;
        enemy.Attack = 15;
        enemy.Defense = 0;
        enemy.ExpReward = 50;

        // Xóa hẳn AI chạy rong/tấn công real-time của Goblin
        foreach (var child in enemy.GetChildren())
        {
            if (child is EnemyAi ai)
            {
                ai.SetPhysicsProcess(false);
                ai.SetProcess(false);
                ai.QueueFree();
                break;
            }
        }

        var manager = new DrawBattleManager();
        manager.Name = "DrawBattleManager";
        tree.Root.AddChild(manager);
        
        manager.Init(player, enemy, arena);
    }

    public void Init(Player player, Enemy enemy, Node3D arena)
    {
        _player = player;
        _enemy = enemy;
        _battleArena = arena;
        _turnCalc = new BattleTurn(_player, _enemy);
        _db = JapaneseDB.Instance;

        // Lưu trạng thái hiện tại
        _originalPlayerPos = _player.GlobalPosition;
        _originalCamera = GetViewport().GetCamera3D();
        
        // Tìm PlayerController để khoá di chuyển
        _playerController = _player.GetNodeOrNull<PlayerController>("Controller");
        if (_playerController != null)
        {
            _playerController.SetPhysicsProcess(false);
            _playerController.SetProcess(false);
            _player.Velocity = Godot.Vector3.Zero;
        }

        // Ẩn UI điều khiển bên ngoài và lưu lại danh sách để khôi phục
        foreach (Node n in GetTree().GetNodesInGroup("hud"))
        {
            if (n is CanvasItem ci && ci.Visible) 
            {
                ci.Visible = false;
                _hiddenHuds.Add(ci);
            }
            else if (n is CanvasLayer cl && cl.Visible) 
            {
                cl.Visible = false;
                _hiddenHuds.Add(cl);
            }
        }

        // Đặt nhân vật vào Arena
        var pPos = _battleArena.GetNode<Marker3D>("PlayerBattlePos");
        var ePos = _battleArena.GetNode<Marker3D>("EnemyBattlePos");
        _battleCamera = _battleArena.GetNode<Camera3D>("BattleCamera");

        _player.GlobalPosition = pPos.GlobalPosition;
        // Ép animator 2.5D của player quay mặt về phía quái vật
        var pAnim = _player.GetNodeOrNull<PlayerAnimator>("Animator");
        if (pAnim != null) pAnim.FaceTarget(ePos.GlobalPosition);

        // Đặt Enemy vào Arena
        _battleArena.AddChild(_enemy);
        _enemy.GlobalPosition = ePos.GlobalPosition;
        
        // Ép animator 2.5D của quái vật quay mặt về phía player
        var eAnim = _enemy.GetNodeOrNull<FragmentOfJapanese.Entities.CharacterAnimator>("Animator");
        if (eAnim != null) eAnim.FaceTarget(pPos.GlobalPosition);

        // Đổi Camera
        if (_battleCamera != null) _battleCamera.MakeCurrent();

        // Setup Timer
        _timer = new Timer();
        _timer.WaitTime = 10.0f;
        _timer.OneShot = true;
        _timer.Timeout += OnTurnTimeout;
        AddChild(_timer);

        // Open UI
        _ui = DrawBattleUi.Open(this, _player, _enemy);
        
        // Bắt đầu lượt Player
        StartPlayerTurn();
    }

    public override void _PhysicsProcess(double delta)
    {
        // Khi AI/Controller bị tắt, MoveAndSlide() không chạy -> IsOnFloor() sẽ = false.
        // Điều này làm Animator tưởng nhân vật đang trên không và cố chạy animation "jump".
        // Ta dùng 1 trick nhỏ: ép rớt xuống đất 1 lần để cập nhật IsOnFloor() = true.
        
        if (IsInstanceValid(_player) && !_player.IsOnFloor())
        {
            _player.Velocity = new Godot.Vector3(0, -10f, 0);
            _player.MoveAndSlide();
            _player.Velocity = Godot.Vector3.Zero; // Đảm bảo đứng yên để play "idle"
        }

        if (IsInstanceValid(_enemy) && !_enemy.IsOnFloor())
        {
            _enemy.Velocity = new Godot.Vector3(0, -10f, 0);
            _enemy.MoveAndSlide();
            _enemy.Velocity = Godot.Vector3.Zero;
        }
    }

    private void StartPlayerTurn()
    {
        if (_db == null || _db.HiraganaStrokes.Count == 0)
        {
            GD.PrintErr("[DrawBattle] DB không có dữ liệu nét chữ.");
            EndBattle(false);
            return;
        }

        var randIndex = GD.Randi() % _db.HiraganaStrokes.Count;
        var targetStrokes = _db.HiraganaStrokes[(int)randIndex];
        
        string kana = targetStrokes.Romaji;
        foreach (var h in _db.Hiragana) 
        {
            if (h.Romaji == targetStrokes.Romaji) { kana = h.Kana; break; }
        }

        _turnResolved = false;
        _ui.ShowPrompt($"Hãy vẽ chữ: {kana} ({targetStrokes.Romaji})", 10.0f);
        _ui.SetTargetStrokes(targetStrokes);

        _timer.Start();
    }

    public void OnDrawEvaluated(StrokeRecognizer.Result result)
    {
        if (_turnResolved) return;
        _turnResolved = true;
        _timer.Stop();

        float score = result.Pass ? result.Score : 0f;

        if (result.Pass)
        {
            // Người chơi đánh
            TriggerPlayerAttack();
            int damage = _turnCalc != null ? _turnCalc.PlayerAttack() : Mathf.CeilToInt(10 * score);
            
            // Trì hoãn sát thương để chờ Animation chém xong (chạy tới 0.2s + chém 0.4s + lùi về 0.2s)
            GetTree().CreateTimer(1.0f).Timeout += () => 
            {
                if (!IsInstanceValid(this) || !IsInstanceValid(_ui)) return;
                _ui.ShowFeedback($"Chính xác! Gây {damage} sát thương.", Colors.LightGreen);
                _enemy.Hp -= damage;
                FlashRed(_enemy);
                CheckEndGame();
            };
        }
        else
        {
            // Quái đánh
            TriggerEnemyAttack();
            int damage = _turnCalc != null ? _turnCalc.EnemyCounterAttack() : 10;
            
            GetTree().CreateTimer(1.0f).Timeout += () => 
            {
                if (!IsInstanceValid(this) || !IsInstanceValid(_ui)) return;
                _ui.ShowFeedback($"Sai rồi! Bị phản đòn {damage} sát thương.", Colors.Crimson);
                _player.TakeDamage(damage);
                FlashRed(_player);
                CheckEndGame();
            };
        }
    }

    private void OnTurnTimeout()
    {
        if (!IsInstanceValid(this) || _ui == null) return;
        if (_turnResolved) return;
        _turnResolved = true;

        // Player không kịp vẽ -> Quái đánh
        _ui.ShowFeedback("Hết giờ! Quái vật tấn công", Colors.Orange);
        _ui.StopTimer(); // Khóa UI không cho vẽ nữa

        TriggerEnemyAttack();
        int damage = _turnCalc != null ? _turnCalc.EnemyCounterAttack() : 15;
        
        GetTree().CreateTimer(1.0f).Timeout += () => 
        {
            if (!IsInstanceValid(this) || !IsInstanceValid(_ui)) return;
            _ui.ShowFeedback($"Hết giờ! Bị tấn công {damage} sát thương.", Colors.Red);
            _player.TakeDamage(damage);
            FlashRed(_player);
            CheckEndGame();
        };
    }

    private void TriggerPlayerAttack()
    {
        var tween = CreateTween();
        var origPos = _player.GlobalPosition;
        // Phóng tới sát quái vật (cách 1.2m)
        var targetPos = _enemy.GlobalPosition + (_player.GlobalPosition - _enemy.GlobalPosition).Normalized() * 1.2f;
        
        tween.TweenProperty(_player, "global_position", targetPos, 0.2f);
        tween.TweenCallback(Callable.From(() => {
            if (_playerController != null) _playerController.RequestAttack();
        }));
        tween.TweenInterval(0.4f); // Chờ vung kiếm
        tween.TweenProperty(_player, "global_position", origPos, 0.2f); // Lùi về
    }

    private void TriggerEnemyAttack()
    {
        var tween = CreateTween();
        var origPos = _enemy.GlobalPosition;
        // Phóng tới sát người chơi
        var targetPos = _player.GlobalPosition + (_enemy.GlobalPosition - _player.GlobalPosition).Normalized() * 1.2f;

        tween.TweenProperty(_enemy, "global_position", targetPos, 0.2f);
        tween.TweenCallback(Callable.From(() => {
            var animator = _enemy.GetNodeOrNull<FragmentOfJapanese.Entities.CharacterAnimator>("Animator");
            if (animator != null) animator.TriggerAttack();
        }));
        tween.TweenInterval(0.4f); // Chờ đánh
        tween.TweenProperty(_enemy, "global_position", origPos, 0.2f); // Lùi về
    }

    private void FlashRed(Node3D character)
    {
        if (!IsInstanceValid(character)) return;
        var children = character.FindChildren("*", "SpriteBase3D", true, false);
        if (children != null && children.Count > 0 && children[0] is SpriteBase3D sprite)
        {
            sprite.Modulate = Colors.Red;
            var tween = CreateTween();
            tween.TweenProperty(sprite, "modulate", Colors.White, 0.4f).SetTrans(Tween.TransitionType.Cubic);
        }
    }

    private void CheckEndGame()
    {
        _ui.UpdateHealthBars();

        if (_enemy.Hp <= 0)
        {
            _ui.ShowResult(true);
            _player.GainExp(_enemy.ExpReward);
            var _ = _player.AwardAsync(0, 30);
            ScheduleEndBattle(true);
        }
        else if (_player.Data.Hp <= 0)
        {
            _player.RestoreFull(); // Khôi phục trạng thái để người chơi chơi tiếp
            _ui.ShowResult(false);
            ScheduleEndBattle(false);
        }
        else
        {
            GetTree().CreateTimer(2.0f).Timeout += () => {
                if (IsInstanceValid(this)) StartPlayerTurn();
            };
        }
    }

    private void ScheduleEndBattle(bool win)
    {
        // Thắng mini game viết → báo tiến độ quest (daily_write / weekly_write).
        if (win) FragmentOfJapanese.Quests.QuestManager.Instance?.Report("minigame", "write");

        GetTree().CreateTimer(3.0f).Timeout += () => {
            if (IsInstanceValid(this)) 
            {
                MinigameRewardUi.ShowReward("TRẬN CHIẾN VẼ", win, () => EndBattle(win));
            }
        };
    }

    private void EndBattle(bool win)
    {
        if (IsInstanceValid(_ui)) _ui.Close();
        
        // Hiện lại UI bên ngoài dựa trên danh sách đã lưu
        foreach (Node n in _hiddenHuds)
        {
            if (IsInstanceValid(n))
            {
                if (n is CanvasItem ci) ci.Visible = true;
                else if (n is CanvasLayer cl) cl.Visible = true;
            }
        }
        _hiddenHuds.Clear();

        // Dọn Quái
        if (IsInstanceValid(_enemy)) _enemy.QueueFree();

        // Mở lại Camera cũ
        if (_originalCamera != null) _originalCamera.MakeCurrent();

        // Mở lại Player
        _player.GlobalPosition = _originalPlayerPos;
        if (_playerController != null)
        {
            _playerController.SetPhysicsProcess(true);
            _playerController.SetProcess(true);
        }

        QueueFree();
    }
}
