using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using System.Text;

using DispatchArtifactExpectation = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.DispatchArtifactExpectation;
using ProcessMockArtifactProjection = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.ProcessMockArtifactProjection;
using SessionFileContent = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.SessionFileContent;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessMockArtifactProjectionCoordinator : IProcessArtifactProjectionSourceCoordinator
{
    private readonly IProcessArtifactProjectionHost host;

    public ProcessMockArtifactProjectionCoordinator(IProcessArtifactProjectionHost host)
    {
        this.host = host;
    }

    public async Task ProjectAsync(ProcessArtifactProjectionContext context)
    {
        if (context.Candidate.ExpectedArtifacts.Count == 0)
        {
            return;
        }

        var projections = host.ResolveProcessMockArtifactProjections(context.Detail.Run.SerializedSessionStateJson);
        if (projections.Count == 0)
        {
            return;
        }

        var projectedExpectationIds = new HashSet<Guid>();
        foreach (var projection in projections)
        {
            var matchedExpectations = context.Candidate.ExpectedArtifacts
                .Where(item => item.IsRequired && !projectedExpectationIds.Contains(item.Id))
                .Where(item => host.ProcessMockArtifactMatchesExpectation(item, projection))
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
            var scopedRelativePath = host.ResolveScopedManagedRelativePath(context.WorkspaceScope, projection.RelativePath);
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

            if (!host.TryResolveArtifactFullPath(context.WorkspaceRoot, scopedRelativePath, out var fullPath, out var pathResolutionFailure) ||
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

            var contentType = host.GuessContentTypeFromPath(fullPath);
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
