using System.CommandLine;
using System.CommandLine.Invocation;
using AdeptTools.Core.Api;
using AdeptTools.Core.Auth;
using AdeptTools.Core.Configuration;
using AdeptTools.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using AdeptTools.Cli.Infrastructure;

namespace AdeptTools.Cli.Commands;

public static class AuthCommands
{
    public static Command CreateAuthCommand()
    {
        var authCommand = new Command("auth", "Authentication commands");
        authCommand.AddCommand(CreateTestCommand());
        authCommand.AddCommand(CreateLogoutCommand());
        return authCommand;
    }

    private static Command CreateTestCommand()
    {
        var testCommand = new Command("test", "Test connection and authentication to an Adept server");

        testCommand.SetHandler(async (InvocationContext context) =>
        {
            var serviceProvider = context.BindingContext.GetRequiredService<IServiceProvider>();
            var settings = serviceProvider.GetRequiredService<AdeptToolSettings>();
            var authService = serviceProvider.GetRequiredService<IAdeptAuthService>();
            var apiClient = serviceProvider.GetRequiredService<IAdeptApiClient>();

            var serverDisplay = settings.MockMode
                ? "mock://localhost (mock mode)"
                : settings.ServerUrl ?? "unknown";

            Console.WriteLine();
            Console.WriteLine($"  Server:     {serverDisplay}");

            // Determine credentials
            string userName;
            string password = string.Empty;

            if (settings.MockMode)
            {
                userName = "MockUser";
                password = "mock";
            }
            else if (settings.Backend == BackendType.Http)
            {
                // HTTP uses SSO — no password needed, browser will open
                userName = settings.UserName ?? "ADM";

                if (string.IsNullOrEmpty(settings.UserName))
                {
                    Console.Write("  User:       ");
                    userName = Console.ReadLine() ?? "ADM";
                }

                Console.WriteLine("  Auth:       SSO (opening browser...)");
            }
            else
            {
                // COM backend — prompt for credentials
                userName = settings.UserName ?? "ADM";

                if (string.IsNullOrEmpty(settings.UserName))
                {
                    Console.Write("  User:       ");
                    userName = Console.ReadLine() ?? "ADM";
                }

                password = CredentialManager.PromptForPassword("  Password:   ");
            }

            // Login
            var authResult = await authService.LoginAsync(
                settings.ServerUrl ?? "mock://localhost",
                userName,
                password);

            if (!authResult.Success && authResult.RequiresUserSelection)
            {
                if (authResult.UserChoices is null || authResult.UserChoices.Count == 0)
                {
                    Console.WriteLine("  Status:     \u001b[31m✗ Connection failed\u001b[0m");
                    Console.WriteLine("  Error:      Multiple Adept accounts were returned, but no selectable users were provided.");
                    Console.WriteLine();
                    context.ExitCode = 1;
                    return;
                }

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
                    Console.WriteLine("  Status:     \u001b[31m✗ Connection failed\u001b[0m");
                    Console.WriteLine("  Error:      Invalid selection.");
                    Console.WriteLine();
                    context.ExitCode = 1;
                    return;
                }

                var selected = authResult.UserChoices[selectedIndex - 1];
                authResult = await authService.SelectUserAsync(selected.Id, selected.UserName);
            }

            if (!authResult.Success)
            {
                var error = string.IsNullOrWhiteSpace(authResult.ErrorMessage)
                    ? "Authentication failed. No additional details were returned by the server."
                    : authResult.ErrorMessage;
                Console.WriteLine($"  Status:     \u001b[31m✗ Connection failed\u001b[0m");
                Console.WriteLine($"  Error:      {error}");
                Console.WriteLine();
                context.ExitCode = 1;
                return;
            }

            // Get additional info (best-effort). Some servers can authenticate successfully
            // while a follow-up user-info endpoint returns non-JSON content.
            UserInfo? userInfo = null;
            string? userInfoWarning = null;
            try
            {
                userInfo = await apiClient.GetUserInfoAsync();
            }
            catch (Exception ex)
            {
                userInfoWarning = ex.Message;
            }

            Console.WriteLine($"  Version:    {userInfo?.AppVersion ?? authResult.AppVersion ?? "unknown"}");
            Console.WriteLine($"  User:       {authResult.UserName} ({authResult.DisplayName})");

            if (settings.MockMode)
                Console.WriteLine($"  Status:     \u001b[32m✓ Connected (mock)\u001b[0m");
            else
                Console.WriteLine($"  Status:     \u001b[32m✓ Connected\u001b[0m");

            if (!string.IsNullOrWhiteSpace(userInfoWarning))
                Console.WriteLine($"  Warning:    Auth succeeded, but user info could not be read: {userInfoWarning}");

            Console.WriteLine();
            context.ExitCode = 0;
        });

        return testCommand;
    }

    private static Command CreateLogoutCommand()
    {
        var logoutCommand = new Command("logout", "Log out and remove any saved session token");

        logoutCommand.SetHandler(async (InvocationContext context) =>
        {
            var serviceProvider = context.BindingContext.GetRequiredService<IServiceProvider>();
            var authService = serviceProvider.GetRequiredService<IAdeptAuthService>();
            var sessionStore = serviceProvider.GetRequiredService<CliAuthSessionStore>();

            await authService.LogoutAsync(context.GetCancellationToken());
            sessionStore.Clear();

            Console.WriteLine();
            Console.WriteLine("  Status:     Logged out");
            Console.WriteLine();
            context.ExitCode = 0;
        });

        return logoutCommand;
    }
}
