using Godot;
using System;
using System.Text.Json;

namespace FragmentOfJapanese.Items;

/// <summary>
/// Ví tiền người chơi (autoload). Tự lưu user://wallet.json khi thay đổi và khi thoát game.
/// </summary>
public partial class Wallet : Node
{
    public static Wallet Instance { get; private set; }

    public int Gold    { get; private set; }
    public int MaThach { get; private set; }

    public event Action Changed;

    private const string SavePath = "user://wallet.json";

    public override void _Ready()
    {
        Instance = this;
        Load();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) Save();
    }

    public void AddGold(int amount)
    {
        if (amount == 0) return;
        Gold = Mathf.Max(0, Gold + amount);
        Changed?.Invoke();
        Save();
    }

    public bool SpendGold(int amount)
    {
        if (amount < 0 || Gold < amount) return false;
        Gold -= amount;
        Changed?.Invoke();
        Save();
        return true;
    }

    public void AddMaThach(int amount)
    {
        if (amount == 0) return;
        MaThach = Mathf.Max(0, MaThach + amount);
        Changed?.Invoke();
        Save();
    }

    public bool SpendMaThach(int amount)
    {
        if (amount < 0 || MaThach < amount) return false;
        MaThach -= amount;
        Changed?.Invoke();
        Save();
        return true;
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(new { Gold, MaThach });
            using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
            f?.StoreString(json);
        }
        catch (Exception e) { GD.PushWarning($"[Wallet] Save lỗi: {e.Message}"); }
    }

    private void Load()
    {
        if (!FileAccess.FileExists(SavePath)) return;
        try
        {
            using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
            var doc = JsonDocument.Parse(f?.GetAsText() ?? "{}");
            Gold    = doc.RootElement.TryGetProperty("Gold",    out var g) ? g.GetInt32() : 0;
            MaThach = doc.RootElement.TryGetProperty("MaThach", out var m) ? m.GetInt32() : 0;
        }
        catch (Exception e) { GD.PushWarning($"[Wallet] Load lỗi: {e.Message}"); }
    }
}
