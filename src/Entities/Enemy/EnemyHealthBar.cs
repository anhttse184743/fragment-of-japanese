using Godot;

namespace FragmentOfJapanese.Entities.Enemy;

/// <summary>
/// Thanh máu nổi trên đầu quái. Một Sprite3D billboard, texture vẽ procedural (viền + nền + thanh đổ đầy),
/// vẽ lại mỗi khi máu đổi (đỏ ít máu → xanh nhiều máu). Gán <see cref="_enemy"/> = node Enemy cha.
/// </summary>
public partial class EnemyHealthBar : Node3D
{
    [Export] private Enemy _enemy;

    [Export] public Vector3 Offset { get; set; } = new(0f, 1.9f, 0f);   // vị trí so với gốc quái
    [Export] public float   Width  { get; set; } = 0.9f;                // bề ngang ngoài đời (m)

    private const int TexW = 48, TexH = 7;

    private Sprite3D _sprite;

    public override void _Ready()
    {
        Position = Offset;

        _sprite = new Sprite3D
        {
            PixelSize     = Width / TexW,
            Billboard     = BaseMaterial3D.BillboardModeEnum.Enabled,
            Shaded        = false,
            NoDepthTest   = true,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
        };
        AddChild(_sprite);

        if (_enemy != null) { _enemy.HpChanged += OnHp; OnHp(_enemy.Hp, _enemy.MaxHp); }
        else Redraw(1f);
    }

    public override void _ExitTree()
    {
        if (_enemy != null) _enemy.HpChanged -= OnHp;
    }

    private void OnHp(int cur, int max) =>
        Redraw(max > 0 ? Mathf.Clamp((float)cur / max, 0f, 1f) : 0f);

    private void Redraw(float ratio)
    {
        var img = Image.CreateEmpty(TexW, TexH, false, Image.Format.Rgba8);

        var border = new Color(0f, 0f, 0f, 0.92f);
        var bg     = new Color(0.10f, 0.10f, 0.12f, 0.88f);
        var fill   = new Color(0.85f, 0.24f, 0.20f).Lerp(new Color(0.40f, 0.85f, 0.32f), ratio);  // đỏ→xanh

        int fillCols = Mathf.RoundToInt((TexW - 2) * ratio);
        for (int y = 0; y < TexH; y++)
            for (int x = 0; x < TexW; x++)
            {
                Color c;
                if (x == 0 || y == 0 || x == TexW - 1 || y == TexH - 1) c = border;
                else if (x - 1 < fillCols)                              c = fill;
                else                                                    c = bg;
                img.SetPixel(x, y, c);
            }

        _sprite.Texture = ImageTexture.CreateFromImage(img);
    }
}
