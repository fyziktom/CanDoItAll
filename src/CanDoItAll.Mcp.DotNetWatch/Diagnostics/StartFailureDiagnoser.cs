using CanDoItAll.Mcp.DotNetWatch.Logging;
using CanDoItAll.Mcp.DotNetWatch.Operations;
using CanDoItAll.Mcp.DotNetWatch.Runtime;

namespace CanDoItAll.Mcp.DotNetWatch.Diagnostics;

public sealed class StartFailureDiagnoser
{
    public DiagnoseStartFailureData Diagnose(AppSession? session, OperationRecord? operation, int maxLogEntries)
    {
        if (session is null && operation is null)
        {
            throw new ToolInvocationException("ValidationError", "No failed session or operation is available to diagnose.");
        }

        var targetType = session is not null ? "AppSession" : "Operation";
        var targetId = session?.SessionId ?? operation!.OperationId;
        var entries = (session is not null ? session.LogBuffer.GetLatest(maxLogEntries) : operation!.LogBuffer.GetLatest(maxLogEntries))
            .OrderByDescending(static entry => entry.Sequence)
            .ToList();

        var category = Classify(entries);
        var summary = category switch
        {
            DiagnosticCategory.PortInUse => "Application failed to bind one of the configured URLs because the port is already in use.",
            DiagnosticCategory.BuildFailed => "The managed build or test command failed before the runtime became healthy.",
            DiagnosticCategory.MissingSdk => "The .NET SDK required by the project could not be found.",
            DiagnosticCategory.HealthTimeout => "The runtime started, but the configured health probe did not report readiness in time.",
            DiagnosticCategory.ProcessExitedEarly => "The managed process exited before the requested lifecycle completed.",
            _ => "The managed process failed for an unclassified reason."
        };

        string[] recommendedActions = category switch
        {
            DiagnosticCategory.PortInUse =>
            [
                "Call candoitall_app_stop if another managed session is active.",
                "Call candoitall_cleanup_stale_processes to remove orphaned managed processes.",
                "Retry with a different URL override if the conflict is external."
            ],
            DiagnosticCategory.BuildFailed =>
            [
                "Read candoitall_app_logs or candoitall_operation_logs for compiler errors.",
                "Run candoitall_solution_build on the failing target to isolate the build problem."
            ],
            DiagnosticCategory.MissingSdk =>
            [
                "Check the installed .NET SDK versions on this machine.",
                "Verify the repo global.json matches an available SDK."
            ],
            DiagnosticCategory.HealthTimeout =>
            [
                "Inspect the recent app logs for runtime faults or slow startup paths.",
                "Verify the configured health URLs still match the development launch profile."
            ],
            DiagnosticCategory.ProcessExitedEarly =>
            [
                "Inspect the recent logs for the process exit reason.",
                "Retry the managed start after resolving the underlying error."
            ],
            _ =>
            [
                "Inspect the recent logs for more detail.",
                "Retry the managed command after resolving any reported errors."
            ]
        };

        return new DiagnoseStartFailureData(
            targetType,
            targetId,
            category,
            category == DiagnosticCategory.Unknown ? "Low" : "High",
            summary,
            recommendedActions,
            entries
                .Take(10)
                .Select(static entry => new DiagnosticEvidence(entry.Sequence, entry.Text))
                .ToArray());
    }

    private static DiagnosticCategory Classify(IEnumerable<LogEntry> entries)
    {
        var lines = entries.Select(static entry => entry.Text).ToArray();
        if (lines.Any(static line => line.Contains("address already in use", StringComparison.OrdinalIgnoreCase) ||
                                     line.Contains("Only one usage of each socket address", StringComparison.OrdinalIgnoreCase)))
        {
            return DiagnosticCategory.PortInUse;
        }

        if (lines.Any(static line => line.Contains("A compatible .NET SDK was not found", StringComparison.OrdinalIgnoreCase) ||
                                     line.Contains("could not execute because the specified command or file was not found", StringComparison.OrdinalIgnoreCase)))
        {
            return DiagnosticCategory.MissingSdk;
        }

        if (lines.Any(static line => line.Contains("Health probe did not succeed", StringComparison.OrdinalIgnoreCase)))
        {
            return DiagnosticCategory.HealthTimeout;
        }

        if (lines.Any(static line => line.Contains("Build FAILED", StringComparison.OrdinalIgnoreCase) ||
                                     line.Contains("error CS", StringComparison.OrdinalIgnoreCase) ||
                                     line.Contains("Test Run Failed", StringComparison.OrdinalIgnoreCase)))
        {
            return DiagnosticCategory.BuildFailed;
        }

        if (lines.Any(static line => line.Contains("exited with code", StringComparison.OrdinalIgnoreCase)))
        {
            return DiagnosticCategory.ProcessExitedEarly;
        }

        return DiagnosticCategory.Unknown;
    }
}
