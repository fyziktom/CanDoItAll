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
    IDbContextFactory<WorkbenchDbContext> dbContextFactory,
    DbContextOptions<WorkbenchDbContext> contextOptions,
    CoordinatedDatabaseTransaction coordinatedTransaction,
    ProjectWriteAdmissionService admissions, IProjectCreationCompensationGuard creationCleanup) : IProjectDeletionParticipant
{
    internal const string ProjectDeletionScopeNodeKey = "project";
    private const int CompletionLockStripeCount = 64;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly SemaphoreSlim[] CompletionLockStripes = Enumerable
        .Range(0, CompletionLockStripeCount)
        .Select(static _ => new SemaphoreSlim(1, 1))
        .ToArray();

    internal const string ParticipantIdValue = "workbench";
    public ProjectDeletionParticipantId Id { get; } = new(ParticipantIdValue);
    public int RecoveryProtocolVersion => ProjectWorkbenchDeletionPayload.RecoveryProtocolVersion;

    public IReadOnlyCollection<ProjectDeletionPreparationScopeKey> PreparationScopeKeys { get; } =
    [
        new(ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey)
    ];

    public Task<ProjectDeletionParticipantPreparation?> PrepareAsync(Guid projectId,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Project cleanup preparation requires the original typed deletion completion context.");

    public async Task<ProjectDeletionParticipantPreparation?> PrepareAsync(ProjectDeletionPreparationContext original,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(original);
        await admissions.RequireForMutationAsync(original.Project, cancellationToken);
        var dbContext = await coordinatedTransaction.CreateEnlistedAsync(
            contextOptions, static options => new WorkbenchDbContext(options), cancellationToken);
        Exception? primary = null;
        try {
            coordinatedTransaction.RequireOwnerProfile(dbContext);
            var preparation = await PrepareCoreAsync(dbContext, original, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return preparation;
        } catch (Exception failure) {
            primary = failure;
            throw;
        } finally {
            try {
                await dbContext.DisposeAsync();
            } catch (Exception cleanup) when (primary is not null) {
                throw new AggregateException("Workbench deletion preparation and enlisted context release failed.", primary, cleanup);
            }
        }
    }

    public async Task<ProjectDeletionParticipantPreparation?> PrepareAsync(ProjectReservationCancellationPreparationContext original,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(original);
        await admissions.RequireReservedCancellationForMutationAsync(original, cancellationToken);
        if (!await creationCleanup.CanCancelReservationForMutationAsync(original, cancellationToken)) {
            throw new InvalidOperationException("The never-created reservation retains Workbench, party or storage state requiring explicit reconciliation.");
        }
        return null;
    }

    private async Task<ProjectDeletionParticipantPreparation?> PrepareCoreAsync(WorkbenchDbContext dbContext,
        ProjectDeletionPreparationContext original, CancellationToken cancellationToken) {
        var projectId = original.Project.ProjectId;
        var sourceProjectMutations = await dbContext.Set<ProjectCrossModuleMutationRecord>()
            .Where(record => record.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        var projectObjects = await LoadProjectObjectsAsync(dbContext, projectId, cancellationToken);
        var allProjectMutations = sourceProjectMutations
            .Where(record => record.MutationKind == ProjectCrossModuleMutationKind.DeleteProject).ToArray();
        var reference = ProjectAssignmentReference.From(original.Project);
        reference.RequireProfile(admissions.DatabaseProfileId, projectId);
        var projectMutations = allProjectMutations
            .Where(record => ProjectWorkbenchDeletionPayload.Read(record).SourceReference == reference)
            .ToList();
        if (projectMutations.Any(record => ProjectWorkbenchDeletionPayload.Read(record).CompletionContext != original)) {
            throw new InvalidOperationException("A retained cleanup for this lifetime belongs to another or legacy deletion completion. Explicit reconciliation is required.");
        }
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
            : ProjectWorkbenchDeletionPayload.Read(existingMutation);
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
                managedStorageCandidates, reference, original),
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

            payload = ProjectWorkbenchDeletionPayload.Read(mutation);
        }

        await mutationProcessor.RequireProjectDeletionReleaseAsync(preparation.ProjectId, preparation.RecoveryId,
            payload, cancellationToken);
        await RequireNoResidualProjectStateAsync(
            preparation.ProjectId,
            preparation.RecoveryId,
            cancellationToken);
        await CompleteDependenciesAsync(
            preparation.RecoveryId,
            payload.OutstandingMutationIds,
            cancellationToken);

        ProjectCrossModuleMutationStatus? status;
        try
        {
            status = await mutationProcessor.ProcessAsync(
                preparation.RecoveryId,
                cancellationToken);
        }
        catch (Exception exception)
        {
            throw new ProjectDeletionParticipantCleanupException(
                preparation.RecoveryId,
                $"Workbench project cleanup '{preparation.RecoveryId:D}' was interrupted before durable completion.",
                exception);
        }

        if (status != ProjectCrossModuleMutationStatus.Completed)
        {
            throw new ProjectDeletionParticipantCleanupException(
                preparation.RecoveryId,
                $"Workbench project cleanup '{preparation.RecoveryId:D}' has status '{status?.ToString() ?? "missing"}'.");
        }

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var completedMutation = await dbContext.Set<ProjectCrossModuleMutationRecord>()
                .AsNoTracking()
                .SingleAsync(
                    record =>
                        record.Id == preparation.RecoveryId &&
                        record.ProjectId == preparation.ProjectId,
                    cancellationToken);
            payload = ProjectWorkbenchDeletionPayload.Read(completedMutation);
        }

        var completionWarnings = await LoadRetainedCompletionWarningsAsync(
            preparation.ProjectId,
            cancellationToken);
        return new ProjectDeletionParticipantCompletion(
            preparation.RecoveryId,
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

    private async Task RequireNoResidualProjectStateAsync(
        Guid projectId,
        Guid recoveryId,
        CancellationToken cancellationToken) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutationScope = await SerializableMutationScope.BeginAsync(
            dbContext,
            [ProjectMutationScopeKeys.ForProject(projectId), ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey],
            cancellationToken);
        if (await dbContext.Set<ProjectObjectRecord>().AnyAsync(record => record.ProjectId == projectId, cancellationToken)) {
            throw new ProjectDeletionParticipantCleanupException(recoveryId,
                "Newly discovered native project rows cannot inherit deletion authority from a saved cleanup. The rows and original receipt are retained and require reconciliation.");
        }

        await mutationScope.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectDeletionParticipantRecovery>> ListPendingRecoveriesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
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
        var recoveries = new List<ProjectDeletionParticipantRecovery>();
        foreach (var record in mutations.Where(record => record.Status != ProjectCrossModuleMutationStatus.Completed)) {
            var payload = ProjectWorkbenchDeletionPayload.Read(record);
            var released = payload.CompletionContext is not null &&
                await mutationProcessor.ObserveProjectDeletionReleaseAsync(record.ProjectId, record.Id, payload, cancellationToken)
                    is ProjectDeletionCompletionObservation.Released;
            recoveries.Add(new(record.ProjectId, record.Id, MapRecoveryStatus(record.Status),
                released && CanRetryNow(record, now, processingOptions.LeaseDuration),
                released ? ResolveRetryAvailableAtUtc(record, processingOptions.LeaseDuration) : null,
                released ? "Retry the exact durable project-cleanup operation after resolving the storage or assignment failure."
                    : "Retain the original recovery. Deletion owner release or explicit legacy reconciliation is required before retry."));
        }
        return recoveries;
    }

    public async Task<ProjectDeletionParticipantObservation> ObserveCompletionAsync(ProjectDeletionPreparationContext original,
        Guid recoveryId, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(original);
        if (recoveryId == Guid.Empty) {
            throw new ArgumentException("Exact deletion completion observation requires a recovery identity.", nameof(recoveryId));
        }
        var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        Exception? readFailure = null;
        ProjectCrossModuleMutationRecord? record;
        try {
            if (context.Database.CurrentTransaction is not null || System.Transactions.Transaction.Current is not null ||
                    context.Database.IsRelational() && System.Transactions.TransactionsDatabaseFacadeExtensions.GetEnlistedTransaction(context.Database) is not null ||
                    context.ChangeTracker.Entries().Any()) {
                throw new InvalidOperationException("Deletion participant observation requires an independent read context.");
            }
            coordinatedTransaction.RequireOwnerProfile(context);
            record = await context.Set<ProjectCrossModuleMutationRecord>().AsNoTracking()
                .SingleOrDefaultAsync(row => row.Id == recoveryId, cancellationToken);
        } catch (Exception failure) {
            readFailure = failure;
            throw;
        } finally {
            try {
                await context.DisposeAsync();
            } catch (Exception cleanupFailure) when (readFailure is not null) {
                throw new AggregateException("Deletion participant observation and context disposal both failed.", readFailure, cleanupFailure);
            }
        }
        if (record is null) {
            return new ProjectDeletionParticipantObservation.Unavailable(ProjectDeletionParticipantObservationFailure.MissingRecord);
        }
        if (record.ProjectId != original.Project.ProjectId || record.MutationKind != ProjectCrossModuleMutationKind.DeleteProject) {
            return new ProjectDeletionParticipantObservation.Unavailable(ProjectDeletionParticipantObservationFailure.OriginalBindingMismatch);
        }
        var payload = ProjectWorkbenchDeletionPayload.Read(record);
        if (!ProjectWorkbenchDeletionPayload.Matches(payload, original)) {
            return new ProjectDeletionParticipantObservation.Unavailable(ProjectDeletionParticipantObservationFailure.OriginalBindingMismatch);
        }
        if (!Enum.IsDefined(record.Status)) {
            return new ProjectDeletionParticipantObservation.Unavailable(ProjectDeletionParticipantObservationFailure.InvalidStoredState);
        }
        return record.Status == ProjectCrossModuleMutationStatus.Completed
            ? new ProjectDeletionParticipantObservation.Completed(new(recoveryId, MapCompletionWarnings(payload.ManagedStorageOutcomes)))
            : new ProjectDeletionParticipantObservation.Pending(MapRecoveryStatus(record.Status));
    }

    public async Task<IReadOnlyList<ProjectDeletionParticipantCompletionNotice>> ListCompletionNoticesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
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
                ProjectWorkbenchDeletionPayload.Read(mutation).ManagedStorageOutcomes ?? [],
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
                ProjectWorkbenchDeletionPayload.Read(mutation).ManagedStorageOutcomes?.Any(
                    outcome => outcome.Kind != ProjectManagedStorageDeletionOutcomeKind.DeletedOrAlreadyAbsent) == true,
            ProjectCrossModuleMutationKind.MoveDescendants or
                ProjectCrossModuleMutationKind.MoveSelectedNodes => false,
            _ => throw new InvalidOperationException(
                $"Unsupported durable mutation kind '{mutation.MutationKind}'.")
        };
    }

    private static async Task<List<ProjectCrossModuleMutationRecord>> LoadIncompleteInboundMoveMutationsAsync(
        WorkbenchDbContext dbContext,
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
        WorkbenchDbContext dbContext,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<ProjectObjectRecord>()
            .Where(record => record.ProjectId == projectId)
            .ToListAsync(cancellationToken);
    }

    private static async Task RemoveProjectRowsAsync(
        WorkbenchDbContext dbContext,
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
