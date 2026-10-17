using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectDefinitionReceiptReader(
    IDbContextFactory<ProjectsDbContext> factory,
    DatasetActivationService activation,
    ProjectWriteAdmissionService admissions,
    ILogger<ProjectDefinitionReceiptReader> logger) {
    public Task<ProjectDefinitionLookup> LookupAsync(ProjectDefinitionOperation operation,
        ProjectDefinitionInvocation invocation, CancellationToken cancellationToken = default)
        => LookupAsync(operation, new ProjectDefinitionReceiptInvocation(invocation.Caller, invocation.ReceiptAccess), cancellationToken);

    public async Task<ProjectDefinitionLookup> LookupAsync(ProjectDefinitionOperation operation,
        ProjectDefinitionReceiptInvocation invocation, CancellationToken cancellationToken = default) {
        var binding = await activation.ReadActiveBindingAsync(admissions.DatabaseProfileId, cancellationToken);
        return (await ObserveAsync(binding, operation, invocation, cancellationToken)).Lookup;
    }

    public async Task<ProjectDefinitionLookup> LookupHistoricalAsync(ProjectDefinitionOperation operation,
        ProjectDefinitionReceiptInvocation invocation, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(invocation);
        var before = await activation.ObserveActiveBindingAsync(admissions.DatabaseProfileId, cancellationToken);
        var observed = await ObserveAsync(before.Record.Plan.Binding, operation, invocation, cancellationToken);
        var lookup = observed.Lookup;
        if (lookup is ProjectDefinitionLookup.FoundFull or ProjectDefinitionLookup.FoundScalars) {
            var evidence = observed.Evidence ?? throw new InvalidDataException("A historical accepted receipt has no owner observation.");
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var final = await context.Database.SqlQuery<ProjectDefinitionHistoricalTargetObservation>($"""
                SELECT d."NamespaceId", d."MaintenanceId", d."SchemaVersion", d."GuardProtocol", d."State",
                       EXISTS (SELECT 1 FROM "Projects_Projects" AS p
                           WHERE p."Id" = {evidence.ProjectId} AND p."LifetimeId" = {evidence.LifetimeId}
                           AND NOT EXISTS (SELECT 1 FROM "Projects_ProjectRetirements" AS r WHERE r."LifetimeId" = p."LifetimeId")) AS "TargetAvailable"
                FROM "Infrastructure_DatasetLifecycle" AS d WHERE d."Id" = 1
                """).SingleOrDefaultAsync(cancellationToken);
            var binding = before.Record.Plan.Binding;
            if (final is null || final.NamespaceId != binding.Namespace.Value || final.MaintenanceId != binding.MaintenanceId ||
                final.SchemaVersion != binding.SchemaVersion || final.GuardProtocol != (int)binding.GuardProtocol ||
                final.State != (int)DatasetLifecycleState.Active) {
                lookup = new ProjectDefinitionLookup.DatasetChanged(operation);
            } else if (!final.TargetAvailable) {
                lookup = new ProjectDefinitionLookup.DetailUnavailable(operation, ProjectDefinitionDetailUnavailableReason.TargetUnavailable);
            }
        }
        var after = await activation.ObserveActiveBindingAsync(admissions.DatabaseProfileId, cancellationToken);
        return before == after ? lookup : new ProjectDefinitionLookup.DatasetChanged(operation);
    }
    internal Task<ProjectDefinitionObservedOperation> ObserveAsync(DatasetActivationBinding binding,
        ProjectDefinitionOperation operation, ProjectDefinitionInvocation invocation, CancellationToken cancellationToken)
        => ObserveAsync(binding, operation, new ProjectDefinitionReceiptInvocation(invocation.Caller, invocation.ReceiptAccess), cancellationToken);

    internal async Task<ProjectDefinitionObservedOperation> ObserveAsync(DatasetActivationBinding binding,
        ProjectDefinitionOperation operation, ProjectDefinitionReceiptInvocation invocation, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(invocation);
        if (operation.Namespace != binding.Namespace) {
            return new(new ProjectDefinitionLookup.DatasetChanged(operation), null);
        }
        if (!invocation.Caller.Matches(binding.Namespace, operation.CallerBinding)) {
            return new(new ProjectDefinitionLookup.CallerContextChanged(operation), null);
        }
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        var observed = await ProjectDefinitionOperationStore.ObserveAsync(context, operation, invocation.Caller, cancellationToken)
            ?? throw new InvalidOperationException("The dataset lifecycle has not been initialized.");
        if (observed.ActiveNamespaceId != binding.Namespace.Value || observed.MaintenanceId != binding.MaintenanceId ||
                observed.DatasetSchemaVersion != binding.SchemaVersion || observed.GuardProtocol != (int)binding.GuardProtocol ||
                observed.DatasetState != (int)DatasetLifecycleState.Active) {
            return new(new ProjectDefinitionLookup.DatasetChanged(operation), null);
        }
        if (observed.RetentionProtocolVersion is null) {
            throw Protocol(FiniteOperationProtocolFailure.MissingState);
        }
        if (observed.RetentionProtocolVersion != ProjectFiniteOperationPool.ProtocolVersion) {
            throw Protocol(FiniteOperationProtocolFailure.UnsupportedProtocol);
        }
        if (observed.CurrentEpoch is not > 0 || observed.RetiredThroughEpoch is not >= 0 || observed.RetiredThroughEpoch >= observed.CurrentEpoch) {
            throw Protocol(FiniteOperationProtocolFailure.InconsistentMetadata);
        }
        if (operation.Epoch.Value <= observed.RetiredThroughEpoch) {
            return new(new ProjectDefinitionLookup.Expired(operation), observed);
        }
        if (operation.Epoch.Value > observed.CurrentEpoch) {
            return new(new ProjectDefinitionLookup.InvalidContext(operation, ProjectDefinitionInvalidContextReason.UnknownFutureEpoch), observed);
        }
        if (observed.EpochState is null || observed.AdmissionClosesAtUtc is null || observed.ReplayRetentionSeconds is null ||
                observed.MutationDeadlineMilliseconds is null) {
            throw Protocol(FiniteOperationProtocolFailure.MissingRetainedEpoch);
        }
        if (observed.AcceptedPayload is not null) {
            return new(await ReadAcceptedAsync(operation, invocation, observed, cancellationToken), observed);
        }
        if (observed.EpochState == (int)FiniteOperationEpochState.Closed && observed.ClosedAtUtc is { } closed) {
            var promised = observed.AdmissionClosesAtUtc.Value.AddMilliseconds(observed.MutationDeadlineMilliseconds.Value)
                .AddSeconds(observed.ReplayRetentionSeconds.Value);
            var retained = closed.AddSeconds(observed.ReplayRetentionSeconds.Value);
            return new(new ProjectDefinitionLookup.ClosedAbsent(operation, closed, observed.ObservedAtUtc,
                promised > retained ? promised : retained), observed);
        }
        if (observed.EpochState != (int)FiniteOperationEpochState.Open || operation.Epoch.Value != observed.CurrentEpoch) {
            throw Protocol(FiniteOperationProtocolFailure.InconsistentMetadata);
        }
        return new(new ProjectDefinitionLookup.NotObserved(operation,
            observed.AdmissionClosesAtUtc > observed.ObservedAtUtc ? ProjectOperationObservationState.Open : ProjectOperationObservationState.Draining), observed);
    }

    private static FiniteOperationProtocolException Protocol(FiniteOperationProtocolFailure failure)
        => new(failure, ProjectFiniteOperationPool.Pool, ProjectFiniteOperationPool.ProtocolVersion);

    private async Task<ProjectDefinitionLookup> ReadAcceptedAsync(ProjectDefinitionOperation operation,
        ProjectDefinitionReceiptInvocation invocation, ProjectDefinitionReceiptObservation observed, CancellationToken cancellationToken) {
        if (!observed.TargetAvailable || observed.ProjectId is not { } projectId || observed.LifetimeId is not { } lifetimeId) {
            return new ProjectDefinitionLookup.DetailUnavailable(operation, ProjectDefinitionDetailUnavailableReason.TargetUnavailable);
        }
        if (!await invocation.ReceiptAccess.CanReadAsync(new(admissions.DatabaseProfileId, projectId, lifetimeId), cancellationToken)) {
            return new ProjectDefinitionLookup.DetailUnavailable(operation, ProjectDefinitionDetailUnavailableReason.AccessDenied);
        }
        if (observed.ResultVersion != ProjectDefinitionResultVersion.VersionOne.Value || observed.SchemaVersion != 1 ||
                observed.NormalizationVersion != 1) {
            return new ProjectDefinitionLookup.DetailUnavailable(operation, ProjectDefinitionDetailUnavailableReason.UnsupportedVersion);
        }
        try {
            var decoded = ProjectDefinitionOperationCodec.DecodeAccepted(ProjectDefinitionResultVersion.VersionOne,
                observed.AcceptedPayload!, ProjectDefinitionLimits.VersionOneMaximum);
            return decoded switch {
                ProjectDefinitionDecodedReceipt.Full full when Matches(full.Result, full.Result.Acknowledgement.Definition)
                    => new ProjectDefinitionLookup.FoundFull(full.Result),
                ProjectDefinitionDecodedReceipt.Scalars scalar when Matches(scalar.Result, scalar.Result.Acknowledgement.Definition)
                    => new ProjectDefinitionLookup.FoundScalars(scalar.Result),
                _ => InvalidResult()
            };
        } catch (ProjectDefinitionReceiptDecodeException exception) {
            logger.LogError(exception, "Stored Projects result is invalid for dataset {NamespaceId}, epoch {Epoch}, operation {OperationId}.",
                operation.Namespace.Value, operation.Epoch.Value, operation.Id);
            return new ProjectDefinitionLookup.DetailUnavailable(operation, ProjectDefinitionDetailUnavailableReason.InvalidStoredResult);
        }

        bool Matches<T>(ProjectDefinitionAccepted<T> result, ProjectDefinitionExpectation definition) where T : class
            => result.Operation == operation && (int)result.Kind == observed.CommandKind && result.AcceptedAtUtc == observed.AcceptedAtUtc &&
                result.ReplayUntilUtc == observed.AdmissionClosesAtUtc!.Value.AddMilliseconds(observed.MutationDeadlineMilliseconds!.Value)
                    .AddSeconds(observed.ReplayRetentionSeconds!.Value) &&
                definition.Project.ProjectId == observed.ProjectId && definition.Project.LifetimeId == observed.LifetimeId &&
                definition.Revision == observed.AcceptedRevision;

        ProjectDefinitionLookup InvalidResult() {
            logger.LogError("Stored Projects result disagrees with its indexed metadata for dataset {NamespaceId}, epoch {Epoch}, operation {OperationId}.",
                operation.Namespace.Value, operation.Epoch.Value, operation.Id);
            return new ProjectDefinitionLookup.DetailUnavailable(operation, ProjectDefinitionDetailUnavailableReason.InvalidStoredResult);
        }
    }
}

internal sealed record ProjectDefinitionObservedOperation(ProjectDefinitionLookup Lookup, ProjectDefinitionReceiptObservation? Evidence);

internal sealed class ProjectDefinitionHistoricalTargetObservation {
    public Guid NamespaceId { get; set; }
    public Guid MaintenanceId { get; set; }
    public int SchemaVersion { get; set; }
    public int GuardProtocol { get; set; }
    public int State { get; set; }
    public bool TargetAvailable { get; set; }
}