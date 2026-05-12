using AdeptTools.Import.Enums;
using AdeptTools.Import.Models;

namespace AdeptTools.Import.Services;

public class SearchBuilder
{
    public SearchRequestDto? BuildSearch(List<SearchKeyMapping> searchKeys, ImportRow row)
    {
        var terms = new List<SearchTermDto>();

        foreach (var key in searchKeys)
        {
            var cellValue = row.GetStringValue(key.ExcelColumn);

            // If any search key has an empty value, skip this row
            if (string.IsNullOrWhiteSpace(cellValue))
                return null;

            var term = new SearchTermDto
            {
                SchemaId = key.SchemaId,
                FieldName = key.AdeptFieldName
            };

            switch (key.Operator)
            {
                case SearchOperator.Equals:
                    term.SearchOp = "Equals";
                    term.ValueStr = cellValue;
                    break;

                case SearchOperator.DateAfter:
                    term.SearchOp = "AfterDate";
                    term.ValueStr = FormatDate(row, key.ExcelColumn);
                    break;

                case SearchOperator.DateBefore:
                    term.SearchOp = "BeforeDate";
                    term.ValueStr = FormatDate(row, key.ExcelColumn);
                    break;

                case SearchOperator.DateBetween:
                    term.SearchOp = "DateRange";
                    term.StartDate = FormatDate(row, key.ExcelColumn);
                    if (key.DateRangeColumn is not null)
                        term.EndDate = FormatDate(row, key.DateRangeColumn);
                    break;
            }

            terms.Add(term);
        }

        if (terms.Count == 0)
            return null;

        return new SearchRequestDto
        {
            SearchCriteria = terms,
            TableNumber = 1 // C_FILES
        };
    }

    private static string FormatDate(ImportRow row, string excelColumn)
    {
        var dateValue = row.GetDateValue(excelColumn);
        if (dateValue.HasValue)
            return dateValue.Value.ToString("yyyyMMdd");

        // Fallback: return raw value — server may accept it
        var raw = row.GetStringValue(excelColumn);
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException(
                $"Row {row.RowNumber}: Date value is empty for column '{excelColumn}'.");

        return raw;
    }
}
