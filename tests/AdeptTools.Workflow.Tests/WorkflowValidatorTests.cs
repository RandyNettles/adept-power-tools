using AdeptTools.Workflow.Input;
using AdeptTools.Workflow.Models;
using AdeptTools.Workflow.Validation;
using Xunit;

namespace AdeptTools.Workflow.Tests;

public class WorkflowValidatorTests
{
    private readonly WorkflowValidator _validator = new();

    [Fact]
    public void Validate_EmptyList_ReturnsError()
    {
        var result = _validator.Validate(new List<WorkflowInputModel>());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("No workflows"));
    }

    [Fact]
    public void Validate_EmptyName_ReturnsError()
    {
        var workflows = new List<WorkflowInputModel>
        {
            new() { Name = "", Steps = new List<WorkflowInputStep> { new() { Name = "Step 1" } } }
        };

        var result = _validator.Validate(workflows);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Name" && e.Message.Contains("empty"));
    }

    [Fact]
    public void Validate_NameExceedsMaxLength_ReturnsError()
    {
        var setup = new WorkflowSetup { MaximumLengthWorkflowName = 10 };
        var workflows = new List<WorkflowInputModel>
        {
            new() { Name = "This name is way too long", Steps = new List<WorkflowInputStep> { new() { Name = "S1" } } }
        };

        var result = _validator.Validate(workflows, setup);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Name" && e.Message.Contains("exceeds"));
    }

    [Fact]
    public void Validate_DuplicateNames_ReturnsError()
    {
        var workflows = new List<WorkflowInputModel>
        {
            new() { Name = "Duplicate", Steps = new List<WorkflowInputStep> { new() { Name = "S1" } } },
            new() { Name = "duplicate", Steps = new List<WorkflowInputStep> { new() { Name = "S1" } } }
        };

        var result = _validator.Validate(workflows);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("Duplicate"));
    }

    [Fact]
    public void Validate_NoSteps_ReturnsError()
    {
        var workflows = new List<WorkflowInputModel>
        {
            new() { Name = "Test WF", Steps = new List<WorkflowInputStep>() }
        };

        var result = _validator.Validate(workflows);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Steps" && e.Message.Contains("at least one step"));
    }

    [Fact]
    public void Validate_EmptyStepName_ReturnsError()
    {
        var workflows = new List<WorkflowInputModel>
        {
            new() { Name = "Test WF", Steps = new List<WorkflowInputStep> { new() { Name = "" } } }
        };

        var result = _validator.Validate(workflows);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Step Name" && e.Message.Contains("empty"));
    }

    [Fact]
    public void Validate_NegativeApprovals_ReturnsError()
    {
        var workflows = new List<WorkflowInputModel>
        {
            new()
            {
                Name = "Test WF",
                Steps = new List<WorkflowInputStep>
                {
                    new() { Name = "Step 1", RequiredApprovalsCount = -1 }
                }
            }
        };

        var result = _validator.Validate(workflows);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "ApprovalsRequired" && e.Message.Contains("negative"));
    }

    [Fact]
    public void Validate_StepWithoutTrustees_ReturnsWarning()
    {
        var workflows = new List<WorkflowInputModel>
        {
            new()
            {
                Name = "Test WF",
                Steps = new List<WorkflowInputStep>
                {
                    new() { Name = "Step 1", Trustees = new List<WorkflowInputTrustee>() }
                }
            }
        };

        var result = _validator.Validate(workflows);

        Assert.True(result.IsValid); // Warnings don't block
        Assert.Contains(result.Warnings, w => w.Message.Contains("no trustees"));
    }

    [Fact]
    public void Validate_BatchExceedsLicenseLimit_ReturnsError()
    {
        var setup = new WorkflowSetup { MaximumWorkflows = 1 };
        var workflows = new List<WorkflowInputModel>
        {
            new() { Name = "WF1", Steps = new List<WorkflowInputStep> { new() { Name = "S1" } } },
            new() { Name = "WF2", Steps = new List<WorkflowInputStep> { new() { Name = "S1" } } }
        };

        var result = _validator.Validate(workflows, setup);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("server limit"));
    }

    [Fact]
    public void Validate_ValidInput_ReturnsNoErrors()
    {
        var workflows = new List<WorkflowInputModel>
        {
            new()
            {
                Name = "Valid Workflow",
                Steps = new List<WorkflowInputStep>
                {
                    new()
                    {
                        Name = "Step 1",
                        RequiredApprovalsCount = 2,
                        Trustees = new List<WorkflowInputTrustee>
                        {
                            new() { TrusteeId = "user1", TrusteeType = WorkflowUserType.User }
                        }
                    }
                }
            }
        };

        var result = _validator.Validate(workflows);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }
}
