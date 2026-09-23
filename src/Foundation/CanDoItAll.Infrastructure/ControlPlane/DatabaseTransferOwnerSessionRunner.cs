using System.Data;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Infrastructure.ControlPlane;

public enum DatabaseTransferOwnerMode { PostgreSqlTransaction, InMemoryTest }

public sealed record DatabaseTransferOwnerRequest(Guid SessionId, Guid SourceProfileId, Guid TargetProfileId,
    DatabaseTransferOwnerMode Mode = DatabaseTransferOwnerMode.PostgreSqlTransaction);

public sealed class DatabaseTransferOwnerSessionRunner {
    private readonly AsyncLocal<Frame?> current = new();

    internal IDisposable Begin(DatabaseTransferOperation transfer, DbContext source, DbContext target, out DatabaseTransferOwnerRequest request) {
        ArgumentNullException.ThrowIfNull(transfer);
        if (current.Value is not null) {
            throw new InvalidOperationException("A transfer owner session is already active on this execution path.");
        }
        CoordinatedDatabaseTransaction.RequireDistinctPhysicalDatabases(transfer.SourceProfile, transfer.TargetProfile);
        if (source.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.RepeatableRead ||
                target.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable) {
            throw new InvalidOperationException("Transfer participants require the actual source snapshot and target Serializable transaction.");
        }
        return BeginCore(transfer, source, target, DatabaseTransferOwnerMode.PostgreSqlTransaction, out request);
    }

    internal IDisposable BeginInMemoryTest(DatabaseTransferOperation transfer, DbContext source, DbContext target, out DatabaseTransferOwnerRequest request) {
        ArgumentNullException.ThrowIfNull(transfer);
        if (current.Value is not null) {
            throw new InvalidOperationException("A transfer owner session is already active on this execution path.");
        }
        CoordinatedDatabaseTransaction.RequireDistinctInMemoryTestStores(source, target);
        return BeginCore(transfer, source, target, DatabaseTransferOwnerMode.InMemoryTest, out request);
    }

    private IDisposable BeginCore(DatabaseTransferOperation transfer, DbContext sourceContext, DbContext targetContext, DatabaseTransferOwnerMode mode, out DatabaseTransferOwnerRequest request) {
        var source = new DatabaseTransferOwnerSession(transfer.SourceProfile, sourceContext);
        DatabaseTransferOwnerSession target;
        try {
            target = new DatabaseTransferOwnerSession(transfer.TargetProfile, targetContext);
        } catch {
            source.Dispose();
            throw;
        }
        request = new(Guid.NewGuid(), transfer.SourceProfile.Profile.Id, transfer.TargetProfile.Profile.Id, mode);
        var frame = new Frame(request, source, target, targetContext);
        current.Value = frame;
        return new Scope(this, frame);
    }

    public Task<TContext> CreateSourceAsync<TContext>(DatabaseTransferOwnerRequest request,
        Func<DbContextOptions<TContext>, TContext> create, CancellationToken cancellationToken = default) where TContext : DbContext
        => RequireCurrent(request).Source.CreateAsync(create, cancellationToken);

    public Task<TContext> CreateTargetAsync<TContext>(DatabaseTransferOwnerRequest request,
        Func<DbContextOptions<TContext>, TContext> create, CancellationToken cancellationToken = default) where TContext : DbContext
        => RequireCurrent(request).Target.CreateAsync(create, cancellationToken);

    public async Task AcquireTargetTableLocksAsync(DatabaseTransferOwnerRequest request,
        IReadOnlyCollection<DatabaseTransferTable> tables, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(tables);
        cancellationToken.ThrowIfCancellationRequested();
        var frame = RequireCurrent(request);
        if (request.Mode == DatabaseTransferOwnerMode.InMemoryTest || tables.Count == 0) {
            return;
        }
        var known = frame.TargetMaintenance.Model.GetEntityTypes().Where(mapping => mapping.GetTableName() is not null)
            .Select(DatabaseTransferTable.From).ToHashSet();
        if (tables.Any(table => !known.Contains(table))) {
            throw new InvalidOperationException("A requested transfer lock is absent from the canonical maintenance model.");
        }
        var sql = frame.TargetMaintenance.GetService<ISqlGenerationHelper>();
        var names = tables.Distinct().Select(table => sql.DelimitIdentifier(table.Name, table.Schema)).Order(StringComparer.Ordinal).ToArray();
        var tableLockCommand = $"LOCK TABLE {string.Join(", ", names)} IN SHARE ROW EXCLUSIVE MODE";
        await frame.TargetMaintenance.Database.ExecuteSqlRawAsync(tableLockCommand, cancellationToken);
        RequireCurrent(request);
    }

    private Frame RequireCurrent(DatabaseTransferOwnerRequest request) {
        var frame = current.Value;
        if (frame is null || frame.Disposed || frame.Request != request) {
            throw new InvalidOperationException("The requested transfer session is absent, mismatched or disposed.");
        }
        frame.Source.RequireActive();
        frame.Target.RequireActive();
        return frame;
    }

    private void End(Frame frame) {
        if (frame.Disposed) {
            return;
        }
        if (!ReferenceEquals(current.Value, frame)) {
            throw new InvalidOperationException("The transfer session must end on its original execution path.");
        }
        frame.Target.Dispose();
        frame.Source.Dispose();
        frame.Disposed = true;
        current.Value = null;
    }

    private sealed class Scope(DatabaseTransferOwnerSessionRunner runner, Frame frame) : IDisposable {
        public void Dispose() => runner.End(frame);
    }

    private sealed class Frame(DatabaseTransferOwnerRequest request, DatabaseTransferOwnerSession source, DatabaseTransferOwnerSession target,
        DbContext targetMaintenance) {
        public DatabaseTransferOwnerRequest Request { get; } = request;
        public DatabaseTransferOwnerSession Source { get; } = source;
        public DatabaseTransferOwnerSession Target { get; } = target;
        public DbContext TargetMaintenance { get; } = targetMaintenance;
        public bool Disposed { get; set; }
    }
}
