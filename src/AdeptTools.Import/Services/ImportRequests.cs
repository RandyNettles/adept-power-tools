using AdeptTools.Import.Models;

namespace AdeptTools.Import.Services;

public class ImportRunRequest
{
    public required string ExcelPath { get; init; }
    public string? ConfigPath { get; init; }
    public bool DryRun { get; init; }
    public string? LogPath { get; init; }
}

public class ImportValidateRequest
{
    public required string ExcelPath { get; init; }
    public string? ConfigPath { get; init; }
}
