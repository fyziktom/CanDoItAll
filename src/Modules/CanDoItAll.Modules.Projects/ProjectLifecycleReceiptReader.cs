using System.Data;
using System.Data.Common;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectLifecycleReceiptReader(IDbContextFactory<ProjectsDbContext> factory,
    ICanonicalRuntimeDatabase database, DatasetActivationService activation, ILogger<ProjectLifecycleReceiptReader> logger) {
    public async Task<ProjectLifecycleLookup> LookupAsync(ProjectLifecycleOperation original,
        ProjectLifecycleReceiptInvocation invocation, CancellationToken cancellationToken = default)
        => (await ObserveAsync(original, invocation, cancellationToken)).Lookup;

    internal async Task<ProjectLifecycleObservedOperation> ObserveAsync(ProjectLifecycleOperation original,
        ProjectLifecycleReceiptInvocation invocation, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(invocation);
        var profile = database.Profile;
        var generation = database.Generation;
        if (profile.Profile.Id != original.DatabaseProfileId) {
            return Changed(ProjectLifecycleContextChange.Profile);
        }
        if (profile.Profile.ProviderKind != DatabaseProviderKind.PostgreSql) {
            throw new NotSupportedException("Durable lifecycle receipts require PostgreSQL snapshot semantics.");
        }
        if (!invocation.Caller.Matches(original.Namespace, original.CallerBinding)) {
            return Changed(ProjectLifecycleContextChange.Caller);
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        var token = deadline.Token;
        try {
            var before = await activation.ObserveActiveBindingAsync(original.DatabaseProfileId, token);
            if (!OriginalOwner()) {
                return Changed(ProjectLifecycleContextChange.Profile);
            }
            if (before.Record.Plan.Binding.Namespace != original.Namespace) {
                return Changed(ProjectLifecycleContextChange.Dataset);
            }
            var row = await ReadIndependentAsync(profile, original, invocation.Caller, token);
            if (!OriginalOwner()) {
                return Changed(ProjectLifecycleContextChange.Profile);
            }
            var lookup = row is not null && row.Matches(before.Record.Plan.Binding)
                ? row.Interpret(original)
                : new ProjectLifecycleLookup.ContextChanged(original, ProjectLifecycleContextChange.Dataset);
            if (lookup is ProjectLifecycleLookup.Found accepted &&
                    !await invocation.Access.CanReadAsync(accepted.Accepted.Original, token)) {
                lookup = new ProjectLifecycleLookup.Unavailable(original, ProjectLifecycleUnavailableReason.AccessDenied);
            }
            if (!OriginalOwner()) {
                return Changed(ProjectLifecycleContextChange.Profile);
            }
            var after = await activation.ObserveActiveBindingAsync(original.DatabaseProfileId, token);
            if (!OriginalOwner()) {
                return Changed(ProjectLifecycleContextChange.Profile);
            }
            if (after != before) {
                return Changed(ProjectLifecycleContextChange.Dataset);
            }
            if (lookup is ProjectLifecycleLookup.Unavailable unavailable) {
                logger.LogWarning("Lifecycle receipt {OperationId} in dataset {NamespaceId}, epoch {Epoch} is unavailable: {Reason}.",
                    original.Id, original.Namespace.Value, original.Epoch.Value, unavailable.Reason);
            }
            return new(lookup, row);
        } catch (OperationCanceledException failure) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested) {
            return Unavailable(failure);
        } catch (Exception failure) when (ReadFailure(failure)) {
            return Unavailable(failure);
        }

        bool OriginalOwner() => database.Profile == profile && database.Generation == generation;

        ProjectLifecycleObservedOperation Changed(ProjectLifecycleContextChange change)
            => new(new ProjectLifecycleLookup.ContextChanged(original, change), null);

        ProjectLifecycleObservedOperation Unavailable(Exception failure) {
            try {
                logger.LogError("Lifecycle receipt {OperationId} in dataset {NamespaceId}, epoch {Epoch} could not be read; failure type {FailureType}.",
                    original.Id, original.Namespace.Value, original.Epoch.Value, failure.GetType().FullName);
            } catch (Exception loggingFailure) {
                throw new AggregateException("Lifecycle receipt observation and diagnostic publication both failed.", failure, loggingFailure);
            }
            return new(new ProjectLifecycleLookup.Unavailable(original, ProjectLifecycleUnavailableReason.ReadUnavailable), null);
        }
    }

    private async Task<ProjectLifecycleReceiptRow?> ReadIndependentAsync(ResolvedDatabaseProfile profile,
        ProjectLifecycleOperation original, ProjectMutationCallerScope caller, CancellationToken cancellationToken) {
        var context = await factory.CreateDbContextAsync(cancellationToken);
        Exception? readFailure = null;
        try {
            if (!context.Database.IsNpgsql() || context.Database.CurrentTransaction is not null ||
                    System.Transactions.Transaction.Current is not null ||
                    System.Transactions.TransactionsDatabaseFacadeExtensions.GetEnlistedTransaction(context.Database) is not null ||
                    context.ChangeTracker.Entries().Any() || context.Database.GetDbConnection() is not NpgsqlConnection connection ||
                    connection.State != ConnectionState.Closed) {
                throw new InvalidOperationException("Lifecycle receipt observation requires a fresh independent PostgreSQL context.");
            }
            CoordinatedDatabaseTransaction.ForProfile(profile).RequireOwnerProfile(context);
            context.Database.SetCommandTimeout(5);
            return await ReadAsync(context, original, caller, cancellationToken);
        } catch (Exception failure) {
            readFailure = failure;
            throw;
        } finally {
            try {
                await context.DisposeAsync();
            } catch (Exception cleanupFailure) when (readFailure is not null) {
                throw new AggregateException("Lifecycle receipt read and context disposal both failed.", readFailure, cleanupFailure);
            }
        }
    }

    private static bool ReadFailure(Exception failure) => failure switch {
        OperationCanceledException => false,
        AggregateException aggregate => aggregate.InnerExceptions.All(ReadFailure),
        DbException or IOException => true,
        InvalidOperationException invalid => invalid.InnerException is null || ReadFailure(invalid.InnerException),
        _ => false
    };

    internal static Task<ProjectLifecycleReceiptRow?> ReadAsync(ProjectsDbContext context, ProjectLifecycleOperation original,
        ProjectMutationCallerScope caller, CancellationToken cancellationToken) {
        var key = caller.Key.ToArray();
        return context.Database.SqlQuery<ProjectLifecycleReceiptRow>($"""
            SELECT d."NamespaceId" AS "ActiveNamespaceId", d."MaintenanceId", d."SchemaVersion" AS "DatasetSchemaVersion",
                   d."GuardProtocol", d."State" AS "DatasetState", s."CurrentEpoch", s."RetiredThroughEpoch", s."RetentionProtocolVersion",
                   e."State" AS "EpochState", e."OpenedAtUtc", e."AdmissionClosesAtUtc", e."ClosedAtUtc",
                   e."MutationDeadlineMilliseconds", e."ReplayRetentionSeconds", clock_timestamp() AS "ObservedAtUtc",
                   r."OperationId" IS NOT NULL AS "ReceiptPresent", r."DatabaseProfileId", r."SchemaVersion",
                   r."NormalizationVersion", r."ResultVersion", r."CommandKind", r."ProjectId", r."LifetimeId", r."AcceptedAtUtc",
                   CASE WHEN octet_length(r."SemanticDigest") = 32 THEN r."SemanticDigest" ELSE NULL END AS "SemanticDigest",
                   CASE WHEN octet_length(r."AcceptedPayload") BETWEEN 1 AND {ProjectLifecycleAccepted.MaximumPayloadBytes}
                       THEN r."AcceptedPayload" ELSE NULL END AS "AcceptedPayload"
            FROM "Infrastructure_DatasetLifecycle" AS d
            LEFT JOIN "Infrastructure_FiniteOperationState" AS s ON s."NamespaceId" = d."NamespaceId"
                AND s."PoolId" = {ProjectFiniteOperationPool.Pool.Value}
            LEFT JOIN "Infrastructure_FiniteOperationEpochs" AS e ON e."NamespaceId" = s."NamespaceId" AND e."PoolId" = s."PoolId"
                AND e."Epoch" = {original.Epoch.Value}
            LEFT JOIN "Projects_LifecycleReceipts" AS r ON r."NamespaceId" = d."NamespaceId" AND r."NamespaceId" = {original.Namespace.Value}
                AND r."Epoch" = {original.Epoch.Value} AND r."CallerKind" = {(int)caller.Kind} AND r."CallerKey" = {key} AND r."OperationId" = {original.Id}
            WHERE d."Id" = 1
            """).SingleOrDefaultAsync(cancellationToken);
    }
}

internal sealed record ProjectLifecycleObservedOperation(ProjectLifecycleLookup Lookup, ProjectLifecycleReceiptRow? Evidence);
