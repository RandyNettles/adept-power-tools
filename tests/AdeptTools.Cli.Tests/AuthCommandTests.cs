using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using AdeptTools.Cli.Commands;
using AdeptTools.Cli.Infrastructure;
using AdeptTools.Core.Configuration;
using AdeptTools.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdeptTools.Cli.Tests;

public class AuthCommandTests
{
    [Fact]
    public async Task AuthTest_MockMode_ReturnsZeroExitCode()
    {
        var exitCode = await InvokeCli("auth", "test", "--mock");

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task AuthTest_MockMode_OutputContainsConnected()
    {
        var (exitCode, output) = await InvokeCliWithOutput("auth", "test", "--mock");

        Assert.Equal(0, exitCode);
        Assert.Contains("Connected", output);
        Assert.Contains("mock", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Help_ShowsAuthCommand()
    {
        var (exitCode, output) = await InvokeCliWithOutput("--help");

        Assert.Equal(0, exitCode);
        Assert.Contains("auth", output);
    }

    [Fact]
    public async Task Version_ShowsVersionString()
    {
        var (exitCode, output) = await InvokeCliWithOutput("--version");

        Assert.Equal(0, exitCode);
        Assert.NotEmpty(output.Trim());
    }

    private static Task<int> InvokeCli(params string[] args)
    {
        var parser = BuildParser();
        return parser.InvokeAsync(args);
    }

    private static async Task<(int ExitCode, string Output)> InvokeCliWithOutput(params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        Console.SetError(writer);

        try
        {
            var parser = BuildParser();
            var exitCode = await parser.InvokeAsync(args);
            return (exitCode, writer.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static Parser BuildParser()
    {
        var rootCommand = new RootCommand("Adept Tool — CLI utility for Adept administrative operations");

        var serverOption = new Option<string?>("--server", "Adept server URL");
        serverOption.AddAlias("-s");
        var userOption = new Option<string?>("--user", "Username for authentication");
        userOption.AddAlias("-u");
        var mockOption = new Option<bool>("--mock", "Use mock data");
        mockOption.AddAlias("-m");
        var backendOption = new Option<BackendType>("--backend", () => BackendType.Http, "Backend type");
        backendOption.AddAlias("-b");
        var verboseOption = new Option<bool>("--verbose", "Enable verbose output");
        verboseOption.AddAlias("-v");
        var logOption = new Option<string?>("--log", "Log file path");

        rootCommand.AddGlobalOption(serverOption);
        rootCommand.AddGlobalOption(userOption);
        rootCommand.AddGlobalOption(mockOption);
        rootCommand.AddGlobalOption(backendOption);
        rootCommand.AddGlobalOption(verboseOption);
        rootCommand.AddGlobalOption(logOption);
        rootCommand.AddCommand(AuthCommands.CreateAuthCommand());
        rootCommand.AddCommand(WorkflowCommands.CreateWorkflowCommand());

        return new CommandLineBuilder(rootCommand)
            .UseDefaults()
            .UseVersionOption()
            .AddMiddleware(async (context, next) =>
            {
                var parseResult = context.ParseResult;
                var server = parseResult.GetValueForOption(serverOption);
                var user = parseResult.GetValueForOption(userOption);
                var mock = parseResult.GetValueForOption(mockOption);
                var backend = parseResult.GetValueForOption(backendOption);
                var verbose = parseResult.GetValueForOption(verboseOption);
                var log = parseResult.GetValueForOption(logOption);

                if (!mock && string.IsNullOrWhiteSpace(server) && context.ParseResult.CommandResult.Command != rootCommand)
                {
                    Console.Error.WriteLine("Error: --server is required unless --mock is specified.");
                    context.ExitCode = 1;
                    return;
                }

                var settings = new AdeptToolSettings
                {
                    ServerUrl = server,
                    UserName = user,
                    MockMode = mock,
                    Backend = backend,
                    Verbose = verbose,
                    LogPath = log
                };

                var services = new ServiceCollection();
                services.AddSingleton(settings);

                try
                {
                    ServiceRegistration.ConfigureServices(services, backend, mock, server);
                }
                catch (InvalidOperationException ex)
                {
                    Console.Error.WriteLine($"Error: {ex.Message}");
                    context.ExitCode = 1;
                    return;
                }

                var serviceProvider = services.BuildServiceProvider();
                context.BindingContext.AddService<IServiceProvider>(_ => serviceProvider);

                await next(context);
            })
            .Build();
    }

    [Fact]
    public async Task WorkflowDelete_DryRun_StatusFilterPreviewMatchesRealDeleteSet()
    {
        var (exitCode, output) = await InvokeCliWithOutput(
            "workflow", "delete", "--filter", "*", "--status", "inactive", "--dry-run", "--mock");

        Assert.Equal(0, exitCode);
        Assert.Contains("Workflows to delete: 1", output);
        Assert.Contains("Final Check", output);
        Assert.DoesNotContain("Design Review", output);
        Assert.DoesNotContain("Piping Approval", output);
    }

    [Fact]
    public async Task WorkflowDelete_DryRun_WritesManifestWhenRequested()
    {
        var manifestPath = Path.Combine(Path.GetTempPath(), $"workflow_delete_manifest_{Guid.NewGuid():N}.json");

        try
        {
            var (exitCode, output) = await InvokeCliWithOutput(
                "workflow", "delete", "--filter", "*Review*", "--dry-run", "--manifest", manifestPath, "--mock");

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(manifestPath));
            Assert.Contains("Manifest saved to:", output);
            var manifestJson = File.ReadAllText(manifestPath);
            Assert.Contains("Design Review", manifestJson);
        }
        finally
        {
            if (File.Exists(manifestPath))
                File.Delete(manifestPath);
        }
    }
}
