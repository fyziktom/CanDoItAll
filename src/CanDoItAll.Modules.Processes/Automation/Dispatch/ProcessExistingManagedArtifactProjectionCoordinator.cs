using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using System.Text;


namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessExistingManagedArtifactProjectionCoordinator : IProcessArtifactProjectionSourceCoordinator
{
    private readonly IProcessProjectionPathResolver pathResolver;
    private readonly IProcessProjectionFileIo fileIo;
    private readonly IProcessProjectionArtifactClassifier artifactClassifier;
    private readonly IProcessProjectionExpectationMatcher expectationMatcher;
    private readonly IProcessProjectionCandidateStateUpdater candidateState;

    public ProcessExistingManagedArtifactProjectionCoordinator(
        IProcessProjectionPathResolver pathResolver,
        IProcessProjectionFileIo fileIo,
        IProcessProjectionArtifactClassifier artifactClassifier,
        IProcessProjectionExpectationMatcher expectationMatcher,
        IProcessProjectionCandidateStateUpdater candidateState)
    {
        this.pathResolver = pathResolver;
        this.fileIo = fileIo;
        this.artifactClassifier = artifactClassifier;
        this.expectationMatcher = expectationMatcher;
        this.candidateState = candidateState;
    }

    public async Task ProjectAsync(ProcessArtifactProjectionContext context)
    {
        if (context.Candidate.ExpectedArtifacts.Count == 0)
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

            var projectedRelativePath = pathResolver.ResolveExpectedManagedArtifactRelativePaths(
                    context.Candidate,
                    context.WorkspaceScope,
                    expectedArtifact)
                .FirstOrDefault(relativePath => expectationMatcher.ExistingManagedArtifactFileMatches(
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
                $"Projected from existing managed workspace artifact '{projectedRelativePath}' for AgentFramework execution run {context.Run.Id:D}.");
        }
    }

    public async Task<bool> TryRecordForResponseProjectionAsync(
        ProcessArtifactProjectionContext context,
        ProcessProjectionArtifactExpectation expectedArtifact,
        string projectedRelativePath,
        string targetFullPath)
    {
        if (!expectationMatcher.ExistingManagedArtifactFileMatches(
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
            $"Projected from existing managed workspace artifact '{projectedRelativePath}' for AgentFramework execution run {context.Run.Id:D}.",
            targetFullPath);
    }

    private async Task<bool> RecordExistingManagedArtifactAsync(
        ProcessArtifactProjectionContext context,
        ProcessProjectionArtifactExpectation expectedArtifact,
        string projectedRelativePath,
        string logSourceName,
        string artifactSummary,
        string? knownFullPath = null)
    {
        var expectedProjection = expectedArtifact;
        var projectionSource = new ExistingManagedArtifactProjectionSource(
            context.Run.Id,
            projectedRelativePath);
        var externalReferenceKey = ExistingManagedArtifactProjectionSourceAdapter.BuildExternalReferenceKey(
            projectionSource,
            expectedProjection,
            context.RecoveryContext);
        if (context.Candidate.MutableState.ExternalReferenceKeys.Contains(externalReferenceKey))
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
        else if (!pathResolver.TryResolveArtifactFullPath(context.WorkspaceRoot, projectedRelativePath, out fullPath, out pathResolutionFailure) ||
                 !fileIo.FileExists(fullPath))
        {
            context.Logger.LogDebug(
                "Skipping existing managed artifact projection for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle} because path '{RelativePath}' is unavailable. Reason: {Reason}",
                context.Candidate.RunId,
                context.Candidate.Step.Id,
                expectedArtifact.Title,
                projectedRelativePath,
                string.IsNullOrWhiteSpace(pathResolutionFailure) ? "File does not exist." : pathResolutionFailure);
            return false;
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
                "Existing managed artifact '{ArtifactTitle}' could not be read for process run {RunId}.",
                expectedArtifact.Title,
                context.Candidate.RunId);
            return false;
        }

        var contentType = artifactClassifier.GuessContentTypeFromPath(fullPath);
        var syntheticArtifact = new ProcessAutomationExecutionArtifact(
            Guid.NewGuid(),
            context.Run.Id,
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
                context.Candidate.RunId,
                context.Candidate.Step.Id,
                context.Candidate.ProjectId,
                projectionPlan,
                Path.GetFileName(fullPath),
                    contentType,
                    content,
                    artifactClassifier.ResolveStorageContentKind(contentType, fullPath),
                    artifactClassifier.BuildStorageRelativePath(context.Candidate, syntheticArtifact)),
            context.CancellationToken);
        if (candidateState.TryApplyExpectedWriteOutcome(
                context.Candidate.MutableState,
                expectedArtifact,
                writeResult,
                out var errorSummary))
        {
            return true;
        }

        context.Logger.LogWarning(
            "{SourceName} projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}. Errors: {Errors}",
            logSourceName,
            context.Candidate.RunId,
            context.Candidate.Step.Id,
            expectedArtifact.Title,
            errorSummary);
        return false;
    }
}
