using Godot;
using System.Collections.Generic;
using FragmentOfJapanese.Autoloads;
using FragmentOfJapanese.Core;
using FragmentOfJapanese.Combat;
using FragmentOfJapanese.Entities;
using FragmentOfJapanese.Items;
using FragmentOfJapanese.Learning;
using FragmentOfJapanese.Quests;
using FragmentOfJapanese.Ui;
using EnemyEntity  = FragmentOfJapanese.Entities.Enemy.Enemy;
using PlayerEntity = FragmentOfJapanese.Entities.Player.Player;

namespace FragmentOfJapanese.World;

/// <summary>
/// Điều khiển một ải dungeon (gắn vào root scene arena). Spawn/quản 4–5 quái, gán mini-game học,
/// chạy challenge khi kết liễu, xử thắng/thua, phát thưởng (1 lần) và về World an toàn.
///   Quiz   : kết liễu, sai → hồi 60–80% (tối đa 3 lần).
///   Grammar: thẻ/trắc nghiệm/ghép câu; đúng→ít spawn, sai→spawn chắc chắn (có trần).
///   Reading: 4 đáp án không VI (hiện khi bài ~80–90%); đúng→bonus thưởng+damage cả lượt, sai→mất máu+quái hồi 1 lần.
/// </summary>
public partial class DungeonController : Node3D
{
    [Export(PropertyHint.File, "*.tscn")] public string ReturnScene { get; set; } = "res://scenes/world/World.tscn";
    [Export] public PackedScene EnemyScene { get; set; }
    [Export] public int    MinEnemies     { get; set; } = 4;
    [Export] public int    MaxEnemies     { get; set; } = 5;
    [Export] public float  GameEnemyRatio { get; set; } = 0.85f;
    [Export] public float  PortalCoeff    { get; set; } = 1.0f;
    [Export] public float  SpawnRadius    { get; set; } = 8f;
    [Export] public double QuizTimeout    { get; set; } = 30.0;

    // Bộ game cho phép ở dungeon này (Inspector). Cả 3 đã hiện thực (Pha 1–3).
    [Export] public bool AllowQuiz    { get; set; } = true;
    [Export] public bool AllowGrammar { get; set; } = false;
    [Export] public bool AllowReading { get; set; } = false;
    [Export] public bool AllowDraw    { get; set; } = false;

    // Grammar: đúng → spawn tiếp viện xác suất thấp; sai → spawn chắc chắn. Có trần.
    [Export] public float GrammarCorrectSpawnChance { get; set; } = 0.30f;
    [Export] public int   MaxReinforcements         { get; set; } = 8;

    // Reading
    [Export] public bool  IgnoreReadingGate { get; set; } = false;  // true = bỏ điều kiện 80–90% (để test)
    [Export] public int   ReadingBonusGold  { get; set; } = 40;
    [Export] public float ReadingDamageMult { get; set; } = 1.25f;  // mỗi lần đúng × sát thương
    [Export] public float ReadingDamageCap  { get; set; } = 2.0f;   // trần so với sát thương gốc

    // Loot rơi mỗi khi hạ 1 quái: EXP (chỉ lên cấp) + Vàng + 1–2 Tai Goblin.
    [Export] public string LootItemId  { get; set; } = "item_goblin_ear";
    [Export] public int    LootDropMin { get; set; } = 1;
    [Export] public int    LootDropMax { get; set; } = 2;

    private readonly List<GameKind> _defeated = new();
    private readonly RandomNumberGenerator _rng = new();
    private PlayerEntity _player;
    private AttackZone   _attackZone;
    private int  _baseAtkDamage;
    private int  _alive;
    private int  _reinforcements;
    private int  _bonusGold;
    private int  _goldEarned;
    private int  _earsDropped;
    private bool _resolved;

    public override void _Ready()
    {
        _rng.Randomize();
        _player = GetTree().GetFirstNodeInGroup("player") as PlayerEntity;
        if (_player != null)
        {
            _player.Died += OnPlayerDied;
            _attackZone   = FindAttackZone(_player);
            _baseAtkDamage = _attackZone?.Damage ?? 0;
        }
        SetupEnemies();
    }

    private void SetupEnemies()
    {
        var enemies = new List<EnemyEntity>();
        foreach (var c in GetChildren())
            if (c is EnemyEntity e) enemies.Add(e);

        int target = _rng.RandiRange(MinEnemies, MaxEnemies);
        Vector3 origin = _player?.GlobalPosition ?? GlobalPosition;
        if (EnemyScene != null)
            while (enemies.Count < target)
                enemies.Add(SpawnEnemyAt(origin));

        foreach (var e in enemies) Register(e);
        _alive = enemies.Count;
        GD.Print($"[Dungeon] {_alive} quái, portalCoeff={PortalCoeff}, allow Q/G/R={AllowQuiz}/{AllowGrammar}/{AllowReading}");
    }

    private EnemyEntity SpawnEnemyAt(Vector3 around)
    {
        var e = EnemyScene.Instantiate<EnemyEntity>();
        AddChild(e);
        float a = _rng.RandfRange(0f, Mathf.Tau);
        float r = _rng.RandfRange(SpawnRadius * 0.4f, SpawnRadius);
        e.GlobalPosition = around + new Vector3(Mathf.Cos(a) * r, 1f, Mathf.Sin(a) * r);
        return e;
    }

    private void Register(EnemyEntity e)
    {
        e.Challenge = RollGameKind();
        if (e.Challenge == GameKind.Reading) e.HealCharges = 1;   // đoạn văn: quái hồi 1 lần
        e.Died += OnEnemyDied;
        e.LethalReached += OnLethalReached;
    }

    private GameKind RollGameKind()
    {
        if (_rng.Randf() > GameEnemyRatio) return GameKind.None;
        var opts = new List<GameKind>();
        if (AllowQuiz)    opts.Add(GameKind.Quiz);
        if (AllowGrammar) opts.Add(GameKind.Grammar);
        if (AllowReading && ReadingUnlocked()) opts.Add(GameKind.Reading);
        if (AllowDraw) opts.Add(GameKind.Draw);
        if (opts.Count == 0) return GameKind.None;
        return opts[(int)(_rng.Randi() % (uint)opts.Count)];
    }

    /// <summary>Đoạn văn chỉ xuất hiện khi bài hiện tại đã học ~80% trở lên.</summary>
    private bool ReadingUnlocked()
    {
        if (IgnoreReadingGate) return true;
        var lt = LearningTracker.Instance;
        if (lt == null) return false;
        var s = lt.GetLessonStats(lt.CurrentLesson);
        return s.Total > 0 && (float)s.Mastered / s.Total >= 0.8f;
    }

    // ───────── Kết liễu = challenge ─────────
    private void OnLethalReached(EnemyEntity enemy)
    {
        switch (enemy.Challenge)
        {
            case GameKind.Quiz:    RunQuizFor(enemy);    break;
            case GameKind.Grammar: RunGrammarFor(enemy); break;
            case GameKind.Reading: RunReadingFor(enemy); break;
            case GameKind.Draw:    RunDrawFor(enemy);    break;
            default:               enemy.ResolveChallenge(true); break;
        }
    }

    private void RunQuizFor(EnemyEntity enemy)
    {
        var db = JapaneseDB.Instance;
        var lt = LearningTracker.Instance;
        if (db == null || lt == null || ChallengeUi.Instance == null) { enemy.ResolveChallenge(true); return; }

        var pick   = lt.NextItem(ItemKind.Vocab);
        var target = pick != null ? FindVocab(db, pick.Id) : null;
        if (target == null) { enemy.ResolveChallenge(true); return; }

        var prog = lt.Get(ItemKind.Vocab, target.Id);
        bool showMeaning = prog == null || prog.Seen < 3;

        var pool = db.GetByLesson(lt.CurrentLesson);
        if (pool.Count < 4) pool = new List<VocabularyEntry>(db.VocabN5);

        ChallengeUi.Instance.RunQuiz(target, pool, showMeaning, QuizTimeout, (correct, ms) =>
        {
            lt.Record(ItemKind.Vocab, target.Id, correct, ms);
            if (correct) QuestManager.Instance?.Report("learn");
            enemy.ResolveChallenge(correct);
        });
    }

    private void RunGrammarFor(EnemyEntity enemy)
    {
        var db = JapaneseDB.Instance;
        var lt = LearningTracker.Instance;
        if (db == null || lt == null || ChallengeUi.Instance == null) { enemy.ResolveChallenge(true); return; }

        var pick = lt.NextItem(ItemKind.Grammar);
        var g    = pick != null ? FindGrammar(db, pick.Id) : null;
        if (g == null) { enemy.ResolveChallenge(true); return; }

        int stage = lt.Get(ItemKind.Grammar, g.Id)?.Seen ?? 0;
        var pool  = db.GetGrammarByLesson(lt.CurrentLesson);

        ChallengeUi.Instance.RunGrammar(g, stage, pool, QuizTimeout, (correct, ms) =>
        {
            lt.Record(ItemKind.Grammar, g.Id, correct, ms);
            if (correct) QuestManager.Instance?.Report("learn");
            if (stage > 0)
            {
                bool spawn = correct ? _rng.Randf() < GrammarCorrectSpawnChance : true;
                if (spawn) SpawnReinforcement();
            }
            enemy.ResolveChallenge(true);   // grammar: luôn chết
        });
    }

    private void RunReadingFor(EnemyEntity enemy)
    {
        var db = JapaneseDB.Instance;
        var lt = LearningTracker.Instance;
        if (db == null || lt == null || ChallengeUi.Instance == null) { enemy.ResolveChallenge(true); return; }

        var pick = lt.NextItem(ItemKind.Reading);
        var r    = pick != null ? FindReading(db, pick.Id) : null;
        if (r == null) { enemy.ResolveChallenge(true); return; }

        int seen = lt.Get(ItemKind.Reading, r.Id)?.Seen ?? 0;

        ChallengeUi.Instance.RunReading(r, QuizTimeout, (correct, ms) =>
        {
            lt.Record(ItemKind.Reading, r.Id, correct, ms);
            if (correct)
            {
                _bonusGold += ReadingBonusGold;          // bonus thưởng
                ApplyReadingDamageBonus();               // bonus damage cả lượt ải
                QuestManager.Instance?.Report("learn");
                enemy.ResolveChallenge(true);            // chết
            }
            else
            {
                float pct = seen < 2 ? _rng.RandfRange(0.10f, 0.50f) : _rng.RandfRange(0.20f, 0.80f);
                _player?.LosePercent(pct);               // mất 10–80% máu hiện tại
                enemy.ResolveChallenge(false);           // quái hồi 1 lần
            }
        });
    }

    private void RunDrawFor(EnemyEntity enemy)
    {
        var db = JapaneseDB.Instance;
        if (db == null || ChallengeUi.Instance == null || db.HiraganaStrokes.Count == 0) { enemy.ResolveChallenge(true); return; }

        var k = db.HiraganaStrokes[(int)(_rng.Randi() % (uint)db.HiraganaStrokes.Count)];
        ChallengeUi.Instance.RunDraw(k, KanaGlyph(db, k.Romaji), QuizTimeout, (correct, ms) =>
        {
            if (correct) QuestManager.Instance?.Report("learn");
            enemy.ResolveChallenge(correct);   // đúng → chết; sai/hết giờ → hồi (như quiz)
        });
    }

    private void ApplyReadingDamageBonus()
    {
        if (_attackZone == null || _baseAtkDamage <= 0) return;
        int cap = (int)(_baseAtkDamage * ReadingDamageCap);
        _attackZone.Damage = Mathf.Min(cap, (int)(_attackZone.Damage * ReadingDamageMult));
    }

    private void SpawnReinforcement()
    {
        if (EnemyScene == null || _reinforcements >= MaxReinforcements) return;
        _reinforcements++;
        var e = SpawnEnemyAt(_player?.GlobalPosition ?? GlobalPosition);
        Register(e);
        _alive++;
        GD.Print($"[Dungeon] Quái tiếp viện ({_reinforcements}/{MaxReinforcements}) — còn sống {_alive}");
    }

    private static VocabularyEntry FindVocab(JapaneseDB db, string id)
    { foreach (var v in db.VocabN5)  if (v.Id == id) return v; return null; }
    private static GrammarPoint FindGrammar(JapaneseDB db, string id)
    { foreach (var g in db.Grammar)  if (g.Id == id) return g; return null; }
    private static ReadingPassage FindReading(JapaneseDB db, string id)
    { foreach (var r in db.Readings) if (r.Id == id) return r; return null; }

    private static string KanaGlyph(JapaneseDB db, string romaji)
    { foreach (var h in db.Hiragana) if (h.Romaji == romaji) return h.Kana; return romaji; }

    private static AttackZone FindAttackZone(Node n)
    {
        foreach (var c in n.GetChildren())
        {
            if (c is AttackZone az) return az;
            var found = FindAttackZone(c);
            if (found != null) return found;
        }
        return null;
    }

    // ───────── Thắng / thua ─────────
    private void OnEnemyDied(EnemyEntity enemy)
    {
        _defeated.Add(enemy.Challenge);
        GrantKillLoot(enemy);
        _alive--;
        if (_alive <= 0) Win();
    }

    /// <summary>Hạ 1 quái → EXP + Vàng + 1–2 Tai Goblin vào túi.
    /// EXP challenge enemy cộng ở đây; Challenge=None đã cộng bởi AttackZone khi hp về 0.</summary>
    private void GrantKillLoot(EnemyEntity enemy)
    {
        if (enemy.Challenge != GameKind.None) _player?.GainExp(enemy.ExpReward);

        int gold = Mathf.RoundToInt(RewardCalculator.BaseGoldPerEnemy
                   * RewardCalculator.KindMult(enemy.Challenge) * PortalCoeff);
        Wallet.Instance?.AddGold(gold);
        _goldEarned += gold;

        int ears = _rng.RandiRange(LootDropMin, LootDropMax);
        if (ears > 0) { Inventory.Instance?.Add(LootItemId, ears); _earsDropped += ears; }
    }

    private void Win()
    {
        if (_resolved) return;
        _resolved = true;
        // Vàng/EXP/Tai đã cấp NGAY mỗi lần hạ quái (GrantKillLoot). Win chỉ cộng bonus đọc + tổng kết.
        if (_bonusGold > 0) { Wallet.Instance?.AddGold(_bonusGold); _goldEarned += _bonusGold; }
        GD.Print($"[Dungeon] WIN — tổng +{_goldEarned} Vàng, +{_earsDropped} Tai Goblin ({_defeated.Count} quái).");
        ShowBanner($"🏆 Hoàn thành ải!   +{_goldEarned} Vàng · +{_earsDropped} Tai Goblin");
        Leave();
    }

    private void OnPlayerDied()
    {
        if (_resolved) return;
        _resolved = true;
        _player?.RestoreFull();
        // Xóa vị trí cổng → World sẽ hồi sinh tại PlayerSpawn mặc định (không phải cổng).
        SceneTransition.PlayerStartPosition = null;
        GD.Print("[Dungeon] Người chơi gục — về World, hồi sinh tại spawn.");
        ShowBanner("💀 Bạn đã gục... quay về làng.");
        Leave();
    }

    private void Leave()
    {
        if (SceneTransition.Instance != null) SceneTransition.Instance.GoTo(ReturnScene);
        else GetTree().ChangeSceneToFile(ReturnScene);
    }

    private void ShowBanner(string msg)
    {
        var layer = new CanvasLayer { Layer = 200, ProcessMode = ProcessModeEnum.Always };
        var ctrl  = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        ctrl.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(ctrl);
        GetTree().Root.AddChild(layer);
        UiKit.Toast(ctrl, msg, 2.2f);
        var t = GetTree().CreateTimer(2.8, true, false, true);
        t.Timeout += () => { if (IsInstanceValid(layer)) layer.QueueFree(); };
    }
}
