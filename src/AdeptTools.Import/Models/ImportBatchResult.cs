namespace AdeptTools.Import.Models;

public class ImportBatchResult
{
    public int TotalRows { get; set; }
    public int Updated { get; set; }
    public int Created { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public bool DryRun { get; set; }
    public List<ImportRowResult> Results { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public string? LogFilePath { get; set; }
}
