using System.CommandLine;
using System.CommandLine.Invocation;
using AdeptTools.Core.Configuration;
using AdeptTools.Core.Logging;
using AdeptTools.Workflow.Api;
using AdeptTools.Workflow.Results;
using AdeptTools.Workflow.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AdeptTools.Cli.Commands;

public static class WorkflowCommands
{
    public static Command CreateWorkflowCommand()
    {
        var workflowCommand = new Command("workflow", "Workflow administration commands");
        workflowCommand.AddCommand(CreateListCommand());
        workflowCommand.AddCommand(CreateUsersCommand());
        workflowCommand.AddCommand(CreateCreateCommand());
        workflowCommand.AddCommand(CreateModifyCommand());
        workflowCommand.AddCommand(CreateDeleteCommand());
        return workflowCommand;
    }

    private static Command CreateUsersCommand()
    {
        var outputOption = new Option<string>("--out", () => "login-ids.txt", "Output file path for login IDs");
        outputOption.AddAlias("-o");

        var formatOption = new Option<string>("--format", () => "txt", "Output format: txt, csv, json");

        var cmd = new Command("users", "Export workflow-eligible users (login IDs)");
        cmd.AddOption(outputOption);
        cmd.AddOption(formatOption);

        cmd.SetHandler(async (InvocationContext context) =>
        {
            var sp = context.BindingContext.GetRequiredService<IServiceProvider>();
            var apiClient = sp.GetRequiredService<IWorkflowApiClient>();

            var outputPath = context.ParseResult.GetValueForOption(outputOption)!;
            var format = context.ParseResult.GetValueForOption(formatOption)!.Trim().ToLowerInvariant();

            if (format is not ("txt" or "csv" or "json"))
            {
                Console.Error.WriteLine("Error: --format must be one of: txt, csv, json.");
                context.ExitCode = 1;
                return;
            }

            var users = await apiClient.GetUsersAsync(context.GetCancellationToken());
            var loginIds = users
                .Select(u => u.UserId?.Trim())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");

            if (format == "json")
            {
                var json = System.Text.Json.JsonSerializer.Serialize(loginIds,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(outputPath, json, context.GetCancellationToken());
            }
            else if (format == "csv")
            {
                var lines = new List<string> { "LoginId" };
                lines.AddRange(loginIds.Select(id => $"\"{id!.Replace("\"", "\"\"")}\""));
                await File.WriteAllLinesAsync(outputPath, lines, context.GetCancellationToken());
            }
            else
            {
                await File.WriteAllLinesAsync(outputPath, loginIds!, context.GetCancellationToken());
            }

            Console.WriteLine();
            Console.WriteLine($"  Exported {loginIds.Count} login ID(s) to: {Path.GetFullPath(outputPath)}");
            context.ExitCode = 0;
        });

        return cmd;
    }

    private static Command CreateListCommand()
    {
        var filterOption = new Option<string?>("--filter", "Filter workflows by name pattern (glob)");
        filterOption.AddAlias("-f");
        var formatOption = new Option<string>("--format", () => "table", "Output format: table, csv, json");

        var cmd = new Command("list", "List workflows on the server");
        cmd.AddOption(filterOption);
        cmd.AddOption(formatOption);

        cmd.SetHandler(async (InvocationContext context) =>
        {
            var sp = context.BindingContext.GetRequiredService<IServiceProvider>();
            var service = sp.GetRequiredService<IWorkflowService>();
            var settings = sp.GetRequiredService<AdeptToolSettings>();

            var request = new WorkflowListRequest
            {
                Filter = context.ParseResult.GetValueForOption(filterOption),
                Format = context.ParseResult.GetValueForOption(formatOption)!
            };

            var result = await service.ListAsync(request, context.GetCancellationToken());

            var format = request.Format.ToLowerInvariant();

            if (format == "json")
            {
                var json = System.Text.Json.JsonSerializer.Serialize(result.Workflows,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine(json);
            }
            else if (format == "csv")
            {
                Console.WriteLine("Name,Active,Steps,Trustees,Reviewers,Notify,Alerts,InProcess,LockedBy");
                foreach (var wf in result.Workflows)
                {
                    Console.WriteLine($"\"{wf.WorkflowName}\",{wf.Active},{wf.StepCount},{wf.TrusteeCount},{wf.ReviewerCount},{wf.NotifyCount},{wf.AlertCount},{wf.InProcessCount},\"{wf.LockedByDisplayName ?? ""}\"");
                }
            }
            else
            {
                // Table format
                Console.WriteLine();
                if (!string.IsNullOrWhiteSpace(result.AppliedFilter))
                    Console.WriteLine($"  Filter: {result.AppliedFilter}");

                Console.WriteLine($"  {"Name",-30} {"Active",-8} {"Steps",-7} {"Trustees",-10} {"Review",-7} {"Notify",-7} {"Alert",-7} {"In-Process",-12} {"Locked By",-15}");
                Console.WriteLine($"  {new string('─', 30)} {new string('─', 8)} {new string('─', 7)} {new string('─', 10)} {new string('─', 7)} {new string('─', 7)} {new string('─', 7)} {new string('─', 12)} {new string('─', 15)}");

                foreach (var wf in result.Workflows)
                {
                    var active = wf.Active ? "✓" : "✗";
                    var locked = wf.LockedByDisplayName ?? "";
                    Console.WriteLine($"  {wf.WorkflowName,-30} {active,-8} {wf.StepCount,-7} {wf.TrusteeCount,-10} {wf.ReviewerCount,-7} {wf.NotifyCount,-7} {wf.AlertCount,-7} {wf.InProcessCount,-12} {locked,-15}");
                }

                Console.WriteLine();
                Console.WriteLine($"  {result.TotalCount} workflow(s) shown.");
            }

            context.ExitCode = 0;
        });

        return cmd;
    }

    private static Command CreateCreateCommand()
    {
        var excelOption = new Option<string?>("--excel", "Path to Excel workbook (.xlsx)");
        var xmlOption = new Option<string?>("--xml", "Path to XML config file (.xml)");
        var dryRunOption = new Option<bool>("--dry-run", "Validate only; do not create workflows");
        var forceOption = new Option<bool>("--force", "Skip interactive confirmation prompt before creating workflows");
        forceOption.AddAlias("--yes");

        var cmd = new Command("create", "Create workflows from an Excel or XML file");
        cmd.AddOption(excelOption);
        cmd.AddOption(xmlOption);
        cmd.AddOption(dryRunOption);
        cmd.AddOption(forceOption);

        cmd.SetHandler(async (InvocationContext context) =>
        {
            var sp = context.BindingContext.GetRequiredService<IServiceProvider>();
            var service = sp.GetRequiredService<IWorkflowService>();
            var settings = sp.GetRequiredService<AdeptToolSettings>();

            var excel = context.ParseResult.GetValueForOption(excelOption);
            var xml = context.ParseResult.GetValueForOption(xmlOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var force = context.ParseResult.GetValueForOption(forceOption);

            var filePath = ResolveInputFile(excel, xml);
            if (filePath is null)
            {
                Console.Error.WriteLine("Error: --excel or --xml is required.");
                context.ExitCode = 1;
                return;
            }

            if (!dryRun && !force && !settings.MockMode)
            {
                Console.Write("\n  Create workflows from this file? [y/N] ");
                var response = Console.ReadLine();
                if (!string.Equals(response?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("  Cancelled.");
                    context.ExitCode = 0;
                    return;
                }
            }

            var logger = new ResultLogger(settings.Verbose);
            var progress = new Progress<WorkflowProgress>(p =>
            {
                var status = p.Status switch
                {
                    WorkflowResultStatus.Success => ResultStatus.OK,
                    WorkflowResultStatus.Fail => ResultStatus.Fail,
                    _ => ResultStatus.Skip
                };
                logger.Log(status, $"{p.WorkflowName} — {p.Message}");
            });

            Console.WriteLine();
            Console.WriteLine(dryRun ? "  DRY RUN — validating only" : "  Creating workflows...");

            var result = await service.CreateAsync(
                new WorkflowCreateRequest { InputFilePath = filePath, DryRun = dryRun },
                progress, context.GetCancellationToken());

            Console.WriteLine();
            Console.WriteLine($"  Summary: {result.Total} total, {result.Succeeded} succeeded, {result.Failed} failed, {result.Skipped} skipped");
            WriteDetailedOperationResults(result, settings.Verbose, includeSuccess: true);

            context.ExitCode = result.Failed > 0 ? 1 : 0;
        });

        return cmd;
    }

    private static Command CreateModifyCommand()
    {
        var excelOption = new Option<string?>("--excel", "Path to Excel workbook (.xlsx)");
        var xmlOption = new Option<string?>("--xml", "Path to XML config file (.xml)");
        var dryRunOption = new Option<bool>("--dry-run", "Validate only; do not modify workflows");
        var forceOption = new Option<bool>("--force", "Skip interactive confirmation prompt before modifying workflows");
        forceOption.AddAlias("--yes");

        var cmd = new Command("modify", "Modify existing workflows from an Excel or XML file");
        cmd.AddOption(excelOption);
        cmd.AddOption(xmlOption);
        cmd.AddOption(dryRunOption);
        cmd.AddOption(forceOption);

        cmd.SetHandler(async (InvocationContext context) =>
        {
            var sp = context.BindingContext.GetRequiredService<IServiceProvider>();
            var service = sp.GetRequiredService<IWorkflowService>();
            var settings = sp.GetRequiredService<AdeptToolSettings>();

            var excel = context.ParseResult.GetValueForOption(excelOption);
            var xml = context.ParseResult.GetValueForOption(xmlOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var force = context.ParseResult.GetValueForOption(forceOption);

            var filePath = ResolveInputFile(excel, xml);
            if (filePath is null)
            {
                Console.Error.WriteLine("Error: --excel or --xml is required.");
                context.ExitCode = 1;
                return;
            }

            if (!dryRun && !force && !settings.MockMode)
            {
                Console.Write("\n  Modify workflows from this file? [y/N] ");
                var response = Console.ReadLine();
                if (!string.Equals(response?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("  Cancelled.");
                    context.ExitCode = 0;
                    return;
                }
            }

            var logger = new ResultLogger(settings.Verbose);
            var progress = new Progress<WorkflowProgress>(p =>
            {
                var status = p.Status switch
                {
                    WorkflowResultStatus.Success => ResultStatus.OK,
                    WorkflowResultStatus.Fail => ResultStatus.Fail,
                    _ => ResultStatus.Skip
                };
                logger.Log(status, $"{p.WorkflowName} — {p.Message}");
            });

            Console.WriteLine();
            Console.WriteLine(dryRun ? "  DRY RUN — validating only" : "  Modifying workflows...");

            var result = await service.ModifyAsync(
                new WorkflowModifyRequest { InputFilePath = filePath, DryRun = dryRun },
                progress, context.GetCancellationToken());

            Console.WriteLine();
            Console.WriteLine($"  Summary: {result.Total} total, {result.Succeeded} succeeded, {result.Failed} failed, {result.Skipped} skipped");
            WriteDetailedOperationResults(result, settings.Verbose);

            context.ExitCode = result.Failed > 0 ? 1 : 0;
        });

        return cmd;
    }

    private static Command CreateDeleteCommand()
    {
        var filterOption = new Option<string>("--filter", "Filter workflows by name pattern (glob)") { IsRequired = true };
        filterOption.AddAlias("-f");
        var statusOption = new Option<string>("--status", () => "all", "Filter by status: active, inactive, all");
        var dryRunOption = new Option<bool>("--dry-run", "Show what would be deleted without deleting");
        var forceOption = new Option<bool>("--force", "Skip interactive confirmation prompt");
        var manifestOption = new Option<string?>("--manifest", "Write pre-deletion manifest JSON to this path");

        var cmd = new Command("delete", "Delete workflows matching a filter");
        cmd.AddOption(filterOption);
        cmd.AddOption(statusOption);
        cmd.AddOption(dryRunOption);
        cmd.AddOption(forceOption);
        cmd.AddOption(manifestOption);

        cmd.SetHandler(async (InvocationContext context) =>
        {
            var sp = context.BindingContext.GetRequiredService<IServiceProvider>();
            var service = sp.GetRequiredService<IWorkflowService>();
            var settings = sp.GetRequiredService<AdeptToolSettings>();

            var filter = context.ParseResult.GetValueForOption(filterOption)!;
            var status = context.ParseResult.GetValueForOption(statusOption)!;
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var force = context.ParseResult.GetValueForOption(forceOption);
            var manifest = context.ParseResult.GetValueForOption(manifestOption);

            // Safety: reject wildcard-only without --force
            if ((filter == "*" || filter == "**") && !dryRun && !force)
            {
                Console.Error.WriteLine("Error: wildcard-only filter requires --force to prevent accidental deletion of all workflows.");
                context.ExitCode = 1;
                return;
            }

            // First, list what would be deleted
            var listResult = await service.ListAsync(
                new WorkflowListRequest { Filter = filter },
                context.GetCancellationToken());

            var eligibleWorkflows = listResult.Workflows
                .Where(w => status.ToLowerInvariant() switch
                {
                    "active" => w.Active,
                    "inactive" => !w.Active,
                    _ => true
                })
                .Where(w => w.Delete)
                .Where(w => string.IsNullOrWhiteSpace(w.LockedByDisplayName))
                .ToList();

            if (eligibleWorkflows.Count == 0)
            {
                Console.WriteLine($"  No deletable workflows match filter '{filter}' with status '{status}'.");
                context.ExitCode = 2;
                return;
            }

            // Show pre-deletion table
            Console.WriteLine();
            Console.WriteLine($"  Workflows to delete: {eligibleWorkflows.Count}");
            Console.WriteLine();
            Console.WriteLine($"  {"Name",-30} {"Active",-8} {"Steps",-7} {"In-Process",-12}");
            Console.WriteLine($"  {new string('─', 30)} {new string('─', 8)} {new string('─', 7)} {new string('─', 12)}");

            const int MaxPreviewRows = 25;
            var totalInProcess = eligibleWorkflows.Sum(w => w.InProcessCount);
            foreach (var wf in eligibleWorkflows.Take(MaxPreviewRows))
            {
                var active = wf.Active ? "✓" : "✗";
                var inProcess = wf.InProcessCount > 0 ? $"{wf.InProcessCount} docs ⚠" : "0";
                Console.WriteLine($"  {wf.WorkflowName,-30} {active,-8} {wf.StepCount,-7} {inProcess,-12}");
            }

            if (eligibleWorkflows.Count > MaxPreviewRows)
            {
                Console.WriteLine($"  ... and {eligibleWorkflows.Count - MaxPreviewRows} more workflows");
            }

            if (totalInProcess > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"  ⚠ {totalInProcess} documents are in-process. They will be moved to the System Workflow.");
            }

            if (dryRun)
            {
                await service.DeleteAsync(new WorkflowDeleteRequest
                {
                    Filter = filter,
                    Status = status,
                    DryRun = true,
                    Force = true,
                    ManifestPath = manifest,
                    PreFetchedPacket = listResult.Packet
                }, null, context.GetCancellationToken());

                Console.WriteLine();
                Console.WriteLine("  DRY RUN — no changes made.");
                if (manifest is not null)
                    Console.WriteLine($"  Manifest saved to: {manifest}");
                context.ExitCode = 0;
                return;
            }

            // Confirmation
            if (!force && !settings.MockMode)
            {
                Console.Write($"\n  Delete these {eligibleWorkflows.Count} workflows? [y/N] ");
                var response = Console.ReadLine();
                if (!string.Equals(response?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("  Cancelled.");
                    context.ExitCode = 0;
                    return;
                }
            }

            var logger = new ResultLogger(settings.Verbose);
            var progress = new Progress<WorkflowProgress>(p =>
            {
                var resultStatus = p.Status switch
                {
                    WorkflowResultStatus.Success => ResultStatus.OK,
                    WorkflowResultStatus.Fail => ResultStatus.Fail,
                    _ => ResultStatus.Skip
                };
                logger.Log(resultStatus, $"{p.WorkflowName} — {p.Message}");
            });

            var result = await service.DeleteAsync(new WorkflowDeleteRequest
            {
                Filter = filter,
                Status = status,
                DryRun = false,
                Force = true, // Already confirmed above
                ManifestPath = manifest,
                PreFetchedPacket = listResult.Packet
            }, progress, context.GetCancellationToken());

            Console.WriteLine();
            Console.WriteLine($"  Summary: {result.Total} total, {result.Succeeded} deleted, {result.Failed} failed, {result.Skipped} skipped");
            WriteDetailedOperationResults(result, settings.Verbose);

            if (manifest is not null)
                Console.WriteLine($"  Manifest saved to: {manifest}");

            context.ExitCode = result.Failed > 0 ? 1 : 0;
        });

        return cmd;
    }

    private static string? ResolveInputFile(string? excel, string? xml)
    {
        if (!string.IsNullOrWhiteSpace(excel)) return excel;
        if (!string.IsNullOrWhiteSpace(xml)) return xml;
        return null;
    }

    private static void WriteDetailedOperationResults(WorkflowBatchResult result, bool verbose, bool includeSuccess = false)
    {
        if (result.Results.Count == 0)
            return;

        var shouldPrint = includeSuccess || result.Results.Any(r => r.Status != WorkflowResultStatus.Success);
        if (!shouldPrint)
            return;

        Console.WriteLine();
        Console.WriteLine("  Operation Results:");

        foreach (var item in result.Results)
        {
            var statusLabel = item.Status switch
            {
                WorkflowResultStatus.Success => "[OK]",
                WorkflowResultStatus.Fail => "[FAIL]",
                _ => "[SKIP]"
            };

            var workflowName = string.IsNullOrWhiteSpace(item.WorkflowName) ? "Unknown" : item.WorkflowName;
            Console.WriteLine($"  {statusLabel} {workflowName}: {item.Message}");

            if (verbose && !string.IsNullOrWhiteSpace(item.Details))
            {
                foreach (var line in item.Details.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        Console.WriteLine($"         {line}");
                }
            }
        }
    }
}
