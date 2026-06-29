namespace AdeptTools.Launcher.Models;

public enum ComConnectionProtocol
{
    TcpIp,
    Http,
    Https
}

public class ComConnectionProfile
{
    public string Name { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public ComConnectionProtocol Protocol { get; set; } = ComConnectionProtocol.Https;
    public int Port { get; set; } = 443;

    public string Address => Protocol switch
    {
        ComConnectionProtocol.TcpIp => $"{ServerName}:{Port}",
        ComConnectionProtocol.Http  => $"http://{ServerName}:{Port}/",
        ComConnectionProtocol.Https => $"https://{ServerName}:{Port}/",
        _ => string.Empty
    };
}
