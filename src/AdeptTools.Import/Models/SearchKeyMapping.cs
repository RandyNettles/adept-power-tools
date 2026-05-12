using AdeptTools.Import.Enums;

namespace AdeptTools.Import.Models;

public class SearchKeyMapping
{
    public required string ExcelColumn { get; set; }
    public required string AdeptFieldName { get; set; }
    public required string SchemaId { get; set; }
    public SearchOperator Operator { get; set; }
    public string? DateRangeColumn { get; set; }
    public bool IsDate { get; set; }
}
