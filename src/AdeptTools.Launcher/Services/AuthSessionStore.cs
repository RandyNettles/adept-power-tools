using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AdeptTools.Launcher.Services;

public sealed class AuthSessionStore
{
    private const string FileName = "http-auth-session.dat";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("AdeptTools.AuthSession.v1");
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly string _filePath;

    public AuthSessionStore()
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AdeptTools");
        Directory.CreateDirectory(baseDir);
        _filePath = Path.Combine(baseDir, FileName);
    }

    public void Save(HttpAuthSessionState state)
    {
        var json = JsonSerializer.Serialize(state, JsonOptions);
        var plain = Encoding.UTF8.GetBytes(json);
        var encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_filePath, encrypted);
    }

    public HttpAuthSessionState? Load()
    {
        if (!File.Exists(_filePath))
            return null;

        try
        {
            var encrypted = File.ReadAllBytes(_filePath);
            if (encrypted.Length == 0)
                return null;

            var plain = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(plain);
            return JsonSerializer.Deserialize<HttpAuthSessionState>(json, JsonOptions);
        }
        catch
        {
            // Corrupt or unreadable state should not block startup.
            return null;
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_filePath))
                File.Delete(_filePath);
        }
        catch
        {
            // Best effort cleanup.
        }
    }
}

public sealed class HttpAuthSessionState
{
    public string ServerUrl { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public DateTimeOffset? AccessTokenExpiresUtc { get; set; }

    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? DisplayName { get; set; }
    public string? EmailAddress { get; set; }
    public string? AppVersion { get; set; }
    public string? WorkAreaId { get; set; }
}