using Godot;
using FragmentOfJapanese.Autoloads;
using FragmentOfJapanese.Ui;

namespace FragmentOfJapanese.World;

/// <summary>
/// Cổng dịch chuyển 2.5D. Kế thừa <see cref="Interactable"/>: lại gần hiện bảng
/// "[E] Vào ...", bấm E → hộp xác nhận "Bạn có muốn vào ... không?" → Đồng ý thì
/// chuyển sang <see cref="ScenePath"/>.
///
/// Kéo Portal.tscn vào map rồi gắn ScenePath (map đích) + DestinationName trong
/// Inspector. ScenePath để trống thì cổng chỉ hiện xác nhận rồi báo "chưa gắn".
/// </summary>
public partial class Portal : Interactable
{
	[Export(PropertyHint.File, "*.tscn")] public string ScenePath { get; set; } = "";
	[Export] public string DestinationName { get; set; } = "khu vực này";

	public override void _Ready()
	{
		// Bảng prompt hiện "Vào {tên map}" — trừ khi đã chỉnh ActionText riêng trong Inspector.
		if (string.IsNullOrWhiteSpace(ActionText) || ActionText == "Tương tác")
			ActionText = $"Vào {DestinationName}";

		base._Ready();
		AddToGroup("portal");   // để SceneTransition tìm được cổng quay-về khi tới map khác
		Interacted += OnInteracted;
	}

	private void OnInteracted()
	{
		if (ConfirmUi.Instance == null)
		{
			GD.PrintErr("[Portal] ConfirmUi chưa được autoload — không hiện được xác nhận.");
			return;
		}

		ConfirmUi.Instance.Ask(
			$"Bạn có muốn vào \"{DestinationName}\" không?",
			onConfirm: EnterScene);
	}

	private void EnterScene()
	{
		if (string.IsNullOrEmpty(ScenePath))
		{
			GD.Print("[Portal] Chưa gắn ScenePath — bỏ qua chuyển map.");
			return;
		}

		// Người chơi sẽ xuất hiện tại cổng quay-về của map đích (SceneTransition lo) — không cần truyền toạ độ.
		if (SceneTransition.Instance != null)
			SceneTransition.Instance.GoTo(ScenePath);
		else
			GetTree().ChangeSceneToFile(ScenePath);
	}
}
