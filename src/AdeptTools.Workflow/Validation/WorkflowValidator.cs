using AdeptTools.Workflow.Input;
using AdeptTools.Workflow.Models;

namespace AdeptTools.Workflow.Validation;

public class WorkflowValidator
{
    public ValidationResult Validate(List<WorkflowInputModel> workflows, WorkflowSetup? serverSetup = null)
    {
        var result = new ValidationResult();

        if (workflows.Count == 0)
        {
            result.Errors.Add(new ValidationError { Message = "No workflows to process." });
            return result;
        }

        int maxNameLen = serverSetup?.MaximumLengthWorkflowName ?? 128;
        int maxStepNameLen = serverSetup?.MaximumLengthStepName ?? 128;
        int maxSteps = serverSetup?.MaximumWorkflowSteps ?? int.MaxValue;
        int maxWorkflows = serverSetup?.MaximumWorkflows ?? int.MaxValue;

        // Check batch count against license limit
        if (serverSetup is not null && workflows.Count > maxWorkflows)
        {
            result.Errors.Add(new ValidationError
            {
                Message = $"Batch contains {workflows.Count} workflows but server limit is {maxWorkflows}."
            });
        }

        // Check for duplicate names
        var duplicates = workflows
            .GroupBy(w => w.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var dup in duplicates)
        {
            result.Errors.Add(new ValidationError
            {
                WorkflowName = dup,
                Field = "Name",
                Message = $"Duplicate workflow name: '{dup}'."
            });
        }

        // Per-workflow validation
        foreach (var wf in workflows)
        {
            ValidateWorkflow(wf, maxNameLen, maxStepNameLen, maxSteps, result);
        }

        return result;
    }

    private static void ValidateWorkflow(
        WorkflowInputModel wf,
        int maxNameLen,
        int maxStepNameLen,
        int maxSteps,
        ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(wf.Name))
        {
            result.Errors.Add(new ValidationError
            {
                WorkflowName = wf.Name,
                Field = "Name",
                Message = "Workflow name is empty."
            });
        }
        else if (wf.Name.Length > maxNameLen)
        {
            result.Errors.Add(new ValidationError
            {
                WorkflowName = wf.Name,
                Field = "Name",
                Message = $"Workflow name exceeds maximum length ({wf.Name.Length} > {maxNameLen})."
            });
        }

        if (wf.Steps.Count == 0)
        {
            result.Errors.Add(new ValidationError
            {
                WorkflowName = wf.Name,
                Field = "Steps",
                Message = "Workflow must have at least one step."
            });
            return;
        }

        if (wf.Steps.Count > maxSteps)
        {
            result.Errors.Add(new ValidationError
            {
                WorkflowName = wf.Name,
                Field = "Steps",
                Message = $"Step count exceeds maximum ({wf.Steps.Count} > {maxSteps})."
            });
        }

        foreach (var step in wf.Steps)
        {
            if (string.IsNullOrWhiteSpace(step.Name))
            {
                result.Errors.Add(new ValidationError
                {
                    WorkflowName = wf.Name,
                    StepName = step.Name,
                    Field = "Step Name",
                    Message = "Step name is empty."
                });
            }
            else if (step.Name.Length > maxStepNameLen)
            {
                result.Errors.Add(new ValidationError
                {
                    WorkflowName = wf.Name,
                    StepName = step.Name,
                    Field = "Step Name",
                    Message = $"Step name exceeds maximum length ({step.Name.Length} > {maxStepNameLen})."
                });
            }

            if (step.RequiredApprovalsCount < 0)
            {
                result.Errors.Add(new ValidationError
                {
                    WorkflowName = wf.Name,
                    StepName = step.Name,
                    Field = "ApprovalsRequired",
                    Message = "Approvals required cannot be negative."
                });
            }

            if (step.Trustees.Count == 0)
            {
                result.Warnings.Add(new ValidationWarning
                {
                    WorkflowName = wf.Name,
                    StepName = step.Name,
                    Field = "Trustees",
                    Message = "Step has no trustees assigned."
                });
            }
        }
    }
}
