using CanDoItAll.AgentFramework.Models;
using System.Text;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static string BuildDomainRecoveryFocusGuidance(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string? responseText,
        string missingConcreteImplementationProofSummary,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<ToolExecutionReceiptRecord> unresolvedCriticalToolFailures)
    {
        return string.Empty;
    }

    private static void AppendDomainImplementationRecoveryGuidance(
        StringBuilder builder,
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string? responseText,
        string missingConcreteImplementationProofSummary,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<ToolExecutionReceiptRecord> unresolvedCriticalToolFailures)
    {
    }

    private static void AppendDomainBrowserRecoveryGuidance(
        StringBuilder builder,
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string? responseText,
        string missingConcreteImplementationProofSummary,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<ToolExecutionReceiptRecord> unresolvedCriticalToolFailures)
    {
    }
}
