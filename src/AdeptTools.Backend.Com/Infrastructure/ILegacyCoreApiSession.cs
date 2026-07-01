namespace AdeptTools.Backend.Com.Infrastructure;

public sealed class LegacySessionInfo
{
    public string? UserId { get; init; }
    public string? UserName { get; init; }
    public string? DisplayName { get; init; }
    public string? EmailAddress { get; init; }
    public string? AppVersion { get; init; }
    public string? WorkAreaId { get; init; }
    public string? ActiveDomainName { get; init; }
    public string? ActiveDatabaseName { get; init; }
}

public interface ILegacyCoreApiSession
{
    bool IsConnected { get; }

    Task<int> ConnectAsync(string serverUrl, string userName, string? password, CancellationToken ct = default);

    Task<LegacySessionInfo> GetSessionInfoAsync(CancellationToken ct = default);

    Task<object> GetConnectedDispatchAsync(CancellationToken ct = default);

    Task DisconnectAsync(CancellationToken ct = default);
}
