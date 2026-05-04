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
        ExecutionRunDetail detail,
        string responseText,
        ProcessStepRunStatus completionStatus,
        CancellationToken cancellationToken)
    {
        var workspaceRoot = Path.GetFullPath(workspacePathResolver.ResolveWorkspaceRoot());
        var workspaceScope = WorkspaceScopeDescriptor.Organization(
            databaseProfileRuntimeAccessor.ResolveCurrentProfile().Profile.Id.ToString("N"));
        foreach (var artifact in detail.Artifacts)
        {
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

            var externalReferenceKey = BuildExternalReferenceKey(artifact);
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
                artifact,
                TryDecodeTextArtifactContent(artifact, fullPath, content));
            var placement = await storagePlacementService.PlaceAsync(
                new StoragePlacementRequest(
                    Path.GetFileName(fullPath),
                    string.IsNullOrWhiteSpace(artifact.ContentType)
                        ? "application/octet-stream"
                        : artifact.ContentType,
                    content,
                    StorageUsagePurpose.Evidence,
                    ResolveStorageContentKind(artifact.ContentType, fullPath),
                    ProjectId: candidate.Run.ProjectId,
                    RelativePathHint: BuildStorageRelativePath(candidate, artifact)),
                cancellationToken);

            var recordResult = await RecordArtifactAsync(
                new ProcessArtifactRecordRequest
                {
                    ProcessRunId = candidate.Run.Id,
                    StepRunId = candidate.StepRun.Id,
                    ArtifactExpectationId = matchedExpectation?.Id,
                    ArtifactKind = matchedExpectation?.ArtifactKind ?? ResolveProcessArtifactKind(candidate, artifact),
                    Title = matchedExpectation?.Title ?? BuildArtifactTitle(artifact),
                    TrustStatus = matchedExpectation is null
                        ? ProcessArtifactTrustStatus.ReviewRequired
                        : ResolveProjectedArtifactTrustStatus(matchedExpectation, completionStatus),
                    SensitivityLevel = matchedExpectation?.SensitivityLevel ?? ProcessSensitivityLevel.Internal,
                    ProvenanceSummary = $"Projected from AgentFramework execution run {detail.Run.Id:D} artifact '{artifact.RelativePath}'.",
                    AllowedFutureUsageSummary = "Process evidence and audit review.",
                    ReviewSummary = string.IsNullOrWhiteSpace(artifact.Summary)
                        ? detail.Run.ResultSummary
                        : artifact.Summary,
                    ManagedStoragePath = placement.RelativePath,
                    ExternalReferenceKey = externalReferenceKey
                },
                cancellationToken);
            if (recordResult.IsSuccess)
            {
                candidate.ExternalReferenceKeys.Add(externalReferenceKey);
            }
            else
            {
                logger.LogWarning(
                    "Process artifact projection failed for run {RunId}, step {StepRunId}, artifact {ArtifactId}. Errors: {Errors}",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    artifact.Id,
                    string.Join(" | ", recordResult.Errors.Select(error => error.Message)));
            }
        }

        await ProjectProcessMockArtifactsAsync(
            candidate,
            detail,
            workspaceRoot,
            workspaceScope,
            completionStatus,
            cancellationToken);
        await ProjectWorkspaceWrittenArtifactsAsync(
            candidate,
            detail,
            workspaceRoot,
            workspaceScope,
            completionStatus,
            cancellationToken);
        await ProjectExistingManagedArtifactFilesAsync(
            candidate,
            detail,
            workspaceRoot,
            workspaceScope,
            completionStatus,
            cancellationToken);
        await ProjectResponseTextArtifactsAsync(
            candidate,
            detail,
            responseText,
            workspaceRoot,
            completionStatus,
            cancellationToken);
        await ProjectProviderNativeBrowserArtifactsAsync(
            candidate,
            detail,
            workspaceRoot,
            completionStatus,
            cancellationToken);
        await EnsureDecisionArtifactsForCompletedStepAsync(
            candidate,
            detail,
            responseText,
            completionStatus,
            cancellationToken);
    }

    private async Task ProjectProcessMockArtifactsAsync(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope,
        ProcessStepRunStatus completionStatus,
        CancellationToken cancellationToken)
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
            var externalReferenceKey = BuildProcessMockArtifactExternalReferenceKey(
                candidate.StepRun.Id,
                expectedArtifact.Id,
                projection.RelativePath);
            if (candidate.ExternalReferenceKeys.Contains(externalReferenceKey))
            {
                projectedExpectationIds.Add(expectedArtifact.Id);
                continue;
            }

            var scopedRelativePath = ResolveScopedManagedRelativePath(workspaceScope, projection.RelativePath);
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
            var placement = await storagePlacementService.PlaceAsync(
                new StoragePlacementRequest(
                    Path.GetFileName(fullPath),
                    contentType,
                    content,
                    StorageUsagePurpose.Evidence,
                    ResolveStorageContentKind(contentType, fullPath),
                    ProjectId: candidate.Run.ProjectId,
                    RelativePathHint: scopedRelativePath),
                cancellationToken);

            var recordResult = await RecordArtifactAsync(
                new ProcessArtifactRecordRequest
                {
                    ProcessRunId = candidate.Run.Id,
                    StepRunId = candidate.StepRun.Id,
                    ArtifactExpectationId = expectedArtifact.Id,
                    ArtifactKind = expectedArtifact.ArtifactKind,
                    Title = expectedArtifact.Title,
                    TrustStatus = ResolveProjectedArtifactTrustStatus(expectedArtifact, completionStatus),
                    SensitivityLevel = expectedArtifact.SensitivityLevel,
                    ProvenanceSummary = $"Projected from deterministic process mock artifact '{projection.RelativePath}' at scoped workspace path '{scopedRelativePath}' for AgentFramework execution run {detail.Run.Id:D}.",
                    AllowedFutureUsageSummary = string.IsNullOrWhiteSpace(expectedArtifact.AllowedFutureUsageSummary)
                        ? "Process mock evidence and regression audit review."
                        : expectedArtifact.AllowedFutureUsageSummary,
                    ReviewSummary = $"Process mock role '{projection.RoleKey}' produced '{Path.GetFileName(projection.RelativePath)}'.",
                    ManagedStoragePath = placement.RelativePath,
                    ExternalReferenceKey = externalReferenceKey
                },
                cancellationToken);
            if (recordResult.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Process mock artifact projection failed for expected artifact '{expectedArtifact.Title}': {string.Join(" | ", recordResult.Errors.Select(error => error.Message))}");
            }

            candidate.ExternalReferenceKeys.Add(externalReferenceKey);
            projectedExpectationIds.Add(expectedArtifact.Id);
        }
    }

    private async Task ProjectWorkspaceWrittenArtifactsAsync(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope,
        ProcessStepRunStatus completionStatus,
        CancellationToken cancellationToken)
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

        foreach (var expectedArtifact in candidate.ExpectedArtifacts)
        {
            if (detail.Artifacts.Any(artifact => ResolveArtifactExpectationId(candidate, artifact) == expectedArtifact.Id))
            {
                continue;
            }

            var matchingWrite = fileWrites
                .LastOrDefault(file => WorkspaceWrittenFileMatchesExpectedArtifact(
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

            var externalReferenceKey = BuildWorkspaceWrittenArtifactExternalReferenceKey(
                detail.Run.Id,
                expectedArtifact.Id,
                projectedRelativePath);
            if (candidate.ExternalReferenceKeys.Contains(externalReferenceKey))
            {
                continue;
            }

            if (!TryResolveArtifactFullPath(workspaceRoot, projectedRelativePath, out var fullPath, out var pathResolutionFailure) ||
                !File.Exists(fullPath))
            {
                logger.LogDebug(
                    "Skipping workspace-written artifact projection for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle} because path '{RelativePath}' is unavailable. Reason: {Reason}",
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
                    "Workspace-written artifact '{ArtifactTitle}' could not be read for process run {RunId}.",
                    expectedArtifact.Title,
                    candidate.Run.Id);
                continue;
            }

            var syntheticArtifact = new ExecutionArtifactRecord(
                Guid.NewGuid(),
                detail.Run.Id,
                "generated-output",
                expectedArtifact.Title,
                projectedRelativePath,
                GuessContentTypeFromPath(fullPath),
                "workspace_write_file",
                $"Projected from workspace file write '{matchingWrite.Path}' for AgentFramework execution run {detail.Run.Id:D}.",
                DateTimeOffset.UtcNow);
            var placement = await storagePlacementService.PlaceAsync(
                new StoragePlacementRequest(
                    Path.GetFileName(fullPath),
                    syntheticArtifact.ContentType,
                    content,
                    StorageUsagePurpose.Evidence,
                    ResolveStorageContentKind(syntheticArtifact.ContentType, fullPath),
                    ProjectId: candidate.Run.ProjectId,
                    RelativePathHint: BuildStorageRelativePath(candidate, syntheticArtifact)),
                cancellationToken);

            var recordResult = await RecordArtifactAsync(
                new ProcessArtifactRecordRequest
                {
                    ProcessRunId = candidate.Run.Id,
                    StepRunId = candidate.StepRun.Id,
                    ArtifactExpectationId = expectedArtifact.Id,
                    ArtifactKind = expectedArtifact.ArtifactKind,
                    Title = expectedArtifact.Title,
                    TrustStatus = ResolveProjectedArtifactTrustStatus(expectedArtifact, completionStatus),
                    SensitivityLevel = expectedArtifact.SensitivityLevel,
                    ProvenanceSummary = syntheticArtifact.Summary,
                    AllowedFutureUsageSummary = string.IsNullOrWhiteSpace(expectedArtifact.AllowedFutureUsageSummary)
                        ? "Process evidence and audit review."
                        : expectedArtifact.AllowedFutureUsageSummary,
                    ReviewSummary = $"Workspace file write produced '{projectedRelativePath}'.",
                    ManagedStoragePath = placement.RelativePath,
                    ExternalReferenceKey = externalReferenceKey
                },
                cancellationToken);
            if (recordResult.IsSuccess)
            {
                candidate.ExternalReferenceKeys.Add(externalReferenceKey);
            }
            else
            {
                logger.LogWarning(
                    "Workspace-written artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}. Errors: {Errors}",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    expectedArtifact.Title,
                    string.Join(" | ", recordResult.Errors.Select(error => error.Message)));
            }
        }
    }

    private static IReadOnlyList<string> ResolveSuccessfulWorkspaceFileMutationReceiptPaths(ExecutionRunDetail detail)
    {
        return detail.ToolReceipts
            .Where(IsSuccessfulWorkspaceFileMutationReceipt)
            .SelectMany(ResolveManagedWorkspacePathsFromReceipt)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task ProjectExistingManagedArtifactFilesAsync(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope,
        ProcessStepRunStatus completionStatus,
        CancellationToken cancellationToken)
    {
        if (candidate.ExpectedArtifacts.Count == 0)
        {
            return;
        }

        foreach (var expectedArtifact in candidate.ExpectedArtifacts)
        {
            if (detail.Artifacts.Any(artifact => ResolveArtifactExpectationId(candidate, artifact) == expectedArtifact.Id))
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

            var externalReferenceKey = BuildExistingManagedArtifactExternalReferenceKey(
                detail.Run.Id,
                expectedArtifact.Id,
                projectedRelativePath);
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
            var placement = await storagePlacementService.PlaceAsync(
                new StoragePlacementRequest(
                    Path.GetFileName(fullPath),
                    contentType,
                    content,
                    StorageUsagePurpose.Evidence,
                    ResolveStorageContentKind(contentType, fullPath),
                    ProjectId: candidate.Run.ProjectId,
                    RelativePathHint: BuildStorageRelativePath(
                        candidate,
                        new ExecutionArtifactRecord(
                            Guid.NewGuid(),
                            detail.Run.Id,
                            "generated-output",
                            expectedArtifact.Title,
                            projectedRelativePath,
                            contentType,
                            "managed-workspace-file",
                            $"Projected from existing managed workspace artifact '{projectedRelativePath}' for AgentFramework execution run {detail.Run.Id:D}.",
                            DateTimeOffset.UtcNow))),
                cancellationToken);

            var recordResult = await RecordArtifactAsync(
                new ProcessArtifactRecordRequest
                {
                    ProcessRunId = candidate.Run.Id,
                    StepRunId = candidate.StepRun.Id,
                    ArtifactExpectationId = expectedArtifact.Id,
                    ArtifactKind = expectedArtifact.ArtifactKind,
                    Title = expectedArtifact.Title,
                    TrustStatus = ResolveProjectedArtifactTrustStatus(expectedArtifact, completionStatus),
                    SensitivityLevel = expectedArtifact.SensitivityLevel,
                    ProvenanceSummary = $"Projected from existing managed workspace artifact '{projectedRelativePath}' for AgentFramework execution run {detail.Run.Id:D}.",
                    AllowedFutureUsageSummary = string.IsNullOrWhiteSpace(expectedArtifact.AllowedFutureUsageSummary)
                        ? "Process evidence and audit review."
                        : expectedArtifact.AllowedFutureUsageSummary,
                    ReviewSummary = $"Managed workspace artifact '{projectedRelativePath}' already existed when the step outcome was finalized.",
                    ManagedStoragePath = placement.RelativePath,
                    ExternalReferenceKey = externalReferenceKey
                },
                cancellationToken);
            if (recordResult.IsSuccess)
            {
                candidate.ExternalReferenceKeys.Add(externalReferenceKey);
            }
            else
            {
                logger.LogWarning(
                    "Existing managed artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}. Errors: {Errors}",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    expectedArtifact.Title,
                    string.Join(" | ", recordResult.Errors.Select(error => error.Message)));
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
                    new ExecutionArtifactRecord(
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

        var syntheticArtifact = new ExecutionArtifactRecord(
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
        ExecutionRunDetail detail,
        string responseText,
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

            var recordResult = await RecordArtifactAsync(
                new ProcessArtifactRecordRequest
                {
                    ProcessRunId = candidate.Run.Id,
                    StepRunId = candidate.StepRun.Id,
                    ArtifactExpectationId = expectedArtifact.Id,
                    ArtifactKind = expectedArtifact.ArtifactKind,
                    Title = expectedArtifact.Title,
                    TrustStatus = ResolveCompletedDecisionArtifactTrustStatus(expectedArtifact.TrustRequirement),
                    SensitivityLevel = expectedArtifact.SensitivityLevel,
                    ProvenanceSummary = BuildCompletedDecisionArtifactProvenanceSummary(candidate, detail),
                    AllowedFutureUsageSummary = string.IsNullOrWhiteSpace(expectedArtifact.AllowedFutureUsageSummary)
                        ? "Reusable for audit, release replay, and governance tuning."
                        : expectedArtifact.AllowedFutureUsageSummary,
                    ReviewSummary = BuildCompletedDecisionArtifactReviewSummary(candidate, detail, responseText, expectedArtifact),
                    ExternalReferenceKey = externalReferenceKey
                },
                cancellationToken);
            if (recordResult.IsSuccess)
            {
                candidate.ExternalReferenceKeys.Add(externalReferenceKey);
            }
            else
            {
                logger.LogWarning(
                    "Completed-step decision artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}. Errors: {Errors}",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    expectedArtifact.Title,
                    string.Join(" | ", recordResult.Errors.Select(error => error.Message)));
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

    private async Task ProjectResponseTextArtifactsAsync(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string responseText,
        string workspaceRoot,
        ProcessStepRunStatus completionStatus,
        CancellationToken cancellationToken)
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

            if (detail.Artifacts.Any(artifact => ResolveArtifactExpectationId(candidate, artifact) == expectedArtifact.Id))
            {
                continue;
            }

            var externalReferenceKey = BuildResponseTextArtifactExternalReferenceKey(detail.Run.Id, projectedRelativePath);
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
                        completionStatus,
                        cancellationToken))
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
                var syntheticArtifact = new ExecutionArtifactRecord(
                    Guid.NewGuid(),
                    detail.Run.Id,
                    "generated-output",
                    expectedArtifact.Title,
                    projectedRelativePath,
                    GuessContentTypeFromPath(targetFullPath),
                    "assistant-response",
                    "Projected the final assistant response into the required managed text artifact path.",
                    DateTimeOffset.UtcNow);

                var placement = await storagePlacementService.PlaceAsync(
                    new StoragePlacementRequest(
                        Path.GetFileName(targetFullPath),
                        syntheticArtifact.ContentType,
                        content,
                        StorageUsagePurpose.Evidence,
                        ResolveStorageContentKind(syntheticArtifact.ContentType, targetFullPath),
                        ProjectId: candidate.Run.ProjectId,
                        RelativePathHint: BuildStorageRelativePath(candidate, syntheticArtifact)),
                    cancellationToken);

                var recordResult = await RecordArtifactAsync(
                    new ProcessArtifactRecordRequest
                    {
                        ProcessRunId = candidate.Run.Id,
                        StepRunId = candidate.StepRun.Id,
                        ArtifactExpectationId = expectedArtifact.Id,
                        ArtifactKind = expectedArtifact.ArtifactKind,
                        Title = expectedArtifact.Title,
                        TrustStatus = ResolveProjectedArtifactTrustStatus(expectedArtifact, completionStatus),
                        SensitivityLevel = expectedArtifact.SensitivityLevel,
                        ProvenanceSummary = $"Projected from the final assistant response for AgentFramework execution run {detail.Run.Id:D}.",
                        AllowedFutureUsageSummary = string.IsNullOrWhiteSpace(expectedArtifact.AllowedFutureUsageSummary)
                            ? "Process evidence and audit review."
                            : expectedArtifact.AllowedFutureUsageSummary,
                        ReviewSummary = syntheticArtifact.Summary,
                        ManagedStoragePath = placement.RelativePath,
                        ExternalReferenceKey = externalReferenceKey
                    },
                    cancellationToken);
                if (recordResult.IsSuccess)
                {
                    candidate.ExternalReferenceKeys.Add(externalReferenceKey);
                }
                else
                {
                    logger.LogWarning(
                        "Response-text artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}. Errors: {Errors}",
                        candidate.Run.Id,
                        candidate.StepRun.Id,
                        expectedArtifact.Title,
                        string.Join(" | ", recordResult.Errors.Select(error => error.Message)));
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
        ExecutionRunDetail detail,
        DispatchArtifactExpectation expectedArtifact,
        string workspaceRoot,
        string projectedRelativePath,
        string targetFullPath,
        ProcessStepRunStatus completionStatus,
        CancellationToken cancellationToken)
    {
        if (!ExistingManagedArtifactFileMatches(
                candidate.ExpectedArtifacts,
                expectedArtifact,
                workspaceRoot,
                projectedRelativePath))
        {
            return false;
        }

        var externalReferenceKey = BuildExistingManagedArtifactExternalReferenceKey(
            detail.Run.Id,
            expectedArtifact.Id,
            projectedRelativePath);
        if (candidate.ExternalReferenceKeys.Contains(externalReferenceKey))
        {
            return true;
        }

        var content = await File.ReadAllBytesAsync(targetFullPath, cancellationToken);
        var contentType = GuessContentTypeFromPath(targetFullPath);
        var syntheticArtifact = new ExecutionArtifactRecord(
            Guid.NewGuid(),
            detail.Run.Id,
            "generated-output",
            expectedArtifact.Title,
            projectedRelativePath,
            contentType,
            "managed-workspace-file",
            $"Projected from existing managed workspace artifact '{projectedRelativePath}' for AgentFramework execution run {detail.Run.Id:D}.",
            DateTimeOffset.UtcNow);
        var placement = await storagePlacementService.PlaceAsync(
            new StoragePlacementRequest(
                Path.GetFileName(targetFullPath),
                contentType,
                content,
                StorageUsagePurpose.Evidence,
                ResolveStorageContentKind(contentType, targetFullPath),
                ProjectId: candidate.Run.ProjectId,
                RelativePathHint: BuildStorageRelativePath(candidate, syntheticArtifact)),
            cancellationToken);
        var recordResult = await RecordArtifactAsync(
            new ProcessArtifactRecordRequest
            {
                ProcessRunId = candidate.Run.Id,
                StepRunId = candidate.StepRun.Id,
                ArtifactExpectationId = expectedArtifact.Id,
                ArtifactKind = expectedArtifact.ArtifactKind,
                Title = expectedArtifact.Title,
                TrustStatus = ResolveProjectedArtifactTrustStatus(expectedArtifact, completionStatus),
                SensitivityLevel = expectedArtifact.SensitivityLevel,
                ProvenanceSummary = syntheticArtifact.Summary,
                AllowedFutureUsageSummary = string.IsNullOrWhiteSpace(expectedArtifact.AllowedFutureUsageSummary)
                    ? "Process evidence and audit review."
                    : expectedArtifact.AllowedFutureUsageSummary,
                ReviewSummary = $"Managed workspace artifact '{projectedRelativePath}' already existed when the step outcome was finalized.",
                ManagedStoragePath = placement.RelativePath,
                ExternalReferenceKey = externalReferenceKey
            },
            cancellationToken);
        if (recordResult.IsFailure)
        {
            logger.LogWarning(
                "Existing response-target artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}. Errors: {Errors}",
                candidate.Run.Id,
                candidate.StepRun.Id,
                expectedArtifact.Title,
                string.Join(" | ", recordResult.Errors.Select(error => error.Message)));
            return false;
        }

        candidate.ExternalReferenceKeys.Add(externalReferenceKey);
        return true;
    }

    private async Task ProjectProviderNativeBrowserArtifactsAsync(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string workspaceRoot,
        ProcessStepRunStatus completionStatus,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(detail.Run.SerializedSessionStateJson))
        {
            return;
        }

        var browserOutputsByToolName = ResolveSuccessfulBrowserToolOutputFiles(detail);
        if (browserOutputsByToolName.Count == 0)
        {
            return;
        }

        var browserWorkingDirectory = ResolveProviderNativeBrowserWorkingDirectory(detail);
        if (string.IsNullOrWhiteSpace(browserWorkingDirectory))
        {
            return;
        }

        var workspaceScope = WorkspaceScopeDescriptor.Organization(
            databaseProfileRuntimeAccessor.ResolveCurrentProfile().Profile.Id.ToString("N"));

        foreach (var expectedArtifact in candidate.ExpectedArtifacts)
        {
            if (!TryExtractExpectedArtifactRelativePath(expectedArtifact.ValidationRequirementSummary, out var expectedRelativePath))
            {
                continue;
            }

            if (detail.Artifacts.Any(artifact => ResolveArtifactExpectationId(candidate, artifact) == expectedArtifact.Id))
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
                var syntheticArtifact = new ExecutionArtifactRecord(
                    Guid.NewGuid(),
                    detail.Run.Id,
                    "generated-output",
                    expectedArtifact.Title,
                    projectedRelativePath,
                    GuessContentTypeFromPath(targetFullPath),
                    requiredToolName,
                    $"Projected provider-native browser output '{matchedOutputFileName}' into the required managed artifact path.",
                    DateTimeOffset.UtcNow);
                var externalReferenceKey = BuildProviderNativeBrowserArtifactExternalReferenceKey(
                    detail.Run.Id,
                    projectedRelativePath);
                if (candidate.ExternalReferenceKeys.Contains(externalReferenceKey))
                {
                    continue;
                }

                var placement = await storagePlacementService.PlaceAsync(
                    new StoragePlacementRequest(
                        Path.GetFileName(targetFullPath),
                        syntheticArtifact.ContentType,
                        content,
                        StorageUsagePurpose.Evidence,
                        ResolveStorageContentKind(syntheticArtifact.ContentType, targetFullPath),
                        ProjectId: candidate.Run.ProjectId,
                        RelativePathHint: BuildStorageRelativePath(candidate, syntheticArtifact)),
                    cancellationToken);

                var recordResult = await RecordArtifactAsync(
                    new ProcessArtifactRecordRequest
                    {
                        ProcessRunId = candidate.Run.Id,
                        StepRunId = candidate.StepRun.Id,
                        ArtifactExpectationId = expectedArtifact.Id,
                        ArtifactKind = expectedArtifact.ArtifactKind,
                        Title = expectedArtifact.Title,
                        TrustStatus = ResolveProjectedArtifactTrustStatus(expectedArtifact, completionStatus),
                        SensitivityLevel = expectedArtifact.SensitivityLevel,
                        ProvenanceSummary = $"Projected from provider-native browser output '{matchedOutputFileName}' for AgentFramework execution run {detail.Run.Id:D}.",
                        AllowedFutureUsageSummary = "Process evidence and audit review.",
                        ReviewSummary = syntheticArtifact.Summary,
                        ManagedStoragePath = placement.RelativePath,
                        ExternalReferenceKey = externalReferenceKey
                    },
                    cancellationToken);
                if (recordResult.IsSuccess)
                {
                    candidate.ExternalReferenceKeys.Add(externalReferenceKey);
                }
                else
                {
                    logger.LogWarning(
                        "Provider-native browser artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}. Errors: {Errors}",
                        candidate.Run.Id,
                        candidate.StepRun.Id,
                        expectedArtifact.Title,
                        string.Join(" | ", recordResult.Errors.Select(error => error.Message)));
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
            cancellationToken);
    }

    private async Task ProjectProviderNativeBrowserOutputArtifactsAsync(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope,
        IReadOnlyDictionary<string, IReadOnlyList<string>> browserOutputsByToolName,
        string browserWorkingDirectory,
        CancellationToken cancellationToken)
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

                var projectedRelativePath = ResolveScopedManagedRelativePath(workspaceScope, normalizedOutputPath);
                var externalReferenceKey = BuildProviderNativeBrowserArtifactExternalReferenceKey(
                    detail.Run.Id,
                    projectedRelativePath);
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
                    var syntheticArtifact = new ExecutionArtifactRecord(
                        Guid.NewGuid(),
                        detail.Run.Id,
                        "generated-output",
                        Path.GetFileName(projectedRelativePath),
                        projectedRelativePath,
                        contentType,
                        pair.Key,
                        $"Projected provider-native browser output '{normalizedOutputPath}' into the scoped managed artifact path.",
                        DateTimeOffset.UtcNow);
                    var placement = await storagePlacementService.PlaceAsync(
                        new StoragePlacementRequest(
                            Path.GetFileName(targetFullPath),
                            contentType,
                            content,
                            StorageUsagePurpose.Evidence,
                            ResolveStorageContentKind(contentType, targetFullPath),
                            ProjectId: candidate.Run.ProjectId,
                            RelativePathHint: projectedRelativePath),
                        cancellationToken);

                    var recordResult = await RecordArtifactAsync(
                        new ProcessArtifactRecordRequest
                        {
                            ProcessRunId = candidate.Run.Id,
                            StepRunId = candidate.StepRun.Id,
                            ArtifactKind = ProcessArtifactKind.Evidence,
                            Title = BuildArtifactTitle(syntheticArtifact),
                            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
                            SensitivityLevel = ProcessSensitivityLevel.Internal,
                            ProvenanceSummary = $"Projected from provider-native browser output '{normalizedOutputPath}' for AgentFramework execution run {detail.Run.Id:D}.",
                            AllowedFutureUsageSummary = "Process evidence and audit review.",
                            ReviewSummary = syntheticArtifact.Summary,
                            ManagedStoragePath = placement.RelativePath,
                            ExternalReferenceKey = externalReferenceKey
                        },
                        cancellationToken);
                    if (recordResult.IsSuccess)
                    {
                        candidate.ExternalReferenceKeys.Add(externalReferenceKey);
                    }
                    else
                    {
                        logger.LogWarning(
                            "Provider-native browser output projection failed for run {RunId}, step {StepRunId}, output {OutputPath}. Errors: {Errors}",
                            candidate.Run.Id,
                            candidate.StepRun.Id,
                            normalizedOutputPath,
                            string.Join(" | ", recordResult.Errors.Select(error => error.Message)));
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

    private static bool IsProviderNativeBrowserArtifactPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath);
        if (!normalizedPath.StartsWith("artifacts/process-runs/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return ResolveProviderNativeBrowserToolName(normalizedPath).Length > 0;
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

}
