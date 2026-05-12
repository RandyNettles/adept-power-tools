namespace AdeptTools.Core.Auth;

public record AuthResult(bool Success, string? ErrorMessage = null, string? AccessToken = null,
    string? UserId = null, string? UserName = null, string? DisplayName = null,
    string? EmailAddress = null, string? AppVersion = null, string? WorkAreaId = null);
