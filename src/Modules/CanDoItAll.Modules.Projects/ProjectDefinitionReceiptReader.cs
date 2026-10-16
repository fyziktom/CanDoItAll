using CanDoItAll.Infrastructure.ControlPlane;
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
        if (observed.CurrentEpoch is null || observed.RetiredThroughEpoch is null) {
            throw new InvalidOperationException("Projects operation state has not been initialized for the active dataset.");
        }
        if (operation.Epoch.Value <= observed.RetiredThroughEpoch) {
            return new(new ProjectDefinitionLookup.Expired(operation), observed);
        }
        if (operation.Epoch.Value > observed.CurrentEpoch) {
            return new(new ProjectDefinitionLookup.InvalidContext(operation, ProjectDefinitionInvalidContextReason.UnknownFutureEpoch), observed);
        }
        if (observed.EpochState is null || observed.AdmissionClosesAtUtc is null || observed.ReplayRetentionSeconds is null ||
                observed.MutationDeadlineMilliseconds is null) {
            throw new InvalidOperationException("Retained Projects epoch evidence is missing or invalid.");
        }
        if (observed.AcceptedPayload is not null) {
            return new(await ReadAcceptedAsync(operation, invocation, observed, cancellationToken), observed);
        }
        if (observed.EpochState == (int)ProjectOperationEpochState.Closed && observed.ClosedAtUtc is { } closed) {
            var promised = observed.AdmissionClosesAtUtc.Value.AddMilliseconds(observed.MutationDeadlineMilliseconds.Value)
                .AddSeconds(observed.ReplayRetentionSeconds.Value);
            var retained = closed.AddSeconds(observed.ReplayRetentionSeconds.Value);
            return new(new ProjectDefinitionLookup.ClosedAbsent(operation, closed, observed.ObservedAtUtc,
                promised > retained ? promised : retained), observed);
        }
        if (observed.EpochState != (int)ProjectOperationEpochState.Open || operation.Epoch.Value != observed.CurrentEpoch) {
            throw new InvalidOperationException("The retained Projects epoch has an inconsistent lifecycle state.");
        }
        return new(new ProjectDefinitionLookup.NotObserved(operation,
            observed.AdmissionClosesAtUtc > observed.ObservedAtUtc ? ProjectOperationObservationState.Open : ProjectOperationObservationState.Draining), observed);
    }

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
