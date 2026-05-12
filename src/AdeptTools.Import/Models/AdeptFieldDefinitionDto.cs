namespace AdeptTools.Import.Models;

public class AdeptFieldDefinitionDto
{
    public string FieldName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string SchemaId { get; set; } = string.Empty;
    public string FieldId { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Precision { get; set; }
    public bool IsSystem { get; set; }
    public bool IsProtected { get; set; }
    public bool IsExtracted { get; set; }
    public bool IsRestricted { get; set; }
    public bool IsIndexed { get; set; }
    public string FieldType { get; set; } = string.Empty;
    public List<string> RestrictedValues { get; set; } = new();
}
