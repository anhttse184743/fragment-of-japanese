using Godot;
using FragmentOfJapanese.Cosmetics;

namespace FragmentOfJapanese.Entities.Player;

/// <summary>
/// Gắn vào Player. Áp dụng skin nhân vật (đổi SpriteFrames + tông màu) và sinh các hiệu ứng
/// cosmetic theo skin đang mặc:
///   - Vệt chém (AttackSwing) khi bấm đánh
///   - Bụi chạy (RunDust) khi chạy nhanh trên mặt đất
///   - Tia trúng đòn (HitSpark) khi AttackZone đánh trúng quái
///   - Hào quang (LevelUpAura) khi lên cấp
///   - Vệt chân (Footstep) khi di chuyển
/// Hiệu ứng dựng bằng texture procedural (<see cref="FxTextures"/>) nên không cần art ngoài.
/// </summary>
public partial class PlayerCosmetics : Node
{
    [Export] private Player            _player;
    [Export] private AnimatedSprite3D  _visual;
    [Export] private PlayerController  _controller;
    [Export] private AttackZone        _attackZone;

    [Export] public float DustInterval = 0.10f;   // giây giữa 2 hạt bụi khi chạy
    [Export] public float StepInterval = 0.34f;   // giây giữa 2 vệt chân

    private Node3D _world;
    private float  _dustTimer;
    private float  _stepTimer;
    private readonly RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        _rng.Randomize();
        _world = _player?.GetParent() as Node3D ?? GetTree().CurrentScene as Node3D;

        if (SkinManager.Instance != null) SkinManager.Instance.SkinChanged += OnSkinChanged;
        if (_controller != null) _controller.AttackPressed += OnAttack;
        if (_attackZone != null) _attackZone.Hit          += OnHit;
        if (_player     != null) _player.LeveledUp         += OnLevelUp;

        ApplyPlayerSkin();
    }

    public override void _ExitTree()
    {
        if (SkinManager.Instance != null) SkinManager.Instance.SkinChanged -= OnSkinChanged;
        if (_controller != null) _controller.AttackPressed -= OnAttack;
        if (_attackZone != null) _attackZone.Hit          -= OnHit;
        if (_player     != null) _player.LeveledUp         -= OnLevelUp;
    }

    private void OnSkinChanged(SkinCategory cat, SkinDef def)
    {
        if (cat == SkinCategory.Player) ApplyPlayerSkin();
    }

    // ───────── Skin nhân vật ─────────

    private void ApplyPlayerSkin()
    {
        if (_visual == null || SkinManager.Instance == null) return;
        var def = SkinManager.Instance.GetEquipped(SkinCategory.Player);
        if (def == null) return;

        if (!string.IsNullOrEmpty(def.Frames) && ResourceLoader.Exists(def.Frames))
        {
            var sf = GD.Load<SpriteFrames>(def.Frames);
            if (sf != null && sf != _visual.SpriteFrames)
            {
                string cur = _visual.Animation;
                _visual.SpriteFrames = sf;
                if (sf.HasAnimation(cur)) _visual.Play(cur);
            }
        }
        _visual.Modulate = def.ColorValue;   // tông màu (mặc định #ffffff = không đổi)
    }

    // ───────── Hiệu ứng định kỳ (bụi / vệt chân) ─────────

    public override void _PhysicsProcess(double delta)
    {
        if (_player == null) return;

        var h        = new Vector2(_player.Velocity.X, _player.Velocity.Z);
        bool moving  = h.Length() > 0.2f && _player.IsOnFloor();
        bool running = moving && _controller != null && _controller.IsRunning;

        _dustTimer -= (float)delta;
        if (running && _dustTimer <= 0f) { SpawnDust(); _dustTimer = DustInterval; }

        _stepTimer -= (float)delta;
        if (moving && _stepTimer <= 0f) { SpawnFootstep(); _stepTimer = StepInterval; }
    }

    // ───────── Các hiệu ứng ─────────

    private void OnAttack()
    {
        if (_attackZone == null) return;
        var tex = FxTextures.Crescent(ColorOf(SkinCategory.AttackSwing));
        var s   = MakeSprite(tex, billboard: true, pixelSize: 0.012f);
        _attackZone.AddChild(s);
        s.Position = new Vector3(0f, _attackZone.Height * 0.5f, _attackZone.ForwardOffset + _attackZone.Range * 0.5f);
        s.Scale    = Vector3.One * 0.4f;
        PopFade(s, 1.3f, 0.18f);
    }

    private void OnHit(Vector3 pos)
    {
        var tex = FxTextures.SoftCircle(ColorOf(SkinCategory.HitSpark));
        var s   = MakeSprite(tex, billboard: true, pixelSize: 0.02f);
        AddToWorld(s, pos);
        s.Scale = Vector3.One * 0.2f;
        PopFade(s, 1.0f, 0.16f);
    }

    private void OnLevelUp()
    {
        Color col = ColorOf(SkinCategory.LevelUpAura);

        // vòng sáng lan dưới chân
        var ring = MakeSprite(FxTextures.Ring(col), billboard: false, pixelSize: 0.02f);
        ring.RotationDegrees = new Vector3(-90f, 0f, 0f);
        AddToWorld(ring, _player.GlobalPosition + Vector3.Up * 0.05f);
        ring.Scale = Vector3.One * 0.5f;
        var tw = ring.CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(ring, "scale", Vector3.One * 2.6f, 0.8);
        tw.TweenProperty(ring, "modulate:a", 0f, 0.8);
        tw.Chain().TweenCallback(Callable.From(ring.QueueFree));

        // vài đốm sáng bay lên quanh người
        for (int i = 0; i < 6; i++)
        {
            var g = MakeSprite(FxTextures.SoftCircle(col), billboard: true, pixelSize: 0.012f);
            var off = new Vector3(_rng.RandfRange(-0.4f, 0.4f), _rng.RandfRange(0f, 0.3f), _rng.RandfRange(-0.4f, 0.4f));
            AddToWorld(g, _player.GlobalPosition + off);
            g.Scale = Vector3.One * 0.5f;
            var gt = g.CreateTween();
            gt.SetParallel(true);
            gt.TweenProperty(g, "global_position:y", g.GlobalPosition.Y + 1.4f, 0.7);
            gt.TweenProperty(g, "modulate:a", 0f, 0.7);
            gt.Chain().TweenCallback(Callable.From(g.QueueFree));
        }
    }

    private void SpawnDust()
    {
        var s = MakeSprite(FxTextures.SoftCircle(ColorOf(SkinCategory.RunDust)), billboard: true, pixelSize: 0.01f);
        var jitter = new Vector3(_rng.RandfRange(-0.15f, 0.15f), 0f, _rng.RandfRange(-0.15f, 0.15f));
        AddToWorld(s, _player.GlobalPosition + Vector3.Up * 0.08f + jitter);
        s.Scale    = Vector3.One * 0.35f;
        s.Modulate = new Color(1f, 1f, 1f, 0.7f);
        var tw = s.CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(s, "global_position:y", s.GlobalPosition.Y + 0.45f, 0.45);
        tw.TweenProperty(s, "scale", Vector3.One * 0.8f, 0.45);
        tw.TweenProperty(s, "modulate:a", 0f, 0.45);
        tw.Chain().TweenCallback(Callable.From(s.QueueFree));
    }

    private void SpawnFootstep()
    {
        var s = MakeSprite(FxTextures.SoftCircle(ColorOf(SkinCategory.Footstep)), billboard: false, pixelSize: 0.01f);
        s.RotationDegrees = new Vector3(-90f, 0f, 0f);   // nằm phẳng trên đất
        AddToWorld(s, _player.GlobalPosition + Vector3.Up * 0.02f);
        s.Scale    = new Vector3(0.5f, 0.32f, 1f);
        s.Modulate = new Color(1f, 1f, 1f, 0.6f);
        var tw = s.CreateTween();
        tw.TweenProperty(s, "modulate:a", 0f, 1.0).SetDelay(0.2);
        tw.TweenCallback(Callable.From(s.QueueFree));
    }

    // ───────── Helper ─────────

    private static Color ColorOf(SkinCategory cat) =>
        SkinManager.Instance?.GetEquipped(cat)?.ColorValue ?? Colors.White;

    private static Sprite3D MakeSprite(Texture2D tex, bool billboard, float pixelSize)
    {
        return new Sprite3D
        {
            Texture       = tex,
            PixelSize     = pixelSize,
            Billboard     = billboard ? BaseMaterial3D.BillboardModeEnum.Enabled : BaseMaterial3D.BillboardModeEnum.Disabled,
            Shaded        = false,
            NoDepthTest   = true,
            TextureFilter  = BaseMaterial3D.TextureFilterEnum.Linear,
        };
    }

    private void AddToWorld(Node3D node, Vector3 globalPos)
    {
        Node parent = _world ?? (Node)this;
        parent.AddChild(node);
        node.GlobalPosition = globalPos;
    }

    /// <summary>Phình to + mờ dần rồi tự huỷ (cho vệt chém / tia trúng).</summary>
    private static void PopFade(Sprite3D s, float toScale, float dur)
    {
        var tw = s.CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(s, "scale", Vector3.One * toScale, dur);
        tw.TweenProperty(s, "modulate:a", 0f, dur);
        tw.Chain().TweenCallback(Callable.From(s.QueueFree));
    }
}
