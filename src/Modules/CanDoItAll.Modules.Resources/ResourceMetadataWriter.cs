using System.Data;
using System.Runtime.ExceptionServices;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CanDoItAll.Modules.Resources;

public sealed class ResourceMetadataWriter(IDbContextFactory<ResourcesDbContext> contexts,
    ICanonicalRuntimeDatabase canonical, DatasetActivationService activation, CoordinatedDatabaseTransaction transactions,
    ProjectWriteAdmissionService admissions, ResourceMetadataPreparation preparation, ISearchIndexService search,
    IClock clock, IActivityStream activity, ILogger<ResourceMetadataWriter> logger) {
    public async Task<ResourceMetadataContext> ReadContextAsync(CancellationToken cancellationToken = default) {
        var profile = canonical.Profile;
        var generation = canonical.Generation;
        var binding = await activation.ReadActiveBindingAsync(profile.Profile.Id, cancellationToken);
        await using var owner = await contexts.CreateDbContextAsync(cancellationToken);
        RequireOwner(owner, profile);
        await using var transaction = await owner.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await InstallDeadlinesAsync(owner, cancellationToken);
        await DatasetLifecycleStore.RequireActiveForMutationAsync(owner, binding, cancellationToken);
        RequireCurrent(profile, generation);
        await transaction.CommitAsync(cancellationToken);
        RequireCurrent(profile, generation);
        return new(profile.Profile.Id, binding.Namespace);
    }

    public async Task<ResourceMetadataReadResult> ReadAsync(Guid id, CancellationToken cancellationToken = default) {
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);
        var profile = canonical.Profile;
        var generation = canonical.Generation;
        var binding = await activation.ReadActiveBindingAsync(profile.Profile.Id, cancellationToken);
        await using var owner = await contexts.CreateDbContextAsync(cancellationToken);
        RequireOwner(owner, profile);
        await using var transaction = await owner.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await InstallDeadlinesAsync(owner, cancellationToken);
        await DatasetLifecycleStore.RequireActiveForMutationAsync(owner, binding, cancellationToken);
        var result = await ResourceMetadataStore.ReadAsync(owner, new(profile.Profile.Id, binding.Namespace), id, preparation, cancellationToken);
        RequireCurrent(profile, generation);
        await transaction.CommitAsync(cancellationToken);
        RequireCurrent(profile, generation);
        return result;
    }

    public async Task<ResourceMetadataSaveOutcome> ReplaceAsync(ReplaceResourceMetadataCommand command,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        PreparedResourceMetadata prepared;
        try {
            prepared = preparation.Prepare(command);
        } catch (ResourceMetadataRefusedException exception) {
            return new ResourceMetadataSaveOutcome.Rejected(exception.Reason, exception.Message);
        } catch (Exception exception) when (exception is ArgumentException or System.Text.Json.JsonException) {
            return new ResourceMetadataSaveOutcome.Rejected(ResourceMetadataRejectionReason.InvalidValues);
        }
        var result = await ExecuteAsync(command.Expected.Context, command.Expected.ResourceId,
            Keys(command.Expected.ResourceId, OriginalProject(command.Expected), command.TargetProject.ProjectId), async owner => {
                var current = await ResourceMetadataStore.ReadAsync(owner, command.Expected.Context, command.Expected.ResourceId, preparation, cancellationToken);
                RequireExpected(command.Expected, current);
                if (current is not ResourceMetadataReadResult.Missing && current is not ResourceMetadataReadResult.Found { ReadOnlyReason: null }) {
                    var governed = current is ResourceMetadataReadResult.Found { ReadOnlyReason: ResourceMetadataReadOnlyReason.GovernedStorageObject };
                    throw Refuse(governed ? ResourceMetadataRejectionReason.GovernedResource : ResourceMetadataRejectionReason.UnsupportedMetadata);
                }
                await admissions.RequireForMutationAsync(command.TargetProject, cancellationToken);
                var now = StoredNow();
                var created = current is ResourceMetadataReadResult.Found found ? found.Snapshot.CreatedAtUtc : now;
                var accepted = Snapshot(command.Expected.Context, command.Expected.ResourceId, command.TargetProject, prepared.Values, created, now);
                ResourceMetadataStore.Stage(owner, accepted, command.Expected);
                await owner.SaveChangesAsync(cancellationToken);
                await search.UpsertForMutationAsync(prepared.Search, cancellationToken);
                return accepted;
            }, cancellationToken);
        if (result.Rejection is { } rejected) {
            return new ResourceMetadataSaveOutcome.Rejected(rejected);
        }
        if (!result.Acknowledged) {
            return new ResourceMetadataSaveOutcome.Unconfirmed(command);
        }
        var snapshot = result.Value!;
        var diagnostic = await PublishAsync(command.Expected.Context, command.Expected.ResourceId,
            command.Expected is ResourceMetadataExpectation.Absent ? "create" : "update", command.TargetProject.ProjectId,
            result.Diagnostic, cancellationToken);
        return new ResourceMetadataSaveOutcome.Accepted(snapshot, diagnostic);
    }

    public async Task<ResourceMetadataDeleteOutcome> DeleteAsync(DeleteResourceMetadataCommand command,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(command);
        var expected = command.Expected;
        var result = await ExecuteAsync(expected.Context, expected.ResourceId, Keys(expected.ResourceId, OriginalProject(expected)), async owner => {
            var current = await ResourceMetadataStore.ReadHeaderAsync(owner, expected.Context, expected.ResourceId, cancellationToken);
            if (current is null) {
                throw Refuse(ResourceMetadataRejectionReason.ResourceMissing);
            }
            if (current != expected) {
                throw Refuse(ResourceMetadataRejectionReason.ResourceChanged);
            }
            ResourceMetadataStore.StageDelete(owner, expected);
            await owner.SaveChangesAsync(cancellationToken);
            await search.DeleteForMutationAsync(SearchDocument.ResourceSourceType, expected.ResourceId.ToString(), cancellationToken);
            return expected;
        }, cancellationToken);
        if (result.Rejection is { } rejected) {
            return new ResourceMetadataDeleteOutcome.Rejected(rejected);
        }
        if (!result.Acknowledged) {
            return new ResourceMetadataDeleteOutcome.Unconfirmed(command);
        }
        var diagnostic = await PublishAsync(expected.Context, expected.ResourceId, "delete", OriginalProject(expected),
            result.Diagnostic, cancellationToken);
        return new ResourceMetadataDeleteOutcome.Accepted(expected, diagnostic);
    }

    internal async Task<StorageObjectResourceWriteResult> PromoteAsync(StorageObjectResourceWriteRequest request,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Context is null || request.ExpectedProjectAdmission is null || request.Config is null ||
            request.ResourceId == Guid.Empty || request.ProjectId != request.ExpectedProjectAdmission.ProjectId ||
            request.Context.DatabaseProfileId != request.ExpectedProjectAdmission.DatabaseProfileId) {
            return new StorageObjectResourceWriteResult.Rejected(ResourceMetadataRejectionReason.InvalidValues);
        }
        PreparedResourceMetadata prepared;
        try {
            prepared = preparation.Prepare(request);
        } catch (Exception exception) when (exception is ArgumentException or ResourceMetadataRefusedException or System.Text.Json.JsonException) {
            return new StorageObjectResourceWriteResult.Rejected(ResourceMetadataRejectionReason.InvalidValues);
        }
        var result = await ExecuteAsync(request.Context, request.ResourceId, Keys(request.ResourceId, request.ProjectId), async owner => {
            await admissions.RequireForMutationAsync(request.ExpectedProjectAdmission, cancellationToken);
            var matches = await owner.Set<ProjectResource>().AsNoTracking()
                .Where(resource => resource.ProjectId == request.ProjectId && resource.ProjectLifetimeId == request.ExpectedProjectAdmission.LifetimeId &&
                    resource.ConnectorPluginKey == StorageObjectResourceConnectorPlugin.PluginKey && resource.ConfigJson == prepared.Values.ConfigJson)
                .Select(resource => resource.Id).Take(2).ToListAsync(cancellationToken);
            if (matches.Count > 1) {
                throw Refuse(ResourceMetadataRejectionReason.DuplicatePromotion);
            }
            if (matches.Count == 1) {
                var retained = await ResourceMetadataStore.ReadAsync(owner, request.Context, matches[0], preparation, cancellationToken);
                if (retained is not ResourceMetadataReadResult.Found { ReadOnlyReason: ResourceMetadataReadOnlyReason.GovernedStorageObject } found) {
                    throw Refuse(ResourceMetadataRejectionReason.UnsupportedMetadata);
                }
                return new PromotionValue(found.Snapshot, false);
            }
            if (await ResourceMetadataStore.ReadHeaderAsync(owner, request.Context, request.ResourceId, cancellationToken) is not null) {
                throw Refuse(ResourceMetadataRejectionReason.ResourceChanged);
            }
            var now = StoredNow();
            var snapshot = Snapshot(request.Context, request.ResourceId, request.ExpectedProjectAdmission, prepared.Values, now, now);
            ResourceMetadataStore.Stage(owner, snapshot, new ResourceMetadataExpectation.Absent(request.Context, request.ResourceId));
            await owner.SaveChangesAsync(cancellationToken);
            await search.UpsertForMutationAsync(prepared.Search, cancellationToken);
            return new PromotionValue(snapshot, true);
        }, cancellationToken);
        if (result.Rejection is { } rejected) {
            return new StorageObjectResourceWriteResult.Rejected(rejected);
        }
        return result.Acknowledged
            ? new StorageObjectResourceWriteResult.Accepted(result.Value!.Snapshot, result.Value.Created, result.Diagnostic)
            : new StorageObjectResourceWriteResult.Unconfirmed();
    }

    private async Task<Mutation<T>> ExecuteAsync<T>(ResourceMetadataContext expected, Guid resourceId, string[] keys,
        Func<ResourcesDbContext, Task<T>> stage, CancellationToken cancellationToken) where T : class {
        cancellationToken.ThrowIfCancellationRequested();
        var profile = canonical.Profile;
        var generation = canonical.Generation;
        if (profile.Profile.Id != expected.DatabaseProfileId) {
            return new(null, false, ResourceMetadataRejectionReason.OwnerRetired, null);
        }
        ResourcesDbContext? owner = null;
        IDbContextTransaction? transaction = null;
        T? accepted = null;
        ResourceMetadataRejectionReason? rejection = null;
        ResourceMetadataSecondaryDiagnostic? diagnostic = null;
        Exception? failure = null;
        var attempted = false;
        var acknowledged = false;
        try {
            var binding = await activation.ReadActiveBindingAsync(profile.Profile.Id, cancellationToken);
            if (binding.Namespace != expected.Namespace) {
                throw Refuse(ResourceMetadataRejectionReason.DatasetChanged);
            }
            owner = await contexts.CreateDbContextAsync(cancellationToken);
            RequireOwner(owner, profile);
            RequireCurrent(profile, generation);
            transaction = await owner.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            await InstallDeadlinesAsync(owner, cancellationToken);
            await DatasetLifecycleStore.RequireActiveForMutationAsync(owner, binding, cancellationToken);
            await SerializableMutationScope.AcquireRelationalScopeLocksAsync(owner, keys, cancellationToken);
            RequireCurrent(profile, generation);
            using (transactions.Enter(owner)) {
                accepted = await stage(owner);
            }
            cancellationToken.ThrowIfCancellationRequested();
            RequireCurrent(profile, generation);
            attempted = true;
            await transaction.CommitAsync(cancellationToken);
            acknowledged = true;
        } catch (Exception exception) {
            failure = exception;
            if (!attempted) {
                rejection = exception switch {
                    ResourceMetadataRefusedException refused => refused.Reason,
                    DatasetActivationMismatchException => ResourceMetadataRejectionReason.DatasetChanged,
                    ProjectWriteAdmissionRejectedException => ResourceMetadataRejectionReason.ProjectUnavailable,
                    DbUpdateConcurrencyException => ResourceMetadataRejectionReason.ResourceChanged,
                    OperationCanceledException => ResourceMetadataRejectionReason.Cancelled,
                    _ when IsConflict(exception) => ResourceMetadataRejectionReason.TransactionConflict,
                    _ => ResourceMetadataRejectionReason.PersistenceUnavailable
                };
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
            if (owner is not null) {
                try {
                    await owner.DisposeAsync();
                } catch (Exception exception) {
                    cleanup = cleanup is null ? exception : new AggregateException(cleanup, exception);
                }
            }
            if (cleanup is not null) {
                diagnostic = ResourceMetadataSecondaryDiagnostic.CleanupPending;
                failure = failure is null ? cleanup : new AggregateException(failure, cleanup);
            }
        }
        if (failure is not null) {
            if (!TryLog(expected, resourceId, failure) && diagnostic != ResourceMetadataSecondaryDiagnostic.CleanupPending) {
                diagnostic = ResourceMetadataSecondaryDiagnostic.LoggingPending;
            }
            if (!acknowledged && !attempted && rejection is null) {
                ExceptionDispatchInfo.Throw(failure);
            }
        }
        return new(accepted, acknowledged, rejection, diagnostic);
    }

    private async Task<ResourceMetadataSecondaryDiagnostic?> PublishAsync(ResourceMetadataContext context, Guid id,
        string action, Guid? projectId, ResourceMetadataSecondaryDiagnostic? diagnostic, CancellationToken cancellationToken) {
        try {
            await activity.RecordAsync(new ActivityWriteRequest("resources", action, "Resource metadata changed",
                $"Resource {id:D}.", ProjectId: projectId, ArtifactKind: SearchDocument.ResourceSourceType,
                ArtifactId: id, Route: action == "delete" ? "/resources" : $"/resources?resourceId={id}"), cancellationToken);
        } catch (Exception exception) {
            diagnostic ??= ResourceMetadataSecondaryDiagnostic.ActivityPending;
            if (!TryLog(context, id, exception) && diagnostic != ResourceMetadataSecondaryDiagnostic.CleanupPending) {
                diagnostic = ResourceMetadataSecondaryDiagnostic.LoggingPending;
            }
        }
        return diagnostic;
    }

    private bool TryLog(ResourceMetadataContext context, Guid id, Exception exception) {
        try {
            var primary = exception is AggregateException aggregate ? aggregate.InnerExceptions[0] : exception;
            logger.LogWarning("Resource metadata outcome needs review. ProfileId={ProfileId} Namespace={Namespace} ResourceId={ResourceId} FailureType={FailureType} AdditionalFailures={AdditionalFailures}.",
                context.DatabaseProfileId, context.Namespace.Value, id, primary.GetType().Name,
                exception is AggregateException failures ? failures.InnerExceptions.Count - 1 : 0);
            return true;
        } catch (Exception) {
            return false;
        }
    }

    private void RequireCurrent(ResolvedDatabaseProfile profile, long generation) {
        if (canonical.Generation != generation || canonical.Profile != profile) {
            throw Refuse(ResourceMetadataRejectionReason.OwnerRetired);
        }
    }

    private void RequireOwner(ResourcesDbContext owner, ResolvedDatabaseProfile profile) {
        if (!owner.Database.IsNpgsql()) {
            throw new NotSupportedException("Protected Resource metadata writes and observations require PostgreSQL.");
        }
        CoordinatedDatabaseTransaction.ForProfile(profile).RequireOwnerProfile(owner);
        transactions.RequireOwnerProfile(owner);
    }

    private static void RequireExpected(ResourceMetadataExpectation expected, ResourceMetadataReadResult current) {
        if (expected is ResourceMetadataExpectation.Absent) {
            if (current is not ResourceMetadataReadResult.Missing) {
                throw Refuse(ResourceMetadataRejectionReason.ResourceChanged);
            }
            return;
        }
        var actual = current switch {
            ResourceMetadataReadResult.Found found => found.Snapshot.Expected,
            ResourceMetadataReadResult.MetadataOnly metadata => metadata.Expected,
            _ => null
        };
        if (actual is null) {
            throw Refuse(ResourceMetadataRejectionReason.ResourceMissing);
        }
        if (actual != expected) {
            throw Refuse(ResourceMetadataRejectionReason.ResourceChanged);
        }
    }

    private static ResourceMetadataSnapshot Snapshot(ResourceMetadataContext context, Guid id, ProjectWriteAdmission admission,
        ResourceMetadataStoredValues values, DateTimeOffset created, DateTimeOffset updated) {
        try {
            return new(new(context, id, new(Guid.NewGuid()), new ResourceProjectBinding.Bound(admission.ProjectId, admission.LifetimeId)),
                values, created, updated);
        } catch (ArgumentException) {
            throw Refuse(ResourceMetadataRejectionReason.InvalidValues);
        }
    }

    private DateTimeOffset StoredNow() {
        var now = clock.GetUtcNow().ToUniversalTime();
        return new(now.Ticks - now.Ticks % TimeSpan.TicksPerMicrosecond, TimeSpan.Zero);
    }

    private static Guid? OriginalProject(ResourceMetadataExpectation expected)
        => expected is ResourceMetadataExpectation.Existing existing ? ResourceMetadataPreparation.ProjectId(existing.Project) : null;

    private static string[] Keys(Guid id, params Guid?[] projects)
        => projects.Where(project => project.HasValue && project != Guid.Empty).Select(project => ProjectMutationScopeKeys.ForProject(project!.Value))
            .Append($"resource:{id:D}").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    private static ResourceMetadataRefusedException Refuse(ResourceMetadataRejectionReason reason)
        => new(reason, "The Resource metadata command no longer meets its original conditions.");

    private static bool IsConflict(Exception exception) {
        var postgres = exception as PostgresException ?? (exception as DbUpdateException)?.InnerException as PostgresException;
        return postgres?.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected or
            PostgresErrorCodes.LockNotAvailable or PostgresErrorCodes.QueryCanceled or PostgresErrorCodes.UniqueViolation;
    }

    private static Task<int> InstallDeadlinesAsync(ResourcesDbContext owner, CancellationToken cancellationToken)
        => owner.Database.ExecuteSqlRawAsync("""
            SELECT set_config('transaction_timeout', '5s', true),
                set_config('statement_timeout', '2s', true), set_config('lock_timeout', '1s', true)
            """, cancellationToken);

    private sealed record Mutation<T>(T? Value, bool Acknowledged, ResourceMetadataRejectionReason? Rejection,
        ResourceMetadataSecondaryDiagnostic? Diagnostic) where T : class;
    private sealed record PromotionValue(ResourceMetadataSnapshot Snapshot, bool Created);
}
