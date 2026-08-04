using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

internal sealed record DeleteSubtreeMutationPayload(
    string RootNodeKey,
    IReadOnlyList<string> DeletedNodeKeys,
    int LinkCount,
    IReadOnlyList<StorageObjectReference>? ManagedStorageObjects = null,
    IReadOnlyList<ProjectManagedStorageDeletionOutcome>? ManagedStorageOutcomes = null,
    IReadOnlyList<ProjectManagedStorageDeletionCandidate>? ManagedStorageCandidates = null);

internal sealed record DeleteProjectMutationPayload(
    IReadOnlyList<string> DeletedNodeKeys,
    IReadOnlyList<StorageObjectReference> ManagedStorageObjects,
    IReadOnlyList<ProjectManagedStorageDeletionOutcome>? ManagedStorageOutcomes = null,
    IReadOnlyList<Guid>? OutstandingMutationIds = null,
    IReadOnlyList<ProjectManagedStorageDeletionCandidate>? ManagedStorageCandidates = null);

internal sealed record MoveDescendantsMutationPayload(
    Guid SourceProjectId,
    Guid TargetProjectId,
    string SourceNodeKey,
    IReadOnlyList<string> MovedNodeKeys,
    IReadOnlyList<string> MovedRootKeys);

public sealed record ProjectCrossModuleMutationProcessingOptions(
    TimeSpan LeaseDuration,
    TimeSpan HeartbeatInterval,
    TimeSpan FailurePersistenceTimeout)
{
    public static ProjectCrossModuleMutationProcessingOptions Default { get; } = new(
        TimeSpan.FromMinutes(5),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(5));

    public ProjectCrossModuleMutationProcessingOptions Validate()
    {
        if (LeaseDuration <= TimeSpan.Zero ||
            HeartbeatInterval <= TimeSpan.Zero ||
            FailurePersistenceTimeout <= TimeSpan.Zero ||
            HeartbeatInterval >= LeaseDuration)
        {
            throw new InvalidOperationException(
                "Durable mutation processing requires positive timing values and a heartbeat interval shorter than the claim lease.");
        }

        return this;
    }
}

public sealed class ProjectCrossModuleMutationProcessor(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IProjectPartyIntegrationBridge projectPartyIntegrationBridge,
    ProjectManagedStorageDeletionService managedStorageDeletionService,
    ProjectCrossModuleMutationCoordinator mutationCoordinator,
    IClock clock,
    ProjectCrossModuleMutationProcessingOptions processingOptions,
    TimeProvider timeProvider,
    ILogger<ProjectCrossModuleMutationProcessor> logger)
{
    private const int ProcessingLockStripeCount = 64;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly SemaphoreSlim[] ProcessingLockStripes = Enumerable
        .Range(0, ProcessingLockStripeCount)
        .Select(static _ => new SemaphoreSlim(1, 1))
        .ToArray();
    private readonly ProjectCrossModuleMutationProcessingOptions processingOptions =
        processingOptions.Validate();

    internal async Task<ProjectCrossModuleMutationStatus?> ProcessAsync(
        Guid mutationId,
        CancellationToken cancellationToken = default)
    {
        var processingLock = ResolveProcessingLock(mutationId);
        await processingLock.WaitAsync(cancellationToken);
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
            var current = await dbContext.Set<ProjectCrossModuleMutationRecord>()
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == mutationId, cancellationToken);
            if (current is null)
            {
                return null;
            }

            if (current.Status == ProjectCrossModuleMutationStatus.Completed)
            {
                return current.Status;
            }

            if (current.ApprovalState is ProjectCrossModuleMutationApprovalState.Pending or
                ProjectCrossModuleMutationApprovalState.Rejected)
            {
                return current.Status;
            }

            var claimToken = $"processing:{Guid.NewGuid():N}";
            if (!await TryClaimAsync(dbContext, mutationId, claimToken, cancellationToken))
            {
                var status = await dbContext.Set<ProjectCrossModuleMutationRecord>()
                    .AsNoTracking()
                    .Where(item => item.Id == mutationId)
                    .Select(item => (ProjectCrossModuleMutationStatus?)item.Status)
                    .FirstOrDefaultAsync(cancellationToken);
                return status;
            }

            var mutation = await dbContext.Set<ProjectCrossModuleMutationRecord>()
                .AsNoTracking()
                .SingleAsync(item => item.Id == mutationId, cancellationToken);
            using var heartbeatStop = new CancellationTokenSource();
            using var processingCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var heartbeatTask = RunClaimHeartbeatAsync(
                mutationId,
                claimToken,
                heartbeatStop.Token,
                processingCancellation);
            try
            {
                await ExecuteCommittedMutationAsync(
                    dbContext,
                    mutation,
                    claimToken,
                    processingCancellation.Token);
                await StopHeartbeatAsync(heartbeatStop, heartbeatTask, suppressFailure: false);
                return await CompleteClaimAsync(
                    dbContext,
                    mutationId,
                    claimToken,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await StopHeartbeatAsync(heartbeatStop, heartbeatTask, suppressFailure: true);
                logger.LogWarning(
                    "Durable Workbench mutation {MutationId} processing was canceled.",
                    mutationId);
                using var failureTimeout = new CancellationTokenSource(
                    processingOptions.FailurePersistenceTimeout);
                var failedByOwner = await FailClaimAsync(
                    dbContext,
                    mutationId,
                    claimToken,
                    "Durable reconciliation was canceled before completion.",
                    failureTimeout.Token);
                if (!failedByOwner)
                {
                    _ = await LoadStatusAsync(mutationId, failureTimeout.Token);
                }
                throw;
            }
            catch (Exception exception)
            {
                await StopHeartbeatAsync(heartbeatStop, heartbeatTask, suppressFailure: true);
                logger.LogWarning(
                    "Durable Workbench mutation {MutationId} failed with {FailureType}.",
                    mutationId,
                    exception.GetType().Name);
                using var failureTimeout = new CancellationTokenSource(
                    processingOptions.FailurePersistenceTimeout);
                var failedByOwner = await FailClaimAsync(
                    dbContext,
                    mutationId,
                    claimToken,
                    $"Durable reconciliation failed ({exception.GetType().Name}). Retry is required.",
                    failureTimeout.Token);
                return failedByOwner
                    ? ProjectCrossModuleMutationStatus.Failed
                    : await LoadStatusAsync(mutationId, failureTimeout.Token)
                      ?? ProjectCrossModuleMutationStatus.Failed;
            }
        }
        finally
        {
            processingLock.Release();
        }
    }

    internal async Task<bool> TryClaimAsync(
        AppDbContext dbContext,
        Guid mutationId,
        string claimToken,
        CancellationToken cancellationToken)
    {
        var now = await ProjectCrossModuleMutationTimeSource.GetUtcNowAsync(
            dbContext,
            clock,
            cancellationToken);
        var staleBefore = now - processingOptions.LeaseDuration;
        if (dbContext.Database.IsRelational())
        {
            var affected = await dbContext.Set<ProjectCrossModuleMutationRecord>()
                .Where(record =>
                    record.Id == mutationId &&
                    record.ApprovalState != ProjectCrossModuleMutationApprovalState.Pending &&
                    record.ApprovalState != ProjectCrossModuleMutationApprovalState.Rejected &&
                    (record.Status == ProjectCrossModuleMutationStatus.WorkbenchCommitted ||
                     record.Status == ProjectCrossModuleMutationStatus.Failed ||
                     record.Status == ProjectCrossModuleMutationStatus.Processing &&
                     (!record.LastAttemptAtUtc.HasValue ||
                      record.LastAttemptAtUtc < staleBefore)))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(record => record.Status, ProjectCrossModuleMutationStatus.Processing)
                    .SetProperty(record => record.ErrorMessage, claimToken)
                    .SetProperty(record => record.AttemptCount, record => record.AttemptCount + 1)
                    .SetProperty(record => record.LastAttemptAtUtc, now)
                    .SetProperty(record => record.UpdatedAtUtc, now),
                    cancellationToken);
            return affected == 1;
        }

        var mutation = await dbContext.Set<ProjectCrossModuleMutationRecord>()
            .SingleAsync(record => record.Id == mutationId, cancellationToken);
        var claimable = mutation.Status is ProjectCrossModuleMutationStatus.WorkbenchCommitted or
            ProjectCrossModuleMutationStatus.Failed ||
            mutation.Status == ProjectCrossModuleMutationStatus.Processing &&
            (!mutation.LastAttemptAtUtc.HasValue ||
             mutation.LastAttemptAtUtc < staleBefore);
        if (!claimable)
        {
            return false;
        }

        mutationCoordinator.MarkAttempt(mutation);
        mutation.Status = ProjectCrossModuleMutationStatus.Processing;
        mutation.ErrorMessage = claimToken;
        mutation.LastAttemptAtUtc = now;
        mutation.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        return true;
    }

    private Task ExecuteCommittedMutationAsync(
        AppDbContext dbContext,
        ProjectCrossModuleMutationRecord mutation,
        string claimToken,
        CancellationToken cancellationToken)
    {
        return mutation.MutationKind switch
        {
            ProjectCrossModuleMutationKind.DeleteSubtree => DeleteSubtreeAsync(
                dbContext,
                mutation,
                claimToken,
                Deserialize<DeleteSubtreeMutationPayload>(mutation.PayloadJson),
                cancellationToken),
            ProjectCrossModuleMutationKind.DeleteProject => DeleteProjectAsync(
                dbContext,
                mutation,
                claimToken,
                Deserialize<DeleteProjectMutationPayload>(mutation.PayloadJson),
                cancellationToken),
            ProjectCrossModuleMutationKind.MoveDescendants => MoveAssignmentsAsync(
                new ProjectPartyAssignmentMoveOperationId(mutation.Id),
                Deserialize<MoveDescendantsMutationPayload>(mutation.PayloadJson),
                cancellationToken),
            ProjectCrossModuleMutationKind.MoveSelectedNodes => MoveAssignmentsAsync(
                new ProjectPartyAssignmentMoveOperationId(mutation.Id),
                Deserialize<MoveDescendantsMutationPayload>(mutation.PayloadJson),
                cancellationToken),
            _ => throw new InvalidOperationException(
                $"Unsupported durable mutation kind '{mutation.MutationKind}'.")
        };
    }

    private async Task DeleteSubtreeAsync(
        AppDbContext dbContext,
        ProjectCrossModuleMutationRecord mutation,
        string claimToken,
        DeleteSubtreeMutationPayload payload,
        CancellationToken cancellationToken)
    {
        await DeleteAssignmentsAsync(mutation.ProjectId, payload.DeletedNodeKeys, cancellationToken);
        await DeleteStorageObjectsAsync(
            dbContext,
            mutation,
            claimToken,
            ResolveDeletionCandidates(
                payload.ManagedStorageCandidates,
                payload.ManagedStorageObjects),
            payload.ManagedStorageOutcomes ?? [],
            outcomes => JsonSerializer.Serialize(
                payload with { ManagedStorageOutcomes = outcomes },
                JsonOptions),
            cancellationToken);
    }

    private async Task DeleteProjectAsync(
        AppDbContext dbContext,
        ProjectCrossModuleMutationRecord mutation,
        string claimToken,
        DeleteProjectMutationPayload payload,
        CancellationToken cancellationToken)
    {
        await projectPartyIntegrationBridge.DeleteAssignmentsForProjectAsync(
            mutation.ProjectId,
            cancellationToken);
        await DeleteStorageObjectsAsync(
            dbContext,
            mutation,
            claimToken,
            ResolveDeletionCandidates(
                payload.ManagedStorageCandidates,
                payload.ManagedStorageObjects),
            payload.ManagedStorageOutcomes ?? [],
            outcomes => JsonSerializer.Serialize(
                payload with { ManagedStorageOutcomes = outcomes },
                JsonOptions),
            cancellationToken);
    }

    private async Task DeleteStorageObjectsAsync(
        AppDbContext dbContext,
        ProjectCrossModuleMutationRecord mutation,
        string claimToken,
        IReadOnlyCollection<ProjectManagedStorageDeletionCandidate> candidates,
        IReadOnlyCollection<ProjectManagedStorageDeletionOutcome> persistedOutcomes,
        Func<IReadOnlyList<ProjectManagedStorageDeletionOutcome>, string> serializePayload,
        CancellationToken cancellationToken)
    {
        var undefinedOutcome = persistedOutcomes.FirstOrDefault(outcome =>
            !Enum.IsDefined(outcome.Kind));
        if (undefinedOutcome is not null)
        {
            throw new InvalidDataException(
                $"Durable managed-storage outcome kind '{undefinedOutcome.Kind}' is not supported.");
        }

        var outcomes = persistedOutcomes
            .GroupBy(outcome => ProjectManagedStorageObjectKey.FromReference(outcome.Reference))
            .Select(group => group.First())
            .ToList();
        var completedKeys = outcomes
            .Select(outcome => ProjectManagedStorageObjectKey.FromReference(outcome.Reference))
            .ToHashSet();
        foreach (var candidate in candidates
                     .GroupBy(item => ProjectManagedStorageObjectKey.FromReference(item.Reference))
                     .Select(group => group.First()))
        {
            var reference = candidate.Reference;
            var key = ProjectManagedStorageObjectKey.FromReference(reference);
            if (completedKeys.Contains(key))
            {
                continue;
            }

            await RenewClaimAsync(dbContext, mutation.Id, claimToken, cancellationToken);
            var outcome = (await managedStorageDeletionService.DeleteAsync(
                [candidate],
                cancellationToken)).Single();
            outcomes.Add(outcome);
            completedKeys.Add(key);
            mutation.PayloadJson = serializePayload(outcomes);
            await PersistPayloadCheckpointAsync(
                dbContext,
                mutation.Id,
                claimToken,
                mutation.PayloadJson,
                cancellationToken);
        }
    }

    private static IReadOnlyList<ProjectManagedStorageDeletionCandidate> ResolveDeletionCandidates(
        IReadOnlyList<ProjectManagedStorageDeletionCandidate>? candidates,
        IReadOnlyList<StorageObjectReference>? legacyReferences)
    {
        if (candidates is not null)
        {
            return candidates;
        }

        return (legacyReferences ?? [])
            .Select(reference => new ProjectManagedStorageDeletionCandidate(
                reference,
                reference.ProviderKind == StorageProviderKind.Ipfs
                    ? ProjectManagedStorageOwnershipBasis.ImmutableContentAddress
                    : ProjectManagedStorageProvenancePolicy.HasManagedMarker(reference)
                        ? ProjectManagedStorageOwnershipBasis.CreationProvenanceV2
                        : ProjectManagedStorageOwnershipBasis.UnverifiedLegacyPayload,
                string.Empty,
                string.Empty))
            .ToArray();
    }

    private Task DeleteAssignmentsAsync(
        Guid projectId,
        IReadOnlyList<string> deletedNodeKeys,
        CancellationToken cancellationToken)
    {
        return projectPartyIntegrationBridge.DeleteAssignmentsForNodesAsync(
            projectId,
            BuildNodeReferences(deletedNodeKeys),
            cancellationToken);
    }

    private Task MoveAssignmentsAsync(
        ProjectPartyAssignmentMoveOperationId operationId,
        MoveDescendantsMutationPayload payload,
        CancellationToken cancellationToken)
    {
        return projectPartyIntegrationBridge.MoveAssignmentsToProjectAsync(
            operationId,
            payload.SourceProjectId,
            BuildNodeReferences(payload.MovedNodeKeys),
            payload.TargetProjectId,
            cancellationToken);
    }

    private async Task RenewClaimAsync(
        AppDbContext dbContext,
        Guid mutationId,
        string claimToken,
        CancellationToken cancellationToken)
    {
        var now = await ProjectCrossModuleMutationTimeSource.GetUtcNowAsync(
            dbContext,
            clock,
            cancellationToken);
        if (!dbContext.Database.IsRelational())
        {
            var mutation = await GetOwnedClaimAsync(
                dbContext,
                mutationId,
                claimToken,
                cancellationToken);
            mutation.LastAttemptAtUtc = now;
            mutation.UpdatedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var affected = await dbContext.Set<ProjectCrossModuleMutationRecord>()
            .Where(record =>
                record.Id == mutationId &&
                record.Status == ProjectCrossModuleMutationStatus.Processing &&
                record.ErrorMessage == claimToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(record => record.LastAttemptAtUtc, now)
                .SetProperty(record => record.UpdatedAtUtc, now),
                cancellationToken);
        EnsureClaimOwned(mutationId, affected);
    }

    private async Task RunClaimHeartbeatAsync(
        Guid mutationId,
        string claimToken,
        CancellationToken cancellationToken,
        CancellationTokenSource processingCancellation)
    {
        try
        {
            using var timer = new PeriodicTimer(
                processingOptions.HeartbeatInterval,
                timeProvider);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
                await RenewClaimAsync(
                    dbContext,
                    mutationId,
                    claimToken,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            await processingCancellation.CancelAsync();
            throw;
        }
    }

    private async Task StopHeartbeatAsync(
        CancellationTokenSource heartbeatStop,
        Task heartbeatTask,
        bool suppressFailure)
    {
        heartbeatStop.Cancel();
        try
        {
            await heartbeatTask;
        }
        catch (OperationCanceledException) when (heartbeatStop.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (suppressFailure)
        {
            logger.LogWarning(
                "Durable Workbench mutation claim heartbeat stopped with {FailureType}.",
                exception.GetType().Name);
        }
    }

    private async Task PersistPayloadCheckpointAsync(
        AppDbContext dbContext,
        Guid mutationId,
        string claimToken,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        var now = await ProjectCrossModuleMutationTimeSource.GetUtcNowAsync(
            dbContext,
            clock,
            cancellationToken);
        if (!dbContext.Database.IsRelational())
        {
            var mutation = await GetOwnedClaimAsync(
                dbContext,
                mutationId,
                claimToken,
                cancellationToken);
            mutation.PayloadJson = payloadJson;
            mutation.LastAttemptAtUtc = now;
            mutation.UpdatedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var affected = await dbContext.Set<ProjectCrossModuleMutationRecord>()
            .Where(record =>
                record.Id == mutationId &&
                record.Status == ProjectCrossModuleMutationStatus.Processing &&
                record.ErrorMessage == claimToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(record => record.PayloadJson, payloadJson)
                .SetProperty(record => record.LastAttemptAtUtc, now)
                .SetProperty(record => record.UpdatedAtUtc, now),
                cancellationToken);
        EnsureClaimOwned(mutationId, affected);
    }

    private async Task<ProjectCrossModuleMutationStatus> CompleteClaimAsync(
        AppDbContext dbContext,
        Guid mutationId,
        string claimToken,
        CancellationToken cancellationToken)
    {
        var now = await ProjectCrossModuleMutationTimeSource.GetUtcNowAsync(
            dbContext,
            clock,
            cancellationToken);
        if (!dbContext.Database.IsRelational())
        {
            var mutation = await GetOwnedClaimAsync(
                dbContext,
                mutationId,
                claimToken,
                cancellationToken);
            mutationCoordinator.MarkCompleted(mutation);
            await dbContext.SaveChangesAsync(cancellationToken);
            return ProjectCrossModuleMutationStatus.Completed;
        }

        var affected = await dbContext.Set<ProjectCrossModuleMutationRecord>()
            .Where(record =>
                record.Id == mutationId &&
                record.Status == ProjectCrossModuleMutationStatus.Processing &&
                record.ErrorMessage == claimToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(record => record.Status, ProjectCrossModuleMutationStatus.Completed)
                .SetProperty(record => record.ErrorMessage, string.Empty)
                .SetProperty(record => record.CompletedAtUtc, now)
                .SetProperty(record => record.UpdatedAtUtc, now),
                cancellationToken);
        EnsureClaimOwned(mutationId, affected);
        return ProjectCrossModuleMutationStatus.Completed;
    }

    private async Task<bool> FailClaimAsync(
        AppDbContext dbContext,
        Guid mutationId,
        string claimToken,
        string safeErrorMessage,
        CancellationToken cancellationToken)
    {
        var now = await ProjectCrossModuleMutationTimeSource.GetUtcNowAsync(
            dbContext,
            clock,
            cancellationToken);
        if (!dbContext.Database.IsRelational())
        {
            var mutation = await dbContext.Set<ProjectCrossModuleMutationRecord>()
                .SingleOrDefaultAsync(record =>
                    record.Id == mutationId &&
                    record.Status == ProjectCrossModuleMutationStatus.Processing &&
                    record.ErrorMessage == claimToken,
                    cancellationToken);
            if (mutation is null)
            {
                return false;
            }

            mutationCoordinator.MarkFailed(mutation, safeErrorMessage);
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        var affected = await dbContext.Set<ProjectCrossModuleMutationRecord>()
            .Where(record =>
                record.Id == mutationId &&
                record.Status == ProjectCrossModuleMutationStatus.Processing &&
                record.ErrorMessage == claimToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(record => record.Status, ProjectCrossModuleMutationStatus.Failed)
                .SetProperty(record => record.ErrorMessage, safeErrorMessage)
                .SetProperty(record => record.CompletedAtUtc, (DateTimeOffset?)null)
                .SetProperty(record => record.UpdatedAtUtc, now),
                cancellationToken);
        return affected == 1;
    }

    private async Task<ProjectCrossModuleMutationStatus?> LoadStatusAsync(
        Guid mutationId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<ProjectCrossModuleMutationRecord>()
            .AsNoTracking()
            .Where(record => record.Id == mutationId)
            .Select(record => (ProjectCrossModuleMutationStatus?)record.Status)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static async Task<ProjectCrossModuleMutationRecord> GetOwnedClaimAsync(
        AppDbContext dbContext,
        Guid mutationId,
        string claimToken,
        CancellationToken cancellationToken)
    {
        return await dbContext.Set<ProjectCrossModuleMutationRecord>()
                   .SingleOrDefaultAsync(record =>
                       record.Id == mutationId &&
                       record.Status == ProjectCrossModuleMutationStatus.Processing &&
                       record.ErrorMessage == claimToken,
                       cancellationToken)
               ?? throw new InvalidOperationException(
                   $"Durable mutation processing claim for '{mutationId:D}' was lost.");
    }

    private static void EnsureClaimOwned(Guid mutationId, int affected)
    {
        if (affected != 1)
        {
            throw new InvalidOperationException(
                $"Durable mutation processing claim for '{mutationId:D}' was lost.");
        }
    }

    private static SemaphoreSlim ResolveProcessingLock(Guid mutationId)
    {
        var hash = mutationId.GetHashCode() & int.MaxValue;
        return ProcessingLockStripes[hash % ProcessingLockStripeCount];
    }

    private static IReadOnlyList<ProjectNodeReference> BuildNodeReferences(IReadOnlyList<string> nodeKeys)
    {
        return nodeKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => new ProjectNodeReference(key))
            .ToList();
    }

    private static TPayload Deserialize<TPayload>(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<TPayload>(
            string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson,
            JsonOptions);
        return payload
            ?? throw new InvalidOperationException($"Unable to deserialize {typeof(TPayload).Name}.");
    }
}
