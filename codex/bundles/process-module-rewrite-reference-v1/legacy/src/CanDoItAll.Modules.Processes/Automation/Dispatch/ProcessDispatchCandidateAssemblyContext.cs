using CanDoItAll.AgentFramework.Models;
using DispatchArtifactExpectation = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.DispatchArtifactExpectation;
using DispatchArtifactInput = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.DispatchArtifactInput;
using DispatchBranchOutcome = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.DispatchBranchOutcome;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessDispatchCandidateAssemblyContext(
    ProcessRun Run,
    ProcessDefinition Definition,
    ProcessStepRun StepRun,
    ProcessStepDefinition StepDefinition,
    ProcessWorkBrief? WorkBrief,
    IReadOnlyList<DispatchArtifactExpectation> ExpectedArtifacts,
    HashSet<Guid> RecordedArtifactExpectationIds,
    IReadOnlyList<DispatchArtifactInput> ArtifactInputs,
    HashSet<string> ExternalReferenceKeys,
    IReadOnlyList<DispatchBranchOutcome> BranchOutcomes,
    bool RequiresExplicitBranchOutcomeSelection,
    ProcessDispatchDirectAgentCandidateFacts? DirectAgentFacts = null);

internal sealed record ProcessDispatchDirectAgentCandidateFacts(
    Guid TechnicalAgentId,
    Guid? ChatSessionId,
    Guid? RecoveryExecutionRunId,
    string ManualRecoveryDirective,
    AgentProcessCooperationMetadata CooperationMetadata);

internal static class ProcessDispatchCandidateAssemblyContextFactory
{
    public static ProcessDispatchCandidateAssemblyContext Create(
        ProcessRun run,
        ProcessDefinition definition,
        ProcessStepRun stepRun,
        ProcessStepDefinition stepDefinition,
        ProcessWorkBrief? workBrief,
        IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts,
        HashSet<Guid> recordedArtifactExpectationIds,
        IReadOnlyList<DispatchArtifactInput> artifactInputs,
        HashSet<string> externalReferenceKeys,
        ProcessDispatchBranchDependencyContext branchContext)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(stepRun);
        ArgumentNullException.ThrowIfNull(stepDefinition);
        ArgumentNullException.ThrowIfNull(expectedArtifacts);
        ArgumentNullException.ThrowIfNull(recordedArtifactExpectationIds);
        ArgumentNullException.ThrowIfNull(artifactInputs);
        ArgumentNullException.ThrowIfNull(externalReferenceKeys);
        ArgumentNullException.ThrowIfNull(branchContext);

        return new ProcessDispatchCandidateAssemblyContext(
            run,
            definition,
            stepRun,
            stepDefinition,
            workBrief,
            expectedArtifacts,
            recordedArtifactExpectationIds,
            artifactInputs,
            externalReferenceKeys,
            branchContext.BranchOutcomes,
            branchContext.RequiresExplicitBranchOutcomeSelection);
    }

    public static ProcessDispatchCandidateAssemblyContext WithDirectAgentFacts(
        ProcessDispatchCandidateAssemblyContext context,
        ProcessDispatchDirectAgentCandidateFacts directAgentFacts)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(directAgentFacts);

        if (directAgentFacts.TechnicalAgentId == Guid.Empty)
        {
            throw new ArgumentException("Direct-agent candidate requires a technical agent id.", nameof(directAgentFacts));
        }

        ArgumentNullException.ThrowIfNull(directAgentFacts.CooperationMetadata);

        return context with
        {
            DirectAgentFacts = directAgentFacts
        };
    }
}
