namespace AdeptTools.Workflow.Input;

public class WorkflowExcelInput
{
    public string? ServerUrl { get; set; }
    public string? ProjectName { get; set; }
    public bool DryRun { get; set; }
    public List<WorkflowInputModel> Workflows { get; set; } = new();
}
