using AdeptTools.Import.Enums;

namespace AdeptTools.Import.Models;

public class ImportProgress
{
    public int RowNumber { get; set; }
    public int TotalRows { get; set; }
    public string? CurrentPrimaryKey { get; set; }
    public ImportPhase Phase { get; set; }
    public ImportOutcome? Outcome { get; set; }
    public string? Message { get; set; }
}
