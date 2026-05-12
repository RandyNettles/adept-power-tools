using AdeptTools.Workflow.Results;

namespace AdeptTools.Workflow.Services;

public interface IWorkflowService
{
    Task<WorkflowBatchResult> CreateAsync(WorkflowCreateRequest request, IProgress<WorkflowProgress>? progress = null, CancellationToken ct = default);
    Task<WorkflowBatchResult> ModifyAsync(WorkflowModifyRequest request, IProgress<WorkflowProgress>? progress = null, CancellationToken ct = default);
    Task<WorkflowBatchResult> DeleteAsync(WorkflowDeleteRequest request, IProgress<WorkflowProgress>? progress = null, CancellationToken ct = default);
    Task<WorkflowListResult> ListAsync(WorkflowListRequest request, CancellationToken ct = default);
}
