using CanDoItAll.AgentFramework.Models;
using System.Text;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static string BuildDomainRecoveryFocusGuidance(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string? responseText,
        string missingConcreteImplementationProofSummary,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<ProcessAutomationToolExecutionReceipt> unresolvedCriticalToolFailures)
    {
        return string.Empty;
    }

    private static void AppendDomainImplementationRecoveryGuidance(
        StringBuilder builder,
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string? responseText,
        string missingConcreteImplementationProofSummary,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<ProcessAutomationToolExecutionReceipt> unresolvedCriticalToolFailures)
    {
    }

    private static void AppendDomainBrowserRecoveryGuidance(
        StringBuilder builder,
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string? responseText,
        string missingConcreteImplementationProofSummary,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<ProcessAutomationToolExecutionReceipt> unresolvedCriticalToolFailures)
    {
    }
}
