using AdeptTools.Core.Models;
using AdeptTools.Import.Models;

namespace AdeptTools.Import.Api;

public interface IImportApiClient
{
    Task<List<AdeptFieldDefinitionDto>> GetAvailableFieldsAsync(CancellationToken ct = default);
    Task<SearchResultDto> SearchByFieldsAsync(SearchRequestDto search, CancellationToken ct = default);
    Task<Dictionary<string, string>> GetDataCardValuesAsync(int tableNumber, string fileId, int majRev, int minRev, CancellationToken ct = default);
    Task<ApiResult> SaveDataCardAsync(int tableNumber, string fileId, int majRev, int minRev, Dictionary<string, string> fieldValues, CancellationToken ct = default);
    Task<List<string>> GetRestrictedValuesAsync(string schemaId, CancellationToken ct = default);
    Task<CreateDocResultDto> CreateNewDocumentAsync(string workAreaId, string fileName, CancellationToken ct = default);
    Task<ApiResult> CheckInToLibraryAsync(string fileId, int majRev, int minRev, string libraryId, CancellationToken ct = default);
    Task<int> GetMaxFilenameLengthAsync(CancellationToken ct = default);
    Task<List<LibraryDto>> GetLibraryTreeAsync(CancellationToken ct = default);
}
