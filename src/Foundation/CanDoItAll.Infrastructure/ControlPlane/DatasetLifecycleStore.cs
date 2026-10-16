using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Infrastructure.ControlPlane;

public sealed class DatasetLifecycleStore(
    DbContextOptions<DatasetLifecycleDbContext> options,
    CoordinatedDatabaseTransaction transactions) {
    private const string ActivationScopeKey = "infrastructure:dataset-activation";

    public async Task<DatasetLifecycleSnapshot?> ReadAsync(CancellationToken cancellationToken = default) {
        await using var context = new DatasetLifecycleDbContext(options);
        var record = await context.Set<DatasetLifecycleRecord>().AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        return record is null ? null : Snapshot(record);
    }

    public async Task RequireActiveForMutationAsync(DatasetActivationBinding capturedBinding,
        CancellationToken cancellationToken = default) {
        await using var context = await CreateEnlistedAsync(cancellationToken);
        await RequireActiveForMutationAsync(context, capturedBinding, cancellationToken);
    }

    public async Task RequireActiveForMaintenanceAsync(DatasetActivationBinding capturedBinding,
        CancellationToken cancellationToken = default) {
        await using var context = await CreateEnlistedAsync(cancellationToken);
        await RequireActiveForMaintenanceAsync(context, capturedBinding, cancellationToken);
    }

    public static Task RequireActiveForMutationAsync(DbContext owner, DatasetActivationBinding capturedBinding,
        CancellationToken cancellationToken = default)
        => RequireActiveAsync(owner, capturedBinding, exclusive: false, cancellationToken);

    public static Task RequireActiveForMaintenanceAsync(DbContext owner, DatasetActivationBinding capturedBinding,
        CancellationToken cancellationToken = default)
        => RequireActiveAsync(owner, capturedBinding, exclusive: true, cancellationToken);

    public async Task StageActivationAsync(DatasetActivationPlan plan, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(plan);
        await using var context = await CreateEnlistedAsync(cancellationToken);
        RequirePostgreSqlTransaction(context);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({ActivationScopeKey}, 0))", cancellationToken);
        var current = await ReadGuardedAsync(context, exclusive: true, cancellationToken);
        if (current is { State: DatasetLifecycleState.Active } && current.Binding == plan.Binding) {
            return;
        }
        if (plan.Kind == DatasetActivationKind.Attach || current?.Binding != plan.ExpectedPreviousSqlBinding ||
                current is { State: not DatasetLifecycleState.Active }) {
            throw new InvalidOperationException("The SQL dataset activation no longer matches the prepared plan.");
        }
        var record = await context.Set<DatasetLifecycleRecord>().SingleOrDefaultAsync(cancellationToken);
        if (record is null) {
            record = new();
            context.Add(record);
        }
        record.NamespaceId = plan.Binding.Namespace.Value;
        record.MaintenanceId = plan.Binding.MaintenanceId;
        record.SchemaVersion = plan.Binding.SchemaVersion;
        record.GuardProtocol = plan.Binding.GuardProtocol;
        record.State = DatasetLifecycleState.Active;
        await context.SaveChangesAsync(cancellationToken);
    }

    private Task<DatasetLifecycleDbContext> CreateEnlistedAsync(CancellationToken cancellationToken)
        => transactions.CreateEnlistedAsync(options, static configured => new DatasetLifecycleDbContext(configured), cancellationToken);

    private static async Task RequireActiveAsync(DbContext owner, DatasetActivationBinding capturedBinding,
        bool exclusive, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(capturedBinding);
        var current = await ReadGuardedAsync(owner, exclusive, cancellationToken);
        if (current is not { State: DatasetLifecycleState.Active } || current.Binding != capturedBinding) {
            throw new DatasetActivationMismatchException(capturedBinding);
        }
    }

    private static async Task<DatasetLifecycleSnapshot?> ReadGuardedAsync(DbContext owner, bool exclusive,
        CancellationToken cancellationToken) {
        RequirePostgreSqlTransaction(owner);
        await using var command = owner.Database.GetDbConnection().CreateCommand();
        command.Transaction = owner.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "SELECT \"Id\", \"NamespaceId\", \"MaintenanceId\", \"SchemaVersion\", \"GuardProtocol\", \"State\" " +
            "FROM \"Infrastructure_DatasetLifecycle\" WHERE \"Id\" = 1 FOR " + (exclusive ? "UPDATE" : "SHARE");
        if (owner.Database.GetCommandTimeout() is { } timeout) {
            command.CommandTimeout = timeout;
        }
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) {
            return null;
        }
        return Snapshot(new() {
            Id = reader.GetInt32(0),
            NamespaceId = reader.GetGuid(1),
            MaintenanceId = reader.GetGuid(2),
            SchemaVersion = reader.GetInt32(3),
            GuardProtocol = (DatasetGuardProtocol)reader.GetInt32(4),
            State = (DatasetLifecycleState)reader.GetInt32(5)
        });
    }

    private static DatasetLifecycleSnapshot Snapshot(DatasetLifecycleRecord record) {
        if (record.Id != DatasetLifecycleRecord.SingletonId || !Enum.IsDefined(record.State)) {
            throw new InvalidOperationException("The SQL dataset lifecycle state is invalid or unsupported.");
        }
        return new(new(new DatasetNamespaceId(record.NamespaceId), record.MaintenanceId,
            record.SchemaVersion, record.GuardProtocol), record.State);
    }

    private static void RequirePostgreSqlTransaction(DbContext owner) {
        ArgumentNullException.ThrowIfNull(owner);
        if (!owner.Database.IsNpgsql() || owner.Database.CurrentTransaction is null) {
            throw new InvalidOperationException("Dataset lifecycle guards require an existing PostgreSQL owner transaction.");
        }
    }
}
