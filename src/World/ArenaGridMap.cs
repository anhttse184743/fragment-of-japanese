using Godot;

namespace FragmentOfJapanese.World;

/// <summary>
/// Arena nhỏ dựng bằng GridMap (lưới ô 3D) — map phụ để đánh quái.
///
/// [Tool]: tự sinh MeshLibrary gồm các ô NỀN có texture (cỏ/đất/đá/cát/gỗ) và một
/// ô "TƯỜNG SƯƠNG MÙ" làm biên. Tường sương mù: có collision (chặn người chơi ra
/// ngoài) + trong mờ che tầm nhìn, nhưng KHÔNG ghi depth nên không che nhân vật
/// trước camera (camera cũng không va chạm vật lý nên không bị đẩy).
///
/// Palette được script gán sẵn → mở scene trong editor là VẼ TAY được ngay: chọn
/// node GridMap, chọn ô rồi tô / mở rộng / đổi nền tuỳ ý. Ô vẽ tay được lưu trong
/// .tscn và KHÔNG bị xoá khi mở lại — script chỉ vẽ phòng mẫu khi map còn trống.
///
/// Muốn dựng lại phòng mẫu theo kích thước mới: tick "ResetToSample" trong Inspector.
/// </summary>
[Tool]
public partial class ArenaGridMap : GridMap
{
	// ID các ô trong MeshLibrary — giữ CỐ ĐỊNH để ô đã vẽ tay không bị lệch loại.
	public const int Grass = 0;
	public const int Dirt  = 1;
	public const int Stone = 2;
	public const int Sand  = 3;
	public const int Wood  = 4;
	public const int Fog   = 5;

	private const float Cell = 2.0f;   // mét mỗi ô (khớp CellSize)

	[Export] public int   RoomWidth  { get; set; } = 11;    // kích thước phòng mẫu — số ô trục X
	[Export] public int   RoomDepth  { get; set; } = 11;    // số ô trục Z
	[Export] public float WallHeight { get; set; } = 3.0f;  // chiều cao tường sương mù (m)

	/// <summary>Tick trong Inspector để xoá sạch và vẽ lại phòng mẫu.</summary>
	[Export] public bool ResetToSample { get => false; set { if (value) BuildSampleRoom(); } }

	public override void _Ready()
	{
		// Ô đối xứng quanh gốc, mặt trên sàn nằm tại y = 0.
		CellSize    = new Vector3(Cell, Cell, Cell);
		CellCenterX = false;
		CellCenterY = false;
		CellCenterZ = false;
		MeshLibrary = BuildLibrary();

		// Chỉ vẽ phòng mẫu khi map còn trống → giữ nguyên ô bạn đã vẽ tay.
		if (GetUsedCells().Count == 0)
			BuildSampleRoom();
	}

	// ── Vẽ 1 phòng mẫu: nền cỏ, viền tường sương mù ──
	private void BuildSampleRoom()
	{
		MeshLibrary ??= BuildLibrary();
		Clear();

		int hw = RoomWidth / 2;
		int hd = RoomDepth / 2;
		for (int x = -hw; x <= hw; x++)
		for (int z = -hd; z <= hd; z++)
		{
			bool border = x == -hw || x == hw || z == -hd || z == hd;
			SetCellItem(new Vector3I(x, 0, z), border ? Fog : Grass);
		}
	}

	// ── Tạo bộ ô (palette) ──
	private MeshLibrary BuildLibrary()
	{
		var lib = new MeshLibrary();

		AddFloor(lib, Grass, "grass", "res://assets/textures/terrain/grass.png", default);
		AddFloor(lib, Dirt,  "dirt",  "res://assets/textures/terrain/dirt.png",  default);
		AddFloor(lib, Stone, "stone", "res://assets/textures/terrain/stone.png", default);
		AddFloor(lib, Sand,  "sand",  "res://assets/textures/terrain/sand.png",  default);
		AddFloor(lib, Wood,  "wood",  null, new Color(0.50f, 0.34f, 0.18f));   // gỗ: màu nâu tạm
		AddFog  (lib, Fog,   "fog");

		return lib;
	}

	// Ô nền: tấm mỏng, mặt trên ngang y = 0
	private static void AddFloor(MeshLibrary lib, int id, string name, string texPath, Color color)
	{
		var mat = new StandardMaterial3D { Roughness = 1.0f };
		if (texPath != null) mat.AlbedoTexture = GD.Load<Texture2D>(texPath);
		else                 mat.AlbedoColor   = color;

		AddTile(lib, id, name,
			size:   new Vector3(Cell, 0.4f, Cell),
			offset: new Vector3(0f, -0.2f, 0f),
			mat:    mat);
	}

	// Ô tường sương mù: trong mờ + KHÔNG ghi depth → che ngoài nhưng không che nhân vật
	private void AddFog(MeshLibrary lib, int id, string name)
	{
		var mat = new StandardMaterial3D
		{
			Transparency  = BaseMaterial3D.TransparencyEnum.Alpha,
			AlbedoColor   = new Color(0.82f, 0.86f, 0.92f, 0.80f),
			ShadingMode   = BaseMaterial3D.ShadingModeEnum.Unshaded,
			CullMode      = BaseMaterial3D.CullModeEnum.Disabled,        // 2 mặt
			DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,   // không che nhân vật/camera
		};

		AddTile(lib, id, name,
			size:   new Vector3(Cell, WallHeight, Cell),
			offset: new Vector3(0f, WallHeight * 0.5f, 0f),   // đứng từ y = 0 lên
			mat:    mat);
	}

	private static void AddTile(MeshLibrary lib, int id, string name, Vector3 size, Vector3 offset, Material mat)
	{
		var mesh = new BoxMesh { Size = size, Material = mat };

		lib.CreateItem(id);
		lib.SetItemName(id, name);
		lib.SetItemMesh(id, mesh);
		lib.SetItemMeshTransform(id, new Transform3D(Basis.Identity, offset));

		// Collision khớp hình hộp: đứng được trên sàn & không xuyên / không ra ngoài tường.
		var shapes = new Godot.Collections.Array();
		shapes.Add(new BoxShape3D { Size = size });
		shapes.Add(new Transform3D(Basis.Identity, offset));
		lib.SetItemShapes(id, shapes);
	}
}
