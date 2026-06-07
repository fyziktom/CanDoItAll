using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessSubprocessProjectionPersistenceService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IWorkspacePathResolver workspacePathResolver,
    IDatabaseProfileRuntimeAccessor databaseProfileRuntimeAccessor,
    IClock clock,
    Func<ProcessRouteDispatchClaim, CancellationToken, Task> ensureStepDispatchClaimHeldAsync)
{
    public async Task ProjectCompletedArtifactsAsync(
        ProcessDispatchSubprocessRuntimeInput input,
        ProcessSubprocessRunStartResult subprocessRun,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(subprocessRun);

        await ensureStepDispatchClaimHeldAsync(input.DispatchClaim, cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var expectations = await dbContext.Set<ProcessArtifactExpectation>()
            .Where(item =>
                item.StepDefinitionId == input.StepRun.StepDefinitionId &&
                item.IsRequired)
            .OrderBy(item => item.Title)
            .ToListAsync(cancellationToken);
        if (expectations.Count == 0)
        {
            return;
        }

        var parentArtifacts = await dbContext.Set<ProcessArtifactRecord>()
            .Where(item =>
                item.ProcessRunId == input.Run.Id &&
                item.StepRunId == input.StepRun.Id)
            .ToListAsync(cancellationToken);
        var missingProjectableExpectations = expectations
            .Where(ProcessSubprocessArtifactSourceResolver.IsCompletionProjectionAllowed)
            .Where(expectation => !parentArtifacts.Any(artifact =>
                ProcessSubprocessProjectionPlanBuilder.SatisfiesCurrentArtifactExpectation(
                    artifact,
                    expectation,
                    subprocessRun.RunId)))
            .ToList();
        if (missingProjectableExpectations.Count == 0)
        {
            return;
        }

        var childArtifacts = await dbContext.Set<ProcessArtifactRecord>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == subprocessRun.RunId)
            .ToListAsync(cancellationToken);
        childArtifacts = childArtifacts
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
        var now = clock.GetUtcNow();
        var scopedProfileId = databaseProfileRuntimeAccessor.ResolveCurrentProfile().Profile.Id.ToString("N");
        var gapJournalCoordinator = new ProcessSubprocessProjectionGapJournalCoordinator();
        var projectionWriterCoordinator = new ProcessSubprocessProjectionWriterCoordinator(workspacePathResolver);

        foreach (var expectation in missingProjectableExpectations)
        {
            await ensureStepDispatchClaimHeldAsync(input.DispatchClaim, cancellationToken);
            var sourceArtifact = ProcessSubprocessArtifactSourceResolver.ResolveSourceArtifact(
                childArtifacts,
                missingProjectableExpectations,
                expectation,
                out var projectionDiagnostic);
            if (sourceArtifact is null)
            {
                await gapJournalCoordinator.RecordAsync(
                    dbContext,
                    input,
                    subprocessRun,
                    expectation,
                    projectionDiagnostic,
                    now,
                    cancellationToken);
                continue;
            }

            var projectionPlan = ProcessSubprocessProjectionPlanBuilder.Build(
                input,
                subprocessRun,
                expectation,
                sourceArtifact,
                projectionDiagnostic,
                scopedProfileId);
            await projectionWriterCoordinator.WriteAsync(
                dbContext,
                input,
                subprocessRun,
                projectionPlan,
                now,
                cancellationToken);
        }

        await ensureStepDispatchClaimHeldAsync(input.DispatchClaim, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
