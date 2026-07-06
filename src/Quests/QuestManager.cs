using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using FragmentOfJapanese.Core;
using FragmentOfJapanese.Items;

namespace FragmentOfJapanese.Quests;

public partial class QuestManager : Node
{
    public static QuestManager Instance { get; private set; }

    private const string DefsPath = "res://data/quests.json";
    private readonly List<QuestEntry> _quests = new();
    private readonly Dictionary<string, QuestState> _state = new();

    public IReadOnlyList<QuestEntry> Quests => _quests;

    public event Action<QuestEntry> Progressed;
    public event Action<QuestEntry> Completed;
    public event Action<QuestEntry> Claimed;     
    public event Action             PeriodReset; 

    private DateTime _lastSyncUtcDate = DateTime.MinValue;
    private double _resetCheckTimer;

    public override void _Ready()
    {
        Instance = this;
        LoadDefs();
        // Thay vì LoadProgress từ local file, giờ sẽ load từ API
        _ = SyncAsync();
    }

    public override void _Process(double delta)
    {
        // Reset THỜI GIAN THỰC: nếu qua mốc ngày UTC (nửa đêm / thứ Hai) so với lần sync gần nhất
        // thì đồng bộ lại (server đã reset → client nhận trạng thái mới). Kiểm tra mỗi 30s.
        _resetCheckTimer += delta;
        if (_resetCheckTimer < 30.0) return;
        _resetCheckTimer = 0;

        if (_lastSyncUtcDate != DateTime.MinValue && DateTime.UtcNow.Date > _lastSyncUtcDate
            && !string.IsNullOrEmpty(Autoloads.ApiClient.Instance.AccessToken))
        {
            GD.Print("[Quest] Qua mốc reset (ngày UTC mới) → đồng bộ lại.");
            _ = SyncAsync();
        }
    }

    public async Task SyncAsync()
    {
        if (string.IsNullOrEmpty(Autoloads.ApiClient.Instance.AccessToken)) return;

        var res = await Autoloads.ApiClient.Instance.GetAsync("/api/quests");
        if (res.IsSuccessStatusCode)
        {
            var data = await Autoloads.ApiClient.Instance.ReadAsAsync<Autoloads.AccountManager.ApiResponse<List<QuestProgressDto>>>(res);
            if (data?.Data != null)
            {
                _state.Clear();
                foreach (var p in data.Data)
                {
                    var s = StateOf(p.QuestId);
                    s.Count = p.CurrentCount;
                    s.Claimed = p.IsClaimed;
                }
                _lastSyncUtcDate = DateTime.UtcNow.Date;
                PeriodReset?.Invoke();
            }
        }
    }

    public int  GetCount(string id)        => _state.TryGetValue(id, out var s) ? s.Count : 0;
    public bool IsCompleted(QuestEntry q)  => GetCount(q.Id) >= q.Target;
    public bool IsClaimed(string id)       => _state.TryGetValue(id, out var s) && s.Claimed;
    public List<QuestEntry> ByPeriod(string period) => _quests.FindAll(q => q.Period == period);

    public void Report(string type, string key = "", int amount = 1)
    {
        if (amount <= 0) return;

        foreach (var q in _quests)
        {
            if (q.Type != type) continue;
            if (!string.IsNullOrEmpty(q.TargetKey) && q.TargetKey != key) continue;

            var s = StateOf(q.Id);
            if (s.Count >= q.Target) continue;

            // Lưu tiến độ lên server; nếu lỗi mạng → đối soát lại để client khớp server (không mất tiến độ ngầm).
            _ = ReportToServerAsync(q.Id, amount);

            s.Count = Math.Min(s.Count + amount, q.Target);
            Progressed?.Invoke(q);
            
            if (s.Count >= q.Target)
            {
                GD.Print($"[Quest] HOÀN THÀNH: {q.NameVi}");
                Completed?.Invoke(q);
            }
        }
    }

    /// <summary>Gửi tiến độ lên server; thất bại thì đối soát lại (SyncAsync) để client không lệch server.</summary>
    private async Task ReportToServerAsync(string questId, int amount)
    {
        var res = await Autoloads.ApiClient.Instance.PostAsync("/api/quests/progress", new { QuestId = questId, Increment = amount });
        if (!res.IsSuccessStatusCode)
        {
            GD.PushWarning($"[Quest] Báo tiến độ '{questId}' lỗi ({(int)res.StatusCode}) → đối soát lại.");
            await SyncAsync();
        }
    }

    // Đổi tên thành ClaimAsync và dùng Task<bool>
    public async Task<bool> ClaimAsync(string id)
    {
        var q = _quests.Find(x => x.Id == id);
        if (q == null || !IsCompleted(q) || IsClaimed(id)) return false;

        // Gọi API nhận thưởng
        var res = await Autoloads.ApiClient.Instance.PostAsync($"/api/quests/{id}/claim", new { });
        if (!res.IsSuccessStatusCode) return false;

        var r = q.Reward;
        // Server cộng HẾT khi /claim (Vàng + EXP + chìa) → client chỉ đồng bộ lại (tránh đếm trùng).
        _ = Wallet.Instance?.SyncAsync();       // Vàng
        _ = Inventory.Instance?.SyncAsync();    // chìa bạc/vàng
        var player = GetTree().GetFirstNodeInGroup("player") as Entities.Player.Player;
        if (player != null) _ = player.SyncFromServerAsync();   // EXP/cấp

        StateOf(id).Claimed = true;
        GD.Print($"[Quest] Nhận '{q.NameVi}': +{r.Exp} exp, +{r.Gold} Vàng, +{r.SilverKeys} bạc, +{r.GoldenKeys} vàng");
        Claimed?.Invoke(q);   
        return true;
    }
    
    // Fallback cho QuestUi.cs đang dùng Claim() đồng bộ (để không bị lỗi build)
    public void Claim(string id)
    {
        _ = ClaimAsync(id);
    }

    private QuestState StateOf(string id)
    {
        if (!_state.TryGetValue(id, out var s)) { s = new QuestState(); _state[id] = s; }
        return s;
    }

    private void LoadDefs()
    {
        if (!FileAccess.FileExists(DefsPath)) { GD.PushWarning($"[Quest] Không thấy {DefsPath}"); return; }
        using var f = FileAccess.Open(DefsPath, FileAccess.ModeFlags.Read);
        var list = JsonSerializer.Deserialize<List<QuestEntry>>(f.GetAsText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (list != null) _quests.AddRange(list);
        foreach (var q in _quests) StateOf(q.Id);
    }

    private class QuestState
    {
        public int  Count   { get; set; }
        public bool Claimed { get; set; }
    }

    private class QuestProgressDto
    {
        public string QuestId { get; set; }
        public int CurrentCount { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsClaimed { get; set; }
    }
}
