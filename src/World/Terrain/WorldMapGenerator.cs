using Godot;

namespace FragmentOfJapanese.World;

/// <summary>
/// CÔNG CỤ sinh map (editor tool) — KHÔNG gắn vào World.tscn đã bake.
///
/// World.tscn hiện tại là map TĨNH (các node đã bake, không cần script này).
/// Script này chỉ dùng khi muốn TẠO MAP MỚI:
///   1. Tạo scene mới (Node3D) → gắn script này
///   2. Mở scene → tự generate terrain + props hiện trong tree
///   3. Đổi Seed → tick "Regenerate Now" để ra map khác
///   4. Ưng → Ctrl+S lưu thành .tscn → dùng làm World mới
///
/// Map sinh ra KHÔNG có player — player do game flow xử lý riêng.
/// </summary>
[Tool]
public partial class WorldMapGenerator : Node3D
{
	[Export] public long Seed = 0;

	/// <summary>
	/// Editor button — tick để xóa map cũ + build mới với Seed hiện tại.
	/// Getter luôn trả false để checkbox tự reset.
	/// </summary>
	[Export]
	public bool RegenerateNow
	{
		get => false;
		set { if (value) CallDeferred(nameof(Regenerate)); }
	}

	public override void _Ready()
	{
		// Đã có nodes (đã bake hoặc đã generate) → giữ nguyên
		if (GetChildCount() > 0) return;

		BuildAll();
	}

	private void BuildAll()
	{
		try
		{
			BuildTerrain();
			SetupLight();

			if (Engine.IsEditorHint())
				SetChildrenOwnerRecursive(this);

			GD.Print($"[WorldMapGenerator] Built — Seed={Seed} (editor={Engine.IsEditorHint()})");
		}
		catch (System.Exception e)
		{
			GD.PrintErr($"[WorldMapGenerator] Build failed: {e.Message}\n{e.StackTrace}");
		}
	}

	private void Regenerate()
	{
		foreach (Node child in GetChildren())
		{
			RemoveChild(child);
			child.QueueFree();
		}
		BuildAll();
	}

	private void BuildTerrain()
	{
		var cfg     = TerrainConfig.Unified(Seed);
		float halfW = cfg.GridWidth  * cfg.CellSize * 0.5f;
		float halfH = cfg.GridHeight * cfg.CellSize * 0.5f;

		var terrain = new TerrainNode
		{
			ZoneType = "Unified",
			Seed     = Seed,
			Name     = "WorldTerrain",
			Position = new Vector3(-halfW, 0, -halfH),
		};
		AddChild(terrain);

		// Props ở root level (không thuộc TerrainNode) — dễ edit
		var propsRoot = new Node3D
		{
			Name     = "Props",
			Position = new Vector3(-halfW, 0, -halfH),
		};
		AddChild(propsRoot);

		PropSpawner.Spawn(propsRoot, terrain.Hmap, terrain.Grid, cfg, terrain.Zones);
	}

	private void SetupLight()
	{
		var skyMat = new ProceduralSkyMaterial
		{
			SkyTopColor        = new Color(0.30f, 0.55f, 0.90f),
			SkyHorizonColor    = new Color(0.70f, 0.82f, 0.95f),
			GroundBottomColor  = new Color(0.30f, 0.25f, 0.18f),
			GroundHorizonColor = new Color(0.50f, 0.42f, 0.32f),
			SunAngleMax        = 30f,
		};

		var env = new Godot.Environment();
		env.BackgroundMode     = Godot.Environment.BGMode.Sky;
		env.Sky                = new Sky { SkyMaterial = skyMat };
		env.AmbientLightSource = Godot.Environment.AmbientSource.Sky;
		env.AmbientLightEnergy = 1.0f;
		env.FogEnabled         = true;
		env.FogLightColor      = new Color(0.75f, 0.85f, 0.95f);
		env.FogDensity         = 0.001f;

		AddChild(new WorldEnvironment { Name = "Env", Environment = env });

		var sun = new DirectionalLight3D
		{
			Name          = "Sun",
			LightEnergy   = 1.2f,
			LightColor    = new Color(1.0f, 0.97f, 0.90f),
			ShadowEnabled = true,
			Rotation      = new Vector3(Mathf.DegToRad(-55f), Mathf.DegToRad(35f), 0f),
		};
		AddChild(sun);
	}

	// -------------------------------------------------------------------------
	// Owner setting — chìa khóa để node được serialize vào .tscn
	// -------------------------------------------------------------------------

	private void SetChildrenOwnerRecursive(Node sceneRoot)
	{
		foreach (Node child in GetChildren())
			SetOwnerRecursive(child, sceneRoot);
	}

	private static void SetOwnerRecursive(Node node, Node owner)
	{
		if (node != owner)
			node.Owner = owner;
		foreach (Node child in node.GetChildren())
			SetOwnerRecursive(child, owner);
	}
}
