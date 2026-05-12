using AdeptTools.Import.Enums;
using AdeptTools.Import.Models;
using AdeptTools.Import.Services;
using Xunit;

namespace AdeptTools.Import.Tests.Services;

public class SearchBuilderTests
{
    private readonly SearchBuilder _builder = new();

    [Fact]
    public void BuildSearch_EqualsOperator_CorrectSearchTerm()
    {
        var keys = new List<SearchKeyMapping>
        {
            new() { ExcelColumn = "FileName", AdeptFieldName = "S_LONGNAME", SchemaId = "S1", Operator = SearchOperator.Equals }
        };
        var row = new ImportRow { RowNumber = 1, Values = new() { ["FileName"] = "DWG-001.dwg" } };

        var result = _builder.BuildSearch(keys, row);

        Assert.NotNull(result);
        Assert.Single(result.SearchCriteria);
        Assert.Equal("Equals", result.SearchCriteria[0].SearchOp);
        Assert.Equal("DWG-001.dwg", result.SearchCriteria[0].ValueStr);
        Assert.Equal("S1", result.SearchCriteria[0].SchemaId);
    }

    [Fact]
    public void BuildSearch_DateAfterOperator_FormattedDate()
    {
        var keys = new List<SearchKeyMapping>
        {
            new() { ExcelColumn = "Date", AdeptFieldName = "S_REVDATE", SchemaId = "S2", Operator = SearchOperator.DateAfter, IsDate = true }
        };
        var row = new ImportRow { RowNumber = 1, Values = new() { ["Date"] = new DateTime(2026, 1, 15) } };

        var result = _builder.BuildSearch(keys, row);

        Assert.NotNull(result);
        Assert.Equal("AfterDate", result.SearchCriteria[0].SearchOp);
        Assert.Equal("20260115", result.SearchCriteria[0].ValueStr);
    }

    [Fact]
    public void BuildSearch_DateBeforeOperator_FormattedDate()
    {
        var keys = new List<SearchKeyMapping>
        {
            new() { ExcelColumn = "Date", AdeptFieldName = "S_REVDATE", SchemaId = "S2", Operator = SearchOperator.DateBefore, IsDate = true }
        };
        var row = new ImportRow { RowNumber = 1, Values = new() { ["Date"] = new DateTime(2026, 6, 30) } };

        var result = _builder.BuildSearch(keys, row);

        Assert.NotNull(result);
        Assert.Equal("BeforeDate", result.SearchCriteria[0].SearchOp);
        Assert.Equal("20260630", result.SearchCriteria[0].ValueStr);
    }

    [Fact]
    public void BuildSearch_DateBetweenOperator_StartAndEndDates()
    {
        var keys = new List<SearchKeyMapping>
        {
            new()
            {
                ExcelColumn = "StartDate", AdeptFieldName = "S_REVDATE", SchemaId = "S2",
                Operator = SearchOperator.DateBetween, IsDate = true, DateRangeColumn = "EndDate"
            }
        };
        var row = new ImportRow
        {
            RowNumber = 1,
            Values = new()
            {
                ["StartDate"] = new DateTime(2026, 1, 1),
                ["EndDate"] = new DateTime(2026, 12, 31)
            }
        };

        var result = _builder.BuildSearch(keys, row);

        Assert.NotNull(result);
        Assert.Equal("DateRange", result.SearchCriteria[0].SearchOp);
        Assert.Equal("20260101", result.SearchCriteria[0].StartDate);
        Assert.Equal("20261231", result.SearchCriteria[0].EndDate);
    }

    [Fact]
    public void BuildSearch_EmptySearchKeyValue_ReturnsNull()
    {
        var keys = new List<SearchKeyMapping>
        {
            new() { ExcelColumn = "FileName", AdeptFieldName = "S_LONGNAME", SchemaId = "S1", Operator = SearchOperator.Equals }
        };
        var row = new ImportRow { RowNumber = 1, Values = new() { ["FileName"] = "" } };

        var result = _builder.BuildSearch(keys, row);

        Assert.Null(result);
    }

    [Fact]
    public void BuildSearch_MultipleSearchKeys_AllIncluded()
    {
        var keys = new List<SearchKeyMapping>
        {
            new() { ExcelColumn = "Name", AdeptFieldName = "S_LONGNAME", SchemaId = "S1", Operator = SearchOperator.Equals },
            new() { ExcelColumn = "Project", AdeptFieldName = "PROJECT", SchemaId = "U2", Operator = SearchOperator.Equals }
        };
        var row = new ImportRow
        {
            RowNumber = 1,
            Values = new() { ["Name"] = "DWG-001.dwg", ["Project"] = "Piping" }
        };

        var result = _builder.BuildSearch(keys, row);

        Assert.NotNull(result);
        Assert.Equal(2, result.SearchCriteria.Count);
    }

    [Fact]
    public void BuildSearch_DateStringParsed_Formatted()
    {
        var keys = new List<SearchKeyMapping>
        {
            new() { ExcelColumn = "Date", AdeptFieldName = "S_REVDATE", SchemaId = "S2", Operator = SearchOperator.DateAfter, IsDate = true }
        };
        var row = new ImportRow { RowNumber = 1, Values = new() { ["Date"] = "2026-03-15" } };

        var result = _builder.BuildSearch(keys, row);

        Assert.NotNull(result);
        Assert.Equal("20260315", result.SearchCriteria[0].ValueStr);
    }
}
