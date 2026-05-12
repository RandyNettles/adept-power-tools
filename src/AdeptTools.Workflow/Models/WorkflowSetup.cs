using AdeptTools.Core.Models;

namespace AdeptTools.Workflow.Models;

public class WorkflowSetup : ApiResult
{
    public int MaximumLengthWorkflowName { get; set; }
    public int MaximumLengthStepName { get; set; }
    public int MaximumWorkflowSteps { get; set; }
    public int MaximumWorkflows { get; set; }
}
