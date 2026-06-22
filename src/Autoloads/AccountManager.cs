using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;

// Godot cũng có RandomNumberGenerator → đặt bí danh cho lớp mã hoá để tránh nhập nhằng.
using Rng = System.Security.Cryptography.RandomNumberGenerator;

namespace FragmentOfJapanese.Autoloads;

/// <summary>
/// Quản lý tài khoản cục bộ (offline) + phiên đăng nhập hiện tại.
/// Mật khẩu được băm PBKDF2-SHA256 với salt ngẫu nhiên — KHÔNG lưu mật khẩu thô.
/// Dữ liệu: user://accounts.json (danh sách tài khoản) + user://session.json (ghi nhớ tên).
/// Là autoload → truy cập mọi nơi qua <c>AccountManager.Instance</c>.
/// Lớp lưu trữ được tách riêng nên sau này dễ thay bằng máy chủ thật.
/// </summary>
public partial class AccountManager : Node
{
    public static AccountManager Instance { get; private set; }

    public enum AuthResult
    {
        Ok,
        EmptyFields,
        InvalidUsername,
        InvalidPassword,
        PasswordMismatch,
        UserExists,
        UserNotFound,
        WrongPassword,
        Error,
    }

    private const string AccountsPath = "user://accounts.json";
    private const string SessionPath  = "user://session.json";

    private const int SaltBytes  = 16;
    private const int HashBytes  = 32;
    private const int Iterations = 100_000;

    /// <summary>Tên hiển thị của người chơi đang đăng nhập (null nếu chưa).</summary>
    public string CurrentUser { get; private set; }
    public bool   IsGuest     { get; private set; }
    public bool   IsLoggedIn  => !string.IsNullOrEmpty(CurrentUser);

    /// <summary>Tên đăng nhập lần trước (để điền sẵn) — chỉ có khi người chơi đã chọn "ghi nhớ".</summary>
    public string LastUsername { get; private set; } = "";
    public bool   RememberPref { get; private set; }

    public bool HasAccounts => _accounts.Count > 0;

    private Dictionary<string, AccountRecord> _accounts = new();

    public override void _Ready()
    {
        Instance = this;
        LoadAccounts();
        LoadSession();
    }

    // ───────────────────────── API ─────────────────────────

    /// <summary>Tạo tài khoản mới. Trả về <see cref="AuthResult.Ok"/> nếu thành công.</summary>
    public AuthResult Register(string username, string password, string confirm)
    {
        username = (username ?? "").Trim();
        password ??= "";
        confirm  ??= "";

        if (username.Length == 0 || password.Length == 0) return AuthResult.EmptyFields;
        if (!IsValidUsername(username))                    return AuthResult.InvalidUsername;
        if (password.Length < 6)                           return AuthResult.InvalidPassword;
        if (password != confirm)                           return AuthResult.PasswordMismatch;

        string key = username.ToLowerInvariant();
        if (_accounts.ContainsKey(key)) return AuthResult.UserExists;

        byte[] salt = Rng.GetBytes(SaltBytes);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashBytes);

        _accounts[key] = new AccountRecord
        {
            Display = username,
            Salt    = Convert.ToBase64String(salt),
            Hash    = Convert.ToBase64String(hash),
            Created = DateTime.UtcNow.ToString("o"),
        };
        SaveAccounts();
        return AuthResult.Ok;
    }

    /// <summary>Đăng nhập. Thành công thì gán <see cref="CurrentUser"/> và xử lý "ghi nhớ".</summary>
    public AuthResult Login(string username, string password, bool remember)
    {
        username = (username ?? "").Trim();
        password ??= "";

        if (username.Length == 0 || password.Length == 0) return AuthResult.EmptyFields;

        string key = username.ToLowerInvariant();
        if (!_accounts.TryGetValue(key, out var rec)) return AuthResult.UserNotFound;

        byte[] salt = Convert.FromBase64String(rec.Salt);
        byte[] want = Convert.FromBase64String(rec.Hash);
        byte[] got  = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, want.Length);
        if (!CryptographicOperations.FixedTimeEquals(got, want)) return AuthResult.WrongPassword;

        CurrentUser = rec.Display;
        IsGuest     = false;
        ApplyRemember(remember, rec.Display);
        return AuthResult.Ok;
    }

    /// <summary>Vào game không cần tài khoản (không lưu phiên).</summary>
    public void PlayAsGuest()
    {
        CurrentUser = "Khách";
        IsGuest     = true;
    }

    public void Logout()
    {
        CurrentUser = null;
        IsGuest     = false;
    }

    // ───────────────────────── Helpers ─────────────────────────

    private void ApplyRemember(bool remember, string display)
    {
        RememberPref = remember;
        if (remember)
        {
            LastUsername = display;
            SaveSession(display, true);
        }
        else
        {
            LastUsername = "";
            DeleteSession();
        }
    }

    private static bool IsValidUsername(string u)
    {
        if (u.Length < 3 || u.Length > 20) return false;
        foreach (char c in u)
            if (!(char.IsLetterOrDigit(c) || c == '_')) return false;
        return true;
    }

    // ----- Lưu / đọc -----

    private void LoadAccounts()
    {
        try
        {
            if (!FileAccess.FileExists(AccountsPath)) return;
            using var f = FileAccess.Open(AccountsPath, FileAccess.ModeFlags.Read);
            string json = f?.GetAsText();
            if (string.IsNullOrWhiteSpace(json)) return;
            _accounts = JsonSerializer.Deserialize<Dictionary<string, AccountRecord>>(json) ?? new();
        }
        catch (Exception e)
        {
            GD.PushWarning($"[AccountManager] Lỗi đọc tài khoản: {e.Message}");
            _accounts = new();
        }
    }

    private void SaveAccounts()
    {
        try
        {
            string json = JsonSerializer.Serialize(_accounts, new JsonSerializerOptions { WriteIndented = true });
            using var f = FileAccess.Open(AccountsPath, FileAccess.ModeFlags.Write);
            f?.StoreString(json);
        }
        catch (Exception e)
        {
            GD.PushWarning($"[AccountManager] Lỗi lưu tài khoản: {e.Message}");
        }
    }

    private void LoadSession()
    {
        try
        {
            if (!FileAccess.FileExists(SessionPath)) return;
            using var f = FileAccess.Open(SessionPath, FileAccess.ModeFlags.Read);
            string json = f?.GetAsText();
            if (string.IsNullOrWhiteSpace(json)) return;
            var s = JsonSerializer.Deserialize<SessionRecord>(json);
            if (s != null && s.Remember && !string.IsNullOrEmpty(s.User))
            {
                LastUsername = s.User;
                RememberPref = true;
            }
        }
        catch { /* phiên hỏng thì bỏ qua */ }
    }

    private void SaveSession(string user, bool remember)
    {
        try
        {
            string json = JsonSerializer.Serialize(new SessionRecord { User = user, Remember = remember });
            using var f = FileAccess.Open(SessionPath, FileAccess.ModeFlags.Write);
            f?.StoreString(json);
        }
        catch { /* không lưu được phiên cũng không sao */ }
    }

    private void DeleteSession()
    {
        if (!FileAccess.FileExists(SessionPath)) return;
        using var dir = DirAccess.Open("user://");
        dir?.Remove("session.json");
    }

    // ----- Mô hình dữ liệu (public để System.Text.Json đọc/ghi được) -----

    public sealed class AccountRecord
    {
        public string Display { get; set; } = "";
        public string Salt    { get; set; } = "";
        public string Hash    { get; set; } = "";
        public string Created { get; set; } = "";
    }

    public sealed class SessionRecord
    {
        public string User     { get; set; } = "";
        public bool   Remember { get; set; }
    }
}
