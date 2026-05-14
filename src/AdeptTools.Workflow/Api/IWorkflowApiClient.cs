using AdeptTools.Core.Models;
using AdeptTools.Workflow.Input;
using AdeptTools.Workflow.Models;

namespace AdeptTools.Workflow.Api;

public interface IWorkflowApiClient
{
    Task<WorkflowSetup> GetSetupAsync(CancellationToken ct = default);
    Task<WorkflowAdminPacket> GetWorkflowsAsync(CancellationToken ct = default);
    Task<WorkflowAdminPacket> GetWorkflowsBasicAsync(CancellationToken ct = default);
    Task<WorkflowEditModel> CreateNewAsync(CancellationToken ct = default);
    Task<WorkflowEditModel> GetWorkflowAsync(string workflowId, CancellationToken ct = default);
    Task<ApiResult> SaveWorkflowAsync(WorkflowEditModel model, CancellationToken ct = default);
    Task<ApiResult> DeleteWorkflowAsync(string workflowId, CancellationToken ct = default);
    Task<WorkflowEditModel> AddStepAsync(WorkflowEditModel model, int position, CancellationToken ct = default);
    Task<WorkflowEditModel> TagAsync(string workflowId, CancellationToken ct = default);
    Task<ApiResult> UntagAsync(string workflowId, CancellationToken ct = default);
    Task<List<WorkflowCommonTarget>> GetMetagroupsAsync(CancellationToken ct = default);
    Task<List<AdeptUserEntry>> GetUsersAsync(CancellationToken ct = default);
}
