using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using System.Text;

using DispatchArtifactExpectation = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.DispatchArtifactExpectation;
using ProcessMockArtifactProjection = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.ProcessMockArtifactProjection;
using SessionFileContent = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.SessionFileContent;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessExistingManagedArtifactProjectionCoordinator : IProcessArtifactProjectionSourceCoordinator
{
    private readonly IProcessArtifactProjectionHost host;

    public ProcessExistingManagedArtifactProjectionCoordinator(IProcessArtifactProjectionHost host)
    {
        this.host = host;
    }

    public async Task ProjectAsync(ProcessArtifactProjectionContext context)
    {
        if (context.Candidate.ExpectedArtifacts.Count == 0)
        {
            return;
        }

        foreach (var expectedArtifact in context.Candidate.ExpectedArtifacts)
        {
            if (context.Candidate.RecordedArtifactExpectationIds.Contains(expectedArtifact.Id) ||
                context.Detail.Artifacts.Any(artifact => host.ResolveArtifactExpectationId(context.Candidate, context.Detail, artifact) == expectedArtifact.Id))
            {
                continue;
            }

            var projectedRelativePath = host.ResolveExpectedManagedArtifactRelativePaths(
                    context.Candidate,
                    context.WorkspaceScope,
                    expectedArtifact)
                .FirstOrDefault(relativePath => host.ExistingManagedArtifactFileMatches(
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
        ProcessArtifactProjectionContext context,
        DispatchArtifactExpectation expectedArtifact,
        string projectedRelativePath,
        string targetFullPath)
    {
        if (!host.ExistingManagedArtifactFileMatches(
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
        ProcessArtifactProjectionContext context,
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
        else if (!host.TryResolveArtifactFullPath(context.WorkspaceRoot, projectedRelativePath, out fullPath, out pathResolutionFailure) ||
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

        var contentType = host.GuessContentTypeFromPath(fullPath);
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
                host.ResolveStorageContentKind(contentType, fullPath),
                host.BuildStorageRelativePath(context.Candidate, syntheticArtifact)),
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
