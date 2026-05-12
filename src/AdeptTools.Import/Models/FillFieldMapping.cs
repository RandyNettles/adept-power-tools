using AdeptTools.Import.Enums;

namespace AdeptTools.Import.Models;

public class FillFieldMapping
{
    public required string ExcelColumn { get; set; }
    public required string AdeptFieldName { get; set; }
    public required string SchemaId { get; set; }
    public FillMode Mode { get; set; }
    public bool IsDate { get; set; }
}
