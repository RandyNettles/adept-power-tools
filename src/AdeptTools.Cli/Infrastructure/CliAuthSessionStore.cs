using System.Text;
using System.Text.Json;

namespace AdeptTools.Cli.Infrastructure;

public sealed class CliAuthSessionStore
{
    private const string FileName = "cli-http-auth-session.dat";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly string _filePath;

    public CliAuthSessionStore()
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AdeptTools");
        Directory.CreateDirectory(baseDir);
        _filePath = Path.Combine(baseDir, FileName);
    }

    public void Save(CliAuthSessionState state)
    {
        var json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(_filePath, json, Encoding.UTF8);
    }

    public CliAuthSessionState? Load()
    {
        if (!File.Exists(_filePath))
            return null;

        try
        {
            var json = File.ReadAllText(_filePath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            return JsonSerializer.Deserialize<CliAuthSessionState>(json, JsonOptions);
        }
        catch
        {
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

public sealed class CliAuthSessionState
{
    public string ServerUrl { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public DateTimeOffset? AccessTokenExpiresUtc { get; set; }
    public string? UserName { get; set; }
}