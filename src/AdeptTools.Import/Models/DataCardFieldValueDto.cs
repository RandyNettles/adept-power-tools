namespace AdeptTools.Import.Models;

public class DataCardFieldValueDto
{
    public int TableNumber { get; set; }
    public string FileId { get; set; } = string.Empty;
    public int MajRev { get; set; }
    public int MinRev { get; set; }
    public Dictionary<string, string> FieldValues { get; set; } = new();
}
