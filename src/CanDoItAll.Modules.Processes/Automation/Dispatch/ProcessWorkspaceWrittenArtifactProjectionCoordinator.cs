using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using System.Text;


namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessWorkspaceWrittenArtifactProjectionCoordinator : IProcessArtifactProjectionSourceCoordinator
{
    private readonly IProcessProjectionPathResolver pathResolver;
    private readonly IProcessProjectionFileIo fileIo;
    private readonly IProcessProjectionArtifactClassifier artifactClassifier;
    private readonly IProcessProjectionExpectationMatcher expectationMatcher;
    private readonly IProcessProjectionProjectStructureMatcher projectStructureMatcher;
    private readonly IProcessProjectionSessionObservationSource sessionObservationSource;
    private readonly IProcessProjectionCandidateStateUpdater candidateState;

    public ProcessWorkspaceWrittenArtifactProjectionCoordinator(
        IProcessProjectionPathResolver pathResolver,
        IProcessProjectionFileIo fileIo,
        IProcessProjectionArtifactClassifier artifactClassifier,
        IProcessProjectionExpectationMatcher expectationMatcher,
        IProcessProjectionProjectStructureMatcher projectStructureMatcher,
        IProcessProjectionSessionObservationSource sessionObservationSource,
        IProcessProjectionCandidateStateUpdater candidateState)
    {
        this.pathResolver = pathResolver;
        this.fileIo = fileIo;
        this.artifactClassifier = artifactClassifier;
        this.expectationMatcher = expectationMatcher;
        this.projectStructureMatcher = projectStructureMatcher;
        this.sessionObservationSource = sessionObservationSource;
        this.candidateState = candidateState;
    }

    public async Task ProjectAsync(ProcessArtifactProjectionContext context)
    {
        if (context.Candidate.ExpectedArtifacts.Count == 0)
        {
            return;
        }

        var fileWrites = sessionObservationSource.ResolveSuccessfulSessionFileWrites(context.Run.SerializedSessionStateJson);
        var receiptFileWrites = sessionObservationSource.ResolveSuccessfulWorkspaceFileMutationReceiptPaths(context.Run)
            .Select(path => new ProcessProjectionSessionFileContent(path, string.Empty))
            .ToList();
        if (fileWrites.Count == 0 && receiptFileWrites.Count == 0)
        {
            return;
        }

        foreach (var expectedArtifact in context.Candidate.ExpectedArtifacts)
        {
            if (context.Candidate.MutableState.RecordedArtifactExpectationIds.Contains(expectedArtifact.Id) ||
                context.Run.Artifacts.Any(artifact => expectationMatcher.ResolveArtifactExpectationId(context.Candidate, context.Run, artifact) == expectedArtifact.Id))
            {
                continue;
            }

            var matchingWrite = projectStructureMatcher.TryResolveProjectStructureExpectedArtifactPath(
                    expectedArtifact,
                    context.Run.InputSummary,
                    out var governedPath)
                ? fileWrites.LastOrDefault(file => projectStructureMatcher.ArtifactPathMatchesGovernedProjectStructurePath(file.Path, governedPath)) ??
                  receiptFileWrites.LastOrDefault(file => projectStructureMatcher.ArtifactPathMatchesGovernedProjectStructurePath(file.Path, governedPath))
                : fileWrites.LastOrDefault(file => expectationMatcher.WorkspaceWrittenFileMatchesExpectedArtifact(
                    context.Candidate.ExpectedArtifacts,
                    expectedArtifact,
                    file.Path,
                    file.Content)) ??
                  receiptFileWrites.LastOrDefault(file => expectationMatcher.WorkspaceWrittenFileMatchesExpectedArtifact(
                    context.Candidate.ExpectedArtifacts,
                    expectedArtifact,
                    file.Path,
                    file.Content));
            if (matchingWrite is null)
            {
                continue;
            }

            var projectedRelativePath = pathResolver.ResolveWorkspaceWrittenArtifactRelativePath(context.WorkspaceScope, matchingWrite.Path);
            if (string.IsNullOrWhiteSpace(projectedRelativePath))
            {
                continue;
            }

            var expectedProjection = expectedArtifact;
            var duplicateProbeSource = new WorkspaceWrittenArtifactProjectionSource(
                context.Run.Id,
                projectedRelativePath,
                projectedRelativePath);
            var externalReferenceKey = WorkspaceWrittenArtifactProjectionSourceAdapter.BuildExternalReferenceKey(
                duplicateProbeSource,
                expectedProjection,
                context.RecoveryContext);
            if (context.Candidate.MutableState.ExternalReferenceKeys.Contains(externalReferenceKey))
            {
                continue;
            }

            if (!pathResolver.TryResolveWorkspaceWrittenArtifactSourceFullPath(
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
                    context.Candidate.RunId,
                    context.Candidate.Step.Id,
                    expectedArtifact.Title,
                    matchingWrite.Path,
                    projectedRelativePath,
                    string.IsNullOrWhiteSpace(pathResolutionFailure) ? "File does not exist." : pathResolutionFailure);
                continue;
            }

            byte[] content;
            try
            {
                content = await fileIo.ReadAllBytesAsync(fullPath, context.CancellationToken);
            }
            catch (Exception exception)
            {
                context.Logger.LogWarning(
                    exception,
                    "Workspace-written artifact '{ArtifactTitle}' could not be read for process run {RunId}.",
                    expectedArtifact.Title,
                    context.Candidate.RunId);
                continue;
            }

            var syntheticArtifact = new ProcessAutomationExecutionArtifact(
                Guid.NewGuid(),
                context.Run.Id,
                "generated-output",
                expectedArtifact.Title,
                projectedRelativePath,
                artifactClassifier.GuessContentTypeFromPath(fullPath),
                "workspace_write_file",
                $"Projected from workspace file write '{sourceRelativePath}' for AgentFramework execution run {context.Run.Id:D}.",
                DateTimeOffset.UtcNow);
            var projectionPlan = WorkspaceWrittenArtifactProjectionSourceAdapter.Plan(
                new WorkspaceWrittenArtifactProjectionSource(
                    context.Run.Id,
                    projectedRelativePath,
                    sourceRelativePath),
                expectedProjection,
                context.CompletionStatus,
                context.RecoveryContext);
            var writeResult = await context.WriteCoordinator.WriteAsync(
                new ProcessArtifactProjectionWriteRequest(
                    context.Candidate.RunId,
                    context.Candidate.Step.Id,
                    context.Candidate.ProjectId,
                    projectionPlan,
                    Path.GetFileName(fullPath),
                    syntheticArtifact.ContentType,
                    content,
                    artifactClassifier.ResolveStorageContentKind(syntheticArtifact.ContentType, fullPath),
                    artifactClassifier.BuildStorageRelativePath(context.Candidate, syntheticArtifact)),
                context.CancellationToken);
            if (!candidateState.TryApplyExpectedWriteOutcome(
                    context.Candidate.MutableState,
                    expectedArtifact,
                    writeResult,
                    out var errorSummary))
            {
                context.Logger.LogWarning(
                    "Workspace-written artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}. Errors: {Errors}",
                    context.Candidate.RunId,
                    context.Candidate.Step.Id,
                    expectedArtifact.Title,
                    errorSummary);
            }
        }
    }
}
