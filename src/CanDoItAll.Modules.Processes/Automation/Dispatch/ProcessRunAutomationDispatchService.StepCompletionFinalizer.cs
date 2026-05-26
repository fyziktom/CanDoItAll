using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    internal enum ProcessStepCompletionExecutorKind
    {
        DirectAgent,
        WorkflowBackedRole,
        SubprocessParent,
        ManagerArtifactRecovery,
        Manual
    }

    internal enum ProcessArtifactExpectationMode
    {
        Narrative,
        Decision,
        Evidence,
        Deliverable,
        RuntimeProof,
        RecoveryDiagnostic
    }

    internal enum ProcessArtifactValidationStatus
    {
        Satisfied,
        Missing,
        InvalidFormat,
        InsufficientEvidence,
        StaleOrWrongRun,
        WrongProducerMode,
        PlaceholderOnly,
        ContentUnavailable,
        ContentHashMismatch
    }

    internal enum ProcessArtifactFailureOwnership
    {
        OwnOutput,
        UpstreamInput,
        RuntimeEvidence,
        ReviewDisposition
    }

    internal enum ProcessArtifactProducerKind
    {
        Unknown,
        AgentExecutionArtifact,
        WorkspaceWrite,
        ExistingManagedFile,
        AssistantResponse,
        CompletedDecision,
        ProcessMock,
        ProviderNativeBrowser,
        WorkflowRun,
        WorkflowArtifact,
        SubprocessArtifact,
        ManagerRecovery,
        Manual
    }

    internal sealed record ProcessArtifactExpectationValidationResult(
        Guid ExpectationId,
        string ExpectationTitle,
        ProcessArtifactExpectationMode Mode,
        ProcessArtifactValidationStatus Status,
        ProcessArtifactProducerKind ProducerKind,
        Guid? ArtifactRecordId,
        string AttemptedPath,
        string Diagnostic,
        string SuggestedAction,
        string Fingerprint,
        ProcessArtifactFailureOwnership FailureOwnership = ProcessArtifactFailureOwnership.OwnOutput)
    {
        public bool IsSatisfied => Status == ProcessArtifactValidationStatus.Satisfied;
    }

    internal interface IProcessArtifactContentReader
    {
        ProcessArtifactContentReadResult Read(string managedStoragePath);
    }

    internal sealed record ProcessArtifactContentReadResult(
        bool Succeeded,
        string ManagedStoragePath,
        string ResolvedPath,
        string ContentType,
        long ByteLength,
        byte[] ContentBytes,
        string? TextContent,
        string Diagnostic)
    {
        public static ProcessArtifactContentReadResult Failure(
            string managedStoragePath,
            string resolvedPath,
            string contentType,
            long byteLength,
            string diagnostic)
        {
            return new(
                false,
                managedStoragePath,
                resolvedPath,
                contentType,
                byteLength,
                [],
                null,
                diagnostic);
        }

        public static ProcessArtifactContentReadResult Success(
            string managedStoragePath,
            string resolvedPath,
            string contentType,
            byte[] contentBytes,
            string? textContent)
        {
            return new(
                true,
                managedStoragePath,
                resolvedPath,
                contentType,
                contentBytes.LongLength,
                contentBytes,
                textContent,
                string.Empty);
        }
    }

    internal sealed class WorkspaceProcessArtifactContentReader(IWorkspacePathResolver workspacePathResolver) : IProcessArtifactContentReader
    {
        public ProcessArtifactContentReadResult Read(string managedStoragePath)
        {
            if (string.IsNullOrWhiteSpace(managedStoragePath))
            {
                return ProcessArtifactContentReadResult.Failure(
                    managedStoragePath,
                    string.Empty,
                    "application/octet-stream",
                    0,
                    "Managed artifact storage path is empty.");
            }

            var workspaceRoot = Path.GetFullPath(workspacePathResolver.ResolveWorkspaceRoot());
            var candidateFullPath = Path.IsPathRooted(managedStoragePath)
                ? Path.GetFullPath(managedStoragePath)
                : Path.GetFullPath(Path.Combine(
                    workspaceRoot,
                    WorkspaceScopeDescriptor.NormalizeRelativePath(managedStoragePath).Replace('/', Path.DirectorySeparatorChar)));
            var contentType = GuessContentTypeFromPath(candidateFullPath);
            if (!IsWithinWorkspace(workspaceRoot, candidateFullPath))
            {
                return ProcessArtifactContentReadResult.Failure(
                    managedStoragePath,
                    candidateFullPath,
                    contentType,
                    0,
                    "Managed artifact storage path resolves outside the configured workspace root.");
            }

            if (!File.Exists(candidateFullPath))
            {
                return ProcessArtifactContentReadResult.Failure(
                    managedStoragePath,
                    candidateFullPath,
                    contentType,
                    0,
                    "Managed artifact content file was not found.");
            }

            var fileInfo = new FileInfo(candidateFullPath);
            if (fileInfo.Length > MaxProcessArtifactValidationContentBytes)
            {
                return ProcessArtifactContentReadResult.Failure(
                    managedStoragePath,
                    candidateFullPath,
                    contentType,
                    fileInfo.Length,
                    $"Managed artifact content is {fileInfo.Length} bytes, exceeding the validation limit of {MaxProcessArtifactValidationContentBytes} bytes.");
            }

            try
            {
                var contentBytes = File.ReadAllBytes(candidateFullPath);
                var textContent = TryDecodeManagedArtifactTextContent(contentType, candidateFullPath, contentBytes);
                return ProcessArtifactContentReadResult.Success(
                    managedStoragePath,
                    candidateFullPath,
                    contentType,
                    contentBytes,
                    textContent);
            }
            catch (IOException exception)
            {
                return ProcessArtifactContentReadResult.Failure(
                    managedStoragePath,
                    candidateFullPath,
                    contentType,
                    fileInfo.Length,
                    $"Managed artifact content could not be read: {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                return ProcessArtifactContentReadResult.Failure(
                    managedStoragePath,
                    candidateFullPath,
                    contentType,
                    fileInfo.Length,
                    $"Managed artifact content could not be read: {exception.Message}");
            }
        }
    }

    internal sealed class StorageBackedProcessArtifactContentReader(
        IWorkspacePathResolver workspacePathResolver,
        IStorageCatalogService storageCatalogService,
        IStorageDriverRegistry storageDriverRegistry) : IProcessArtifactContentReader
    {
        private readonly WorkspaceProcessArtifactContentReader workspaceReader = new(workspacePathResolver);

        public ProcessArtifactContentReadResult Read(string managedStoragePath)
        {
            if (!StorageJson.TryParseReference(managedStoragePath, out var reference) || reference is null)
            {
                return workspaceReader.Read(managedStoragePath);
            }

            if (!reference.StorageId.HasValue)
            {
                return workspaceReader.Read(reference.Locator);
            }

            try
            {
                return ReadStorageReferenceAsync(managedStoragePath, reference).GetAwaiter().GetResult();
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                return ProcessArtifactContentReadResult.Failure(
                    managedStoragePath,
                    reference.Locator,
                    string.IsNullOrWhiteSpace(reference.ContentType) ? "application/octet-stream" : reference.ContentType,
                    reference.ContentLength ?? 0,
                    $"Managed storage object could not be read: {exception.Message}");
            }
        }

        private async Task<ProcessArtifactContentReadResult> ReadStorageReferenceAsync(
            string managedStoragePath,
            StorageObjectReference reference)
        {
            var storage = await storageCatalogService.GetAsync(reference.StorageId!.Value, CancellationToken.None);
            if (storage is null)
            {
                return ProcessArtifactContentReadResult.Failure(
                    managedStoragePath,
                    reference.Locator,
                    reference.ContentType,
                    reference.ContentLength ?? 0,
                    $"Storage catalog record '{reference.StorageId.Value:D}' was not found.");
            }

            var driver = storageDriverRegistry.Resolve(storage.ProviderKind);
            await using var stream = await driver.OpenReadAsync(storage, reference, CancellationToken.None);
            if (stream.CanSeek && stream.Length > MaxProcessArtifactValidationContentBytes)
            {
                return ProcessArtifactContentReadResult.Failure(
                    managedStoragePath,
                    reference.Locator,
                    reference.ContentType,
                    stream.Length,
                    $"Managed artifact content is {stream.Length} bytes, exceeding the validation limit of {MaxProcessArtifactValidationContentBytes} bytes.");
            }

            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, CancellationToken.None);
            if (memory.Length > MaxProcessArtifactValidationContentBytes)
            {
                return ProcessArtifactContentReadResult.Failure(
                    managedStoragePath,
                    reference.Locator,
                    reference.ContentType,
                    memory.Length,
                    $"Managed artifact content is {memory.Length} bytes, exceeding the validation limit of {MaxProcessArtifactValidationContentBytes} bytes.");
            }

            var contentBytes = memory.ToArray();
            var contentType = string.IsNullOrWhiteSpace(reference.ContentType)
                ? GuessContentTypeFromPath(reference.Locator)
                : reference.ContentType;
            var textContent = TryDecodeManagedArtifactTextContent(contentType, reference.Locator, contentBytes);
            return ProcessArtifactContentReadResult.Success(
                managedStoragePath,
                reference.Locator,
                contentType,
                contentBytes,
                textContent);
        }
    }

    private const int MaxProcessArtifactValidationContentBytes = 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8Encoding = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private sealed record ProcessStepCompletionFinalizerContext(
        ProcessStepCompletionExecutorKind ExecutorKind,
        DispatchCandidate Candidate,
        ProcessStepRunStatus CompletionStatus,
        string CompletionReason,
        Guid? SelectedBranchOutcomeId,
        ExecutionRunDetail? ExecutionDetail,
        Guid? WorkflowRunId,
        Guid? SubprocessRunId,
        string ResponseText,
        bool ProjectExecutionArtifacts,
        bool AllowManagerArtifactRecovery,
        string Trigger,
        Func<CancellationToken, Task>? RenewLeaseAsync,
        Guid? RecoveryExecutionRunId = null,
        Guid? RecoveredForExecutionRunId = null);

    private sealed record ProcessStepCompletionFinalizerResult(
        ProcessStepRunStatus CompletionStatus,
        string CompletionReason,
        ProcessStepBlockCause? BlockCause,
        Guid? SelectedBranchOutcomeId,
        Guid StepRunConcurrencyToken,
        IReadOnlyList<ProcessArtifactExpectationValidationResult> ArtifactValidationResults);

    private sealed record RuntimeInvariantViolation(
        ProcessConformanceSeverity Severity,
        string Code,
        string Observation,
        string DeviationReason);

    private sealed record ProcessArtifactValidationDiagnosticPayload(
        Guid ProcessRunId,
        Guid StepRunId,
        Guid ExpectationId,
        string ExpectationTitle,
        ProcessArtifactExpectationMode Mode,
        ProcessArtifactValidationStatus Status,
        ProcessArtifactProducerKind ProducerKind,
        ProcessArtifactFailureOwnership FailureOwnership,
        Guid? ArtifactRecordId,
        string AttemptedPath,
        string Diagnostic,
        string SuggestedAction,
        string Fingerprint,
        ProcessStepCompletionExecutorKind ExecutorKind,
        Guid? ExecutionRunId,
        Guid? WorkflowRunId,
        Guid? SubprocessRunId,
        DateTimeOffset CreatedAtUtc);

    private async Task<ProcessStepCompletionFinalizerResult?> FinalizeStepCompletionAsync(
        ProcessStepCompletionFinalizerContext context,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        var candidate = context.Candidate;
        var stepRunSnapshot = await LoadStepRunTransitionSnapshotAsync(candidate.StepRun.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Process step run {candidate.StepRun.Id} could not be reloaded before process-owned completion finalization.");

        if (context.CompletionStatus == ProcessStepRunStatus.InProgress ||
            stepRunSnapshot.Status == context.CompletionStatus)
        {
            logger.LogInformation(
                "Process-owned finalizer observed {ExecutorKind} completion for run {RunId}, step {StepRunId} as {Status}; no transition is required.",
                context.ExecutorKind,
                candidate.Run.Id,
                candidate.StepRun.Id,
                context.CompletionStatus);
            return null;
        }

        if (ShouldSkipAutomationCompletionTransition(stepRunSnapshot.Status, context.CompletionStatus))
        {
            logger.LogInformation(
                "Skipping stale process-owned finalizer transition for run {RunId}, step {StepRunId}. Current status is {CurrentStatus}, requested status is {RequestedStatus}, executor kind is {ExecutorKind}.",
                candidate.Run.Id,
                candidate.StepRun.Id,
                stepRunSnapshot.Status,
                context.CompletionStatus,
                context.ExecutorKind);
            return null;
        }

        var completionStatus = context.CompletionStatus;
        var completionReason = context.CompletionReason;
        ProcessStepBlockCause? blockCause = null;
        var selectedBranchOutcomeId = context.SelectedBranchOutcomeId;

        if (context.ExecutionDetail is not null && context.ProjectExecutionArtifacts)
        {
            await ProjectExecutionArtifactsAsync(
                candidate,
                context.ExecutionDetail,
                context.ResponseText,
                completionStatus,
                dispatchClaim,
                cancellationToken);
        }

        var validationResults = await ValidateRequiredCompletionArtifactsAsync(context, cancellationToken);
        if (completionStatus == ProcessStepRunStatus.Completed)
        {
            await PersistArtifactValidationDiagnosticsAsync(context, validationResults, cancellationToken);
            var unsatisfiedResults = validationResults.Where(result => !result.IsSatisfied).ToList();
            if (unsatisfiedResults.Count > 0 &&
                context.ExecutionDetail is not null &&
                context.AllowManagerArtifactRecovery)
            {
                var recoveryOutcome = await RecoverMissingCompletionArtifactsWithManagerAsync(
                    candidate,
                    new DispatchExecutionOutcome(
                        context.ExecutionDetail,
                        context.ResponseText,
                        completionStatus,
                        completionReason,
                        [],
                        AttemptNumber: 1,
                        selectedBranchOutcomeId),
                    ResolveUnsatisfiedArtifactExpectations(candidate, unsatisfiedResults),
                    context.Trigger,
                    dispatchClaim,
                    context.RenewLeaseAsync,
                    cancellationToken);

                completionStatus = recoveryOutcome.CompletionStatus;
                completionReason = recoveryOutcome.CompletionReason;
                selectedBranchOutcomeId = recoveryOutcome.SelectedBranchOutcomeId;

                validationResults = await ValidateRequiredCompletionArtifactsAsync(
                    context with
                    {
                        CompletionStatus = completionStatus,
                        CompletionReason = completionReason,
                        SelectedBranchOutcomeId = selectedBranchOutcomeId,
                        ProjectExecutionArtifacts = false,
                        AllowManagerArtifactRecovery = false,
                        RecoveryExecutionRunId = recoveryOutcome.Detail.Run.Id,
                        RecoveredForExecutionRunId = context.ExecutionDetail.Run.Id
                    },
                    cancellationToken);
                await PersistArtifactValidationDiagnosticsAsync(context, validationResults, cancellationToken);
                unsatisfiedResults = validationResults.Where(result => !result.IsSatisfied).ToList();
            }

            if (completionStatus == ProcessStepRunStatus.Completed && unsatisfiedResults.Count > 0)
            {
                var routedDisposition = ResolveArtifactContractDispositionBranchOutcome(candidate, unsatisfiedResults);
                if (routedDisposition is not null)
                {
                    completionReason = BuildArtifactContractDispositionReason(routedDisposition, unsatisfiedResults);
                    selectedBranchOutcomeId = routedDisposition.Id;
                }
                else
                {
                    completionStatus = ProcessStepRunStatus.Blocked;
                    completionReason = BuildArtifactContractBlockedReason(unsatisfiedResults);
                    blockCause = ResolveArtifactContractBlockCause(unsatisfiedResults);
                    selectedBranchOutcomeId = null;
                }
            }
        }
        else
        {
            RefreshCandidateArtifactSatisfaction(candidate, validationResults);
        }

        var invariantViolations = await PersistRuntimeInvariantAuditAsync(
            context,
            completionStatus,
            validationResults,
            cancellationToken);
        var severeInvariant = invariantViolations.FirstOrDefault(violation =>
            violation.Severity is ProcessConformanceSeverity.High or ProcessConformanceSeverity.Critical);
        if (completionStatus == ProcessStepRunStatus.Completed && severeInvariant is not null)
        {
            completionStatus = ProcessStepRunStatus.Blocked;
            completionReason = $"Runtime invariant violation: {severeInvariant.Observation}";
            blockCause = ProcessStepBlockCause.RuntimeEvidence;
            selectedBranchOutcomeId = null;
        }

        if (completionStatus is ProcessStepRunStatus.Blocked or ProcessStepRunStatus.Failed &&
            !blockCause.HasValue)
        {
            blockCause = ProcessBlockStateClassifier.InferBlockCause(completionReason);
        }

        return new ProcessStepCompletionFinalizerResult(
            completionStatus,
            completionReason,
            blockCause,
            selectedBranchOutcomeId,
            stepRunSnapshot.ConcurrencyToken,
            validationResults);
    }

    private async Task ApplyFinalizedStepTransitionAsync(
        DispatchCandidate candidate,
        ProcessStepCompletionFinalizerResult finalizerResult,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        var completionResult = await TransitionStepWithClaimAsync(
            new ProcessStepTransitionRequest
            {
                StepRunId = candidate.StepRun.Id,
                StepRunConcurrencyToken = finalizerResult.StepRunConcurrencyToken,
                TargetStatus = finalizerResult.CompletionStatus,
                Reason = finalizerResult.CompletionReason,
                BlockCause = finalizerResult.BlockCause,
                SelectedBranchOutcomeId = finalizerResult.SelectedBranchOutcomeId,
                DecidedBy = AutomationActor,
                SuppressAutomationDispatch = finalizerResult.CompletionStatus != ProcessStepRunStatus.Completed
            },
            dispatchClaim,
            cancellationToken);

        if (completionResult.IsSuccess)
        {
            return;
        }

        var refreshedSnapshot = await LoadStepRunTransitionSnapshotAsync(candidate.StepRun.Id, cancellationToken);
        if (refreshedSnapshot is not null &&
            ShouldSkipAutomationCompletionTransition(refreshedSnapshot.Status, finalizerResult.CompletionStatus))
        {
            logger.LogInformation(
                "Skipping stale process-owned finalizer transition after a failed attempt for run {RunId}, step {StepRunId}. Current status is {CurrentStatus}, requested status is {RequestedStatus}.",
                candidate.Run.Id,
                candidate.StepRun.Id,
                refreshedSnapshot.Status,
                finalizerResult.CompletionStatus);
            return;
        }

        throw new InvalidOperationException(string.Join(" | ", completionResult.Errors.Select(error => error.Message)));
    }

    private async Task<IReadOnlyList<ProcessArtifactExpectationValidationResult>> ValidateRequiredCompletionArtifactsAsync(
        ProcessStepCompletionFinalizerContext context,
        CancellationToken cancellationToken)
    {
        var candidate = context.Candidate;
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var artifacts = await dbContext.Set<ProcessArtifactRecord>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == candidate.Run.Id && item.StepRunId == candidate.StepRun.Id)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var managedArtifactContentReader = new StorageBackedProcessArtifactContentReader(
            workspacePathResolver,
            storageCatalogService,
            storageDriverRegistry);
        var results = candidate.ExpectedArtifacts
            .Where(expectation => expectation.IsRequired)
            .Select(expectation => ValidateArtifactExpectationForRecordedArtifacts(
                candidate.Run.Id,
                candidate.StepRun.Id,
                expectation,
                artifacts,
                context.ExecutorKind,
                context.ExecutionDetail?.Run.Id,
                context.ExecutorKind == ProcessStepCompletionExecutorKind.WorkflowBackedRole
                    ? context.WorkflowRunId ?? ResolveWorkflowRunIdForStep(artifacts)
                    : null,
                context.ExecutorKind == ProcessStepCompletionExecutorKind.SubprocessParent
                    ? context.SubprocessRunId ?? ResolveSubprocessRunIdForStep(artifacts)
                    : null,
                context.RecoveryExecutionRunId,
                context.RecoveredForExecutionRunId,
                managedArtifactContentReader))
            .ToList();

        candidate.ExternalReferenceKeys.Clear();
        foreach (var externalReferenceKey in artifacts
                     .Select(item => item.ExternalReferenceKey)
                     .Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            candidate.ExternalReferenceKeys.Add(externalReferenceKey);
        }

        RefreshCandidateArtifactSatisfaction(candidate, results);
        return results;
    }

    private async Task<IReadOnlyList<RuntimeInvariantViolation>> PersistRuntimeInvariantAuditAsync(
        ProcessStepCompletionFinalizerContext context,
        ProcessStepRunStatus completionStatus,
        IReadOnlyList<ProcessArtifactExpectationValidationResult> validationResults,
        CancellationToken cancellationToken)
    {
        var violations = new List<RuntimeInvariantViolation>();
        var candidate = context.Candidate;
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var artifacts = await dbContext.Set<ProcessArtifactRecord>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == candidate.Run.Id && item.StepRunId == candidate.StepRun.Id)
            .ToListAsync(cancellationToken);

        if (context.ExecutionDetail is not null &&
            !ExecutionInvocationMetadata.ResolveProcessAllowsProductMutation(context.ExecutionDetail.Run) &&
            context.ExecutionDetail.ToolReceipts.Any(IsConcreteProductMutationReceipt))
        {
            violations.Add(new RuntimeInvariantViolation(
                ProcessConformanceSeverity.Critical,
                "product-mutation-without-operation",
                "A non-mutating governed step recorded a product mutation tool receipt.",
                "Tool receipts must match the persisted operation contract."));
        }

        foreach (var artifact in artifacts)
        {
            if (IsWrongRootArtifact(artifact))
            {
                violations.Add(new RuntimeInvariantViolation(
                    ProcessConformanceSeverity.High,
                    "wrong-root-artifact",
                    $"Artifact '{artifact.Title}' points at '{artifact.ManagedStoragePath}', which is outside the current-run managed artifact boundary.",
                    "Evidence and deliverables must be recorded from current-run managed storage or an explicitly allowed external artifact destination."));
            }

            if (RequiresProjectionLineage(artifact) &&
                string.IsNullOrWhiteSpace(artifact.ProjectionIdentityHash))
            {
                violations.Add(new RuntimeInvariantViolation(
                    ProcessConformanceSeverity.High,
                    "missing-projection-lineage",
                    $"Artifact '{artifact.Title}' is missing projection identity lineage.",
                    "Evidence and deliverable artifact records need typed source lineage for dedupe and recovery audit."));
            }
        }

        foreach (var unsatisfiedResult in validationResults.Where(result => !result.IsSatisfied))
        {
            violations.Add(new RuntimeInvariantViolation(
                ProcessConformanceSeverity.Moderate,
                "artifact-validation-unsatisfied",
                $"Artifact expectation '{unsatisfiedResult.ExpectationTitle}' was not satisfied: {unsatisfiedResult.Diagnostic}",
                unsatisfiedResult.SuggestedAction));
        }

        if (violations.Count == 0)
        {
            return [];
        }

        foreach (var violation in violations)
        {
            await dbContext.Set<ProcessConformanceObservation>().AddAsync(
                new ProcessConformanceObservation
                {
                    ProcessRunId = candidate.Run.Id,
                    StepRunId = candidate.StepRun.Id,
                    Severity = violation.Severity,
                    Category = "runtime-invariant",
                    Observation = violation.Observation,
                    DeviationReason = violation.DeviationReason,
                    IsSafeNonAction = false,
                    ContainsSensitiveAssessment = false,
                    CreatedAtUtc = clock.GetUtcNow()
                },
                cancellationToken);
            await dbContext.Set<ProcessJournalEntry>().AddAsync(
                new ProcessJournalEntry
                {
                    ProcessRunId = candidate.Run.Id,
                    StepRunId = candidate.StepRun.Id,
                    EventType = ProcessRuntimeEventTypes.RuntimeInvariantViolationRecorded,
                    Title = "Runtime invariant violation recorded",
                    Description = violation.Observation,
                    CorrelationId = $"{candidate.StepRun.Id:D}:{violation.Code}",
                    OperatingMode = candidate.Run.OperatingMode,
                    PolicyVersion = $"definition-version:{candidate.Run.ProcessDefinitionVersionId:D}",
                    EnvironmentMode = candidate.Run.OperatingMode.ToString(),
                    ReplayContextJson = JsonSerializer.Serialize(new
                    {
                        RunId = candidate.Run.Id,
                        StepRunId = candidate.StepRun.Id,
                        CompletionStatus = completionStatus.ToString(),
                        violation.Code,
                        Severity = violation.Severity.ToString(),
                        violation.DeviationReason
                    }),
                    OccurredAtUtc = clock.GetUtcNow()
                },
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return violations;
    }

    internal static ProcessArtifactExpectationValidationResult ValidateArtifactExpectationForRecordedArtifacts(
        Guid processRunId,
        Guid stepRunId,
        DispatchArtifactExpectation expectation,
        IReadOnlyList<ProcessArtifactRecord> artifacts,
        ProcessStepCompletionExecutorKind executorKind,
        Guid? executionRunId = null,
        Guid? workflowRunId = null,
        Guid? subprocessRunId = null,
        Guid? recoveryExecutionRunId = null,
        Guid? recoveredForExecutionRunId = null,
        IProcessArtifactContentReader? managedArtifactContentReader = null)
        => ProcessCompletionArtifactValidator.ValidateArtifactExpectationForRecordedArtifacts(
            processRunId,
            stepRunId,
            expectation,
            artifacts,
            executorKind,
            executionRunId,
            workflowRunId,
            subprocessRunId,
            recoveryExecutionRunId,
            recoveredForExecutionRunId,
            managedArtifactContentReader);

    private async Task PersistArtifactValidationDiagnosticsAsync(
        ProcessStepCompletionFinalizerContext context,
        IReadOnlyList<ProcessArtifactExpectationValidationResult> validationResults,
        CancellationToken cancellationToken)
    {
        var failures = validationResults.Where(result => !result.IsSatisfied).ToList();
        if (failures.Count == 0)
        {
            return;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingFingerprints = await dbContext.Set<ProcessJournalEntry>()
            .AsNoTracking()
            .Where(item =>
                item.ProcessRunId == context.Candidate.Run.Id &&
                item.StepRunId == context.Candidate.StepRun.Id &&
                item.EventType == ProcessRuntimeEventTypes.ArtifactValidationDiagnostic)
            .Select(item => item.CorrelationId)
            .ToListAsync(cancellationToken);
        var existingFingerprintSet = existingFingerprints.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var now = clock.GetUtcNow();

        foreach (var failure in failures)
        {
            if (existingFingerprintSet.Contains(failure.Fingerprint))
            {
                continue;
            }

            var payload = new ProcessArtifactValidationDiagnosticPayload(
                context.Candidate.Run.Id,
                context.Candidate.StepRun.Id,
                failure.ExpectationId,
                failure.ExpectationTitle,
                failure.Mode,
                failure.Status,
                failure.ProducerKind,
                failure.FailureOwnership,
                failure.ArtifactRecordId,
                failure.AttemptedPath,
                failure.Diagnostic,
                failure.SuggestedAction,
                failure.Fingerprint,
                context.ExecutorKind,
                context.ExecutionDetail?.Run.Id,
                context.WorkflowRunId,
                context.SubprocessRunId,
                now);

            await dbContext.Set<ProcessJournalEntry>().AddAsync(
                new ProcessJournalEntry
                {
                    ProcessRunId = context.Candidate.Run.Id,
                    StepRunId = context.Candidate.StepRun.Id,
                    EventType = ProcessRuntimeEventTypes.ArtifactValidationDiagnostic,
                    Title = $"Artifact validation failed: {failure.ExpectationTitle}",
                    Description = $"{failure.Status}: {failure.Diagnostic}",
                    CorrelationId = failure.Fingerprint,
                    OperatingMode = context.Candidate.Run.OperatingMode,
                    PolicyVersion = $"definition-version:{context.Candidate.Run.ProcessDefinitionVersionId:D}",
                    EnvironmentMode = context.Candidate.Run.OperatingMode.ToString(),
                    ReplayContextJson = JsonSerializer.Serialize(payload, AgentOutputJson.SerializerOptions),
                    OccurredAtUtc = now
                },
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void RefreshCandidateArtifactSatisfaction(
        DispatchCandidate candidate,
        IReadOnlyList<ProcessArtifactExpectationValidationResult> validationResults)
    {
        candidate.RecordedArtifactExpectationIds.Clear();
        foreach (var result in validationResults.Where(result => result.IsSatisfied))
        {
            candidate.RecordedArtifactExpectationIds.Add(result.ExpectationId);
        }
    }

    private static IReadOnlyList<DispatchArtifactExpectation> ResolveUnsatisfiedArtifactExpectations(
        DispatchCandidate candidate,
        IReadOnlyList<ProcessArtifactExpectationValidationResult> unsatisfiedResults)
    {
        var unsatisfiedIds = unsatisfiedResults.Select(result => result.ExpectationId).ToHashSet();
        return candidate.ExpectedArtifacts
            .Where(expectation => unsatisfiedIds.Contains(expectation.Id))
            .ToList();
    }

    private static string BuildArtifactContractBlockedReason(
        IReadOnlyList<ProcessArtifactExpectationValidationResult> unsatisfiedResults)
    {
        var summary = string.Join(
            "; ",
            unsatisfiedResults
                .Take(5)
                .Select(result => $"{result.ExpectationTitle}: {result.Status} ({result.Diagnostic})"));
        return $"Required artifact contract validation failed: {summary}. The process step is blocked instead of completing with missing, malformed, stale, placeholder, or weakly produced artifacts.";
    }

    internal static ProcessStepBlockCause ResolveArtifactContractBlockCause(
        IReadOnlyList<ProcessArtifactExpectationValidationResult> unsatisfiedResults)
    {
        ArgumentNullException.ThrowIfNull(unsatisfiedResults);

        if (unsatisfiedResults.Any(result => result.FailureOwnership == ProcessArtifactFailureOwnership.UpstreamInput))
        {
            return ProcessStepBlockCause.UpstreamInput;
        }

        if (unsatisfiedResults.Any(result => result.FailureOwnership == ProcessArtifactFailureOwnership.RuntimeEvidence))
        {
            return ProcessStepBlockCause.RuntimeEvidence;
        }

        return ProcessStepBlockCause.OwnOutput;
    }

    private static DispatchBranchOutcome? ResolveArtifactContractDispositionBranchOutcome(
        DispatchCandidate candidate,
        IReadOnlyList<ProcessArtifactExpectationValidationResult> unsatisfiedResults)
    {
        if (unsatisfiedResults.Count == 0 ||
            candidate.BranchOutcomes.Count == 0 ||
            ResolveMissingUpstreamArtifactInputs(candidate).Count > 0 ||
            !IsDispositionRoutingStep(candidate) ||
            !CanRouteArtifactContractDispositionFailures(candidate, unsatisfiedResults))
        {
            return null;
        }

        return ResolveNegativeDispositionBranchOutcome(candidate, unsatisfiedResults);
    }

    private static bool IsDispositionRoutingStep(DispatchCandidate candidate)
    {
        if (candidate.StepRun.StepKind is ProcessStepKind.Decision or ProcessStepKind.Approval or ProcessStepKind.Review)
        {
            return true;
        }

        var text = CollapsePromptWhitespace(string.Join(
                ' ',
                candidate.StepRun.Title,
                candidate.StepDefinition.Title,
                candidate.StepDefinition.DecisionRightsSummary,
                candidate.StepDefinition.OutputContractSummary,
                candidate.WorkBrief?.WorkBriefText,
                candidate.WorkBrief?.ExpectedOutcome))
            .ToLowerInvariant();
        return ContainsAnyToken(
            text,
            "qa",
            "quality",
            "review",
            "approval",
            "approve",
            "decision",
            "decide",
            "escalation",
            "escalate",
            "inspection",
            "inspect");
    }

    private static bool CanRouteArtifactContractDispositionFailures(
        DispatchCandidate candidate,
        IReadOnlyList<ProcessArtifactExpectationValidationResult> unsatisfiedResults)
    {
        if (!HasSatisfiedRequiredDecisionArtifact(candidate))
        {
            return false;
        }

        return unsatisfiedResults.All(result =>
            ResolveDispositionRoutingFailureOwnership(result) == ProcessArtifactFailureOwnership.ReviewDisposition);
    }

    private static bool HasSatisfiedRequiredDecisionArtifact(DispatchCandidate candidate)
    {
        return candidate.ExpectedArtifacts.Any(expectation =>
            expectation.IsRequired &&
            expectation.ArtifactKind is ProcessArtifactKind.Decision or ProcessArtifactKind.DecisionRecord &&
            candidate.RecordedArtifactExpectationIds.Contains(expectation.Id));
    }

    private static ProcessArtifactFailureOwnership ResolveDispositionRoutingFailureOwnership(
        ProcessArtifactExpectationValidationResult result)
    {
        if (result.FailureOwnership == ProcessArtifactFailureOwnership.UpstreamInput ||
            result.Diagnostic.Contains("upstream", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactFailureOwnership.UpstreamInput;
        }

        if (IsOwnOutputArtifactProductionFailure(result))
        {
            return ProcessArtifactFailureOwnership.OwnOutput;
        }

        return result.FailureOwnership;
    }

    private static bool IsOwnOutputArtifactProductionFailure(ProcessArtifactExpectationValidationResult result)
    {
        return result.Status is ProcessArtifactValidationStatus.Missing or
            ProcessArtifactValidationStatus.InvalidFormat or
            ProcessArtifactValidationStatus.PlaceholderOnly or
            ProcessArtifactValidationStatus.StaleOrWrongRun or
            ProcessArtifactValidationStatus.ContentUnavailable or
            ProcessArtifactValidationStatus.ContentHashMismatch;
    }

    private static DispatchBranchOutcome? ResolveNegativeDispositionBranchOutcome(
        DispatchCandidate candidate,
        IReadOnlyList<ProcessArtifactExpectationValidationResult> unsatisfiedResults)
    {
        if (TryResolveRepairBranchOutcome(candidate, out var repairBranchOutcome))
        {
            return IsRepairDispositionCompatible(unsatisfiedResults)
                ? repairBranchOutcome
                : null;
        }

        return candidate.BranchOutcomes.FirstOrDefault(IsNegativeDispositionBranchOutcomeCandidate);
    }

    private static bool IsRepairDispositionCompatible(
        IReadOnlyList<ProcessArtifactExpectationValidationResult> unsatisfiedResults)
    {
        return unsatisfiedResults.Any(result =>
            result.Status is ProcessArtifactValidationStatus.InvalidFormat or
                ProcessArtifactValidationStatus.InsufficientEvidence or
                ProcessArtifactValidationStatus.WrongProducerMode or
                ProcessArtifactValidationStatus.PlaceholderOnly or
                ProcessArtifactValidationStatus.ContentUnavailable or
                ProcessArtifactValidationStatus.ContentHashMismatch);
    }

    private static bool IsNegativeDispositionBranchOutcomeCandidate(DispatchBranchOutcome outcome)
    {
        var token = NormalizeBranchOutcomeToken($"{outcome.Key} {outcome.Title} {outcome.Description}");
        if (string.IsNullOrWhiteSpace(token) || IsAcceptingBranchOutcomeToken(token))
        {
            return false;
        }

        return token.Contains("nogo", StringComparison.Ordinal) ||
               token.Contains("escalat", StringComparison.Ordinal) ||
               token.Contains("reject", StringComparison.Ordinal) ||
               token.Contains("decline", StringComparison.Ordinal) ||
               token.Contains("fail", StringComparison.Ordinal) ||
               token.Contains("blocked", StringComparison.Ordinal) ||
               token.Contains("risk", StringComparison.Ordinal);
    }

    private static string BuildArtifactContractDispositionReason(
        DispatchBranchOutcome routedDisposition,
        IReadOnlyList<ProcessArtifactExpectationValidationResult> unsatisfiedResults)
    {
        var summary = string.Join(
            "; ",
            unsatisfiedResults
                .Take(5)
                .Select(result => $"{result.ExpectationTitle}: {result.Status} ({result.Diagnostic})"));
        return $"Required artifact contract validation produced governed disposition '{routedDisposition.Title}' instead of hard blocking: {summary}.";
    }

    private static ProcessArtifactExpectationMode ResolveArtifactExpectationMode(DispatchArtifactExpectation expectation)
    {
        var contractText = CollapsePromptWhitespace(string.Join(
            ' ',
            expectation.Title,
            expectation.ValidationRequirementSummary,
            expectation.AllowedFutureUsageSummary)).ToLowerInvariant();
        if (TryResolveExplicitArtifactExpectationMode(contractText, out var explicitMode))
        {
            return explicitMode;
        }

        if (contractText.Contains("runtime proof", StringComparison.Ordinal) ||
            contractText.Contains("browser proof", StringComparison.Ordinal) ||
            contractText.Contains("test output", StringComparison.Ordinal) ||
            contractText.Contains("build output", StringComparison.Ordinal) ||
            contractText.Contains("command output", StringComparison.Ordinal) ||
            contractText.Contains("screenshot", StringComparison.Ordinal) ||
            ContainsRuntimeLogSignal(contractText))
        {
            return ProcessArtifactExpectationMode.RuntimeProof;
        }

        return expectation.ArtifactKind switch
        {
            ProcessArtifactKind.Decision or ProcessArtifactKind.DecisionRecord => ProcessArtifactExpectationMode.Decision,
            ProcessArtifactKind.Deliverable => ProcessArtifactExpectationMode.Deliverable,
            ProcessArtifactKind.Evidence or ProcessArtifactKind.Transcript or ProcessArtifactKind.Dataset => ProcessArtifactExpectationMode.Evidence,
            _ => ProcessArtifactExpectationMode.Narrative
        };
    }

    private static bool TryResolveExplicitArtifactExpectationMode(
        string contractText,
        out ProcessArtifactExpectationMode mode)
    {
        mode = ProcessArtifactExpectationMode.Narrative;
        if (!contractText.Contains("artifact mode", StringComparison.OrdinalIgnoreCase) &&
            !contractText.Contains("expectation mode", StringComparison.OrdinalIgnoreCase) &&
            !contractText.Contains("mode:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var candidateMode in Enum.GetValues<ProcessArtifactExpectationMode>())
        {
            if (contractText.Contains(candidateMode.ToString().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
            {
                mode = candidateMode;
                return true;
            }
        }

        return false;
    }

    private static bool ContainsRuntimeLogSignal(string contractText)
    {
        return contractText.Contains("test log", StringComparison.Ordinal) ||
               contractText.Contains("build log", StringComparison.Ordinal) ||
               contractText.Contains("command log", StringComparison.Ordinal) ||
               contractText.Contains("runtime log", StringComparison.Ordinal) ||
               contractText.Contains("execution log", StringComparison.Ordinal) ||
               contractText.Contains("browser console log", StringComparison.Ordinal) ||
               contractText.Contains("console log", StringComparison.Ordinal);
    }

    private static bool IsArtifactCandidateForExpectation(
        DispatchArtifactExpectation expectation,
        ProcessArtifactRecord artifact)
    {
        if (artifact.ArtifactExpectationId == expectation.Id)
        {
            return true;
        }

        if (artifact.ArtifactKind != expectation.ArtifactKind)
        {
            return false;
        }

        if (string.Equals(artifact.Title, expectation.Title, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var expectedSlug = FileSafeSlugBuilder.Build(expectation.Title);
        if (string.IsNullOrWhiteSpace(expectedSlug))
        {
            return false;
        }

        return string.Equals(FileSafeSlugBuilder.Build(artifact.Title), expectedSlug, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(FileSafeSlugBuilder.Build(Path.GetFileNameWithoutExtension(artifact.ManagedStoragePath)), expectedSlug, StringComparison.OrdinalIgnoreCase);
    }

    private static int ResolveArtifactCandidatePriority(
        DispatchArtifactExpectation expectation,
        ProcessArtifactRecord artifact)
    {
        if (artifact.ArtifactExpectationId == expectation.Id)
        {
            return 0;
        }

        if (string.Equals(artifact.Title, expectation.Title, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }

    private static ProcessArtifactProducerKind ResolveArtifactProducerKind(ProcessArtifactRecord artifact)
    {
        var lineage = ProcessArtifactProjectionLineageJson.Deserialize(artifact.ProjectionLineageJson);
        if (lineage is not null)
        {
            if (IsManagerRecoveryLineage(lineage))
            {
                return ProcessArtifactProducerKind.ManagerRecovery;
            }

            var typedProducerKind = ResolveArtifactProducerKind(lineage.SourceKind);
            if (typedProducerKind != ProcessArtifactProducerKind.Unknown)
            {
                return typedProducerKind;
            }
        }

        var key = artifact.ExternalReferenceKey;
        if (key.StartsWith("agentframework-artifact:", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactProducerKind.AgentExecutionArtifact;
        }

        if (key.StartsWith("workspace-written-artifact|", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactProducerKind.WorkspaceWrite;
        }

        if (key.StartsWith("existing-managed-artifact|", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactProducerKind.ExistingManagedFile;
        }

        if (key.StartsWith("assistant-response|", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactProducerKind.AssistantResponse;
        }

        if (key.StartsWith("process-step-decision:", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactProducerKind.CompletedDecision;
        }

        if (key.StartsWith("process-mock-artifact:", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactProducerKind.ProcessMock;
        }

        if (key.StartsWith("agentframework-browser-artifact:", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactProducerKind.ProviderNativeBrowser;
        }

        if (key.StartsWith("workflow-run:", StringComparison.OrdinalIgnoreCase) &&
            key.Contains(":artifact:", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactProducerKind.WorkflowArtifact;
        }

        if (key.StartsWith("workflow-run:", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactProducerKind.WorkflowRun;
        }

        if (key.StartsWith("subprocess-run:", StringComparison.OrdinalIgnoreCase) &&
            key.Contains(":artifact:", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactProducerKind.SubprocessArtifact;
        }

        if (key.StartsWith("manager-recovery-artifact|", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactProducerKind.ManagerRecovery;
        }

        if (artifact.ProvenanceSummary.Contains("manager", StringComparison.OrdinalIgnoreCase) ||
            artifact.ProvenanceSummary.Contains("recovery", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactProducerKind.ManagerRecovery;
        }

        return string.IsNullOrWhiteSpace(key)
            ? ProcessArtifactProducerKind.Manual
            : ProcessArtifactProducerKind.Unknown;
    }

    private static bool IsProducerAllowedForMode(
        ProcessArtifactExpectationMode mode,
        ProcessArtifactProducerKind producerKind,
        DispatchArtifactExpectation expectation)
    {
        return mode switch
        {
            ProcessArtifactExpectationMode.Narrative => producerKind != ProcessArtifactProducerKind.WorkflowRun,
            ProcessArtifactExpectationMode.Decision => producerKind is not ProcessArtifactProducerKind.WorkflowRun and not ProcessArtifactProducerKind.ProviderNativeBrowser,
            ProcessArtifactExpectationMode.Evidence => producerKind is not ProcessArtifactProducerKind.AssistantResponse and not ProcessArtifactProducerKind.CompletedDecision,
            ProcessArtifactExpectationMode.Deliverable => producerKind is
                ProcessArtifactProducerKind.AgentExecutionArtifact or
                ProcessArtifactProducerKind.WorkspaceWrite or
                ProcessArtifactProducerKind.ExistingManagedFile or
                ProcessArtifactProducerKind.WorkflowArtifact or
                ProcessArtifactProducerKind.SubprocessArtifact or
                ProcessArtifactProducerKind.ProcessMock or
                ProcessArtifactProducerKind.ManagerRecovery or
                ProcessArtifactProducerKind.Manual,
            ProcessArtifactExpectationMode.RuntimeProof => producerKind is
                ProcessArtifactProducerKind.AgentExecutionArtifact or
                ProcessArtifactProducerKind.WorkspaceWrite or
                ProcessArtifactProducerKind.ProviderNativeBrowser or
                ProcessArtifactProducerKind.WorkflowArtifact or
                ProcessArtifactProducerKind.SubprocessArtifact or
                ProcessArtifactProducerKind.ProcessMock or
                ProcessArtifactProducerKind.ManagerRecovery or
                ProcessArtifactProducerKind.Manual,
            ProcessArtifactExpectationMode.RecoveryDiagnostic => false,
            _ => false
        };
    }

    private static bool RequiresManagedEvidencePath(
        ProcessArtifactExpectationMode mode,
        ProcessArtifactProducerKind producerKind)
    {
        if (producerKind == ProcessArtifactProducerKind.WorkflowArtifact)
        {
            return false;
        }

        return mode is ProcessArtifactExpectationMode.Evidence or
            ProcessArtifactExpectationMode.Deliverable or
            ProcessArtifactExpectationMode.RuntimeProof;
    }

    private static bool IsCurrentRunArtifact(
        ProcessArtifactRecord artifact,
        ProcessArtifactProducerKind producerKind,
        Guid processRunId,
        Guid stepRunId,
        Guid? executionRunId,
        Guid? workflowRunId,
        Guid? subprocessRunId,
        Guid? recoveryExecutionRunId = null,
        Guid? recoveredForExecutionRunId = null)
    {
        if (artifact.ProcessRunId != processRunId || artifact.StepRunId != stepRunId)
        {
            return false;
        }

        var lineage = ProcessArtifactProjectionLineageJson.Deserialize(artifact.ProjectionLineageJson);
        if (lineage is not null && (lineage.SourceKind != ProcessArtifactProjectionSourceKind.Unknown || IsManagerRecoveryLineage(lineage)))
        {
            return IsCurrentRunArtifact(
                lineage,
                producerKind,
                executionRunId,
                workflowRunId,
                subprocessRunId,
                recoveryExecutionRunId,
                recoveredForExecutionRunId);
        }

        var key = artifact.ExternalReferenceKey;
        var provenance = artifact.ProvenanceSummary;
        return producerKind switch
        {
            ProcessArtifactProducerKind.AgentExecutionArtifact or
            ProcessArtifactProducerKind.WorkspaceWrite or
            ProcessArtifactProducerKind.ExistingManagedFile or
            ProcessArtifactProducerKind.AssistantResponse or
            ProcessArtifactProducerKind.ProviderNativeBrowser => executionRunId.HasValue &&
                ContainsGuidToken(key, executionRunId.Value) ||
                executionRunId.HasValue &&
                ContainsGuidToken(provenance, executionRunId.Value),
            ProcessArtifactProducerKind.WorkflowRun or
            ProcessArtifactProducerKind.WorkflowArtifact => workflowRunId.HasValue &&
                ContainsGuidToken(key, workflowRunId.Value),
            ProcessArtifactProducerKind.SubprocessArtifact => subprocessRunId.HasValue &&
                ContainsGuidToken(key, subprocessRunId.Value),
            ProcessArtifactProducerKind.ManagerRecovery => IsCurrentManagerRecoveryArtifact(
                key,
                provenance,
                executionRunId,
                recoveryExecutionRunId,
                recoveredForExecutionRunId),
            ProcessArtifactProducerKind.CompletedDecision or
            ProcessArtifactProducerKind.ProcessMock or
            ProcessArtifactProducerKind.Manual => true,
            _ => string.IsNullOrWhiteSpace(key)
        };
    }

    private static ProcessArtifactProducerKind ResolveArtifactProducerKind(ProcessArtifactProjectionSourceKind sourceKind)
    {
        return sourceKind switch
        {
            ProcessArtifactProjectionSourceKind.AgentExecutionArtifact => ProcessArtifactProducerKind.AgentExecutionArtifact,
            ProcessArtifactProjectionSourceKind.WorkspaceWrite => ProcessArtifactProducerKind.WorkspaceWrite,
            ProcessArtifactProjectionSourceKind.ExistingManagedFile => ProcessArtifactProducerKind.ExistingManagedFile,
            ProcessArtifactProjectionSourceKind.AssistantResponse => ProcessArtifactProducerKind.AssistantResponse,
            ProcessArtifactProjectionSourceKind.WorkflowRun => ProcessArtifactProducerKind.WorkflowRun,
            ProcessArtifactProjectionSourceKind.WorkflowArtifact => ProcessArtifactProducerKind.WorkflowArtifact,
            ProcessArtifactProjectionSourceKind.SubprocessArtifact => ProcessArtifactProducerKind.SubprocessArtifact,
            ProcessArtifactProjectionSourceKind.CompletedDecision => ProcessArtifactProducerKind.CompletedDecision,
            ProcessArtifactProjectionSourceKind.ProcessMock => ProcessArtifactProducerKind.ProcessMock,
            ProcessArtifactProjectionSourceKind.ProviderNativeBrowser => ProcessArtifactProducerKind.ProviderNativeBrowser,
            ProcessArtifactProjectionSourceKind.Manual => ProcessArtifactProducerKind.Manual,
            _ => ProcessArtifactProducerKind.Unknown
        };
    }

    private static bool IsCurrentRunArtifact(
        ProcessArtifactProjectionLineage lineage,
        ProcessArtifactProducerKind producerKind,
        Guid? executionRunId,
        Guid? workflowRunId,
        Guid? subprocessRunId,
        Guid? recoveryExecutionRunId,
        Guid? recoveredForExecutionRunId)
    {
        if (producerKind == ProcessArtifactProducerKind.ManagerRecovery || IsManagerRecoveryLineage(lineage))
        {
            return IsCurrentManagerRecoveryArtifact(lineage, executionRunId, recoveryExecutionRunId, recoveredForExecutionRunId);
        }

        return producerKind switch
        {
            ProcessArtifactProducerKind.AgentExecutionArtifact or
            ProcessArtifactProducerKind.WorkspaceWrite or
            ProcessArtifactProducerKind.ExistingManagedFile or
            ProcessArtifactProducerKind.AssistantResponse or
            ProcessArtifactProducerKind.ProviderNativeBrowser => executionRunId.HasValue &&
                lineage.SourceExecutionRunId == executionRunId.Value,
            ProcessArtifactProducerKind.WorkflowRun or
            ProcessArtifactProducerKind.WorkflowArtifact => workflowRunId.HasValue &&
                lineage.WorkflowRunId == workflowRunId.Value,
            ProcessArtifactProducerKind.SubprocessArtifact => subprocessRunId.HasValue &&
                lineage.SubprocessRunId == subprocessRunId.Value,
            ProcessArtifactProducerKind.CompletedDecision or
            ProcessArtifactProducerKind.ProcessMock or
            ProcessArtifactProducerKind.Manual => true,
            _ => false
        };
    }

    private static bool IsManagerRecoveryLineage(ProcessArtifactProjectionLineage lineage)
    {
        return lineage.RecoveryExecutionRunId.HasValue && lineage.RecoveredForExecutionRunId.HasValue;
    }

    private static bool IsCurrentManagerRecoveryArtifact(
        ProcessArtifactProjectionLineage lineage,
        Guid? executionRunId,
        Guid? recoveryExecutionRunId,
        Guid? recoveredForExecutionRunId)
    {
        var effectiveRecoveryExecutionRunId = recoveryExecutionRunId ?? executionRunId;
        return effectiveRecoveryExecutionRunId.HasValue &&
               lineage.RecoveryExecutionRunId == effectiveRecoveryExecutionRunId.Value &&
               recoveredForExecutionRunId.HasValue &&
               lineage.RecoveredForExecutionRunId == recoveredForExecutionRunId.Value;
    }

    private static bool IsCurrentManagerRecoveryArtifact(
        string key,
        string provenance,
        Guid? executionRunId,
        Guid? recoveryExecutionRunId,
        Guid? recoveredForExecutionRunId)
    {
        var effectiveRecoveryExecutionRunId = recoveryExecutionRunId ?? executionRunId;
        if (!effectiveRecoveryExecutionRunId.HasValue)
        {
            return false;
        }

        if (!ContainsGuidToken(key, effectiveRecoveryExecutionRunId.Value) &&
            !ContainsGuidToken(provenance, effectiveRecoveryExecutionRunId.Value))
        {
            return false;
        }

        if (!recoveredForExecutionRunId.HasValue)
        {
            return false;
        }

        return ContainsGuidToken(key, recoveredForExecutionRunId.Value) ||
               ContainsGuidToken(provenance, recoveredForExecutionRunId.Value);
    }

    private static bool ContainsGuidToken(string? text, Guid value)
    {
        return !string.IsNullOrWhiteSpace(text) &&
               text.Contains(value.ToString("D"), StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesDeclaredFormat(
        DispatchArtifactExpectation expectation,
        ProcessArtifactRecord artifact,
        ProcessArtifactExpectationMode mode,
        ProcessArtifactProducerKind producerKind,
        IProcessArtifactContentReader? managedArtifactContentReader,
        out string diagnostic)
    {
        diagnostic = string.Empty;
        var contractText = CollapsePromptWhitespace(string.Join(' ', expectation.Title, expectation.ValidationRequirementSummary)).ToLowerInvariant();
        var extension = ResolveManagedArtifactExtension(artifact.ManagedStoragePath);
        var requiresStoredContent = managedArtifactContentReader is not null && RequiresManagedEvidencePath(mode, producerKind);
        ProcessArtifactContentReadResult? content = null;
        var contentRead = false;

        bool TryReadStoredContent(
            out ProcessArtifactContentReadResult? readContent,
            out string readDiagnostic)
        {
            readDiagnostic = string.Empty;
            if (contentRead)
            {
                readContent = content;
                return true;
            }

            contentRead = true;
            if (!TryReadManagedArtifactContent(artifact, managedArtifactContentReader, out content, out readDiagnostic))
            {
                readContent = null;
                return false;
            }

            readContent = content;
            return true;
        }

        var requiresJson = contractText.Contains("json", StringComparison.Ordinal);
        if (requiresJson && !string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase))
        {
            diagnostic = "The artifact contract declares JSON, but the managed artifact path is not a .json file.";
            return false;
        }

        if (requiresJson && !HasValidJsonArtifactContent(artifact, managedArtifactContentReader, out diagnostic))
        {
            return false;
        }

        var requiresYaml =
            contractText.Contains("yaml", StringComparison.Ordinal) ||
            contractText.Contains(".yml", StringComparison.Ordinal) ||
            contractText.Contains(".yaml", StringComparison.Ordinal);
        if (requiresYaml && extension is not ".yml" and not ".yaml")
        {
            diagnostic = "The artifact contract declares YAML, but the managed artifact path is not a .yml or .yaml file.";
            return false;
        }

        if (requiresYaml &&
            requiresStoredContent &&
            (!TryReadStoredContent(out var yamlContent, out diagnostic) ||
             !HasReadableTextArtifactContent(yamlContent, "YAML", out diagnostic)))
        {
            return false;
        }

        var requiresMarkdown = contractText.Contains("markdown", StringComparison.Ordinal) || contractText.Contains(".md", StringComparison.Ordinal);
        if (requiresMarkdown &&
            !string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase))
        {
            diagnostic = "The artifact contract declares Markdown, but the managed artifact path is not a .md file.";
            return false;
        }

        if (requiresMarkdown &&
            requiresStoredContent &&
            (!TryReadStoredContent(out var markdownContent, out diagnostic) ||
             !HasReadableTextArtifactContent(markdownContent, "Markdown", out diagnostic)))
        {
            return false;
        }

        var requiresImage = contractText.Contains("screenshot", StringComparison.Ordinal) || contractText.Contains("image", StringComparison.Ordinal);
        if (requiresImage &&
            extension is not ".png" and not ".jpg" and not ".jpeg" and not ".webp" and not ".svg")
        {
            diagnostic = "The artifact contract declares image or screenshot evidence, but the managed artifact path is not an image file.";
            return false;
        }

        if (requiresImage &&
            requiresStoredContent &&
            (!TryReadStoredContent(out var imageContent, out diagnostic) ||
             !HasValidImageArtifactContent(imageContent, out diagnostic)))
        {
            return false;
        }

        if (requiresStoredContent &&
            mode is ProcessArtifactExpectationMode.Evidence or ProcessArtifactExpectationMode.RuntimeProof &&
            !TryReadStoredContent(out _, out diagnostic))
        {
            return false;
        }

        return true;
    }

    private static string ResolveManagedArtifactExtension(string managedStoragePath)
    {
        if (StorageJson.TryParseReference(managedStoragePath, out var reference) && reference is not null)
        {
            return Path.GetExtension(reference.Locator).ToLowerInvariant();
        }

        return Path.GetExtension(managedStoragePath).ToLowerInvariant();
    }

    private static bool ContainsPlaceholderArtifactSignal(
        ProcessArtifactRecord artifact,
        ProcessArtifactExpectationMode mode)
    {
        var text = CollapsePromptWhitespace(string.Join(
            ' ',
            artifact.Title,
            artifact.ReviewSummary,
            artifact.ProvenanceSummary,
            artifact.ManagedStoragePath,
            artifact.ExternalReferenceKey)).ToLowerInvariant();
        if (IsLegitimateUnavailableOrPlanningArtifact(text, mode))
        {
            return false;
        }

        return text.Contains("placeholder", StringComparison.Ordinal) ||
               text.Contains("gap marker", StringComparison.Ordinal) ||
               text.Contains("missing artifact diagnostic", StringComparison.Ordinal) ||
               text.Contains("artifact is not available", StringComparison.Ordinal) ||
               text.Contains("no artifact available", StringComparison.Ordinal);
    }

    private static bool IsLegitimateUnavailableOrPlanningArtifact(
        string text,
        ProcessArtifactExpectationMode mode)
    {
        if (mode is not (ProcessArtifactExpectationMode.Narrative or ProcessArtifactExpectationMode.Decision or ProcessArtifactExpectationMode.Deliverable))
        {
            return false;
        }

        return text.Contains("todo register", StringComparison.Ordinal) ||
               text.Contains("todo list", StringComparison.Ordinal) ||
               text.Contains("unavailable findings", StringComparison.Ordinal) ||
               text.Contains("not available finding", StringComparison.Ordinal) ||
               text.Contains("missing artifact analysis", StringComparison.Ordinal) ||
               text.Contains("missing-artifact analysis", StringComparison.Ordinal);
    }

    private static bool HasValidJsonArtifactContent(
        ProcessArtifactRecord artifact,
        IProcessArtifactContentReader? managedArtifactContentReader,
        out string diagnostic)
    {
        diagnostic = string.Empty;
        if (managedArtifactContentReader is not null)
        {
            if (!TryReadManagedArtifactContent(artifact, managedArtifactContentReader, out var content, out diagnostic))
            {
                return false;
            }

            if (content?.TextContent is null)
            {
                diagnostic = $"The artifact contract declares JSON, but the managed artifact content type '{content?.ContentType ?? "unknown"}' is not readable text.";
                return false;
            }

            try
            {
                using var _ = JsonDocument.Parse(content.TextContent);
                return true;
            }
            catch (JsonException exception)
            {
                diagnostic = $"The artifact contract declares JSON, but the managed artifact content is malformed JSON: {exception.Message}";
                return false;
            }
        }

        if (Path.IsPathRooted(artifact.ManagedStoragePath) && File.Exists(artifact.ManagedStoragePath))
        {
            try
            {
                using var stream = File.OpenRead(artifact.ManagedStoragePath);
                using var _ = JsonDocument.Parse(stream);
                return true;
            }
            catch (JsonException exception)
            {
                diagnostic = $"The artifact contract declares JSON, but the managed artifact content is malformed JSON: {exception.Message}";
                return false;
            }
            catch (IOException exception)
            {
                diagnostic = $"The artifact contract declares JSON, but the managed artifact content could not be read: {exception.Message}";
                return false;
            }
        }

        if (TryResolveInlineJsonArtifactContent(artifact, out var jsonContent))
        {
            try
            {
                using var _ = JsonDocument.Parse(jsonContent);
                return true;
            }
            catch (JsonException exception)
            {
                diagnostic = $"The artifact contract declares JSON, but the recorded JSON content is malformed: {exception.Message}";
                return false;
            }
        }

        return true;
    }

    private static bool TryReadManagedArtifactContent(
        ProcessArtifactRecord artifact,
        IProcessArtifactContentReader? managedArtifactContentReader,
        out ProcessArtifactContentReadResult? content,
        out string diagnostic)
    {
        content = null;
        diagnostic = string.Empty;
        if (managedArtifactContentReader is null)
        {
            return true;
        }

        content = managedArtifactContentReader.Read(artifact.ManagedStoragePath);
        if (!content.Succeeded)
        {
            diagnostic = $"The managed artifact content could not be loaded from '{artifact.ManagedStoragePath}': {content.Diagnostic}";
            return false;
        }

        if (content.ByteLength == 0)
        {
            diagnostic = $"The managed artifact content at '{artifact.ManagedStoragePath}' is empty.";
            return false;
        }

        return true;
    }

    private static bool TryValidateManagedArtifactContent(
        ProcessArtifactRecord artifact,
        IProcessArtifactContentReader managedArtifactContentReader,
        out string diagnostic,
        out ProcessArtifactValidationStatus status)
    {
        status = ProcessArtifactValidationStatus.Satisfied;
        if (!TryReadManagedArtifactContent(artifact, managedArtifactContentReader, out var content, out diagnostic))
        {
            status = ProcessArtifactValidationStatus.ContentUnavailable;
            return false;
        }

        var lineage = ProcessArtifactProjectionLineageJson.Deserialize(artifact.ProjectionLineageJson);
        var expectedContentHash = lineage?.ContentHash?.Trim();
        if (string.IsNullOrWhiteSpace(expectedContentHash))
        {
            return true;
        }

        var actualContentHash = ProcessArtifactIdentityService.ComputeContentHash(content?.ContentBytes ?? []);
        if (string.Equals(expectedContentHash, actualContentHash, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        diagnostic = "The managed artifact content hash does not match the recorded projection lineage content hash.";
        status = ProcessArtifactValidationStatus.ContentHashMismatch;
        return false;
    }

    private static bool IsConcreteProductMutationReceipt(ToolExecutionReceiptRecord receipt)
    {
        if (!ConcreteProductMutationToolNames.Contains(receipt.ToolName))
        {
            return false;
        }

        var summary = CollapsePromptWhitespace(string.Join(' ', receipt.RequestSummary, receipt.WorkingDirectory));
        if (summary.Contains("external-target/", StringComparison.OrdinalIgnoreCase))
        {
            return !summary.Contains("/artifact", StringComparison.OrdinalIgnoreCase) &&
                   !summary.Contains("/artifacts", StringComparison.OrdinalIgnoreCase) &&
                   !summary.Contains("/evidence", StringComparison.OrdinalIgnoreCase) &&
                   !summary.Contains("/output", StringComparison.OrdinalIgnoreCase);
        }

        return summary.Contains("/src/", StringComparison.OrdinalIgnoreCase) ||
               summary.Contains("\\src\\", StringComparison.OrdinalIgnoreCase) ||
               summary.Contains("/tests/", StringComparison.OrdinalIgnoreCase) ||
               summary.Contains("\\tests\\", StringComparison.OrdinalIgnoreCase) ||
               summary.Contains(" output/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWrongRootArtifact(ProcessArtifactRecord artifact)
    {
        if (string.IsNullOrWhiteSpace(artifact.ManagedStoragePath))
        {
            return false;
        }

        var normalizedPath = artifact.ManagedStoragePath.Replace('\\', '/').Trim().TrimStart('/');
        if (StorageJson.TryParseReference(normalizedPath, out _))
        {
            return false;
        }

        if (normalizedPath.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedAlias = NormalizeExternalTargetAlias(normalizedPath);
            return !IsExternalArtifactDestinationAlias(normalizedAlias);
        }

        if (string.Equals(normalizedPath, "output", StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith("output/", StringComparison.OrdinalIgnoreCase)) {
            return !IsCurrentRunManagedOutputArtifactPath(artifact.ProcessRunId, normalizedPath);
        }

        return normalizedPath.StartsWith("src/", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.StartsWith("tests/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCurrentRunManagedOutputArtifactPath(Guid processRunId, string normalizedPath) {
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 3 ||
            !string.Equals(segments[0], "output", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        return ContainsCurrentRunPathSegment(segments, "process-runs", processRunId.ToString("D")) ||
               ContainsCurrentRunPathSegment(segments, "process-runs", processRunId.ToString("N")) ||
               ContainsCurrentRunPathSegment(segments, "process-mock", ResolveProcessMockRunKey(processRunId));
    }

    private static bool ContainsCurrentRunPathSegment(
        IReadOnlyList<string> segments,
        string markerSegment,
        string expectedValue) {
        if (string.IsNullOrWhiteSpace(expectedValue)) {
            return false;
        }

        for (var index = 0; index < segments.Count - 1; index++) {
            if (string.Equals(segments[index], markerSegment, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(segments[index + 1], expectedValue, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }

    private static string ResolveProcessMockRunKey(Guid processRunId) {
        var normalized = processRunId.ToString("N").ToLowerInvariant();
        return normalized.Length <= 16
            ? normalized
            : normalized[..16];
    }

    private static bool RequiresProjectionLineage(ProcessArtifactRecord artifact)
    {
        return artifact.ArtifactKind is ProcessArtifactKind.Evidence or ProcessArtifactKind.Deliverable;
    }

    private static bool IsExternalArtifactDestinationAlias(string normalizedAlias)
    {
        var segments = normalizedAlias.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Any(segment =>
            string.Equals(segment, "artifact", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "artifacts", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "evidence", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "output", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "report", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "reports", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasReadableTextArtifactContent(
        ProcessArtifactContentReadResult? content,
        string declaredFormat,
        out string diagnostic)
    {
        if (content is null)
        {
            diagnostic = string.Empty;
            return true;
        }

        if (string.IsNullOrWhiteSpace(content.TextContent))
        {
            diagnostic = $"The artifact contract declares {declaredFormat}, but the managed artifact content type '{content.ContentType}' is not readable non-empty text.";
            return false;
        }

        diagnostic = string.Empty;
        return true;
    }

    private static bool HasValidImageArtifactContent(
        ProcessArtifactContentReadResult? content,
        out string diagnostic)
    {
        if (content is null)
        {
            diagnostic = string.Empty;
            return true;
        }

        if (!content.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            diagnostic = $"The artifact contract declares image or screenshot evidence, but the managed artifact content type is '{content.ContentType}'.";
            return false;
        }

        var extension = Path.GetExtension(content.ResolvedPath).ToLowerInvariant();
        var bytes = content.ContentBytes;
        var isValidImage = extension switch
        {
            ".png" => bytes.Length >= 8 &&
                bytes[0] == 0x89 &&
                bytes[1] == 0x50 &&
                bytes[2] == 0x4E &&
                bytes[3] == 0x47 &&
                bytes[4] == 0x0D &&
                bytes[5] == 0x0A &&
                bytes[6] == 0x1A &&
                bytes[7] == 0x0A,
            ".jpg" or ".jpeg" => bytes.Length >= 2 &&
                bytes[0] == 0xFF &&
                bytes[1] == 0xD8,
            ".webp" => bytes.Length >= 12 &&
                bytes[0] == (byte)'R' &&
                bytes[1] == (byte)'I' &&
                bytes[2] == (byte)'F' &&
                bytes[3] == (byte)'F' &&
                bytes[8] == (byte)'W' &&
                bytes[9] == (byte)'E' &&
                bytes[10] == (byte)'B' &&
                bytes[11] == (byte)'P',
            ".svg" => content.TextContent?.Contains("<svg", StringComparison.OrdinalIgnoreCase) == true,
            _ => false
        };

        if (isValidImage)
        {
            diagnostic = string.Empty;
            return true;
        }

        diagnostic = "The artifact contract declares image or screenshot evidence, but the stored bytes do not match the declared image format.";
        return false;
    }

    private static string? TryDecodeManagedArtifactTextContent(
        string contentType,
        string fullPath,
        byte[] contentBytes)
    {
        if (!IsTextReadableArtifactContent(contentType, fullPath))
        {
            return null;
        }

        if (contentBytes.Contains((byte)0))
        {
            return null;
        }

        try
        {
            return StrictUtf8Encoding.GetString(contentBytes);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static bool IsTextReadableArtifactContent(string contentType, string fullPath)
    {
        return contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("json", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("xml", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("yaml", StringComparison.OrdinalIgnoreCase) ||
               IsTextReadableManagedArtifactPath(fullPath);
    }

    private static bool TryResolveInlineJsonArtifactContent(
        ProcessArtifactRecord artifact,
        out string jsonContent)
    {
        jsonContent = string.Empty;
        foreach (var text in new[] { artifact.ReviewSummary, artifact.ProvenanceSummary })
        {
            var trimmed = text.Trim();
            if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
            {
                jsonContent = trimmed;
                return true;
            }

            const string jsonContentPrefix = "json content:";
            var prefixIndex = trimmed.IndexOf(jsonContentPrefix, StringComparison.OrdinalIgnoreCase);
            if (prefixIndex < 0)
            {
                continue;
            }

            jsonContent = trimmed[(prefixIndex + jsonContentPrefix.Length)..].Trim();
            return !string.IsNullOrWhiteSpace(jsonContent);
        }

        return false;
    }

    private static Guid? ResolveWorkflowRunIdForStep(IReadOnlyList<ProcessArtifactRecord> artifacts)
    {
        foreach (var artifact in artifacts)
        {
            var lineage = ProcessArtifactProjectionLineageJson.Deserialize(artifact.ProjectionLineageJson);
            if (lineage?.WorkflowRunId.HasValue == true)
            {
                return lineage.WorkflowRunId.Value;
            }

            var key = artifact.ExternalReferenceKey;
            if (!key.StartsWith("workflow-run:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var start = "workflow-run:".Length;
            var length = Math.Min(36, key.Length - start);
            if (length <= 0)
            {
                continue;
            }

            if (Guid.TryParse(key.Substring(start, length), out var workflowRunId))
            {
                return workflowRunId;
            }
        }

        return null;
    }

    private static Guid? ResolveSubprocessRunIdForStep(IReadOnlyList<ProcessArtifactRecord> artifacts)
    {
        foreach (var artifact in artifacts)
        {
            var lineage = ProcessArtifactProjectionLineageJson.Deserialize(artifact.ProjectionLineageJson);
            if (lineage?.SubprocessRunId.HasValue == true)
            {
                return lineage.SubprocessRunId.Value;
            }

            var key = artifact.ExternalReferenceKey;
            if (!key.StartsWith("subprocess-run:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var start = "subprocess-run:".Length;
            var length = Math.Min(36, key.Length - start);
            if (length <= 0)
            {
                continue;
            }

            if (Guid.TryParse(key.Substring(start, length), out var subprocessRunId))
            {
                return subprocessRunId;
            }
        }

        return null;
    }

    private static ProcessArtifactExpectationValidationResult CreateArtifactValidationResult(
        Guid processRunId,
        Guid stepRunId,
        DispatchArtifactExpectation expectation,
        ProcessArtifactExpectationMode mode,
        ProcessArtifactValidationStatus status,
        ProcessArtifactProducerKind producerKind,
        ProcessArtifactRecord? artifact,
        string attemptedPath,
        string diagnostic,
        string suggestedAction,
        ProcessStepCompletionExecutorKind executorKind,
        Guid? executionRunId,
        Guid? workflowRunId,
        Guid? subprocessRunId,
        Guid? recoveryExecutionRunId = null,
        Guid? recoveredForExecutionRunId = null)
    {
        var failureOwnership = ResolveArtifactFailureOwnership(mode, status, diagnostic);
        var fingerprint = CreateArtifactFailureFingerprint(
            processRunId,
            stepRunId,
            expectation.Id,
            status,
            attemptedPath,
            mode,
            failureOwnership,
            expectation.ValidationRequirementSummary,
            executorKind,
            executionRunId,
            workflowRunId,
            subprocessRunId,
            recoveryExecutionRunId,
            recoveredForExecutionRunId);
        return new ProcessArtifactExpectationValidationResult(
            expectation.Id,
            expectation.Title,
            mode,
            status,
            producerKind,
            artifact?.Id,
            attemptedPath,
            diagnostic,
            suggestedAction,
            fingerprint,
            failureOwnership);
    }

    private static ProcessArtifactFailureOwnership ResolveArtifactFailureOwnership(
        ProcessArtifactExpectationMode mode,
        ProcessArtifactValidationStatus status,
        string diagnostic)
    {
        if (diagnostic.Contains("upstream", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactFailureOwnership.UpstreamInput;
        }

        if (status is ProcessArtifactValidationStatus.Missing or
            ProcessArtifactValidationStatus.InvalidFormat or
            ProcessArtifactValidationStatus.PlaceholderOnly or
            ProcessArtifactValidationStatus.StaleOrWrongRun or
            ProcessArtifactValidationStatus.ContentUnavailable or
            ProcessArtifactValidationStatus.ContentHashMismatch)
        {
            return ProcessArtifactFailureOwnership.OwnOutput;
        }

        return mode switch
        {
            ProcessArtifactExpectationMode.Decision => ProcessArtifactFailureOwnership.ReviewDisposition,
            ProcessArtifactExpectationMode.Evidence or ProcessArtifactExpectationMode.RuntimeProof => ProcessArtifactFailureOwnership.RuntimeEvidence,
            _ => ProcessArtifactFailureOwnership.OwnOutput
        };
    }

    private static string CreateArtifactFailureFingerprint(
        Guid processRunId,
        Guid stepRunId,
        Guid expectationId,
        ProcessArtifactValidationStatus status,
        string attemptedPath,
        ProcessArtifactExpectationMode mode,
        ProcessArtifactFailureOwnership failureOwnership,
        string validationRequirementSummary,
        ProcessStepCompletionExecutorKind executorKind,
        Guid? executionRunId,
        Guid? workflowRunId,
        Guid? subprocessRunId,
        Guid? recoveryExecutionRunId = null,
        Guid? recoveredForExecutionRunId = null)
    {
        var normalized = string.Join(
            "|",
            processRunId.ToString("D"),
            stepRunId.ToString("D"),
            expectationId.ToString("D"),
            status,
            NormalizeManagedRelativePathForComparison(attemptedPath),
            mode,
            failureOwnership,
            CollapsePromptWhitespace(validationRequirementSummary).ToLowerInvariant(),
            executorKind,
            executionRunId?.ToString("D") ?? string.Empty,
            workflowRunId?.ToString("D") ?? string.Empty,
            subprocessRunId?.ToString("D") ?? string.Empty,
            recoveryExecutionRunId?.ToString("D") ?? string.Empty,
            recoveredForExecutionRunId?.ToString("D") ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    }
}
