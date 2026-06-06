using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;
using System.Text;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private sealed record ArtifactProjectionCoordinatorContext(
        DispatchCandidate Candidate,
        ProcessAutomationExecutionRunDetail Detail,
        string ResponseText,
        string WorkspaceRoot,
        WorkspaceScopeDescriptor WorkspaceScope,
        ProcessArtifactProjectionWriteCoordinator WriteCoordinator,
        ProcessArtifactProjectionRecordOnlyCoordinator RecordOnlyCoordinator,
        ILogger<ProcessRunAutomationDispatchService> Logger,
        ProcessStepRunStatus CompletionStatus,
        CancellationToken CancellationToken,
        ArtifactProjectionLineage? Lineage)
    {
        public ProcessArtifactRecoveryProjectionContext RecoveryContext { get; } = CreateArtifactRecoveryProjectionContext(Lineage);
    }

    private static class ProcessArtifactProjectionCandidateState
    {
        public static bool TryApplyExpectedWriteOutcome(
            DispatchCandidate candidate,
            DispatchArtifactExpectation expectedArtifact,
            Result<ProcessArtifactProjectionWriteResult> writeResult,
            out string errorSummary)
        {
            return TryApplyWriteOutcome(
                candidate,
                writeResult,
                expectedArtifact.Id,
                out errorSummary);
        }

        public static bool TryApplyWriteOutcome(
            DispatchCandidate candidate,
            Result<ProcessArtifactProjectionWriteResult> writeResult,
            Guid? expectedArtifactId,
            out string errorSummary)
        {
            if (writeResult.IsFailure)
            {
                errorSummary = string.Join(" | ", writeResult.Errors.Select(error => error.Message));
                return false;
            }

            if (writeResult.Value is not { } writeOutcome)
            {
                errorSummary = "Coordinator completed without a projection write outcome.";
                return false;
            }

            if (!TryValidateArtifactExpectationId(expectedArtifactId, writeOutcome.ArtifactExpectationId, out errorSummary))
            {
                return false;
            }

            Apply(candidate, writeOutcome.ExternalReferenceKey, writeOutcome.ArtifactExpectationId);
            errorSummary = string.Empty;
            return true;
        }

        public static bool TryApplyExpectedRecordOnlyOutcome(
            DispatchCandidate candidate,
            DispatchArtifactExpectation expectedArtifact,
            Result<ProcessArtifactProjectionRecordOnlyResult> recordResult,
            out string errorSummary)
        {
            if (recordResult.IsFailure)
            {
                errorSummary = string.Join(" | ", recordResult.Errors.Select(error => error.Message));
                return false;
            }

            if (recordResult.Value is not { } recordOutcome)
            {
                errorSummary = "Record-only coordinator completed without an artifact record outcome.";
                return false;
            }

            if (!TryValidateArtifactExpectationId(expectedArtifact.Id, recordOutcome.ArtifactExpectationId, out errorSummary))
            {
                return false;
            }

            Apply(candidate, recordOutcome.ExternalReferenceKey, recordOutcome.ArtifactExpectationId);
            errorSummary = string.Empty;
            return true;
        }

        private static bool TryValidateArtifactExpectationId(
            Guid? expectedArtifactId,
            Guid? actualArtifactExpectationId,
            out string errorSummary)
        {
            if (expectedArtifactId.HasValue && !actualArtifactExpectationId.HasValue)
            {
                errorSummary = "Coordinator completed without an artifact expectation id.";
                return false;
            }

            if (expectedArtifactId is { } expectedId &&
                actualArtifactExpectationId is { } actualId &&
                actualId != expectedId)
            {
                errorSummary = $"Coordinator returned artifact expectation id '{actualId:D}' instead of '{expectedId:D}'.";
                return false;
            }

            if (expectedArtifactId is null &&
                actualArtifactExpectationId is { } unexpectedId)
            {
                errorSummary = $"Coordinator returned unexpected artifact expectation id '{unexpectedId:D}'.";
                return false;
            }

            errorSummary = string.Empty;
            return true;
        }

        private static void Apply(
            DispatchCandidate candidate,
            string externalReferenceKey,
            Guid? artifactExpectationId)
        {
            candidate.ExternalReferenceKeys.Add(externalReferenceKey);
            if (artifactExpectationId is { } expectationId)
            {
                candidate.RecordedArtifactExpectationIds.Add(expectationId);
            }
        }
    }

    private sealed class ProcessExecutionArtifactProjectionCoordinator
    {
        private readonly ProcessRunAutomationDispatchService dispatchService;

        public ProcessExecutionArtifactProjectionCoordinator(ProcessRunAutomationDispatchService dispatchService)
        {
            this.dispatchService = dispatchService;
        }

        public async Task ProjectAsync(
            ArtifactProjectionCoordinatorContext context,
            ProcessStepDispatchClaim dispatchClaim)
        {
            foreach (var artifact in context.Detail.Artifacts)
            {
                await dispatchService.EnsureStepDispatchClaimHeldAsync(dispatchClaim, context.CancellationToken);
                if (IsTransientExecutionArtifact(artifact))
                {
                    context.Logger.LogDebug(
                        "Skipping transient execution artifact projection for run {RunId}, step {StepRunId}, artifact {ArtifactId}, path {RelativePath}.",
                        context.Candidate.Run.Id,
                        context.Candidate.StepRun.Id,
                        artifact.Id,
                        artifact.RelativePath);
                    continue;
                }

                var sourceExternalReferenceKey = ProcessArtifactProjectionPlanner.BuildExecutionArtifactExternalReferenceKey(artifact.Id);
                var externalReferenceKey = ProcessArtifactProjectionLineageBuilder.ApplyRecoveryLineage(
                    sourceExternalReferenceKey,
                    context.Detail.Run.Id,
                    context.RecoveryContext);
                if (context.Candidate.ExternalReferenceKeys.Contains(externalReferenceKey))
                {
                    continue;
                }

                if (!TryResolveArtifactFullPath(context.WorkspaceRoot, artifact.RelativePath, out var fullPath, out var pathResolutionFailure) ||
                    !File.Exists(fullPath))
                {
                    context.Logger.LogDebug(
                        "Skipping execution artifact projection for run {RunId}, step {StepRunId}, artifact {ArtifactId} because the file path is unavailable. Reason: {Reason}",
                        context.Candidate.Run.Id,
                        context.Candidate.StepRun.Id,
                        artifact.Id,
                        string.IsNullOrWhiteSpace(pathResolutionFailure) ? "File does not exist." : pathResolutionFailure);
                    continue;
                }

                byte[] content;
                try
                {
                    content = await File.ReadAllBytesAsync(fullPath, context.CancellationToken);
                }
                catch (Exception exception)
                {
                    context.Logger.LogWarning(
                        exception,
                        "Execution artifact {ArtifactId} could not be read for process run {RunId}.",
                        artifact.Id,
                        context.Candidate.Run.Id);
                    continue;
                }

                var matchedExpectation = ResolveArtifactExpectation(
                    context.Candidate,
                    context.Detail.Run.InputSummary,
                    artifact,
                    TryDecodeTextArtifactContent(artifact, fullPath, content));
                var projectionPlan = ProcessArtifactProjectionPlanner.PlanExecutionArtifact(
                    context.Detail.Run.Id,
                    artifact,
                    matchedExpectation is null ? null : ProcessArtifactValidationSnapshotBuilder.ToProjectionExpectation(matchedExpectation),
                    ResolveProcessArtifactKind(context.Candidate, artifact),
                    context.CompletionStatus,
                    context.Detail.Run.ResultSummary,
                    context.RecoveryContext);

                var writeResult = await context.WriteCoordinator.WriteAsync(
                    new ProcessArtifactProjectionWriteRequest(
                        context.Candidate.Run.Id,
                        context.Candidate.StepRun.Id,
                        context.Candidate.Run.ProjectId,
                        projectionPlan,
                        Path.GetFileName(fullPath),
                        string.IsNullOrWhiteSpace(artifact.ContentType)
                            ? "application/octet-stream"
                            : artifact.ContentType,
                        content,
                        ResolveStorageContentKind(artifact.ContentType, fullPath),
                        BuildStorageRelativePath(context.Candidate, artifact)),
                    context.CancellationToken);
                if (!ProcessArtifactProjectionCandidateState.TryApplyWriteOutcome(
                        context.Candidate,
                        writeResult,
                        projectionPlan.ArtifactExpectationId,
                        out var errorSummary))
                {
                    context.Logger.LogWarning(
                        "Process artifact projection failed for run {RunId}, step {StepRunId}, artifact {ArtifactId}. Errors: {Errors}",
                        context.Candidate.Run.Id,
                        context.Candidate.StepRun.Id,
                        artifact.Id,
                        errorSummary);
                }
            }
        }
    }

    private sealed class ProcessMockArtifactProjectionCoordinator
    {
        private readonly ProcessRunAutomationDispatchService dispatchService;

        public ProcessMockArtifactProjectionCoordinator(ProcessRunAutomationDispatchService dispatchService)
        {
            this.dispatchService = dispatchService;
        }

        public async Task ProjectAsync(ArtifactProjectionCoordinatorContext context)
        {
            if (context.Candidate.ExpectedArtifacts.Count == 0)
            {
                return;
            }

            var projections = ResolveProcessMockArtifactProjections(context.Detail.Run.SerializedSessionStateJson);
            if (projections.Count == 0)
            {
                return;
            }

            var projectedExpectationIds = new HashSet<Guid>();
            foreach (var projection in projections)
            {
                var matchedExpectations = context.Candidate.ExpectedArtifacts
                    .Where(item => item.IsRequired && !projectedExpectationIds.Contains(item.Id))
                    .Where(item => ProcessMockArtifactMatchesExpectation(item, projection))
                    .ToList();
                if (matchedExpectations.Count == 0)
                {
                    continue;
                }

                if (matchedExpectations.Count > 1)
                {
                    throw new InvalidOperationException(
                        $"Process mock artifact '{projection.RelativePath}' for role '{projection.RoleKey}' matched multiple required artifact expectations for step '{context.Candidate.StepRun.Title}': {string.Join(", ", matchedExpectations.Select(item => item.Title))}.");
                }

                var expectedArtifact = matchedExpectations[0];
                var expectedProjection = ProcessArtifactValidationSnapshotBuilder.ToProjectionExpectation(expectedArtifact);
                var scopedRelativePath = ResolveScopedManagedRelativePath(context.WorkspaceScope, projection.RelativePath);
                var projectionSource = new ProcessMockArtifactProjectionSource(
                    context.Candidate.StepRun.Id,
                    context.Detail.Run.Id,
                    projection.RelativePath,
                    scopedRelativePath,
                    projection.RoleKey);
                var projectionPlan = ProcessMockArtifactProjectionSourceAdapter.Plan(
                    projectionSource,
                    expectedProjection,
                    context.CompletionStatus,
                    context.RecoveryContext);
                if (context.Candidate.ExternalReferenceKeys.Contains(projectionPlan.ExternalReferenceKey))
                {
                    projectedExpectationIds.Add(expectedArtifact.Id);
                    continue;
                }

                if (!TryResolveArtifactFullPath(context.WorkspaceRoot, scopedRelativePath, out var fullPath, out var pathResolutionFailure) ||
                    !File.Exists(fullPath))
                {
                    throw new InvalidOperationException(
                        $"Process mock artifact '{projection.RelativePath}' for expected artifact '{expectedArtifact.Title}' was declared by execution run {context.Detail.Run.Id:D}, but scoped path '{scopedRelativePath}' could not be found. {pathResolutionFailure}".Trim());
                }

                byte[] content;
                try
                {
                    content = await File.ReadAllBytesAsync(fullPath, context.CancellationToken);
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        $"Process mock artifact '{projection.RelativePath}' for expected artifact '{expectedArtifact.Title}' at scoped path '{scopedRelativePath}' could not be read: {exception.Message}",
                        exception);
                }

                var contentType = GuessContentTypeFromPath(fullPath);
                var writeResult = await context.WriteCoordinator.WriteAsync(
                    new ProcessArtifactProjectionWriteRequest(
                        context.Candidate.Run.Id,
                        context.Candidate.StepRun.Id,
                        context.Candidate.Run.ProjectId,
                        projectionPlan,
                        Path.GetFileName(fullPath),
                        contentType,
                        content,
                        ResolveStorageContentKind(contentType, fullPath),
                        scopedRelativePath),
                    context.CancellationToken);
                if (!ProcessArtifactProjectionCandidateState.TryApplyExpectedWriteOutcome(
                        context.Candidate,
                        expectedArtifact,
                        writeResult,
                        out var errorSummary))
                {
                    throw new InvalidOperationException(
                        $"Process mock artifact projection failed for expected artifact '{expectedArtifact.Title}': {errorSummary}");
                }

                projectedExpectationIds.Add(expectedArtifact.Id);
            }
        }
    }

    private sealed class ProcessWorkspaceWrittenArtifactProjectionCoordinator
    {
        private readonly ProcessRunAutomationDispatchService dispatchService;

        public ProcessWorkspaceWrittenArtifactProjectionCoordinator(ProcessRunAutomationDispatchService dispatchService)
        {
            this.dispatchService = dispatchService;
        }

        public async Task ProjectAsync(ArtifactProjectionCoordinatorContext context)
        {
            if (context.Candidate.ExpectedArtifacts.Count == 0)
            {
                return;
            }

            var fileWrites = ResolveSuccessfulSessionFileWrites(context.Detail.Run.SerializedSessionStateJson);
            var receiptFileWrites = ResolveSuccessfulWorkspaceFileMutationReceiptPaths(context.Detail)
                .Select(path => new SessionFileContent(path, string.Empty))
                .ToList();
            if (fileWrites.Count == 0 && receiptFileWrites.Count == 0)
            {
                return;
            }

            foreach (var expectedArtifact in context.Candidate.ExpectedArtifacts)
            {
                if (context.Candidate.RecordedArtifactExpectationIds.Contains(expectedArtifact.Id) ||
                    context.Detail.Artifacts.Any(artifact => ResolveArtifactExpectationId(context.Candidate, context.Detail, artifact) == expectedArtifact.Id))
                {
                    continue;
                }

                var matchingWrite = TryResolveProjectStructureExpectedArtifactPath(
                        context.Candidate,
                        expectedArtifact,
                        context.Detail.Run.InputSummary,
                        out var governedPath)
                    ? fileWrites.LastOrDefault(file => ArtifactPathMatchesGovernedProjectStructurePath(file.Path, governedPath)) ??
                      receiptFileWrites.LastOrDefault(file => ArtifactPathMatchesGovernedProjectStructurePath(file.Path, governedPath))
                    : fileWrites.LastOrDefault(file => WorkspaceWrittenFileMatchesExpectedArtifact(
                        context.Candidate.ExpectedArtifacts,
                        expectedArtifact,
                        file.Path,
                        file.Content)) ??
                      receiptFileWrites.LastOrDefault(file => WorkspaceWrittenFileMatchesExpectedArtifact(
                        context.Candidate.ExpectedArtifacts,
                        expectedArtifact,
                        file.Path,
                        file.Content));
                if (matchingWrite is null)
                {
                    continue;
                }

                var projectedRelativePath = ResolveWorkspaceWrittenArtifactRelativePath(context.WorkspaceScope, matchingWrite.Path);
                if (string.IsNullOrWhiteSpace(projectedRelativePath))
                {
                    continue;
                }

                var expectedProjection = ProcessArtifactValidationSnapshotBuilder.ToProjectionExpectation(expectedArtifact);
                var duplicateProbeSource = new WorkspaceWrittenArtifactProjectionSource(
                    context.Detail.Run.Id,
                    projectedRelativePath,
                    projectedRelativePath);
                var externalReferenceKey = WorkspaceWrittenArtifactProjectionSourceAdapter.BuildExternalReferenceKey(
                    duplicateProbeSource,
                    expectedProjection,
                    context.RecoveryContext);
                if (context.Candidate.ExternalReferenceKeys.Contains(externalReferenceKey))
                {
                    continue;
                }

                if (!TryResolveWorkspaceWrittenArtifactSourceFullPath(
                        context.WorkspaceRoot,
                        context.WorkspaceScope,
                        matchingWrite.Path,
                        projectedRelativePath,
                        out var fullPath,
                        out var sourceRelativePath,
                        out var pathResolutionFailure))
                {
                    context.Logger.LogDebug(
                        "Skipping workspace-written artifact projection for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle} because write path '{WrittenPath}' could not be read as projected path '{ProjectedPath}'. Reason: {Reason}",
                        context.Candidate.Run.Id,
                        context.Candidate.StepRun.Id,
                        expectedArtifact.Title,
                        matchingWrite.Path,
                        projectedRelativePath,
                        string.IsNullOrWhiteSpace(pathResolutionFailure) ? "File does not exist." : pathResolutionFailure);
                    continue;
                }

                byte[] content;
                try
                {
                    content = await File.ReadAllBytesAsync(fullPath, context.CancellationToken);
                }
                catch (Exception exception)
                {
                    context.Logger.LogWarning(
                        exception,
                        "Workspace-written artifact '{ArtifactTitle}' could not be read for process run {RunId}.",
                        expectedArtifact.Title,
                        context.Candidate.Run.Id);
                    continue;
                }

                var syntheticArtifact = new ProcessAutomationExecutionArtifact(
                    Guid.NewGuid(),
                    context.Detail.Run.Id,
                    "generated-output",
                    expectedArtifact.Title,
                    projectedRelativePath,
                    GuessContentTypeFromPath(fullPath),
                    "workspace_write_file",
                    $"Projected from workspace file write '{sourceRelativePath}' for AgentFramework execution run {context.Detail.Run.Id:D}.",
                    DateTimeOffset.UtcNow);
                var projectionPlan = WorkspaceWrittenArtifactProjectionSourceAdapter.Plan(
                    new WorkspaceWrittenArtifactProjectionSource(
                        context.Detail.Run.Id,
                        projectedRelativePath,
                        sourceRelativePath),
                    expectedProjection,
                    context.CompletionStatus,
                    context.RecoveryContext);
                var writeResult = await context.WriteCoordinator.WriteAsync(
                    new ProcessArtifactProjectionWriteRequest(
                        context.Candidate.Run.Id,
                        context.Candidate.StepRun.Id,
                        context.Candidate.Run.ProjectId,
                        projectionPlan,
                        Path.GetFileName(fullPath),
                        syntheticArtifact.ContentType,
                        content,
                        ResolveStorageContentKind(syntheticArtifact.ContentType, fullPath),
                        BuildStorageRelativePath(context.Candidate, syntheticArtifact)),
                    context.CancellationToken);
                if (!ProcessArtifactProjectionCandidateState.TryApplyExpectedWriteOutcome(
                        context.Candidate,
                        expectedArtifact,
                        writeResult,
                        out var errorSummary))
                {
                    context.Logger.LogWarning(
                        "Workspace-written artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}. Errors: {Errors}",
                        context.Candidate.Run.Id,
                        context.Candidate.StepRun.Id,
                        expectedArtifact.Title,
                        errorSummary);
                }
            }
        }
    }

    private sealed class ProcessExistingManagedArtifactProjectionCoordinator
    {
        private readonly ProcessRunAutomationDispatchService dispatchService;

        public ProcessExistingManagedArtifactProjectionCoordinator(ProcessRunAutomationDispatchService dispatchService)
        {
            this.dispatchService = dispatchService;
        }

        public async Task ProjectAsync(ArtifactProjectionCoordinatorContext context)
        {
            if (context.Candidate.ExpectedArtifacts.Count == 0)
            {
                return;
            }

            foreach (var expectedArtifact in context.Candidate.ExpectedArtifacts)
            {
                if (context.Candidate.RecordedArtifactExpectationIds.Contains(expectedArtifact.Id) ||
                    context.Detail.Artifacts.Any(artifact => ResolveArtifactExpectationId(context.Candidate, context.Detail, artifact) == expectedArtifact.Id))
                {
                    continue;
                }

                var projectedRelativePath = ResolveExpectedManagedArtifactRelativePaths(
                        context.Candidate,
                        context.WorkspaceScope,
                        expectedArtifact)
                    .FirstOrDefault(relativePath => ExistingManagedArtifactFileMatches(
                        context.Candidate.ExpectedArtifacts,
                        expectedArtifact,
                        context.WorkspaceRoot,
                        relativePath));
                if (string.IsNullOrWhiteSpace(projectedRelativePath))
                {
                    continue;
                }

                await RecordExistingManagedArtifactAsync(
                    context,
                    expectedArtifact,
                    projectedRelativePath,
                    "existing managed artifact",
                    $"Projected from existing managed workspace artifact '{projectedRelativePath}' for AgentFramework execution run {context.Detail.Run.Id:D}.");
            }
        }

        public async Task<bool> TryRecordForResponseProjectionAsync(
            ArtifactProjectionCoordinatorContext context,
            DispatchArtifactExpectation expectedArtifact,
            string projectedRelativePath,
            string targetFullPath)
        {
            if (!ExistingManagedArtifactFileMatches(
                    context.Candidate.ExpectedArtifacts,
                    expectedArtifact,
                    context.WorkspaceRoot,
                    projectedRelativePath))
            {
                return false;
            }

            return await RecordExistingManagedArtifactAsync(
                context,
                expectedArtifact,
                projectedRelativePath,
                "existing response-target artifact",
                $"Projected from existing managed workspace artifact '{projectedRelativePath}' for AgentFramework execution run {context.Detail.Run.Id:D}.",
                targetFullPath);
        }

        private async Task<bool> RecordExistingManagedArtifactAsync(
            ArtifactProjectionCoordinatorContext context,
            DispatchArtifactExpectation expectedArtifact,
            string projectedRelativePath,
            string logSourceName,
            string artifactSummary,
            string? knownFullPath = null)
        {
            var expectedProjection = ProcessArtifactValidationSnapshotBuilder.ToProjectionExpectation(expectedArtifact);
            var projectionSource = new ExistingManagedArtifactProjectionSource(
                context.Detail.Run.Id,
                projectedRelativePath);
            var externalReferenceKey = ExistingManagedArtifactProjectionSourceAdapter.BuildExternalReferenceKey(
                projectionSource,
                expectedProjection,
                context.RecoveryContext);
            if (context.Candidate.ExternalReferenceKeys.Contains(externalReferenceKey))
            {
                return true;
            }

            string fullPath;
            string pathResolutionFailure;
            if (knownFullPath is not null)
            {
                fullPath = knownFullPath;
                pathResolutionFailure = string.Empty;
            }
            else if (!TryResolveArtifactFullPath(context.WorkspaceRoot, projectedRelativePath, out fullPath, out pathResolutionFailure) ||
                     !File.Exists(fullPath))
            {
                context.Logger.LogDebug(
                    "Skipping existing managed artifact projection for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle} because path '{RelativePath}' is unavailable. Reason: {Reason}",
                    context.Candidate.Run.Id,
                    context.Candidate.StepRun.Id,
                    expectedArtifact.Title,
                    projectedRelativePath,
                    string.IsNullOrWhiteSpace(pathResolutionFailure) ? "File does not exist." : pathResolutionFailure);
                return false;
            }

            byte[] content;
            try
            {
                content = await File.ReadAllBytesAsync(fullPath, context.CancellationToken);
            }
            catch (Exception exception)
            {
                context.Logger.LogWarning(
                    exception,
                    "Existing managed artifact '{ArtifactTitle}' could not be read for process run {RunId}.",
                    expectedArtifact.Title,
                    context.Candidate.Run.Id);
                return false;
            }

            var contentType = GuessContentTypeFromPath(fullPath);
            var syntheticArtifact = new ProcessAutomationExecutionArtifact(
                Guid.NewGuid(),
                context.Detail.Run.Id,
                "generated-output",
                expectedArtifact.Title,
                projectedRelativePath,
                contentType,
                "managed-workspace-file",
                artifactSummary,
                DateTimeOffset.UtcNow);
            var projectionPlan = ExistingManagedArtifactProjectionSourceAdapter.Plan(
                projectionSource,
                expectedProjection,
                context.CompletionStatus,
                context.RecoveryContext);
            var writeResult = await context.WriteCoordinator.WriteAsync(
                new ProcessArtifactProjectionWriteRequest(
                    context.Candidate.Run.Id,
                    context.Candidate.StepRun.Id,
                    context.Candidate.Run.ProjectId,
                    projectionPlan,
                    Path.GetFileName(fullPath),
                    contentType,
                    content,
                    ResolveStorageContentKind(contentType, fullPath),
                    BuildStorageRelativePath(context.Candidate, syntheticArtifact)),
                context.CancellationToken);
            if (ProcessArtifactProjectionCandidateState.TryApplyExpectedWriteOutcome(
                    context.Candidate,
                    expectedArtifact,
                    writeResult,
                    out var errorSummary))
            {
                return true;
            }

            context.Logger.LogWarning(
                "{SourceName} projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}. Errors: {Errors}",
                logSourceName,
                context.Candidate.Run.Id,
                context.Candidate.StepRun.Id,
                expectedArtifact.Title,
                errorSummary);
            return false;
        }
    }

    private sealed class ProcessResponseTextArtifactProjectionCoordinator
    {
        private readonly ProcessRunAutomationDispatchService dispatchService;
        private readonly ProcessExistingManagedArtifactProjectionCoordinator existingManagedCoordinator;

        public ProcessResponseTextArtifactProjectionCoordinator(
            ProcessRunAutomationDispatchService dispatchService,
            ProcessExistingManagedArtifactProjectionCoordinator existingManagedCoordinator)
        {
            this.dispatchService = dispatchService;
            this.existingManagedCoordinator = existingManagedCoordinator;
        }

        public async Task ProjectAsync(ArtifactProjectionCoordinatorContext context)
        {
            if (!ShouldProjectResponseTextArtifacts(context.Detail.Run, context.CompletionStatus) ||
                context.Candidate.ExpectedArtifacts.Count == 0 ||
                string.IsNullOrWhiteSpace(context.ResponseText))
            {
                return;
            }

            var normalizedResponseText = ResolveProjectableResponseArtifactText(context.ResponseText).ReplaceLineEndings(Environment.NewLine);
            if (string.IsNullOrWhiteSpace(normalizedResponseText))
            {
                return;
            }

            foreach (var expectedArtifact in context.Candidate.ExpectedArtifacts)
            {
                if (!IsUsableProjectedResponseArtifactContent(expectedArtifact, normalizedResponseText))
                {
                    context.Logger.LogInformation(
                        "Skipping response-text artifact projection for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle} because the assistant response is not usable artifact content.",
                        context.Candidate.Run.Id,
                        context.Candidate.StepRun.Id,
                        expectedArtifact.Title);
                    continue;
                }

                if (!TryResolveResponseTextArtifactRelativePath(
                        context.Candidate,
                        context.WorkspaceScope,
                        expectedArtifact,
                        out var projectedRelativePath))
                {
                    continue;
                }

                if (context.Candidate.RecordedArtifactExpectationIds.Contains(expectedArtifact.Id) ||
                    context.Detail.Artifacts.Any(artifact => ResolveArtifactExpectationId(context.Candidate, context.Detail, artifact) == expectedArtifact.Id))
                {
                    continue;
                }

                var projectionSource = new ResponseTextArtifactProjectionSource(
                    context.Detail.Run.Id,
                    projectedRelativePath);
                var externalReferenceKey = ResponseTextArtifactProjectionSourceAdapter.BuildExternalReferenceKey(
                    projectionSource,
                    context.RecoveryContext);
                if (context.Candidate.ExternalReferenceKeys.Contains(externalReferenceKey))
                {
                    continue;
                }

                var targetFullPath = Path.GetFullPath(Path.Combine(
                    context.WorkspaceRoot,
                    projectedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
                if (!IsWithinWorkspace(context.WorkspaceRoot, targetFullPath))
                {
                    context.Logger.LogWarning(
                        "Skipping response-text artifact projection for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle} because target path '{ExpectedPath}' resolves outside the workspace root.",
                        context.Candidate.Run.Id,
                        context.Candidate.StepRun.Id,
                        expectedArtifact.Title,
                        projectedRelativePath);
                    continue;
                }

                try
                {
                    if (File.Exists(targetFullPath) &&
                        await existingManagedCoordinator.TryRecordForResponseProjectionAsync(
                            context,
                            expectedArtifact,
                            projectedRelativePath,
                            targetFullPath))
                    {
                        continue;
                    }

                    var targetDirectory = Path.GetDirectoryName(targetFullPath);
                    if (!string.IsNullOrWhiteSpace(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                    }

                    var persistedResponseText = normalizedResponseText.EndsWith(Environment.NewLine, StringComparison.Ordinal)
                        ? normalizedResponseText
                        : normalizedResponseText + Environment.NewLine;
                    await File.WriteAllTextAsync(targetFullPath, persistedResponseText, Encoding.UTF8, context.CancellationToken);

                    var content = Encoding.UTF8.GetBytes(persistedResponseText);
                    var syntheticArtifact = new ProcessAutomationExecutionArtifact(
                        Guid.NewGuid(),
                        context.Detail.Run.Id,
                        "generated-output",
                        expectedArtifact.Title,
                        projectedRelativePath,
                        GuessContentTypeFromPath(targetFullPath),
                        "assistant-response",
                        "Projected the final assistant response into the required managed text artifact path.",
                        DateTimeOffset.UtcNow);
                    var projectionPlan = ResponseTextArtifactProjectionSourceAdapter.Plan(
                        projectionSource,
                        ProcessArtifactValidationSnapshotBuilder.ToProjectionExpectation(expectedArtifact),
                        context.CompletionStatus,
                        context.RecoveryContext);

                    var writeResult = await context.WriteCoordinator.WriteAsync(
                        new ProcessArtifactProjectionWriteRequest(
                            context.Candidate.Run.Id,
                            context.Candidate.StepRun.Id,
                            context.Candidate.Run.ProjectId,
                            projectionPlan,
                            Path.GetFileName(targetFullPath),
                            syntheticArtifact.ContentType,
                            content,
                            ResolveStorageContentKind(syntheticArtifact.ContentType, targetFullPath),
                            BuildStorageRelativePath(context.Candidate, syntheticArtifact)),
                        context.CancellationToken);

                    if (!ProcessArtifactProjectionCandidateState.TryApplyExpectedWriteOutcome(
                            context.Candidate,
                            expectedArtifact,
                            writeResult,
                            out var errorSummary))
                    {
                        context.Logger.LogWarning(
                            "Response-text artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}. Errors: {Errors}",
                            context.Candidate.Run.Id,
                            context.Candidate.StepRun.Id,
                            expectedArtifact.Title,
                            errorSummary);
                    }
                }
                catch (Exception exception)
                {
                    context.Logger.LogWarning(
                        exception,
                        "Response-text artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}.",
                        context.Candidate.Run.Id,
                        context.Candidate.StepRun.Id,
                        expectedArtifact.Title);
                }
            }
        }
    }

    private sealed class ProcessProviderNativeBrowserArtifactProjectionCoordinator
    {
        private readonly ProcessRunAutomationDispatchService dispatchService;

        public ProcessProviderNativeBrowserArtifactProjectionCoordinator(ProcessRunAutomationDispatchService dispatchService)
        {
            this.dispatchService = dispatchService;
        }

        public async Task ProjectAsync(ArtifactProjectionCoordinatorContext context)
        {
            var browserOutputsByToolName = ResolveSuccessfulBrowserToolOutputFiles(context.Detail);
            if (browserOutputsByToolName.Count == 0)
            {
                return;
            }

            var browserWorkingDirectory = ResolveProviderNativeBrowserWorkingDirectory(context.Detail) ?? context.WorkspaceRoot;
            if (string.IsNullOrWhiteSpace(browserWorkingDirectory))
            {
                return;
            }

            await ProjectExpectedOutputsAsync(context, browserOutputsByToolName, browserWorkingDirectory);
            await ProjectDiscoveredOutputsAsync(context, browserOutputsByToolName, browserWorkingDirectory);
        }

        private async Task ProjectExpectedOutputsAsync(
            ArtifactProjectionCoordinatorContext context,
            IReadOnlyDictionary<string, IReadOnlyList<string>> browserOutputsByToolName,
            string browserWorkingDirectory)
        {
            foreach (var expectedArtifact in context.Candidate.ExpectedArtifacts)
            {
                if (!TryExtractExpectedArtifactRelativePath(expectedArtifact.ValidationRequirementSummary, out var expectedRelativePath))
                {
                    continue;
                }

                if (context.Candidate.RecordedArtifactExpectationIds.Contains(expectedArtifact.Id) ||
                    context.Detail.Artifacts.Any(artifact => ResolveArtifactExpectationId(context.Candidate, context.Detail, artifact) == expectedArtifact.Id))
                {
                    continue;
                }

                var requiredToolName = ResolveProviderNativeBrowserToolName(expectedRelativePath);
                if (string.IsNullOrWhiteSpace(requiredToolName) ||
                    !browserOutputsByToolName.TryGetValue(requiredToolName, out var outputFileNames))
                {
                    continue;
                }

                var matchedOutputFileName = outputFileNames.FirstOrDefault(outputFileName =>
                    MatchesExpectedBrowserOutputFile(expectedRelativePath, outputFileName));
                if (string.IsNullOrWhiteSpace(matchedOutputFileName))
                {
                    continue;
                }

                var sourceFullPath = Path.GetFullPath(Path.Combine(
                    browserWorkingDirectory,
                    matchedOutputFileName.Replace('/', Path.DirectorySeparatorChar)));
                if (!IsWithinWorkspace(context.WorkspaceRoot, sourceFullPath) || !File.Exists(sourceFullPath))
                {
                    context.Logger.LogDebug(
                        "Skipping provider-native browser artifact projection for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle} because source file {SourcePath} is unavailable.",
                        context.Candidate.Run.Id,
                        context.Candidate.StepRun.Id,
                        expectedArtifact.Title,
                        sourceFullPath);
                    continue;
                }

                var projectedRelativePath = ResolveScopedManagedRelativePath(context.WorkspaceScope, expectedRelativePath);
                var targetFullPath = Path.GetFullPath(Path.Combine(
                    context.WorkspaceRoot,
                    projectedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
                if (!IsWithinWorkspace(context.WorkspaceRoot, targetFullPath))
                {
                    context.Logger.LogWarning(
                        "Skipping provider-native browser artifact projection for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle} because target path '{ExpectedPath}' resolves outside the workspace root.",
                        context.Candidate.Run.Id,
                        context.Candidate.StepRun.Id,
                        expectedArtifact.Title,
                        projectedRelativePath);
                    continue;
                }

                try
                {
                    var targetDirectory = Path.GetDirectoryName(targetFullPath);
                    if (!string.IsNullOrWhiteSpace(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                    }

                    if (!string.Equals(sourceFullPath, targetFullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(sourceFullPath, targetFullPath, overwrite: true);
                    }

                    var content = await File.ReadAllBytesAsync(targetFullPath, context.CancellationToken);
                    var syntheticArtifact = new ProcessAutomationExecutionArtifact(
                        Guid.NewGuid(),
                        context.Detail.Run.Id,
                        "generated-output",
                        expectedArtifact.Title,
                        projectedRelativePath,
                        GuessContentTypeFromPath(targetFullPath),
                        requiredToolName,
                        $"Projected provider-native browser output '{matchedOutputFileName}' into the required managed artifact path.",
                        DateTimeOffset.UtcNow);
                    var projectionSource = new ProviderNativeBrowserArtifactProjectionSource(
                        context.Detail.Run.Id,
                        projectedRelativePath,
                        matchedOutputFileName,
                        requiredToolName);
                    var projectionPlan = ProviderNativeBrowserArtifactProjectionSourceAdapter.PlanExpectedOutput(
                        projectionSource,
                        ProcessArtifactValidationSnapshotBuilder.ToProjectionExpectation(expectedArtifact),
                        context.CompletionStatus,
                        context.RecoveryContext);
                    if (context.Candidate.ExternalReferenceKeys.Contains(projectionPlan.ExternalReferenceKey))
                    {
                        continue;
                    }

                    var writeResult = await context.WriteCoordinator.WriteAsync(
                        new ProcessArtifactProjectionWriteRequest(
                            context.Candidate.Run.Id,
                            context.Candidate.StepRun.Id,
                            context.Candidate.Run.ProjectId,
                            projectionPlan,
                            Path.GetFileName(targetFullPath),
                            syntheticArtifact.ContentType,
                            content,
                            ResolveStorageContentKind(syntheticArtifact.ContentType, targetFullPath),
                            BuildStorageRelativePath(context.Candidate, syntheticArtifact)),
                        context.CancellationToken);

                    if (!ProcessArtifactProjectionCandidateState.TryApplyExpectedWriteOutcome(
                            context.Candidate,
                            expectedArtifact,
                            writeResult,
                            out var errorSummary))
                    {
                        context.Logger.LogWarning(
                            "Provider-native browser artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}. Errors: {Errors}",
                            context.Candidate.Run.Id,
                            context.Candidate.StepRun.Id,
                            expectedArtifact.Title,
                            errorSummary);
                    }
                }
                catch (Exception exception)
                {
                    context.Logger.LogWarning(
                        exception,
                        "Provider-native browser artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}.",
                        context.Candidate.Run.Id,
                        context.Candidate.StepRun.Id,
                        expectedArtifact.Title);
                }
            }
        }

        private async Task ProjectDiscoveredOutputsAsync(
            ArtifactProjectionCoordinatorContext context,
            IReadOnlyDictionary<string, IReadOnlyList<string>> browserOutputsByToolName,
            string browserWorkingDirectory)
        {
            foreach (var pair in browserOutputsByToolName)
            {
                foreach (var outputFileName in pair.Value)
                {
                    var normalizedOutputPath = WorkspaceScopeDescriptor.NormalizeRelativePath(outputFileName);
                    if (!IsProviderNativeBrowserArtifactPath(normalizedOutputPath))
                    {
                        continue;
                    }

                    var projectedRelativePath = ResolveProviderNativeBrowserProjectedRelativePath(
                        context.Candidate,
                        context.WorkspaceScope,
                        normalizedOutputPath);
                    var projectionSource = new ProviderNativeBrowserArtifactProjectionSource(
                        context.Detail.Run.Id,
                        projectedRelativePath,
                        normalizedOutputPath,
                        pair.Key);
                    var externalReferenceKey = ProviderNativeBrowserArtifactProjectionSourceAdapter.BuildExternalReferenceKey(
                        projectionSource,
                        context.RecoveryContext);
                    if (context.Candidate.ExternalReferenceKeys.Contains(externalReferenceKey))
                    {
                        continue;
                    }

                    var sourceFullPath = Path.GetFullPath(Path.Combine(
                        browserWorkingDirectory,
                        normalizedOutputPath.Replace('/', Path.DirectorySeparatorChar)));
                    if (!IsWithinWorkspace(context.WorkspaceRoot, sourceFullPath) || !File.Exists(sourceFullPath))
                    {
                        context.Logger.LogDebug(
                            "Skipping provider-native browser output projection for run {RunId}, step {StepRunId} because source file {SourcePath} is unavailable.",
                            context.Candidate.Run.Id,
                            context.Candidate.StepRun.Id,
                            sourceFullPath);
                        continue;
                    }

                    var targetFullPath = Path.GetFullPath(Path.Combine(
                        context.WorkspaceRoot,
                        projectedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
                    if (!IsWithinWorkspace(context.WorkspaceRoot, targetFullPath))
                    {
                        context.Logger.LogWarning(
                            "Skipping provider-native browser output projection for run {RunId}, step {StepRunId} because target path '{ProjectedPath}' resolves outside the workspace root.",
                            context.Candidate.Run.Id,
                            context.Candidate.StepRun.Id,
                            projectedRelativePath);
                        continue;
                    }

                    try
                    {
                        var targetDirectory = Path.GetDirectoryName(targetFullPath);
                        if (!string.IsNullOrWhiteSpace(targetDirectory))
                        {
                            Directory.CreateDirectory(targetDirectory);
                        }

                        if (!string.Equals(sourceFullPath, targetFullPath, StringComparison.OrdinalIgnoreCase))
                        {
                            File.Copy(sourceFullPath, targetFullPath, overwrite: true);
                        }

                        var content = await File.ReadAllBytesAsync(targetFullPath, context.CancellationToken);
                        var contentType = GuessContentTypeFromPath(targetFullPath);
                        var syntheticArtifact = new ProcessAutomationExecutionArtifact(
                            Guid.NewGuid(),
                            context.Detail.Run.Id,
                            "generated-output",
                            Path.GetFileName(projectedRelativePath),
                            projectedRelativePath,
                            contentType,
                            pair.Key,
                            $"Projected provider-native browser output '{normalizedOutputPath}' into the scoped managed artifact path.",
                            DateTimeOffset.UtcNow);
                        var matchedExpectation = ResolveArtifactExpectation(
                            context.Candidate,
                            context.Detail.Run.InputSummary,
                            syntheticArtifact);
                        var recordExpectation = matchedExpectation is not null &&
                                                !context.Candidate.RecordedArtifactExpectationIds.Contains(matchedExpectation.Id)
                            ? matchedExpectation
                            : null;
                        var projectionPlan = ProviderNativeBrowserArtifactProjectionSourceAdapter.PlanDiscoveredOutput(
                            projectionSource,
                            recordExpectation is null ? null : ProcessArtifactValidationSnapshotBuilder.ToProjectionExpectation(recordExpectation),
                            ProcessArtifactKind.Evidence,
                            BuildProviderNativeBrowserArtifactTitle(syntheticArtifact),
                            context.CompletionStatus,
                            context.RecoveryContext);

                        var writeResult = await context.WriteCoordinator.WriteAsync(
                            new ProcessArtifactProjectionWriteRequest(
                                context.Candidate.Run.Id,
                                context.Candidate.StepRun.Id,
                                context.Candidate.Run.ProjectId,
                                projectionPlan,
                                Path.GetFileName(targetFullPath),
                                contentType,
                                content,
                                ResolveStorageContentKind(contentType, targetFullPath),
                                projectedRelativePath),
                            context.CancellationToken);

                        if (!ProcessArtifactProjectionCandidateState.TryApplyWriteOutcome(
                                context.Candidate,
                                writeResult,
                                recordExpectation?.Id,
                                out var errorSummary))
                        {
                            context.Logger.LogWarning(
                                "Provider-native browser output projection failed for run {RunId}, step {StepRunId}, output {OutputPath}. Errors: {Errors}",
                                context.Candidate.Run.Id,
                                context.Candidate.StepRun.Id,
                                normalizedOutputPath,
                                errorSummary);
                        }
                    }
                    catch (Exception exception)
                    {
                        context.Logger.LogWarning(
                            exception,
                            "Provider-native browser output projection failed for run {RunId}, step {StepRunId}, output {OutputPath}.",
                            context.Candidate.Run.Id,
                            context.Candidate.StepRun.Id,
                            normalizedOutputPath);
                    }
                }
            }
        }
    }

    private sealed class ProcessCompletedDecisionArtifactCoordinator
    {
        private readonly ProcessRunAutomationDispatchService dispatchService;

        public ProcessCompletedDecisionArtifactCoordinator(ProcessRunAutomationDispatchService dispatchService)
        {
            this.dispatchService = dispatchService;
        }

        public async Task ProjectAsync(ArtifactProjectionCoordinatorContext context)
        {
            if (context.CompletionStatus != ProcessStepRunStatus.Completed ||
                context.Candidate.ExpectedArtifacts.Count == 0)
            {
                return;
            }

            foreach (var expectedArtifact in context.Candidate.ExpectedArtifacts.Where(ShouldAutoRecordCompletedDecisionArtifact))
            {
                if (context.Candidate.RecordedArtifactExpectationIds.Contains(expectedArtifact.Id) ||
                    HasProjectedArtifactExpectationExternalReference(context.Candidate.ExternalReferenceKeys, expectedArtifact.Id))
                {
                    continue;
                }

                var externalReferenceKey = BuildCompletedDecisionArtifactExternalReferenceKey(
                    context.Candidate.StepRun.Id,
                    expectedArtifact.Id);
                if (context.Candidate.ExternalReferenceKeys.Contains(externalReferenceKey))
                {
                    continue;
                }

                var recordResult = await context.RecordOnlyCoordinator.RecordAsync(
                    new ProcessArtifactProjectionRecordOnlyRequest(
                        context.Candidate.Run.Id,
                        context.Candidate.StepRun.Id,
                        expectedArtifact.Id,
                        expectedArtifact.ArtifactKind,
                        expectedArtifact.Title,
                        ResolveCompletedDecisionArtifactTrustStatus(expectedArtifact.TrustRequirement),
                        expectedArtifact.SensitivityLevel,
                        BuildCompletedDecisionArtifactProvenanceSummary(context.Candidate, context.Detail),
                        string.IsNullOrWhiteSpace(expectedArtifact.AllowedFutureUsageSummary)
                            ? "Reusable for audit, release replay, and governance tuning."
                            : expectedArtifact.AllowedFutureUsageSummary,
                        BuildCompletedDecisionArtifactReviewSummary(
                            context.Candidate,
                            context.Detail,
                            context.ResponseText,
                            expectedArtifact),
                        externalReferenceKey,
                        BuildArtifactProjectionLineage(
                            ProcessArtifactProjectionSourceKind.CompletedDecision,
                            context.Detail.Run.Id,
                            sourceExternalReferenceKey: externalReferenceKey)),
                    context.CancellationToken);
                if (!ProcessArtifactProjectionCandidateState.TryApplyExpectedRecordOnlyOutcome(
                        context.Candidate,
                        expectedArtifact,
                        recordResult,
                        out var errorSummary))
                {
                    context.Logger.LogWarning(
                        "Completed-step decision artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}. Errors: {Errors}",
                        context.Candidate.Run.Id,
                        context.Candidate.StepRun.Id,
                        expectedArtifact.Title,
                        errorSummary);
                }
            }
        }
    }

    private static bool TryApplyExpectedArtifactProjectionWriteOutcome(
        DispatchCandidate candidate,
        DispatchArtifactExpectation expectedArtifact,
        Result<ProcessArtifactProjectionWriteResult> writeResult,
        out string errorSummary)
    {
        return ProcessArtifactProjectionCandidateState.TryApplyExpectedWriteOutcome(
            candidate,
            expectedArtifact,
            writeResult,
            out errorSummary);
    }

    private static bool TryApplyProjectionWriteOutcome(
        DispatchCandidate candidate,
        Result<ProcessArtifactProjectionWriteResult> writeResult,
        Guid? expectedArtifactId,
        out string errorSummary)
    {
        return ProcessArtifactProjectionCandidateState.TryApplyWriteOutcome(
            candidate,
            writeResult,
            expectedArtifactId,
            out errorSummary);
    }
}

