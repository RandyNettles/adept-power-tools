using AdeptTools.Import.Enums;
using AdeptTools.Import.Models;
using AdeptTools.Import.Services;
using Xunit;

namespace AdeptTools.Import.Tests.Services;

public class MappingValidatorTests
{
    private readonly MappingValidator _validator = new();
    private readonly ImportConfig _defaultConfig = new() { ImportMode = ImportMode.UpdateDataCard };

    private List<AdeptFieldDefinitionDto> DefaultFields => new()
    {
        new() { FieldName = "S_LONGNAME", SchemaId = "S1", FieldType = "FDT_STRING", Width = 255, IsSystem = true },
        new() { FieldName = "DESCRIPTION", SchemaId = "U1", FieldType = "FDT_STRING", Width = 128, IsSystem = false },
    };

    [Fact]
    public void Validate_NoSearchKeys_Error()
    {
        var result = _validator.Validate(_defaultConfig, new(), new List<FillFieldMapping>
        {
            new() { ExcelColumn = "Desc", AdeptFieldName = "DESCRIPTION", SchemaId = "U1", Mode = FillMode.Overwrite }
        }, new(), DefaultFields);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("search key"));
    }

    [Fact]
    public void Validate_NoFillFields_Error()
    {
        var result = _validator.Validate(_defaultConfig, new List<SearchKeyMapping>
        {
            new() { ExcelColumn = "Name", AdeptFieldName = "S_LONGNAME", SchemaId = "S1", Operator = SearchOperator.Equals }
        }, new(), new(), DefaultFields);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("fill field"));
    }

    [Fact]
    public void Validate_SearchResultsOnly_NoFillFieldsAllowed()
    {
        var config = new ImportConfig { ImportMode = ImportMode.SearchResultsOnly };

        var result = _validator.Validate(config, new List<SearchKeyMapping>
        {
            new() { ExcelColumn = "Name", AdeptFieldName = "S_LONGNAME", SchemaId = "S1", Operator = SearchOperator.Equals }
        }, new(), new(), DefaultFields);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_DuplicateSearchKey_Error()
    {
        var searchKeys = new List<SearchKeyMapping>
        {
            new() { ExcelColumn = "Col1", AdeptFieldName = "S_LONGNAME", SchemaId = "S1", Operator = SearchOperator.Equals },
            new() { ExcelColumn = "Col2", AdeptFieldName = "S_LONGNAME", SchemaId = "S1", Operator = SearchOperator.Equals }
        };
        var fillFields = new List<FillFieldMapping>
        {
            new() { ExcelColumn = "Desc", AdeptFieldName = "DESCRIPTION", SchemaId = "U1", Mode = FillMode.Overwrite }
        };

        var result = _validator.Validate(_defaultConfig, searchKeys, fillFields, new(), DefaultFields);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("Duplicate search key"));
    }

    [Fact]
    public void Validate_DuplicateFillField_Error()
    {
        var searchKeys = new List<SearchKeyMapping>
        {
            new() { ExcelColumn = "Name", AdeptFieldName = "S_LONGNAME", SchemaId = "S1", Operator = SearchOperator.Equals }
        };
        var fillFields = new List<FillFieldMapping>
        {
            new() { ExcelColumn = "Desc1", AdeptFieldName = "DESCRIPTION", SchemaId = "U1", Mode = FillMode.Overwrite },
            new() { ExcelColumn = "Desc2", AdeptFieldName = "DESCRIPTION", SchemaId = "U1", Mode = FillMode.IfEmpty }
        };

        var result = _validator.Validate(_defaultConfig, searchKeys, fillFields, new(), DefaultFields);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("Duplicate fill field"));
    }

    [Fact]
    public void Validate_DateBetweenWithoutDateRangeColumn_Error()
    {
        var searchKeys = new List<SearchKeyMapping>
        {
            new() { ExcelColumn = "Date", AdeptFieldName = "S_REVDATE", SchemaId = "S2", Operator = SearchOperator.DateBetween, IsDate = true }
        };
        var fillFields = new List<FillFieldMapping>
        {
            new() { ExcelColumn = "Desc", AdeptFieldName = "DESCRIPTION", SchemaId = "U1", Mode = FillMode.Overwrite }
        };

        var result = _validator.Validate(_defaultConfig, searchKeys, fillFields, new(), DefaultFields);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("DateBetween") && e.Message.Contains("DateRangeColumn"));
    }

    [Fact]
    public void Validate_LongNameExceedsWidth_Error()
    {
        var config = new ImportConfig { ImportMode = ImportMode.UpdateDataCard, AddIfNotFound = true };
        var searchKeys = new List<SearchKeyMapping>
        {
            new() { ExcelColumn = "FileName", AdeptFieldName = "S_LONGNAME", SchemaId = "S1", Operator = SearchOperator.Equals }
        };
        var fillFields = new List<FillFieldMapping>
        {
            new() { ExcelColumn = "Desc", AdeptFieldName = "DESCRIPTION", SchemaId = "U1", Mode = FillMode.Overwrite }
        };

        var fields = new List<AdeptFieldDefinitionDto>
        {
            new() { FieldName = "S_LONGNAME", SchemaId = "S1", FieldType = "FDT_STRING", Width = 10, IsSystem = true },
            new() { FieldName = "DESCRIPTION", SchemaId = "U1", FieldType = "FDT_STRING", Width = 128 },
        };

        var dataRows = new List<ImportRow>
        {
            new() { RowNumber = 2, Values = new() { ["FileName"] = "VeryLongFileNameThatExceedsLimit.dwg" } }
        };

        var result = _validator.Validate(config, searchKeys, fillFields, dataRows, fields);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("S_LONGNAME") && e.Message.Contains("exceeds"));
    }

    [Fact]
    public void Validate_ValidMapping_NoErrors()
    {
        var searchKeys = new List<SearchKeyMapping>
        {
            new() { ExcelColumn = "Name", AdeptFieldName = "S_LONGNAME", SchemaId = "S1", Operator = SearchOperator.Equals }
        };
        var fillFields = new List<FillFieldMapping>
        {
            new() { ExcelColumn = "Desc", AdeptFieldName = "DESCRIPTION", SchemaId = "U1", Mode = FillMode.Overwrite }
        };

        var result = _validator.Validate(_defaultConfig, searchKeys, fillFields, new(), DefaultFields);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}
