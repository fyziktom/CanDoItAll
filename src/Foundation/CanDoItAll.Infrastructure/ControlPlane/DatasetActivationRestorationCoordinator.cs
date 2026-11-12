using System.Collections.Immutable;
using System.Data;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Infrastructure.ControlPlane;

public enum DatasetRestorationPrecondition { AllWriterHostsStopped = 1 }

public abstract record DatasetRestorationOutcome {
    private DatasetRestorationOutcome() { }
    public bool DiagnosticsUnavailable { get; init; }
    public sealed record Restored(DatasetActivationBinding Binding, Guid ReplacementPlanId,
        bool CleanupUnconfirmed = false) : DatasetRestorationOutcome;
    public sealed record Refused : DatasetRestorationOutcome;
    public sealed record Unconfirmed : DatasetRestorationOutcome;
}

public sealed class DatasetActivationRestorationCoordinator(Guid profileId, DatasetActivationService files,
    DbContextOptions<DatasetLifecycleDbContext> lifecycleOptions, FiniteOperationEpochStore epochs,
    CoordinatedDatabaseTransaction transactions, IEnumerable<FiniteOperationPoolRegistration> registrations,
    Func<CancellationToken, Task> requireOriginalProfile, ILogger<DatasetActivationRestorationCoordinator> logger) {
    private readonly ImmutableArray<FiniteOperationPoolRegistration> pools = Capture(registrations);

    public async Task<DatasetRestorationOutcome> RestorePreviousAsync(Guid expectedPlanId,
        DatasetNamespaceId expectedNamespace, DatasetRestorationPrecondition precondition,
        CancellationToken cancellationToken = default) {
        if (precondition != DatasetRestorationPrecondition.AllWriterHostsStopped) {
            return new DatasetRestorationOutcome.Refused();
        }
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(30));
        var token = budget.Token;
        DatasetActivationRestorationLease? lease = null;
        DatasetRestorationOutcome result = new DatasetRestorationOutcome.Refused();
        bool publicationAttempted = false;
        try {
            await requireOriginalProfile(token);
            lease = await files.AcquireRestorationAsync(profileId, expectedPlanId, expectedNamespace, token);
            var binding = lease.Selection.Plan.Binding;
            await using (var owner = new DatasetLifecycleDbContext(lifecycleOptions)) {
                await using var transaction = await owner.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
                using (transactions.Enter(owner)) {
                    await DatasetLifecycleStore.RequireActiveForMaintenanceAsync(owner, binding, token);
                    await epochs.RequireInstalledPoolSetEnlistedAsync(binding, binding.Namespace, pools, token);
                    await requireOriginalProfile(token);
                    publicationAttempted = true;
                    await lease.PublishPreparedAsync(token);
                }
                await transaction.CommitAsync(token);
            }
            await requireOriginalProfile(token);
            await lease.PublishActiveAsync(token);
            result = new DatasetRestorationOutcome.Restored(binding, lease.Selection.Plan.PlanId);
        } catch (Exception failure) {
            result = publicationAttempted ? new DatasetRestorationOutcome.Unconfirmed() : new DatasetRestorationOutcome.Refused();
            if (!Report(failure, profileId, expectedPlanId)) {
                result = result with { DiagnosticsUnavailable = true };
            }
        } finally {
            if (lease is not null) {
                try {
                    await lease.DisposeAsync();
                } catch (Exception release) {
                    result = result is DatasetRestorationOutcome.Restored restored
                        ? restored with { CleanupUnconfirmed = true }
                        : new DatasetRestorationOutcome.Unconfirmed { DiagnosticsUnavailable = result.DiagnosticsUnavailable };
                    if (!Report(release, profileId, expectedPlanId)) {
                        result = result with { DiagnosticsUnavailable = true };
                    }
                }
            }
        }
        return result;
    }

    private bool Report(Exception failure, Guid profileId, Guid planId) {
        try {
            logger.LogWarning("Previous dataset selection restoration failed for profile {ProfileId}, plan {PlanId}: {FailureType}.",
                profileId, planId, failure.GetType().Name);
            return true;
        } catch {
            return false;
        }
    }

    private static ImmutableArray<FiniteOperationPoolRegistration> Capture(IEnumerable<FiniteOperationPoolRegistration> registrations) {
        var captured = registrations.ToImmutableArray();
        if (captured.IsEmpty || captured.Any(pool => pool is null) || captured.Select(pool => pool.Pool).Distinct().Count() != captured.Length) {
            throw new ArgumentException("Restoration requires the exact installed finite operation pools.", nameof(registrations));
        }
        return captured;
    }
}
