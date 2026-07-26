using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public sealed class InMemoryWorkflowLaunchIdempotencyStore :
    IWorkflowLaunchIdempotencyStore,
    IWorkflowLaunchIdempotencyQueryStore
{
    private readonly object gate = new();
    private readonly Dictionary<WorkflowLaunchIdempotencyScope, ClaimEntry> claims = [];

    public Task<WorkflowLaunchIdempotencyClaimResult> TryClaimAsync(
        WorkflowLaunchIdempotencyScope scope,
        WorkflowLaunchRequestFingerprint fingerprint,
        WorkflowLaunchIdempotencyClaimToken claimToken,
        WorkflowRunId proposedRunId,
        DateTimeOffset claimedAtUtc,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        ValidateClaimWindow(claimedAtUtc, leaseExpiresAtUtc);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (!TryFindClaim(scope, out var storedScope, out var existing))
            {
                claims.Add(scope, ClaimEntry.Pending(
                    fingerprint,
                    claimToken,
                    proposedRunId,
                    claimedAtUtc,
                    leaseExpiresAtUtc));
                return Task.FromResult(new WorkflowLaunchIdempotencyClaimResult(
                    WorkflowLaunchIdempotencyClaimOutcome.Acquired,
                    proposedRunId));
            }

            ThrowIfPublicApiScopeConflicts(scope, storedScope);
            ThrowIfFingerprintConflicts(scope, fingerprint, existing.Fingerprint);
            if (existing.Completion is not null)
            {
                claims[storedScope] = existing with
                {
                    ReplayCount = existing.ReplayCount + 1,
                    LastReplayedAtUtc = claimedAtUtc
                };
                return Task.FromResult(new WorkflowLaunchIdempotencyClaimResult(
                    WorkflowLaunchIdempotencyClaimOutcome.Completed,
                    existing.ReservedRunId,
                    existing.Completion));
            }

            if (existing.LeaseExpiresAtUtc <= claimedAtUtc)
            {
                claims[storedScope] = existing with
                {
                    ClaimToken = claimToken,
                    LeaseExpiresAtUtc = leaseExpiresAtUtc
                };
                return Task.FromResult(new WorkflowLaunchIdempotencyClaimResult(
                    WorkflowLaunchIdempotencyClaimOutcome.Acquired,
                    existing.ReservedRunId));
            }

            return Task.FromResult(new WorkflowLaunchIdempotencyClaimResult(
                WorkflowLaunchIdempotencyClaimOutcome.InProgress));
        }
    }

    public Task<bool> TryRenewClaimAsync(
        WorkflowLaunchIdempotencyScope scope,
        WorkflowLaunchIdempotencyClaimToken claimToken,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (!TryFindClaim(scope, out var storedScope, out var existing) ||
                existing.Completion is not null ||
                existing.ClaimToken != claimToken)
            {
                return Task.FromResult(false);
            }

            claims[storedScope] = existing with { LeaseExpiresAtUtc = leaseExpiresAtUtc };
            return Task.FromResult(true);
        }
    }

    public Task<bool> TryCompleteClaimAsync(
        WorkflowLaunchIdempotencyScope scope,
        WorkflowLaunchIdempotencyClaimToken claimToken,
        WorkflowLaunchIdempotencyCompletion completion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(completion);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (!TryFindClaim(scope, out var storedScope, out var existing) ||
                existing.Completion is not null ||
                existing.ClaimToken != claimToken)
            {
                return Task.FromResult(false);
            }

            claims[storedScope] = existing with { Completion = completion };
            return Task.FromResult(true);
        }
    }

    public Task<bool> TryReleaseClaimAsync(
        WorkflowLaunchIdempotencyScope scope,
        WorkflowLaunchIdempotencyClaimToken claimToken,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (!TryFindClaim(scope, out var storedScope, out var existing) ||
                existing.Completion is not null ||
                existing.ClaimToken != claimToken)
            {
                return Task.FromResult(false);
            }

            claims.Remove(storedScope);
            return Task.FromResult(true);
        }
    }

    public Task<WorkflowLaunchIdempotencyRecord?> FindApiKeyAsync(
        WorkflowLaunchIdempotencyKey callerKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            var match = claims.FirstOrDefault(item =>
                item.Key.OriginKind == WorkflowLaunchOriginKind.Api &&
                item.Key.CallerKey == callerKey);
            if (match.Value is null)
            {
                return Task.FromResult<WorkflowLaunchIdempotencyRecord?>(null);
            }

            var entry = match.Value;
            return Task.FromResult<WorkflowLaunchIdempotencyRecord?>(new(
                match.Key,
                entry.Fingerprint,
                entry.ReservedRunId,
                entry.Completion is null
                    ? WorkflowLaunchIdempotencyRecordState.Pending
                    : WorkflowLaunchIdempotencyRecordState.Completed,
                entry.CreatedAtUtc,
                entry.Completion?.CompletedAtUtc,
                entry.ReplayCount,
                entry.LastReplayedAtUtc,
                entry.Completion));
        }
    }

    private bool TryFindClaim(
        WorkflowLaunchIdempotencyScope requestedScope,
        out WorkflowLaunchIdempotencyScope storedScope,
        out ClaimEntry entry)
    {
        if (requestedScope.OriginKind != WorkflowLaunchOriginKind.Api)
        {
            storedScope = requestedScope;
            return claims.TryGetValue(requestedScope, out entry!);
        }

        var match = claims.FirstOrDefault(item =>
            item.Key.OriginKind == WorkflowLaunchOriginKind.Api &&
            item.Key.CallerKey == requestedScope.CallerKey);
        storedScope = match.Key;
        entry = match.Value!;
        return entry is not null;
    }

    private static void ThrowIfPublicApiScopeConflicts(
        WorkflowLaunchIdempotencyScope requested,
        WorkflowLaunchIdempotencyScope existing)
    {
        if (requested.OriginKind != WorkflowLaunchOriginKind.Api)
        {
            return;
        }

        if (requested.WorkflowId != existing.WorkflowId ||
            requested.SelectionKind != existing.SelectionKind ||
            requested.RequestedVersionId != existing.RequestedVersionId ||
            requested.Mode != existing.Mode)
        {
            throw new WorkflowLaunchIdempotencyConflictException(requested);
        }
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
        WorkflowLaunchRequestFingerprint existing)
    {
        if (requested != existing)
        {
            throw new WorkflowLaunchIdempotencyConflictException(scope);
        }
    }

    private sealed record ClaimEntry(
        WorkflowLaunchRequestFingerprint Fingerprint,
        WorkflowLaunchIdempotencyClaimToken ClaimToken,
        WorkflowRunId ReservedRunId,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset LeaseExpiresAtUtc,
        WorkflowLaunchIdempotencyCompletion? Completion,
        int ReplayCount,
        DateTimeOffset? LastReplayedAtUtc)
    {
        public static ClaimEntry Pending(
            WorkflowLaunchRequestFingerprint fingerprint,
            WorkflowLaunchIdempotencyClaimToken claimToken,
            WorkflowRunId reservedRunId,
            DateTimeOffset createdAtUtc,
            DateTimeOffset leaseExpiresAtUtc) => new(
                fingerprint,
                claimToken,
                reservedRunId,
                createdAtUtc,
                leaseExpiresAtUtc,
                Completion: null,
                ReplayCount: 0,
                LastReplayedAtUtc: null);
    }
}
