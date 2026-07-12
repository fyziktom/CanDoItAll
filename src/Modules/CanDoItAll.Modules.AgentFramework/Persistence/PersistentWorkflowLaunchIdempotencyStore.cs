using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Npgsql;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class PersistentWorkflowLaunchIdempotencyStore(
    IDbContextFactory<AppDbContext> dbContextFactory) : IWorkflowLaunchIdempotencyStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<WorkflowLaunchIdempotencyClaimResult> TryClaimAsync(
        WorkflowLaunchIdempotencyScope scope,
        WorkflowLaunchRequestFingerprint fingerprint,
        WorkflowLaunchIdempotencyClaimToken claimToken,
        WorkflowRunId proposedRunId,
        DateTimeOffset claimedAtUtc,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        ValidateClaimWindow(claimedAtUtc, leaseExpiresAtUtc);
        while (true)
        {
            if (await TryInsertClaimAsync(
                    scope,
                    fingerprint,
                    claimToken,
                    proposedRunId,
                    claimedAtUtc,
                    leaseExpiresAtUtc,
                    cancellationToken))
            {
                return new WorkflowLaunchIdempotencyClaimResult(
                    WorkflowLaunchIdempotencyClaimOutcome.Acquired,
                    proposedRunId);
            }

            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var existing = await ScopeQuery(dbContext, scope)
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);
            if (existing is null)
            {
                continue;
            }

            ThrowIfFingerprintConflicts(scope, fingerprint, existing.Fingerprint);
            if (existing.State == WorkflowLaunchIdempotencyClaimState.Completed)
            {
                return Completed(existing);
            }

            if (existing.LeaseExpiresAtUtc > claimedAtUtc)
            {
                return new WorkflowLaunchIdempotencyClaimResult(
                    WorkflowLaunchIdempotencyClaimOutcome.InProgress,
                    existing.ReservedRunIdAsValue());
            }

            var affected = await ScopeQuery(dbContext, scope)
                .Where(record =>
                    record.State == WorkflowLaunchIdempotencyClaimState.Pending &&
                    record.Fingerprint == fingerprint.Value &&
                    record.LeaseExpiresAtUtc <= claimedAtUtc)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(record => record.ClaimToken, claimToken.Value)
                    .SetProperty(record => record.ClaimedAtUtc, claimedAtUtc)
                    .SetProperty(record => record.LeaseExpiresAtUtc, leaseExpiresAtUtc),
                    cancellationToken);
            if (affected == 1)
            {
                return new WorkflowLaunchIdempotencyClaimResult(
                    WorkflowLaunchIdempotencyClaimOutcome.Acquired,
                    existing.ReservedRunIdAsValue());
            }

            var current = await ScopeQuery(dbContext, scope)
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);
            if (current is null)
            {
                continue;
            }

            ThrowIfFingerprintConflicts(scope, fingerprint, current.Fingerprint);
            return current.State == WorkflowLaunchIdempotencyClaimState.Completed
                ? Completed(current)
                : new WorkflowLaunchIdempotencyClaimResult(
                    WorkflowLaunchIdempotencyClaimOutcome.InProgress,
                    current.ReservedRunIdAsValue());
        }
    }

    public async Task<bool> TryRenewClaimAsync(
        WorkflowLaunchIdempotencyScope scope,
        WorkflowLaunchIdempotencyClaimToken claimToken,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var affected = await ScopeQuery(dbContext, scope)
            .Where(record =>
                record.State == WorkflowLaunchIdempotencyClaimState.Pending &&
                record.ClaimToken == claimToken.Value)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(record => record.LeaseExpiresAtUtc, leaseExpiresAtUtc),
                cancellationToken);
        return affected == 1;
    }

    public async Task<bool> TryCompleteClaimAsync(
        WorkflowLaunchIdempotencyScope scope,
        WorkflowLaunchIdempotencyClaimToken claimToken,
        WorkflowLaunchIdempotencyCompletion completion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(completion);
        var completionJson = JsonSerializer.Serialize(completion, JsonOptions);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var affected = await ScopeQuery(dbContext, scope)
            .Where(record =>
                record.State == WorkflowLaunchIdempotencyClaimState.Pending &&
                record.ClaimToken == claimToken.Value)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(record => record.State, WorkflowLaunchIdempotencyClaimState.Completed)
                .SetProperty(record => record.CompletionJson, completionJson)
                .SetProperty(record => record.CompletedAtUtc, completion.CompletedAtUtc),
                cancellationToken);
        return affected == 1;
    }

    public async Task<bool> TryReleaseClaimAsync(
        WorkflowLaunchIdempotencyScope scope,
        WorkflowLaunchIdempotencyClaimToken claimToken,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var affected = await ScopeQuery(dbContext, scope)
            .Where(record =>
                record.State == WorkflowLaunchIdempotencyClaimState.Pending &&
                record.ClaimToken == claimToken.Value)
            .ExecuteDeleteAsync(cancellationToken);
        return affected == 1;
    }

    private async Task<bool> TryInsertClaimAsync(
        WorkflowLaunchIdempotencyScope scope,
        WorkflowLaunchRequestFingerprint fingerprint,
        WorkflowLaunchIdempotencyClaimToken claimToken,
        WorkflowRunId proposedRunId,
        DateTimeOffset claimedAtUtc,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.Set<WorkflowLaunchIdempotencyRecordEntity>().Add(
            WorkflowLaunchIdempotencyRecordEntity.CreatePending(
                scope,
                fingerprint,
                claimToken,
                proposedRunId,
                claimedAtUtc,
                leaseExpiresAtUtc));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return false;
        }
    }

    private static IQueryable<WorkflowLaunchIdempotencyRecordEntity> ScopeQuery(
        AppDbContext dbContext,
        WorkflowLaunchIdempotencyScope scope)
    {
        var requestedVersionId = scope.RequestedVersionId?.Value ?? Guid.Empty;
        return dbContext.Set<WorkflowLaunchIdempotencyRecordEntity>()
            .Where(record =>
                record.CallerKey == scope.CallerKey.Value &&
                record.WorkflowId == scope.WorkflowId.Value &&
                record.SelectionKind == scope.SelectionKind &&
                record.RequestedVersionId == requestedVersionId &&
                record.Mode == scope.Mode &&
                record.OriginKind == scope.OriginKind &&
                record.OriginScopeKey == scope.OriginScopeKey.Value);
    }

    private static WorkflowLaunchIdempotencyClaimResult Completed(
        WorkflowLaunchIdempotencyRecordEntity record)
    {
        if (string.IsNullOrWhiteSpace(record.CompletionJson))
        {
            throw new InvalidOperationException(
                $"Completed workflow launch idempotency record '{record.Id}' has no completion payload.");
        }

        var completion = JsonSerializer.Deserialize<WorkflowLaunchIdempotencyCompletion>(
                record.CompletionJson,
                JsonOptions)
            ?? throw new InvalidOperationException(
                $"Completed workflow launch idempotency record '{record.Id}' could not be deserialized.");
        return new WorkflowLaunchIdempotencyClaimResult(
            WorkflowLaunchIdempotencyClaimOutcome.Completed,
            record.ReservedRunIdAsValue(),
            completion);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return true;
            }
        }

        return false;
    }

    private static void ValidateClaimWindow(
        DateTimeOffset claimedAtUtc,
        DateTimeOffset leaseExpiresAtUtc)
    {
        if (leaseExpiresAtUtc <= claimedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(leaseExpiresAtUtc),
                "Workflow launch idempotency lease must expire after it is claimed.");
        }
    }

    private static void ThrowIfFingerprintConflicts(
        WorkflowLaunchIdempotencyScope scope,
        WorkflowLaunchRequestFingerprint requested,
        string existing)
    {
        if (!string.Equals(requested.Value, existing, StringComparison.Ordinal))
        {
            throw new WorkflowLaunchIdempotencyConflictException(scope);
        }
    }
}

public sealed class WorkflowLaunchIdempotencyRecordEntity
{
    public Guid Id { get; set; }

    public string CallerKey { get; set; } = string.Empty;

    public Guid WorkflowId { get; set; }

    public WorkflowDefinitionSelectionKind SelectionKind { get; set; }

    public Guid RequestedVersionId { get; set; }

    public WorkflowLaunchMode Mode { get; set; }

    public WorkflowLaunchOriginKind OriginKind { get; set; }

    public string OriginScopeKey { get; set; } = string.Empty;

    public string Fingerprint { get; set; } = string.Empty;

    public WorkflowLaunchIdempotencyClaimState State { get; set; }

    public Guid ClaimToken { get; set; }

    public Guid ReservedRunId { get; set; }

    public DateTimeOffset ClaimedAtUtc { get; set; }

    public DateTimeOffset LeaseExpiresAtUtc { get; set; }

    public string CompletionJson { get; set; } = string.Empty;

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public static WorkflowLaunchIdempotencyRecordEntity CreatePending(
        WorkflowLaunchIdempotencyScope scope,
        WorkflowLaunchRequestFingerprint fingerprint,
        WorkflowLaunchIdempotencyClaimToken claimToken,
        WorkflowRunId proposedRunId,
        DateTimeOffset claimedAtUtc,
        DateTimeOffset leaseExpiresAtUtc) => new()
        {
            Id = Guid.NewGuid(),
            CallerKey = scope.CallerKey.Value,
            WorkflowId = scope.WorkflowId.Value,
            SelectionKind = scope.SelectionKind,
            RequestedVersionId = scope.RequestedVersionId?.Value ?? Guid.Empty,
            Mode = scope.Mode,
            OriginKind = scope.OriginKind,
            OriginScopeKey = scope.OriginScopeKey.Value,
            Fingerprint = fingerprint.Value,
            State = WorkflowLaunchIdempotencyClaimState.Pending,
            ClaimToken = claimToken.Value,
            ReservedRunId = proposedRunId.Value,
            ClaimedAtUtc = claimedAtUtc,
            LeaseExpiresAtUtc = leaseExpiresAtUtc
        };

    public WorkflowRunId ReservedRunIdAsValue() => new(ReservedRunId);
}

public enum WorkflowLaunchIdempotencyClaimState
{
    Pending,
    Completed
}

internal sealed class WorkflowLaunchIdempotencyRecordEntityConfiguration :
    IEntityTypeConfiguration<WorkflowLaunchIdempotencyRecordEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowLaunchIdempotencyRecordEntity> builder)
    {
        builder.ToTable("AgentFramework_WorkflowLaunchIdempotency");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.CallerKey).HasMaxLength(256).IsRequired();
        builder.Property(record => record.SelectionKind).HasConversion<int>();
        builder.Property(record => record.Mode).HasConversion<int>();
        builder.Property(record => record.OriginKind).HasConversion<int>();
        builder.Property(record => record.OriginScopeKey).HasMaxLength(64).IsRequired();
        builder.Property(record => record.Fingerprint).HasMaxLength(64).IsRequired();
        builder.Property(record => record.State).HasConversion<int>();
        builder.Property(record => record.CompletionJson).HasColumnType("TEXT");
        builder.HasIndex(record => new
            {
                record.CallerKey,
                record.WorkflowId,
                record.SelectionKind,
                record.RequestedVersionId,
                record.Mode,
                record.OriginKind,
                record.OriginScopeKey
            })
            .IsUnique()
            .HasDatabaseName("UX_AF_WorkflowLaunchIdempotency_Scope");
        builder.HasIndex(record => new { record.State, record.LeaseExpiresAtUtc })
            .HasDatabaseName("IX_AF_WorkflowLaunchIdempotency_Lease");
        builder.HasIndex(record => record.ReservedRunId)
            .IsUnique()
            .HasDatabaseName("UX_AF_WorkflowLaunchIdempotency_Run");
    }
}
