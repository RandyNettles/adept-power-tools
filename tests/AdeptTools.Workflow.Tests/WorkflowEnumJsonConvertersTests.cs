using System.Text.Json;
using AdeptTools.Workflow.Models;
using Xunit;

namespace AdeptTools.Workflow.Tests;

public class WorkflowEnumJsonConvertersTests
{
    [Fact]
    public void WorkflowUserType_SerializesAsNumericCode()
    {
        var json = JsonSerializer.Serialize(WorkflowUserType.User);
        Assert.Equal("85", json);
    }

    [Fact]
    public void WorkflowNotificationAction_SerializesAsNumericCode()
    {
        var json = JsonSerializer.Serialize(WorkflowNotificationAction.Timeout);
        Assert.Equal("84", json);
    }

    [Fact]
    public void WorkflowNotificationDefinition_ContainsNumericTypeAndAction()
    {
        var model = new WorkflowNotificationDefinition
        {
            WorkflowId = "wf",
            StepId = "step",
            TrusteeId = "randy.nettles.wf",
            Type = WorkflowUserType.User,
            Action = WorkflowNotificationAction.Timeout
        };

        var json = JsonSerializer.Serialize(model);

        Assert.Contains("\"type\":85", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"action\":84", json, StringComparison.OrdinalIgnoreCase);
    }
}
