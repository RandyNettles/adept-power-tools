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
    [InlineData("Reviewer", WorkflowUserType.User)]
    [InlineData("Approver", WorkflowUserType.User)]
    [InlineData("Group", WorkflowUserType.Group)]
    [InlineData("Grp", WorkflowUserType.Group)]
    [InlineData("G", WorkflowUserType.Group)]
    [InlineData("Meta", WorkflowUserType.Key)]
    [InlineData("Key", WorkflowUserType.Key)]
    [InlineData("K", WorkflowUserType.Key)]
    [InlineData("Email", WorkflowUserType.Email)]
    [InlineData("E", WorkflowUserType.Email)]
    [InlineData("EMAIL", WorkflowUserType.Email)]
    [InlineData("Approvers", WorkflowUserType.Approvers)]
    [InlineData("A", WorkflowUserType.Approvers)]
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

    [Theory]
    [InlineData("Reviewer", TrusteeRole.Reviewer)]
    [InlineData("Review", TrusteeRole.Reviewer)]
    [InlineData("R", TrusteeRole.Reviewer)]
    [InlineData("Approve", TrusteeRole.Reviewer)]
    [InlineData("Approver", TrusteeRole.Reviewer)]
    [InlineData("Notify", TrusteeRole.EmailNotify)]
    [InlineData("Notification", TrusteeRole.EmailNotify)]
    [InlineData("Email", TrusteeRole.EmailNotify)]
    [InlineData("EmailNotify", TrusteeRole.EmailNotify)]
    [InlineData("N", TrusteeRole.EmailNotify)]
    [InlineData("Alert", TrusteeRole.AlertNotify)]
    [InlineData("AlertNotify", TrusteeRole.AlertNotify)]
    public void TryMapRole_ValidInputs_ReturnsTrue(string input, TrusteeRole expected)
    {
        var result = TrusteeTypeMapper.TryMapRole(input, out var role);

        Assert.True(result);
        Assert.Equal(expected, role);
    }

    [Fact]
    public void TryMapRole_NullOrEmpty_DefaultsToReviewer()
    {
        Assert.True(TrusteeTypeMapper.TryMapRole("", out var role1));
        Assert.Equal(TrusteeRole.Reviewer, role1);

        Assert.True(TrusteeTypeMapper.TryMapRole(null!, out var role2));
        Assert.Equal(TrusteeRole.Reviewer, role2);
    }

    [Theory]
    [InlineData("Invalid")]
    [InlineData("Xyz")]
    public void TryMapRole_InvalidInputs_ReturnsFalse(string input)
    {
        var result = TrusteeTypeMapper.TryMapRole(input, out _);

        Assert.False(result);
    }
}
