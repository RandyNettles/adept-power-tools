namespace AdeptTools.Import.Models;

public class SearchTermDto
{
    public string SchemaId { get; set; } = string.Empty;
    public string? ValueStr { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string SearchOp { get; set; } = string.Empty;
    public string? FieldName { get; set; }
}

public class SearchRequestDto
{
    public List<SearchTermDto> SearchCriteria { get; set; } = new();
    public int TableNumber { get; set; } = 1; // C_FILES = 1
}

public class SearchResultDto
{
    public int MatchCount { get; set; }
    public List<SearchResultRow> Rows { get; set; } = new();
}

public class SearchResultRow
{
    public int TableNumber { get; set; }
    public string FileId { get; set; } = string.Empty;
    public int MajRev { get; set; }
    public int MinRev { get; set; }
    public Dictionary<string, string> FieldValues { get; set; } = new();
}
