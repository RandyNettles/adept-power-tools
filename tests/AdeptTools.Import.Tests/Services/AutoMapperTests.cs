using AdeptTools.Import.Enums;
using AdeptTools.Import.Models;
using AdeptTools.Import.Services;
using Xunit;

namespace AdeptTools.Import.Tests.Services;

public class AutoMapperTests
{
    private readonly AutoMapper _mapper = new();

    private List<AdeptFieldDefinitionDto> MockFields => new()
    {
        new() { FieldName = "S_LONGNAME", DisplayName = "File Name", SchemaId = "S1", FieldType = "FDT_STRING", Width = 255, IsSystem = true },
        new() { FieldName = "S_LIBNAME", DisplayName = "Library Name", SchemaId = "S3", FieldType = "FDT_STRING", Width = 64, IsSystem = true },
        new() { FieldName = "DESCRIPTION", DisplayName = "Description", SchemaId = "U1", FieldType = "FDT_STRING", Width = 128, IsSystem = false },
        new() { FieldName = "PROJECT", DisplayName = "Project", SchemaId = "U2", FieldType = "FDT_STRING", Width = 64, IsSystem = false },
    };

    [Fact]
    public void AutoMap_FieldNameMatch_SearchKey()
    {
        var result = _mapper.AutoMap(new List<string> { "S_LONGNAME" }, MockFields);

        Assert.Single(result);
        Assert.Equal("S_LONGNAME", result[0].AdeptField);
        Assert.Equal(MappingAction.SearchKey, result[0].Action);
    }

    [Fact]
    public void AutoMap_DisplayNameMatch_SearchKeyForSystem()
    {
        var result = _mapper.AutoMap(new List<string> { "Library Name" }, MockFields);

        Assert.Single(result);
        Assert.Equal("S_LIBNAME", result[0].AdeptField);
        Assert.Equal(MappingAction.SearchKey, result[0].Action);
    }

    [Fact]
    public void AutoMap_UserField_FillOverwrite()
    {
        var result = _mapper.AutoMap(new List<string> { "DESCRIPTION" }, MockFields);

        Assert.Single(result);
        Assert.Equal("DESCRIPTION", result[0].AdeptField);
        Assert.Equal(MappingAction.FillOverwrite, result[0].Action);
    }

    [Fact]
    public void AutoMap_UnknownColumn_DoNotImport()
    {
        var result = _mapper.AutoMap(new List<string> { "RandomColumn" }, MockFields);

        Assert.Single(result);
        Assert.Equal(MappingAction.DoNotImport, result[0].Action);
    }

    [Fact]
    public void AutoMap_CaseInsensitive_Match()
    {
        var result = _mapper.AutoMap(new List<string> { "description" }, MockFields);

        Assert.Single(result);
        Assert.Equal("DESCRIPTION", result[0].AdeptField);
        Assert.Equal(MappingAction.FillOverwrite, result[0].Action);
    }

    [Fact]
    public void AutoMap_MultipleHeaders_CorrectMappings()
    {
        var headers = new List<string> { "S_LONGNAME", "DESCRIPTION", "UnknownCol" };
        var result = _mapper.AutoMap(headers, MockFields);

        Assert.Equal(3, result.Count);
        Assert.Equal(MappingAction.SearchKey, result[0].Action);
        Assert.Equal(MappingAction.FillOverwrite, result[1].Action);
        Assert.Equal(MappingAction.DoNotImport, result[2].Action);
    }
}
