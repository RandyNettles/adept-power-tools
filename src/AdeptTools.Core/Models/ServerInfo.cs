namespace AdeptTools.Core.Models;

public class ServerInfo
{
    public string? ServerUrl { get; set; }
    public string? AppVersion { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? DisplayName { get; set; }
    public bool IsConnected { get; set; }
}
