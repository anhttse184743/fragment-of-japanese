using Godot;

namespace FragmentOfJapanese.World;

/// <summary>
/// Node3D tổng hợp terrain hoàn chỉnh:
///   - ArrayMesh visual + texture blending shader (4 biome: grass/dirt/rock/sand)
///   - HeightMapShape3D collision (StaticBody3D)
///   - WalkableGrid (game logic grid)
///   - NavigationRegion3D (NPC pathfinding)
///   - PropSpawner (cây, cỏ, hoa, nấm, đá, boulder)
///   - ZoneMap (5 vùng biome tròn: village + 4 corner + forest)
///
/// [Tool] enabled — script chạy trong editor, mesh hiện trong viewport.
/// Smart _Ready(): nếu đã có children (= đã bake) → skip Build().
/// </summary>
[Tool]
public partial class TerrainNode : Node3D
{
	[Export] public string ZoneType = "Unified";
	[Export] public long   Seed     = 0;
	[Export] public float  TexScale = 0.5f;

	private const string ShaderPath   = "res://assets/shaders/terrain_lowpoly.gdshader";
	private const string GrassTexPath = "res://assets/textures/terrain/grass.png";
	private const string DirtTexPath  = "res://assets/textures/terrain/dirt.png";
	private const string RockTexPath  = "res://assets/textures/terrain/stone.png";
	private const string SandTexPath  = "res://assets/textures/terrain/sand.png";

	public WalkableGrid Grid    { get; private set; }
	public ZoneMap      Zones   { get; private set; }
	public float[,]     Hmap    { get; private set; }
	public TerrainConfig Cfg    { get; private set; }

	public override void _Ready()
	{
		// Editor + đã bake → map tĩnh, mesh hiện qua node con. Không cần chạy logic.
		if (Engine.IsEditorHint() && GetChildCount() > 0) return;

		try
		{
			Cfg   = TerrainConfig.FromZoneType(ZoneType, Seed);
			Zones = new ZoneMap(Cfg);
			Hmap  = TerrainGenerator.Generate(Cfg, Zones);
			Grid  = new WalkableGrid(Hmap, Cfg);

			// Runtime + đã bake → chỉ giữ Grid/Zones cho game logic (NPC, AI, spawn)
			// KHÔNG rebuild visual (mesh đã lưu trong .tscn)
			if (GetChildCount() > 0) return;

			// Fresh build: visual + collision + nav (props handled bởi WorldMapGenerator)
			BuildVisualMesh(Hmap, Cfg);
			BuildCollision(Hmap, Cfg);
			BuildNavigation();

			GD.Print($"[TerrainNode] Built zone={ZoneType} seed={Seed} grid={Cfg.GridWidth}x{Cfg.GridHeight}");
		}
		catch (System.Exception e)
		{
			GD.PrintErr($"[TerrainNode] _Ready failed: {e.Message}\n{e.StackTrace}");
		}
	}

	// -------------------------------------------------------------------------

	private void BuildVisualMesh(float[,] hmap, TerrainConfig cfg)
	{
		var mesh = TerrainMeshBuilder.Build(hmap, cfg, Zones);

		var mat = new ShaderMaterial();
		mat.Shader = GD.Load<Shader>(ShaderPath);
		mat.SetShaderParameter("tex_grass", GD.Load<Texture2D>(GrassTexPath));
		mat.SetShaderParameter("tex_dirt",  GD.Load<Texture2D>(DirtTexPath));
		mat.SetShaderParameter("tex_rock",  GD.Load<Texture2D>(RockTexPath));
		mat.SetShaderParameter("tex_sand",  GD.Load<Texture2D>(SandTexPath));
		mat.SetShaderParameter("tex_scale", TexScale);

		var mi = new MeshInstance3D { Mesh = mesh, Name = "TerrainMesh" };
		mi.MaterialOverride = mat;
		AddChild(mi);
	}

	private void BuildCollision(float[,] hmap, TerrainConfig cfg)
	{
		var shape = TerrainMeshBuilder.BuildCollision(hmap, cfg);

		var body     = new StaticBody3D { Name = "TerrainBody" };
		var collider = new CollisionShape3D { Shape = shape };

		float offsetX = cfg.GridWidth  * cfg.CellSize * 0.5f;
		float offsetZ = cfg.GridHeight * cfg.CellSize * 0.5f;
		body.Position = new Vector3(offsetX, 0f, offsetZ);
		body.Scale    = new Vector3(cfg.CellSize, 1f, cfg.CellSize);

		body.AddChild(collider);
		AddChild(body);
	}

	private void BuildNavigation()
	{
		var navMesh = new NavigationMesh();
		navMesh.AgentHeight   = 1.8f;
		navMesh.AgentRadius   = 0.4f;
		navMesh.AgentMaxSlope = 45f;

		var navRegion = new NavigationRegion3D
		{
			NavigationMesh = navMesh,
			Name           = "NavRegion"
		};
		AddChild(navRegion);
		navRegion.BakeNavigationMesh(false);
	}
}
