using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class AgentProjectStructureAccessDeletionParticipant(
    IAgentFrameworkWorkspaceService workspaceService,
    IDbContextFactory<AppDbContext> dbContextFactory,
    TimeProvider timeProvider,
    ILogger<AgentProjectStructureAccessDeletionParticipant> logger)
    : IProjectDeletionParticipant
{
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
        AppDbContext dbContext,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("A project id is required.", nameof(projectId));
        }

        var trackedRecord = dbContext.Set<AgentProjectStructureAccessRevocationRecord>()
            .Local
            .SingleOrDefault(record => record.ProjectId == projectId);
        var record = trackedRecord ?? await dbContext
            .Set<AgentProjectStructureAccessRevocationRecord>()
            .SingleOrDefaultAsync(record => record.ProjectId == projectId, cancellationToken);
        if (record?.Status == AgentProjectStructureAccessRevocationStatus.Completed)
        {
            return null;
        }

        if (record is null)
        {
            var now = timeProvider.GetUtcNow();
            record = new AgentProjectStructureAccessRevocationRecord
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Status = AgentProjectStructureAccessRevocationStatus.Pending,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            await dbContext.Set<AgentProjectStructureAccessRevocationRecord>()
                .AddAsync(record, cancellationToken);
        }

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
        ProjectDeletionParticipantPreparation preparation,
        CancellationToken cancellationToken)
    {
        var record = await LoadRequiredRecordAsync(preparation, cancellationToken);
        if (record.Status == AgentProjectStructureAccessRevocationStatus.Completed)
        {
            return ProjectDeletionParticipantCompletion.Empty(preparation.RecoveryId);
        }

        try
        {
            if (!await TryMarkProcessingAsync(preparation, cancellationToken))
            {
                return ProjectDeletionParticipantCompletion.Empty(preparation.RecoveryId);
            }

            await workspaceService.RevokeProjectStructureAccessFromAllAgentsAsync(
                preparation.ProjectId,
                cancellationToken);
            await MarkCompletedAsync(preparation, cancellationToken);
            return ProjectDeletionParticipantCompletion.Empty(preparation.RecoveryId);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Agent Framework project-access cleanup failed for project {ProjectId} and recovery {RecoveryId}.",
                preparation.ProjectId,
                preparation.RecoveryId);
            var stateFailure = await TryMarkFailedAsync(preparation, exception);
            var cleanupFailure = stateFailure is null
                ? exception
                : new AggregateException(exception, stateFailure);
            throw new ProjectDeletionParticipantCleanupException(
                preparation.RecoveryId,
                $"Agent Framework project-access cleanup '{preparation.RecoveryId:D}' failed for project '{preparation.ProjectId:D}'.",
                cleanupFailure);
        }
    }

    public async Task<IReadOnlyList<ProjectDeletionParticipantRecovery>> ListPendingRecoveriesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var records = await dbContext.Set<AgentProjectStructureAccessRevocationRecord>()
            .AsNoTracking()
            .Where(record => record.Status != AgentProjectStructureAccessRevocationStatus.Completed)
            .OrderBy(record => record.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return records
            .Select(record => new ProjectDeletionParticipantRecovery(
                record.ProjectId,
                record.Id,
                MapRecoveryStatus(record.Status),
                CanRetryNow: true,
                RetryAvailableAtUtc: null,
                RetryGuidance: RetryGuidance))
            .ToArray();
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

    private async Task<bool> TryMarkProcessingAsync(
        ProjectDeletionParticipantPreparation preparation,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var affectedRows = await dbContext.Set<AgentProjectStructureAccessRevocationRecord>()
            .Where(record =>
                record.Id == preparation.RecoveryId &&
                record.ProjectId == preparation.ProjectId &&
                record.Status != AgentProjectStructureAccessRevocationStatus.Completed)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        record => record.Status,
                        AgentProjectStructureAccessRevocationStatus.Processing)
                    .SetProperty(record => record.AttemptCount, record => record.AttemptCount + 1)
                    .SetProperty(record => record.LastAttemptAtUtc, now)
                    .SetProperty(record => record.UpdatedAtUtc, now)
                    .SetProperty(record => record.LastFailureCode, (string?)null),
                cancellationToken);
        if (affectedRows > 0)
        {
            return true;
        }

        if (await IsCompletedAsync(preparation, cancellationToken))
        {
            return false;
        }

        throw new InvalidOperationException(
            $"Agent Framework project-access cleanup '{preparation.RecoveryId:D}' could not enter processing state.");
    }

    private async Task MarkCompletedAsync(
        ProjectDeletionParticipantPreparation preparation,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var completedAtUtc = timeProvider.GetUtcNow();
        var affectedRows = await dbContext.Set<AgentProjectStructureAccessRevocationRecord>()
            .Where(record =>
                record.Id == preparation.RecoveryId &&
                record.ProjectId == preparation.ProjectId &&
                record.Status != AgentProjectStructureAccessRevocationStatus.Completed)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        record => record.Status,
                        AgentProjectStructureAccessRevocationStatus.Completed)
                    .SetProperty(record => record.CompletedAtUtc, completedAtUtc)
                    .SetProperty(record => record.UpdatedAtUtc, completedAtUtc)
                    .SetProperty(record => record.LastFailureCode, (string?)null),
                cancellationToken);
        if (affectedRows == 0 &&
            !await IsCompletedAsync(preparation, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Agent Framework project-access cleanup '{preparation.RecoveryId:D}' could not enter completed state.");
        }
    }

    private async Task<Exception?> TryMarkFailedAsync(
        ProjectDeletionParticipantPreparation preparation,
        Exception failure)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(CancellationToken.None);
            var failedAtUtc = timeProvider.GetUtcNow();
            await dbContext.Set<AgentProjectStructureAccessRevocationRecord>()
                .Where(record =>
                    record.Id == preparation.RecoveryId &&
                    record.ProjectId == preparation.ProjectId &&
                    record.Status != AgentProjectStructureAccessRevocationStatus.Completed)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            record => record.Status,
                            AgentProjectStructureAccessRevocationStatus.Failed)
                        .SetProperty(record => record.UpdatedAtUtc, failedAtUtc)
                        .SetProperty(record => record.LastFailureCode, failure.GetType().Name),
                    CancellationToken.None);
            return null;
        }
        catch (Exception stateException)
        {
            logger.LogError(
                stateException,
                "Agent Framework project-access cleanup failure state could not be persisted for project {ProjectId} and recovery {RecoveryId}.",
                preparation.ProjectId,
                preparation.RecoveryId);
            return stateException;
        }
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
        builder.ToTable("AgentFramework_ProjectAccessRevocations");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Status).HasConversion<int>();
        builder.Property(record => record.LastFailureCode).HasMaxLength(256);
        builder.HasIndex(record => record.ProjectId)
            .IsUnique()
            .HasDatabaseName("UX_AF_ProjectAccessRevocations_Project");
        builder.HasIndex(record => new { record.Status, record.CreatedAtUtc })
            .HasDatabaseName("IX_AF_ProjectAccessRevocations_Status");
    }
}
