using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class AgentProjectStructureAccessDeletionParticipant(
    IAgentFrameworkWorkspaceService workspaceService,
    IDbContextFactory<AgentProjectAccessDbContext> dbContextFactory,
    TimeProvider timeProvider,
    ILogger<AgentProjectStructureAccessDeletionParticipant> logger,
    DbContextOptions<AgentProjectAccessDbContext> contextOptions,
    CoordinatedDatabaseTransaction coordinatedTransaction,
    AgentProjectAccessClaimOptions claimOptions,
    ProjectWriteAdmissionService writeAdmissionService,
    IProjectDeletionCompletionQuery deletionCompletions)
    : IProjectDeletionParticipant
{
    private readonly AgentProjectAccessClaimOptions options = claimOptions.Validate();
    private const int CompletionLockStripeCount = 64;
    private static readonly SemaphoreSlim[] CompletionLockStripes = Enumerable
        .Range(0, CompletionLockStripeCount)
        .Select(static _ => new SemaphoreSlim(1, 1))
        .ToArray();

    internal const string ParticipantIdValue = "agent-project-structure-access";
    internal const string RetryGuidance =
        "Retry the exact Agent Framework project-access cleanup with this recovery id.";

    public ProjectDeletionParticipantId Id { get; } = new(ParticipantIdValue);
    public int RecoveryProtocolVersion => AgentProjectDeletionBinding.ProtocolVersion;

    public IReadOnlyCollection<ProjectDeletionPreparationScopeKey> PreparationScopeKeys { get; } = [];

    public Task<ProjectDeletionParticipantPreparation?> PrepareAsync(Guid projectId,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Project-access cleanup preparation requires the original typed deletion completion context.");

    public async Task<ProjectDeletionParticipantPreparation?> PrepareAsync(ProjectDeletionPreparationContext original,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(original);
        await writeAdmissionService.RequireForMutationAsync(original.Project, cancellationToken);
        return await PrepareCoreAsync(original, cancellationToken);
    }

    public async Task<ProjectDeletionParticipantPreparation?> PrepareAsync(ProjectReservationCancellationPreparationContext original,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(original);
        await writeAdmissionService.RequireReservedCancellationForMutationAsync(original, cancellationToken);
        return await PrepareCoreAsync(original.Completion, cancellationToken);
    }

    private async Task<ProjectDeletionParticipantPreparation?> PrepareCoreAsync(ProjectDeletionPreparationContext original,
        CancellationToken cancellationToken) {
        var dbContext = await coordinatedTransaction.CreateEnlistedAsync(
            contextOptions, static options => new AgentProjectAccessDbContext(options), cancellationToken);
        Exception? primary = null;
        try {
            coordinatedTransaction.RequireOwnerProfile(dbContext);
            var project = original.Project;
            var record = await dbContext.Set<AgentProjectStructureAccessRevocationRecord>()
                .SingleOrDefaultAsync(record => record.ProjectId == project.ProjectId &&
                    record.ProjectLifetimeId == project.LifetimeId, cancellationToken);
            if (record is not null && OriginalContext(record) != original) {
                throw new InvalidOperationException("The retained project-access cleanup belongs to another or legacy deletion completion.");
            }
            if (record?.Status == AgentProjectStructureAccessRevocationStatus.Completed) {
                return null;
            }
            if (record is null) {
                var now = timeProvider.GetUtcNow();
                record = new AgentProjectStructureAccessRevocationRecord {
                    Id = Guid.NewGuid(),
                    ProjectId = project.ProjectId,
                    DatabaseProfileId = project.DatabaseProfileId,
                    ProjectLifetimeId = project.LifetimeId,
                    CompletionNamespaceId = original.Completion.Namespace.Value,
                    CompletionId = original.Completion.Id.Value,
                    Status = AgentProjectStructureAccessRevocationStatus.Pending,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };
                await dbContext.Set<AgentProjectStructureAccessRevocationRecord>().AddAsync(record, cancellationToken);
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            return new(project.ProjectId, record.Id);
        } catch (Exception failure) {
            primary = failure;
            throw;
        } finally {
            try {
                await dbContext.DisposeAsync();
            } catch (Exception cleanup) when (primary is not null) {
                throw new AggregateException("Agent project-access preparation and enlisted context release failed.", primary, cleanup);
            }
        }
    }

    public async Task<ProjectDeletionParticipantCompletion> CompleteAsync(
        ProjectDeletionParticipantPreparation preparation,
        CancellationToken cancellationToken = default)
    {
        var completionLock = ResolveCompletionLock(preparation.RecoveryId);
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
        ProjectDeletionParticipantPreparation preparation, CancellationToken cancellationToken) {
        var record = await LoadRequiredRecordAsync(preparation, cancellationToken);
        await RequireOwnerReleaseAsync(record, cancellationToken);
        if (record.Status == AgentProjectStructureAccessRevocationStatus.Completed) {
            return ProjectDeletionParticipantCompletion.Empty(preparation.RecoveryId);
        }
        var claim = await TryClaimAsync(record, cancellationToken);
        if (claim is null) {
            if (await IsCompletedAsync(preparation, cancellationToken)) {
                return ProjectDeletionParticipantCompletion.Empty(preparation.RecoveryId);
            }
            throw new ProjectDeletionParticipantCleanupException(preparation.RecoveryId,
                $"Agent Framework project-access cleanup '{preparation.RecoveryId:D}' is owned by another active attempt. Retry this recovery when its lease is available.");
        }
        using var heartbeatStop = new CancellationTokenSource();
        using var processingCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var heartbeatTask = RunHeartbeatAsync(claim, heartbeatStop.Token, processingCancellation);
        try {
            if (!await TryRenewClaimAsync(claim, processingCancellation.Token)) {
                throw new InvalidOperationException("Agent project-access revocation lost its claim before workspace dispatch.");
            }
            await RequireOwnerReleaseAsync(record, processingCancellation.Token);
            await workspaceService.RevokeProjectStructureLifetimeAccessFromAllAgentsAsync(
                ResolveRevocationTarget(record), processingCancellation.Token);
            await StopHeartbeatAsync(heartbeatStop, heartbeatTask, suppressFailure: false);
            if (!await TryCompleteClaimAsync(claim, cancellationToken) && !await IsCompletedAsync(preparation, cancellationToken)) {
                throw new InvalidOperationException("Agent project-access revocation lost its claim before completion.");
            }
            return ProjectDeletionParticipantCompletion.Empty(preparation.RecoveryId);
        } catch (Exception exception) {
            await StopHeartbeatAsync(heartbeatStop, heartbeatTask, suppressFailure: true);
            logger.LogError(exception,
                "Agent Framework project-access cleanup failed for project {ProjectId} and recovery {RecoveryId} at attempt {Attempt}.",
                preparation.ProjectId, preparation.RecoveryId, claim.Generation);
            var stateFailure = await TryMarkFailedAsync(claim, exception);
            var cleanupFailure = stateFailure is null ? exception : new AggregateException(exception, stateFailure);
            throw new ProjectDeletionParticipantCleanupException(preparation.RecoveryId,
                $"Agent Framework project-access cleanup '{preparation.RecoveryId:D}' failed for project '{preparation.ProjectId:D}'.",
                cleanupFailure);
        }
    }

    public async Task<IReadOnlyList<ProjectDeletionParticipantRecovery>> ListPendingRecoveriesAsync(
        CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = await GetDatabaseUtcNowAsync(dbContext, cancellationToken);
        var records = await dbContext.Set<AgentProjectStructureAccessRevocationRecord>().AsNoTracking()
            .Where(record => record.Status != AgentProjectStructureAccessRevocationStatus.Completed)
            .OrderBy(record => record.CreatedAtUtc).ToListAsync(cancellationToken);
        var recoveries = new List<ProjectDeletionParticipantRecovery>(records.Count);
        foreach (var record in records) {
            var original = OriginalContext(record);
            var released = original is not null &&
                await deletionCompletions.ObserveAsync(original, new(Id, record.Id), cancellationToken)
                    is ProjectDeletionCompletionObservation.Released;
            var lastOwnershipAt = record.LastAttemptAtUtc is { } attempted && attempted > record.UpdatedAtUtc
                ? attempted : record.UpdatedAtUtc;
            var retryAt = lastOwnershipAt + options.LeaseDuration;
            var active = record.Status == AgentProjectStructureAccessRevocationStatus.Processing && retryAt > now;
            recoveries.Add(new(record.ProjectId, record.Id, MapRecoveryStatus(record.Status),
                CanRetryNow: released && !active, RetryAvailableAtUtc: released && active ? retryAt : null,
                RetryGuidance: released ? RetryGuidance
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
        AgentProjectStructureAccessRevocationRecord? record;
        try {
            if (context.Database.CurrentTransaction is not null || System.Transactions.Transaction.Current is not null ||
                    context.Database.IsRelational() && System.Transactions.TransactionsDatabaseFacadeExtensions.GetEnlistedTransaction(context.Database) is not null ||
                    context.ChangeTracker.Entries().Any()) {
                throw new InvalidOperationException("Deletion participant observation requires an independent read context.");
            }
            coordinatedTransaction.RequireOwnerProfile(context);
            record = await context.Set<AgentProjectStructureAccessRevocationRecord>().AsNoTracking()
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
        if (OriginalContext(record) != original) {
            return new ProjectDeletionParticipantObservation.Unavailable(ProjectDeletionParticipantObservationFailure.OriginalBindingMismatch);
        }
        if (!Enum.IsDefined(record.Status)) {
            return new ProjectDeletionParticipantObservation.Unavailable(ProjectDeletionParticipantObservationFailure.InvalidStoredState);
        }
        return record.Status == AgentProjectStructureAccessRevocationStatus.Completed
            ? new ProjectDeletionParticipantObservation.Completed(ProjectDeletionParticipantCompletion.Empty(recoveryId))
            : new ProjectDeletionParticipantObservation.Pending(MapRecoveryStatus(record.Status));
    }

    public async Task<IReadOnlyList<ProjectDeletionParticipantCompletionNotice>> ListCompletionNoticesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var records = await dbContext.Set<AgentProjectStructureAccessRevocationRecord>()
            .AsNoTracking()
            .Where(record => record.Status == AgentProjectStructureAccessRevocationStatus.Completed)
            .OrderBy(record => record.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return records
            .Select(record => new ProjectDeletionParticipantCompletionNotice(
                record.ProjectId,
                record.Id,
                ProjectDeletionCompletionOperation.ProjectDeletion,
                Array.Empty<ProjectDeletionParticipantWarning>()))
            .ToArray();
    }

    private async Task<AgentProjectStructureAccessRevocationRecord> LoadRequiredRecordAsync(
        ProjectDeletionParticipantPreparation preparation,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<AgentProjectStructureAccessRevocationRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == preparation.RecoveryId,
                cancellationToken);
        if (record is null)
        {
            throw new ProjectDeletionParticipantCleanupException(
                preparation.RecoveryId,
                $"Required Agent Framework project-access cleanup '{preparation.RecoveryId:D}' is missing from durable storage.");
        }

        if (record.ProjectId != preparation.ProjectId)
        {
            throw new ProjectDeletionParticipantCleanupException(
                preparation.RecoveryId,
                $"Agent Framework project-access cleanup '{preparation.RecoveryId:D}' does not belong to project '{preparation.ProjectId:D}'.");
        }

        return record;
    }

    internal async Task<AgentProjectAccessRevocationClaim?> TryClaimAsync(
        AgentProjectStructureAccessRevocationRecord observed, CancellationToken cancellationToken = default) {
        if (observed.AttemptCount < 0 || observed.AttemptCount == int.MaxValue) {
            throw new InvalidOperationException("Agent project-access revocation has no valid remaining claim generation.");
        }
        var generation = observed.AttemptCount + 1;
        var affected = await ExecuteClaimTransitionAsync(observed.ProjectId, observed.Id, $"""
            WITH claim_clock AS MATERIALIZED (SELECT clock_timestamp() AS now)
            UPDATE "AgentFramework_ProjectAccessRevocations" AS record
            SET "Status" = {(int)AgentProjectStructureAccessRevocationStatus.Processing},
                "AttemptCount" = {generation}, "LastAttemptAtUtc" = claim_clock.now,
                "UpdatedAtUtc" = claim_clock.now, "LastFailureCode" = NULL
            FROM claim_clock
            WHERE record."Id" = {observed.Id} AND record."ProjectId" = {observed.ProjectId}
                AND record."AttemptCount" = {observed.AttemptCount}
                AND (record."Status" IN ({(int)AgentProjectStructureAccessRevocationStatus.Pending}, {(int)AgentProjectStructureAccessRevocationStatus.Failed})
                    OR record."Status" = {(int)AgentProjectStructureAccessRevocationStatus.Processing}
                        AND record."UpdatedAtUtc" <= claim_clock.now - {options.LeaseDuration}
                        AND (record."LastAttemptAtUtc" IS NULL OR record."LastAttemptAtUtc" <= claim_clock.now - {options.LeaseDuration}))
            """, cancellationToken);
        return affected == 1 ? new(observed.ProjectId, observed.Id, generation) : null;
    }

    internal Task<bool> TryRenewClaimAsync(AgentProjectAccessRevocationClaim claim, CancellationToken cancellationToken = default) =>
        TryTransitionOwnedClaimAsync(claim, AgentProjectStructureAccessRevocationStatus.Processing, null, cancellationToken);

    internal Task<bool> TryCompleteClaimAsync(AgentProjectAccessRevocationClaim claim, CancellationToken cancellationToken = default) =>
        TryTransitionOwnedClaimAsync(claim, AgentProjectStructureAccessRevocationStatus.Completed, null, cancellationToken);

    internal Task<bool> TryFailClaimAsync(AgentProjectAccessRevocationClaim claim, string failureCode,
        CancellationToken cancellationToken = default) =>
        TryTransitionOwnedClaimAsync(claim, AgentProjectStructureAccessRevocationStatus.Failed, failureCode, cancellationToken);

    private async Task<bool> TryTransitionOwnedClaimAsync(AgentProjectAccessRevocationClaim claim,
        AgentProjectStructureAccessRevocationStatus status, string? failureCode, CancellationToken cancellationToken) =>
        await ExecuteClaimTransitionAsync(claim.ProjectId, claim.RecoveryId, $"""
            WITH claim_clock AS MATERIALIZED (SELECT clock_timestamp() AS now)
            UPDATE "AgentFramework_ProjectAccessRevocations" AS record
            SET "Status" = {(int)status}, "UpdatedAtUtc" = claim_clock.now,
                "CompletedAtUtc" = CASE WHEN {(int)status} = {(int)AgentProjectStructureAccessRevocationStatus.Completed}
                    THEN claim_clock.now ELSE record."CompletedAtUtc" END,
                "LastFailureCode" = CASE WHEN {(int)status} = {(int)AgentProjectStructureAccessRevocationStatus.Processing}
                    THEN record."LastFailureCode" ELSE {failureCode} END
            FROM claim_clock
            WHERE record."Id" = {claim.RecoveryId} AND record."ProjectId" = {claim.ProjectId}
                AND record."Status" = {(int)AgentProjectStructureAccessRevocationStatus.Processing}
                AND record."AttemptCount" = {claim.Generation}
                AND (record."UpdatedAtUtc" > claim_clock.now - {options.LeaseDuration}
                    OR record."LastAttemptAtUtc" > claim_clock.now - {options.LeaseDuration})
            """, cancellationToken) == 1;

    private async Task<int> ExecuteClaimTransitionAsync(Guid projectId, Guid recoveryId, FormattableString update,
        CancellationToken cancellationToken) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (!dbContext.Database.IsNpgsql()) {
            throw new InvalidOperationException("Agent project-access claim transitions require the canonical PostgreSQL provider.");
        }
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.Database.SqlQuery<Guid>($"""
            SELECT "Id" AS "Value" FROM "AgentFramework_ProjectAccessRevocations"
            WHERE "Id" = {recoveryId} AND "ProjectId" = {projectId}
            FOR UPDATE
            """).ToArrayAsync(cancellationToken);
        var affected = await dbContext.Database.ExecuteSqlInterpolatedAsync(update, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return affected;
    }

    private async Task RunHeartbeatAsync(AgentProjectAccessRevocationClaim claim, CancellationToken stop,
        CancellationTokenSource processingCancellation) {
        try {
            while (true) {
                await Task.Delay(options.HeartbeatInterval, timeProvider, stop);
                if (!await TryRenewClaimAsync(claim, stop)) {
                    throw new InvalidOperationException("Agent project-access revocation lost its active claim lease.");
                }
            }
        } catch (OperationCanceledException) when (stop.IsCancellationRequested) {
        } catch {
            await processingCancellation.CancelAsync();
            throw;
        }
    }

    private static async Task StopHeartbeatAsync(CancellationTokenSource stop, Task heartbeat, bool suppressFailure) {
        await stop.CancelAsync();
        try {
            await heartbeat;
        } catch when (suppressFailure) {
        }
    }

    private async Task<Exception?> TryMarkFailedAsync(AgentProjectAccessRevocationClaim claim, Exception failure) {
        try {
            using var timeout = new CancellationTokenSource(options.FailurePersistenceTimeout);
            await TryFailClaimAsync(claim, failure.GetType().Name, timeout.Token);
            return null;
        } catch (Exception stateException) {
            logger.LogError(stateException,
                "Agent Framework project-access cleanup failure state could not be persisted for project {ProjectId} and recovery {RecoveryId} at attempt {Attempt}.",
                claim.ProjectId, claim.RecoveryId, claim.Generation);
            return stateException;
        }
    }

    private Task<DateTimeOffset> GetDatabaseUtcNowAsync(AgentProjectAccessDbContext dbContext, CancellationToken cancellationToken) {
        if (!dbContext.Database.IsRelational()) {
            return Task.FromResult(timeProvider.GetUtcNow());
        }
        if (!dbContext.Database.IsNpgsql()) {
            throw new InvalidOperationException("Agent project-access claim time requires the canonical PostgreSQL provider.");
        }
        return dbContext.Database.SqlQueryRaw<DateTimeOffset>("SELECT clock_timestamp() AS \"Value\"").SingleAsync(cancellationToken);
    }

    private async Task<bool> IsCompletedAsync(
        ProjectDeletionParticipantPreparation preparation,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<AgentProjectStructureAccessRevocationRecord>()
            .AsNoTracking()
            .AnyAsync(
                record =>
                    record.Id == preparation.RecoveryId &&
                    record.ProjectId == preparation.ProjectId &&
                    record.Status == AgentProjectStructureAccessRevocationStatus.Completed,
                cancellationToken);
    }

    private static ProjectDeletionRecoveryStatus MapRecoveryStatus(
        AgentProjectStructureAccessRevocationStatus status)
    {
        return status switch
        {
            AgentProjectStructureAccessRevocationStatus.Pending => ProjectDeletionRecoveryStatus.Pending,
            AgentProjectStructureAccessRevocationStatus.Processing => ProjectDeletionRecoveryStatus.Processing,
            AgentProjectStructureAccessRevocationStatus.Failed => ProjectDeletionRecoveryStatus.Failed,
            AgentProjectStructureAccessRevocationStatus.Completed => ProjectDeletionRecoveryStatus.Finalizing,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }

    private async Task RequireOwnerReleaseAsync(AgentProjectStructureAccessRevocationRecord record,
        CancellationToken cancellationToken) {
        var original = OriginalContext(record);
        if (original is null || await deletionCompletions.ObserveAsync(original, new(Id, record.Id), cancellationToken)
                is not ProjectDeletionCompletionObservation.Released) {
            throw new ProjectDeletionParticipantCleanupException(record.Id,
                "The exact deletion owner release is not confirmed. Retain the original recovery and reconcile its owner before retrying.");
        }
    }

    private ProjectDeletionPreparationContext? OriginalContext(AgentProjectStructureAccessRevocationRecord record)
        => AgentProjectDeletionBinding.Read(record, writeAdmissionService.DatabaseProfileId);

    private AgentProjectStructureRevocationTarget ResolveRevocationTarget(AgentProjectStructureAccessRevocationRecord record) {
        var original = OriginalContext(record) ?? throw new ProjectDeletionParticipantCleanupException(record.Id,
            "Legacy project-access cleanup requires explicit original-owner reconciliation.");
        return AgentProjectStructureRevocationTarget.ForLifetime(new(original.Project.DatabaseProfileId,
            original.Project.ProjectId, original.Project.LifetimeId));
    }

    private static SemaphoreSlim ResolveCompletionLock(Guid recoveryId)
    {
        var stripeIndex = (recoveryId.GetHashCode() & int.MaxValue) % CompletionLockStripeCount;
        return CompletionLockStripes[stripeIndex];
    }
}

public enum AgentProjectStructureAccessRevocationStatus
{
    Pending = 1,
    Processing = 2,
    Failed = 3,
    Completed = 4
}

public sealed class AgentProjectStructureAccessRevocationRecord
{
    internal static string PendingFilter => FormattableString.Invariant($"\"Status\" <> {(int)AgentProjectStructureAccessRevocationStatus.Completed}");

    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public Guid? DatabaseProfileId { get; set; }

    public Guid? ProjectLifetimeId { get; set; }

    public Guid? CompletionNamespaceId { get; set; }

    public Guid? CompletionId { get; set; }

    public AgentProjectStructureAccessRevocationStatus Status { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public int AttemptCount { get; set; }

    public DateTimeOffset? LastAttemptAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public string? LastFailureCode { get; set; }
}

internal sealed class AgentProjectStructureAccessRevocationRecordConfiguration
    : IEntityTypeConfiguration<AgentProjectStructureAccessRevocationRecord>
{
    public void Configure(EntityTypeBuilder<AgentProjectStructureAccessRevocationRecord> builder)
    {
        builder.ToTable("AgentFramework_ProjectAccessRevocations", table => {
            table.HasCheckConstraint("CK_AF_ProjectAccessRevocations_Lifetime",
                "(\"DatabaseProfileId\" IS NULL AND \"ProjectLifetimeId\" IS NULL) OR (\"DatabaseProfileId\" IS NOT NULL AND \"ProjectLifetimeId\" IS NOT NULL)");
            table.HasCheckConstraint("CK_AF_ProjectAccessRevocations_Completion",
                "(\"CompletionNamespaceId\" IS NULL AND \"CompletionId\" IS NULL) OR (\"CompletionNamespaceId\" IS NOT NULL AND \"CompletionId\" IS NOT NULL AND \"CompletionNamespaceId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"CompletionId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"DatabaseProfileId\" IS NOT NULL AND \"ProjectLifetimeId\" IS NOT NULL)");
        });
        builder.HasKey(record => record.Id);
        builder.HasIndex(record => record.Id, "IX_AF_ProjectAccessRevocations_Unfinished")
            .HasFilter(AgentProjectStructureAccessRevocationRecord.PendingFilter);
        builder.Property(record => record.Status).HasConversion<int>();
        builder.Property(record => record.LastFailureCode).HasMaxLength(256);
        builder.HasIndex(record => record.ProjectId)
            .IsUnique()
            .HasFilter("\"ProjectLifetimeId\" IS NULL")
            .HasDatabaseName("UX_AF_ProjectAccessRevocations_Project");
        builder.HasIndex(record => new { record.ProjectId, record.ProjectLifetimeId })
            .IsUnique()
            .HasDatabaseName("UX_AF_ProjectAccessRevocations_ProjectLifetime");
        builder.HasIndex(record => new { record.Status, record.CreatedAtUtc })
            .HasDatabaseName("IX_AF_ProjectAccessRevocations_Status");
    }
}
