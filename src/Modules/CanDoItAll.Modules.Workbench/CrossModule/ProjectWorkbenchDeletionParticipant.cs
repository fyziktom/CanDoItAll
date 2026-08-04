using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectWorkbenchDeletionParticipant(
    ProjectManagedStorageDeletionPlanner storageDeletionPlanner,
    ProjectCrossModuleMutationCoordinator mutationCoordinator,
    ProjectCrossModuleMutationProcessor mutationProcessor,
    ProjectCrossModuleMutationProcessingOptions processingOptions,
    IClock clock,
    IDbContextFactory<AppDbContext> dbContextFactory) : IProjectDeletionParticipant
{
    private const string ProjectDeletionScopeNodeKey = "project";
    private const int CompletionLockStripeCount = 64;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly SemaphoreSlim[] CompletionLockStripes = Enumerable
        .Range(0, CompletionLockStripeCount)
        .Select(static _ => new SemaphoreSlim(1, 1))
        .ToArray();

    public ProjectDeletionParticipantId Id { get; } = new("workbench");

    public IReadOnlyCollection<ProjectDeletionPreparationScopeKey> PreparationScopeKeys { get; } =
    [
        new(ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey)
    ];

    public async Task<ProjectDeletionParticipantPreparation?> PrepareAsync(
        AppDbContext dbContext,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var sourceProjectMutations = await dbContext.Set<ProjectCrossModuleMutationRecord>()
            .Where(record => record.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        var projectMutations = sourceProjectMutations
            .Where(record => record.MutationKind == ProjectCrossModuleMutationKind.DeleteProject)
            .ToList();
        var existingMutation = projectMutations
            .Where(record => record.Status != ProjectCrossModuleMutationStatus.Completed)
            .OrderByDescending(record => record.CreatedAtUtc)
            .FirstOrDefault()
            ?? projectMutations
                .OrderByDescending(record => record.CreatedAtUtc)
                .FirstOrDefault();

        var inboundMoveMutations = await LoadIncompleteInboundMoveMutationsAsync(
            dbContext,
            projectId,
            cancellationToken);
        var outstandingMutationIds = sourceProjectMutations
            .Where(record =>
                record.Id != existingMutation?.Id &&
                record.Status != ProjectCrossModuleMutationStatus.Completed)
            .Concat(inboundMoveMutations)
            .Select(record => record.Id)
            .Distinct()
            .Order()
            .ToArray();

        var projectObjects = await LoadProjectObjectsAsync(
            dbContext,
            projectId,
            cancellationToken);
        var objectIds = projectObjects.Select(record => record.Id).ToArray();
        var storagePlan = await storageDeletionPlanner.PlanAsync(
            dbContext,
            objectIds,
            cancellationToken);
        var hasNewDeletionWork = projectObjects.Count > 0 || storagePlan.References.Count > 0;
        if (existingMutation?.Status == ProjectCrossModuleMutationStatus.Processing &&
            !hasNewDeletionWork &&
            outstandingMutationIds.Length == 0)
        {
            return new ProjectDeletionParticipantPreparation(projectId, existingMutation.Id);
        }

        if (existingMutation?.Status == ProjectCrossModuleMutationStatus.Processing)
        {
            outstandingMutationIds = outstandingMutationIds
                .Append(existingMutation.Id)
                .Distinct()
                .Order()
                .ToArray();
            existingMutation = null;
        }

        var existingPayload = existingMutation is null
            ? null
            : DeserializeProjectPayload(existingMutation.PayloadJson);
        if (existingMutation?.Status == ProjectCrossModuleMutationStatus.Completed &&
            !hasNewDeletionWork &&
            outstandingMutationIds.Length == 0)
        {
            await RemoveProjectRowsAsync(
                dbContext,
                projectId,
                projectObjects,
                cancellationToken);
            return null;
        }

        if (existingMutation?.Status == ProjectCrossModuleMutationStatus.Completed)
        {
            existingMutation = null;
        }

        var deletedNodeKeys = projectObjects
            .Select(record => record.NodeKey)
            .Concat(existingPayload?.DeletedNodeKeys ?? [])
            .Where(static nodeKey => !string.IsNullOrWhiteSpace(nodeKey))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static nodeKey => nodeKey, StringComparer.Ordinal)
            .ToArray();
        var managedStorageCandidates = ResolveDeletionCandidates(existingPayload)
            .Concat(storagePlan.Candidates)
            .GroupBy(candidate => ProjectManagedStorageObjectKey.FromReference(candidate.Reference))
            .Select(group => group.First())
            .ToArray();
        var managedStorageObjects = managedStorageCandidates
            .Select(candidate => candidate.Reference)
            .ToArray();
        var completedStorageKeys = managedStorageObjects
            .Select(ProjectManagedStorageObjectKey.FromReference)
            .ToHashSet();
        var managedStorageOutcomes = (existingPayload?.ManagedStorageOutcomes ?? [])
            .Concat(storagePlan.Outcomes)
            .Where(outcome => completedStorageKeys.Contains(
                ProjectManagedStorageObjectKey.FromReference(outcome.Reference)))
            .GroupBy(outcome => ProjectManagedStorageObjectKey.FromReference(outcome.Reference))
            .Select(group => group.First())
            .ToArray();
        var payloadJson = JsonSerializer.Serialize(
            new DeleteProjectMutationPayload(
                deletedNodeKeys,
                managedStorageObjects,
                managedStorageOutcomes,
                outstandingMutationIds,
                managedStorageCandidates),
            JsonOptions);

        var durableMutation = existingMutation;
        if (durableMutation is null)
        {
            durableMutation = mutationCoordinator.Begin(
                projectId,
                ProjectDeletionScopeNodeKey,
                ProjectCrossModuleMutationKind.DeleteProject,
                payloadJson);
            await dbContext.Set<ProjectCrossModuleMutationRecord>()
                .AddAsync(durableMutation, cancellationToken);
        }
        else
        {
            durableMutation.PayloadJson = payloadJson;
        }

        mutationCoordinator.MarkWorkbenchCommitted(durableMutation);
        await RemoveProjectRowsAsync(
            dbContext,
            projectId,
            projectObjects,
            cancellationToken);
        return new ProjectDeletionParticipantPreparation(projectId, durableMutation.Id);
    }

    public async Task<ProjectDeletionParticipantCompletion> CompleteAsync(
        ProjectDeletionParticipantPreparation preparation,
        CancellationToken cancellationToken = default)
    {
        var completionLock = ResolveCompletionLock(preparation.ProjectId);
        await completionLock.WaitAsync(cancellationToken);
        try
        {
            return await CompleteCoreAsync(preparation, cancellationToken);
        }
        finally
        {
            completionLock.Release();
        }
    }

    private async Task<ProjectDeletionParticipantCompletion> CompleteCoreAsync(
        ProjectDeletionParticipantPreparation preparation,
        CancellationToken cancellationToken)
    {
        DeleteProjectMutationPayload payload;
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var mutation = await dbContext.Set<ProjectCrossModuleMutationRecord>()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    record => record.Id == preparation.RecoveryId,
                    cancellationToken);
            if (mutation is null)
            {
                throw new ProjectDeletionParticipantCleanupException(
                    preparation.RecoveryId,
                    $"Required Workbench project cleanup '{preparation.RecoveryId:D}' is missing from durable storage.");
            }

            if (mutation.ProjectId != preparation.ProjectId ||
                mutation.MutationKind != ProjectCrossModuleMutationKind.DeleteProject)
            {
                throw new InvalidOperationException(
                    $"Workbench project cleanup '{preparation.RecoveryId:D}' does not belong to project '{preparation.ProjectId:D}'.");
            }

            payload = DeserializeProjectPayload(mutation.PayloadJson);
        }

        await CompleteDependenciesAsync(
            preparation.RecoveryId,
            payload.OutstandingMutationIds,
            cancellationToken);

        var effectiveRecoveryId = await StageResidualProjectStateAsync(
            preparation.ProjectId,
            preparation.RecoveryId,
            cancellationToken);
        if (effectiveRecoveryId != preparation.RecoveryId)
        {
            payload = await LoadProjectPayloadAsync(
                preparation.ProjectId,
                effectiveRecoveryId,
                cancellationToken);
            await CompleteDependenciesAsync(
                effectiveRecoveryId,
                payload.OutstandingMutationIds,
                cancellationToken);
        }

        ProjectCrossModuleMutationStatus? status;
        try
        {
            status = await mutationProcessor.ProcessAsync(
                effectiveRecoveryId,
                cancellationToken);
        }
        catch (Exception exception)
        {
            throw new ProjectDeletionParticipantCleanupException(
                effectiveRecoveryId,
                $"Workbench project cleanup '{effectiveRecoveryId:D}' was interrupted before durable completion.",
                exception);
        }

        if (status != ProjectCrossModuleMutationStatus.Completed)
        {
            throw new ProjectDeletionParticipantCleanupException(
                effectiveRecoveryId,
                $"Workbench project cleanup '{effectiveRecoveryId:D}' has status '{status?.ToString() ?? "missing"}'.");
        }

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var completedMutation = await dbContext.Set<ProjectCrossModuleMutationRecord>()
                .AsNoTracking()
                .SingleAsync(
                    record =>
                        record.Id == effectiveRecoveryId &&
                        record.ProjectId == preparation.ProjectId,
                    cancellationToken);
            payload = DeserializeProjectPayload(completedMutation.PayloadJson);
        }

        var completionWarnings = await LoadRetainedCompletionWarningsAsync(
            preparation.ProjectId,
            cancellationToken);
        return new ProjectDeletionParticipantCompletion(
            effectiveRecoveryId,
            completionWarnings);
    }

    private async Task CompleteDependenciesAsync(
        Guid recoveryId,
        IReadOnlyList<Guid>? dependencyIds,
        CancellationToken cancellationToken)
    {
        foreach (var dependencyId in dependencyIds ?? [])
        {
            var status = await mutationProcessor.ProcessAsync(
                dependencyId,
                cancellationToken);
            if (status != ProjectCrossModuleMutationStatus.Completed)
            {
                throw new ProjectDeletionParticipantCleanupException(
                    recoveryId,
                    $"Required Workbench cleanup '{dependencyId:D}' has status '{status?.ToString() ?? "missing"}'.");
            }
        }
    }

    private async Task<DeleteProjectMutationPayload> LoadProjectPayloadAsync(
        Guid projectId,
        Guid recoveryId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var mutation = await dbContext.Set<ProjectCrossModuleMutationRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                record =>
                    record.Id == recoveryId &&
                    record.ProjectId == projectId &&
                    record.MutationKind == ProjectCrossModuleMutationKind.DeleteProject,
                cancellationToken);
        if (mutation is null)
        {
            throw new ProjectDeletionParticipantCleanupException(
                recoveryId,
                $"Required Workbench project cleanup '{recoveryId:D}' is missing from durable storage.");
        }

        return DeserializeProjectPayload(mutation.PayloadJson);
    }

    private async Task<Guid> StageResidualProjectStateAsync(
        Guid projectId,
        Guid currentRecoveryId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutationScope = await SerializableMutationScope.BeginAsync(
            dbContext,
            ProjectMutationScopeKeys.ForProject(projectId),
            cancellationToken);
        var currentMutation = await dbContext.Set<ProjectCrossModuleMutationRecord>()
            .SingleOrDefaultAsync(
                record =>
                    record.Id == currentRecoveryId &&
                    record.ProjectId == projectId &&
                    record.MutationKind == ProjectCrossModuleMutationKind.DeleteProject,
                cancellationToken);
        if (currentMutation is null)
        {
            await mutationScope.CommitAsync(cancellationToken);
            return currentRecoveryId;
        }

        var residualObjects = await LoadProjectObjectsAsync(
            dbContext,
            projectId,
            cancellationToken);
        if (residualObjects.Count == 0)
        {
            await mutationScope.CommitAsync(cancellationToken);
            return currentRecoveryId;
        }

        var storagePlan = await storageDeletionPlanner.PlanAsync(
            dbContext,
            residualObjects.Select(record => record.Id).ToArray(),
            cancellationToken);
        var currentPayload = DeserializeProjectPayload(currentMutation.PayloadJson);
        var canAmendCurrent = currentMutation.Status is
            ProjectCrossModuleMutationStatus.Pending or
            ProjectCrossModuleMutationStatus.WorkbenchCommitted or
            ProjectCrossModuleMutationStatus.Failed;
        var basePayload = canAmendCurrent
            ? currentPayload
            : new DeleteProjectMutationPayload(
                [],
                [],
                [],
                currentMutation.Status == ProjectCrossModuleMutationStatus.Completed
                    ? []
                    : [currentMutation.Id],
                []);
        var candidates = ResolveDeletionCandidates(basePayload)
            .Concat(storagePlan.Candidates)
            .GroupBy(candidate => ProjectManagedStorageObjectKey.FromReference(candidate.Reference))
            .Select(group => group.First())
            .ToArray();
        var candidateKeys = candidates
            .Select(candidate => ProjectManagedStorageObjectKey.FromReference(candidate.Reference))
            .ToHashSet();
        var outcomes = (basePayload.ManagedStorageOutcomes ?? [])
            .Concat(storagePlan.Outcomes)
            .Where(outcome => candidateKeys.Contains(
                ProjectManagedStorageObjectKey.FromReference(outcome.Reference)))
            .GroupBy(outcome => ProjectManagedStorageObjectKey.FromReference(outcome.Reference))
            .Select(group => group.First())
            .ToArray();
        var payload = new DeleteProjectMutationPayload(
            basePayload.DeletedNodeKeys
                .Concat(residualObjects.Select(record => record.NodeKey))
                .Where(nodeKey => !string.IsNullOrWhiteSpace(nodeKey))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(nodeKey => nodeKey, StringComparer.Ordinal)
                .ToArray(),
            candidates.Select(candidate => candidate.Reference).ToArray(),
            outcomes,
            basePayload.OutstandingMutationIds,
            candidates);
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        ProjectCrossModuleMutationRecord effectiveMutation;
        if (canAmendCurrent)
        {
            currentMutation.PayloadJson = payloadJson;
            mutationCoordinator.MarkWorkbenchCommitted(currentMutation);
            effectiveMutation = currentMutation;
        }
        else
        {
            effectiveMutation = mutationCoordinator.Begin(
                projectId,
                ProjectDeletionScopeNodeKey,
                ProjectCrossModuleMutationKind.DeleteProject,
                payloadJson);
            mutationCoordinator.MarkWorkbenchCommitted(effectiveMutation);
            await dbContext.Set<ProjectCrossModuleMutationRecord>()
                .AddAsync(effectiveMutation, cancellationToken);
        }

        await RemoveProjectRowsAsync(
            dbContext,
            projectId,
            residualObjects,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await mutationScope.CommitAsync(cancellationToken);
        return effectiveMutation.Id;
    }

    public async Task<IReadOnlyList<ProjectDeletionParticipantRecovery>> ListPendingRecoveriesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var mutations = await dbContext.Set<ProjectCrossModuleMutationRecord>()
            .AsNoTracking()
            .Where(record =>
                record.MutationKind == ProjectCrossModuleMutationKind.DeleteProject)
            .OrderBy(record => record.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var now = await ProjectCrossModuleMutationTimeSource.GetUtcNowAsync(
            dbContext,
            clock,
            cancellationToken);
        return mutations
            .Where(record => record.Status != ProjectCrossModuleMutationStatus.Completed)
            .Select(record => new ProjectDeletionParticipantRecovery(
                record.ProjectId,
                record.Id,
                MapRecoveryStatus(record.Status),
                CanRetryNow(record, now, processingOptions.LeaseDuration),
                ResolveRetryAvailableAtUtc(record, processingOptions.LeaseDuration),
                "Retry the exact durable project-cleanup operation after resolving the storage or assignment failure."))
            .ToArray();
    }

    public async Task<IReadOnlyList<ProjectDeletionParticipantCompletionNotice>> ListCompletionNoticesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var mutations = await dbContext.Set<ProjectCrossModuleMutationRecord>()
            .AsNoTracking()
            .Where(record =>
                record.Status == ProjectCrossModuleMutationStatus.Completed &&
                (record.MutationKind == ProjectCrossModuleMutationKind.DeleteProject ||
                 record.MutationKind == ProjectCrossModuleMutationKind.DeleteSubtree))
            .OrderBy(record => record.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return mutations
            .Select(record => (
                Record: record,
                Notice: new ProjectDeletionParticipantCompletionNotice(
                    record.ProjectId,
                    record.Id,
                    record.MutationKind == ProjectCrossModuleMutationKind.DeleteProject
                        ? ProjectDeletionCompletionOperation.ProjectDeletion
                        : ProjectDeletionCompletionOperation.ProjectNodeCleanup,
                    MapCompletionWarnings(ResolveStorageOutcomes(record)))))
            .Where(item =>
                item.Record.MutationKind == ProjectCrossModuleMutationKind.DeleteProject ||
                item.Notice.Warnings.Count > 0)
            .Select(item => item.Notice)
            .ToArray();
    }

    private async Task<IReadOnlyList<ProjectDeletionParticipantWarning>> LoadRetainedCompletionWarningsAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var mutations = await dbContext.Set<ProjectCrossModuleMutationRecord>()
            .AsNoTracking()
            .Where(record =>
                record.ProjectId == projectId &&
                record.Status == ProjectCrossModuleMutationStatus.Completed &&
                (record.MutationKind == ProjectCrossModuleMutationKind.DeleteProject ||
                 record.MutationKind == ProjectCrossModuleMutationKind.DeleteSubtree))
            .ToListAsync(cancellationToken);
        var outcomes = mutations
            .SelectMany(ResolveStorageOutcomes)
            .Where(outcome =>
                outcome.Kind != ProjectManagedStorageDeletionOutcomeKind.DeletedOrAlreadyAbsent)
            .GroupBy(outcome => ProjectManagedStorageObjectKey.FromReference(outcome.Reference))
            .Select(group => group.First())
            .ToArray();
        return MapCompletionWarnings(outcomes);
    }

    private static IReadOnlyList<ProjectManagedStorageDeletionOutcome> ResolveStorageOutcomes(
        ProjectCrossModuleMutationRecord mutation)
    {
        return mutation.MutationKind switch
        {
            ProjectCrossModuleMutationKind.DeleteSubtree =>
                DeserializeSubtreePayload(mutation.PayloadJson).ManagedStorageOutcomes ?? [],
            ProjectCrossModuleMutationKind.DeleteProject =>
                DeserializeProjectPayload(mutation.PayloadJson).ManagedStorageOutcomes ?? [],
            _ => []
        };
    }

    private static IReadOnlyList<ProjectDeletionParticipantWarning> MapCompletionWarnings(
        IReadOnlyList<ProjectManagedStorageDeletionOutcome>? outcomes)
    {
        return (outcomes ?? [])
            .Where(outcome =>
                outcome.Kind != ProjectManagedStorageDeletionOutcomeKind.DeletedOrAlreadyAbsent)
            .Select(outcome => outcome.Kind switch
            {
                ProjectManagedStorageDeletionOutcomeKind.RetainedByProvider =>
                    new ProjectDeletionParticipantWarning(
                        ProjectDeletionWarningKind.ManagedStorageRetainedByProvider,
                        MapRetainedObject(outcome),
                        $"Managed media was retained by the immutable '{outcome.Reference.ProviderKind}' provider.",
                        "No cleanup retry is required. Retain the content address or remove any external pin according to provider policy."),
                ProjectManagedStorageDeletionOutcomeKind.RetainedWithoutOwnershipProof =>
                    new ProjectDeletionParticipantWarning(
                        ProjectDeletionWarningKind.ManagedStorageRetainedWithoutOwnershipProof,
                        MapRetainedObject(outcome),
                        $"Legacy managed media on '{outcome.Reference.ProviderKind}' was retained because physical ownership could not be proven.",
                        "Migrate the asset to a currently managed storage reference or remove the legacy object manually after verifying ownership."),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(outcome.Kind),
                    outcome.Kind,
                    null)
            })
            .ToArray();
    }

    private static ProjectDeletionRetainedObjectDescriptor MapRetainedObject(
        ProjectManagedStorageDeletionOutcome outcome)
        => new(
            outcome.Reference.ProviderKind,
            outcome.Reference.StorageId,
            outcome.Reference.LocatorKind,
            outcome.Reference.Locator,
            outcome.Reason);

    private static ProjectDeletionRecoveryStatus MapRecoveryStatus(
        ProjectCrossModuleMutationStatus status)
    {
        return status switch
        {
            ProjectCrossModuleMutationStatus.Pending or
                ProjectCrossModuleMutationStatus.WorkbenchCommitted => ProjectDeletionRecoveryStatus.Pending,
            ProjectCrossModuleMutationStatus.Processing => ProjectDeletionRecoveryStatus.Processing,
            ProjectCrossModuleMutationStatus.Failed => ProjectDeletionRecoveryStatus.Failed,
            ProjectCrossModuleMutationStatus.Completed => ProjectDeletionRecoveryStatus.Finalizing,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }

    private static bool CanRetryNow(
        ProjectCrossModuleMutationRecord mutation,
        DateTimeOffset now,
        TimeSpan leaseDuration)
        => mutation.Status != ProjectCrossModuleMutationStatus.Processing ||
           !mutation.LastAttemptAtUtc.HasValue ||
           mutation.LastAttemptAtUtc.Value + leaseDuration <= now;

    private static DateTimeOffset? ResolveRetryAvailableAtUtc(
        ProjectCrossModuleMutationRecord mutation,
        TimeSpan leaseDuration)
        => mutation.Status == ProjectCrossModuleMutationStatus.Processing &&
           mutation.LastAttemptAtUtc.HasValue
            ? mutation.LastAttemptAtUtc.Value + leaseDuration
            : null;

    private static bool HasRetainedStorageOutcome(ProjectCrossModuleMutationRecord mutation)
    {
        return mutation.MutationKind switch
        {
            ProjectCrossModuleMutationKind.DeleteSubtree =>
                DeserializeSubtreePayload(mutation.PayloadJson).ManagedStorageOutcomes?.Any(
                    outcome => outcome.Kind != ProjectManagedStorageDeletionOutcomeKind.DeletedOrAlreadyAbsent) == true,
            ProjectCrossModuleMutationKind.DeleteProject =>
                DeserializeProjectPayload(mutation.PayloadJson).ManagedStorageOutcomes?.Any(
                    outcome => outcome.Kind != ProjectManagedStorageDeletionOutcomeKind.DeletedOrAlreadyAbsent) == true,
            ProjectCrossModuleMutationKind.MoveDescendants or
                ProjectCrossModuleMutationKind.MoveSelectedNodes => false,
            _ => throw new InvalidOperationException(
                $"Unsupported durable mutation kind '{mutation.MutationKind}'.")
        };
    }

    private static async Task<List<ProjectCrossModuleMutationRecord>> LoadIncompleteInboundMoveMutationsAsync(
        AppDbContext dbContext,
        Guid targetProjectId,
        CancellationToken cancellationToken)
    {
        var candidates = await dbContext.Set<ProjectCrossModuleMutationRecord>()
            .Where(record =>
                record.ProjectId != targetProjectId &&
                record.Status != ProjectCrossModuleMutationStatus.Completed &&
                (record.MutationKind == ProjectCrossModuleMutationKind.MoveDescendants ||
                 record.MutationKind == ProjectCrossModuleMutationKind.MoveSelectedNodes))
            .ToListAsync(cancellationToken);
        return candidates
            .Where(record =>
                DeserializeMovePayload(record.PayloadJson).TargetProjectId == targetProjectId)
            .ToList();
    }

    private static Task<List<ProjectObjectRecord>> LoadProjectObjectsAsync(
        AppDbContext dbContext,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<ProjectObjectRecord>()
            .Where(record => record.ProjectId == projectId)
            .ToListAsync(cancellationToken);
    }

    private static async Task RemoveProjectRowsAsync(
        AppDbContext dbContext,
        Guid projectId,
        IReadOnlyCollection<ProjectObjectRecord> projectObjects,
        CancellationToken cancellationToken)
    {
        var objectIds = projectObjects.Select(record => record.Id).ToArray();
        var bindings = await dbContext.Set<ProjectNodeBindingRecord>()
            .Where(record => objectIds.Contains(record.ProjectObjectId))
            .ToListAsync(cancellationToken);
        var references = await dbContext.Set<ProjectNodeReferenceRecord>()
            .Where(record => objectIds.Contains(record.ProjectObjectId))
            .ToListAsync(cancellationToken);
        var lifecycleEvents = await dbContext.Set<ProjectNodeLifecycleEventRecord>()
            .Where(record => objectIds.Contains(record.ProjectObjectId))
            .ToListAsync(cancellationToken);
        var links = await dbContext.Set<ProjectObjectLinkRecord>()
            .Where(record => record.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        var viewStates = await dbContext.Set<ProjectWorkbenchViewStateRecord>()
            .Where(record => record.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        var layouts = await dbContext.Set<ProjectStructureProjectionLayoutRecord>()
            .Where(record => record.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        var analytics = await dbContext.Set<ProjectStructureOperationAnalyticsRecord>()
            .Where(record => record.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        var leases = await dbContext.Set<ProjectStructureLeaseRecord>()
            .ToListAsync(cancellationToken);
        var nodeKeys = projectObjects
            .Select(record => record.NodeKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var projectLeases = leases.Where(record =>
            (record.ScopeKind == ProjectStructureLeaseScopeKind.Project &&
             Guid.TryParse(record.ScopeKey, out var scopedProjectId) &&
             scopedProjectId == projectId) ||
            (record.ScopeKind == ProjectStructureLeaseScopeKind.ProjectNode &&
             nodeKeys.Contains(record.ScopeKey))).ToList();

        dbContext.RemoveRange(bindings);
        dbContext.RemoveRange(references);
        dbContext.RemoveRange(lifecycleEvents);
        dbContext.RemoveRange(links);
        dbContext.RemoveRange(viewStates);
        dbContext.RemoveRange(layouts);
        dbContext.RemoveRange(analytics);
        dbContext.RemoveRange(projectLeases);
        dbContext.RemoveRange(projectObjects);
    }

    private static DeleteProjectMutationPayload DeserializeProjectPayload(string payloadJson)
    {
        return JsonSerializer.Deserialize<DeleteProjectMutationPayload>(
                   string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson,
                   JsonOptions)
               ?? throw new InvalidOperationException(
                   "Unable to deserialize the durable Workbench project-deletion payload.");
    }

    private static IReadOnlyList<ProjectManagedStorageDeletionCandidate> ResolveDeletionCandidates(
        DeleteProjectMutationPayload? payload)
    {
        if (payload?.ManagedStorageCandidates is not null)
        {
            return payload.ManagedStorageCandidates;
        }

        return (payload?.ManagedStorageObjects ?? [])
            .Select(reference => new ProjectManagedStorageDeletionCandidate(
                reference,
                reference.ProviderKind == CanDoItAll.Infrastructure.Storage.StorageProviderKind.Ipfs
                    ? ProjectManagedStorageOwnershipBasis.ImmutableContentAddress
                    : ProjectManagedStorageProvenancePolicy.HasManagedMarker(reference)
                        ? ProjectManagedStorageOwnershipBasis.CreationProvenanceV2
                        : ProjectManagedStorageOwnershipBasis.UnverifiedLegacyPayload,
                string.Empty,
                string.Empty))
            .ToArray();
    }

    private static DeleteSubtreeMutationPayload DeserializeSubtreePayload(string payloadJson)
    {
        return JsonSerializer.Deserialize<DeleteSubtreeMutationPayload>(
                   string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson,
                   JsonOptions)
               ?? throw new InvalidOperationException(
                   "Unable to deserialize the durable Workbench subtree-deletion payload.");
    }

    private static SemaphoreSlim ResolveCompletionLock(Guid projectId)
    {
        var stripeIndex = (projectId.GetHashCode() & int.MaxValue) % CompletionLockStripeCount;
        return CompletionLockStripes[stripeIndex];
    }

    private static MoveDescendantsMutationPayload DeserializeMovePayload(string payloadJson)
    {
        return JsonSerializer.Deserialize<MoveDescendantsMutationPayload>(
                   string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson,
                   JsonOptions)
               ?? throw new InvalidOperationException(
                   "Unable to deserialize the durable Workbench move payload.");
    }
}
