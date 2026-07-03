using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using AdeptTools.Cli.Commands;
using AdeptTools.Cli.Infrastructure;
using AdeptTools.Backend.Http.Auth;
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
            var sessionStore = serviceProvider.GetRequiredService<CliAuthSessionStore>();

            // For HTTP backend, try to reuse a saved token first.
            if (backend == BackendType.Http && !authService.IsAuthenticated && authService is HttpAdeptAuthService httpAuth)
            {
                var saved = sessionStore.Load();
                var normalizedServer = server!.TrimEnd('/') + "/";
                if (saved is not null &&
                    !string.IsNullOrWhiteSpace(saved.ServerUrl) &&
                    !string.IsNullOrWhiteSpace(saved.AccessToken) &&
                    string.Equals(saved.ServerUrl.TrimEnd('/') + "/", normalizedServer, StringComparison.OrdinalIgnoreCase))
                {
                    var expiry = saved.AccessTokenExpiresUtc ?? httpAuth.GetAccessTokenExpiryUtc(saved.AccessToken);
                    if (!expiry.HasValue || expiry.Value > DateTimeOffset.UtcNow)
                    {
                        var resumeResult = await httpAuth.TryResumeSessionAsync(
                            server,
                            saved.AccessToken,
                            saved.RefreshToken,
                            saved.AccessTokenExpiresUtc,
                            userId: null,
                            userName: saved.UserName,
                            displayName: null,
                            emailAddress: null,
                            appVersion: null,
                            workAreaId: null,
                            context.GetCancellationToken());

                        if (!resumeResult.Success)
                            sessionStore.Clear();
                    }
                    else
                    {
                        sessionStore.Clear();
                    }
                }
            }

            if (!authService.IsAuthenticated)
            {
                var loginUser = string.IsNullOrWhiteSpace(user) ? "ADM" : user;
                var loginPassword = Environment.GetEnvironmentVariable("ADEPTTOOLS_PASSWORD") ?? string.Empty;

                var authResult = await authService.LoginAsync(
                    server!,
                    loginUser,
                    loginPassword,
                    context.GetCancellationToken());

                if (!authResult.Success && authResult.RequiresUserSelection)
                {
                    authResult = await PromptForUserSelectionAsync(authService, authResult, context.GetCancellationToken());
                }

                if (!authResult.Success)
                {
                    var error = string.IsNullOrWhiteSpace(authResult.ErrorMessage)
                        ? "Authentication failed."
                        : authResult.ErrorMessage;

                    Console.Error.WriteLine($"Error: unable to establish {backend} session. {error}");

                    if (backend == BackendType.Com)
                    {
                        Console.Error.WriteLine("Hint: verify Adept desktop is open and logged in, or set ADEPTTOOLS_PASSWORD for direct login.");
                    }

                    if (backend == BackendType.Http)
                        sessionStore.Clear();

                    context.ExitCode = 1;
                    return;
                }

                if (backend == BackendType.Http && authService is HttpAdeptAuthService httpAuthAfterLogin)
                {
                    sessionStore.Save(new CliAuthSessionState
                    {
                        ServerUrl = server!.TrimEnd('/') + "/",
                        AccessToken = authService.AccessToken!,
                        RefreshToken = httpAuthAfterLogin.RefreshToken,
                        AccessTokenExpiresUtc = httpAuthAfterLogin.GetAccessTokenExpiryUtc(authService.AccessToken),
                        UserName = loginUser
                    });
                }
            }
        }

        await next(context);
    })
    .Build();

return await parser.InvokeAsync(args);

static async Task<AuthResult> PromptForUserSelectionAsync(
    IAdeptAuthService authService,
    AuthResult authResult,
    CancellationToken ct)
{
    if (authResult.UserChoices is null || authResult.UserChoices.Count == 0)
        return new AuthResult(false, "Multiple Adept accounts were returned, but no selectable users were provided.");

    Console.WriteLine("  Select user:");
    for (var i = 0; i < authResult.UserChoices.Count; i++)
    {
        var choice = authResult.UserChoices[i];
        Console.WriteLine($"    {i + 1}. {choice.DisplayLabel}");
    }

    Console.Write("  Choice:     ");
    var input = Console.ReadLine();
    if (!int.TryParse(input, out var selectedIndex) ||
        selectedIndex < 1 || selectedIndex > authResult.UserChoices.Count)
    {
        return new AuthResult(false, "Invalid selection.");
    }

    var selected = authResult.UserChoices[selectedIndex - 1];
    return await authService.SelectUserAsync(selected.Id, selected.UserName, ct);
}

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
