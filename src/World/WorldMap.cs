using Godot;

namespace FragmentOfJapanese.World;

/// <summary>
/// Bản đồ chính — 1 scene duy nhất.
/// Hiển thị các khu vực, quản lý trạng thái unlock.
/// </summary>
public partial class WorldMap : Node2D
{
	public override void _Ready()
	{
		// TODO: Load zone unlock state từ SaveSystem
		GD.Print("[WorldMap] Ready");
	}

	public void UnlockZone(string zoneId)
	{
		// Cập nhật visual: bỏ lock icon, hiện tên zone
		GD.Print($"[WorldMap] Zone unlocked: {zoneId}");
	}
}
