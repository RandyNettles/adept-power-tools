using AdeptTools.Import.Enums;

namespace AdeptTools.Import.Models;

public class ImportRowResult
{
    public int RowNumber { get; set; }
    public string? PrimaryKeyDisplay { get; set; }
    public ImportOutcome Outcome { get; set; }
    public string? Message { get; set; }
    public int FieldsUpdated { get; set; }
}
