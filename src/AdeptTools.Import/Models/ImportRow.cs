namespace AdeptTools.Import.Models;

public class ImportRow
{
    public int RowNumber { get; set; }
    public Dictionary<string, object?> Values { get; set; } = new();

    public object? GetValue(string excelColumn)
    {
        return Values.TryGetValue(excelColumn, out var value) ? value : null;
    }

    public string GetStringValue(string excelColumn)
    {
        var value = GetValue(excelColumn);
        return value?.ToString() ?? string.Empty;
    }

    public DateTime? GetDateValue(string excelColumn)
    {
        var value = GetValue(excelColumn);
        if (value is DateTime dt)
            return dt;

        var str = value?.ToString();
        if (string.IsNullOrWhiteSpace(str))
            return null;

        // Try multiple date formats
        string[] formats = ["yyyy-MM-dd", "yyyyMMdd", "M/d/yyyy", "d/M/yyyy", "MM/dd/yyyy", "dd/MM/yyyy",
                            "yyyy-MM-ddTHH:mm:ss", "M/d/yyyy h:mm:ss tt"];

        if (DateTime.TryParseExact(str, formats, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var parsed))
            return parsed;

        if (DateTime.TryParse(str, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var fallback))
            return fallback;

        return null;
    }
}
