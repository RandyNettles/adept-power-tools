using AdeptTools.Import.Api;
using AdeptTools.Import.Enums;
using AdeptTools.Import.Models;
using AdeptTools.Import.Services;
using Xunit;

namespace AdeptTools.Import.Tests.Services;

public class FieldResolverTests
{
    private readonly FieldResolver _resolver = new();
    private readonly MockImportApiClient _mockClient = new();

    [Fact]
    public async Task ResolveFieldsAsync_AllFieldsFound_ResolvedWithSchemaIds()
    {
        var mappings = new List<ColumnMapping>
        {
            new() { ExcelColumn = "FileName", AdeptField = "S_LONGNAME", Action = MappingAction.SearchKey, Operator = SearchOperator.Equals },
            new() { ExcelColumn = "Desc", AdeptField = "DESCRIPTION", Action = MappingAction.FillOverwrite }
        };

        var result = await _resolver.ResolveFieldsAsync(mappings, _mockClient);

        Assert.True(result.IsValid);
        Assert.Single(result.SearchKeys);
        Assert.Equal("S1", result.SearchKeys[0].SchemaId);
        Assert.Single(result.FillFields);
        Assert.Equal("U1", result.FillFields[0].SchemaId);
    }

    [Fact]
    public async Task ResolveFieldsAsync_UnknownField_ErrorInResult()
    {
        var mappings = new List<ColumnMapping>
        {
            new() { ExcelColumn = "Col1", AdeptField = "NONEXISTENT_FIELD", Action = MappingAction.SearchKey }
        };

        var result = await _resolver.ResolveFieldsAsync(mappings, _mockClient);

        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("NONEXISTENT_FIELD", result.Errors[0]);
    }

    [Fact]
    public async Task ResolveFieldsAsync_MatchByDisplayName_Resolved()
    {
        var mappings = new List<ColumnMapping>
        {
            new() { ExcelColumn = "Col1", AdeptField = "File Name", Action = MappingAction.SearchKey }
        };

        var result = await _resolver.ResolveFieldsAsync(mappings, _mockClient);

        Assert.True(result.IsValid);
        Assert.Single(result.SearchKeys);
        Assert.Equal("S1", result.SearchKeys[0].SchemaId);
        Assert.Equal("S_LONGNAME", result.SearchKeys[0].AdeptFieldName);
    }

    [Fact]
    public async Task ResolveFieldsAsync_DateField_MarkedAsDate()
    {
        var mappings = new List<ColumnMapping>
        {
            new() { ExcelColumn = "RevDate", AdeptField = "REV_DATE", Action = MappingAction.FillOverwrite }
        };

        var result = await _resolver.ResolveFieldsAsync(mappings, _mockClient);

        Assert.True(result.IsValid);
        Assert.Single(result.FillFields);
        Assert.True(result.FillFields[0].IsDate);
    }

    [Fact]
    public async Task ResolveFieldsAsync_FillIfEmpty_CorrectMode()
    {
        var mappings = new List<ColumnMapping>
        {
            new() { ExcelColumn = "Desc", AdeptField = "DESCRIPTION", Action = MappingAction.FillIfEmpty }
        };

        var result = await _resolver.ResolveFieldsAsync(mappings, _mockClient);

        Assert.True(result.IsValid);
        Assert.Single(result.FillFields);
        Assert.Equal(FillMode.IfEmpty, result.FillFields[0].Mode);
    }
}
