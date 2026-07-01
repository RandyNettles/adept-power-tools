using AdeptTools.Backend.Com.Infrastructure;
using AdeptTools.Core.Api;
using AdeptTools.Core.Models;

namespace AdeptTools.Backend.Com.Api;

/// <summary>
/// COM-based implementation of IAdeptApiClient.
/// Wraps NxProject for server info, version, and connection status.
/// </summary>
public class ComAdeptApiClient : IAdeptApiClient
{
    private readonly ILegacyCoreApiSession _legacySession;

    public ComAdeptApiClient(ILegacyCoreApiSession legacySession)
    {
        _legacySession = legacySession;
    }

    public async Task<UserInfo> GetUserInfoAsync(CancellationToken ct = default)
    {
        var info = await _legacySession.GetSessionInfoAsync(ct);

        return new UserInfo
        {
            UserId = info.UserId,
            UserName = info.UserName,
            DisplayName = info.DisplayName,
            EmailAddress = info.EmailAddress,
            AppVersion = info.AppVersion,
            WorkAreaId = info.WorkAreaId
        };
    }

    public Task<bool> IsLoggedInAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_legacySession.IsConnected);
    }

    public async Task<string> GetServerVersionAsync(CancellationToken ct = default)
    {
        var info = await _legacySession.GetSessionInfoAsync(ct);
        return info.AppVersion ?? "LegacyCoreApi";
    }
}
