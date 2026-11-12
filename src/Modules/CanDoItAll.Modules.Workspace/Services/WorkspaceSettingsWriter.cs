using System.Data;
using System.Runtime.ExceptionServices;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CanDoItAll.Modules.Workspace;

public sealed class WorkspaceSettingsWriter(IDbContextFactory<WorkspaceSettingsDbContext> contexts,
    ICanonicalRuntimeDatabase canonical, DatasetActivationService activation, IClock clock,
    CurrencyDisplayState currency, IActivityStream activity, ILogger<WorkspaceSettingsWriter> logger) {
    public Task<WorkspaceSettingsSnapshot> ReadAsync(CancellationToken cancellationToken = default)
        => ReadCoreAsync(WorkspaceSettingsHeadStore.Snapshot, cancellationToken);

    public Task<WorkspaceSettingsSnapshotV2> ReadV2Async(CancellationToken cancellationToken = default)
        => ReadCoreAsync(WorkspaceSettingsHeadStore.SnapshotV2, cancellationToken);

    private async Task<TSnapshot> ReadCoreAsync<TSnapshot>(Func<DatasetNamespaceId, WorkspaceSettings?, TSnapshot> capture,
        CancellationToken cancellationToken) {
        var profile = canonical.Profile;
        var binding = await activation.ReadActiveBindingAsync(profile.Profile.Id, cancellationToken);
        await using var context = await contexts.CreateDbContextAsync(cancellationToken);
        RequireOwner(context, profile);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await InstallDeadlinesAsync(context, cancellationToken);
        await DatasetLifecycleStore.RequireActiveForMutationAsync(context, binding, cancellationToken);
        var snapshot = capture(binding.Namespace, await WorkspaceSettingsHeadStore.ReadAsync(context, tracking: false, cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return snapshot;
    }

    public Task<WorkspaceSettingsSaveOutcome> ReplaceAsync(ReplaceWorkspaceSettingsCommand command,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(command);
        return ReplaceCoreAsync(command.Expected, command.Values, null, cancellationToken);
    }

    public async Task<WorkspaceSettingsSaveOutcomeV2> ReplaceV2Async(ReplaceWorkspaceSettingsV2Command command,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(command);
        var policyJson = DecisionDataPolicyCodec.Serialize(command.DecisionDataPolicy);
        var outcome = await ReplaceCoreAsync(command.Expected, command.Values, policyJson, cancellationToken);
        return outcome switch {
            WorkspaceSettingsSaveOutcome.Accepted accepted => new WorkspaceSettingsSaveOutcomeV2.Accepted(
                new(accepted.Snapshot.Expectation, accepted.Snapshot.Values, command.DecisionDataPolicy), accepted.Diagnostic),
            WorkspaceSettingsSaveOutcome.Rejected rejected => new WorkspaceSettingsSaveOutcomeV2.Rejected(rejected.Reason),
            WorkspaceSettingsSaveOutcome.Unconfirmed => new WorkspaceSettingsSaveOutcomeV2.Unconfirmed(),
            _ => throw new InvalidOperationException("The Workspace replacement returned an unsupported outcome.")
        };
    }

    private async Task<WorkspaceSettingsSaveOutcome> ReplaceCoreAsync(WorkspaceSettingsExpectation expected,
        WorkspaceSettingsValues submitted, string? policyJson, CancellationToken cancellationToken) {
        if (!WorkspaceSettingsHeadStore.TryNormalize(submitted, out var values)) {
            return new WorkspaceSettingsSaveOutcome.Rejected(WorkspaceSettingsRejectionReason.InvalidValues);
        }
        cancellationToken.ThrowIfCancellationRequested();
        var profile = canonical.Profile;
        var binding = await activation.ReadActiveBindingAsync(profile.Profile.Id, cancellationToken);
        if (expected.Namespace != binding.Namespace) {
            return new WorkspaceSettingsSaveOutcome.Rejected(WorkspaceSettingsRejectionReason.DatasetChanged);
        }
        WorkspaceSettingsDbContext? context = null;
        IDbContextTransaction? transaction = null;
        WorkspaceSettingsSaveOutcome? outcome = null;
        Exception? primary = null;
        var commitAttempted = false;
        var committed = false;
        try {
            context = await contexts.CreateDbContextAsync(cancellationToken);
            RequireOwner(context, profile);
            transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            await InstallDeadlinesAsync(context, cancellationToken);
            await DatasetLifecycleStore.RequireActiveForMutationAsync(context, binding, cancellationToken);
            await WorkspaceSettingsHeadStore.ExcludeWritersAsync(context, cancellationToken);
            var settings = await WorkspaceSettingsHeadStore.ReadAsync(context, tracking: true, cancellationToken);
            if (WorkspaceSettingsHeadStore.Expectation(binding.Namespace, settings) != expected) {
                outcome = new WorkspaceSettingsSaveOutcome.Rejected(WorkspaceSettingsRejectionReason.HeadChanged);
            } else {
                settings ??= WorkspaceSettingsHeadStore.Create(context);
                WorkspaceSettingsHeadStore.Replace(context, settings, values, clock.GetUtcNow());
                if (policyJson is not null) {
                    settings.DecisionDataPolicyJson = policyJson;
                }
                await context.SaveChangesAsync(cancellationToken);
                var accepted = new WorkspaceSettingsSaveOutcome.Accepted(WorkspaceSettingsHeadStore.Snapshot(binding.Namespace, settings));
                cancellationToken.ThrowIfCancellationRequested();
                commitAttempted = true;
                await transaction.CommitAsync(cancellationToken);
                committed = true;
                outcome = accepted;
            }
        } catch (DatasetActivationMismatchException exception) when (!commitAttempted) {
            primary = exception;
            outcome = new WorkspaceSettingsSaveOutcome.Rejected(WorkspaceSettingsRejectionReason.DatasetChanged);
        } catch (DbUpdateConcurrencyException exception) when (!commitAttempted) {
            primary = exception;
            outcome = new WorkspaceSettingsSaveOutcome.Rejected(WorkspaceSettingsRejectionReason.HeadChanged);
        } catch (Exception exception) when (IsKnownTransactionRejection(exception, commitAttempted)) {
            primary = exception;
            outcome = new WorkspaceSettingsSaveOutcome.Rejected(WorkspaceSettingsRejectionReason.TransactionConflict);
        } catch (Exception exception) {
            primary = exception;
            if (commitAttempted && !committed) {
                outcome = new WorkspaceSettingsSaveOutcome.Unconfirmed();
            }
        } finally {
            Exception? cleanup = null;
            if (transaction is not null) {
                try {
                    await transaction.DisposeAsync();
                } catch (Exception exception) {
                    cleanup = exception;
                }
            }
            if (context is not null) {
                try {
                    await context.DisposeAsync();
                } catch (Exception exception) {
                    cleanup = cleanup is null ? exception : new AggregateException(cleanup, exception);
                }
            }
            if (cleanup is not null) {
                if (committed && outcome is WorkspaceSettingsSaveOutcome.Accepted accepted) {
                    outcome = new WorkspaceSettingsSaveOutcome.Accepted(accepted.Snapshot, WorkspaceSettingsSecondaryDiagnostic.CleanupPending);
                } else if (primary is null && outcome is null) {
                    primary = cleanup;
                }
                TryLog(cleanup, "cleanup");
            }
        }
        if (outcome is WorkspaceSettingsSaveOutcome.Accepted saved) {
            return await PublishAcceptedAsync(saved, cancellationToken);
        }
        if (outcome is not null) {
            if (primary is not null) {
                TryLog(primary, commitAttempted ? "commit" : "transaction");
            }
            return outcome;
        }
        ExceptionDispatchInfo.Throw(primary ?? new InvalidOperationException("The Workspace replacement produced no outcome."));
        throw new InvalidOperationException("Unreachable Workspace outcome.");
    }

    private async Task<WorkspaceSettingsSaveOutcome.Accepted> PublishAcceptedAsync(WorkspaceSettingsSaveOutcome.Accepted accepted,
        CancellationToken cancellationToken) {
        var diagnostic = accepted.Diagnostic;
        try {
            currency.Update(accepted.Snapshot.Values.CurrencyCode, accepted.Snapshot.Values.CurrencyCultureName);
        } catch (Exception exception) {
            diagnostic ??= WorkspaceSettingsSecondaryDiagnostic.CurrencyPublicationPending;
            if (!TryLog(exception, "currency-publication") && diagnostic != WorkspaceSettingsSecondaryDiagnostic.CleanupPending) {
                diagnostic = WorkspaceSettingsSecondaryDiagnostic.LoggingPending;
            }
        }
        try {
            await activity.RecordAsync(new ActivityWriteRequest("workspace", "save-defaults", "Updated workspace defaults",
                $"Workspace name: {accepted.Snapshot.Values.WorkspaceName}.", Route: "/settings"), cancellationToken);
        } catch (Exception exception) {
            diagnostic ??= WorkspaceSettingsSecondaryDiagnostic.ActivityPending;
            if (!TryLog(exception, "activity") && diagnostic != WorkspaceSettingsSecondaryDiagnostic.CleanupPending) {
                diagnostic = WorkspaceSettingsSecondaryDiagnostic.LoggingPending;
            }
        }
        return new(accepted.Snapshot, diagnostic);
    }

    private bool TryLog(Exception exception, string phase) {
        try {
            logger.LogWarning("Workspace settings {Phase} requires review: {ErrorType}.", phase, exception.GetType().Name);
            return true;
        } catch (Exception) {
            return false;
        }
    }

    private static bool IsKnownTransactionRejection(Exception exception, bool commitAttempted) {
        var postgres = exception as PostgresException ?? (exception as DbUpdateException)?.InnerException as PostgresException;
        return postgres?.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected ||
            !commitAttempted && postgres?.SqlState is PostgresErrorCodes.LockNotAvailable or PostgresErrorCodes.QueryCanceled;
    }

    private static void RequireOwner(WorkspaceSettingsDbContext context, ResolvedDatabaseProfile profile) {
        if (!context.Database.IsNpgsql()) {
            throw new NotSupportedException("Protected Workspace defaults require PostgreSQL.");
        }
        CoordinatedDatabaseTransaction.ForProfile(profile).RequireOwnerProfile(context);
    }

    private static Task<int> InstallDeadlinesAsync(WorkspaceSettingsDbContext context, CancellationToken cancellationToken)
        => context.Database.ExecuteSqlRawAsync("""
            SELECT set_config('transaction_timeout', '5s', true),
                set_config('statement_timeout', '2s', true), set_config('lock_timeout', '1s', true)
            """, cancellationToken);
}
