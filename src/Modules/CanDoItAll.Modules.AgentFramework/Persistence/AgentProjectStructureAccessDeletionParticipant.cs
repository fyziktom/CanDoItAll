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
    ProjectWriteAdmissionService writeAdmissionService)
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

    public IReadOnlyCollection<ProjectDeletionPreparationScopeKey> PreparationScopeKeys { get; } = [];

    public async Task<ProjectDeletionParticipantPreparation?> PrepareAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("A project id is required.", nameof(projectId));
        }

        var admission = await writeAdmissionService.CaptureForMutationAsync(projectId, cancellationToken);
        var lifetimeId = admission?.LifetimeId;
        await using var dbContext = await coordinatedTransaction.CreateEnlistedAsync(
            contextOptions, static options => new AgentProjectAccessDbContext(options), cancellationToken);
        var record = await dbContext.Set<AgentProjectStructureAccessRevocationRecord>()
            .SingleOrDefaultAsync(record => record.ProjectId == projectId && record.ProjectLifetimeId == lifetimeId, cancellationToken);
        if (record is not null && record.DatabaseProfileId != admission?.DatabaseProfileId) {
            throw new InvalidOperationException("The stored project-access revocation belongs to another database profile.");
        }
        if (record?.Status == AgentProjectStructureAccessRevocationStatus.Completed) {
            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        if (record is null)
        {
            var now = timeProvider.GetUtcNow();
            record = new AgentProjectStructureAccessRevocationRecord
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                DatabaseProfileId = admission?.DatabaseProfileId,
                ProjectLifetimeId = lifetimeId,
                Status = AgentProjectStructureAccessRevocationStatus.Pending,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            await dbContext.Set<AgentProjectStructureAccessRevocationRecord>()
                .AddAsync(record, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new ProjectDeletionParticipantPreparation(projectId, record.Id);
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
        return records.Select(record => {
            var lastOwnershipAt = record.LastAttemptAtUtc is { } attempted && attempted > record.UpdatedAtUtc
                ? attempted : record.UpdatedAtUtc;
            var retryAt = lastOwnershipAt + options.LeaseDuration;
            var active = record.Status == AgentProjectStructureAccessRevocationStatus.Processing && retryAt > now;
            return new ProjectDeletionParticipantRecovery(record.ProjectId, record.Id, MapRecoveryStatus(record.Status),
                CanRetryNow: !active, RetryAvailableAtUtc: active ? retryAt : null, RetryGuidance: RetryGuidance);
        }).ToArray();
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

    private AgentProjectStructureRevocationTarget ResolveRevocationTarget(AgentProjectStructureAccessRevocationRecord record) {
        if (record.DatabaseProfileId is null && record.ProjectLifetimeId is null) {
            return AgentProjectStructureRevocationTarget.UnboundLegacy(writeAdmissionService.DatabaseProfileId, record.ProjectId);
        }
        if (record.DatabaseProfileId != writeAdmissionService.DatabaseProfileId || record.ProjectLifetimeId is not { } lifetimeId) {
            throw new InvalidOperationException("The stored project-access revocation does not belong to the canonical project lifetime profile.");
        }
        return AgentProjectStructureRevocationTarget.ForLifetime(new(record.DatabaseProfileId.Value, record.ProjectId, lifetimeId));
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
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public Guid? DatabaseProfileId { get; set; }

    public Guid? ProjectLifetimeId { get; set; }

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
        builder.ToTable("AgentFramework_ProjectAccessRevocations", table => table.HasCheckConstraint(
            "CK_AF_ProjectAccessRevocations_Lifetime",
            "(\"DatabaseProfileId\" IS NULL AND \"ProjectLifetimeId\" IS NULL) OR (\"DatabaseProfileId\" IS NOT NULL AND \"ProjectLifetimeId\" IS NOT NULL)"));
        builder.HasKey(record => record.Id);
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
