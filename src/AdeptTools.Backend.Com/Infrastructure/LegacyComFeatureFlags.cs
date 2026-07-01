namespace AdeptTools.Backend.Com.Infrastructure;

/// <summary>
/// Feature flags for staged legacy COM migration.
/// Flags are read from environment variables and default to enabled.
/// </summary>
public sealed class LegacyComFeatureFlags
{
    public bool EnableLegacyAuth { get; }
    public bool EnableLegacyWorkflow { get; }
    public bool EnableLegacyImport { get; }

    public LegacyComFeatureFlags()
    {
        EnableLegacyAuth = ReadBool("ADEPTTOOLS_LEGACYCOM_AUTH", defaultValue: true);
        EnableLegacyWorkflow = ReadBool("ADEPTTOOLS_LEGACYCOM_WORKFLOW", defaultValue: true);
        EnableLegacyImport = ReadBool("ADEPTTOOLS_LEGACYCOM_IMPORT", defaultValue: true);
    }

    private static bool ReadBool(string name, bool defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;

        return value.Trim().ToLowerInvariant() switch
        {
            "1" => true,
            "true" => true,
            "yes" => true,
            "on" => true,
            "0" => false,
            "false" => false,
            "no" => false,
            "off" => false,
            _ => defaultValue
        };
    }
}
