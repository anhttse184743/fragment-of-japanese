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
        AccessToken = token;
        if (!string.IsNullOrEmpty(token))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            _http.DefaultRequestHeaders.Authorization = null;
        }
    }

    public async Task<HttpResponseMessage> PostAsync<T>(string endpoint, T payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        try
        {
            return await _http.PostAsync(endpoint, content);
        }
        catch (Exception ex)
        {
            GD.PushError($"[ApiClient] PostAsync failed: {ex.Message}");
            return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);
        }
    }

    public async Task<HttpResponseMessage> PutAsync<T>(string endpoint, T payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        try
        {
            return await _http.PutAsync(endpoint, content);
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
            return await _http.GetAsync(endpoint);
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
