using Godot;

namespace FragmentOfJapanese.Ui;

/// <summary>Hành động khi chọn 1 lựa chọn (map sang code — delegate không export được).</summary>
public enum DialogueAction
{
    None,           // chỉ đóng thoại (hoặc chỉ hiện ReplyLines)
    OpenShop,       // mở ShopUi (trao đổi / mua bán)
    OpenLearning,   // mở màn học từ (chỉ NPC dạy học mới có tác dụng)
}

/// <summary>
/// Một dòng thoại SONG NGỮ — soạn trong Inspector.
/// Tiếng Nhật hiện to + gõ typewriter; tiếng Việt là bản dịch (nhỏ, hiện ngay).
/// Để trống 1 trong 2 = dòng đơn ngữ.
/// </summary>
[GlobalClass]
public partial class DialogueLineRes : Resource
{
    [Export(PropertyHint.MultilineText)] public string Japanese   { get; set; } = "";
    [Export(PropertyHint.MultilineText)] public string Vietnamese { get; set; } = "";
}

/// <summary>Một lựa chọn trong hội thoại — soạn ngay trong Inspector.</summary>
[GlobalClass]
public partial class DialogueChoiceRes : Resource
{
    [Export] public string Label { get; set; } = "Lựa chọn";

    /// <summary>NPC đáp lại khi chọn (mỗi phần tử = 1 dòng song ngữ). Để trống = không đáp.</summary>
    [Export] public DialogueLineRes[] ReplyLines { get; set; } = System.Array.Empty<DialogueLineRes>();

    /// <summary>Mở UI khác khi chọn (sau khi đáp xong, nếu có).</summary>
    [Export] public DialogueAction Action { get; set; } = DialogueAction.None;
}

/// <summary>
/// Một đoạn hội thoại NPC — soạn trong Inspector.
/// Gán vào ô <c>Dialogue</c> của NPC; để trống = dùng thoại mặc định trong code.
/// </summary>
[GlobalClass]
public partial class DialogueRes : Resource
{
    /// <summary>Tên người nói (để trống = lấy NpcName).</summary>
    [Export] public string Speaker { get; set; } = "";

    /// <summary>Các dòng thoại song ngữ — SỐ phần tử = số dòng, THỨ TỰ phần tử = thứ tự hiện.</summary>
    [Export] public DialogueLineRes[] Lines { get; set; } = System.Array.Empty<DialogueLineRes>();

    /// <summary>Lựa chọn 1/2/3 hiện sau dòng cuối. Để trống = không có lựa chọn.</summary>
    [Export] public DialogueChoiceRes[] Choices { get; set; } = System.Array.Empty<DialogueChoiceRes>();
}
