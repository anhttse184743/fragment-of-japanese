using System;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

namespace FragmentOfJapanese.Autoloads;

/// <summary>
/// Quản lý tài khoản thông qua API thay vì lưu cục bộ.
/// </summary>
public partial class AccountManager : Node
{
    public static AccountManager Instance { get; private set; }

    public enum AuthResult
    {
        Ok,
        EmptyFields,
        InvalidUsername,
        InvalidEmail,
        InvalidPassword,
        PasswordMismatch,
        UserExists,
        EmailExists,
        UserNotFound,
        WrongPassword,
        Error,
    }

    private const string SessionPath = "user://session.json";

    public string CurrentUser { get; private set; }
    public string CurrentEmail { get; private set; }
    public bool IsGuest { get; private set; }
    public bool IsLoggedIn => !string.IsNullOrEmpty(CurrentUser);

    public string LastEmail { get; private set; } = "";
    public bool RememberPref { get; private set; }

    private string _savedRefreshToken = "";
    /// <summary>Có phiên đã ghi nhớ (refresh token) để thử tự đăng nhập không?</summary>
    public bool HasRememberedSession => RememberPref && !string.IsNullOrEmpty(_savedRefreshToken);

    public override void _Ready()
    {
        Instance = this;
        LoadSession();
    }

    public async Task<AuthResult> Register(string username, string email, string password, string confirm)
    {
        username = (username ?? "").Trim();
        email = (email ?? "").Trim().ToLowerInvariant();
        password ??= "";
        confirm ??= "";

        if (username.Length == 0 || email.Length == 0 || password.Length == 0) return AuthResult.EmptyFields;
        if (!IsValidUsername(username)) return AuthResult.InvalidUsername;
        if (!IsValidEmail(email)) return AuthResult.InvalidEmail;
        if (password.Length < 6) return AuthResult.InvalidPassword;
        if (password != confirm) return AuthResult.PasswordMismatch;

        var req = new RegisterRequest { Username = username, Email = email, Password = password };
        var res = await ApiClient.Instance.PostAsync("/api/auth/register", req);

        if (res.IsSuccessStatusCode)
        {
            var data = await ApiClient.Instance.ReadAsAsync<ApiResponse<AuthResponse>>(res);
            if (data?.Data != null)
            {
                ApiClient.Instance.SetAccessToken(data.Data.AccessToken);
                CurrentUser = data.Data.Username;
                CurrentEmail = email;
                IsGuest = false;
                ApplyRemember(true, email, data.Data.RefreshToken);
                ClearLegacyData();
                await GameSync.SyncAllAsync();   // nạp lại ví/túi/học/nhiệm vụ cho tài khoản mới
                return AuthResult.Ok;
            }
        }
        
        if (res.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            var err = await ApiClient.Instance.ReadAsAsync<ApiResponse<object>>(res);
            if (err?.Message != null)
            {
                if (err.Message.Contains("Username")) return AuthResult.UserExists;
                if (err.Message.Contains("Email")) return AuthResult.EmailExists;
            }
        }
        return AuthResult.Error;
    }

    public async Task<AuthResult> Login(string email, string password, bool remember)
    {
        email = (email ?? "").Trim().ToLowerInvariant();
        password ??= "";

        if (email.Length == 0 || password.Length == 0) return AuthResult.EmptyFields;

        var req = new LoginRequest { Email = email, Password = password };
        var res = await ApiClient.Instance.PostAsync("/api/auth/login", req);

        if (res.IsSuccessStatusCode)
        {
            var data = await ApiClient.Instance.ReadAsAsync<ApiResponse<AuthResponse>>(res);
            if (data?.Data != null)
            {
                ApiClient.Instance.SetAccessToken(data.Data.AccessToken);
                CurrentUser = data.Data.Username;
                CurrentEmail = email;
                IsGuest = false;
                ApplyRemember(remember, email, data.Data.RefreshToken);
                ClearLegacyData();
                await GameSync.SyncAllAsync();   // nạp lại ví/túi/học/nhiệm vụ sau khi đăng nhập
                return AuthResult.Ok;
            }
        }

        if (res.StatusCode == System.Net.HttpStatusCode.BadRequest || res.StatusCode == System.Net.HttpStatusCode.Unauthorized || res.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            var err = await ApiClient.Instance.ReadAsAsync<ApiResponse<object>>(res);
            if (err?.Message != null && err.Message.Contains("password", StringComparison.OrdinalIgnoreCase)) return AuthResult.WrongPassword;
            return AuthResult.UserNotFound;
        }

        return AuthResult.Error;
    }

    /// <summary>
    /// Tự đăng nhập lại bằng refresh token đã lưu (phiên "ghi nhớ").
    /// Dùng khi mở lại game hoặc quay về sau khi thanh toán (Android có thể khởi động lại app).
    /// Trả true nếu khôi phục được phiên. Refresh token hết hạn/không hợp lệ → xóa phiên, trả false.
    /// </summary>
    public async Task<bool> TryAutoLoginAsync()
    {
        if (string.IsNullOrEmpty(_savedRefreshToken)) return false;

        var res = await ApiClient.Instance.PostAsync("/api/auth/refresh",
            new RefreshRequest { RefreshToken = _savedRefreshToken });

        if (!res.IsSuccessStatusCode)
        {
            // Refresh token hết hạn/thu hồi → bỏ phiên để lần sau không thử lại vô ích.
            if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized) { _savedRefreshToken = ""; DeleteSession(); }
            return false;
        }

        var data = await ApiClient.Instance.ReadAsAsync<ApiResponse<AuthResponse>>(res);
        if (data?.Data == null || string.IsNullOrEmpty(data.Data.AccessToken)) return false;

        ApiClient.Instance.SetAccessToken(data.Data.AccessToken);
        CurrentUser  = data.Data.Username;
        CurrentEmail = LastEmail;
        IsGuest      = false;
        ApplyRemember(true, LastEmail, data.Data.RefreshToken);   // lưu refresh token đã xoay vòng
        ClearLegacyData();
        await GameSync.SyncAllAsync();
        return true;
    }

    public void Logout()
    {
        CurrentUser = null;
        CurrentEmail = null;
        IsGuest = false;
        _savedRefreshToken = "";
        RememberPref = false;
        ApiClient.Instance.SetAccessToken(null);
        DeleteSession();
    }

    private void ApplyRemember(bool remember, string email, string refreshToken)
    {
        RememberPref = remember;
        if (remember)
        {
            LastEmail = email;
            _savedRefreshToken = refreshToken ?? "";
            SaveSession(email, refreshToken);
        }
        else
        {
            LastEmail = "";
            _savedRefreshToken = "";
            DeleteSession();
        }
    }

    private static bool IsValidUsername(string u)
    {
        if (u.Length < 3 || u.Length > 20) return false;
        foreach (char c in u) if (!(char.IsLetterOrDigit(c) || c == '_')) return false;
        return true;
    }

    private static bool IsValidEmail(string e)
    {
        int at = e.IndexOf('@');
        if (at <= 0 || at == e.Length - 1) return false;
        int dot = e.LastIndexOf('.');
        return dot > at + 1 && dot < e.Length - 1;
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
                LastEmail = s.User;
                RememberPref = true;
                _savedRefreshToken = s.RefreshToken ?? "";   // dùng để tự đăng nhập lại (TryAutoLoginAsync)
            }
        }
        catch { }
    }

    private void SaveSession(string user, string refreshToken)
    {
        try
        {
            string json = JsonSerializer.Serialize(new SessionRecord { User = user, RefreshToken = refreshToken, Remember = true });
            using var f = FileAccess.Open(SessionPath, FileAccess.ModeFlags.Write);
            f?.StoreString(json);
        }
        catch { }
    }

    private void DeleteSession()
    {
        if (!FileAccess.FileExists(SessionPath)) return;
        using var dir = DirAccess.Open("user://");
        dir?.Remove("session.json");
    }

    private void ClearLegacyData()
    {
        using var dir = DirAccess.Open("user://");
        if (dir != null)
        {
            dir.Remove("player.json");
            dir.Remove("wallet.json");
            dir.Remove("quests.save.json");
            dir.Remove("save.json");
        }
    }

    public sealed class SessionRecord
    {
        public string User { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public bool Remember { get; set; }
    }

    // --- DTOs ---
    public class RegisterRequest { public string Username { get; set; } public string Email { get; set; } public string Password { get; set; } }
    public class LoginRequest { public string Email { get; set; } public string Password { get; set; } }
    public class RefreshRequest { public string RefreshToken { get; set; } }
    public class AuthResponse { public string AccessToken { get; set; } public string RefreshToken { get; set; } public DateTime ExpiresAt { get; set; } public string Username { get; set; } public string Role { get; set; } }
    public class ApiResponse<T> { public bool Success { get; set; } public T Data { get; set; } public string Message { get; set; } }
}
