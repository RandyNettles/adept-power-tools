using AdeptTools.Import.Enums;

namespace AdeptTools.Launcher.ViewModels;

public class MappingRowItem
{
    public string ExcelColumn { get; init; } = string.Empty;
    public string AdeptField { get; init; } = string.Empty;
    public string ActionDisplay { get; init; } = string.Empty;
    public string OperatorDisplay { get; init; } = string.Empty;
    public string? DateRangeColumn { get; init; }
    public string? FieldType { get; init; }
    public string? PreviewValue { get; init; }
    public MappingAction Action { get; init; }
    public int SortOrder => Action switch
    {
        MappingAction.SearchKey => 0,
        MappingAction.FillOverwrite => 1,
        MappingAction.FillIfEmpty => 1,
        MappingAction.DoNotImport => 2,
        _ => 3
    };

    public bool IsSearchKey => Action == MappingAction.SearchKey;
    public bool IsDoNotImport => Action == MappingAction.DoNotImport;
    public bool IsMissingField => Action != MappingAction.DoNotImport && string.IsNullOrWhiteSpace(AdeptField);

    public static string FormatAction(MappingAction action) => action switch
    {
        MappingAction.SearchKey => "Search Key",
        MappingAction.FillOverwrite => "Fill: Overwrite",
        MappingAction.FillIfEmpty => "Fill: If Empty",
        MappingAction.DoNotImport => "Do Not Import",
        _ => action.ToString()
    };

    public static string FormatOperator(SearchOperator? op) => op switch
    {
        SearchOperator.Equals => "Exact Match",
        SearchOperator.DateAfter => "Date After",
        SearchOperator.DateBefore => "Date Before",
        SearchOperator.DateBetween => "Date Between",
        null => string.Empty,
        _ => op.ToString() ?? string.Empty
    };
}
