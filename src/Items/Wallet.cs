using Godot;
using System;

namespace FragmentOfJapanese.Items;

/// <summary>
/// Ví tiền người chơi (autoload). Hai loại tiền tệ:
///   - Vàng (Gold)     : kiếm trong game — mua/bán ở shop, đổi đồ.
///   - Ma Thạch (MaThach): tiền nạp (premium) — dùng cho gacha cao cấp.
/// Phát <see cref="Changed"/> khi số dư đổi → UI tự cập nhật.
/// </summary>
public partial class Wallet : Node
{
    public static Wallet Instance { get; private set; }

    public int Gold    { get; private set; }
    public int MaThach { get; private set; }

    public event Action Changed;

    public override void _Ready()
    {
        Instance = this;

        // DEMO: số dư khởi đầu để xem UI. Xóa khi có nguồn thu/chi thật.
        Gold    = 1250;
        MaThach = 48;
    }

    public void AddGold(int amount)
    {
        if (amount == 0) return;
        Gold = Mathf.Max(0, Gold + amount);
        Changed?.Invoke();
    }

    /// <summary>Trừ Vàng. False nếu không đủ.</summary>
    public bool SpendGold(int amount)
    {
        if (amount < 0 || Gold < amount) return false;
        Gold -= amount;
        Changed?.Invoke();
        return true;
    }

    public void AddMaThach(int amount)
    {
        if (amount == 0) return;
        MaThach = Mathf.Max(0, MaThach + amount);
        Changed?.Invoke();
    }

    /// <summary>Trừ Ma Thạch. False nếu không đủ.</summary>
    public bool SpendMaThach(int amount)
    {
        if (amount < 0 || MaThach < amount) return false;
        MaThach -= amount;
        Changed?.Invoke();
        return true;
    }
}
