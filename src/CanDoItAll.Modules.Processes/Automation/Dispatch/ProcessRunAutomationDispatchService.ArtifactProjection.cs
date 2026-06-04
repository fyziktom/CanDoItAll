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
    private async Task ProjectExecutionArtifactsAsync(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string responseText,
        ProcessStepRunStatus completionStatus,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken,
        ArtifactProjectionLineage? lineage = null)
    {
        await EnsureStepDispatchClaimHeldAsync(dispatchClaim, cancellationToken);
        var workspaceRoot = Path.GetFullPath(workspacePathResolver.ResolveWorkspaceRoot());
        var workspaceScope = WorkspaceScopeDescriptor.Organization(
            databaseProfileRuntimeAccessor.ResolveCurrentProfile().Profile.Id.ToString("N"));
        var writeCoordinator = new ProcessArtifactProjectionWriteCoordinator(
            storagePlacementService,
            RecordArtifactAsync);
        var recordOnlyCoordinator = new ProcessArtifactProjectionRecordOnlyCoordinator(RecordArtifactAsync);
        foreach (var artifact in detail.Artifacts)
        {
            await EnsureStepDispatchClaimHeldAsync(dispatchClaim, cancellationToken);
            if (IsTransientExecutionArtifact(artifact))
            {
                logger.LogDebug(
                    "Skipping transient execution artifact projection for run {RunId}, step {StepRunId}, artifact {ArtifactId}, path {RelativePath}.",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    artifact.Id,
                    artifact.RelativePath);
                continue;
            }

            var recoveryContext = CreateArtifactRecoveryProjectionContext(lineage);
            var sourceExternalReferenceKey = ProcessArtifactProjectionPlanner.BuildExecutionArtifactExternalReferenceKey(artifact.Id);
            var externalReferenceKey = ProcessArtifactProjectionLineageBuilder.ApplyRecoveryLineage(
                sourceExternalReferenceKey,
                detail.Run.Id,
                recoveryContext);
            if (candidate.ExternalReferenceKeys.Contains(externalReferenceKey))
            {
                continue;
            }

            if (!TryResolveArtifactFullPath(workspaceRoot, artifact.RelativePath, out var fullPath, out var pathResolutionFailure) ||
                !File.Exists(fullPath))
            {
                logger.LogDebug(
                    "Skipping execution artifact projection for run {RunId}, step {StepRunId}, artifact {ArtifactId} because the file path is unavailable. Reason: {Reason}",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    artifact.Id,
                    string.IsNullOrWhiteSpace(pathResolutionFailure) ? "File does not exist." : pathResolutionFailure);
                continue;
            }

            byte[] content;
            try
            {
                content = await File.ReadAllBytesAsync(fullPath, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Execution artifact {ArtifactId} could not be read for process run {RunId}.",
                    artifact.Id,
                    candidate.Run.Id);
                continue;
            }

            var matchedExpectation = ResolveArtifactExpectation(
                candidate,
                detail.Run.InputSummary,
                artifact,
                TryDecodeTextArtifactContent(artifact, fullPath, content));
            var projectionPlan = ProcessArtifactProjectionPlanner.PlanExecutionArtifact(
                detail.Run.Id,
                artifact,
                matchedExpectation is null ? null : ToProjectionExpectation(matchedExpectation),
                ResolveProcessArtifactKind(candidate, artifact),
                completionStatus,
                detail.Run.ResultSummary,
                recoveryContext);

            var writeResult = await writeCoordinator.WriteAsync(
                new ProcessArtifactProjectionWriteRequest(
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    candidate.Run.ProjectId,
                    projectionPlan,
                    Path.GetFileName(fullPath),
                    string.IsNullOrWhiteSpace(artifact.ContentType)
                        ? "application/octet-stream"
                        : artifact.ContentType,
                    content,
                    ResolveStorageContentKind(artifact.ContentType, fullPath),
                    BuildStorageRelativePath(candidate, artifact)),
                cancellationToken);
            if (writeResult.IsSuccess && writeResult.Value is { } writeOutcome)
            {
                candidate.ExternalReferenceKeys.Add(writeOutcome.ExternalReferenceKey);
                if (writeOutcome.ArtifactExpectationId.HasValue)
                {
                    candidate.RecordedArtifactExpectationIds.Add(writeOutcome.ArtifactExpectationId.Value);
                }
            }
            else
            {
                logger.LogWarning(
                    "Process artifact projection failed for run {RunId}, step {StepRunId}, artifact {ArtifactId}. Errors: {Errors}",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    artifact.Id,
                    string.Join(" | ", writeResult.Errors.Select(error => error.Message)));
            }
        }

        await ProjectProcessMockArtifactsAsync(
            candidate,
            detail,
            workspaceRoot,
            workspaceScope,
            writeCoordinator,
            completionStatus,
            cancellationToken,
            lineage);
        await ProjectWorkspaceWrittenArtifactsAsync(
            candidate,
            detail,
            workspaceRoot,
            workspaceScope,
            writeCoordinator,
            completionStatus,
            cancellationToken,
            lineage);
        await ProjectExistingManagedArtifactFilesAsync(
            candidate,
            detail,
            workspaceRoot,
            workspaceScope,
            writeCoordinator,
            completionStatus,
            cancellationToken,
            lineage);
        await ProjectResponseTextArtifactsAsync(
            candidate,
            detail,
            responseText,
            workspaceRoot,
            writeCoordinator,
            completionStatus,
            cancellationToken,
            lineage);
        await ProjectProviderNativeBrowserArtifactsAsync(
            candidate,
            detail,
            workspaceRoot,
            writeCoordinator,
            completionStatus,
            cancellationToken,
            lineage);
        await EnsureDecisionArtifactsForCompletedStepAsync(
            candidate,
            detail,
            responseText,
            recordOnlyCoordinator,
            completionStatus,
            cancellationToken);
    }

    private async Task ProjectProcessMockArtifactsAsync(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope,
        ProcessArtifactProjectionWriteCoordinator writeCoordinator,
        ProcessStepRunStatus completionStatus,
        CancellationToken cancellationToken,
        ArtifactProjectionLineage? lineage = null)
    {
        if (candidate.ExpectedArtifacts.Count == 0)
        {
            return;
        }

        var projections = ResolveProcessMockArtifactProjections(detail.Run.SerializedSessionStateJson);
        if (projections.Count == 0)
        {
            return;
        }

        var projectedExpectationIds = new HashSet<Guid>();
        var recoveryContext = CreateArtifactRecoveryProjectionContext(lineage);
        foreach (var projection in projections)
        {
            var matchedExpectations = candidate.ExpectedArtifacts
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
                    $"Process mock artifact '{projection.RelativePath}' for role '{projection.RoleKey}' matched multiple required artifact expectations for step '{candidate.StepRun.Title}': {string.Join(", ", matchedExpectations.Select(item => item.Title))}.");
            }

            var expectedArtifact = matchedExpectations[0];
            var expectedProjection = ToProjectionExpectation(expectedArtifact);
            var scopedRelativePath = ResolveScopedManagedRelativePath(workspaceScope, projection.RelativePath);
            var projectionSource = new ProcessMockArtifactProjectionSource(
                candidate.StepRun.Id,
                detail.Run.Id,
                projection.RelativePath,
                scopedRelativePath,
                projection.RoleKey);
            var projectionPlan = ProcessMockArtifactProjectionSourceAdapter.Plan(
                projectionSource,
                expectedProjection,
                completionStatus,
                recoveryContext);
            if (candidate.ExternalReferenceKeys.Contains(projectionPlan.ExternalReferenceKey))
            {
                projectedExpectationIds.Add(expectedArtifact.Id);
                continue;
            }

            if (!TryResolveArtifactFullPath(workspaceRoot, scopedRelativePath, out var fullPath, out var pathResolutionFailure) ||
                !File.Exists(fullPath))
            {
                throw new InvalidOperationException(
                    $"Process mock artifact '{projection.RelativePath}' for expected artifact '{expectedArtifact.Title}' was declared by execution run {detail.Run.Id:D}, but scoped path '{scopedRelativePath}' could not be found. {pathResolutionFailure}".Trim());
            }

            byte[] content;
            try
            {
                content = await File.ReadAllBytesAsync(fullPath, cancellationToken);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Process mock artifact '{projection.RelativePath}' for expected artifact '{expectedArtifact.Title}' at scoped path '{scopedRelativePath}' could not be read: {exception.Message}",
                    exception);
            }

            var contentType = GuessContentTypeFromPath(fullPath);
            var writeResult = await writeCoordinator.WriteAsync(
                new ProcessArtifactProjectionWriteRequest(
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    candidate.Run.ProjectId,
                    projectionPlan,
                    Path.GetFileName(fullPath),
                    contentType,
                    content,
                    ResolveStorageContentKind(contentType, fullPath),
                    scopedRelativePath),
                cancellationToken);
            if (writeResult.IsFailure || writeResult.Value is not { } writeOutcome)
            {
                throw new InvalidOperationException(
                    $"Process mock artifact projection failed for expected artifact '{expectedArtifact.Title}': {string.Join(" | ", writeResult.Errors.Select(error => error.Message))}");
            }

            if (writeOutcome.ArtifactExpectationId != expectedArtifact.Id)
            {
                throw new InvalidOperationException(
                    $"Process mock artifact projection for expected artifact '{expectedArtifact.Title}' returned artifact expectation id '{writeOutcome.ArtifactExpectationId?.ToString("D") ?? "null"}' instead of '{expectedArtifact.Id:D}'.");
            }

            candidate.ExternalReferenceKeys.Add(writeOutcome.ExternalReferenceKey);
            candidate.RecordedArtifactExpectationIds.Add(writeOutcome.ArtifactExpectationId.Value);
            projectedExpectationIds.Add(expectedArtifact.Id);
        }
    }

    private async Task ProjectWorkspaceWrittenArtifactsAsync(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope,
        ProcessArtifactProjectionWriteCoordinator writeCoordinator,
        ProcessStepRunStatus completionStatus,
        CancellationToken cancellationToken,
        ArtifactProjectionLineage? lineage = null)
    {
        if (candidate.ExpectedArtifacts.Count == 0)
        {
            return;
        }

        var fileWrites = ResolveSuccessfulSessionFileWrites(detail.Run.SerializedSessionStateJson);
        var receiptFileWrites = ResolveSuccessfulWorkspaceFileMutationReceiptPaths(detail)
            .Select(path => new SessionFileContent(path, string.Empty))
            .ToList();
        if (fileWrites.Count == 0 && receiptFileWrites.Count == 0)
        {
            return;
        }

        var recoveryContext = CreateArtifactRecoveryProjectionContext(lineage);
        foreach (var expectedArtifact in candidate.ExpectedArtifacts)
        {
            if (candidate.RecordedArtifactExpectationIds.Contains(expectedArtifact.Id) ||
                detail.Artifacts.Any(artifact => ResolveArtifactExpectationId(candidate, detail, artifact) == expectedArtifact.Id))
            {
                continue;
            }

            var matchingWrite = TryResolveProjectStructureExpectedArtifactPath(
                    candidate,
                    expectedArtifact,
                    detail.Run.InputSummary,
                    out var governedPath)
                ? fileWrites.LastOrDefault(file => ArtifactPathMatchesGovernedProjectStructurePath(file.Path, governedPath)) ??
                  receiptFileWrites.LastOrDefault(file => ArtifactPathMatchesGovernedProjectStructurePath(file.Path, governedPath))
                : fileWrites.LastOrDefault(file => WorkspaceWrittenFileMatchesExpectedArtifact(
                    candidate.ExpectedArtifacts,
                    expectedArtifact,
                    file.Path,
                    file.Content)) ??
                  receiptFileWrites.LastOrDefault(file => WorkspaceWrittenFileMatchesExpectedArtifact(
                    candidate.ExpectedArtifacts,
                    expectedArtifact,
                    file.Path,
                    file.Content));
            if (matchingWrite is null)
            {
                continue;
            }

            var projectedRelativePath = ResolveWorkspaceWrittenArtifactRelativePath(workspaceScope, matchingWrite.Path);
            if (string.IsNullOrWhiteSpace(projectedRelativePath))
            {
                continue;
            }

            var expectedProjection = ToProjectionExpectation(expectedArtifact);
            var duplicateProbeSource = new WorkspaceWrittenArtifactProjectionSource(
                detail.Run.Id,
                projectedRelativePath,
                projectedRelativePath);
            var externalReferenceKey = WorkspaceWrittenArtifactProjectionSourceAdapter.BuildExternalReferenceKey(
                duplicateProbeSource,
                expectedProjection,
                recoveryContext);
            if (candidate.ExternalReferenceKeys.Contains(externalReferenceKey))
            {
                continue;
            }

            if (!TryResolveWorkspaceWrittenArtifactSourceFullPath(
                    workspaceRoot,
                    workspaceScope,
                    matchingWrite.Path,
                    projectedRelativePath,
                    out var fullPath,
                    out var sourceRelativePath,
                    out var pathResolutionFailure))
            {
                logger.LogDebug(
                    "Skipping workspace-written artifact projection for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle} because write path '{WrittenPath}' could not be read as projected path '{ProjectedPath}'. Reason: {Reason}",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    expectedArtifact.Title,
                    matchingWrite.Path,
                    projectedRelativePath,
                    string.IsNullOrWhiteSpace(pathResolutionFailure) ? "File does not exist." : pathResolutionFailure);
                continue;
            }

            byte[] content;
            try
            {
                content = await File.ReadAllBytesAsync(fullPath, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Workspace-written artifact '{ArtifactTitle}' could not be read for process run {RunId}.",
                    expectedArtifact.Title,
                    candidate.Run.Id);
                continue;
            }

            var syntheticArtifact = new ProcessAutomationExecutionArtifact(
                Guid.NewGuid(),
                detail.Run.Id,
                "generated-output",
                expectedArtifact.Title,
                projectedRelativePath,
                GuessContentTypeFromPath(fullPath),
                "workspace_write_file",
                $"Projected from workspace file write '{sourceRelativePath}' for AgentFramework execution run {detail.Run.Id:D}.",
                DateTimeOffset.UtcNow);
            var projectionPlan = WorkspaceWrittenArtifactProjectionSourceAdapter.Plan(
                new WorkspaceWrittenArtifactProjectionSource(
                    detail.Run.Id,
                    projectedRelativePath,
                    sourceRelativePath),
                expectedProjection,
                completionStatus,
                recoveryContext);
            var writeResult = await writeCoordinator.WriteAsync(
                new ProcessArtifactProjectionWriteRequest(
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    candidate.Run.ProjectId,
                    projectionPlan,
                    Path.GetFileName(fullPath),
                    syntheticArtifact.ContentType,
                    content,
                    ResolveStorageContentKind(syntheticArtifact.ContentType, fullPath),
                    BuildStorageRelativePath(candidate, syntheticArtifact)),
                cancellationToken);
            if (!TryApplyExpectedArtifactProjectionWriteOutcome(candidate, expectedArtifact, writeResult, out var errorSummary))
            {
                logger.LogWarning(
                    "Workspace-written artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}. Errors: {Errors}",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    expectedArtifact.Title,
                    errorSummary);
            }
        }
    }

    private static IReadOnlyList<string> ResolveSuccessfulWorkspaceFileMutationReceiptPaths(ProcessAutomationExecutionRunDetail detail)
    {
        return ProcessAutomationReceiptObservationHelper.ResolveSuccessfulReceipts(detail)
            .Where(IsSuccessfulWorkspaceFileMutationReceipt)
            .SelectMany(ResolveManagedWorkspacePathsFromReceipt)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task ProjectExistingManagedArtifactFilesAsync(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope,
        ProcessArtifactProjectionWriteCoordinator writeCoordinator,
        ProcessStepRunStatus completionStatus,
        CancellationToken cancellationToken,
        ArtifactProjectionLineage? lineage = null)
    {
        if (candidate.ExpectedArtifacts.Count == 0)
        {
            return;
        }

        var recoveryContext = CreateArtifactRecoveryProjectionContext(lineage);
        foreach (var expectedArtifact in candidate.ExpectedArtifacts)
        {
            if (candidate.RecordedArtifactExpectationIds.Contains(expectedArtifact.Id) ||
                detail.Artifacts.Any(artifact => ResolveArtifactExpectationId(candidate, detail, artifact) == expectedArtifact.Id))
            {
                continue;
            }

            var projectedRelativePath = ResolveExpectedManagedArtifactRelativePaths(
                    candidate,
                    workspaceScope,
                    expectedArtifact)
                .FirstOrDefault(relativePath => ExistingManagedArtifactFileMatches(
                    candidate.ExpectedArtifacts,
                    expectedArtifact,
                    workspaceRoot,
                    relativePath));
            if (string.IsNullOrWhiteSpace(projectedRelativePath))
            {
                continue;
            }

            var expectedProjection = ToProjectionExpectation(expectedArtifact);
            var projectionSource = new ExistingManagedArtifactProjectionSource(
                detail.Run.Id,
                projectedRelativePath);
            var externalReferenceKey = ExistingManagedArtifactProjectionSourceAdapter.BuildExternalReferenceKey(
                projectionSource,
                expectedProjection,
                recoveryContext);
            if (candidate.ExternalReferenceKeys.Contains(externalReferenceKey))
            {
                continue;
            }

            if (!TryResolveArtifactFullPath(workspaceRoot, projectedRelativePath, out var fullPath, out var pathResolutionFailure) ||
                !File.Exists(fullPath))
            {
                logger.LogDebug(
                    "Skipping existing managed artifact projection for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle} because path '{RelativePath}' is unavailable. Reason: {Reason}",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    expectedArtifact.Title,
                    projectedRelativePath,
                    string.IsNullOrWhiteSpace(pathResolutionFailure) ? "File does not exist." : pathResolutionFailure);
                continue;
            }

            byte[] content;
            try
            {
                content = await File.ReadAllBytesAsync(fullPath, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Existing managed artifact '{ArtifactTitle}' could not be read for process run {RunId}.",
                    expectedArtifact.Title,
                    candidate.Run.Id);
                continue;
            }

            var contentType = GuessContentTypeFromPath(fullPath);
            var syntheticArtifact = new ProcessAutomationExecutionArtifact(
                Guid.NewGuid(),
                detail.Run.Id,
                "generated-output",
                expectedArtifact.Title,
                projectedRelativePath,
                contentType,
                "managed-workspace-file",
                $"Projected from existing managed workspace artifact '{projectedRelativePath}' for AgentFramework execution run {detail.Run.Id:D}.",
                DateTimeOffset.UtcNow);
            var projectionPlan = ExistingManagedArtifactProjectionSourceAdapter.Plan(
                projectionSource,
                expectedProjection,
                completionStatus,
                recoveryContext);
            var writeResult = await writeCoordinator.WriteAsync(
                new ProcessArtifactProjectionWriteRequest(
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    candidate.Run.ProjectId,
                    projectionPlan,
                    Path.GetFileName(fullPath),
                    contentType,
                    content,
                    ResolveStorageContentKind(contentType, fullPath),
                    BuildStorageRelativePath(candidate, syntheticArtifact)),
                cancellationToken);
            if (!TryApplyExpectedArtifactProjectionWriteOutcome(candidate, expectedArtifact, writeResult, out var errorSummary))
            {
                logger.LogWarning(
                    "Existing managed artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}. Errors: {Errors}",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    expectedArtifact.Title,
                    errorSummary);
            }
        }
    }

    private static IReadOnlyList<string> ResolveExpectedManagedArtifactRelativePaths(
        DispatchCandidate candidate,
        WorkspaceScopeDescriptor workspaceScope,
        DispatchArtifactExpectation expectedArtifact)
    {
        var paths = new List<string>();
        if (TryExtractExpectedArtifactRelativePath(expectedArtifact.ValidationRequirementSummary, out var declaredRelativePath))
        {
            AddManagedArtifactPath(paths, workspaceScope, declaredRelativePath);
        }

        if (CanProjectResponseTextArtifactWithoutDeclaredPath(expectedArtifact))
        {
            AddManagedArtifactPath(
                paths,
                workspaceScope,
                BuildFallbackResponseTextArtifactRelativePath(candidate, expectedArtifact));
        }

        return paths;
    }

    private static void AddManagedArtifactPath(
        ICollection<string> paths,
        WorkspaceScopeDescriptor workspaceScope,
        string relativePath)
    {
        var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath);
        if (string.IsNullOrWhiteSpace(normalizedPath) ||
            IsExternalTargetAliasPath(normalizedPath))
        {
            return;
        }

        var scopedPath = ResolveScopedManagedRelativePath(workspaceScope, normalizedPath);
        if (string.IsNullOrWhiteSpace(scopedPath) ||
            paths.Contains(scopedPath, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        paths.Add(scopedPath);
    }

    private static bool ExistingManagedArtifactFileMatches(
        IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts,
        DispatchArtifactExpectation expectedArtifact,
        string workspaceRoot,
        string relativePath)
    {
        if (!TryResolveArtifactFullPath(workspaceRoot, relativePath, out var fullPath, out _) ||
            !File.Exists(fullPath))
        {
            return false;
        }

        string? textContent = null;
        try
        {
            var fileInfo = new FileInfo(fullPath);
            if (fileInfo.Length is > 0 and <= 512 * 1024)
            {
                var bytes = File.ReadAllBytes(fullPath);
                textContent = TryDecodeTextArtifactContent(
                    new ProcessAutomationExecutionArtifact(
                        Guid.Empty,
                        Guid.Empty,
                        "generated-output",
                        expectedArtifact.Title,
                        relativePath,
                        GuessContentTypeFromPath(fullPath),
                        "managed-workspace-file",
                        "Existing managed workspace artifact.",
                        DateTimeOffset.MinValue),
                    fullPath,
                    bytes);
            }
        }
        catch (Exception)
        {
            textContent = null;
        }

        var syntheticArtifact = new ProcessAutomationExecutionArtifact(
            Guid.Empty,
            Guid.Empty,
            "generated-output",
            expectedArtifact.Title,
            relativePath,
            GuessContentTypeFromPath(fullPath),
            "managed-workspace-file",
            "Existing managed workspace artifact.",
            DateTimeOffset.MinValue);
        var matchedExpectationId = MatchExpectedArtifactId(expectedArtifacts, syntheticArtifact, textContent);
        return matchedExpectationId == expectedArtifact.Id;
    }

    private async Task EnsureDecisionArtifactsForCompletedStepAsync(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string responseText,
        ProcessArtifactProjectionRecordOnlyCoordinator recordOnlyCoordinator,
        ProcessStepRunStatus completionStatus,
        CancellationToken cancellationToken)
    {
        if (completionStatus != ProcessStepRunStatus.Completed || candidate.ExpectedArtifacts.Count == 0)
        {
            return;
        }

        foreach (var expectedArtifact in candidate.ExpectedArtifacts.Where(ShouldAutoRecordCompletedDecisionArtifact))
        {
            if (candidate.RecordedArtifactExpectationIds.Contains(expectedArtifact.Id) ||
                HasProjectedArtifactExpectationExternalReference(candidate.ExternalReferenceKeys, expectedArtifact.Id))
            {
                continue;
            }

            var externalReferenceKey = BuildCompletedDecisionArtifactExternalReferenceKey(
                candidate.StepRun.Id,
                expectedArtifact.Id);
            if (candidate.ExternalReferenceKeys.Contains(externalReferenceKey))
            {
                continue;
            }

            var recordResult = await recordOnlyCoordinator.RecordAsync(
                new ProcessArtifactProjectionRecordOnlyRequest(
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    expectedArtifact.Id,
                    expectedArtifact.ArtifactKind,
                    expectedArtifact.Title,
                    ResolveCompletedDecisionArtifactTrustStatus(expectedArtifact.TrustRequirement),
                    expectedArtifact.SensitivityLevel,
                    BuildCompletedDecisionArtifactProvenanceSummary(candidate, detail),
                    string.IsNullOrWhiteSpace(expectedArtifact.AllowedFutureUsageSummary)
                        ? "Reusable for audit, release replay, and governance tuning."
                        : expectedArtifact.AllowedFutureUsageSummary,
                    BuildCompletedDecisionArtifactReviewSummary(candidate, detail, responseText, expectedArtifact),
                    externalReferenceKey,
                    BuildArtifactProjectionLineage(
                        ProcessArtifactProjectionSourceKind.CompletedDecision,
                        detail.Run.Id,
                        sourceExternalReferenceKey: externalReferenceKey)),
                cancellationToken);
            if (recordResult.IsSuccess &&
                recordResult.Value is { ArtifactExpectationId: { } artifactExpectationId } recordOutcome &&
                artifactExpectationId == expectedArtifact.Id)
            {
                candidate.ExternalReferenceKeys.Add(recordOutcome.ExternalReferenceKey);
                candidate.RecordedArtifactExpectationIds.Add(artifactExpectationId);
            }
            else
            {
                var errorSummary = recordResult.IsFailure
                    ? string.Join(" | ", recordResult.Errors.Select(error => error.Message))
                    : recordResult.Value is null
                        ? "Record-only coordinator completed without an artifact record outcome."
                        : recordResult.Value.ArtifactExpectationId.HasValue
                            ? $"Record-only coordinator returned artifact expectation id '{recordResult.Value.ArtifactExpectationId.Value:D}' instead of '{expectedArtifact.Id:D}'."
                            : "Record-only coordinator completed without an artifact expectation id.";

                logger.LogWarning(
                    "Completed-step decision artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}. Errors: {Errors}",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    expectedArtifact.Title,
                    errorSummary);
            }
        }
    }

    private static bool HasProjectedArtifactExpectationExternalReference(
        IEnumerable<string> externalReferenceKeys,
        Guid artifactExpectationId)
    {
        var marker = $"|{artifactExpectationId:D}|";
        var suffix = $"|{artifactExpectationId:D}";
        return externalReferenceKeys.Any(key =>
            !string.IsNullOrWhiteSpace(key) &&
            (key.Contains(marker, StringComparison.OrdinalIgnoreCase) ||
             key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool TryApplyExpectedArtifactProjectionWriteOutcome(
        DispatchCandidate candidate,
        DispatchArtifactExpectation expectedArtifact,
        Result<ProcessArtifactProjectionWriteResult> writeResult,
        out string errorSummary)
        => TryApplyProjectionWriteOutcome(
            candidate,
            writeResult,
            expectedArtifact.Id,
            out errorSummary);

    private static bool TryApplyProjectionWriteOutcome(
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

        if (expectedArtifactId.HasValue &&
            !writeOutcome.ArtifactExpectationId.HasValue)
        {
            errorSummary = "Coordinator completed without an artifact expectation id.";
            return false;
        }

        if (expectedArtifactId is { } expectedId &&
            writeOutcome.ArtifactExpectationId is { } actualId &&
            actualId != expectedId)
        {
            errorSummary = $"Coordinator returned artifact expectation id '{actualId:D}' instead of '{expectedId:D}'.";
            return false;
        }

        if (expectedArtifactId is null &&
            writeOutcome.ArtifactExpectationId is { } unexpectedId)
        {
            errorSummary = $"Coordinator returned unexpected artifact expectation id '{unexpectedId:D}'.";
            return false;
        }

        candidate.ExternalReferenceKeys.Add(writeOutcome.ExternalReferenceKey);
        if (writeOutcome.ArtifactExpectationId is { } artifactExpectationId)
        {
            candidate.RecordedArtifactExpectationIds.Add(artifactExpectationId);
        }

        errorSummary = string.Empty;
        return true;
    }

    private async Task ProjectResponseTextArtifactsAsync(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string responseText,
        string workspaceRoot,
        ProcessArtifactProjectionWriteCoordinator writeCoordinator,
        ProcessStepRunStatus completionStatus,
        CancellationToken cancellationToken,
        ArtifactProjectionLineage? lineage = null)
    {
        if (!ShouldProjectResponseTextArtifacts(detail.Run, completionStatus) ||
            candidate.ExpectedArtifacts.Count == 0 ||
            string.IsNullOrWhiteSpace(responseText))
        {
            return;
        }

        var normalizedResponseText = ResolveProjectableResponseArtifactText(responseText).ReplaceLineEndings(Environment.NewLine);
        if (string.IsNullOrWhiteSpace(normalizedResponseText))
        {
            return;
        }

        var workspaceScope = WorkspaceScopeDescriptor.Organization(
            databaseProfileRuntimeAccessor.ResolveCurrentProfile().Profile.Id.ToString("N"));
        var recoveryContext = CreateArtifactRecoveryProjectionContext(lineage);

        foreach (var expectedArtifact in candidate.ExpectedArtifacts)
        {
            if (!IsUsableProjectedResponseArtifactContent(expectedArtifact, normalizedResponseText))
            {
                logger.LogInformation(
                    "Skipping response-text artifact projection for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle} because the assistant response is not usable artifact content.",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    expectedArtifact.Title);
                continue;
            }

            if (!TryResolveResponseTextArtifactRelativePath(
                    candidate,
                    workspaceScope,
                    expectedArtifact,
                    out var projectedRelativePath))
            {
                continue;
            }

            if (candidate.RecordedArtifactExpectationIds.Contains(expectedArtifact.Id) ||
                detail.Artifacts.Any(artifact => ResolveArtifactExpectationId(candidate, detail, artifact) == expectedArtifact.Id))
            {
                continue;
            }

            var projectionSource = new ResponseTextArtifactProjectionSource(
                detail.Run.Id,
                projectedRelativePath);
            var externalReferenceKey = ResponseTextArtifactProjectionSourceAdapter.BuildExternalReferenceKey(
                projectionSource,
                recoveryContext);
            if (candidate.ExternalReferenceKeys.Contains(externalReferenceKey))
            {
                continue;
            }

            var targetFullPath = Path.GetFullPath(Path.Combine(
                workspaceRoot,
                projectedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsWithinWorkspace(workspaceRoot, targetFullPath))
            {
                logger.LogWarning(
                    "Skipping response-text artifact projection for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle} because target path '{ExpectedPath}' resolves outside the workspace root.",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    expectedArtifact.Title,
                    projectedRelativePath);
                continue;
            }

            try
            {
                if (File.Exists(targetFullPath) &&
                    await TryRecordExistingManagedArtifactForResponseProjectionAsync(
                        candidate,
                        detail,
                        expectedArtifact,
                        workspaceRoot,
                        projectedRelativePath,
                        targetFullPath,
                        writeCoordinator,
                        completionStatus,
                        cancellationToken,
                        lineage))
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
                await File.WriteAllTextAsync(targetFullPath, persistedResponseText, Encoding.UTF8, cancellationToken);

                var content = Encoding.UTF8.GetBytes(persistedResponseText);
                var syntheticArtifact = new ProcessAutomationExecutionArtifact(
                    Guid.NewGuid(),
                    detail.Run.Id,
                    "generated-output",
                    expectedArtifact.Title,
                    projectedRelativePath,
                    GuessContentTypeFromPath(targetFullPath),
                    "assistant-response",
                    "Projected the final assistant response into the required managed text artifact path.",
                    DateTimeOffset.UtcNow);
                var projectionPlan = ResponseTextArtifactProjectionSourceAdapter.Plan(
                    projectionSource,
                    ToProjectionExpectation(expectedArtifact),
                    completionStatus,
                    recoveryContext);

                var writeResult = await writeCoordinator.WriteAsync(
                    new ProcessArtifactProjectionWriteRequest(
                        candidate.Run.Id,
                        candidate.StepRun.Id,
                        candidate.Run.ProjectId,
                        projectionPlan,
                        Path.GetFileName(targetFullPath),
                        syntheticArtifact.ContentType,
                        content,
                        ResolveStorageContentKind(syntheticArtifact.ContentType, targetFullPath),
                        BuildStorageRelativePath(candidate, syntheticArtifact)),
                    cancellationToken);

                if (!TryApplyExpectedArtifactProjectionWriteOutcome(candidate, expectedArtifact, writeResult, out var errorSummary))
                {
                    logger.LogWarning(
                        "Response-text artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}. Errors: {Errors}",
                        candidate.Run.Id,
                        candidate.StepRun.Id,
                        expectedArtifact.Title,
                        errorSummary);
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Response-text artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}.",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    expectedArtifact.Title);
            }
        }
    }

    private async Task<bool> TryRecordExistingManagedArtifactForResponseProjectionAsync(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        DispatchArtifactExpectation expectedArtifact,
        string workspaceRoot,
        string projectedRelativePath,
        string targetFullPath,
        ProcessArtifactProjectionWriteCoordinator writeCoordinator,
        ProcessStepRunStatus completionStatus,
        CancellationToken cancellationToken,
        ArtifactProjectionLineage? lineage = null)
    {
        if (!ExistingManagedArtifactFileMatches(
                candidate.ExpectedArtifacts,
                expectedArtifact,
                workspaceRoot,
                projectedRelativePath))
        {
            return false;
        }

        var recoveryContext = CreateArtifactRecoveryProjectionContext(lineage);
        var expectedProjection = ToProjectionExpectation(expectedArtifact);
        var projectionSource = new ExistingManagedArtifactProjectionSource(
            detail.Run.Id,
            projectedRelativePath);
        var externalReferenceKey = ExistingManagedArtifactProjectionSourceAdapter.BuildExternalReferenceKey(
            projectionSource,
            expectedProjection,
            recoveryContext);
        if (candidate.ExternalReferenceKeys.Contains(externalReferenceKey))
        {
            return true;
        }

        var content = await File.ReadAllBytesAsync(targetFullPath, cancellationToken);
        var contentType = GuessContentTypeFromPath(targetFullPath);
        var syntheticArtifact = new ProcessAutomationExecutionArtifact(
            Guid.NewGuid(),
            detail.Run.Id,
            "generated-output",
            expectedArtifact.Title,
            projectedRelativePath,
            contentType,
            "managed-workspace-file",
            $"Projected from existing managed workspace artifact '{projectedRelativePath}' for AgentFramework execution run {detail.Run.Id:D}.",
            DateTimeOffset.UtcNow);
        var projectionPlan = ExistingManagedArtifactProjectionSourceAdapter.Plan(
            projectionSource,
            expectedProjection,
            completionStatus,
            recoveryContext);
        var writeResult = await writeCoordinator.WriteAsync(
            new ProcessArtifactProjectionWriteRequest(
                candidate.Run.Id,
                candidate.StepRun.Id,
                candidate.Run.ProjectId,
                projectionPlan,
                Path.GetFileName(targetFullPath),
                contentType,
                content,
                ResolveStorageContentKind(contentType, targetFullPath),
                BuildStorageRelativePath(candidate, syntheticArtifact)),
            cancellationToken);
        if (!TryApplyExpectedArtifactProjectionWriteOutcome(candidate, expectedArtifact, writeResult, out var errorSummary))
        {
            logger.LogWarning(
                "Existing response-target artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}. Errors: {Errors}",
                candidate.Run.Id,
                candidate.StepRun.Id,
                expectedArtifact.Title,
                errorSummary);
            return false;
        }

        return true;
    }

    private async Task ProjectProviderNativeBrowserArtifactsAsync(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string workspaceRoot,
        ProcessArtifactProjectionWriteCoordinator writeCoordinator,
        ProcessStepRunStatus completionStatus,
        CancellationToken cancellationToken,
        ArtifactProjectionLineage? lineage = null)
    {
        var browserOutputsByToolName = ResolveSuccessfulBrowserToolOutputFiles(detail);
        if (browserOutputsByToolName.Count == 0)
        {
            return;
        }

        var browserWorkingDirectory = ResolveProviderNativeBrowserWorkingDirectory(detail) ?? workspaceRoot;
        if (string.IsNullOrWhiteSpace(browserWorkingDirectory))
        {
            return;
        }

        var workspaceScope = WorkspaceScopeDescriptor.Organization(
            databaseProfileRuntimeAccessor.ResolveCurrentProfile().Profile.Id.ToString("N"));
        var recoveryContext = CreateArtifactRecoveryProjectionContext(lineage);

        foreach (var expectedArtifact in candidate.ExpectedArtifacts)
        {
            if (!TryExtractExpectedArtifactRelativePath(expectedArtifact.ValidationRequirementSummary, out var expectedRelativePath))
            {
                continue;
            }

            if (candidate.RecordedArtifactExpectationIds.Contains(expectedArtifact.Id) ||
                detail.Artifacts.Any(artifact => ResolveArtifactExpectationId(candidate, detail, artifact) == expectedArtifact.Id))
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
            if (!IsWithinWorkspace(workspaceRoot, sourceFullPath) || !File.Exists(sourceFullPath))
            {
                logger.LogDebug(
                    "Skipping provider-native browser artifact projection for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle} because source file {SourcePath} is unavailable.",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    expectedArtifact.Title,
                    sourceFullPath);
                continue;
            }

            var projectedRelativePath = ResolveScopedManagedRelativePath(workspaceScope, expectedRelativePath);
            var targetFullPath = Path.GetFullPath(Path.Combine(
                workspaceRoot,
                projectedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsWithinWorkspace(workspaceRoot, targetFullPath))
            {
                logger.LogWarning(
                    "Skipping provider-native browser artifact projection for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle} because target path '{ExpectedPath}' resolves outside the workspace root.",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
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

                var content = await File.ReadAllBytesAsync(targetFullPath, cancellationToken);
                var syntheticArtifact = new ProcessAutomationExecutionArtifact(
                    Guid.NewGuid(),
                    detail.Run.Id,
                    "generated-output",
                    expectedArtifact.Title,
                    projectedRelativePath,
                    GuessContentTypeFromPath(targetFullPath),
                    requiredToolName,
                    $"Projected provider-native browser output '{matchedOutputFileName}' into the required managed artifact path.",
                    DateTimeOffset.UtcNow);
                var projectionSource = new ProviderNativeBrowserArtifactProjectionSource(
                    detail.Run.Id,
                    projectedRelativePath,
                    matchedOutputFileName,
                    requiredToolName);
                var projectionPlan = ProviderNativeBrowserArtifactProjectionSourceAdapter.PlanExpectedOutput(
                    projectionSource,
                    ToProjectionExpectation(expectedArtifact),
                    completionStatus,
                    recoveryContext);
                if (candidate.ExternalReferenceKeys.Contains(projectionPlan.ExternalReferenceKey))
                {
                    continue;
                }

                var writeResult = await writeCoordinator.WriteAsync(
                    new ProcessArtifactProjectionWriteRequest(
                        candidate.Run.Id,
                        candidate.StepRun.Id,
                        candidate.Run.ProjectId,
                        projectionPlan,
                        Path.GetFileName(targetFullPath),
                        syntheticArtifact.ContentType,
                        content,
                        ResolveStorageContentKind(syntheticArtifact.ContentType, targetFullPath),
                        BuildStorageRelativePath(candidate, syntheticArtifact)),
                    cancellationToken);

                if (!TryApplyExpectedArtifactProjectionWriteOutcome(candidate, expectedArtifact, writeResult, out var errorSummary))
                {
                    logger.LogWarning(
                        "Provider-native browser artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}. Errors: {Errors}",
                        candidate.Run.Id,
                        candidate.StepRun.Id,
                        expectedArtifact.Title,
                        errorSummary);
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Provider-native browser artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}.",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    expectedArtifact.Title);
            }
        }

        await ProjectProviderNativeBrowserOutputArtifactsAsync(
            candidate,
            detail,
            workspaceRoot,
            workspaceScope,
            browserOutputsByToolName,
            browserWorkingDirectory,
            writeCoordinator,
            completionStatus,
            cancellationToken,
            lineage);
    }

    private async Task ProjectProviderNativeBrowserOutputArtifactsAsync(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope,
        IReadOnlyDictionary<string, IReadOnlyList<string>> browserOutputsByToolName,
        string browserWorkingDirectory,
        ProcessArtifactProjectionWriteCoordinator writeCoordinator,
        ProcessStepRunStatus completionStatus,
        CancellationToken cancellationToken,
        ArtifactProjectionLineage? lineage = null)
    {
        var recoveryContext = CreateArtifactRecoveryProjectionContext(lineage);
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
                    candidate,
                    workspaceScope,
                    normalizedOutputPath);
                var projectionSource = new ProviderNativeBrowserArtifactProjectionSource(
                    detail.Run.Id,
                    projectedRelativePath,
                    normalizedOutputPath,
                    pair.Key);
                var externalReferenceKey = ProviderNativeBrowserArtifactProjectionSourceAdapter.BuildExternalReferenceKey(
                    projectionSource,
                    recoveryContext);
                if (candidate.ExternalReferenceKeys.Contains(externalReferenceKey))
                {
                    continue;
                }

                var sourceFullPath = Path.GetFullPath(Path.Combine(
                    browserWorkingDirectory,
                    normalizedOutputPath.Replace('/', Path.DirectorySeparatorChar)));
                if (!IsWithinWorkspace(workspaceRoot, sourceFullPath) || !File.Exists(sourceFullPath))
                {
                    logger.LogDebug(
                        "Skipping provider-native browser output projection for run {RunId}, step {StepRunId} because source file {SourcePath} is unavailable.",
                        candidate.Run.Id,
                        candidate.StepRun.Id,
                        sourceFullPath);
                    continue;
                }

                var targetFullPath = Path.GetFullPath(Path.Combine(
                    workspaceRoot,
                    projectedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
                if (!IsWithinWorkspace(workspaceRoot, targetFullPath))
                {
                    logger.LogWarning(
                        "Skipping provider-native browser output projection for run {RunId}, step {StepRunId} because target path '{ProjectedPath}' resolves outside the workspace root.",
                        candidate.Run.Id,
                        candidate.StepRun.Id,
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

                    var content = await File.ReadAllBytesAsync(targetFullPath, cancellationToken);
                    var contentType = GuessContentTypeFromPath(targetFullPath);
                    var syntheticArtifact = new ProcessAutomationExecutionArtifact(
                        Guid.NewGuid(),
                        detail.Run.Id,
                        "generated-output",
                        Path.GetFileName(projectedRelativePath),
                        projectedRelativePath,
                        contentType,
                        pair.Key,
                        $"Projected provider-native browser output '{normalizedOutputPath}' into the scoped managed artifact path.",
                        DateTimeOffset.UtcNow);
                    var matchedExpectation = ResolveArtifactExpectation(
                        candidate,
                        detail.Run.InputSummary,
                        syntheticArtifact);
                    var recordExpectation = matchedExpectation is not null &&
                                            !candidate.RecordedArtifactExpectationIds.Contains(matchedExpectation.Id)
                        ? matchedExpectation
                        : null;
                    var projectionPlan = ProviderNativeBrowserArtifactProjectionSourceAdapter.PlanDiscoveredOutput(
                        projectionSource,
                        recordExpectation is null ? null : ToProjectionExpectation(recordExpectation),
                        ProcessArtifactKind.Evidence,
                        BuildProviderNativeBrowserArtifactTitle(syntheticArtifact),
                        completionStatus,
                        recoveryContext);

                    var writeResult = await writeCoordinator.WriteAsync(
                        new ProcessArtifactProjectionWriteRequest(
                            candidate.Run.Id,
                            candidate.StepRun.Id,
                            candidate.Run.ProjectId,
                            projectionPlan,
                            Path.GetFileName(targetFullPath),
                            contentType,
                            content,
                            ResolveStorageContentKind(contentType, targetFullPath),
                            projectedRelativePath),
                        cancellationToken);

                    if (!TryApplyProjectionWriteOutcome(candidate, writeResult, recordExpectation?.Id, out var errorSummary))
                    {
                        logger.LogWarning(
                            "Provider-native browser output projection failed for run {RunId}, step {StepRunId}, output {OutputPath}. Errors: {Errors}",
                            candidate.Run.Id,
                            candidate.StepRun.Id,
                            normalizedOutputPath,
                            errorSummary);
                    }
                }
                catch (Exception exception)
                {
                    logger.LogWarning(
                        exception,
                        "Provider-native browser output projection failed for run {RunId}, step {StepRunId}, output {OutputPath}.",
                        candidate.Run.Id,
                        candidate.StepRun.Id,
                        normalizedOutputPath);
                }
            }
        }
    }

    private static string ResolveProviderNativeBrowserProjectedRelativePath(
        DispatchCandidate candidate,
        WorkspaceScopeDescriptor workspaceScope,
        string normalizedOutputPath)
    {
        if (IsManagedBrowserArtifactPath(normalizedOutputPath))
        {
            return ResolveScopedManagedRelativePath(workspaceScope, normalizedOutputPath);
        }

        var fileName = Path.GetFileName(normalizedOutputPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "browser-proof";
        }

        return ResolveScopedManagedRelativePath(
            workspaceScope,
            WorkspaceScopeDescriptor.NormalizeRelativePath(Path.Combine(
                BuildCurrentRunManagedArtifactRoot(candidate),
                "browser",
                fileName)));
    }

    private static bool IsManagedBrowserArtifactPath(string relativePath)
    {
        var comparablePath = NormalizeManagedRelativePathForComparison(
            WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath));
        return comparablePath.StartsWith("artifacts/process-runs/", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildProviderNativeBrowserArtifactTitle(ProcessAutomationExecutionArtifact artifact)
    {
        var normalizedToolName = NormalizeToolToken(artifact.ProducedBy);
        return normalizedToolName switch
        {
            "browser_take_screenshot" => "Browser screenshot",
            "browser_snapshot" => "Browser snapshot",
            "browser_console_messages" => "Browser console log",
            "browser_evaluate" => "Browser DOM or state proof",
            _ => BuildArtifactTitle(artifact)
        };
    }

    private static bool IsProviderNativeBrowserArtifactPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath);
        var comparablePath = NormalizeManagedRelativePathForComparison(normalizedPath);
        if (comparablePath.StartsWith("artifacts/process-runs/", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveProviderNativeBrowserToolName(comparablePath).Length > 0;
        }

        return normalizedPath.StartsWith(".playwright-mcp/", StringComparison.OrdinalIgnoreCase) &&
               ResolveProviderNativeBrowserToolName(normalizedPath).Length > 0;
    }

    private void EnsureProviderNativeBrowserOutputDirectories(DispatchCandidate candidate)
    {
        if (!RequiresConcreteBrowserProof(candidate))
        {
            return;
        }

        var workspaceRoot = workspacePathResolver.ResolveWorkspaceRoot();
        foreach (var relativeDirectory in ResolveProviderNativeBrowserOutputDirectoryPaths(
                     BuildCurrentRunManagedArtifactRoot(candidate),
                     candidate.ExpectedArtifacts))
        {
            var fullPath = Path.GetFullPath(Path.Combine(
                workspaceRoot,
                relativeDirectory.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsWithinWorkspace(workspaceRoot, fullPath))
            {
                logger.LogWarning(
                    "Skipping provider-native browser output directory preflight for process run {RunId}; path '{RelativeDirectory}' resolves outside the workspace root.",
                    candidate.Run.Id,
                    relativeDirectory);
                continue;
            }

            Directory.CreateDirectory(fullPath);
        }
    }

    internal static IReadOnlyList<string> ResolveProviderNativeBrowserOutputDirectoryPaths(
        string currentRunManagedArtifactRoot,
        IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts)
    {
        var directories = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        AddProviderNativeBrowserDirectory(directories, currentRunManagedArtifactRoot);

        foreach (var expectedArtifact in expectedArtifacts)
        {
            if (!TryExtractExpectedArtifactRelativePath(expectedArtifact.ValidationRequirementSummary, out var expectedRelativePath) ||
                string.IsNullOrWhiteSpace(ResolveProviderNativeBrowserToolName(expectedRelativePath)))
            {
                continue;
            }

            AddProviderNativeBrowserDirectory(
                directories,
                Path.GetDirectoryName(expectedRelativePath.Replace('/', Path.DirectorySeparatorChar)) ?? string.Empty);
        }

        return directories.ToList();
    }

    private static void AddProviderNativeBrowserDirectory(
        ISet<string> directories,
        string relativeDirectory)
    {
        var normalizedDirectory = WorkspaceScopeDescriptor.NormalizeRelativePath(relativeDirectory);
        if (string.IsNullOrWhiteSpace(normalizedDirectory) ||
            Path.IsPathRooted(normalizedDirectory) ||
            IsExternalTargetAliasPath(normalizedDirectory))
        {
            return;
        }

        var segments = normalizedDirectory.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0 || !IsManagedRootSegment(segments[0]))
        {
            return;
        }

        directories.Add(normalizedDirectory);
    }

    private async Task<Result> TransitionStepAsync(
        ProcessStepTransitionRequest request,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        return await processesService.TransitionStepAsync(request, cancellationToken);
    }

    private async Task<Result> TransitionStepWithClaimAsync(
        ProcessStepTransitionRequest request,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        await EnsureStepDispatchClaimHeldAsync(dispatchClaim, cancellationToken);
        return await TransitionStepAsync(request, cancellationToken);
    }

    private async Task<StepRunTransitionSnapshot?> LoadStepRunTransitionSnapshotAsync(
        Guid stepRunId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<ProcessStepRun>()
            .AsNoTracking()
            .Where(item => item.Id == stepRunId)
            .Select(item => new StepRunTransitionSnapshot(item.Id, item.Status, item.ConcurrencyToken))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<Result<Guid>> RecordArtifactAsync(
        ProcessArtifactRecordRequest request,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        return await processesService.RecordArtifactAsync(request, cancellationToken);
    }

    private static string ApplyArtifactProjectionLineage(
        string externalReferenceKey,
        Guid executionRunId,
        ArtifactProjectionLineage? lineage)
        => ProcessArtifactProjectionLineageBuilder.ApplyRecoveryLineage(
            externalReferenceKey,
            executionRunId,
            CreateArtifactRecoveryProjectionContext(lineage));

    private static ProcessArtifactProjectionLineage BuildArtifactProjectionLineage(
        ProcessArtifactProjectionSourceKind sourceKind,
        Guid? sourceExecutionRunId = null,
        ArtifactProjectionLineage? lineage = null,
        Guid? sourceArtifactId = null,
        string sourceExternalReferenceKey = "")
        => ProcessArtifactProjectionLineageBuilder.BuildLineage(
            sourceKind,
            sourceExecutionRunId,
            CreateArtifactRecoveryProjectionContext(lineage),
            sourceArtifactId,
            sourceExternalReferenceKey);

    private static string BuildArtifactProjectionProvenance(
        string baseProvenance,
        Guid executionRunId,
        ArtifactProjectionLineage? lineage)
        => ProcessArtifactProjectionLineageBuilder.BuildProvenance(
            baseProvenance,
            executionRunId,
            CreateArtifactRecoveryProjectionContext(lineage));

    private static ProcessArtifactRecoveryProjectionContext CreateArtifactRecoveryProjectionContext(ArtifactProjectionLineage? lineage)
    {
        return lineage is null
            ? ProcessArtifactRecoveryProjectionContext.None
            : new ProcessArtifactRecoveryProjectionContext(
                lineage.RecoveryExecutionRunId,
                lineage.RecoveredForExecutionRunId,
                lineage.ReworkPacketId);
    }

}
