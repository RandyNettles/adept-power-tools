using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using AdeptTools.Cli.Commands;
using AdeptTools.Cli.Infrastructure;
using AdeptTools.Core.Configuration;
using AdeptTools.Core.Models;
using Microsoft.Extensions.DependencyInjection;

var rootCommand = new RootCommand("Adept Tool — CLI utility for Adept administrative operations");

// Global options
var serverOption = new Option<string?>("--server", "Adept server URL (e.g., https://adept.company.com)");
serverOption.AddAlias("-s");

var userOption = new Option<string?>("--user", "Username for authentication");
userOption.AddAlias("-u");

var mockOption = new Option<bool>("--mock", "Use mock data (no server required)");
mockOption.AddAlias("-m");

var backendOption = new Option<BackendType>("--backend", () => BackendType.Http, "Backend type: http or com");
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

// Add commands
rootCommand.AddCommand(AuthCommands.CreateAuthCommand());

// Build parser with DI middleware
var parser = new CommandLineBuilder(rootCommand)
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

        // Validate: --server required unless --mock
        if (!mock && string.IsNullOrWhiteSpace(server) && context.ParseResult.CommandResult.Command != rootCommand)
        {
            // Only enforce for actual commands, not --help or --version
            var commandPath = context.ParseResult.CommandResult.Command.Name;
            if (commandPath != rootCommand.Name)
            {
                Console.Error.WriteLine("Error: --server is required unless --mock is specified.");
                context.ExitCode = 1;
                return;
            }
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

return await parser.InvokeAsync(args);
