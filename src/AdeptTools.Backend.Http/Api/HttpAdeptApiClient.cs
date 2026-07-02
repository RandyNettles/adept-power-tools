using System.Net.Http.Json;
using System.Text.Json;
using AdeptTools.Core.Api;
using AdeptTools.Core.Models;

namespace AdeptTools.Backend.Http.Api;

public class HttpAdeptApiClient : IAdeptApiClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public HttpAdeptApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<UserInfo> GetUserInfoAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("api/account/UserInfo", ct);
        response.EnsureSuccessStatusCode();

        var userInfo = await response.Content.ReadFromJsonAsync<UserInfo>(JsonOptions, ct);
        return userInfo ?? new UserInfo();
    }

    public async Task<bool> IsLoggedInAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/account/isLoggedIn", ct);
            if (!response.IsSuccessStatusCode) return false;
            var content = await response.Content.ReadAsStringAsync(ct);
            return bool.TryParse(content, out var result) && result;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GetServerVersionAsync(CancellationToken ct = default)
    {
        var userInfo = await GetUserInfoAsync(ct);
        return userInfo.AppVersion ?? "unknown";
    }
}
