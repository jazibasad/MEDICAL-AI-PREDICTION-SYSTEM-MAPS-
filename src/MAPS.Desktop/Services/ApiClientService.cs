using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace MAPS.Desktop.Services;

/// <summary>
/// Shared HTTP client service for all WinForms forms.
/// Handles JWT auth header injection automatically.
/// </summary>
public class ApiClientService
{
    private readonly HttpClient _http;
    private string?  _accessToken;
    private string?  _refreshToken;

    public ApiClientService(string baseUrl)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout     = TimeSpan.FromSeconds(120) // Long timeout for AI inference
        };
    }

    public void SetTokens(string accessToken, string refreshToken)
    {
        _accessToken  = accessToken;
        _refreshToken = refreshToken;
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
    }

    public void ClearTokens()
    {
        _accessToken  = null;
        _refreshToken = null;
        _http.DefaultRequestHeaders.Authorization = null;
    }

    public string? AccessToken  => _accessToken;
    public bool    IsLoggedIn   => !string.IsNullOrEmpty(_accessToken);

    // ── GET ────────────────────────────────────────────────────────────────────
    public async Task<T?> GetAsync<T>(string endpoint)
    {
        var response = await _http.GetAsync(endpoint);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }

    // ── POST ───────────────────────────────────────────────────────────────────
    public async Task<T?> PostAsync<T>(string endpoint, object? body)
    {
        var content  = body is null
            ? new StringContent("", Encoding.UTF8, "application/json")
            : new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(endpoint, content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }

    public async Task PostAsync(string endpoint, object? body)
        => await PostAsync<object>(endpoint, body);

    // ── PUT ────────────────────────────────────────────────────────────────────
    public async Task<T?> PutAsync<T>(string endpoint, object? body)
    {
        var content  = body is null
            ? new StringContent("", Encoding.UTF8, "application/json")
            : new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await _http.PutAsync(endpoint, content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }

    public async Task PutAsync(string endpoint, object? body)
        => await PutAsync<object>(endpoint, body);

    // ── DELETE ─────────────────────────────────────────────────────────────────
    public async Task DeleteAsync(string endpoint)
    {
        var response = await _http.DeleteAsync(endpoint);
        response.EnsureSuccessStatusCode();
    }

    // ── MULTIPART (file upload) ────────────────────────────────────────────────
    public async Task<T?> PostMultipartAsync<T>(string endpoint, MultipartFormDataContent content)
    {
        var response = await _http.PostAsync(endpoint, content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }
}
