namespace AdeptTools.Workflow.Input;

public static class TrusteeParser
{
    public static List<string> Split(string cellValue)
    {
        if (string.IsNullOrWhiteSpace(cellValue))
            return new List<string>();

        return cellValue
            .Split(',')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
    }
}
