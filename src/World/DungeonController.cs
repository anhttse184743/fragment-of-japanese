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
    [Export] public bool   ScatterIfNoPoints { get; set; } = true;   // không có điểm spawn (group "enemy_spawn") → rải quanh người chơi

    // Spawn theo ĐỢT (không ra hết 1 lượt). Tổng quái vẫn = Min..MaxEnemies, chia thành đợt tăng dần.
    [Export] public bool   UseWaves        { get; set; } = true;     // false = ra hết 1 lượt như cũ
    [Export] public int    FirstWaveSize   { get; set; } = 1;        // số quái đợt đầu
    [Export] public int    WaveIncrement   { get; set; } = 1;        // mỗi đợt sau +bao nhiêu (1 → 1,2,3...)
    [Export] public int    NextWaveAtAlive { get; set; } = 0;        // còn ≤ bấy nhiêu quái sống thì ra đợt kế (0 = dọn sạch mới ra)
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
    private readonly List<Node3D>   _spawnPoints = new();   // marker group "enemy_spawn" (nếu có)
    private readonly List<int>      _waveSizes   = new();   // kích thước từng đợt
    private int _waveIndex;     // đợt kế tiếp sẽ ra
    private int _spawnCursor;   // chỉ số xoay vòng điểm spawn
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
        var manual = new List<EnemyEntity>();
        foreach (var c in GetChildren())
            if (c is EnemyEntity e) manual.Add(e);   // quái đặt-tay (nếu có) tính vào tổng, có sẵn từ đầu

        // Điểm spawn đặt-tay = node trong group "enemy_spawn" (vd EnemySpawn.tscn) — ẩn marker khi chơi.
        _spawnPoints.Clear();
        foreach (var n in GetTree().GetNodesInGroup("enemy_spawn"))
            if (n is Node3D p) { _spawnPoints.Add(p); p.Visible = false; }

        foreach (var e in manual) Register(e);
        _alive = manual.Count;

        int  target   = _rng.RandiRange(MinEnemies, MaxEnemies);
        bool canSpawn = EnemyScene != null && (_spawnPoints.Count > 0 || ScatterIfNoPoints);
        int  toSpawn  = canSpawn ? Mathf.Max(0, target - manual.Count) : 0;

        BuildWaves(toSpawn);
        _waveIndex   = 0;
        _spawnCursor = 0;
        if (canSpawn) SpawnNextWave();   // ra đợt đầu (UseWaves=false → BuildWaves gộp 1 đợt = ra hết)

        GD.Print($"[Dungeon] mục tiêu {target} quái / {_waveSizes.Count} đợt ({_spawnPoints.Count} điểm spawn), portalCoeff={PortalCoeff}, allow Q/G/R={AllowQuiz}/{AllowGrammar}/{AllowReading}");
    }

    /// <summary>Chia tổng số quái thành các đợt tăng dần (FirstWaveSize rồi +WaveIncrement). UseWaves=false → 1 đợt.</summary>
    private void BuildWaves(int total)
    {
        _waveSizes.Clear();
        if (total <= 0) return;
        if (!UseWaves) { _waveSizes.Add(total); return; }

        int remaining = total;
        int size = Mathf.Max(1, FirstWaveSize);
        while (remaining > 0)
        {
            int s = Mathf.Min(size, remaining);
            _waveSizes.Add(s);
            remaining -= s;
            size += Mathf.Max(0, WaveIncrement);
        }
    }

    /// <summary>Ra đợt kế (nếu còn): spawn + Register + tăng _alive.</summary>
    private void SpawnNextWave()
    {
        if (_waveIndex >= _waveSizes.Count || EnemyScene == null) return;
        int n = _waveSizes[_waveIndex++];
        for (int k = 0; k < n; k++)
        {
            var e = SpawnEnemy(SpawnPos(_spawnCursor++));
            Register(e);
            _alive++;
        }
        GD.Print($"[Dungeon] Đợt {_waveIndex}/{_waveSizes.Count}: +{n} quái — còn sống {_alive}");
    }

    /// <summary>Vị trí spawn thứ i: xoay vòng các điểm "enemy_spawn" (lệch nhẹ); không có điểm → rải quanh người chơi.</summary>
    private Vector3 SpawnPos(int i)
    {
        if (_spawnPoints.Count > 0)
        {
            var p = _spawnPoints[i % _spawnPoints.Count];
            return p.GlobalPosition + new Vector3(_rng.RandfRange(-0.6f, 0.6f), 1f, _rng.RandfRange(-0.6f, 0.6f));
        }
        Vector3 c = _player?.GlobalPosition ?? GlobalPosition;
        float a = _rng.RandfRange(0f, Mathf.Tau);
        float r = _rng.RandfRange(SpawnRadius * 0.4f, SpawnRadius);
        return c + new Vector3(Mathf.Cos(a) * r, 1f, Mathf.Sin(a) * r);
    }

    private EnemyEntity SpawnEnemy(Vector3 pos)
    {
        var e = EnemyScene.Instantiate<EnemyEntity>();
        AddChild(e);
        e.GlobalPosition = pos;
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
        if (target == null)
        {
            // Không có từ tới hạn/mới → lấy ngẫu nhiên 1 từ trong bài để thử thách VẪN hiện (không "chết trơn").
            System.Collections.Generic.IReadOnlyList<VocabularyEntry> poolV = db.GetByLesson(lt.CurrentLesson);
            if (poolV.Count == 0) poolV = db.VocabN5;
            if (poolV.Count > 0) target = poolV[(int)(_rng.Randi() % (uint)poolV.Count)];
        }
        if (target == null) { enemy.ResolveChallenge(true); return; }

        var prog = lt.Get(ItemKind.Vocab, target.Id);
        bool showMeaning = prog == null || prog.Seen < 3;

        var pool = db.GetByLesson(lt.CurrentLesson);
        if (pool.Count < 4) pool = new List<VocabularyEntry>(db.VocabN5);

        ChallengeUi.Instance.RunQuiz(target, pool, showMeaning, QuizTimeout, (correct, ms) =>
        {
            lt.Record(ItemKind.Vocab, target.Id, correct, ms);
            if (correct) QuestManager.Instance?.Report("learn", "vocab");
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
        if (g == null)
        {
            System.Collections.Generic.IReadOnlyList<GrammarPoint> poolG = db.GetGrammarByLesson(lt.CurrentLesson);
            if (poolG.Count == 0) poolG = db.Grammar;
            if (poolG.Count > 0) g = poolG[(int)(_rng.Randi() % (uint)poolG.Count)];
        }
        if (g == null) { enemy.ResolveChallenge(true); return; }

        int stage = lt.Get(ItemKind.Grammar, g.Id)?.Seen ?? 0;
        var pool  = db.GetGrammarByLesson(lt.CurrentLesson);

        ChallengeUi.Instance.RunGrammar(g, stage, pool, QuizTimeout, (correct, ms) =>
        {
            lt.Record(ItemKind.Grammar, g.Id, correct, ms);
            if (correct) QuestManager.Instance?.Report("learn", "grammar");
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
        if (r == null)
        {
            System.Collections.Generic.IReadOnlyList<ReadingPassage> poolR = db.GetReadingsByLesson(lt.CurrentLesson);
            if (poolR.Count == 0) poolR = db.Readings;
            if (poolR.Count > 0) r = poolR[(int)(_rng.Randi() % (uint)poolR.Count)];
        }
        if (r == null) { enemy.ResolveChallenge(true); return; }

        int seen = lt.Get(ItemKind.Reading, r.Id)?.Seen ?? 0;

        ChallengeUi.Instance.RunReading(r, QuizTimeout, (correct, ms) =>
        {
            lt.Record(ItemKind.Reading, r.Id, correct, ms);
            if (correct)
            {
                _bonusGold += ReadingBonusGold;          // bonus thưởng
                ApplyReadingDamageBonus();               // bonus damage cả lượt ải
                QuestManager.Instance?.Report("learn", "reading");
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
        var e = SpawnEnemy(SpawnPos(_spawnCursor++));   // tiếp viện cũng dùng điểm spawn (xoay vòng)
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
        QuestManager.Instance?.Report("kill", "goblin");
        _alive--;

        if (_waveIndex < _waveSizes.Count)          // còn đợt chưa ra
        {
            if (_alive <= NextWaveAtAlive) SpawnNextWave();
        }
        else if (_alive <= 0)                       // hết đợt + sạch quái → thắng
        {
            Win();
        }
    }

    /// <summary>Hạ 1 quái → EXP + Vàng + 1–2 Tai Goblin vào túi.
    /// EXP challenge enemy cộng ở đây; Challenge=None đã cộng bởi AttackZone khi hp về 0.</summary>
    private void GrantKillLoot(EnemyEntity enemy)
    {
        // EXP: quái Challenge=None đã được AttackZone cộng khi hp về 0 → chỉ cộng cho quái có challenge (tránh trùng).
        int exp = enemy.Challenge != GameKind.None ? enemy.ExpReward : 0;

        int gold = Mathf.RoundToInt(RewardCalculator.BaseGoldPerEnemy
                   * RewardCalculator.KindMult(enemy.Challenge) * PortalCoeff);

        // Gộp exp + gold vào MỘT request /api/player/reward (trước đây bắn 2 request mỗi quái).
        _ = _player?.AwardAsync(exp, gold, _player.Data?.Level ?? -1);
        _goldEarned += gold;

        int ears = _rng.RandiRange(LootDropMin, LootDropMax);
        if (ears > 0) { _ = Inventory.Instance?.GrantAsync(LootItemId, ears); _earsDropped += ears; }
    }

    private void Win()
    {
        if (_resolved) return;
        _resolved = true;
        // Vàng/EXP/Tai đã cấp NGAY mỗi lần hạ quái (GrantKillLoot). Win chỉ cộng bonus đọc + tổng kết.
        if (_bonusGold > 0) { _ = _player?.AwardAsync(0, _bonusGold); _goldEarned += _bonusGold; }
        GD.Print($"[Dungeon] WIN — tổng +{_goldEarned} Vàng, +{_earsDropped} Tai Goblin ({_defeated.Count} quái).");
        
        // Hiển thị màn hình phần thưởng, nó sẽ gọi Leave() khi người chơi nhấn nút
        DungeonRewardUi.ShowReward(this, _goldEarned, _earsDropped, _defeated.Count);
    }

    private void OnPlayerDied()
    {
        if (_resolved) return;
        _resolved = true;
        _player?.RestoreFull();
        // Gục → về World hồi sinh tại PlayerSpawn mặc định (KHÔNG đặt tại cổng).
        SceneTransition.ForceDefaultArrival = true;
        GD.Print("[Dungeon] Người chơi gục — về World, hồi sinh tại spawn.");
        ShowBanner("💀 Bạn đã gục... quay về làng.");
        Leave();
    }

    public void Leave()
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
