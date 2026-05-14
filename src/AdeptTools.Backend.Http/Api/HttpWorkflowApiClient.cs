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
        var response = await _httpClient.GetAsync("api/admin/workflow/setup", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkflowSetup>(JsonOptions, ct) ?? new WorkflowSetup();
    }

    public async Task<WorkflowAdminPacket> GetWorkflowsAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("api/admin/workflow/workflows", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkflowAdminPacket>(JsonOptions, ct) ?? new WorkflowAdminPacket();
    }

    public async Task<WorkflowAdminPacket> GetWorkflowsBasicAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("api/admin/workflow/workflows/basic", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkflowAdminPacket>(JsonOptions, ct) ?? new WorkflowAdminPacket();
    }

    public async Task<WorkflowEditModel> CreateNewAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("api/admin/workflow/create-new", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkflowEditModel>(JsonOptions, ct) ?? new WorkflowEditModel();
    }

    public async Task<WorkflowEditModel> GetWorkflowAsync(string workflowId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"api/admin/workflow/workflow/{workflowId}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkflowEditModel>(JsonOptions, ct) ?? new WorkflowEditModel();
    }

    public async Task<ApiResult> SaveWorkflowAsync(WorkflowEditModel model, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(model, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("api/admin/workflow/workflow", content, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiResult>(JsonOptions, ct) ?? new ApiResult();
    }

    public async Task<ApiResult> DeleteWorkflowAsync(string workflowId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/admin/workflow/workflow/{workflowId}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiResult>(JsonOptions, ct) ?? new ApiResult();
    }

    public async Task<WorkflowEditModel> AddStepAsync(WorkflowEditModel model, int position, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(model, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"api/admin/workflow/steps/add/{position}", content, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkflowEditModel>(JsonOptions, ct) ?? new WorkflowEditModel();
    }

    public async Task<WorkflowEditModel> TagAsync(string workflowId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"api/admin/workflow/tag/{workflowId}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkflowEditModel>(JsonOptions, ct) ?? new WorkflowEditModel();
    }

    public async Task<ApiResult> UntagAsync(string workflowId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"api/admin/workflow/untag/{workflowId}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiResult>(JsonOptions, ct) ?? new ApiResult();
    }

    public async Task<List<WorkflowCommonTarget>> GetMetagroupsAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("api/admin/workflow/metagroups/true", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<WorkflowCommonTarget>>(JsonOptions, ct) ?? new List<WorkflowCommonTarget>();
    }

    public async Task<List<AdeptUserEntry>> GetUsersAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("api/admin/user/users", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<AdeptUserEntry>>(JsonOptions, ct) ?? new List<AdeptUserEntry>();
    }
}
