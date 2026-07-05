using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

namespace FragmentOfJapanese.Autoloads;

/// <summary>
/// Quản lý gọi API tới Backend, tự động đính kèm Access Token.
/// Thay thế HTTPRequest của Godot để dùng HttpClient bất đồng bộ mạnh mẽ của C#.
/// </summary>
public partial class ApiClient : Node
{
    public static ApiClient Instance { get; private set; }

    private readonly System.Net.Http.HttpClient _http;
    public string AccessToken { get; private set; }
    
    /// <summary>URL backend mặc định = bản deploy Render (cloud, chung DB Supabase).
    /// Muốn test với backend chạy máy: đặt biến môi trường FOJ_API_URL=http://localhost:5044.</summary>
    private const string DefaultBaseUrl = "https://foj-backend.onrender.com";

    public static string BaseUrl
    {
        get
        {
            var env = System.Environment.GetEnvironmentVariable("FOJ_API_URL");
            return string.IsNullOrEmpty(env) ? DefaultBaseUrl : env;
        }
    }

    public ApiClient()
    {
        _http = new System.Net.Http.HttpClient();
        _http.BaseAddress = new Uri(BaseUrl);
        _http.Timeout = TimeSpan.FromSeconds(40);   // chịu cold-start Render free (server ngủ dậy ~30-50s)
    }

    public override void _Ready()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        _http?.Dispose();
    }

    public void SetAccessToken(string token)
    {
        // Token gắn theo TỪNG request (xem BuildRequest) để tránh tranh chấp header dùng chung
        // khi nhiều request chạy song song lúc đổi tài khoản (login/logout).
        AccessToken = token;
    }

    /// <summary>Tạo request có sẵn Authorization của token hiện tại (chụp tại thời điểm gọi).</summary>
    private HttpRequestMessage BuildRequest(HttpMethod method, string endpoint, HttpContent content = null)
    {
        var req = new HttpRequestMessage(method, endpoint) { Content = content };
        var token = AccessToken;
        if (!string.IsNullOrEmpty(token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }

    private static StringContent JsonBody<T>(T payload)
        => new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    public async Task<HttpResponseMessage> PostAsync<T>(string endpoint, T payload)
    {
        try
        {
            GD.Print($"[ApiClient] POST {BaseUrl}{endpoint}");
            var resp = await _http.SendAsync(BuildRequest(HttpMethod.Post, endpoint, JsonBody(payload)));
            GD.Print($"[ApiClient] POST {endpoint} → {(int)resp.StatusCode} {resp.StatusCode}");
            return resp;
        }
        catch (Exception ex)
        {
            GD.PushError($"[ApiClient] PostAsync {endpoint} FAILED: {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException != null)
                GD.PushError($"[ApiClient]   Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);
        }
    }

    public async Task<HttpResponseMessage> PutAsync<T>(string endpoint, T payload)
    {
        try
        {
            return await _http.SendAsync(BuildRequest(HttpMethod.Put, endpoint, JsonBody(payload)));
        }
        catch (Exception ex)
        {
            GD.PushError($"[ApiClient] PutAsync failed: {ex.Message}");
            return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);
        }
    }

    public async Task<HttpResponseMessage> GetAsync(string endpoint)
    {
        try
        {
            return await _http.SendAsync(BuildRequest(HttpMethod.Get, endpoint));
        }
        catch (Exception ex)
        {
            GD.PushError($"[ApiClient] GetAsync failed: {ex.Message}");
            return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);
        }
    }

    public async Task<TResponse> ReadAsAsync<TResponse>(HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception e)
        {
            GD.PushError($"[ApiClient] Deserialize failed: {e.Message}");
            return default;
        }
    }
}
