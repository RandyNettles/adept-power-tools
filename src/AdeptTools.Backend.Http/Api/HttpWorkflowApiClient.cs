using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AdeptTools.Core.Models;
using AdeptTools.Workflow.Api;
using AdeptTools.Workflow.Input;
using AdeptTools.Workflow.Models;

namespace AdeptTools.Backend.Http.Api;

public class HttpWorkflowApiClient : IWorkflowApiClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public HttpWorkflowApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<WorkflowSetup> GetSetupAsync(CancellationToken ct = default)
    {
        var endpoint = "api/admin/workflow/setup";
        var response = await _httpClient.GetAsync(endpoint, ct);
        await EnsureSuccessOrThrowAsync(response, endpoint, ct);
        return await response.Content.ReadFromJsonAsync<WorkflowSetup>(JsonOptions, ct) ?? new WorkflowSetup();
    }

    public async Task<WorkflowAdminPacket> GetWorkflowsAsync(CancellationToken ct = default)
    {
        var endpoint = "api/admin/workflow/workflows";
        var response = await _httpClient.GetAsync(endpoint, ct);
        await EnsureSuccessOrThrowAsync(response, endpoint, ct);
        return await response.Content.ReadFromJsonAsync<WorkflowAdminPacket>(JsonOptions, ct) ?? new WorkflowAdminPacket();
    }

    public async Task<WorkflowAdminPacket> GetWorkflowsBasicAsync(CancellationToken ct = default)
    {
        var endpoint = "api/admin/workflow/workflows/basic";
        var response = await _httpClient.GetAsync(endpoint, ct);
        await EnsureSuccessOrThrowAsync(response, endpoint, ct);
        return await response.Content.ReadFromJsonAsync<WorkflowAdminPacket>(JsonOptions, ct) ?? new WorkflowAdminPacket();
    }

    public async Task<WorkflowEditModel> CreateNewAsync(CancellationToken ct = default)
    {
        var endpoint = "api/admin/workflow/create-new";
        var response = await _httpClient.GetAsync(endpoint, ct);
        await EnsureSuccessOrThrowAsync(response, endpoint, ct);
        return await response.Content.ReadFromJsonAsync<WorkflowEditModel>(JsonOptions, ct) ?? new WorkflowEditModel();
    }

    public async Task<WorkflowEditModel> GetWorkflowAsync(string workflowId, CancellationToken ct = default)
    {
        var endpoint = $"api/admin/workflow/workflow/{workflowId}";
        var response = await _httpClient.GetAsync(endpoint, ct);
        await EnsureSuccessOrThrowAsync(response, endpoint, ct);
        return await response.Content.ReadFromJsonAsync<WorkflowEditModel>(JsonOptions, ct) ?? new WorkflowEditModel();
    }

    public async Task<ApiResult> SaveWorkflowAsync(WorkflowEditModel model, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(model, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var endpoint = "api/admin/workflow/workflow";
        var response = await _httpClient.PostAsync(endpoint, content, ct);
        await EnsureSuccessOrThrowAsync(response, endpoint, ct);
        return await response.Content.ReadFromJsonAsync<ApiResult>(JsonOptions, ct) ?? new ApiResult();
    }

    public async Task<ApiResult> DeleteWorkflowAsync(string workflowId, CancellationToken ct = default)
    {
        var endpoint = $"api/admin/workflow/workflow/{workflowId}";
        var response = await _httpClient.DeleteAsync(endpoint, ct);
        await EnsureSuccessOrThrowAsync(response, endpoint, ct);
        return await response.Content.ReadFromJsonAsync<ApiResult>(JsonOptions, ct) ?? new ApiResult();
    }

    public async Task<WorkflowEditModel> AddStepAsync(WorkflowEditModel model, int position, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(model, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var endpoint = $"api/admin/workflow/steps/add/{position}";
        var response = await _httpClient.PostAsync(endpoint, content, ct);
        await EnsureSuccessOrThrowAsync(response, endpoint, ct);
        return await response.Content.ReadFromJsonAsync<WorkflowEditModel>(JsonOptions, ct) ?? new WorkflowEditModel();
    }

    public async Task<WorkflowEditModel> TagAsync(string workflowId, CancellationToken ct = default)
    {
        var endpoint = $"api/admin/workflow/tag/{workflowId}";
        var response = await _httpClient.GetAsync(endpoint, ct);
        await EnsureSuccessOrThrowAsync(response, endpoint, ct);
        return await response.Content.ReadFromJsonAsync<WorkflowEditModel>(JsonOptions, ct) ?? new WorkflowEditModel();
    }

    public async Task<ApiResult> UntagAsync(string workflowId, CancellationToken ct = default)
    {
        var endpoint = $"api/admin/workflow/untag/{workflowId}";
        var response = await _httpClient.GetAsync(endpoint, ct);
        await EnsureSuccessOrThrowAsync(response, endpoint, ct);
        return await response.Content.ReadFromJsonAsync<ApiResult>(JsonOptions, ct) ?? new ApiResult();
    }

    public async Task<List<WorkflowCommonTarget>> GetMetagroupsAsync(CancellationToken ct = default)
    {
        var endpoint = "api/admin/workflow/metagroups/true";
        var response = await _httpClient.GetAsync(endpoint, ct);
        await EnsureSuccessOrThrowAsync(response, endpoint, ct);
        return await response.Content.ReadFromJsonAsync<List<WorkflowCommonTarget>>(JsonOptions, ct) ?? new List<WorkflowCommonTarget>();
    }

    public async Task<List<AdeptUserEntry>> GetUsersAsync(CancellationToken ct = default)
    {
        var endpoint = "api/admin/user/users";
        var response = await _httpClient.GetAsync(endpoint, ct);
        await EnsureSuccessOrThrowAsync(response, endpoint, ct);
        return await response.Content.ReadFromJsonAsync<List<AdeptUserEntry>>(JsonOptions, ct) ?? new List<AdeptUserEntry>();
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
