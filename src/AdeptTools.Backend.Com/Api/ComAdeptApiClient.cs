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
    private readonly ComOperationRunner _runner;
    private readonly ComSessionManager _session;

    public ComAdeptApiClient(ComOperationRunner runner, ComSessionManager session)
    {
        _runner = runner;
        _session = session;
    }

    public async Task<UserInfo> GetUserInfoAsync(CancellationToken ct = default)
    {
        var project = _session.GetProject();

        return await _runner.RunAsync(() => new UserInfo
        {
            UserId = project.UserId,
            UserName = project.UserName,
            DisplayName = project.DisplayName,
            EmailAddress = project.EmailAddress,
            AppVersion = project.AppVersion,
            WorkAreaId = project.WorkAreaId
        }, ct);
    }

    public Task<bool> IsLoggedInAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_session.IsConnected);
    }

    public async Task<string> GetServerVersionAsync(CancellationToken ct = default)
    {
        var project = _session.GetProject();
        return await _runner.RunAsync(() => project.AppVersion, ct);
    }
}
