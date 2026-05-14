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

    public void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var json = File.ReadAllText(SettingsPath);
            var loaded = JsonSerializer.Deserialize<List<string>>(json);
            if (loaded is not null)
                _entries = loaded;
        }
        catch
        {
            // Corrupt file — start fresh
            _entries = new List<string>();
        }
    }

    public void Add(string serverUrl)
    {
        if (string.IsNullOrWhiteSpace(serverUrl)) return;

        var normalized = serverUrl.Trim();
        _entries.RemoveAll(e => string.Equals(e, normalized, StringComparison.OrdinalIgnoreCase));
        _entries.Insert(0, normalized);

        if (_entries.Count > MaxEntries)
            _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);

        Save();
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Best-effort persistence
        }
    }
}
