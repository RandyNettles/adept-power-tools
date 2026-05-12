using AdeptTools.Import.Enums;

namespace AdeptTools.Import.Models;

public class ColumnMapping
{
    public required string ExcelColumn { get; set; }
    public required string AdeptField { get; set; }
    public MappingAction Action { get; set; }
    public SearchOperator? Operator { get; set; }
    public string? DateRangeColumn { get; set; }
    public string? FieldType { get; set; }
}
