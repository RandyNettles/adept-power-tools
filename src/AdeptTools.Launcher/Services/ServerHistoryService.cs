using System.IO;
using System.Text.Json;

namespace AdeptTools.Launcher.Services;

public class ServerHistoryService
{
    private const int MaxEntries = 10;
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AdeptTools", "server-history.json");

    private List<string> _entries = new();

    public IReadOnlyList<string> Entries => _entries;
    public string? LastUserName { get; private set; }

    public void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var json = File.ReadAllText(SettingsPath);
            var loaded = JsonSerializer.Deserialize<ServerHistoryData>(json);
            if (loaded is not null)
            {
                _entries = loaded.ServerUrls ?? new List<string>();
                LastUserName = loaded.LastUserName;
            }
            else
            {
                // Legacy: plain string array
                var urls = JsonSerializer.Deserialize<List<string>>(json);
                if (urls is not null) _entries = urls;
            }
        }
        catch
        {
            _entries = new List<string>();
        }
    }

    public void Add(string serverUrl, string? userName = null)
    {
        if (string.IsNullOrWhiteSpace(serverUrl)) return;

        var normalized = serverUrl.Trim();
        _entries.RemoveAll(e => string.Equals(e, normalized, StringComparison.OrdinalIgnoreCase));
        _entries.Insert(0, normalized);

        if (_entries.Count > MaxEntries)
            _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);

        if (!string.IsNullOrWhiteSpace(userName))
            LastUserName = userName.Trim();

        Save();
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(dir);
            var data = new ServerHistoryData { ServerUrls = _entries, LastUserName = LastUserName };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Best-effort persistence
        }
    }
}

internal class ServerHistoryData
{
    public List<string>? ServerUrls { get; set; }
    public string? LastUserName { get; set; }
}
