using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using AdeptTools.Core.Configuration;
using AdeptTools.Core.Logging;
using AdeptTools.Import.Enums;
using AdeptTools.Import.Models;
using AdeptTools.Import.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AdeptTools.Cli.Commands;

public static class ImportCommands
{
    public static Command CreateImportCommand()
    {
        var importCommand = new Command("import", "Data import commands");
        importCommand.AddCommand(CreateFetchFieldsCommand());
        importCommand.AddCommand(CreateMapCommand());
        importCommand.AddCommand(CreateValidateCommand());
        importCommand.AddCommand(CreateRunCommand());
        return importCommand;
    }

    private static Command CreateFetchFieldsCommand()
    {
        var outputOption = new Option<string>("--output", "Output file path") { IsRequired = true };
        outputOption.AddAlias("-o");
        var formatOption = new Option<string>("--format", () => "csv", "Output format: csv, json");

        var cmd = new Command("fetch-fields", "Fetch available field definitions from the server");
        cmd.AddOption(outputOption);
        cmd.AddOption(formatOption);

        cmd.SetHandler(async (InvocationContext context) =>
        {
            var sp = context.BindingContext.GetRequiredService<IServiceProvider>();
            var service = sp.GetRequiredService<IImportService>();

            var output = context.ParseResult.GetValueForOption(outputOption)!;
            var format = context.ParseResult.GetValueForOption(formatOption)!;

            Console.WriteLine();
            Console.WriteLine("  Fetching field definitions...");

            var fields = await service.FetchFieldsAsync(context.GetCancellationToken());

            if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                var json = JsonSerializer.Serialize(fields, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(output, json, context.GetCancellationToken());
            }
            else
            {
                var lines = new List<string>
                {
                    "FieldName,DisplayName,SchemaID,FieldType,Width,System,Protected,Restricted,Indexed"
                };
                foreach (var f in fields)
                {
                    lines.Add($"\"{f.FieldName}\",\"{f.DisplayName}\",\"{f.SchemaId}\",\"{f.FieldType}\",{f.Width},{f.IsSystem},{f.IsProtected},{f.IsRestricted},{f.IsIndexed}");
                }
                await File.WriteAllLinesAsync(output, lines, context.GetCancellationToken());
            }

            Console.WriteLine($"  {fields.Count} fields written to: {output}");
            context.ExitCode = 0;
        });

        return cmd;
    }

    private static Command CreateMapCommand()
    {
        var excelOption = new Option<string>("--excel", "Path to Excel file with data headers") { IsRequired = true };
        var sheetOption = new Option<string?>("--sheet", "Specific sheet name (default: first sheet)");
        var outputOption = new Option<string>("--output", "Output XML mapping file path") { IsRequired = true };
        outputOption.AddAlias("-o");

        var cmd = new Command("map", "Auto-map Excel columns to Adept fields");
        cmd.AddOption(excelOption);
        cmd.AddOption(sheetOption);
        cmd.AddOption(outputOption);

        cmd.SetHandler(async (InvocationContext context) =>
        {
            var sp = context.BindingContext.GetRequiredService<IServiceProvider>();
            var service = sp.GetRequiredService<IImportService>();

            var excel = context.ParseResult.GetValueForOption(excelOption)!;
            var sheet = context.ParseResult.GetValueForOption(sheetOption);
            var output = context.ParseResult.GetValueForOption(outputOption)!;

            Console.WriteLine();
            Console.WriteLine("  Auto-mapping Excel columns to Adept fields...");

            var mappings = await service.AutoMapAsync(excel, sheet, context.GetCancellationToken());

            // Display mapping table
            Console.WriteLine();
            Console.WriteLine($"  {"Excel Column",-25} {"Adept Field",-25} {"Action",-15}");
            Console.WriteLine($"  {new string('─', 25)} {new string('─', 25)} {new string('─', 15)}");

            foreach (var m in mappings)
            {
                Console.WriteLine($"  {m.ExcelColumn,-25} {m.AdeptField,-25} {m.Action,-15}");
            }

            // Write XML config
            WriteXmlMapping(output, mappings);

            Console.WriteLine();
            Console.WriteLine($"  Mapping written to: {output}");
            context.ExitCode = 0;
        });

        return cmd;
    }

    private static Command CreateValidateCommand()
    {
        var excelOption = new Option<string>("--excel", "Path to Excel workbook") { IsRequired = true };
        var configOption = new Option<string?>("--config", "Path to XML mapping config file");

        var cmd = new Command("validate", "Validate import mapping and data");
        cmd.AddOption(excelOption);
        cmd.AddOption(configOption);

        cmd.SetHandler(async (InvocationContext context) =>
        {
            var sp = context.BindingContext.GetRequiredService<IServiceProvider>();
            var service = sp.GetRequiredService<IImportService>();

            var excel = context.ParseResult.GetValueForOption(excelOption)!;
            var config = context.ParseResult.GetValueForOption(configOption);

            Console.WriteLine();
            Console.WriteLine("  Validating import configuration...");

            var result = await service.ValidateAsync(
                new ImportValidateRequest { ExcelPath = excel, ConfigPath = config },
                context.GetCancellationToken());

            Console.WriteLine();
            Console.WriteLine($"  Data rows:    {result.RowCount}");
            Console.WriteLine($"  Search keys:  {result.SearchKeyCount}");
            Console.WriteLine($"  Fill fields:  {result.FillFieldCount}");
            Console.WriteLine();

            if (result.Errors.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  {result.Errors.Count} error(s):");
                foreach (var error in result.Errors)
                    Console.WriteLine($"    ✗ {error.Message}");
                Console.ResetColor();
            }

            if (result.Warnings.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  {result.Warnings.Count} warning(s):");
                foreach (var warning in result.Warnings)
                    Console.WriteLine($"    ⚠ {warning.Message}");
                Console.ResetColor();
            }

            if (result.IsValid)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  ✓ Validation passed");
                Console.ResetColor();
            }

            context.ExitCode = result.IsValid ? 0 : 1;
        });

        return cmd;
    }

    private static Command CreateRunCommand()
    {
        var excelOption = new Option<string>("--excel", "Path to Excel workbook") { IsRequired = true };
        var configOption = new Option<string?>("--config", "Path to XML mapping config file");
        var dryRunOption = new Option<bool>("--dry-run", "Validate and report, no data changes");
        var logOption = new Option<string?>("--log-file", "Write detailed log to this file");

        var cmd = new Command("run", "Run data import from Excel workbook");
        cmd.AddOption(excelOption);
        cmd.AddOption(configOption);
        cmd.AddOption(dryRunOption);
        cmd.AddOption(logOption);

        cmd.SetHandler(async (InvocationContext context) =>
        {
            var sp = context.BindingContext.GetRequiredService<IServiceProvider>();
            var service = sp.GetRequiredService<IImportService>();
            var settings = sp.GetRequiredService<AdeptToolSettings>();

            var excel = context.ParseResult.GetValueForOption(excelOption)!;
            var config = context.ParseResult.GetValueForOption(configOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var logFile = context.ParseResult.GetValueForOption(logOption);

            var logger = new ResultLogger(settings.Verbose, logFile);

            var progress = new Progress<ImportProgress>(p =>
            {
                if (p.Outcome.HasValue)
                {
                    var status = p.Outcome.Value switch
                    {
                        ImportOutcome.Updated => ResultStatus.OK,
                        ImportOutcome.Created => ResultStatus.Add,
                        ImportOutcome.Skipped => ResultStatus.Skip,
                        ImportOutcome.Failed => ResultStatus.Fail,
                        _ => ResultStatus.OK
                    };
                    logger.Log(status, $"Row {p.RowNumber}: {p.CurrentPrimaryKey} — {p.Message}");
                }
            });

            Console.WriteLine();
            Console.WriteLine(dryRun ? "  DRY RUN — validating only" : "  Running import...");

            try
            {
                var result = await service.RunAsync(
                    new ImportRunRequest { ExcelPath = excel, ConfigPath = config, DryRun = dryRun, LogPath = logFile },
                    progress, context.GetCancellationToken());

                if (result.Errors.Count > 0)
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Red;
                    foreach (var error in result.Errors)
                        Console.Error.WriteLine($"  Error: {error}");
                    Console.ResetColor();
                    context.ExitCode = 2;
                    return;
                }

                Console.WriteLine();
                Console.WriteLine("  Summary:");
                Console.WriteLine($"    {result.TotalRows} rows processed");
                Console.WriteLine($"    {result.Updated} updated, {result.Created} created, {result.Skipped} skipped, {result.Failed} failed");

                if (logFile is not null)
                    Console.WriteLine($"    Log: {logFile}");

                context.ExitCode = result.Failed > 0 ? 1 : 0;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine();
                Console.WriteLine("  Operation cancelled.");
                context.ExitCode = 0;
            }
        });

        return cmd;
    }

    private static void WriteXmlMapping(string outputPath, List<ColumnMapping> mappings)
    {
        using var writer = new System.Xml.XmlTextWriter(outputPath, System.Text.Encoding.UTF8)
        {
            Formatting = System.Xml.Formatting.Indented
        };

        writer.WriteStartDocument();
        writer.WriteStartElement("DataImportConfiguration");

        writer.WriteElementString("ImportMode", "UpdateDataCard");
        writer.WriteElementString("AddIfNotFound", "false");

        writer.WriteStartElement("Mappings");
        foreach (var m in mappings)
        {
            writer.WriteStartElement("Mapping");
            writer.WriteAttributeString("ExcelColumn", m.ExcelColumn);
            writer.WriteAttributeString("AdeptField", m.AdeptField);
            writer.WriteAttributeString("Action", m.Action.ToString());
            if (m.Operator.HasValue)
                writer.WriteAttributeString("Operator", m.Operator.Value.ToString());
            writer.WriteEndElement();
        }
        writer.WriteEndElement(); // Mappings

        writer.WriteEndElement(); // DataImportConfiguration
        writer.WriteEndDocument();
    }
}
