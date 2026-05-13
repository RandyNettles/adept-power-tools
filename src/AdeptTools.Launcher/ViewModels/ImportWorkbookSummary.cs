namespace AdeptTools.Launcher.ViewModels;

public class ImportWorkbookSummary
{
    public string ImportModeDisplay { get; init; } = string.Empty;
    public bool AddIfNotFound { get; init; }
    public string? WorkArea { get; init; }
    public int DataRowCount { get; init; }
    public int SearchKeyCount { get; init; }
    public int FillFieldCount { get; init; }
    public int SkippedCount { get; init; }
    public string SearchKeyNames { get; init; } = string.Empty;
    public string FillFieldNames { get; init; } = string.Empty;
}
