using System.Collections.Generic;
using System.Threading.Tasks;

namespace FragmentOfJapanese.Autoloads;

/// <summary>
/// Điểm đồng bộ DUY NHẤT sau khi đăng nhập (hoặc khi vào lại scene gameplay).
/// Nạp lại toàn bộ trạng thái người chơi từ server: ví, túi đồ, tiến trình học, nhiệm vụ.
/// (Player tự đồng bộ Level/Exp trong _Ready của nó.)
/// Mỗi hệ SyncAsync đều "xóa rồi nạp lại" nên gọi sau login sẽ thay sạch dữ liệu của phiên trước.
/// </summary>
public static class GameSync
{
    public static async Task SyncAllAsync()
    {
        var tasks = new List<Task>();
        if (Items.Wallet.Instance            != null) tasks.Add(Items.Wallet.Instance.SyncAsync());
        if (Items.Inventory.Instance         != null) tasks.Add(Items.Inventory.Instance.SyncAsync());
        if (Learning.LearningTracker.Instance != null) tasks.Add(Learning.LearningTracker.Instance.SyncAsync());
        if (Quests.QuestManager.Instance     != null) tasks.Add(Quests.QuestManager.Instance.SyncAsync());
        if (Ads.AdManager.Instance           != null) tasks.Add(Ads.AdManager.Instance.SyncAsync());
        if (Cosmetics.SkinManager.Instance   != null) tasks.Add(Cosmetics.SkinManager.Instance.SyncAsync());
        await Task.WhenAll(tasks);
    }
}
