using System.Data;
using System.Data.Common;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectReservationCancellationReader(IDbContextFactory<ProjectsDbContext> contexts,
    ICanonicalRuntimeDatabase database, DatasetActivationService activation, ILogger<ProjectReservationCancellationReader> logger) {
    public async Task<ProjectReservationCancellationObservation> ObserveAsync(ProjectReservationCancellationRequest original,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(original);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(5));
        var profile = database.Profile;
        var generation = database.Generation;
        if (profile.Profile.ProviderKind != DatabaseProviderKind.PostgreSql) {
            throw new NotSupportedException("Original reservation cancellation observation requires PostgreSQL.");
        }
        if (profile.Profile.Id != original.Original.Completion.DatabaseProfileId) {
            return Unavailable(ProjectCreationCompensationUnavailableReason.OriginalContextChanged);
        }
        try {
            var before = await activation.ObserveActiveBindingAsync(profile.Profile.Id, budget.Token);
            if (!Current() || before.Record.Plan.Binding.Namespace != original.Original.Completion.Namespace) {
                return Unavailable(ProjectCreationCompensationUnavailableReason.OriginalContextChanged);
            }
            var row = await ReadIndependentAsync(profile, original, budget.Token);
            if (!Current()) {
                return Unavailable(ProjectCreationCompensationUnavailableReason.OriginalContextChanged);
            }
            var after = await activation.ObserveActiveBindingAsync(profile.Profile.Id, budget.Token);
            if (!Current() || after != before || row is null || !row.Matches(before.Record.Plan.Binding)) {
                return Unavailable(ProjectCreationCompensationUnavailableReason.OriginalContextChanged);
            }
            return Interpret(row, original);
        } catch (Exception failure) when (ReadFailure(failure)) {
            var diagnosticsUnavailable = false;
            try {
                logger.LogWarning("Original reservation cancellation {CompletionId} in dataset {NamespaceId} could not be observed; failure type {FailureType}.",
                    original.Original.Completion.Id.Value, original.Original.Completion.Namespace.Value, failure.GetType().FullName);
            } catch (Exception) {
                diagnosticsUnavailable = true;
            }
            return new ProjectReservationCancellationObservation.Unavailable(ProjectCreationCompensationUnavailableReason.ReadUnavailable,
                diagnosticsUnavailable);
        }

        bool Current() => database.Profile == profile && database.Generation == generation;
    }

    private async Task<ProjectDeletionCompletionRow?> ReadIndependentAsync(ResolvedDatabaseProfile profile,
        ProjectReservationCancellationRequest original, CancellationToken cancellationToken) {
        var context = await contexts.CreateDbContextAsync(cancellationToken);
        Exception? primary = null;
        try {
            if (!context.Database.IsNpgsql() || context.Database.CurrentTransaction is not null ||
                    System.Transactions.Transaction.Current is not null ||
                    System.Transactions.TransactionsDatabaseFacadeExtensions.GetEnlistedTransaction(context.Database) is not null ||
                    context.ChangeTracker.Entries().Any() || context.Database.GetDbConnection() is not NpgsqlConnection connection ||
                    connection.State != ConnectionState.Closed) {
                throw new InvalidOperationException("Reservation cancellation observation requires a fresh independent PostgreSQL context.");
            }
            CoordinatedDatabaseTransaction.ForProfile(profile).RequireOwnerProfile(context);
            context.Database.SetCommandTimeout(5);
            return await ProjectDeletionCompletionReader.ReadAsync(context, original.Original, cancellationToken);
        } catch (Exception failure) {
            primary = failure;
            throw;
        } finally {
            try {
                await context.DisposeAsync();
            } catch (Exception cleanupFailure) when (primary is not null) {
                throw new AggregateException("Reservation cancellation observation and context disposal both failed.", primary, cleanupFailure);
            }
        }
    }

    internal static async Task<ProjectReservationCancellationObservation> ReadEnlistedAsync(ProjectsDbContext context,
        DatasetActivationBinding binding, ProjectReservationCancellationRequest original, CancellationToken cancellationToken) {
        if (!context.Database.IsNpgsql() || context.Database.CurrentTransaction is null ||
                binding.Namespace != original.Original.Completion.Namespace) {
            throw new InvalidOperationException("Reservation cancellation replay requires the original lifecycle-held owner transaction.");
        }
        var row = await ProjectDeletionCompletionReader.ReadAsync(context, original.Original, cancellationToken);
        return row is null || !row.Matches(binding)
            ? Unavailable(ProjectCreationCompensationUnavailableReason.OriginalContextChanged) : Interpret(row, original);
    }

    private static ProjectReservationCancellationObservation Interpret(ProjectDeletionCompletionRow row,
        ProjectReservationCancellationRequest original) {
        if (row.CompletionId is null) {
            return new ProjectReservationCancellationObservation.NotObserved();
        }
        if (!row.MatchesReservedCancellation(original.Original, original.Preparation.Target) || row.ReferencePayload is null) {
            return Unavailable(ProjectCreationCompensationUnavailableReason.InvalidStoredEvidence);
        }
        try {
            var binding = ProjectDeletionCompletionCodec.Read(row.ReferencePayload, original.Original);
            return row.Interpret(binding) is ProjectDeletionCompletionObservation.Released or ProjectDeletionCompletionObservation.AwaitingOwnerRelease
                ? new ProjectReservationCancellationObservation.Committed(binding)
                : Unavailable(ProjectCreationCompensationUnavailableReason.InvalidStoredEvidence);
        } catch (InvalidDataException) {
            return Unavailable(ProjectCreationCompensationUnavailableReason.InvalidStoredEvidence);
        }
    }

    private static bool ReadFailure(Exception failure) => failure switch {
        OperationCanceledException => false,
        AggregateException aggregate => aggregate.InnerExceptions.All(ReadFailure),
        DbException or IOException or InvalidDataException => true,
        InvalidOperationException invalid => invalid.InnerException is null || ReadFailure(invalid.InnerException),
        _ => false
    };

    private static ProjectReservationCancellationObservation Unavailable(ProjectCreationCompensationUnavailableReason reason)
        => new ProjectReservationCancellationObservation.Unavailable(reason);
}
