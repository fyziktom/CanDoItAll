using CanDoItAll.Infrastructure.ControlPlane;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Infrastructure.Persistence;

public sealed class ProjectTransferTargetInspectionRunner {
    private readonly AsyncLocal<Frame?> current = new();

    internal IDisposable Begin(ResolvedDatabaseProfile profile, DbContext owner, ProjectTransferTargetInspectionMode mode,
        out ProjectTransferTargetInspection request) {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(owner);
        if (profile.Profile.Id == Guid.Empty || !Enum.IsDefined(mode)) {
            throw new ArgumentException("A valid target profile and inspection mode are required.");
        }
        if (current.Value is not null) {
            throw new InvalidOperationException("A target inspection is already active on this execution path.");
        }

        var extensions = owner.GetService<IDbContextOptions>().Extensions.ToDictionary(extension => extension.GetType());
        if (extensions.Values.OfType<CoreOptionsExtension>().SingleOrDefault()?.Model is not null) {
            throw new InvalidOperationException("Target inspection requires owner models, without a supplied global model.");
        }
        var transactions = CoordinatedDatabaseTransaction.ForProfile(profile);
        transactions.RequireOwnerProfile(owner);
        IDisposable? participation = null;
        if (mode == ProjectTransferTargetInspectionMode.Locked) {
            if (!owner.Database.IsNpgsql() || profile.Profile.ProviderKind != DatabaseProviderKind.PostgreSql) {
                throw new NotSupportedException("Locked target inspection requires an actual PostgreSQL transaction.");
            }
            participation = transactions.Enter(owner);
        } else if (profile.Profile.ProviderKind == DatabaseProviderKind.InMemory && owner.Database.IsInMemory()) {
            participation = transactions.Enter(owner);
        } else if (profile.Profile.ProviderKind == DatabaseProviderKind.PostgreSql && owner.Database.IsNpgsql()) {
            if (owner.Database.CurrentTransaction is not null) {
                throw new InvalidOperationException("Independent inspection requires the supplied target profile without an active transaction.");
            }
        } else {
            throw new InvalidOperationException("The inspection context does not use the supplied target profile provider.");
        }

        request = new(Guid.NewGuid(), profile.Profile.Id, mode);
        var frame = new Frame(request, owner, owner.Database.CurrentTransaction, extensions, transactions, participation);
        current.Value = frame;
        return new InspectionScope(this, frame);
    }

    public async Task<TResult> ReadOwnerAsync<TContext, TResult>(ProjectTransferTargetInspection request,
        Func<DbContextOptions<TContext>, TContext> create, Func<TContext, CancellationToken, Task<TResult>> read,
        CancellationToken cancellationToken = default) where TContext : DbContext {
        ArgumentNullException.ThrowIfNull(create);
        ArgumentNullException.ThrowIfNull(read);
        var frame = RequireCurrent(request);
        var options = new DbContextOptions<TContext>(frame.Options);
        await using var context = frame.Participation is not null
            ? await frame.Transactions.CreateEnlistedAsync(options, create, cancellationToken)
            : create(options);
        var result = await read(context, cancellationToken);
        RequireCurrent(request);
        return result;
    }

    private Frame RequireCurrent(ProjectTransferTargetInspection request) {
        var frame = current.Value;
        if (frame is null || frame.Retired || frame.Request != request) {
            throw new InvalidOperationException("The requested target inspection is absent, mismatched or disposed.");
        }
        _ = frame.Owner.Model;
        if (frame.Request.Mode == ProjectTransferTargetInspectionMode.Locked &&
            (frame.Owner.Database.CurrentTransaction is null || !ReferenceEquals(frame.Owner.Database.CurrentTransaction, frame.Transaction))) {
            throw new InvalidOperationException("The target inspection no longer owns its original transaction.");
        }
        return frame;
    }

    private void End(Frame frame) {
        if (frame.Retired) {
            return;
        }
        if (!ReferenceEquals(current.Value, frame)) {
            throw new InvalidOperationException("The target inspection must end on its original execution path.");
        }
        frame.Participation?.Dispose();
        frame.Retired = true;
        current.Value = null;
    }

    private sealed class InspectionScope(ProjectTransferTargetInspectionRunner runner, Frame frame) : IDisposable {
        public void Dispose() => runner.End(frame);
    }

    private sealed class Frame(ProjectTransferTargetInspection request, DbContext owner, IDbContextTransaction? transaction,
        IReadOnlyDictionary<Type, IDbContextOptionsExtension> options, CoordinatedDatabaseTransaction transactions, IDisposable? participation) {
        public ProjectTransferTargetInspection Request { get; } = request;
        public DbContext Owner { get; } = owner;
        public IDbContextTransaction? Transaction { get; } = transaction;
        public IReadOnlyDictionary<Type, IDbContextOptionsExtension> Options { get; } = options;
        public CoordinatedDatabaseTransaction Transactions { get; } = transactions;
        public IDisposable? Participation { get; } = participation;
        public bool Retired { get; set; }
    }
}
