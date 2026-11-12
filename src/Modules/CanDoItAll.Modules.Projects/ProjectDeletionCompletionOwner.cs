using System.Data;
using System.Security.Cryptography;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectDeletionCompletionOwner(IDbContextFactory<ProjectsDbContext> factory,
    ICanonicalRuntimeDatabase database, DatasetActivationService activation, TimeProvider clock) {
    private readonly TimeProvider time = clock;
    internal Attempt Capture(ProjectsDbContext context, ProjectSourceMutationScope scope, ResolvedDatabaseProfile profile,
        long generation, DatasetActivationBinding binding, ProjectDeletionRecoveryEvidence evidence) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(evidence);
        RequireOriginal(profile, generation);
        CoordinatedDatabaseTransaction.ForProfile(profile).RequireOwnerProfile(context);
        if (!context.Database.IsNpgsql() || context.Database.CurrentTransaction is null ||
                evidence.Original.Completion.DatabaseProfileId != profile.Profile.Id ||
                evidence.Original.Completion.Namespace != binding.Namespace ||
                evidence.Origin is ProjectDeletionOriginEvidence.LegacyInstallation || scope.DeletionSource != evidence.Source ||
                !scope.RequiredProjects.Append(evidence.Original.Project).ToHashSet().SetEquals(evidence.RequiredProjects)) {
            throw new InvalidOperationException("The deletion completion must retain its actual original owner and acquired source.");
        }
        return new(this, context, scope, profile, generation, binding, evidence, ProjectDeletionRecoveryCodec.Write(evidence));
    }

    private void RequireOriginal(ResolvedDatabaseProfile profile, long generation) {
        if (database.Profile != profile || database.Generation != generation ||
                profile.Profile.ProviderKind != DatabaseProviderKind.PostgreSql) {
            throw new InvalidOperationException("The original deletion completion database owner is no longer current.");
        }
    }

    private async Task<ReleaseResult> MarkReleasedAsync(Attempt attempt, CancellationToken cancellationToken) {
        using var admission = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        admission.CancelAfter(TimeSpan.FromSeconds(5));
        cancellationToken = admission.Token;
        RequireOriginal(attempt.Profile, attempt.Generation);
        var observed = await activation.ObserveActiveBindingAsync(attempt.Profile.Profile.Id, cancellationToken);
        RequireOriginal(attempt.Profile, attempt.Generation);
        if (observed.Record.Plan.Binding != attempt.Binding) {
            throw new InvalidOperationException("The original deletion dataset is no longer active.");
        }
        ProjectsDbContext? context = null;
        IDbContextTransaction? transaction = null;
        Exception? failure = null;
        var released = false;
        try {
            context = await factory.CreateDbContextAsync(cancellationToken);
            RequireOriginal(attempt.Profile, attempt.Generation);
            if (!context.Database.IsNpgsql() || context.Database.CurrentTransaction is not null ||
                    System.Transactions.Transaction.Current is not null ||
                    System.Transactions.TransactionsDatabaseFacadeExtensions.GetEnlistedTransaction(context.Database) is not null ||
                    context.ChangeTracker.Entries().Any() || context.Database.GetDbConnection() is not NpgsqlConnection connection ||
                    connection.State != ConnectionState.Closed) {
                throw new InvalidOperationException("Release persistence requires a fresh independent PostgreSQL context.");
            }
            CoordinatedDatabaseTransaction.ForProfile(attempt.Profile).RequireOwnerProfile(context);
            context.Database.SetCommandTimeout(5);
            transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            await context.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'; SET LOCAL statement_timeout = '5s'", cancellationToken);
            await DatasetLifecycleStore.RequireActiveForMutationAsync(context, attempt.Binding, cancellationToken);
            RequireOriginal(attempt.Profile, attempt.Generation);
            var original = attempt.Evidence.Original;
            var now = time.GetUtcNow();
            if (now == DateTimeOffset.MinValue || now == DateTimeOffset.MaxValue || now.Offset != TimeSpan.Zero) {
                throw new InvalidOperationException("Deletion release requires a finite UTC timestamp.");
            }
            var changed = await ProjectDeletionReleasePersistence.MarkAsync(context, attempt.Evidence,
                attempt.CreatedAtUtc, attempt.References, attempt.Recovery, attempt.RecoveryDigest, now, cancellationToken);
            if (changed is < 0 or > 1) {
                throw new InvalidDataException("Deletion release did not address exactly one possible completion.");
            }
            await RequireOriginalBindingAsync();
            cancellationToken.ThrowIfCancellationRequested();
            await transaction.CommitAsync(cancellationToken);
            released = changed == 1;
        } catch (Exception operationFailure) {
            failure = operationFailure;
        }
        if (transaction is not null) {
            try {
                await transaction.DisposeAsync();
            } catch (Exception cleanupFailure) {
                failure = Combine(failure, cleanupFailure);
            }
        }
        if (context is not null) {
            try {
                await context.DisposeAsync();
            } catch (Exception cleanupFailure) {
                failure = Combine(failure, cleanupFailure);
            }
        }
        try {
            await RequireOriginalBindingAsync();
        } catch (Exception bindingFailure) {
            failure = Combine(failure, bindingFailure);
        }
        return new(released, failure);

        async Task RequireOriginalBindingAsync() {
            RequireOriginal(attempt.Profile, attempt.Generation);
            var current = await activation.ObserveActiveBindingAsync(attempt.Profile.Profile.Id, cancellationToken);
            RequireOriginal(attempt.Profile, attempt.Generation);
            if (current != observed) {
                throw new InvalidOperationException("The original deletion activation observation changed during release persistence.");
            }
        }
    }

    private static Exception Combine(Exception? first, Exception next) => first is null ? next : new AggregateException(first, next);

    internal sealed class Attempt {
        private readonly ProjectDeletionCompletionOwner owner;
        private readonly ProjectsDbContext context;
        private readonly ProjectSourceMutationScope scope;
        private readonly object closeSync = new();
        private Task<CloseResult>? close;
        private bool staged;

        internal Attempt(ProjectDeletionCompletionOwner owner, ProjectsDbContext context, ProjectSourceMutationScope scope,
            ResolvedDatabaseProfile profile, long generation, DatasetActivationBinding binding,
            ProjectDeletionRecoveryEvidence evidence, byte[] recovery) {
            this.owner = owner;
            this.context = context;
            this.scope = scope;
            Profile = profile;
            Generation = generation;
            Binding = binding;
            Evidence = evidence;
            Recovery = recovery;
            RecoveryDigest = SHA256.HashData(recovery);
            OriginKind = evidence.Origin switch {
                ProjectDeletionOriginEvidence.Ordinary => ProjectDeletionCompletionOrigin.OrdinaryDeletion,
                ProjectDeletionOriginEvidence.Compensation => ProjectDeletionCompletionOrigin.CreationCompensation,
                ProjectDeletionOriginEvidence.ReservedCancellation => ProjectDeletionCompletionOrigin.ReservedCancellation,
                _ => throw new InvalidOperationException("An online owner cannot install legacy release evidence.")
            };
        }

        internal ResolvedDatabaseProfile Profile { get; }
        internal long Generation { get; }
        internal DatasetActivationBinding Binding { get; }
        internal ProjectDeletionRecoveryEvidence Evidence { get; }
        internal byte[] Recovery { get; }
        internal byte[] RecoveryDigest { get; }
        internal byte[] References { get; private set; } = [];
        internal ProjectDeletionCompletionOrigin OriginKind { get; }
        internal DateTimeOffset CreatedAtUtc { get; private set; }

        internal void StageParticipants(ProjectDeletionCompletionBinding completion) {
            if (staged || close is not null || completion.Original != Evidence.Original) {
                throw new InvalidOperationException("Deletion completion may be staged only once for its exact original owner.");
            }
            owner.RequireOriginal(Profile, Generation);
            References = ProjectDeletionCompletionCodec.Write(completion);
            CreatedAtUtc = owner.time.GetUtcNow();
            if (CreatedAtUtc == DateTimeOffset.MinValue || CreatedAtUtc == DateTimeOffset.MaxValue || CreatedAtUtc.Offset != TimeSpan.Zero) {
                throw new InvalidOperationException("Deletion completion requires a finite UTC timestamp.");
            }
            var original = Evidence.Original;
            context.Add(new ProjectDeletionCompletionRecord {
                NamespaceId = original.Completion.Namespace.Value,
                CompletionId = original.Completion.Id.Value,
                DatabaseProfileId = original.Project.DatabaseProfileId,
                ProjectId = original.Project.ProjectId,
                LifetimeId = original.Project.LifetimeId,
                SchemaVersion = ProjectDeletionCompletionProtocol.SchemaVersion,
                OriginKind = OriginKind,
                CreatedAtUtc = CreatedAtUtc,
                ReleaseState = ProjectDeletionCompletionReleaseState.AwaitingOwnerRelease,
                ReferencePayload = References.ToArray(),
                RecoveryPayload = Recovery.ToArray(),
                RecoveryDigest = RecoveryDigest.ToArray()
            });
            staged = true;
        }

        internal Task<CloseResult> CloseAsync(CancellationToken cancellationToken) {
            lock (closeSync) {
                return close ??= CloseCoreAsync(cancellationToken);
            }
        }

        private async Task<CloseResult> CloseCoreAsync(CancellationToken cancellationToken) {
            Exception? failure = null;
            try {
                await scope.DisposeAsync();
            } catch (Exception cleanupFailure) {
                failure = cleanupFailure;
            }
            try {
                await context.DisposeAsync();
            } catch (Exception cleanupFailure) {
                failure = Combine(failure, cleanupFailure);
            }
            if (failure is not null) {
                return new(scope.IsCommitted, false, false, failure);
            }
            if (!staged || !scope.CommitAttempted) {
                return new(scope.IsCommitted, true, false, null);
            }
            try {
                var persisted = await owner.MarkReleasedAsync(this, cancellationToken);
                return new(scope.IsCommitted, true, persisted.Acknowledged, persisted.Failure);
            } catch (Exception persistenceFailure) {
                return new(scope.IsCommitted, true, false, persistenceFailure);
            }
        }
    }

    private sealed record ReleaseResult(bool Acknowledged, Exception? Failure);

    internal sealed record CloseResult(bool CommitAcknowledged, bool OriginalOwnerReleased, bool ReleaseAcknowledged, Exception? Failure);
}
