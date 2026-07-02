using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Net;
using System.Linq;
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
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            var (legacy, legacyFailure) = await TryGetLegacyWorkflowPacketAsync(ct);
            if (legacy is not null)
                return legacy;

            var resolved = response.RequestMessage?.RequestUri?.ToString() ?? endpoint;
            throw new HttpRequestException(
                $"Workflow API request failed at '{resolved}' with HTTP 404 (Not Found). " +
                "Legacy fallback routes were also unsuccessful. " +
                (string.IsNullOrWhiteSpace(legacyFailure) ? string.Empty : $"Details: {legacyFailure}"));
        }

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

    private async Task<(WorkflowAdminPacket? Packet, string? Failure)> TryGetLegacyWorkflowPacketAsync(CancellationToken ct)
    {
        // Legacy server route family (Adept 11): api/Reports/GetWorkflowItems
        // Accept empty filter arrays to request full results visible to current user.
        var endpoint = "api/Reports/GetWorkflowItems";
        var requestBody = new
        {
            S_WFID = Array.Empty<string>(),
            S_STEPID = Array.Empty<string>(),
            S_USERID = Array.Empty<string>(),
            S_DEPCODE = Array.Empty<string>(),
            S_LIBID = Array.Empty<string>(),
            S_LONGNAME = Array.Empty<string>()
        };

        var response = await _httpClient.PostAsJsonAsync(endpoint, requestBody, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            var bodySnippet = string.IsNullOrWhiteSpace(body)
                ? string.Empty
                : $" Body: {body[..Math.Min(220, body.Length)]}";

            return (null,
                $"POST {endpoint} => HTTP {(int)response.StatusCode} ({response.ReasonPhrase ?? "Unknown"}).{bodySnippet}");
        }

        var items = await response.Content.ReadFromJsonAsync<List<LegacyWorkflowReportItem>>(JsonOptions, ct)
                    ?? new List<LegacyWorkflowReportItem>();

        var grouped = items
            .Where(i => !string.IsNullOrWhiteSpace(i.WorkflowId) || !string.IsNullOrWhiteSpace(i.WorkflowName))
            .GroupBy(i => new
            {
                Id = string.IsNullOrWhiteSpace(i.WorkflowId) ? i.WorkflowName! : i.WorkflowId!,
                Name = string.IsNullOrWhiteSpace(i.WorkflowName) ? i.WorkflowId! : i.WorkflowName!
            })
            .Select(g => new WorkflowAdminItem
            {
                WorkflowId = g.Key.Id,
                WorkflowName = g.Key.Name,
                Active = true,
                StepCount = g.Select(x => x.StepId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                InProcessCount = g.Count(),
                Edit = true,
                Share = true,
                Delete = true,
                LockedByDisplayName = null
            })
            .OrderBy(w => w.WorkflowName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var packet = new WorkflowAdminPacket
        {
            CurrentUserId = string.Empty,
            WfNameLen = Math.Max(64, grouped.Count == 0 ? 0 : grouped.Max(w => w.WorkflowName?.Length ?? 0)),
            WfStepNameLen = 64,
            Workflows = grouped
        };

        return (packet, null);
    }

    private sealed class LegacyWorkflowReportItem
    {
        public string? WorkflowId { get; set; }
        public string? WorkflowName { get; set; }
        public string? StepId { get; set; }
    }
}
