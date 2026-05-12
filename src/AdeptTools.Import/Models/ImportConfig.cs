using AdeptTools.Import.Enums;

namespace AdeptTools.Import.Models;

public class ImportConfig
{
    public string? ServerUrl { get; set; }
    public ImportMode ImportMode { get; set; } = ImportMode.UpdateDataCard;
    public bool AddIfNotFound { get; set; }
    public string? WorkAreaId { get; set; }
    public bool SkipHiddenRows { get; set; }
    public int HeaderRows { get; set; } = 1;
    public bool DryRun { get; set; }
}
