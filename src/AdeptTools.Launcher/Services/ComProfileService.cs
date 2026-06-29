using System.IO;
using System.Text.Json;
using AdeptTools.Launcher.Models;

namespace AdeptTools.Launcher.Services;

public class ComProfileService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AdeptTools", "com-profiles.json");

    private List<ComConnectionProfile> _profiles = new();

    public IReadOnlyList<ComConnectionProfile> Profiles => _profiles;

    public void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var json = File.ReadAllText(SettingsPath);
            var loaded = JsonSerializer.Deserialize<List<ComConnectionProfile>>(json);
            if (loaded is not null)
                _profiles = loaded;
        }
        catch
        {
            _profiles = new List<ComConnectionProfile>();
        }
    }

    public void Save(IEnumerable<ComConnectionProfile> profiles)
    {
        _profiles = profiles.ToList();
        Persist();
    }

    public void Add(ComConnectionProfile profile)
    {
        _profiles.Add(profile);
        Persist();
    }

    public void Update(ComConnectionProfile original, ComConnectionProfile updated)
    {
        var index = _profiles.IndexOf(original);
        if (index >= 0)
            _profiles[index] = updated;
        Persist();
    }

    public void Remove(ComConnectionProfile profile)
    {
        _profiles.Remove(profile);
        Persist();
    }

    private void Persist()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(_profiles, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }
}
