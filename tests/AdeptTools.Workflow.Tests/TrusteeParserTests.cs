using AdeptTools.Workflow.Input;
using Xunit;

namespace AdeptTools.Workflow.Tests;

public class TrusteeParserTests
{
    [Fact]
    public void Split_SingleValue_ReturnsList()
    {
        var result = TrusteeParser.Split("jsmith");
        Assert.Single(result);
        Assert.Equal("jsmith", result[0]);
    }

    [Fact]
    public void Split_CommaSeparated_ReturnsMultiple()
    {
        var result = TrusteeParser.Split("jsmith, mdoe, eng-managers");
        Assert.Equal(3, result.Count);
        Assert.Equal("jsmith", result[0]);
        Assert.Equal("mdoe", result[1]);
        Assert.Equal("eng-managers", result[2]);
    }

    [Fact]
    public void Split_EmptyString_ReturnsEmpty()
    {
        var result = TrusteeParser.Split("");
        Assert.Empty(result);
    }

    [Fact]
    public void Split_NullString_ReturnsEmpty()
    {
        var result = TrusteeParser.Split(null!);
        Assert.Empty(result);
    }

    [Fact]
    public void Split_WhitespaceOnly_ReturnsEmpty()
    {
        var result = TrusteeParser.Split("   ");
        Assert.Empty(result);
    }

    [Fact]
    public void Split_TrailingComma_IgnoresEmpty()
    {
        var result = TrusteeParser.Split("jsmith, mdoe,");
        Assert.Equal(2, result.Count);
        Assert.Equal("jsmith", result[0]);
        Assert.Equal("mdoe", result[1]);
    }

    [Fact]
    public void Split_TrimsWhitespace()
    {
        var result = TrusteeParser.Split("  jsmith  ,  mdoe  ");
        Assert.Equal(2, result.Count);
        Assert.Equal("jsmith", result[0]);
        Assert.Equal("mdoe", result[1]);
    }

    [Fact]
    public void Split_SingleWithWhitespace_Trims()
    {
        var result = TrusteeParser.Split("  jsmith  ");
        Assert.Single(result);
        Assert.Equal("jsmith", result[0]);
    }
}
