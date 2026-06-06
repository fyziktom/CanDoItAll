using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using System.Text;


namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessMockArtifactProjectionCoordinator : IProcessArtifactProjectionSourceCoordinator
{
    private readonly IProcessProjectionPathResolver pathResolver;
    private readonly IProcessProjectionFileIo fileIo;
    private readonly IProcessProjectionArtifactClassifier artifactClassifier;
    private readonly IProcessProjectionProcessMockRules processMockRules;
    private readonly IProcessProjectionCandidateStateUpdater candidateState;

    public ProcessMockArtifactProjectionCoordinator(
        IProcessProjectionPathResolver pathResolver,
        IProcessProjectionFileIo fileIo,
        IProcessProjectionArtifactClassifier artifactClassifier,
        IProcessProjectionProcessMockRules processMockRules,
        IProcessProjectionCandidateStateUpdater candidateState)
    {
        this.pathResolver = pathResolver;
        this.fileIo = fileIo;
        this.artifactClassifier = artifactClassifier;
        this.processMockRules = processMockRules;
        this.candidateState = candidateState;
    }

    public async Task ProjectAsync(ProcessArtifactProjectionContext context)
    {
        if (context.Candidate.ExpectedArtifacts.Count == 0)
        {
            return;
        }

        var projections = processMockRules.ResolveProcessMockArtifactProjections(context.Run.SerializedSessionStateJson);
        if (projections.Count == 0)
        {
            return;
        }

        var projectedExpectationIds = new HashSet<Guid>();
        foreach (var projection in projections)
        {
            var matchedExpectations = context.Candidate.ExpectedArtifacts
                .Where(item => item.IsRequired && !projectedExpectationIds.Contains(item.Id))
                .Where(item => processMockRules.ProcessMockArtifactMatchesExpectation(item, projection))
                .ToList();
            if (matchedExpectations.Count == 0)
            {
                continue;
            }

            if (matchedExpectations.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Process mock artifact '{projection.RelativePath}' for role '{projection.RoleKey}' matched multiple required artifact expectations for step '{context.Candidate.Step.Title}': {string.Join(", ", matchedExpectations.Select(item => item.Title))}.");
            }

            var expectedArtifact = matchedExpectations[0];
            var expectedProjection = expectedArtifact;
            var scopedRelativePath = pathResolver.ResolveScopedManagedRelativePath(context.WorkspaceScope, projection.RelativePath);
            var projectionSource = new ProcessMockArtifactProjectionSource(
                context.Candidate.Step.Id,
                context.Run.Id,
                projection.RelativePath,
                scopedRelativePath,
                projection.RoleKey);
            var projectionPlan = ProcessMockArtifactProjectionSourceAdapter.Plan(
                projectionSource,
                expectedProjection,
                context.CompletionStatus,
                context.RecoveryContext);
            if (context.Candidate.MutableState.ExternalReferenceKeys.Contains(projectionPlan.ExternalReferenceKey))
            {
                projectedExpectationIds.Add(expectedArtifact.Id);
                continue;
            }

            if (!pathResolver.TryResolveArtifactFullPath(context.WorkspaceRoot, scopedRelativePath, out var fullPath, out var pathResolutionFailure) ||
                !fileIo.FileExists(fullPath))
            {
                throw new InvalidOperationException(
                    $"Process mock artifact '{projection.RelativePath}' for expected artifact '{expectedArtifact.Title}' was declared by execution run {context.Run.Id:D}, but scoped path '{scopedRelativePath}' could not be found. {pathResolutionFailure}".Trim());
            }

            byte[] content;
            try
            {
                content = await fileIo.ReadAllBytesAsync(fullPath, context.CancellationToken);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Process mock artifact '{projection.RelativePath}' for expected artifact '{expectedArtifact.Title}' at scoped path '{scopedRelativePath}' could not be read: {exception.Message}",
                    exception);
            }

            var contentType = artifactClassifier.GuessContentTypeFromPath(fullPath);
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
                    scopedRelativePath),
                context.CancellationToken);
            if (!candidateState.TryApplyExpectedWriteOutcome(
                    context.Candidate.MutableState,
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
