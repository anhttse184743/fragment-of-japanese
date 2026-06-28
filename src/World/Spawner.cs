using System.Collections.Generic;
using Godot;

namespace FragmentOfJapanese.World;

/// <summary>
/// Điểm spawn kéo-thả. Lúc CHẠY game tạo <see cref="Scene"/> tại vị trí node này.
/// Editor hiện marker (quả cầu phát sáng) để đặt vị trí; marker ẩn khi chơi.
///
/// Hỗ trợ SỐ LƯỢNG + THỜI GIAN:
///   - <see cref="Count"/>: số tạo mỗi đợt.
///   - <see cref="MaxAlive"/>: tối đa còn sống cùng lúc (0 = không giới hạn).
///   - <see cref="Interval"/>: giây giữa các đợt. 0 = chỉ spawn 1 lần. >0 = lặp lại,
///     tự spawn BÙ khi quái chết (tới khi đủ MaxAlive).
/// Mọi thông số chỉnh ở Inspector.
/// </summary>
public partial class Spawner : Node3D
{
    [Export] public PackedScene Scene;             // prefab spawn (Player.tscn / Goblin.tscn)
    [Export] public int   Count         = 1;       // số tạo mỗi đợt
    [Export] public int   MaxAlive      = 3;       // tối đa còn sống (0 = vô hạn)
    [Export] public float Interval      = 0f;      // giây giữa các đợt (0 = spawn 1 lần)
    [Export] public float ScatterRadius = 0f;      // bán kính rải ngẫu nhiên (m)
    [Export] public bool  SpawnOnReady  = true;    // spawn đợt đầu ngay khi vào game

    private readonly List<Node3D> _alive = new();
    private readonly RandomNumberGenerator _rng = new();
    private float _timer;

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) { SetProcess(false); return; }
        _rng.Randomize();

        foreach (var c in GetChildren())            // ẩn marker khi chơi
            if (c is Node3D n) n.Visible = false;

        // Defer đợt spawn ĐẦU: trong _Ready, cha (World) còn "bận dựng con" → add_child() bị chặn.
        // Hoãn tới idle (cha dựng xong) thì AddChild + đặt vị trí mới chạy được.
        if (SpawnOnReady) CallDeferred(nameof(Wave));
        if (Interval <= 0f) SetProcess(false);      // one-shot → khỏi tick
    }

    public override void _Process(double delta)
    {
        _timer += (float)delta;
        if (_timer >= Interval)
        {
            _timer = 0f;
            Wave();
        }
    }

    /// <summary>Spawn một đợt: tối đa <see cref="Count"/>, không vượt <see cref="MaxAlive"/>.</summary>
    public void Wave()
    {
        if (Scene == null) { GD.PrintErr($"[Spawner] '{Name}': chưa gán Scene."); return; }

        _alive.RemoveAll(n => !IsInstanceValid(n));        // bỏ con đã chết

        int budget = Count;
        if (MaxAlive > 0) budget = Mathf.Min(budget, MaxAlive - _alive.Count);
        if (budget <= 0) return;

        Node parent = GetParent() ?? this;
        for (int i = 0; i < budget; i++)
        {
            var inst = Scene.Instantiate<Node3D>();
            var pos = GlobalPosition;
            if (ScatterRadius > 0f)
            {
                float a = _rng.RandfRange(0f, Mathf.Tau);
                float r = _rng.RandfRange(ScatterRadius * 0.45f, ScatterRadius);   // đẩy ra vành ngoài, đỡ chụm
                pos += new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            }
            parent.AddChild(inst);       // vào cây trước
            inst.GlobalPosition = pos;   // rồi mới đặt vị trí toàn cục (cần is_inside_tree)
            _alive.Add(inst);
        }
    }
}
