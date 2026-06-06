using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using System.Text;

using DispatchArtifactExpectation = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.DispatchArtifactExpectation;
using ProcessMockArtifactProjection = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.ProcessMockArtifactProjection;
using SessionFileContent = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.SessionFileContent;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessWorkspaceWrittenArtifactProjectionCoordinator : IProcessArtifactProjectionSourceCoordinator
{
    private readonly IProcessArtifactProjectionHost host;

    public ProcessWorkspaceWrittenArtifactProjectionCoordinator(IProcessArtifactProjectionHost host)
    {
        this.host = host;
    }

    public async Task ProjectAsync(ProcessArtifactProjectionContext context)
    {
        if (context.Candidate.ExpectedArtifacts.Count == 0)
        {
            return;
        }

        var fileWrites = host.ResolveSuccessfulSessionFileWrites(context.Detail.Run.SerializedSessionStateJson);
        var receiptFileWrites = host.ResolveSuccessfulWorkspaceFileMutationReceiptPaths(context.Detail)
            .Select(path => new SessionFileContent(path, string.Empty))
            .ToList();
        if (fileWrites.Count == 0 && receiptFileWrites.Count == 0)
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

            var matchingWrite = host.TryResolveProjectStructureExpectedArtifactPath(
                    context.Candidate,
                    expectedArtifact,
                    context.Detail.Run.InputSummary,
                    out var governedPath)
                ? fileWrites.LastOrDefault(file => host.ArtifactPathMatchesGovernedProjectStructurePath(file.Path, governedPath)) ??
                  receiptFileWrites.LastOrDefault(file => host.ArtifactPathMatchesGovernedProjectStructurePath(file.Path, governedPath))
                : fileWrites.LastOrDefault(file => host.WorkspaceWrittenFileMatchesExpectedArtifact(
                    context.Candidate.ExpectedArtifacts,
                    expectedArtifact,
                    file.Path,
                    file.Content)) ??
                  receiptFileWrites.LastOrDefault(file => host.WorkspaceWrittenFileMatchesExpectedArtifact(
                    context.Candidate.ExpectedArtifacts,
                    expectedArtifact,
                    file.Path,
                    file.Content));
            if (matchingWrite is null)
            {
                continue;
            }

            var projectedRelativePath = host.ResolveWorkspaceWrittenArtifactRelativePath(context.WorkspaceScope, matchingWrite.Path);
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

            if (!host.TryResolveWorkspaceWrittenArtifactSourceFullPath(
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
                host.GuessContentTypeFromPath(fullPath),
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
                    host.ResolveStorageContentKind(syntheticArtifact.ContentType, fullPath),
                    host.BuildStorageRelativePath(context.Candidate, syntheticArtifact)),
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
