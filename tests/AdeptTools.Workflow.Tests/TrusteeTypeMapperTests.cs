using AdeptTools.Workflow.Input;
using AdeptTools.Workflow.Models;
using Xunit;

namespace AdeptTools.Workflow.Tests;

public class TrusteeTypeMapperTests
{
    [Theory]
    [InlineData("User", WorkflowUserType.User)]
    [InlineData("U", WorkflowUserType.User)]
    [InlineData("user", WorkflowUserType.User)]
    [InlineData("Group", WorkflowUserType.Group)]
    [InlineData("Grp", WorkflowUserType.Group)]
    [InlineData("G", WorkflowUserType.Group)]
    [InlineData("Meta", WorkflowUserType.Key)]
    [InlineData("Key", WorkflowUserType.Key)]
    [InlineData("K", WorkflowUserType.Key)]
    [InlineData("Email", WorkflowUserType.Email)]
    [InlineData("E", WorkflowUserType.Email)]
    [InlineData("EMAIL", WorkflowUserType.Email)]
    public void TryMap_ValidInputs_ReturnsTrue(string input, WorkflowUserType expected)
    {
        var result = TrusteeTypeMapper.TryMap(input, out var type);

        Assert.True(result);
        Assert.Equal(expected, type);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Invalid")]
    [InlineData("Admin")]
    [InlineData(null)]
    public void TryMap_InvalidInputs_ReturnsFalse(string? input)
    {
        var result = TrusteeTypeMapper.TryMap(input!, out _);

        Assert.False(result);
    }
}
