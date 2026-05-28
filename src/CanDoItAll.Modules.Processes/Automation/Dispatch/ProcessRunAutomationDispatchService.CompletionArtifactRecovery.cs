using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    internal sealed record ManagerArtifactRecoveryAgent(
        Guid TechnicalAgentId,
        string DisplayName,
        string ResolutionSource);

    private async Task<DispatchExecutionOutcome> TryRecoverMissingCompletionArtifactsAsync(
        DispatchCandidate candidate,
        DispatchExecutionOutcome executionOutcome,
        string trigger,
        ProcessStepDispatchClaim dispatchClaim,
        Func<CancellationToken, Task>? renewLeaseAsync,
        CancellationToken cancellationToken)
    {
        if (executionOutcome.CompletionStatus != ProcessStepRunStatus.Completed)
        {
            return executionOutcome;
        }

        var missingArtifacts = ResolveMissingRequiredCompletionArtifacts(candidate);
        if (missingArtifacts.Count == 0)
        {
            return executionOutcome;
        }

        return await RecoverMissingCompletionArtifactsWithManagerAsync(
            candidate,
            executionOutcome,
            missingArtifacts,
            trigger,
            dispatchClaim,
            renewLeaseAsync,
            cancellationToken);
    }

    private async Task<DispatchExecutionOutcome?> TryRecoverStrandedMissingCompletionArtifactsAsync(
        DispatchCandidate candidate,
        string trigger,
        ProcessStepDispatchClaim dispatchClaim,
        Func<CancellationToken, Task>? renewLeaseAsync,
        CancellationToken cancellationToken)
    {
        if (!ShouldAttemptManagerArtifactRecoveryForStrandedStep(
                candidate.StepRun.Status,
                candidate.RecoveryExecutionRunId,
                candidate.ExpectedArtifacts,
                candidate.RecordedArtifactExpectationIds))
        {
            return null;
        }

        var previousExecutionRunId = candidate.RecoveryExecutionRunId!.Value;
        var previousExecutionDetail = await workspaceService.GetExecutionRunDetailAsync(
            previousExecutionRunId,
            cancellationToken);
        var responseText = await ResolveRecoveredExecutionResponseTextWithCurrentRunArtifactsAsync(
            candidate,
            previousExecutionDetail,
            cancellationToken);
        var dispositionRecoveryOutcome = await TryRecoverStrandedDispositionArtifactsAsync(
            candidate,
            previousExecutionDetail,
            responseText,
            dispatchClaim,
            cancellationToken);
        if (dispositionRecoveryOutcome is not null)
        {
            return dispositionRecoveryOutcome;
        }

        var syntheticOutcome = new DispatchExecutionOutcome(
            previousExecutionDetail,
            responseText,
            ProcessStepRunStatus.Completed,
            $"Prior automation execution run {previousExecutionRunId:D} left required artifact records missing; process manager artifact recovery was requested instead of rerunning the step executor.",
            [],
            AttemptNumber: 1,
            SelectedBranchOutcomeId: null);
        var missingArtifacts = ResolveMissingRequiredCompletionArtifacts(candidate);

        logger.LogWarning(
            "Stranded process run {RunId}, step {StepRunId} has recoverable execution run {ExecutionRunId} and missing required artifacts: {MissingArtifacts}. Asking the process manager for targeted artifact recovery instead of starting another step executor attempt.",
            candidate.Run.Id,
            candidate.StepRun.Id,
            previousExecutionRunId,
            FormatMissingArtifactTitles(missingArtifacts));

        return await RecoverMissingCompletionArtifactsWithManagerAsync(
            candidate,
            syntheticOutcome,
            missingArtifacts,
            trigger,
            dispatchClaim,
            renewLeaseAsync,
            cancellationToken);
    }

    private async Task<DispatchExecutionOutcome?> TryRecoverStrandedDispositionArtifactsAsync(
        DispatchCandidate candidate,
        ExecutionRunDetail previousExecutionDetail,
        string responseText,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        if (!ShouldAttemptStrandedDispositionArtifactFinalization(
                candidate,
                previousExecutionDetail.Run,
                responseText,
                clock.GetUtcNow()))
        {
            return null;
        }

        logger.LogWarning(
            "Stranded process run {RunId}, step {StepRunId} has execution run {ExecutionRunId} with durable current-run artifacts and explicit disposition text. Projecting artifacts and finalizing from the governed disposition instead of starting manager recovery.",
            candidate.Run.Id,
            candidate.StepRun.Id,
            previousExecutionDetail.Run.Id);

        await ProjectExecutionArtifactsAsync(
            candidate,
            previousExecutionDetail,
            responseText,
            ProcessStepRunStatus.Failed,
            dispatchClaim,
            cancellationToken);

        if (HasMissingRequiredCompletionArtifacts(candidate.ExpectedArtifacts, candidate.RecordedArtifactExpectationIds))
        {
            logger.LogWarning(
                "Stranded disposition artifact recovery for run {RunId}, step {StepRunId}, execution run {ExecutionRunId} could not satisfy all required artifact records after projection. Manager recovery remains eligible.",
                candidate.Run.Id,
                candidate.StepRun.Id,
                previousExecutionDetail.Run.Id);
            return null;
        }

        return new DispatchExecutionOutcome(
            previousExecutionDetail,
            responseText,
            ProcessStepRunStatus.Failed,
            $"Prior automation execution run {previousExecutionDetail.Run.Id:D} produced the required current-run artifacts and explicit branch disposition, but failed before the structured finalizer completed; process-owned recovery will finalize from the durable artifacts.",
            [],
            AttemptNumber: 1,
            SelectedBranchOutcomeId: null);
    }

    internal static bool ShouldAttemptStrandedDispositionArtifactFinalization(
        DispatchCandidate candidate,
        ExecutionRunRecord executionRun,
        string? responseText,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(executionRun);

        return executionRun.PendingApprovals.Count == 0 &&
               (IsTerminalAutomationExecutionRun(executionRun) || IsStaleAutomationExecutionRun(executionRun, now)) &&
               candidate.RequiresExplicitBranchOutcomeSelection &&
               candidate.BranchOutcomes.Count > 0 &&
               ResolveMissingUpstreamArtifactInputs(candidate).Count == 0 &&
               IsDispositionRoutingStep(candidate) &&
               TryResolveExplicitDispositionBranchOutcome(candidate, responseText, out _);
    }

    private async Task<string> ResolveRecoveredExecutionResponseTextWithCurrentRunArtifactsAsync(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        CancellationToken cancellationToken)
    {
        var recoveredText = ResolveRecoveredExecutionResponseText(detail);
        if (!candidate.RequiresExplicitBranchOutcomeSelection ||
            TryResolveExplicitDispositionBranchOutcome(candidate, recoveredText, out _))
        {
            return recoveredText;
        }

        var workspaceRoot = Path.GetFullPath(workspacePathResolver.ResolveWorkspaceRoot());
        var workspaceScope = WorkspaceScopeDescriptor.Organization(
            databaseProfileRuntimeAccessor.ResolveCurrentProfile().Profile.Id.ToString("N"));
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(recoveredText))
        {
            builder.AppendLine(recoveredText.Trim());
        }

        foreach (var expectedArtifact in candidate.ExpectedArtifacts.Where(artifact => artifact.IsRequired))
        {
            foreach (var relativePath in ResolveExpectedManagedArtifactRelativePaths(candidate, workspaceScope, expectedArtifact))
            {
                if (!IsTextReadableManagedArtifactPath(relativePath) ||
                    !TryResolveArtifactFullPath(workspaceRoot, relativePath, out var fullPath, out _) ||
                    !File.Exists(fullPath))
                {
                    continue;
                }

                string content;
                try
                {
                    content = await File.ReadAllTextAsync(fullPath, cancellationToken);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    logger.LogWarning(
                        exception,
                        "Could not read current-run artifact file {RelativePath} while recovering response text for process run {RunId}, step {StepRunId}.",
                        relativePath,
                        candidate.Run.Id,
                        candidate.StepRun.Id);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(content))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.AppendLine($"Current-run artifact '{expectedArtifact.Title}' ({relativePath}):");
                builder.AppendLine(content.Length > 128_000 ? content[..128_000] : content);
            }
        }

        return builder.Length == 0
            ? recoveredText
            : builder.ToString();
    }

    internal static bool ShouldAttemptManagerArtifactRecoveryForStrandedStep(
        ProcessStepRunStatus stepStatus,
        Guid? recoveryExecutionRunId,
        IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts,
        IReadOnlySet<Guid> recordedArtifactExpectationIds)
    {
        ArgumentNullException.ThrowIfNull(expectedArtifacts);
        ArgumentNullException.ThrowIfNull(recordedArtifactExpectationIds);

        return stepStatus == ProcessStepRunStatus.InProgress &&
               recoveryExecutionRunId.HasValue &&
               HasMissingRequiredCompletionArtifacts(expectedArtifacts, recordedArtifactExpectationIds);
    }

    internal static Guid? ResolveArtifactRecoveryExecutionRunId(
        ProcessStepRun stepRun,
        IReadOnlyList<ExecutionRunRecord> executionRuns,
        IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts,
        IReadOnlySet<Guid> recordedArtifactExpectationIds,
        DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(stepRun);
        ArgumentNullException.ThrowIfNull(executionRuns);
        ArgumentNullException.ThrowIfNull(expectedArtifacts);
        ArgumentNullException.ThrowIfNull(recordedArtifactExpectationIds);

        if (stepRun.Status != ProcessStepRunStatus.InProgress ||
            !HasMissingRequiredCompletionArtifacts(expectedArtifacts, recordedArtifactExpectationIds))
        {
            return null;
        }

        var resolvedNow = now ?? DateTimeOffset.UtcNow;
        return executionRuns
            .Where(executionRun =>
                string.Equals(executionRun.RequestedBy, AutomationActor, StringComparison.OrdinalIgnoreCase) &&
                (executionRun.State is ExecutionState.Completed or ExecutionState.Failed ||
                 IsStaleAutomationExecutionRun(executionRun, resolvedNow)))
            .OrderByDescending(executionRun => executionRun.CompletedAtUtc ?? executionRun.UpdatedAtUtc)
            .ThenByDescending(executionRun => executionRun.UpdatedAtUtc)
            .ThenByDescending(executionRun => executionRun.CreatedAtUtc)
            .Select(executionRun => (Guid?)executionRun.Id)
            .FirstOrDefault();
    }

    internal static bool ShouldReusePriorArtifactRecoveryExecutionRun(string trigger)
        => !string.Equals(
            trigger?.Trim(),
            ProcessRuntimeEventTypes.ManualAgentStepRerun,
            StringComparison.OrdinalIgnoreCase);

    private static bool HasMissingRequiredCompletionArtifacts(
        IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts,
        IReadOnlySet<Guid> recordedArtifactExpectationIds)
    {
        return expectedArtifacts.Any(artifact =>
            artifact.IsRequired &&
            !recordedArtifactExpectationIds.Contains(artifact.Id));
    }

    private async Task<DispatchExecutionOutcome> RecoverMissingCompletionArtifactsWithManagerAsync(
        DispatchCandidate candidate,
        DispatchExecutionOutcome executionOutcome,
        IReadOnlyList<DispatchArtifactExpectation> missingArtifacts,
        string trigger,
        ProcessStepDispatchClaim dispatchClaim,
        Func<CancellationToken, Task>? renewLeaseAsync,
        CancellationToken cancellationToken)
    {
        var missingArtifactSummary = FormatMissingArtifactTitles(missingArtifacts);
        var recoveryReason = $"Required artifacts remained missing after execution run {executionOutcome.Detail.Run.Id:D}: {missingArtifactSummary}.";
        var recoveryDecision = AgentRecoveryDecisionFactory.Create(
            AgentFailureCategory.ArtifactMissing,
            recoveryReason,
            executionOutcome.AttemptNumber,
            executionOutcome.Detail.Run.Id.ToString("D"),
            nextAttemptAtUtc: clock.GetUtcNow());
        var recoveryPacket = CreateReworkPacketForDecision(
            candidate,
            executionOutcome.Detail,
            recoveryDecision,
            [],
            [],
            clock.GetUtcNow());
        if (recoveryPacket is not null)
        {
            recoveryDecision = recoveryDecision with
            {
                Mode = AgentRecoveryMode.ReworkContinuation,
                ReworkPacketId = recoveryPacket.Id
            };
        }

        await PersistRecoveryJournalAsync(
            candidate,
            recoveryDecision,
            recoveryPacket,
            providerFallbackCount: 0,
            cancellationToken);

        logger.LogWarning(
            "AgentFramework execution run {ExecutionRunId} for process run {RunId}, step {StepRunId} left required artifacts missing: {MissingArtifacts}. Asking the process manager for targeted artifact recovery before step transition.",
            executionOutcome.Detail.Run.Id,
            candidate.Run.Id,
            candidate.StepRun.Id,
            missingArtifactSummary);

        var managerRecoveryAgent = await ResolveManagerArtifactRecoveryAgentAsync(candidate, cancellationToken);
        var recoveryDirective = BuildTypedRecoveryDirective(
            recoveryDecision,
            recoveryPacket,
            BuildCompletionArtifactRecoveryDirective(candidate, executionOutcome, missingArtifacts));
        var directiveRecordingFailure = await TryRecordManagerArtifactRecoveryDirectiveAsync(
            candidate,
            recoveryDirective,
            cancellationToken);
        if (!string.IsNullOrWhiteSpace(directiveRecordingFailure))
        {
            return BlockMissingCompletionArtifacts(
                executionOutcome,
                missingArtifacts,
                $"the manager recovery directive could not be recorded: {directiveRecordingFailure}");
        }

        if (managerRecoveryAgent is null)
        {
            logger.LogWarning(
                "Missing completion artifact recovery for process run {RunId}, step {StepRunId} could not start because no bound process manager technical agent was resolved.",
                candidate.Run.Id,
                candidate.StepRun.Id);
            return BlockMissingCompletionArtifacts(
                executionOutcome,
                missingArtifacts,
                "no bound process manager technical agent could be resolved");
        }

        if (managerRecoveryAgent.TechnicalAgentId == candidate.TechnicalAgentId)
        {
            logger.LogWarning(
                "Missing completion artifact recovery for process run {RunId}, step {StepRunId} resolved manager agent {TechnicalAgentId}, but it is the same technical agent as the current executor.",
                candidate.Run.Id,
                candidate.StepRun.Id,
                managerRecoveryAgent.TechnicalAgentId);
            return BlockMissingCompletionArtifacts(
                executionOutcome,
                missingArtifacts,
                "the resolved process manager technical agent is the same as the current step executor");
        }

        logger.LogInformation(
            "Process manager artifact recovery for run {RunId}, step {StepRunId} will use manager agent {ManagerAgentName} ({TechnicalAgentId}) resolved by {ResolutionSource}.",
            candidate.Run.Id,
            candidate.StepRun.Id,
            managerRecoveryAgent.DisplayName,
            managerRecoveryAgent.TechnicalAgentId,
            managerRecoveryAgent.ResolutionSource);

        var recoveryCandidate = candidate with
        {
            StepRun = CreateManagerArtifactRecoveryStepRun(candidate.StepRun, managerRecoveryAgent.DisplayName),
            StepDefinition = CreateManagerArtifactRecoveryStepDefinition(candidate.StepDefinition),
            WorkBrief = CreateManagerArtifactRecoveryWorkBrief(candidate.WorkBrief, missingArtifactSummary),
            TechnicalAgentId = managerRecoveryAgent.TechnicalAgentId,
            ChatSessionId = null,
            RecoveryExecutionRunId = null,
            ManualRecoveryDirective = recoveryDirective
        };
        var recoveryOutcome = await ExecuteUntilSettledAsync(
            recoveryCandidate,
            $"{NormalizeTrigger(trigger, candidate.StepRun.Id)}:completion-artifact-recovery",
            renewLeaseAsync,
            cancellationToken);

        await ProjectExecutionArtifactsAsync(
            recoveryCandidate,
            recoveryOutcome.Detail,
            recoveryOutcome.ResponseText,
            recoveryOutcome.CompletionStatus,
            dispatchClaim,
            cancellationToken,
            new ArtifactProjectionLineage(
                recoveryOutcome.Detail.Run.Id,
                executionOutcome.Detail.Run.Id,
                recoveryDecision.ReworkPacketId));

        var remainingArtifacts = ResolveMissingRequiredCompletionArtifacts(candidate);
        if (remainingArtifacts.Count == 0)
        {
            return recoveryOutcome with
            {
                CompletionStatus = ProcessStepRunStatus.Completed,
                CompletionReason = $"{recoveryOutcome.CompletionReason} Targeted artifact recovery recorded the previously missing required artifact(s): {missingArtifactSummary}."
            };
        }

        var remainingArtifactSummary = FormatMissingArtifactTitles(remainingArtifacts);
        logger.LogWarning(
            "Process manager artifact recovery for process run {RunId}, step {StepRunId} did not record all required artifacts. Remaining artifacts: {RemainingArtifacts}.",
            candidate.Run.Id,
            candidate.StepRun.Id,
            remainingArtifactSummary);

        return recoveryOutcome with
        {
            CompletionStatus = ProcessStepRunStatus.Blocked,
            CompletionReason = $"Required artifacts remain missing after process manager artifact recovery: {remainingArtifactSummary}. The step is blocked instead of retrying the implementation executor again. Manager recovery outcome: {recoveryOutcome.CompletionReason}",
            SelectedBranchOutcomeId = null
        };
    }

    private static ProcessStepRun CreateManagerArtifactRecoveryStepRun(
        ProcessStepRun source,
        string managerDisplayName)
    {
        return new ProcessStepRun
        {
            Id = source.Id,
            ProcessRunId = source.ProcessRunId,
            StepDefinitionId = source.StepDefinitionId,
            Sequence = source.Sequence,
            Title = source.Title,
            StepKind = ProcessStepKind.Review,
            Status = source.Status,
            RoleSnapshotSummary = source.RoleSnapshotSummary,
            CurrentExecutorName = managerDisplayName,
            CurrentExecutorPartyId = source.CurrentExecutorPartyId,
            DecisionSummary = source.DecisionSummary,
            BlockedReason = source.BlockedReason,
            BlockReasonCode = source.BlockReasonCode,
            RecoveryOptionsJson = source.RecoveryOptionsJson,
            NextRecoveryAction = source.NextRecoveryAction,
            RefusalReason = source.RefusalReason,
            ExceptionSummary = source.ExceptionSummary,
            InputQualitySummary = source.InputQualitySummary,
            SelectedBranchOutcomeId = source.SelectedBranchOutcomeId,
            SelectedBranchOutcomeTitle = source.SelectedBranchOutcomeTitle,
            ReadyAtUtc = source.ReadyAtUtc,
            StartedAtUtc = source.StartedAtUtc,
            CompletedAtUtc = source.CompletedAtUtc,
            WaitMinutes = source.WaitMinutes,
            TouchMinutes = source.TouchMinutes,
            BlockedMinutes = source.BlockedMinutes,
            ReworkCount = source.ReworkCount,
            CapabilityGapSeverity = source.CapabilityGapSeverity,
            ConcurrencyToken = source.ConcurrencyToken
        };
    }

    private static ProcessStepDefinition CreateManagerArtifactRecoveryStepDefinition(ProcessStepDefinition source)
    {
        return new ProcessStepDefinition
        {
            Id = source.Id,
            ProcessDefinitionVersionId = source.ProcessDefinitionVersionId,
            Key = source.Key,
            Title = source.Title,
            Subtitle = source.Subtitle,
            Notes = source.Notes,
            StepKind = ProcessStepKind.Review,
            SubprocessDefinitionId = source.SubprocessDefinitionId,
            SubprocessDefinitionSnapshotName = source.SubprocessDefinitionSnapshotName,
            AllowsManualSkip = source.AllowsManualSkip,
            AllowsSafeRefusal = source.AllowsSafeRefusal,
            RequiresApproval = source.RequiresApproval,
            RequiresDecisionRecord = source.RequiresDecisionRecord,
            InputContractSummary = "Recover missing process artifact records from existing run history.",
            OutputContractSummary = "Missing artifact records are created from existing evidence, or the exact evidence gap is blocked.",
            EvidenceContractSummary = source.EvidenceContractSummary,
            DecisionRightsSummary = source.DecisionRightsSummary,
            ExceptionPolicySummary = source.ExceptionPolicySummary,
            TargetLeadHours = source.TargetLeadHours,
            OrderIndex = source.OrderIndex,
            DecisionRoleRequirementId = source.DecisionRoleRequirementId,
            CanvasX = source.CanvasX,
            CanvasY = source.CanvasY,
            BranchCanvasX = source.BranchCanvasX,
            BranchCanvasY = source.BranchCanvasY
        };
    }

    private static ProcessWorkBrief? CreateManagerArtifactRecoveryWorkBrief(
        ProcessWorkBrief? source,
        string missingArtifactSummary)
    {
        if (source is null)
        {
            return null;
        }

        return new ProcessWorkBrief
        {
            Id = source.Id,
            ProcessRunId = source.ProcessRunId,
            StepRunId = source.StepRunId,
            Title = source.Title,
            WorkBriefText = $"Recover missing artifact records only: {missingArtifactSummary}. Use existing run history and evidence; do not implement product changes.",
            HandoffSummary = source.HandoffSummary,
            AssignmentReason = source.AssignmentReason,
            ExpectedOutcome = "Missing artifact records are recovered from existing evidence, or blocked with exact evidence gaps.",
            EvidenceExpectationSummary = source.EvidenceExpectationSummary,
            CreatedAtUtc = source.CreatedAtUtc
        };
    }

    private static IReadOnlyList<DispatchArtifactExpectation> ResolveMissingRequiredCompletionArtifacts(DispatchCandidate candidate)
    {
        return candidate.ExpectedArtifacts
            .Where(artifact => artifact.IsRequired)
            .Where(artifact => !candidate.RecordedArtifactExpectationIds.Contains(artifact.Id))
            .ToList();
    }

    private static string BuildCompletionArtifactRecoveryDirective(
        DispatchCandidate candidate,
        DispatchExecutionOutcome executionOutcome,
        IReadOnlyList<DispatchArtifactExpectation> missingArtifacts)
    {
        var builder = new StringBuilder();
        var missingArtifactSummary = FormatMissingArtifactTitles(missingArtifacts);
        builder.AppendLine("Targeted completion-artifact recovery is required.");
        builder.AppendLine("You are the process manager for this process run. Recover the missing artifact record(s) from prior step history and current attempt history.");
        builder.AppendLine($"The previous AgentFramework execution run for step '{candidate.StepRun.Title}' left the process without required artifact record(s): {missingArtifactSummary}.");
        builder.AppendLine($"Process run id: {candidate.Run.Id:D}.");
        builder.AppendLine($"Step run id: {candidate.StepRun.Id:D}.");
        builder.AppendLine($"Previous execution run id: {executionOutcome.Detail.Run.Id:D}.");
        builder.AppendLine("Inspect previous step history information, upstream artifact records, finalizer summaries, tool receipts, changed files, and current-run evidence before writing recovery artifacts.");
        builder.AppendLine("Use current-run records, upstream artifacts, tool receipts, changed files, build/test/run logs, and project-structure context to create only the missing handoff artifact(s).");
        builder.AppendLine("Do not delegate this back to the implementation executor solely because artifact records are missing.");
        builder.AppendLine("Do not repeat broad implementation work, regenerate unrelated product files, or restart from the original request when the existing current-run evidence is enough.");
        builder.AppendLine("If product files or validation evidence are insufficient to produce an honest artifact, return Blocked with the exact missing record, source file, tool receipt, or upstream decision instead of inventing evidence.");
        builder.AppendLine("If the artifact can be produced, write it as a durable current-run artifact with workspace_write_file and then return a valid ProcessStepOutcomeResult with Status Completed.");

        var governedInspectionPaths = ResolveGovernedInspectionPaths(missingArtifacts);
        if (governedInspectionPaths.StatPaths.Count > 0)
        {
            builder.AppendLine($"Expected artifact path(s) to stat after writing: {FormatPromptPathList(governedInspectionPaths.StatPaths)}.");
        }

        if (governedInspectionPaths.ReadPaths.Count > 0)
        {
            builder.AppendLine($"Expected text artifact path(s) to read back after writing: {FormatPromptPathList(governedInspectionPaths.ReadPaths)}.");
        }
        else
        {
            builder.AppendLine($"Use the current-run managed artifact root for new evidence files: {BuildCurrentRunManagedArtifactRoot(candidate)}.");
        }

        if (candidate.ArtifactInputs.Count > 0)
        {
            builder.AppendLine("Inspect upstream artifacts and their source step history before writing recovery artifacts when they contain the source contract or prior-step outputs.");
        }

        foreach (var input in candidate.ArtifactInputs.Take(8))
        {
            var sourceStepStatus = input.SourceStepRunStatus?.ToString() ?? "unknown";
            builder.AppendLine($"Previous step history source: '{input.SourceStepTitle}' supplied expected artifact '{input.ExpectedArtifactTitle}' with source step status {sourceStepStatus}.");
            foreach (var artifact in input.Artifacts.Take(4))
            {
                if (!string.IsNullOrWhiteSpace(artifact.ManagedStoragePath))
                {
                    builder.AppendLine($"Upstream artifact to inspect: {artifact.Title} at {artifact.ManagedStoragePath}.");
                }
            }
        }

        builder.AppendLine("The recovery output must satisfy the named artifact expectation titles exactly so the process can transition without another full-step retry.");
        return builder.ToString().Trim();
    }

    private async Task<ManagerArtifactRecoveryAgent?> ResolveManagerArtifactRecoveryAgentAsync(
        DispatchCandidate candidate,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AgentDefinition> agents;
        try
        {
            agents = await workspaceService.ListAgentsAsync(includeTemplates: false, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not list Agent Framework agents while resolving process manager recovery for process run {RunId}, step {StepRunId}.",
                candidate.Run.Id,
                candidate.StepRun.Id);
            return null;
        }

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var managerOptions = await processesService.ListManagerAgentOptionsAsync(cancellationToken);
        var runDetails = await processesService.GetRunDetailsAsync(candidate.Run.Id, cancellationToken);
        return ResolveManagerArtifactRecoveryAgent(candidate.Run, runDetails.Assignments, managerOptions, agents);
    }

    internal static ManagerArtifactRecoveryAgent? ResolveManagerArtifactRecoveryAgent(
        ProcessRun run,
        IReadOnlyList<ProcessManagerAgentOption> managerOptions,
        IReadOnlyList<AgentDefinition> agents)
        => ResolveManagerArtifactRecoveryAgent(run, [], managerOptions, agents);

    internal static ManagerArtifactRecoveryAgent? ResolveManagerArtifactRecoveryAgent(
        ProcessRun run,
        IReadOnlyList<ProcessRunAssignmentViewModel> assignments,
        IReadOnlyList<ProcessManagerAgentOption> managerOptions,
        IReadOnlyList<AgentDefinition> agents)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentNullException.ThrowIfNull(managerOptions);
        ArgumentNullException.ThrowIfNull(agents);

        var byIdentifier = ResolveManagerArtifactRecoveryAgentByIdentifier(
            run.ManagerAgentId,
            managerOptions,
            agents,
            "run-manager-id");
        if (byIdentifier is not null)
        {
            return byIdentifier;
        }

        var byName = ResolveManagerArtifactRecoveryAgentByName(
            run.ManagerAgentName,
            managerOptions,
            agents);
        if (byName is not null)
        {
            return byName;
        }

        var byAssignedManager = ResolveManagerArtifactRecoveryAgentByAssignment(
            assignments,
            managerOptions,
            agents);
        if (byAssignedManager is not null)
        {
            return byAssignedManager;
        }

        var explicitlyCapableOption = managerOptions
            .Where(option => option.TechnicalAgentId.HasValue)
            .Where(option => ContainsExplicitArtifactRecoveryCapability(option.DisplayName) ||
                             ContainsExplicitArtifactRecoveryCapability(option.BindingSummary))
            .ToList();
        if (explicitlyCapableOption.Count == 1)
        {
            var option = explicitlyCapableOption[0];
            return new ManagerArtifactRecoveryAgent(
                option.TechnicalAgentId!.Value,
                ResolveManagerRecoveryDisplayName(option.DisplayName, option.TechnicalAgentId.Value),
                "single-explicit-recovery-option");
        }

        var explicitlyCapableAgents = agents
            .Where(agent =>
                ContainsExplicitArtifactRecoveryCapability(agent.Name) ||
                ContainsExplicitArtifactRecoveryCapability(agent.RoleTitle) ||
                agent.Tags.Any(ContainsExplicitArtifactRecoveryCapability))
            .OrderByDescending(agent => agent.UpdatedAtUtc)
            .ToList();
        return explicitlyCapableAgents.Count == 1
            ? new ManagerArtifactRecoveryAgent(
                explicitlyCapableAgents[0].Id,
                ResolveManagerRecoveryDisplayName(explicitlyCapableAgents[0].Name, explicitlyCapableAgents[0].Id),
                "single-explicit-recovery-agent")
            : null;
    }

    private static ManagerArtifactRecoveryAgent? ResolveManagerArtifactRecoveryAgentByAssignment(
        IReadOnlyList<ProcessRunAssignmentViewModel> assignments,
        IReadOnlyList<ProcessManagerAgentOption> managerOptions,
        IReadOnlyList<AgentDefinition> agents)
    {
        var candidates = assignments
            .Where(assignment => !assignment.IsCapabilityGap)
            .Select(assignment => new
            {
                Assignment = assignment,
                Agent = ResolveManagerArtifactRecoveryAgentByIdentifier(
                    assignment.PartyId,
                    managerOptions,
                    agents,
                    "run-manager-assignment"),
                Score = ResolveManagerAssignmentRecoveryScore(assignment)
            })
            .Where(item => item.Agent is not null && item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Assignment.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        var topScore = candidates[0].Score;
        return candidates.Count(item => item.Score == topScore) == 1
            ? candidates[0].Agent
            : null;
    }

    private static int ResolveManagerAssignmentRecoveryScore(ProcessRunAssignmentViewModel assignment)
    {
        var score = 0;
        score = Math.Max(score, ResolveManagerRecoveryTextScore(assignment.RoleDisplayName));
        score = Math.Max(score, ResolveManagerRecoveryTextScore(assignment.DisplayName));
        score = Math.Max(score, ResolveManagerRecoveryTextScore(assignment.BindingReason));
        return assignment.AllowsDirectMessaging
            ? score + 5
            : score;
    }

    private static ManagerArtifactRecoveryAgent? ResolveManagerArtifactRecoveryAgentByIdentifier(
        Guid? managerId,
        IReadOnlyList<ProcessManagerAgentOption> managerOptions,
        IReadOnlyList<AgentDefinition> agents,
        string resolutionSource)
    {
        if (!managerId.HasValue || managerId.Value == Guid.Empty)
        {
            return null;
        }

        var option = managerOptions.FirstOrDefault(item =>
            item.PartyId == managerId.Value ||
            item.TechnicalAgentId == managerId.Value);
        if (option?.TechnicalAgentId is Guid optionTechnicalAgentId)
        {
            return new ManagerArtifactRecoveryAgent(
                optionTechnicalAgentId,
                ResolveManagerRecoveryDisplayName(option.DisplayName, optionTechnicalAgentId),
                resolutionSource);
        }

        var agent = agents.FirstOrDefault(item => item.Id == managerId.Value);
        return agent is null
            ? null
            : new ManagerArtifactRecoveryAgent(
                agent.Id,
                ResolveManagerRecoveryDisplayName(agent.Name, agent.Id),
                resolutionSource);
    }

    private static ManagerArtifactRecoveryAgent? ResolveManagerArtifactRecoveryAgentByName(
        string managerName,
        IReadOnlyList<ProcessManagerAgentOption> managerOptions,
        IReadOnlyList<AgentDefinition> agents)
    {
        if (string.IsNullOrWhiteSpace(managerName))
        {
            return null;
        }

        var normalizedName = managerName.Trim();
        var option = managerOptions.FirstOrDefault(item =>
            string.Equals(item.DisplayName, normalizedName, StringComparison.OrdinalIgnoreCase));
        if (option?.TechnicalAgentId is Guid optionTechnicalAgentId)
        {
            return new ManagerArtifactRecoveryAgent(
                optionTechnicalAgentId,
                ResolveManagerRecoveryDisplayName(option.DisplayName, optionTechnicalAgentId),
                "run-manager-name");
        }

        var agent = agents.FirstOrDefault(item =>
            string.Equals(item.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
        return agent is null
            ? null
            : new ManagerArtifactRecoveryAgent(
                agent.Id,
                ResolveManagerRecoveryDisplayName(agent.Name, agent.Id),
                "run-manager-name");
    }

    private static int ResolveManagerRecoveryScore(AgentDefinition agent)
    {
        var score = 0;
        score = Math.Max(score, ResolveManagerRecoveryTextScore(agent.Name));
        score = Math.Max(score, ResolveManagerRecoveryTextScore(agent.RoleTitle));
        score = Math.Max(score, agent.Tags.Any(ContainsManagerRecoveryToken) ? 20 : 0);
        return score;
    }

    private static int ResolveManagerRecoveryTextScore(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        if (ContainsExplicitArtifactRecoveryCapability(value))
        {
            return 120;
        }

        if (value.Contains("process manager", StringComparison.OrdinalIgnoreCase))
        {
            return 100;
        }

        if (value.Contains("delivery manager", StringComparison.OrdinalIgnoreCase))
        {
            return 90;
        }

        if (value.Contains("manager", StringComparison.OrdinalIgnoreCase))
        {
            return 70;
        }

        if (value.Contains("orchestrator", StringComparison.OrdinalIgnoreCase))
        {
            return 60;
        }

        return 0;
    }

    private static bool ContainsManagerRecoveryToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var tokens = value.Split(
            [' ', '-', '_', '/', '\\', '.', ':', ';', ',', '(', ')', '[', ']'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.Any(token =>
            string.Equals(token, "manager", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "orchestrator", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsExplicitArtifactRecoveryCapability(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains("process-artifact-recovery-manager", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("artifact-recovery-manager", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("artifact recovery manager", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("process artifact recovery", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveManagerRecoveryDisplayName(string displayName, Guid technicalAgentId)
    {
        return string.IsNullOrWhiteSpace(displayName)
            ? technicalAgentId.ToString("D")
            : displayName.Trim();
    }

    private async Task<string> TryRecordManagerArtifactRecoveryDirectiveAsync(
        DispatchCandidate candidate,
        string directive,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var result = await processesService.RecordManagerDirectiveAsync(
            new ProcessManagerDirectiveRequest
            {
                ProcessRunId = candidate.Run.Id,
                Directive = directive,
                InstructedBy = AutomationActor
            },
            cancellationToken);
        return result.IsSuccess
            ? string.Empty
            : string.Join(" | ", result.Errors.Select(error => error.Message));
    }

    private static DispatchExecutionOutcome BlockMissingCompletionArtifacts(
        DispatchExecutionOutcome executionOutcome,
        IReadOnlyList<DispatchArtifactExpectation> missingArtifacts,
        string reason)
    {
        var missingArtifactSummary = FormatMissingArtifactTitles(missingArtifacts);
        return executionOutcome with
        {
            CompletionStatus = ProcessStepRunStatus.Blocked,
            CompletionReason = $"Required artifacts remain missing after completed execution: {missingArtifactSummary}. Process manager artifact recovery cannot proceed because {reason}.",
            SelectedBranchOutcomeId = null
        };
    }

    private static string FormatMissingArtifactTitles(IReadOnlyList<DispatchArtifactExpectation> artifacts)
    {
        return string.Join(", ", artifacts.Select(artifact => artifact.Title));
    }
}
