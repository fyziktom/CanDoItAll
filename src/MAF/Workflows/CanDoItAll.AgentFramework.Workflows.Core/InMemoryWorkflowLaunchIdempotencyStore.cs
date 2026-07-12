using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public sealed class InMemoryWorkflowLaunchIdempotencyStore : IWorkflowLaunchIdempotencyStore
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
            if (!claims.TryGetValue(scope, out var existing))
            {
                claims.Add(scope, ClaimEntry.Pending(
                    fingerprint,
                    claimToken,
                    proposedRunId,
                    leaseExpiresAtUtc));
                return Task.FromResult(new WorkflowLaunchIdempotencyClaimResult(
                    WorkflowLaunchIdempotencyClaimOutcome.Acquired,
                    proposedRunId));
            }

            ThrowIfFingerprintConflicts(scope, fingerprint, existing.Fingerprint);
            if (existing.Completion is not null)
            {
                return Task.FromResult(new WorkflowLaunchIdempotencyClaimResult(
                    WorkflowLaunchIdempotencyClaimOutcome.Completed,
                    existing.ReservedRunId,
                    existing.Completion));
            }

            if (existing.LeaseExpiresAtUtc <= claimedAtUtc)
            {
                claims[scope] = existing with
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
            if (!claims.TryGetValue(scope, out var existing) ||
                existing.Completion is not null ||
                existing.ClaimToken != claimToken)
            {
                return Task.FromResult(false);
            }

            claims[scope] = existing with { LeaseExpiresAtUtc = leaseExpiresAtUtc };
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
            if (!claims.TryGetValue(scope, out var existing) ||
                existing.Completion is not null ||
                existing.ClaimToken != claimToken)
            {
                return Task.FromResult(false);
            }

            claims[scope] = existing with { Completion = completion };
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
            if (!claims.TryGetValue(scope, out var existing) ||
                existing.Completion is not null ||
                existing.ClaimToken != claimToken)
            {
                return Task.FromResult(false);
            }

            claims.Remove(scope);
            return Task.FromResult(true);
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
        DateTimeOffset LeaseExpiresAtUtc,
        WorkflowLaunchIdempotencyCompletion? Completion)
    {
        public static ClaimEntry Pending(
            WorkflowLaunchRequestFingerprint fingerprint,
            WorkflowLaunchIdempotencyClaimToken claimToken,
            WorkflowRunId reservedRunId,
            DateTimeOffset leaseExpiresAtUtc) => new(
                fingerprint,
                claimToken,
                reservedRunId,
                leaseExpiresAtUtc,
                Completion: null);
    }
}
