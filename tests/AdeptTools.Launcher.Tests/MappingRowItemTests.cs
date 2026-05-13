using AdeptTools.Import.Enums;
using AdeptTools.Launcher.ViewModels;

namespace AdeptTools.Launcher.Tests;

public class MappingRowItemTests
{
    [Theory]
    [InlineData(MappingAction.SearchKey, "Search Key")]
    [InlineData(MappingAction.FillOverwrite, "Fill: Overwrite")]
    [InlineData(MappingAction.FillIfEmpty, "Fill: If Empty")]
    [InlineData(MappingAction.DoNotImport, "Do Not Import")]
    public void FormatAction_ReturnsExpectedDisplay(MappingAction action, string expected)
    {
        Assert.Equal(expected, MappingRowItem.FormatAction(action));
    }

    [Theory]
    [InlineData(SearchOperator.Equals, "Exact Match")]
    [InlineData(SearchOperator.DateAfter, "Date After")]
    [InlineData(SearchOperator.DateBefore, "Date Before")]
    [InlineData(SearchOperator.DateBetween, "Date Between")]
    [InlineData(null, "")]
    public void FormatOperator_ReturnsExpectedDisplay(SearchOperator? op, string expected)
    {
        Assert.Equal(expected, MappingRowItem.FormatOperator(op));
    }

    [Fact]
    public void IsSearchKey_TrueForSearchKeyAction()
    {
        var row = new MappingRowItem { Action = MappingAction.SearchKey };
        Assert.True(row.IsSearchKey);
        Assert.False(row.IsDoNotImport);
    }

    [Fact]
    public void IsDoNotImport_TrueForDoNotImportAction()
    {
        var row = new MappingRowItem { Action = MappingAction.DoNotImport };
        Assert.True(row.IsDoNotImport);
        Assert.False(row.IsSearchKey);
    }

    [Fact]
    public void IsMissingField_TrueWhenActionSetButFieldEmpty()
    {
        var row = new MappingRowItem
        {
            Action = MappingAction.FillOverwrite,
            AdeptField = ""
        };
        Assert.True(row.IsMissingField);
    }

    [Fact]
    public void IsMissingField_FalseForDoNotImport()
    {
        var row = new MappingRowItem
        {
            Action = MappingAction.DoNotImport,
            AdeptField = ""
        };
        Assert.False(row.IsMissingField);
    }

    [Fact]
    public void SortOrder_SearchKeysFirst()
    {
        var searchKey = new MappingRowItem { Action = MappingAction.SearchKey };
        var fill = new MappingRowItem { Action = MappingAction.FillOverwrite };
        var doNotImport = new MappingRowItem { Action = MappingAction.DoNotImport };

        Assert.True(searchKey.SortOrder < fill.SortOrder);
        Assert.True(fill.SortOrder < doNotImport.SortOrder);
    }
}
