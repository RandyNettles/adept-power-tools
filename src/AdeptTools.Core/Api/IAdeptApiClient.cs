using AdeptTools.Core.Models;

namespace AdeptTools.Core.Api;

public interface IAdeptApiClient
{
    Task<UserInfo> GetUserInfoAsync(CancellationToken ct = default);
    Task<bool> IsLoggedInAsync(CancellationToken ct = default);
    Task<string> GetServerVersionAsync(CancellationToken ct = default);
}
