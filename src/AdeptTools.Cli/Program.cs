using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using AdeptTools.Cli.Commands;
using AdeptTools.Cli.Infrastructure;
using AdeptTools.Core.Auth;
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
rootCommand.AddCommand(WorkflowCommands.CreateWorkflowCommand());
rootCommand.AddCommand(ImportCommands.CreateImportCommand());

// Build parser with DI middleware
var parser = new CommandLineBuilder(rootCommand)
    .UseDefaults()
    .UseExceptionHandler((ex, context) =>
    {
        var details = new List<string>();
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(current.Message))
                details.Add(current.Message);
        }

        var message = details.Count == 0
            ? "Command failed due to an unexpected error."
            : string.Join(" -> ", details);

        Console.Error.WriteLine($"Error: {message}");
        context.ExitCode = 1;
    })
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

        // Ensure command handlers have an active authenticated session unless this is the auth command path.
        if (!mock && !IsAuthCommand(context.ParseResult.CommandResult))
        {
            var authService = serviceProvider.GetRequiredService<IAdeptAuthService>();
            if (!authService.IsAuthenticated)
            {
                var loginUser = string.IsNullOrWhiteSpace(user) ? "ADM" : user;
                var loginPassword = Environment.GetEnvironmentVariable("ADEPTTOOLS_PASSWORD") ?? string.Empty;

                var authResult = await authService.LoginAsync(
                    server!,
                    loginUser,
                    loginPassword,
                    context.GetCancellationToken());

                if (!authResult.Success)
                {
                    var error = string.IsNullOrWhiteSpace(authResult.ErrorMessage)
                        ? "Authentication failed."
                        : authResult.ErrorMessage;

                    Console.Error.WriteLine($"Error: unable to establish {backend} session. {error}");

                    if (authResult.RequiresUserSelection)
                    {
                        Console.Error.WriteLine("Hint: run 'auth test' to choose the desired account, then rerun this command.");
                    }
                    else if (backend == BackendType.Com)
                    {
                        Console.Error.WriteLine("Hint: verify Adept desktop is open and logged in, or set ADEPTTOOLS_PASSWORD for direct login.");
                    }

                    context.ExitCode = 1;
                    return;
                }
            }
        }

        await next(context);
    })
    .Build();

return await parser.InvokeAsync(args);

static bool IsAuthCommand(CommandResult commandResult)
{
    SymbolResult? current = commandResult;
    while (current is not null)
    {
        if (current is CommandResult currentCommand &&
            string.Equals(currentCommand.Command.Name, "auth", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        current = current.Parent;
    }

    return false;
}
