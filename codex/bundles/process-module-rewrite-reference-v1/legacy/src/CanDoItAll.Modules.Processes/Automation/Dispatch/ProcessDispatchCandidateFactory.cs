using CanDoItAll.AgentFramework.Models;
using DispatchCandidate = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.DispatchCandidate;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessDispatchCandidateFactory
{
    public static DispatchCandidate CreateSubprocessCandidate(ProcessDispatchCandidateAssemblyContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return CreateCandidate(
            context,
            Guid.Empty,
            null,
            null,
            string.Empty,
            new AgentProcessCooperationMetadata(
                AgentProcessCooperationMode.ProcessArtifactHandoff,
                AgentWorkspaceToolProfileKind.ReadOnly,
                "Subprocess step is orchestrated by the process runtime."));
    }

    public static DispatchCandidate CreateWorkflowCandidate(ProcessDispatchCandidateAssemblyContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return CreateCandidate(
            context,
            Guid.Empty,
            null,
            null,
            string.Empty,
            new AgentProcessCooperationMetadata(
                AgentProcessCooperationMode.ProcessArtifactHandoff,
                AgentWorkspaceToolProfileKind.ReadOnly,
                "Workflow step is orchestrated through the Microsoft Agent Framework workflow runtime."));
    }

    public static DispatchCandidate CreateDirectAgentCandidate(ProcessDispatchCandidateAssemblyContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var facts = context.DirectAgentFacts
            ?? throw new InvalidOperationException("Direct-agent candidate facts are required.");
        return CreateCandidate(
            context,
            facts.TechnicalAgentId,
            facts.ChatSessionId,
            facts.RecoveryExecutionRunId,
            facts.ManualRecoveryDirective,
            facts.CooperationMetadata);
    }

    private static DispatchCandidate CreateCandidate(
        ProcessDispatchCandidateAssemblyContext context,
        Guid technicalAgentId,
        Guid? chatSessionId,
        Guid? recoveryExecutionRunId,
        string manualRecoveryDirective,
        AgentProcessCooperationMetadata cooperationMetadata)
    {
        return new DispatchCandidate(
            context.Run,
            context.Definition,
            context.StepRun,
            context.StepDefinition,
            context.WorkBrief,
            technicalAgentId,
            context.ExpectedArtifacts,
            context.RecordedArtifactExpectationIds,
            context.ArtifactInputs,
            context.ExternalReferenceKeys,
            chatSessionId,
            recoveryExecutionRunId,
            manualRecoveryDirective,
            context.BranchOutcomes,
            context.RequiresExplicitBranchOutcomeSelection,
            cooperationMetadata);
    }
}
