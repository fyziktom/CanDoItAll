using System.Text.Json;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.SharedProviders.E2E;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        E2eInvocation? invocation = null;
        string? commandToken = null;
        string? roleToken = null;
        try
        {
            if (E2ePreparationCommandLine.IsPrepareCommand(args))
            {
                commandToken = "prepare";
                var preparationOptions = E2ePreparationCommandLine.Parse(args);
                await new E2ePreparationService().PrepareAsync(
                    preparationOptions,
                    cancellation.Token);
                WriteResult(new E2eCommandResult(
                    commandToken,
                    Role: null,
                    "succeeded",
                    Error: null));
                return 0;
            }

            if (E2eScenarioCommandLine.IsScenarioCommand(args))
            {
                commandToken = "run-scenarios";
                var scenarioOptions = E2eScenarioCommandLine.Parse(args);
                roleToken = E2eScenarioCommandLine.ToToken(scenarioOptions.Phase);
                using var runner = new E2eScenarioRunner(scenarioOptions);
                await runner.RunAsync(cancellation.Token);
                WriteResult(new E2eCommandResult(
                    commandToken,
                    roleToken,
                    "succeeded",
                    Error: null));
                return 0;
            }

            invocation = E2eCommandLine.Parse(args);
            commandToken = E2eCommandLine.ToToken(invocation.Command);
            roleToken = E2eCommandLine.ToToken(invocation.Role);
            await using var host = await E2eServiceHost.CreateAsync(
                invocation.Options,
                cancellation.Token);
            await using var scope = host.Services.CreateAsyncScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<E2eOrchestrator>();
            await orchestrator.ExecuteAsync(
                invocation.Command,
                invocation.Role,
                cancellation.Token);
            WriteResult(new E2eCommandResult(
                commandToken,
                roleToken,
                "succeeded",
                Error: null));
            return 0;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            WriteResult(new E2eCommandResult(
                commandToken,
                roleToken,
                "cancelled",
                "The E2E command was cancelled."));
            return 2;
        }
        catch (E2eSafeException exception)
        {
            WriteResult(new E2eCommandResult(
                commandToken,
                roleToken,
                "failed",
                SensitiveTextRedactor.Redact(exception.Message)));
            return 1;
        }
        catch
        {
            WriteResult(new E2eCommandResult(
                commandToken,
                roleToken,
                "failed",
                "The E2E command failed before producing a sanitized handoff."));
            return 1;
        }
    }

    private static void WriteResult(E2eCommandResult result)
        => Console.Out.WriteLine(JsonSerializer.Serialize(result));
}

internal sealed record E2eCommandResult(
    string? Command,
    string? Role,
    string Status,
    string? Error);
