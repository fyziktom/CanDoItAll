using CanDoItAll.AgentFramework.Models;
using System.Text;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessProviderRecoveryDirectiveBuilder
{
    public static AgentRecoveryDecision CreateRecoveryDecision(
        ProcessRunAutomationDispatchService.ProviderRepairOutcome repairOutcome,
        int attemptNumber,
        Guid executionRunId,
        DateTimeOffset nextAttemptAtUtc)
    {
        return AgentRecoveryDecisionFactory.Create(
            AgentFailureCategory.ProviderFailure,
            repairOutcome.FailureSummary,
            attemptNumber,
            executionRunId.ToString("D"),
            nextAttemptAtUtc: nextAttemptAtUtc);
    }

    public static string BuildDirective(
        string recoveryDirective,
        ProcessRunAutomationDispatchService.ProviderRepairOutcome repairOutcome)
    {
        var builder = new StringBuilder();
        builder.Append("Infrastructure recovery: the previous attempt hit a provider failure. ");
        builder.Append("Assigned internal agents using provider '")
            .Append(repairOutcome.FailedProviderName)
            .Append("' were moved to '")
            .Append(repairOutcome.FallbackProviderName)
            .Append("' with model '")
            .Append(repairOutcome.FallbackModel)
            .Append("'. ");
        builder.AppendLine($"Failure summary: {repairOutcome.FailureSummary}");

        if (!string.IsNullOrWhiteSpace(recoveryDirective))
        {
            builder.AppendLine(recoveryDirective.Trim());
        }

        return builder.ToString().Trim();
    }
}
