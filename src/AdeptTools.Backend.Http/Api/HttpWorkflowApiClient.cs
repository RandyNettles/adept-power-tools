using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AdeptTools.Core.Auth;
using AdeptTools.Core.Models;
using AdeptTools.Workflow.Api;
using AdeptTools.Workflow.Input;
using AdeptTools.Workflow.Models;

namespace AdeptTools.Backend.Http.Api;

public class HttpWorkflowApiClient : IWorkflowApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IAdeptAuthService? _authService;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public HttpWorkflowApiClient(HttpClient httpClient, IAdeptAuthService? authService = null)
    {
        _httpClient = httpClient;
        _authService = authService;
    }

    public async Task<WorkflowSetup> GetSetupAsync(CancellationToken ct = default)
    {
        var endpoint = "api/admin/workflow/setup";
        var response = await GetAsyncWithAuthRefreshAsync(endpoint, ct);
        await EnsureSuccessOrThrowAsync(response, endpoint, ct);
        return await response.Content.ReadFromJsonAsync<WorkflowSetup>(JsonOptions, ct) ?? new WorkflowSetup();
    }

    public async Task<WorkflowAdminPacket> GetWorkflowsAsync(CancellationToken ct = default)
    {
        var endpoint = "api/admin/workflow/workflows";
        var response = await GetAsyncWithAuthRefreshAsync(endpoint, ct);
        await EnsureSuccessOrThrowAsync(response, endpoint, ct);
        return await response.Content.ReadFromJsonAsync<WorkflowAdminPacket>(JsonOptions, ct) ?? new WorkflowAdminPacket();
    }

    public async Task<WorkflowAdminPacket> GetWorkflowsBasicAsync(CancellationToken ct = default)
    {
        var endpoint = "api/admin/workflow/workflows/basic";
        var response = await GetAsyncWithAuthRefreshAsync(endpoint, ct);
        await EnsureSuccessOrThrowAsync(response, endpoint, ct);
        return await response.Content.ReadFromJsonAsync<WorkflowAdminPacket>(JsonOptions, ct) ?? new WorkflowAdminPacket();
    }

    public async Task<WorkflowEditModel> CreateNewAsync(CancellationToken ct = default)
    {
        var endpoint = "api/admin/workflow/create-new";
        var response = await GetAsyncWithAuthRefreshAsync(endpoint, ct);
        await EnsureSuccessOrThrowAsync(response, endpoint, ct);
        return await response.Content.ReadFromJsonAsync<WorkflowEditModel>(JsonOptions, ct) ?? new WorkflowEditModel();
    }

    public async Task<WorkflowEditModel> GetWorkflowAsync(string workflowId, CancellationToken ct = default)
    {
        var endpoint = $"api/admin/workflow/workflow/{workflowId}";
        var response = await GetAsyncWithAuthRefreshAsync(endpoint, ct);
        await EnsureSuccessOrThrowAsync(response, endpoint, ct);
        return await response.Content.ReadFromJsonAsync<WorkflowEditModel>(JsonOptions, ct) ?? new WorkflowEditModel();
    }

    public async Task<ApiResult> SaveWorkflowAsync(WorkflowEditModel model, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(model, JsonOptions);
        var endpoint = "api/admin/workflow/workflow";
        var response = await PostJsonWithAuthRefreshAsync(endpoint, json, ct);
        await EnsureSuccessOrThrowAsync(response, endpoint, ct);
        return await response.Content.ReadFromJsonAsync<ApiResult>(JsonOptions, ct) ?? new ApiResult();
    }

    public async Task<ApiResult> DeleteWorkflowAsync(string workflowId, CancellationToken ct = default)
    {
        var endpoint = $"api/admin/workflow/workflow/{workflowId}";
        var response = await DeleteWithAuthRefreshAsync(endpoint, ct);
        await EnsureSuccessOrThrowAsync(response, endpoint, ct);
        return await response.Content.ReadFromJsonAsync<ApiResult>(JsonOptions, ct) ?? new ApiResult();
    }

    public async Task<WorkflowEditModel> AddStepAsync(WorkflowEditModel model, int position, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(model, JsonOptions);
        var endpoint = $"api/admin/workflow/steps/add/{position}";
        var response = await PostJsonWithAuthRefreshAsync(endpoint, json, ct);
        await EnsureSuccessOrThrowAsync(response, endpoint, ct);
        return await response.Content.ReadFromJsonAsync<WorkflowEditModel>(JsonOptions, ct) ?? new WorkflowEditModel();
    }

    public async Task<WorkflowEditModel> TagAsync(string workflowId, CancellationToken ct = default)
    {
        var endpoint = $"api/admin/workflow/tag/{workflowId}";
        var response = await GetAsyncWithAuthRefreshAsync(endpoint, ct);
        await EnsureSuccessOrThrowAsync(response, endpoint, ct);
        return await response.Content.ReadFromJsonAsync<WorkflowEditModel>(JsonOptions, ct) ?? new WorkflowEditModel();
    }

    public async Task<ApiResult> UntagAsync(string workflowId, CancellationToken ct = default)
    {
        var endpoint = $"api/admin/workflow/untag/{workflowId}";
        var response = await GetAsyncWithAuthRefreshAsync(endpoint, ct);
        await EnsureSuccessOrThrowAsync(response, endpoint, ct);
        return await response.Content.ReadFromJsonAsync<ApiResult>(JsonOptions, ct) ?? new ApiResult();
    }

    public async Task<List<WorkflowCommonTarget>> GetMetagroupsAsync(CancellationToken ct = default)
    {
        var endpoint = "api/admin/workflow/metagroups/true";
        var response = await GetAsyncWithAuthRefreshAsync(endpoint, ct);
        await EnsureSuccessOrThrowAsync(response, endpoint, ct);
        return await response.Content.ReadFromJsonAsync<List<WorkflowCommonTarget>>(JsonOptions, ct) ?? new List<WorkflowCommonTarget>();
    }

    public async Task<List<AdeptUserEntry>> GetUsersAsync(CancellationToken ct = default)
    {
        var attempted = new List<string>();

        var primaryEndpoint = "api/admin/user/users";
        attempted.Add(primaryEndpoint);
        var primaryResponse = await GetAsyncWithAuthRefreshAsync(primaryEndpoint, ct);
        if (primaryResponse.IsSuccessStatusCode)
        {
            return await primaryResponse.Content.ReadFromJsonAsync<List<AdeptUserEntry>>(JsonOptions, ct) ?? new List<AdeptUserEntry>();
        }

        if (primaryResponse.StatusCode != HttpStatusCode.NotFound)
        {
            await EnsureSuccessOrThrowAsync(primaryResponse, primaryEndpoint, ct);
        }

        var legacyEndpoint = "api/Account/UserInfo/EmailList";
        attempted.Add(legacyEndpoint);
        var legacyResponse = await GetAsyncWithAuthRefreshAsync(legacyEndpoint, ct);
        if (legacyResponse.IsSuccessStatusCode)
        {
            var legacyUsers = await legacyResponse.Content.ReadFromJsonAsync<List<LegacyUserInfo>>(JsonOptions, ct)
                ?? new List<LegacyUserInfo>();

            return legacyUsers
                .Where(u => !string.IsNullOrWhiteSpace(u.LoginName))
                .Select(u => new AdeptUserEntry
                {
                    UserId = u.LoginName ?? string.Empty,
                    DisplayName = string.IsNullOrWhiteSpace(u.UserName) ? u.LoginName ?? string.Empty : u.UserName
                })
                .ToList();
        }

        if (legacyResponse.StatusCode != HttpStatusCode.NotFound)
        {
            await EnsureSuccessOrThrowAsync(legacyResponse, legacyEndpoint, ct);
        }

        await EnsureSuccessOrThrowAsync(
            legacyResponse,
            $"{primaryEndpoint} (fallbacks tried: {string.Join(", ", attempted)})",
            ct);

        return new List<AdeptUserEntry>();
    }

    private async Task<HttpResponseMessage> GetAsyncWithAuthRefreshAsync(string endpoint, CancellationToken ct)
    {
        var response = await _httpClient.GetAsync(endpoint, ct);
        return await RetryAfterAuthRefreshAsync(response, () => _httpClient.GetAsync(endpoint, ct), ct);
    }

    private async Task<HttpResponseMessage> DeleteWithAuthRefreshAsync(string endpoint, CancellationToken ct)
    {
        var response = await _httpClient.DeleteAsync(endpoint, ct);
        return await RetryAfterAuthRefreshAsync(response, () => _httpClient.DeleteAsync(endpoint, ct), ct);
    }

    private async Task<HttpResponseMessage> PostJsonWithAuthRefreshAsync(string endpoint, string json, CancellationToken ct)
    {
        var response = await _httpClient.PostAsync(endpoint, new StringContent(json, Encoding.UTF8, "application/json"), ct);
        return await RetryAfterAuthRefreshAsync(
            response,
            () => _httpClient.PostAsync(endpoint, new StringContent(json, Encoding.UTF8, "application/json"), ct),
            ct);
    }

    private async Task<HttpResponseMessage> RetryAfterAuthRefreshAsync(
        HttpResponseMessage response,
        Func<Task<HttpResponseMessage>> retry,
        CancellationToken ct)
    {
        if (response.StatusCode != HttpStatusCode.Unauthorized || _authService is null)
            return response;

        var refresh = await _authService.RefreshAsync(ct);
        if (!refresh.Success || string.IsNullOrWhiteSpace(refresh.AccessToken))
            return response;

        response.Dispose();
        return await retry();
    }

    private class LegacyUserInfo
    {
        public string? LoginName { get; set; }
        public string? UserName { get; set; }
    }

    private async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response, string endpoint, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        var resolved = response.RequestMessage?.RequestUri?.ToString() ?? endpoint;
        var statusText = $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase ?? "Unknown"})";
        var body = await response.Content.ReadAsStringAsync(ct);
        var bodySnippet = string.IsNullOrWhiteSpace(body)
            ? string.Empty
            : $" Body: {body[..Math.Min(220, body.Length)]}";

        var hint = response.StatusCode == HttpStatusCode.NotFound
            ? " The configured server URL may be missing the Adept web app path (for example '/Synergis.WebApi/' or '/AdeptWeb/'), or this server version uses different workflow routes."
            : string.Empty;

        throw new HttpRequestException(
            $"Workflow API request failed at '{resolved}' with {statusText}.{hint}{bodySnippet}");
    }
}
