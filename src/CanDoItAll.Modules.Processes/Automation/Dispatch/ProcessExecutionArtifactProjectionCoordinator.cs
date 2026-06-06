using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using System.Text;


namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessExecutionArtifactProjectionCoordinator : IProcessArtifactProjectionSourceCoordinator
{
    private readonly IProcessProjectionClaimGuard claimGuard;
    private readonly IProcessProjectionPathResolver pathResolver;
    private readonly IProcessProjectionFileIo fileIo;
    private readonly IProcessProjectionArtifactClassifier artifactClassifier;
    private readonly IProcessProjectionExpectationMatcher expectationMatcher;
    private readonly IProcessProjectionCandidateStateUpdater candidateState;

    public ProcessExecutionArtifactProjectionCoordinator(
        IProcessProjectionClaimGuard claimGuard,
        IProcessProjectionPathResolver pathResolver,
        IProcessProjectionFileIo fileIo,
        IProcessProjectionArtifactClassifier artifactClassifier,
        IProcessProjectionExpectationMatcher expectationMatcher,
        IProcessProjectionCandidateStateUpdater candidateState)
    {
        this.claimGuard = claimGuard;
        this.pathResolver = pathResolver;
        this.fileIo = fileIo;
        this.artifactClassifier = artifactClassifier;
        this.expectationMatcher = expectationMatcher;
        this.candidateState = candidateState;
    }

    public async Task ProjectAsync(ProcessArtifactProjectionContext context)
    {
        foreach (var artifact in context.Run.Artifacts)
        {
            await claimGuard.EnsureStepDispatchClaimHeldAsync(context.DispatchClaim, context.CancellationToken);
            if (artifactClassifier.IsTransientExecutionArtifact(artifact))
            {
                context.Logger.LogDebug(
                    "Skipping transient execution artifact projection for run {RunId}, step {StepRunId}, artifact {ArtifactId}, path {RelativePath}.",
                    context.Candidate.RunId,
                    context.Candidate.Step.Id,
                    artifact.Id,
                    artifact.RelativePath);
                continue;
            }

            var sourceExternalReferenceKey = ProcessArtifactProjectionPlanner.BuildExecutionArtifactExternalReferenceKey(artifact.Id);
            var externalReferenceKey = ProcessArtifactProjectionLineageBuilder.ApplyRecoveryLineage(
                sourceExternalReferenceKey,
                context.Run.Id,
                context.RecoveryContext);
            if (context.Candidate.MutableState.ExternalReferenceKeys.Contains(externalReferenceKey))
            {
                continue;
            }

            if (!pathResolver.TryResolveArtifactFullPath(context.WorkspaceRoot, artifact.RelativePath, out var fullPath, out var pathResolutionFailure) ||
                !fileIo.FileExists(fullPath))
            {
                context.Logger.LogDebug(
                    "Skipping execution artifact projection for run {RunId}, step {StepRunId}, artifact {ArtifactId} because the file path is unavailable. Reason: {Reason}",
                    context.Candidate.RunId,
                    context.Candidate.Step.Id,
                    artifact.Id,
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
                    "Execution artifact {ArtifactId} could not be read for process run {RunId}.",
                    artifact.Id,
                    context.Candidate.RunId);
                continue;
            }

            var matchedExpectation = expectationMatcher.ResolveArtifactExpectation(
                context.Candidate,
                context.Run.InputSummary,
                artifact,
                artifactClassifier.TryDecodeTextArtifactContent(artifact, fullPath, content));
            var projectionPlan = ProcessArtifactProjectionPlanner.PlanExecutionArtifact(
                context.Run.Id,
                artifact,
                matchedExpectation is null ? null : matchedExpectation,
                artifactClassifier.ResolveProcessArtifactKind(context.Candidate, artifact),
                context.CompletionStatus,
                context.Run.ResultSummary,
                context.RecoveryContext);

            var writeResult = await context.WriteCoordinator.WriteAsync(
                new ProcessArtifactProjectionWriteRequest(
                    context.Candidate.RunId,
                    context.Candidate.Step.Id,
                    context.Candidate.ProjectId,
                    projectionPlan,
                    Path.GetFileName(fullPath),
                    string.IsNullOrWhiteSpace(artifact.ContentType)
                        ? "application/octet-stream"
                        : artifact.ContentType,
                    content,
                    artifactClassifier.ResolveStorageContentKind(artifact.ContentType, fullPath),
                    artifactClassifier.BuildStorageRelativePath(context.Candidate, artifact)),
                context.CancellationToken);
            if (!candidateState.TryApplyWriteOutcome(
                    context.Candidate.MutableState,
                    writeResult,
                    projectionPlan.ArtifactExpectationId,
                    out var errorSummary))
            {
                context.Logger.LogWarning(
                    "Process artifact projection failed for run {RunId}, step {StepRunId}, artifact {ArtifactId}. Errors: {Errors}",
                    context.Candidate.RunId,
                    context.Candidate.Step.Id,
                    artifact.Id,
                    errorSummary);
            }
        }
    }
}
