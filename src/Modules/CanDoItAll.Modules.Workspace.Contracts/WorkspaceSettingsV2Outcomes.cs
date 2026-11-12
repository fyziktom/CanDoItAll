using System.Text.Json.Serialization;

namespace CanDoItAll.Modules.Workspace;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "status")]
[JsonDerivedType(typeof(WorkspaceSettingsSaveOutcomeV2.Accepted), "accepted")]
[JsonDerivedType(typeof(WorkspaceSettingsSaveOutcomeV2.Rejected), "rejected")]
[JsonDerivedType(typeof(WorkspaceSettingsSaveOutcomeV2.Unconfirmed), "unconfirmed")]
public abstract record WorkspaceSettingsSaveOutcomeV2 {
    private WorkspaceSettingsSaveOutcomeV2() {
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record Accepted : WorkspaceSettingsSaveOutcomeV2 {
        public Accepted(WorkspaceSettingsSnapshotV2 snapshot, WorkspaceSettingsSecondaryDiagnostic? diagnostic = null) {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (snapshot.Expectation.State != WorkspaceSettingsHeadState.Existing || snapshot.DecisionDataPolicy is null) {
                throw new ArgumentException("An accepted v2 replacement requires the exact committed head and complete policy.", nameof(snapshot));
            }
            if (diagnostic is { } value && !Enum.IsDefined(value)) {
                throw new ArgumentOutOfRangeException(nameof(diagnostic));
            }
            Snapshot = snapshot;
            Diagnostic = diagnostic;
        }

        [JsonInclude, JsonRequired, JsonPropertyName("snapshot")]
        public WorkspaceSettingsSnapshotV2 Snapshot { get; private init; }

        [JsonInclude, JsonPropertyName("diagnostic"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkspaceSettingsSecondaryDiagnostic? Diagnostic { get; private init; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record Rejected : WorkspaceSettingsSaveOutcomeV2 {
        public Rejected(WorkspaceSettingsRejectionReason reason) {
            if (!Enum.IsDefined(reason)) {
                throw new ArgumentOutOfRangeException(nameof(reason));
            }
            Reason = reason;
        }

        [JsonInclude, JsonRequired, JsonPropertyName("reason")]
        public WorkspaceSettingsRejectionReason Reason { get; private init; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record Unconfirmed : WorkspaceSettingsSaveOutcomeV2;
}

public sealed class WorkspaceSettingsPolicyUnavailableException()
    : InvalidOperationException("The stored Workspace decision policy is invalid or unsupported; explicit replacement is required.");
