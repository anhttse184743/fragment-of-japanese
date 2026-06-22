using Godot;
using FragmentOfJapanese.Items;

namespace FragmentOfJapanese.Quests;

/// <summary>
/// Harness test bảng nhiệm vụ (log-based).
///   L = học +1 · B = thắng trận +1 · K = đọc từ (voice) +1 · C = nhận mọi thưởng đã xong · P = in bảng
/// Tiến độ được lưu vào user://quests.save.json — tắt mở lại vẫn còn; sang ngày/tuần mới sẽ tự reset.
/// </summary>
public partial class QuestTest : Node
{
    public override void _Ready()
    {
        GD.Print("[QuestTest] L=học+1  B=thắng+1  K=đọc(voice)+1  C=nhận thưởng  P=in bảng");
        PrintBoard();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventKey k || !k.Pressed || k.Echo) return;
        var qm = QuestManager.Instance;
        switch (k.Keycode)
        {
            case Key.L: qm.Report("learn");  PrintBoard(); break;
            case Key.B: qm.Report("battle"); PrintBoard(); break;
            case Key.K: qm.Report("voice");  PrintBoard(); break;
            case Key.C: ClaimAll();          PrintBoard(); break;
            case Key.P: PrintBoard();                      break;
        }
    }

    private void ClaimAll()
    {
        var qm = QuestManager.Instance;
        foreach (var q in qm.Quests)
            if (qm.IsCompleted(q) && !qm.IsClaimed(q.Id))
                qm.Claim(q.Id);
    }

    private void PrintBoard()
    {
        GD.Print("──────── BẢNG NHIỆM VỤ ────────");
        PrintGroup("HÀNG NGÀY", "daily");
        PrintGroup("HÀNG TUẦN", "weekly");
        GD.Print($"  Ví: {Wallet.Instance?.Gold} Vàng / {Wallet.Instance?.MaThach} Ma Thạch");
        GD.Print("───────────────────────────────");
    }

    private void PrintGroup(string title, string period)
    {
        var qm = QuestManager.Instance;
        GD.Print($"  ▶ {title}");
        foreach (var q in qm.ByPeriod(period))
        {
            string mark = qm.IsClaimed(q.Id) ? "★"
                        : qm.IsCompleted(q)  ? "✓"
                        : " ";
            string note = qm.IsClaimed(q.Id) ? "đã nhận"
                        : qm.IsCompleted(q)  ? "→ bấm C nhận"
                        : "";
            GD.Print($"    [{mark}] {q.NameVi}  {qm.GetCount(q.Id)}/{q.Target}  {note}");
        }
    }
}
