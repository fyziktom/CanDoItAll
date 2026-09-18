using System.Data;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Infrastructure.ControlPlane;

public enum DatabaseTransferProfileMode { Independent, Serializable }

public sealed record DatabaseTransferProfileSession(Guid SessionId, Guid ProfileId, DatabaseTransferProfileMode Mode);

public sealed class DatabaseTransferOperationRunner(
    IProfileAppDbContextFactory contexts,
    DatabaseTransferOwnerSessionRunner owners,
    ProjectTransferTargetInspectionRunner inspections) {
    private readonly AsyncLocal<ProfileFrame?> currentProfile = new();

    public Task<TResult> RunIndependentAsync<TResult>(ResolvedDatabaseProfile profile,
        Func<DatabaseTransferProfileSession, CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default)
        => RunProfileAsync(profile, null, null, operation, cancellationToken);

    public Task<TResult> RunSerializableAsync<TResult>(ResolvedDatabaseProfile profile, IReadOnlyCollection<string> scopeKeys,
        Func<DatabaseTransferProfileSession, CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(scopeKeys);
        return RunProfileAsync(profile, scopeKeys.ToArray(), null, operation, cancellationToken);
    }

    public Task<TResult> RunExclusiveImportAsync<TResult>(ResolvedDatabaseProfile profile, IReadOnlyCollection<string> scopeKeys,
        IReadOnlyCollection<Type> entityTypes, Func<DatabaseTransferProfileSession, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(scopeKeys);
        ArgumentNullException.ThrowIfNull(entityTypes);
        return RunProfileAsync(profile, scopeKeys.ToArray(), entityTypes.ToArray(), operation, cancellationToken);
    }

    private async Task<TResult> RunProfileAsync<TResult>(ResolvedDatabaseProfile profile, IReadOnlyCollection<string>? scopeKeys,
        IReadOnlyCollection<Type>? exclusionTypes,
        Func<DatabaseTransferProfileSession, CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(operation);
        if (currentProfile.Value is not null) {
            throw new InvalidOperationException("A profile transfer operation is already active on this execution path.");
        }
        await using var maintenance = await contexts.CreateDbContextForProfileAsync(profile, cancellationToken);
        if (exclusionTypes is not null) {
            if (!maintenance.Database.IsNpgsql()) {
                throw new NotSupportedException("Project import exclusion locks require PostgreSQL.");
            }
            CoordinatedDatabaseTransaction.ForProfile(profile).RequireOwnerProfile(maintenance);
            return await DatabaseTransferImportScope.RunAsync(maintenance, scopeKeys!, ResolveTableNames(maintenance, exclusionTypes),
                transaction => RunActiveProfileAsync(profile, maintenance, DatabaseTransferProfileMode.Serializable,
                    operation, transaction.CommitAsync, cancellationToken), cancellationToken);
        }
        await using var mutation = scopeKeys is null ? null : await SerializableMutationScope.BeginAsync(maintenance, scopeKeys, cancellationToken);
        return await RunActiveProfileAsync(profile, maintenance,
            scopeKeys is null ? DatabaseTransferProfileMode.Independent : DatabaseTransferProfileMode.Serializable,
            operation, token => mutation?.CommitAsync(token) ?? Task.CompletedTask, cancellationToken);
    }

    private async Task<TResult> RunActiveProfileAsync<TResult>(ResolvedDatabaseProfile profile, DbContext maintenance,
        DatabaseTransferProfileMode mode, Func<DatabaseTransferProfileSession, CancellationToken, Task<TResult>> operation,
        Func<CancellationToken, Task> commit, CancellationToken cancellationToken) {
        using var session = new DatabaseTransferOwnerSession(profile, maintenance);
        var request = new DatabaseTransferProfileSession(Guid.NewGuid(), profile.Profile.Id, mode);
        var frame = new ProfileFrame(request, profile, maintenance, session);
        currentProfile.Value = frame;
        try {
            var result = await operation(request, cancellationToken);
            RequireProfile(request);
            await commit(cancellationToken);
            return result;
        } finally {
            frame.Retired = true;
            currentProfile.Value = null;
        }
    }

    public Task<TContext> CreateOwnerAsync<TContext>(DatabaseTransferProfileSession session,
        Func<DbContextOptions<TContext>, TContext> create, CancellationToken cancellationToken = default) where TContext : DbContext
        => RequireProfile(session).Owner.CreateAsync(create, cancellationToken);

    public async Task<TResult> InspectTargetAsync<TResult>(DatabaseTransferProfileSession session,
        Func<ProjectTransferTargetInspection, CancellationToken, Task<TResult>> inspect, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(inspect);
        var frame = RequireProfile(session);
        var mode = session.Mode == DatabaseTransferProfileMode.Serializable
            ? ProjectTransferTargetInspectionMode.Locked : ProjectTransferTargetInspectionMode.Independent;
        using var inspection = inspections.Begin(frame.Profile, frame.Maintenance, mode, out var request);
        var result = await inspect(request, cancellationToken);
        RequireProfile(session);
        return result;
    }

    internal static IReadOnlyList<string> ResolveTableNames(DbContext maintenance, IReadOnlyCollection<Type> entityTypes) {
        ArgumentNullException.ThrowIfNull(entityTypes);
        var sql = maintenance.GetService<ISqlGenerationHelper>();
        return entityTypes.Distinct().Select(type => {
            var mapping = maintenance.Model.FindEntityType(type)
                ?? throw new InvalidOperationException($"The canonical transfer model does not map '{type.FullName}'.");
            var table = mapping.GetTableName()
                ?? throw new InvalidOperationException($"The canonical transfer model has no table for '{type.FullName}'.");
            return sql.DelimitIdentifier(table, mapping.GetSchema());
        }).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    public async Task<DatabaseTransferItemResult> RunTransferAsync(DatabaseTransferOperation transfer,
        Func<DatabaseTransferOwnerRequest, CancellationToken, Task<DatabaseTransferItemResult>> operation,
        bool allowInMemoryTest = false, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(transfer);
        ArgumentNullException.ThrowIfNull(operation);
        if (transfer.SourceProfile.Profile.Id == transfer.TargetProfile.Profile.Id) {
            throw new InvalidOperationException("A transfer requires different source and target database profiles.");
        }
        await using var source = await contexts.CreateDbContextForProfileAsync(transfer.SourceProfile, cancellationToken);
        await using var target = await contexts.CreateDbContextForProfileAsync(transfer.TargetProfile, cancellationToken);
        if (allowInMemoryTest && source.Database.IsInMemory() && target.Database.IsInMemory()) {
            using var testScope = owners.BeginInMemoryTest(transfer, source, target, out var testRequest);
            return await operation(testRequest, cancellationToken);
        }
        if (!source.Database.IsNpgsql() || !target.Database.IsNpgsql()) {
            throw new InvalidOperationException("Transfer requires two PostgreSQL databases; mixed-provider transfer is unsupported.");
        }
        CoordinatedDatabaseTransaction.RequireDistinctPhysicalDatabases(transfer.SourceProfile, transfer.TargetProfile);
        await using var sourceTransaction = await source.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        await using var targetTransaction = await target.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        DatabaseTransferItemResult result;
        using (owners.Begin(transfer, source, target, out var request)) {
            result = await operation(request, cancellationToken);
        }
        if (result.Success) {
            await targetTransaction.CommitAsync(cancellationToken);
            await sourceTransaction.CommitAsync(cancellationToken);
        }
        return result;
    }

    private ProfileFrame RequireProfile(DatabaseTransferProfileSession request) {
        var frame = currentProfile.Value;
        if (frame is null || frame.Retired || frame.Request != request) {
            throw new InvalidOperationException("The profile transfer session is absent, mismatched or disposed.");
        }
        frame.Owner.RequireActive();
        return frame;
    }

    private sealed class ProfileFrame(DatabaseTransferProfileSession request, ResolvedDatabaseProfile profile,
        DbContext maintenance, DatabaseTransferOwnerSession owner) {
        public DatabaseTransferProfileSession Request { get; } = request;
        public ResolvedDatabaseProfile Profile { get; } = profile;
        public DbContext Maintenance { get; } = maintenance;
        public DatabaseTransferOwnerSession Owner { get; } = owner;
        public bool Retired { get; set; }
    }
}
