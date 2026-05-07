using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private sealed record DispatchCandidate(
        ProcessRun Run,
        ProcessDefinition Definition,
        ProcessStepRun StepRun,
        ProcessStepDefinition StepDefinition,
        ProcessWorkBrief? WorkBrief,
        Guid TechnicalAgentId,
        IReadOnlyList<DispatchArtifactExpectation> ExpectedArtifacts,
        IReadOnlySet<Guid> RecordedArtifactExpectationIds,
        IReadOnlyList<DispatchArtifactInput> ArtifactInputs,
        HashSet<string> ExternalReferenceKeys,
        Guid? ChatSessionId,
        Guid? RecoveryExecutionRunId,
        string ManualRecoveryDirective,
        IReadOnlyList<DispatchBranchOutcome> BranchOutcomes,
        bool RequiresExplicitBranchOutcomeSelection,
        AgentProcessCooperationMetadata CooperationMetadata);

    private sealed record ConcurrentAutomationExecution(
        Guid ExecutionRunId,
        ExecutionRunDetail Detail,
        string ResponseText);

    private sealed record StepRunTransitionSnapshot(
        Guid Id,
        ProcessStepRunStatus Status,
        Guid ConcurrencyToken);

    internal sealed record DispatchArtifactExpectation(
        Guid Id,
        ProcessArtifactKind ArtifactKind,
        string Title,
        bool IsRequired,
        ProcessArtifactTrustRequirement TrustRequirement,
        ProcessSensitivityLevel SensitivityLevel,
        string ValidationRequirementSummary,
        string AllowedFutureUsageSummary);

internal sealed record ProjectStructureRequiredArtifactPath(
    string FileName,
    string AliasPath);

internal sealed record SubprocessCapabilityGapStep(
    string Title,
    ProcessStepRunStatus Status,
    ProcessCapabilityGapSeverity CapabilityGapSeverity,
    Guid? CurrentExecutorPartyId,
    string CurrentExecutorName);

    private sealed record DispatchArtifactInput(
        string SourceStepTitle,
        string ExpectedArtifactTitle,
        IReadOnlyList<DispatchArtifactReference> Artifacts);

    private sealed record DispatchArtifactReference(
        string Title,
        string ArtifactKind,
        string ManagedStoragePath,
        string ReviewSummary,
        string ProvenanceSummary);

    private sealed record GovernedInspectionPaths(
        IReadOnlyList<string> StatPaths,
        IReadOnlyList<string> ReadPaths);

    private sealed record DispatchExecutionOutcome(
        ExecutionRunDetail Detail,
        string ResponseText,
        ProcessStepRunStatus CompletionStatus,
        string CompletionReason,
        IReadOnlyList<string> MissingRequiredTools,
        int AttemptNumber,
        Guid? SelectedBranchOutcomeId);

    private readonly record struct CarriedImplementationProof(
        bool HasConcreteImplementationProof,
        bool HasRunnableApplicationProof,
        bool HasConcreteProductMutation)
    {
        public static CarriedImplementationProof None { get; } = new(false, false, false);
    }

    private sealed record ProviderFallbackResolution(
        ProviderProfile Provider,
        string Model,
        string HealthSummary);

    private sealed record ProviderRepairOutcome(
        string FailedProviderName,
        string FallbackProviderName,
        string FallbackModel,
        int AffectedAgentCount,
        string FailureSummary);

    private sealed record ProcessAutomationDatabaseRequirementFailure(string Message);

    private readonly record struct DeclaredStepOutcome(
        ProcessStepRunStatus Status,
        string Reason,
        Guid? SelectedBranchOutcomeId,
        string BranchOutcomeKey,
        string BranchOutcomeTitle);

    private sealed record DispatchBranchOutcome(
        Guid Id,
        string Key,
        string Title,
        string Description);

    private readonly record struct ProcessMockArtifactProjection(
        string RoleKey,
        string? BranchOutcomeKey,
        string RelativePath,
        string ContentSignalText);

    private sealed record SessionToolCall(string ToolName, string OutputFileName);

    private sealed record SessionToolResultText(string ToolName, string Text);

    private sealed record SessionFileContent(string Path, string Content);
}
