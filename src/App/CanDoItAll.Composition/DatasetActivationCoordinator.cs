using System.Data;
using System.Collections.Immutable;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Composition;

public sealed class DatasetActivationCoordinator(DatasetActivationService files,
    IEnumerable<FiniteOperationPoolRegistration> registrations) {
    private readonly ImmutableArray<FiniteOperationPoolRegistration> pools = CaptureRegistrations(registrations);

    public async Task<DatasetActivationBinding> EnsureReadyAsync(ResolvedDatabaseProfile profile,
        CancellationToken cancellationToken = default) {
        RequirePostgreSql(profile);
        using var lease = files.Acquire(profile.Profile.Id, cancellationToken);
        var local = await lease.ReadAsync(cancellationToken);
        var stores = CreateStores(profile);
        var sql = await stores.Lifecycle.ReadAsync(cancellationToken);
        if (local is null && sql is null) {
            var binding = NewBinding(Guid.NewGuid());
            var plan = new DatasetActivationPlan(Guid.NewGuid(), DatasetActivationKind.Bootstrap, binding, null);
            await lease.WritePreparedAsync(plan, cancellationToken);
            await ApplyAsync(stores, lease, plan, cancellationToken);
            return binding;
        }
        if (local is not { State: DatasetActivationFileState.Active } || sql is not { State: DatasetLifecycleState.Active } ||
                local.Plan.Binding != sql.Binding) {
            throw new InvalidOperationException("Dataset readiness requires matching active file and SQL bindings. Use explicit offline attach or activation recovery.");
        }
        await RequireReadyAsync(stores, sql.Binding, cancellationToken);
        return sql.Binding;
    }

    public async Task<DatasetActivationBinding> AttachAsync(ResolvedDatabaseProfile profile,
        CancellationToken cancellationToken = default) {
        RequirePostgreSql(profile);
        using var lease = files.Acquire(profile.Profile.Id, cancellationToken);
        var local = await lease.ReadAsync(cancellationToken);
        var stores = CreateStores(profile);
        var sql = await stores.Lifecycle.ReadAsync(cancellationToken);
        if (sql is not { State: DatasetLifecycleState.Active }) {
            throw new InvalidOperationException("Attachment requires an existing active SQL dataset.");
        }
        if (local is not null) {
            if (local.State == DatasetActivationFileState.Active && local.Plan.Binding == sql.Binding) {
                await RequireReadyAsync(stores, sql.Binding, cancellationToken);
                return sql.Binding;
            }
            throw new InvalidOperationException("This profile already has an activation record. Resume its exact plan or resolve the mismatch before attachment.");
        }
        var plan = new DatasetActivationPlan(Guid.NewGuid(), DatasetActivationKind.Attach, sql.Binding, sql.Binding);
        await lease.WritePreparedAsync(plan, cancellationToken);
        await ApplyAsync(stores, lease, plan, cancellationToken);
        return plan.Binding;
    }

    public async Task<DatasetActivationBinding> ReactivateAsync(ResolvedDatabaseProfile profile,
        DatasetNamespaceId expectedNamespace, Guid maintenanceId, CancellationToken cancellationToken = default) {
        RequirePostgreSql(profile);
        ArgumentNullException.ThrowIfNull(expectedNamespace);
        if (maintenanceId == Guid.Empty) {
            throw new ArgumentException("Reactivation requires an explicit nonempty maintenance identity.", nameof(maintenanceId));
        }
        using var lease = files.Acquire(profile.Profile.Id, cancellationToken);
        var local = await lease.ReadAsync(cancellationToken);
        if (local is { State: DatasetActivationFileState.Prepared }) {
            throw new InvalidOperationException("An activation is already prepared. Resume that exact plan before starting another.");
        }
        var stores = CreateStores(profile);
        var sql = await stores.Lifecycle.ReadAsync(cancellationToken);
        if (sql is not { State: DatasetLifecycleState.Active } || sql.Binding.Namespace != expectedNamespace) {
            throw new InvalidOperationException("The restored SQL dataset does not match the explicitly expected previous namespace.");
        }
        var plan = new DatasetActivationPlan(Guid.NewGuid(), DatasetActivationKind.Reactivate, NewBinding(maintenanceId), sql.Binding);
        await lease.WritePreparedAsync(plan, cancellationToken);
        await ApplyAsync(stores, lease, plan, cancellationToken);
        return plan.Binding;
    }

    public async Task<DatasetActivationBinding> AttachAfterSupersededBootstrapAsync(ResolvedDatabaseProfile profile,
        Guid expectedPlanId, DatasetNamespaceId expectedSqlNamespace, CancellationToken cancellationToken = default) {
        RequirePostgreSql(profile);
        ArgumentNullException.ThrowIfNull(expectedSqlNamespace);
        if (expectedPlanId == Guid.Empty) {
            throw new ArgumentException("Superseded bootstrap recovery requires the exact saved plan identity.", nameof(expectedPlanId));
        }
        using var lease = files.Acquire(profile.Profile.Id, cancellationToken);
        var local = await lease.ReadAsync(cancellationToken);
        if (local is not { State: DatasetActivationFileState.Prepared } || local.Plan.PlanId != expectedPlanId ||
                local.Plan.Kind != DatasetActivationKind.Bootstrap || local.Plan.ExpectedPreviousSqlBinding is not null) {
            throw new InvalidOperationException("Explicit superseded-bootstrap attachment requires the exact Prepared bootstrap plan.");
        }
        var stores = CreateStores(profile);
        var sql = await stores.Lifecycle.ReadAsync(cancellationToken);
        if (sql is not { State: DatasetLifecycleState.Active } || sql.Binding.Namespace != expectedSqlNamespace ||
                sql.Binding.Namespace == local.Plan.Binding.Namespace) {
            throw new InvalidOperationException("The active SQL dataset must match the expected namespace and differ from the superseded bootstrap.");
        }
        var attachment = new DatasetActivationPlan(Guid.NewGuid(), DatasetActivationKind.Attach, sql.Binding, sql.Binding);
        await lease.ReplaceSupersededBootstrapAsync(expectedPlanId, attachment, cancellationToken);
        await ApplyAsync(stores, lease, attachment, cancellationToken);
        return attachment.Binding;
    }

    public async Task<DatasetActivationBinding> ResumeAsync(ResolvedDatabaseProfile profile,
        CancellationToken cancellationToken = default) {
        RequirePostgreSql(profile);
        using var lease = files.Acquire(profile.Profile.Id, cancellationToken);
        var local = await lease.ReadAsync(cancellationToken)
            ?? throw new InvalidOperationException("There is no saved activation plan to resume.");
        var stores = CreateStores(profile);
        if (local.State == DatasetActivationFileState.Active) {
            var sql = await stores.Lifecycle.ReadAsync(cancellationToken);
            if (sql is not { State: DatasetLifecycleState.Active } || sql.Binding != local.Plan.Binding) {
                throw new InvalidOperationException("The completed activation no longer matches SQL. Resume cannot mint a replacement identity.");
            }
            await RequireReadyAsync(stores, sql.Binding, cancellationToken);
            return sql.Binding;
        }
        await ApplyAsync(stores, lease, local.Plan, cancellationToken);
        return local.Plan.Binding;
    }

    private async Task ApplyAsync(ActivationStores stores, DatasetActivationLease lease,
        DatasetActivationPlan plan, CancellationToken cancellationToken) {
        await using (var owner = new DatasetLifecycleDbContext(stores.LifecycleOptions)) {
            await using var transaction = await owner.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            using (stores.Transactions.Enter(owner)) {
                var staged = await stores.Lifecycle.StageActivationAsync(plan, cancellationToken);
                if (staged == DatasetActivationStageResult.Staged) {
                    if (plan.Kind == DatasetActivationKind.Reactivate) {
                        await stores.Epochs.RequireInstalledPoolSetEnlistedAsync(plan.Binding,
                            plan.ExpectedPreviousSqlBinding!.Namespace, pools, cancellationToken);
                    }
                    await stores.Epochs.RequireUnissuedNamespaceEnlistedAsync(plan.Binding, pools,
                        plan.Kind == DatasetActivationKind.Reactivate ? plan.ExpectedPreviousSqlBinding!.Namespace : null, cancellationToken);
                    foreach (var pool in pools) {
                        if (plan.Kind == DatasetActivationKind.Reactivate) {
                            await stores.Epochs.CloseForReactivationEnlistedAsync(plan.ExpectedPreviousSqlBinding!, plan.Binding, pool, cancellationToken);
                        }
                        await stores.Epochs.InitializeNewNamespaceEnlistedAsync(plan.Binding, pool, cancellationToken);
                    }
                }
                await RequireReadyEnlistedAsync(stores, plan.Binding, cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        await lease.MarkActiveAsync(plan, cancellationToken);
    }

    private ActivationStores CreateStores(ResolvedDatabaseProfile profile) {
        var lifecycleOptions = new DbContextOptionsBuilder<DatasetLifecycleDbContext>();
        AppDbContextOptionsConfigurator.Configure(lifecycleOptions, profile);
        var metadataOptions = new DbContextOptionsBuilder<FiniteOperationDbContext>();
        AppDbContextOptionsConfigurator.Configure(metadataOptions, profile);
        var transactions = CoordinatedDatabaseTransaction.ForProfile(profile);
        return new(lifecycleOptions.Options, transactions, new(lifecycleOptions.Options, transactions),
            new(metadataOptions.Options, transactions));
    }

    private async Task RequireReadyAsync(ActivationStores stores, DatasetActivationBinding binding, CancellationToken cancellationToken) {
        await using var owner = new DatasetLifecycleDbContext(stores.LifecycleOptions);
        await using var transaction = await owner.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        using (stores.Transactions.Enter(owner)) {
            await RequireReadyEnlistedAsync(stores, binding, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private Task RequireReadyEnlistedAsync(ActivationStores stores, DatasetActivationBinding binding,
        CancellationToken cancellationToken)
        => stores.Epochs.RequireInstalledPoolSetEnlistedAsync(binding, binding.Namespace, pools, cancellationToken);

    private static ImmutableArray<FiniteOperationPoolRegistration> CaptureRegistrations(IEnumerable<FiniteOperationPoolRegistration> registrations) {
        var captured = registrations.ToImmutableArray();
        if (captured.IsEmpty || captured.Any(pool => pool is null) || captured.Select(pool => pool.Pool).Distinct().Count() != captured.Length) {
            throw new InvalidOperationException("Dataset readiness requires the exact installed finite operation pools.");
        }
        return captured;
    }

    private static DatasetActivationBinding NewBinding(Guid maintenanceId)
        => new(new(Guid.NewGuid()), maintenanceId, DatasetLifecycleRecord.CurrentSchemaVersion, DatasetGuardProtocol.SharedRows);

    private static void RequirePostgreSql(ResolvedDatabaseProfile profile) {
        if (profile.Profile.ProviderKind != DatabaseProviderKind.PostgreSql) {
            throw new NotSupportedException("Dataset activation requires PostgreSQL.");
        }
    }

    private sealed record ActivationStores(DbContextOptions<DatasetLifecycleDbContext> LifecycleOptions,
        CoordinatedDatabaseTransaction Transactions, DatasetLifecycleStore Lifecycle, FiniteOperationEpochStore Epochs);
}
