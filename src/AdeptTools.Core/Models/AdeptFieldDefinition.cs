namespace AdeptTools.Core.Models;

public class AdeptFieldDefinition
{
    public string? FieldName { get; set; }
    public string? DisplayName { get; set; }
    public string? DataType { get; set; }
    public int? MaxLength { get; set; }
    public bool IsRequired { get; set; }
    public bool IsReadOnly { get; set; }
}
