using Godot;

namespace FragmentOfJapanese.World;

/// <summary>
/// Sinh props (cây, cỏ, hoa, nấm, đá, boulder) thành các Node3D RIÊNG LẺ.
/// Mỗi prop là 1 node có thể click/edit trong scene tree.
///
/// Performance: Godot tự auto-batch các MeshInstance3D static dùng chung Mesh resource
/// (như GPU instancing) → runtime gần MultiMesh, editor có thể edit từng cái.
///
/// Kỹ thuật mesh:
///   Cây / Nấm  → Cross-plane: 2 quad đan chéo 90° (dấu +)
///   Cỏ         → 1 quad thẳng đứng (Vertical Billboard)
///   Hoa        → ArrayMesh 2 surface: stem (vertical) + head (horizontal)
///   Đá/Boulder → SphereMesh low-poly
///
/// Tổ chức scene tree:
///   Props (root container, child của World)
///   ├── Trees      → Tree0001, Tree0002, ... (mỗi cây có Visual + Body)
///   ├── Boulders   → Boulder0001, ...
///   ├── Rocks      → Rock0001, ...
///   ├── Mushrooms  → MushBrown0001, MushRed0001, ...
///   ├── Flowers    → Flower0001, ...
///   └── Grass      → Grass0001, ... (chỉ MeshInstance3D, không collision)
/// </summary>
public static class PropSpawner
{
	// Đường dẫn sprite (tên file thực tế trong assets/sprites/props/)
	private const string PathTree     = "res://assets/sprites/props/pine_tree.png";
	private const string PathMushBrown = "res://assets/sprites/props/brown_mushroom.png";
	private const string PathMushRed   = "res://assets/sprites/props/red_mushroom.png";
	private const string PathGrass    = "res://assets/sprites/props/short_grass.png";
	private const string PathFlower   = "res://assets/sprites/props/flower.png";
	private const string PathStem     = "res://assets/sprites/props/flowe_stem.png"; // (typo tên file gốc)

	public static void Spawn(Node3D parent, float[,] hmap, WalkableGrid grid, TerrainConfig cfg, ZoneMap zones = null)
	{
		var rng = new RandomNumberGenerator { Seed = (ulong)cfg.Seed };

		// ============= Pre-create SHARED mesh resources =============
		// Mỗi instance dùng CHUNG mesh → Godot auto-batch ở runtime
		// Kích cỡ tham chiếu player 1.75m:
		var treeMesh       = MakeCrossPlane(PathTree, 4.0f, 6.0f);            // cây thông 6m
		var mushBrownMesh  = MakeCrossPlane(PathMushBrown, 0.7f, 0.8f);
		var mushRedMesh    = MakeCrossPlane(PathMushRed, 0.7f, 0.8f);
		var grassMesh      = MakeVerticalPlane(PathGrass, 0.8f, 1.0f);        // cỏ 1m
		var flowerMesh     = MakeFlowerComposite(PathStem, PathFlower);       // 2-surface mesh
		var rockMesh       = MakeRockMesh();
		var boulderMesh    = MakeBoulderMesh();

		// ============= Container nodes (organization) =============
		var trees     = AddContainer(parent, "Trees");
		var boulders  = AddContainer(parent, "Boulders");
		var rocks     = AddContainer(parent, "Rocks");
		var mushrooms = AddContainer(parent, "Mushrooms");
		var flowers   = AddContainer(parent, "Flowers");
		var grassRoot = AddContainer(parent, "Grass");

		int treeCount = 0, mushBrownCount = 0, mushRedCount = 0;
		int grassCount = 0, flowerCount = 0, rockCount = 0, boulderCount = 0;

		// ============= Spawn loop =============
		for (int x = 0; x < grid.Width; x++)
		for (int z = 0; z < grid.Height; z++)
		{
			if (!grid.IsWalkable[x, z]) continue;

			float wy    = grid.HeightData[x, z];
			float cellX = x * cfg.CellSize;
			float cellZ = z * cfg.CellSize;

			BiomeZone zone = zones?.GetZone(cellX, cellZ) ?? BiomeZone.Forest;
			var rules = GetSpawnRules(zone);

			// ----- Cỏ — multi-attempt break grid pattern -----
			if (rules.GrassMult > 0f)
			{
				int   attempts = 4;
				float chance   = (cfg.GrassDensity * rules.GrassMult) / attempts;
				for (int i = 0; i < attempts; i++)
				{
					if (rng.Randf() >= chance) continue;
					var pos = new Vector3(
						cellX + rng.RandfRange(0f, 1f) * cfg.CellSize,
						wy,
						cellZ + rng.RandfRange(0f, 1f) * cfg.CellSize
					);
					float s = rng.RandfRange(0.6f, 1.2f);
					float r = rng.RandfRange(0f, Mathf.Pi * 2f);
					AddSimpleProp(grassRoot, $"Grass{++grassCount:D4}", grassMesh, pos, s, r);
				}
			}

			float wx = cellX + rng.RandfRange(0.1f, 0.9f) * cfg.CellSize;
			float wz = cellZ + rng.RandfRange(0.1f, 0.9f) * cfg.CellSize;
			var basePos = new Vector3(wx, wy, wz);

			// ----- Cây (có collision) -----
			if (rules.TreeMult > 0f && rng.Randf() < cfg.TreeDensity * rules.TreeMult)
			{
				float s = rng.RandfRange(0.85f, 1.5f);
				float r = rng.RandfRange(0f, Mathf.Pi * 2f);
				AddTreeNode(trees, $"Tree{++treeCount:D4}", treeMesh, basePos, s, r);
			}

			// ----- Nấm -----
			if (rules.MushroomMult > 0f && rng.Randf() < cfg.MushroomDensity * rules.MushroomMult)
			{
				float s = rng.RandfRange(0.5f, 1.0f);
				float r = rng.RandfRange(0f, Mathf.Pi * 2f);
				if (rng.Randf() < 0.5f)
					AddSimpleProp(mushrooms, $"MushBrown{++mushBrownCount:D4}", mushBrownMesh, basePos, s, r);
				else
					AddSimpleProp(mushrooms, $"MushRed{++mushRedCount:D4}", mushRedMesh, basePos, s, r);
			}

			// ----- Hoa (1 mesh 2 surface: stem + head) -----
			if (rules.FlowerMult > 0f && rng.Randf() < cfg.FlowerDensity * rules.FlowerMult)
			{
				float s = rng.RandfRange(0.5f, 1.0f);
				float r = rng.RandfRange(0f, Mathf.Pi * 2f);
				AddSimpleProp(flowers, $"Flower{++flowerCount:D4}", flowerMesh, basePos, s, r);
			}

			// ----- Đá nhỏ (có collision nhẹ) -----
			float rockChance = zone switch
			{
				BiomeZone.RockWaste => cfg.RockDensity * 3f * rules.RockMult,
				BiomeZone.Sand      => cfg.RockDensity * rules.RockMult,
				_                   => cfg.RockDensity * 0.4f * rules.RockMult,
			};
			if (rules.RockMult > 0f && rng.Randf() < rockChance)
			{
				float s = rng.RandfRange(0.5f, 1.5f);
				float r = rng.RandfRange(0f, Mathf.Pi * 2f);
				var sunkenPos = basePos + new Vector3(0, -s * 0.2f, 0);
				AddSimpleProp(rocks, $"Rock{++rockCount:D4}", rockMesh, sunkenPos, s, r);
			}

			// ----- Boulder khổng lồ (có collision) -----
			if (rules.BoulderMult > 0f && rng.Randf() < cfg.BoulderDensity * rules.BoulderMult)
			{
				float s = rng.RandfRange(4.0f, 7.0f);
				float r = rng.RandfRange(0f, Mathf.Pi * 2f);
				var pos = basePos + new Vector3(0, 0.3f * s - 0.15f, 0);
				AddBoulderNode(boulders, $"Boulder{++boulderCount:D4}", boulderMesh, pos, s, r);
			}
		}

		GD.Print($"[PropSpawner] Trees={treeCount} Mushrooms={mushBrownCount + mushRedCount} " +
				 $"Grass={grassCount} Flowers={flowerCount} Rocks={rockCount} Boulders={boulderCount} " +
				 $"(total {treeCount + mushBrownCount + mushRedCount + grassCount + flowerCount + rockCount + boulderCount} nodes)");
	}

	// =========================================================
	// Node factory helpers
	// =========================================================

	private static Node3D AddContainer(Node3D parent, string name)
	{
		var c = new Node3D { Name = name };
		parent.AddChild(c);
		return c;
	}

	/// <summary>Prop đơn giản: chỉ visual mesh, không collision. Cho cỏ/hoa/nấm/đá nhỏ.</summary>
	private static void AddSimpleProp(Node3D parent, string name, Mesh mesh,
									  Vector3 pos, float scale, float rotY)
	{
		var node = new MeshInstance3D
		{
			Name     = name,
			Mesh     = mesh,
			Position = pos,
			Rotation = new Vector3(0, rotY, 0),
			Scale    = new Vector3(scale, scale, scale),
		};
		parent.AddChild(node);
	}

	/// <summary>Cây: Node3D root + MeshInstance3D visual + StaticBody3D collision.</summary>
	private static void AddTreeNode(Node3D parent, string name, Mesh mesh,
									Vector3 pos, float scale, float rotY)
	{
		var root = new Node3D
		{
			Name     = name,
			Position = pos,
			Rotation = new Vector3(0, rotY, 0),
			Scale    = new Vector3(scale, scale, scale),
		};

		root.AddChild(new MeshInstance3D { Name = "Visual", Mesh = mesh });

		var body = new StaticBody3D { Name = "Body" };
		body.AddChild(new CollisionShape3D
		{
			Shape    = new CapsuleShape3D { Radius = 0.45f, Height = 5.0f },
			Position = new Vector3(0, 2.5f, 0),
		});
		root.AddChild(body);

		parent.AddChild(root);
	}

	/// <summary>Boulder: Node3D root + MeshInstance3D + SphereShape3D collision.</summary>
	private static void AddBoulderNode(Node3D parent, string name, Mesh mesh,
									   Vector3 pos, float scale, float rotY)
	{
		var root = new Node3D
		{
			Name     = name,
			Position = pos,
			Rotation = new Vector3(0, rotY, 0),
			Scale    = new Vector3(scale, scale, scale),
		};

		root.AddChild(new MeshInstance3D { Name = "Visual", Mesh = mesh });

		var body = new StaticBody3D { Name = "Body" };
		body.AddChild(new CollisionShape3D
		{
			Shape = new SphereShape3D { Radius = 0.5f },
		});
		root.AddChild(body);

		parent.AddChild(root);
	}

	// =========================================================
	// Zone-based spawn rules
	// =========================================================

	private struct SpawnRules
	{
		public float TreeMult, MushroomMult, GrassMult, FlowerMult, RockMult, BoulderMult;
	}

	private static SpawnRules GetSpawnRules(BiomeZone zone) => zone switch
	{
		BiomeZone.Village => new SpawnRules
		{
			TreeMult = 0f, MushroomMult = 0f, GrassMult = 0.6f,
			FlowerMult = 1.8f, RockMult = 0f, BoulderMult = 0f,
		},
		BiomeZone.Sand => new SpawnRules
		{
			TreeMult = 0f, MushroomMult = 0f, GrassMult = 0.2f,
			FlowerMult = 0f, RockMult = 3f, BoulderMult = 5f,
		},
		BiomeZone.RockWaste => new SpawnRules
		{
			TreeMult = 0f, MushroomMult = 0f, GrassMult = 0.1f,
			FlowerMult = 0f, RockMult = 6f, BoulderMult = 8f,
		},
		BiomeZone.GrassPlain => new SpawnRules
		{
			TreeMult = 0f, MushroomMult = 0.5f, GrassMult = 2.0f,
			FlowerMult = 3.0f, RockMult = 0.3f, BoulderMult = 0.4f,
		},
		BiomeZone.Elevated => new SpawnRules
		{
			TreeMult = 0f, MushroomMult = 0.4f, GrassMult = 0.8f,
			FlowerMult = 0.5f, RockMult = 1.8f, BoulderMult = 1.5f,
		},
		_ => new SpawnRules    // Forest
		{
			TreeMult = 1.6f, MushroomMult = 1.5f, GrassMult = 1.0f,
			FlowerMult = 1.0f, RockMult = 0.4f, BoulderMult = 0.3f,
		},
	};

	// =========================================================
	// Mesh builders (giữ nguyên)
	// =========================================================

	/// <summary>Cross-plane: 2 quad đan chéo 90° (dấu +).</summary>
	private static ArrayMesh MakeCrossPlane(string texPath, float width, float height)
	{
		var tex = GD.Load<Texture2D>(texPath);
		float hw = width * 0.5f;

		var verts = new Vector3[]
		{
			// Plane 1 (theo Z) front + back
			new(-hw, 0, 0), new( hw, 0, 0), new( hw, height, 0),
			new(-hw, 0, 0), new( hw, height, 0), new(-hw, height, 0),
			new( hw, 0, 0), new(-hw, 0, 0), new(-hw, height, 0),
			new( hw, 0, 0), new(-hw, height, 0), new( hw, height, 0),
			// Plane 2 (theo X) front + back
			new(0, 0, -hw), new(0, 0,  hw), new(0, height,  hw),
			new(0, 0, -hw), new(0, height, hw), new(0, height, -hw),
			new(0, 0,  hw), new(0, 0, -hw), new(0, height, -hw),
			new(0, 0,  hw), new(0, height, -hw), new(0, height,  hw),
		};
		var uvs = new Vector2[]
		{
			new(0,1), new(1,1), new(1,0),  new(0,1), new(1,0), new(0,0),
			new(1,1), new(0,1), new(0,0),  new(1,1), new(0,0), new(1,0),
			new(0,1), new(1,1), new(1,0),  new(0,1), new(1,0), new(0,0),
			new(1,1), new(0,1), new(0,0),  new(1,1), new(0,0), new(1,0),
		};
		return BuildQuadMesh(verts, uvs, tex, true);
	}

	/// <summary>Vertical Billboard: 1 quad thẳng đứng — dùng cho cỏ.</summary>
	private static ArrayMesh MakeVerticalPlane(string texPath, float width, float height)
	{
		var tex = GD.Load<Texture2D>(texPath);
		float hw = width * 0.5f;

		var verts = new Vector3[]
		{
			new(-hw, 0, 0), new(hw, 0, 0), new(hw, height, 0),
			new(-hw, 0, 0), new(hw, height, 0), new(-hw, height, 0),
			new(hw, 0, 0), new(-hw, 0, 0), new(-hw, height, 0),
			new(hw, 0, 0), new(-hw, height, 0), new(hw, height, 0),
		};
		var uvs = new Vector2[]
		{
			new(0,1), new(1,1), new(1,0), new(0,1), new(1,0), new(0,0),
			new(1,1), new(0,1), new(0,0), new(1,1), new(0,0), new(1,0),
		};
		return BuildQuadMesh(verts, uvs, tex, true);
	}

	/// <summary>
	/// Hoa = 1 ArrayMesh có 2 surface:
	///   Surface 0 = thân (quad đứng, texture flowe_stem.png)
	///   Surface 1 = bông (quad ngang, texture flower.png)
	/// → 1 MeshInstance3D / 1 hoa.
	/// </summary>
	private static ArrayMesh MakeFlowerComposite(string stemPath, string flowerPath)
	{
		float sw = 0.25f, sh = 0.6f;   // stem width, stem height
		float fw = 0.5f;                // flower width

		// Surface 0 — thân
		var stemVerts = new Vector3[]
		{
			new(-sw/2, 0, 0), new(sw/2, 0, 0), new(sw/2, sh, 0),
			new(-sw/2, 0, 0), new(sw/2, sh, 0), new(-sw/2, sh, 0),
			new(sw/2, 0, 0), new(-sw/2, 0, 0), new(-sw/2, sh, 0),
			new(sw/2, 0, 0), new(-sw/2, sh, 0), new(sw/2, sh, 0),
		};
		var stemUvs = new Vector2[]
		{
			new(0,1), new(1,1), new(1,0), new(0,1), new(1,0), new(0,0),
			new(1,1), new(0,1), new(0,0), new(1,1), new(0,0), new(1,0),
		};

		// Surface 1 — bông hoa
		float fh = sh;  // bông nằm tại đỉnh thân
		var headVerts = new Vector3[]
		{
			new(-fw/2, fh, -fw/2), new(fw/2, fh, -fw/2), new(fw/2, fh, fw/2),
			new(-fw/2, fh, -fw/2), new(fw/2, fh,  fw/2), new(-fw/2, fh, fw/2),
			new(fw/2, fh, -fw/2), new(-fw/2, fh, -fw/2), new(-fw/2, fh, fw/2),
			new(fw/2, fh, -fw/2), new(-fw/2, fh, fw/2),  new(fw/2, fh, fw/2),
		};
		var headUvs = new Vector2[]
		{
			new(0,0), new(1,0), new(1,1), new(0,0), new(1,1), new(0,1),
			new(1,0), new(0,0), new(0,1), new(1,0), new(0,1), new(1,1),
		};

		var mesh = new ArrayMesh();

		// Surface 0
		var arr0 = new Godot.Collections.Array();
		arr0.Resize((int)Mesh.ArrayType.Max);
		arr0[(int)Mesh.ArrayType.Vertex] = stemVerts;
		arr0[(int)Mesh.ArrayType.TexUV]  = stemUvs;
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arr0);
		mesh.SurfaceSetMaterial(0, MakeAlphaMaterial(GD.Load<Texture2D>(stemPath)));

		// Surface 1
		var arr1 = new Godot.Collections.Array();
		arr1.Resize((int)Mesh.ArrayType.Max);
		arr1[(int)Mesh.ArrayType.Vertex] = headVerts;
		arr1[(int)Mesh.ArrayType.TexUV]  = headUvs;
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arr1);
		mesh.SurfaceSetMaterial(1, MakeAlphaMaterial(GD.Load<Texture2D>(flowerPath)));

		return mesh;
	}

	/// <summary>Đá nhỏ low-poly.</summary>
	private static Mesh MakeRockMesh()
	{
		var mat = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.50f, 0.47f, 0.42f),
			Roughness   = 0.95f,
			Metallic    = 0.0f,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
		};
		return new SphereMesh
		{
			Radius = 0.5f, Height = 0.7f,
			RadialSegments = 6, Rings = 3,
			Material = mat,
		};
	}

	/// <summary>Boulder khổng lồ low-poly.</summary>
	private static Mesh MakeBoulderMesh()
	{
		var mat = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.42f, 0.40f, 0.38f),
			Roughness   = 1.0f,
			Metallic    = 0.0f,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
		};
		return new SphereMesh
		{
			Radius = 0.5f, Height = 0.6f,
			RadialSegments = 8, Rings = 4,
			Material = mat,
		};
	}

	// =========================================================
	// Mesh utility helpers
	// =========================================================

	private static ArrayMesh BuildQuadMesh(Vector3[] verts, Vector2[] uvs, Texture2D tex, bool alphaScissor)
	{
		var mat = MakeAlphaMaterial(tex, alphaScissor);

		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = verts;
		arrays[(int)Mesh.ArrayType.TexUV]  = uvs;

		var mesh = new ArrayMesh();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
		mesh.SurfaceSetMaterial(0, mat);
		return mesh;
	}

	private static StandardMaterial3D MakeAlphaMaterial(Texture2D tex, bool alphaScissor = true)
	{
		var mat = new StandardMaterial3D
		{
			AlbedoTexture = tex,
			CullMode      = BaseMaterial3D.CullModeEnum.Disabled,
			ShadingMode   = BaseMaterial3D.ShadingModeEnum.Unshaded,
		};
		if (alphaScissor)
		{
			mat.Transparency        = BaseMaterial3D.TransparencyEnum.AlphaScissor;
			mat.AlphaScissorThreshold = 0.5f;
		}
		return mat;
	}
}
