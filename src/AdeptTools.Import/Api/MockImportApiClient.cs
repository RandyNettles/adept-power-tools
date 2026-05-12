using AdeptTools.Core.Models;
using AdeptTools.Import.Models;

namespace AdeptTools.Import.Api;

public enum MockImportMode
{
    Default,
    NoMatch,
    MultiMatch,
    Mixed
}

public class MockImportApiClient : IImportApiClient
{
    private readonly MockImportMode _mode;
    private int _searchCallCount;

    public List<Dictionary<string, string>> SavedDataCards { get; } = new();
    public List<(string WorkAreaId, string FileName)> CreatedDocuments { get; } = new();
    public List<(string FileId, string LibraryId)> CheckedInDocuments { get; } = new();
    public int SearchCallCount => _searchCallCount;

    public MockImportApiClient(MockImportMode mode = MockImportMode.Default)
    {
        _mode = mode;
    }

    public virtual Task<List<AdeptFieldDefinitionDto>> GetAvailableFieldsAsync(CancellationToken ct = default)
    {
        var fields = new List<AdeptFieldDefinitionDto>
        {
            new() { FieldName = "S_LONGNAME", DisplayName = "File Name", SchemaId = "S1", FieldType = "FDT_STRING", Width = 255, IsSystem = true },
            new() { FieldName = "S_REVDATE", DisplayName = "Revision Date", SchemaId = "S2", FieldType = "FDT_DATE", Width = 10, IsSystem = true },
            new() { FieldName = "S_LIBNAME", DisplayName = "Library Name", SchemaId = "S3", FieldType = "FDT_STRING", Width = 64, IsSystem = true },
            new() { FieldName = "S_LIBPATH", DisplayName = "Library Path", SchemaId = "S4", FieldType = "FDT_STRING", Width = 255, IsSystem = true },
            new() { FieldName = "S_LIBID", DisplayName = "Library Id", SchemaId = "S5", FieldType = "FDT_STRING", Width = 10, IsSystem = true },
            new() { FieldName = "DESCRIPTION", DisplayName = "Description", SchemaId = "U1", FieldType = "FDT_STRING", Width = 128, IsSystem = false },
            new() { FieldName = "PROJECT", DisplayName = "Project", SchemaId = "U2", FieldType = "FDT_STRING", Width = 64, IsSystem = false },
            new() { FieldName = "DISCIPLINE", DisplayName = "Discipline", SchemaId = "U3", FieldType = "FDT_STRING", Width = 32, IsSystem = false, IsRestricted = true,
                     RestrictedValues = new List<string> { "Piping", "Electrical", "Civil", "Structural", "Mechanical" } },
            new() { FieldName = "STATUS", DisplayName = "Status", SchemaId = "U4", FieldType = "FDT_STRING", Width = 32, IsSystem = false },
            new() { FieldName = "DWG_NUMBER", DisplayName = "Drawing Number", SchemaId = "U5", FieldType = "FDT_STRING", Width = 50, IsSystem = false },
            new() { FieldName = "REV_DATE", DisplayName = "Revision Date", SchemaId = "U6", FieldType = "FDT_DATE", Width = 10, IsSystem = false },
            new() { FieldName = "QUANTITY", DisplayName = "Quantity", SchemaId = "U7", FieldType = "FDT_INTEGER", Width = 10, IsSystem = false },
        };

        return Task.FromResult(fields);
    }

    public virtual Task<SearchResultDto> SearchByFieldsAsync(SearchRequestDto search, CancellationToken ct = default)
    {
        var callNumber = Interlocked.Increment(ref _searchCallCount);

        var matchCount = _mode switch
        {
            MockImportMode.Default => 1,
            MockImportMode.NoMatch => 0,
            MockImportMode.MultiMatch => 3,
            MockImportMode.Mixed => (callNumber % 3) switch
            {
                1 => 1,
                2 => 0,
                _ => 3
            },
            _ => 1
        };

        var result = new SearchResultDto { MatchCount = matchCount };

        for (var i = 0; i < matchCount; i++)
        {
            result.Rows.Add(new SearchResultRow
            {
                TableNumber = 1,
                FileId = $"MOCK-FILE-{callNumber}-{i}",
                MajRev = 1,
                MinRev = 0,
                FieldValues = new Dictionary<string, string>
                {
                    ["S_LONGNAME"] = $"MockDoc-{callNumber}.dwg",
                    ["DESCRIPTION"] = $"Mock document {callNumber}"
                }
            });
        }

        return Task.FromResult(result);
    }

    public virtual Task<Dictionary<string, string>> GetDataCardValuesAsync(
        int tableNumber, string fileId, int majRev, int minRev, CancellationToken ct = default)
    {
        // Return some empty fields to allow Fill: If Empty to work
        return Task.FromResult(new Dictionary<string, string>
        {
            ["S_LONGNAME"] = $"{fileId}.dwg",
            ["DESCRIPTION"] = ""
        });
    }

    public virtual Task<ApiResult> SaveDataCardAsync(
        int tableNumber, string fileId, int majRev, int minRev,
        Dictionary<string, string> fieldValues, CancellationToken ct = default)
    {
        SavedDataCards.Add(new Dictionary<string, string>(fieldValues));
        return Task.FromResult(ApiResult.Success($"Saved {fieldValues.Count} fields"));
    }

    public virtual Task<List<string>> GetRestrictedValuesAsync(string schemaId, CancellationToken ct = default)
    {
        return Task.FromResult(new List<string> { "Piping", "Electrical", "Civil", "Structural", "Mechanical" });
    }

    public virtual Task<CreateDocResultDto> CreateNewDocumentAsync(
        string workAreaId, string fileName, CancellationToken ct = default)
    {
        CreatedDocuments.Add((workAreaId, fileName));
        return Task.FromResult(new CreateDocResultDto
        {
            StatusCode = 0,
            TableNumber = 1,
            FileId = $"NEW-{Guid.NewGuid():N}",
            MajRev = 1,
            MinRev = 0
        });
    }

    public virtual Task<ApiResult> CheckInToLibraryAsync(
        string fileId, int majRev, int minRev, string libraryId, CancellationToken ct = default)
    {
        CheckedInDocuments.Add((fileId, libraryId));
        return Task.FromResult(ApiResult.Success("Checked in"));
    }

    public virtual Task<int> GetMaxFilenameLengthAsync(CancellationToken ct = default)
    {
        return Task.FromResult(255);
    }

    public virtual Task<List<LibraryDto>> GetLibraryTreeAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new List<LibraryDto>
        {
            new()
            {
                LibraryId = "LIB-001", Name = "Piping", Path = @"Piping",
                Children = new List<LibraryDto>
                {
                    new() { LibraryId = "LIB-002", Name = "P&ID", Path = @"Piping\P&ID" },
                    new() { LibraryId = "LIB-003", Name = "Isometrics", Path = @"Piping\Isometrics" }
                }
            },
            new() { LibraryId = "LIB-004", Name = "Electrical", Path = @"Electrical" },
            new() { LibraryId = "LIB-005", Name = "Civil", Path = @"Civil" },
        });
    }
}
