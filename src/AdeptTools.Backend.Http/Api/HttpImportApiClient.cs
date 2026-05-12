using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AdeptTools.Core.Models;
using AdeptTools.Import.Api;
using AdeptTools.Import.Models;

namespace AdeptTools.Backend.Http.Api;

public class HttpImportApiClient : IImportApiClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public HttpImportApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<AdeptFieldDefinitionDto>> GetAvailableFieldsAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("api/Column/allavailable", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<AdeptFieldDefinitionDto>>(JsonOptions, ct)
            ?? new List<AdeptFieldDefinitionDto>();
    }

    public async Task<SearchResultDto> SearchByFieldsAsync(SearchRequestDto search, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(search, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("api/Document/ByFieldsSystem", content, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SearchResultDto>(JsonOptions, ct)
            ?? new SearchResultDto();
    }

    public async Task<Dictionary<string, string>> GetDataCardValuesAsync(
        int tableNumber, string fileId, int majRev, int minRev, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync(
            $"api/DataCard/DataCardDataForEdit/{tableNumber}/{fileId}/{majRev}/{minRev}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(JsonOptions, ct)
            ?? new Dictionary<string, string>();
    }

    public async Task<ApiResult> SaveDataCardAsync(
        int tableNumber, string fileId, int majRev, int minRev,
        Dictionary<string, string> fieldValues, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(fieldValues, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync(
            $"api/DataCard/{tableNumber}/{fileId}/{majRev}/{minRev}", content, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiResult>(JsonOptions, ct) ?? new ApiResult();
    }

    public async Task<List<string>> GetRestrictedValuesAsync(string schemaId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync(
            $"api/DataCard/GetRestrictedFieldValues/{schemaId}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<string>>(JsonOptions, ct)
            ?? new List<string>();
    }

    public async Task<CreateDocResultDto> CreateNewDocumentAsync(
        string workAreaId, string fileName, CancellationToken ct = default)
    {
        var payload = new { eWorkAreaId = workAreaId, eNewName = fileName };
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync("api/SelectionCommand/New", content, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CreateDocResultDto>(JsonOptions, ct)
            ?? new CreateDocResultDto();
    }

    public async Task<ApiResult> CheckInToLibraryAsync(
        string fileId, int majRev, int minRev, string libraryId, CancellationToken ct = default)
    {
        var payload = new { fileId, majRev, minRev, libraryId };
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync("api/SelectionCommand/CheckInItem", content, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiResult>(JsonOptions, ct) ?? new ApiResult();
    }

    public async Task<int> GetMaxFilenameLengthAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("api/SelectionCommand/MaxFilenameLength", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<int>(JsonOptions, ct);
    }

    public async Task<List<LibraryDto>> GetLibraryTreeAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("api/Library/GetLibraryTree", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<LibraryDto>>(JsonOptions, ct)
            ?? new List<LibraryDto>();
    }
}
