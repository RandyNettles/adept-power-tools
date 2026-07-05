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
    public WorkflowApiCapabilities Capabilities { get; } = WorkflowApiCapabilities.Full;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

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

    public async Task<ApiResult> SetWorkflowSharedAsync(string workflowId, bool shared, CancellationToken ct = default)
    {
        var workflows = await GetWorkflowsAsync(ct);
        var item = workflows.Workflows.FirstOrDefault(w =>
            string.Equals(w.WorkflowId, workflowId, StringComparison.OrdinalIgnoreCase));

        if (item is null)
        {
            return ApiResult.Failure(-1, $"Workflow '{workflowId}' was not found while applying share state.");
        }

        if (string.IsNullOrWhiteSpace(item.ContainerId))
        {
            return ApiResult.Failure(-1, $"Workflow '{workflowId}' has no containerId; cannot apply share state.");
        }

        if (shared)
        {
            var getShareEndpoint = $"api/shares/share/9/{item.ContainerId}";
            var getShareResponse = await GetAsyncWithAuthRefreshAsync(getShareEndpoint, ct);
            await EnsureSuccessOrThrowAsync(getShareResponse, getShareEndpoint, ct);

            var packet = await getShareResponse.Content.ReadFromJsonAsync<WorkflowSharePacket>(JsonOptions, ct)
                ?? new WorkflowSharePacket();
            packet.BIsGlobal = true;
            packet.ShareModelList ??= new List<WorkflowShareModel>();

            var setShareEndpoint = $"api/shares/share/9/{item.ContainerId}";
            var payload = JsonSerializer.Serialize(packet, JsonOptions);
            var setShareResponse = await PostJsonWithAuthRefreshAsync(setShareEndpoint, payload, ct);
            await EnsureSuccessOrThrowAsync(setShareResponse, setShareEndpoint, ct);
            return await setShareResponse.Content.ReadFromJsonAsync<ApiResult>(JsonOptions, ct) ?? ApiResult.Success();
        }
        else
        {
            var unshareEndpoint = $"api/shares/unshare/9/{item.ContainerId}";
            var unshareResponse = await PostJsonWithAuthRefreshAsync(unshareEndpoint, "null", ct);
            await EnsureSuccessOrThrowAsync(unshareResponse, unshareEndpoint, ct);
            return await unshareResponse.Content.ReadFromJsonAsync<ApiResult>(JsonOptions, ct) ?? ApiResult.Success();
        }
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
            var mappedUsers = await ReadPrimaryUsersAsync(primaryResponse.Content, ct);
            var mergedUsers = await MergeWithLegacyUsersAsync(mappedUsers, ct);

            if (mergedUsers.Count > 0)
            {
                return mergedUsers;
            }

            if (mappedUsers.Count > 0)
            {
                return mappedUsers;
            }
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

    public async Task<AdeptUserEntry?> GetUserByIdAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        var endpoint = $"api/user/{Uri.EscapeDataString(userId.Trim())}";

        try
        {
            var response = await GetAsyncWithAuthRefreshAsync(endpoint, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
                return null;

            await EnsureSuccessOrThrowAsync(response, endpoint, ct);

            var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
            if (payload.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                return null;

            var id = FirstNonEmpty(
                GetStringProperty(payload, "loginName"),
                GetStringProperty(payload, "userName"),
                GetStringProperty(payload, "id"));

            if (string.IsNullOrWhiteSpace(id))
                return null;

            var displayName = FirstNonEmpty(
                GetStringProperty(payload, "userName"),
                GetStringProperty(payload, "displayName"),
                id);

            return new AdeptUserEntry
            {
                UserId = id,
                DisplayName = displayName,
                NotificationTargetId = FirstNonEmpty(
                    GetStringProperty(payload, "id"),
                    GetStringProperty(payload, "userId"),
                    GetStringProperty(payload, "loginName"),
                    id)
            };
        }
        catch (JsonException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    public async Task<List<AdeptGroupEntry>> GetGroupsAsync(CancellationToken ct = default)
    {
        var endpoint = "api/group/groups";

        try
        {
            var response = await GetAsyncWithAuthRefreshAsync(endpoint, ct);
            if (!response.IsSuccessStatusCode)
                return new List<AdeptGroupEntry>();

            var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
            var groups = new Dictionary<string, AdeptGroupEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in EnumerateGroupItems(payload))
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var id = FirstNonEmpty(
                    GetStringProperty(item, "groupId"),
                    GetStringProperty(item, "adeptGroupId"),
                    GetStringProperty(item, "id"),
                    GetStringProperty(item, "groupName"),
                    GetStringProperty(item, "name"));

                var name = FirstNonEmpty(
                    GetStringProperty(item, "name"),
                    GetStringProperty(item, "groupName"),
                    GetStringProperty(item, "displayName"),
                    id);

                if (!string.IsNullOrWhiteSpace(id))
                {
                    var key = id.Trim();
                    if (!groups.ContainsKey(key))
                    {
                        groups[key] = new AdeptGroupEntry
                        {
                            GroupId = key,
                            Name = name
                        };
                    }
                }
            }

            return groups.Values.ToList();
        }
        catch (HttpRequestException)
        {
            return new List<AdeptGroupEntry>();
        }
        catch (JsonException)
        {
            return new List<AdeptGroupEntry>();
        }
    }

    private async Task<List<AdeptUserEntry>> MergeWithLegacyUsersAsync(
        List<AdeptUserEntry> primaryUsers,
        CancellationToken ct)
    {
        var legacyEndpoint = "api/Account/UserInfo/EmailList";
        try
        {
            var legacyResponse = await GetAsyncWithAuthRefreshAsync(legacyEndpoint, ct);
            if (!legacyResponse.IsSuccessStatusCode)
            {
                return primaryUsers;
            }

            var legacyUsers = await legacyResponse.Content.ReadFromJsonAsync<List<LegacyUserInfo>>(JsonOptions, ct)
                ?? new List<LegacyUserInfo>();

            if (legacyUsers.Count == 0)
            {
                return primaryUsers;
            }

            var merged = new Dictionary<string, AdeptUserEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (var user in primaryUsers)
            {
                if (string.IsNullOrWhiteSpace(user.UserId))
                    continue;

                merged[user.UserId] = new AdeptUserEntry
                {
                    UserId = user.UserId,
                    DisplayName = user.DisplayName,
                    NotificationTargetId = user.NotificationTargetId
                };
            }

            foreach (var legacy in legacyUsers)
            {
                if (string.IsNullOrWhiteSpace(legacy.LoginName))
                    continue;

                var userId = legacy.LoginName.Trim();
                var displayName = string.IsNullOrWhiteSpace(legacy.UserName)
                    ? userId
                    : legacy.UserName;

                merged[userId] = new AdeptUserEntry
                {
                    UserId = userId,
                    DisplayName = displayName,
                    NotificationTargetId = userId
                };
            }

            return merged.Values.ToList();
        }
        catch
        {
            // Best-effort merge only; keep primary users if legacy route is unavailable.
            return primaryUsers;
        }
    }

    private async Task<List<AdeptUserEntry>> ReadPrimaryUsersAsync(HttpContent content, CancellationToken ct)
    {
        var payload = await content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
        if (payload.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return new List<AdeptUserEntry>();

        var users = new Dictionary<string, AdeptUserEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in EnumerateUserItems(payload))
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var userId = FirstNonEmpty(
                GetStringProperty(item, "loginId"),
                GetStringProperty(item, "loginName"),
                GetStringProperty(item, "userName"),
                GetStringProperty(item, "userId"),
                GetStringProperty(item, "id"));

            if (string.IsNullOrWhiteSpace(userId))
                continue;

            var displayName = FirstNonEmpty(
                GetStringProperty(item, "displayName"),
                GetStringProperty(item, "fullName"),
                GetStringProperty(item, "name"),
                GetStringProperty(item, "userName"),
                userId);

            users[userId] = new AdeptUserEntry
            {
                UserId = userId,
                DisplayName = displayName,
                NotificationTargetId = FirstNonEmpty(
                    GetStringProperty(item, "id"),
                    GetStringProperty(item, "userId"),
                    GetStringProperty(item, "loginName"),
                    GetStringProperty(item, "loginId"),
                    userId)
            };
        }

        return users.Values.ToList();
    }

    private static IEnumerable<JsonElement> EnumerateUserItems(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in payload.EnumerateArray())
                yield return item;

            yield break;
        }

        if (payload.ValueKind != JsonValueKind.Object)
            yield break;

        foreach (var key in new[] { "users", "items", "data", "results" })
        {
            if (TryGetPropertyIgnoreCase(payload, key, out var container) &&
                container.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in container.EnumerateArray())
                    yield return item;

                yield break;
            }
        }

        // Some installations return a single user object.
        yield return payload;
    }

    private static IEnumerable<JsonElement> EnumerateGroupItems(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in payload.EnumerateArray())
                yield return item;

            yield break;
        }

        if (payload.ValueKind != JsonValueKind.Object)
            yield break;

        foreach (var key in new[] { "groups", "items", "data", "results" })
        {
            if (TryGetPropertyIgnoreCase(payload, key, out var container) &&
                container.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in container.EnumerateArray())
                    yield return item;

                yield break;
            }
        }

        // Some installations return a single group object.
        yield return payload;
    }

    private static string GetStringProperty(JsonElement item, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(item, propertyName, out var value))
            return string.Empty;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => string.Empty
        };
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return string.Empty;
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

    private sealed class WorkflowSharePacket
    {
        public bool BIsGlobal { get; set; }
        public List<WorkflowShareModel>? ShareModelList { get; set; }
    }

    private sealed class WorkflowShareModel
    {
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
