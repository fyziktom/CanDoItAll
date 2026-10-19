using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.Projects;

internal sealed class ProjectLifecycleReceiptRow {
    internal const int RequiredRetentionProtocol = 2;
    public Guid ActiveNamespaceId { get; set; }
    public Guid MaintenanceId { get; set; }
    public int DatasetSchemaVersion { get; set; }
    public int GuardProtocol { get; set; }
    public int DatasetState { get; set; }
    public long? CurrentEpoch { get; set; }
    public long? RetiredThroughEpoch { get; set; }
    public int? RetentionProtocolVersion { get; set; }
    public int? EpochState { get; set; }
    public DateTimeOffset? OpenedAtUtc { get; set; }
    public DateTimeOffset? AdmissionClosesAtUtc { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public int? MutationDeadlineMilliseconds { get; set; }
    public long? ReplayRetentionSeconds { get; set; }
    public DateTimeOffset ObservedAtUtc { get; set; }
    public bool ReceiptPresent { get; set; }
    public Guid? DatabaseProfileId { get; set; }
    public int? SchemaVersion { get; set; }
    public int? NormalizationVersion { get; set; }
    public int? ResultVersion { get; set; }
    public int? CommandKind { get; set; }
    public byte[]? SemanticDigest { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? LifetimeId { get; set; }
    public DateTimeOffset? AcceptedAtUtc { get; set; }
    public byte[]? AcceptedPayload { get; set; }

    internal bool Matches(DatasetActivationBinding binding)
        => ActiveNamespaceId == binding.Namespace.Value && MaintenanceId == binding.MaintenanceId &&
            DatasetSchemaVersion == binding.SchemaVersion && GuardProtocol == (int)binding.GuardProtocol &&
            DatasetState == (int)DatasetLifecycleState.Active;

    internal ProjectLifecycleLookup Interpret(ProjectLifecycleOperation original) {
        if (RetentionProtocolVersion is null || CurrentEpoch is null || RetiredThroughEpoch is null) {
            return Unavailable(ProjectLifecycleUnavailableReason.MissingMetadata);
        }
        if (RetentionProtocolVersion != RequiredRetentionProtocol) {
            return Unavailable(ProjectLifecycleUnavailableReason.UnsupportedProtocol);
        }
        if (CurrentEpoch <= 0 || RetiredThroughEpoch < 0 || RetiredThroughEpoch >= CurrentEpoch || !FiniteUtc(ObservedAtUtc)) {
            return Unavailable(ProjectLifecycleUnavailableReason.InconsistentMetadata);
        }
        if (original.Epoch.Value <= RetiredThroughEpoch) {
            return new ProjectLifecycleLookup.Expired(original);
        }
        if (original.Epoch.Value > CurrentEpoch) {
            if (ReceiptPresent || EpochState is not null || OpenedAtUtc is not null || AdmissionClosesAtUtc is not null ||
                    ClosedAtUtc is not null || MutationDeadlineMilliseconds is not null || ReplayRetentionSeconds is not null) {
                return Unavailable(ProjectLifecycleUnavailableReason.InconsistentMetadata);
            }
            return new ProjectLifecycleLookup.UnknownFutureEpoch(original);
        }
        if (EpochState is null || OpenedAtUtc is null || AdmissionClosesAtUtc is null ||
                MutationDeadlineMilliseconds is null || ReplayRetentionSeconds is null) {
            return Unavailable(ProjectLifecycleUnavailableReason.MissingMetadata);
        }
        if (!FiniteUtc(OpenedAtUtc.Value) || !FiniteUtc(AdmissionClosesAtUtc.Value) || AdmissionClosesAtUtc <= OpenedAtUtc ||
                MutationDeadlineMilliseconds is < 1 or > 60_000 || ReplayRetentionSeconds is < 1 or > 31_536_000 ||
                EpochState == (int)FiniteOperationEpochState.Open && (ClosedAtUtc is not null || original.Epoch.Value != CurrentEpoch) ||
                EpochState == (int)FiniteOperationEpochState.Closed &&
                    (ClosedAtUtc is null || !FiniteUtc(ClosedAtUtc.Value) || ClosedAtUtc < OpenedAtUtc || ClosedAtUtc > ObservedAtUtc) ||
                EpochState is not ((int)FiniteOperationEpochState.Open or (int)FiniteOperationEpochState.Closed)) {
            return Unavailable(ProjectLifecycleUnavailableReason.InconsistentMetadata);
        }
        DateTimeOffset mutationCloses;
        DateTimeOffset promised;
        try {
            mutationCloses = AdmissionClosesAtUtc.Value.AddMilliseconds(MutationDeadlineMilliseconds.Value);
            promised = mutationCloses.AddSeconds(ReplayRetentionSeconds.Value);
        } catch (ArgumentOutOfRangeException) {
            return Unavailable(ProjectLifecycleUnavailableReason.InconsistentMetadata);
        }
        if (ReceiptPresent) {
            if (SchemaVersion != 1 || NormalizationVersion != 1 || ResultVersion != ProjectLifecycleAccepted.VersionOne) {
                return Unavailable(ProjectLifecycleUnavailableReason.UnsupportedResult);
            }
            if (DatabaseProfileId != original.DatabaseProfileId || SemanticDigest is not { Length: 32 } ||
                    AcceptedPayload is null || AcceptedAtUtc is not { } acceptedAt || !FiniteUtc(acceptedAt) ||
                    acceptedAt < OpenedAtUtc || acceptedAt >= mutationCloses) {
                return Unavailable(ProjectLifecycleUnavailableReason.InvalidStoredResult);
            }
            try {
                var accepted = ProjectLifecycleOperationCodec.DecodeAccepted(AcceptedPayload, original);
                var target = accepted.Original.Command switch {
                    ProjectLifecycleCommand.Delete command => command.Expected.Project,
                    ProjectLifecycleCommand.EnsureLink command => command.Child,
                    ProjectLifecycleCommand.RemoveLink command => command.Expected.Child,
                    ProjectLifecycleCommand.ReconnectLink command => command.Expected.Child,
                    _ => throw new InvalidDataException("Unsupported retained lifecycle target.")
                };
                if (accepted.AcceptedAtUtc != AcceptedAtUtc || accepted.ReplayUntilUtc != promised ||
                        (int)accepted.Original.Command.Kind != CommandKind || target.ProjectId != ProjectId || target.LifetimeId != LifetimeId) {
                    return Unavailable(ProjectLifecycleUnavailableReason.InvalidStoredResult);
                }
                return new ProjectLifecycleLookup.Found(accepted);
            } catch (Exception failure) when (failure is InvalidDataException or NotSupportedException) {
                return Unavailable(ProjectLifecycleUnavailableReason.InvalidStoredResult);
            }
        }
        if (EpochState == (int)FiniteOperationEpochState.Closed) {
            try {
                var retained = ClosedAtUtc!.Value.AddSeconds(ReplayRetentionSeconds.Value);
                return new ProjectLifecycleLookup.ClosedAbsent(original, ClosedAtUtc.Value, ObservedAtUtc,
                    retained > promised ? retained : promised);
            } catch (ArgumentOutOfRangeException) {
                return Unavailable(ProjectLifecycleUnavailableReason.InconsistentMetadata);
            }
        }
        return new ProjectLifecycleLookup.NotObserved(original, AdmissionClosesAtUtc > ObservedAtUtc
            ? ProjectOperationObservationState.Open : ProjectOperationObservationState.Draining);

        ProjectLifecycleLookup Unavailable(ProjectLifecycleUnavailableReason reason) => new ProjectLifecycleLookup.Unavailable(original, reason);
    }

    private static bool FiniteUtc(DateTimeOffset value)
        => value.Offset == TimeSpan.Zero && value != DateTimeOffset.MinValue && value != DateTimeOffset.MaxValue;
}
