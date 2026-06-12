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

        var childRun = await dbContext.Set<ProcessRun>()
            .AsNoTracking()
            .Where(item => item.Id == subprocessRun.RunId)
            .Select(item => new
            {
                item.Id,
                item.ProcessDefinitionVersionId
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (childRun is null)
        {
            return;
        }

        var childArtifacts = await dbContext.Set<ProcessArtifactRecord>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == subprocessRun.RunId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var effectiveExpectations = await ResolveEffectiveProjectionExpectationsAsync(
            dbContext,
            expectations,
            childArtifacts,
            childRun.ProcessDefinitionVersionId,
            cancellationToken);

        var parentArtifacts = await dbContext.Set<ProcessArtifactRecord>()
            .Where(item =>
                item.ProcessRunId == input.Run.Id &&
                item.StepRunId == input.StepRun.Id)
            .ToListAsync(cancellationToken);
        var missingProjectableExpectations = effectiveExpectations
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

    internal static async Task<IReadOnlyList<ProcessArtifactExpectation>> ResolveEffectiveProjectionExpectationsAsync(
        AppDbContext dbContext,
        IReadOnlyList<ProcessArtifactExpectation> parentExpectations,
        IReadOnlyCollection<ProcessArtifactRecord> childArtifacts,
        Guid childDefinitionVersionId,
        CancellationToken cancellationToken)
    {
        var childArtifactExpectationIds = childArtifacts
            .Select(item => item.ArtifactExpectationId)
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .ToHashSet();
        var mappedExpectationIds = parentExpectations
            .Select(item => item.SubprocessChildArtifactExpectationId)
            .Where(item => item.HasValue && item.Value != Guid.Empty)
            .Select(item => item!.Value)
            .Distinct()
            .ToList();

        var originalTargets = mappedExpectationIds.Count == 0
            ? new Dictionary<Guid, SubprocessChildArtifactTarget>()
            : await (
                    from artifact in dbContext.Set<ProcessArtifactExpectation>().AsNoTracking()
                    join step in dbContext.Set<ProcessStepDefinition>().AsNoTracking()
                        on artifact.StepDefinitionId equals step.Id
                    where mappedExpectationIds.Contains(artifact.Id)
                    select new SubprocessChildArtifactTarget(
                        artifact.Id,
                        step.Key,
                        artifact.Title))
                .ToDictionaryAsync(item => item.ArtifactExpectationId, cancellationToken);
        var childTargets = await (
                from step in dbContext.Set<ProcessStepDefinition>().AsNoTracking()
                join artifact in dbContext.Set<ProcessArtifactExpectation>().AsNoTracking()
                    on step.Id equals artifact.StepDefinitionId
                where step.ProcessDefinitionVersionId == childDefinitionVersionId
                select new SubprocessChildArtifactTarget(
                    artifact.Id,
                    step.Key,
                    artifact.Title))
            .ToListAsync(cancellationToken);

        return parentExpectations
            .Select(expectation => ResolveEffectiveProjectionExpectation(
                expectation,
                childArtifactExpectationIds,
                originalTargets,
                childTargets))
            .ToList();
    }

    private static ProcessArtifactExpectation ResolveEffectiveProjectionExpectation(
        ProcessArtifactExpectation expectation,
        IReadOnlySet<Guid> childArtifactExpectationIds,
        IReadOnlyDictionary<Guid, SubprocessChildArtifactTarget> originalTargets,
        IReadOnlyList<SubprocessChildArtifactTarget> childTargets)
    {
        if (expectation.SubprocessChildArtifactExpectationId is { } mappedExpectationId &&
            mappedExpectationId != Guid.Empty &&
            childArtifactExpectationIds.Contains(mappedExpectationId))
        {
            return expectation;
        }

        var stableTarget = ResolveStableTarget(expectation, originalTargets);
        if (stableTarget is null)
        {
            return expectation;
        }

        var matches = childTargets
            .Where(target =>
                string.Equals(target.StepKey, stableTarget.StepKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(target.ArtifactTitle, stableTarget.ArtifactTitle, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return matches.Count == 1
            ? CloneWithMappedSubprocessChildExpectation(expectation, matches[0])
            : expectation;
    }

    private static SubprocessChildArtifactTarget? ResolveStableTarget(
        ProcessArtifactExpectation expectation,
        IReadOnlyDictionary<Guid, SubprocessChildArtifactTarget> originalTargets)
    {
        if (!string.IsNullOrWhiteSpace(expectation.SubprocessChildStepKey) &&
            !string.IsNullOrWhiteSpace(expectation.SubprocessChildArtifactTitle))
        {
            return new SubprocessChildArtifactTarget(
                expectation.SubprocessChildArtifactExpectationId ?? Guid.Empty,
                expectation.SubprocessChildStepKey.Trim(),
                expectation.SubprocessChildArtifactTitle.Trim());
        }

        return expectation.SubprocessChildArtifactExpectationId is { } mappedExpectationId &&
               originalTargets.TryGetValue(mappedExpectationId, out var originalTarget)
            ? originalTarget
            : null;
    }

    private static ProcessArtifactExpectation CloneWithMappedSubprocessChildExpectation(
        ProcessArtifactExpectation expectation,
        SubprocessChildArtifactTarget target)
    {
        return new ProcessArtifactExpectation
        {
            Id = expectation.Id,
            StepDefinitionId = expectation.StepDefinitionId,
            ArtifactKind = expectation.ArtifactKind,
            Title = expectation.Title,
            IsRequired = expectation.IsRequired,
            TrustRequirement = expectation.TrustRequirement,
            SensitivityLevel = expectation.SensitivityLevel,
            RetentionDays = expectation.RetentionDays,
            AllowedFutureUsageSummary = expectation.AllowedFutureUsageSummary,
            ValidationRequirementSummary = expectation.ValidationRequirementSummary,
            WorkflowOutputId = expectation.WorkflowOutputId,
            WorkflowOutputName = expectation.WorkflowOutputName,
            WorkflowOutputKind = expectation.WorkflowOutputKind,
            SubprocessChildArtifactExpectationId = target.ArtifactExpectationId,
            SubprocessChildStepKey = target.StepKey,
            SubprocessChildArtifactTitle = target.ArtifactTitle
        };
    }

    private sealed record SubprocessChildArtifactTarget(
        Guid ArtifactExpectationId,
        string StepKey,
        string ArtifactTitle);
}
