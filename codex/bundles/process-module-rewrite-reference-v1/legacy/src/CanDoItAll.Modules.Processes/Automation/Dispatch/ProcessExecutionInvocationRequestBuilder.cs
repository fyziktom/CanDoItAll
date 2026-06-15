using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Contracts;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessExecutionInvocationRequestBuilder
{
    internal static ProcessAutomationExecutionRequest Build(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        string prompt,
        string trigger,
        string correlationId,
        string metadataJson,
        ExecutionInvocationPolicy processInvocationPolicy)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentNullException.ThrowIfNull(processInvocationPolicy);

        return new ProcessAutomationExecutionRequest(
            candidate.TechnicalAgentId,
            prompt,
            new ProcessAutomationInvocationSource(
                SourceKind: "process-step",
                SourceId: candidate.StepRun.Id.ToString("D"),
                CorrelationId: correlationId,
                CausationId: string.IsNullOrWhiteSpace(trigger)
                    ? string.Empty
                    : trigger.Trim(),
                RequestedBy: ProcessRunAutomationDispatchService.AutomationActor,
                RequestedByKind: "system",
                MetadataJson: metadataJson,
                ProcessRunId: candidate.Run.Id.ToString("D"),
                ProcessStepId: candidate.StepRun.Id.ToString("D")),
            new ProcessAutomationInvocationPolicy(
                ProcessAutomationFinalizerMode.Required,
                processInvocationPolicy.MaxStructuredOutputRepairAttempts,
                processInvocationPolicy.RequireStructuredOutputValidation),
            AutoApprovePendingToolCalls: true,
            StructuredOutputKind: ProcessAutomationStructuredOutputKind.ProcessStepOutcomeResult);
    }
}
