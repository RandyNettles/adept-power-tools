namespace AdeptTools.Core.Configuration;

using AdeptTools.Core.Models;

public class AdeptToolSettings
{
    public string? ServerUrl { get; set; }
    public string? UserName { get; set; }
    public bool MockMode { get; set; }
    public BackendType Backend { get; set; } = BackendType.Http;
    public bool Verbose { get; set; }
    public string? LogPath { get; set; }
}
