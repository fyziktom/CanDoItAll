using System.Text.Json.Serialization;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workspace;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkspaceSettingsValues {
    public WorkspaceSettingsValues(string workspaceName, Guid? defaultProviderProfileId, string defaultPromptOutputFormat,
        string currencyCode, string currencyCultureName, string notes) {
        ArgumentNullException.ThrowIfNull(workspaceName);
        ArgumentNullException.ThrowIfNull(defaultPromptOutputFormat);
        ArgumentNullException.ThrowIfNull(currencyCode);
        ArgumentNullException.ThrowIfNull(currencyCultureName);
        ArgumentNullException.ThrowIfNull(notes);
        if (defaultProviderProfileId == Guid.Empty) {
            throw new ArgumentException("A selected default provider must have a nonempty identifier.", nameof(defaultProviderProfileId));
        }
        WorkspaceName = workspaceName;
        DefaultProviderProfileId = defaultProviderProfileId;
        DefaultPromptOutputFormat = defaultPromptOutputFormat;
        CurrencyCode = currencyCode;
        CurrencyCultureName = currencyCultureName;
        Notes = notes;
    }

    [JsonInclude, JsonRequired, JsonPropertyName("workspaceName")]
    public string WorkspaceName { get; private init; }

    [JsonInclude, JsonRequired, JsonPropertyName("defaultProviderProfileId"), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public Guid? DefaultProviderProfileId { get; private init; }

    [JsonInclude, JsonRequired, JsonPropertyName("defaultPromptOutputFormat")]
    public string DefaultPromptOutputFormat { get; private init; }

    [JsonInclude, JsonRequired, JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; private init; }

    [JsonInclude, JsonRequired, JsonPropertyName("currencyCultureName")]
    public string CurrencyCultureName { get; private init; }

    [JsonInclude, JsonRequired, JsonPropertyName("notes")]
    public string Notes { get; private init; }

    public static WorkspaceSettingsValues Capture(WorkspaceSettingsModel model) {
        ArgumentNullException.ThrowIfNull(model);
        return new(model.WorkspaceName, model.DefaultProviderProfileId, model.DefaultPromptOutputFormat,
            model.CurrencyCode, model.CurrencyCultureName, model.Notes);
    }

    public WorkspaceSettingsModel ToEditorModel() => new() {
        WorkspaceName = WorkspaceName,
        DefaultProviderProfileId = DefaultProviderProfileId,
        DefaultPromptOutputFormat = DefaultPromptOutputFormat,
        CurrencyCode = CurrencyCode,
        CurrencyCultureName = CurrencyCultureName,
        Notes = Notes
    };
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkspaceSettingsHead {
    public WorkspaceSettingsHead(Guid id, Guid revision) {
        if (id == Guid.Empty || revision == Guid.Empty) {
            throw new ArgumentException("A workspace settings head requires nonempty row and revision identifiers.");
        }
        Id = id;
        Revision = revision;
    }

    [JsonInclude, JsonRequired, JsonPropertyName("id")]
    public Guid Id { get; private init; }

    [JsonInclude, JsonRequired, JsonPropertyName("revision")]
    public Guid Revision { get; private init; }
}

public enum WorkspaceSettingsHeadState { Empty = 1, Existing = 2 }

[JsonConverter(typeof(WorkspaceSettingsExpectationJsonConverter))]
public sealed record WorkspaceSettingsExpectation {
    public WorkspaceSettingsExpectation(DatasetNamespaceId @namespace, WorkspaceSettingsHeadState state, WorkspaceSettingsHead? head) {
        ArgumentNullException.ThrowIfNull(@namespace);
        if (!Enum.IsDefined(state) || (state == WorkspaceSettingsHeadState.Existing) != (head is not null)) {
            throw new ArgumentException("A settings expectation must explicitly identify an empty dataset head or an existing head.");
        }
        Namespace = @namespace;
        State = state;
        Head = head;
    }

    public DatasetNamespaceId Namespace { get; }
    public WorkspaceSettingsHeadState State { get; }
    public WorkspaceSettingsHead? Head { get; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkspaceSettingsSnapshot {
    public WorkspaceSettingsSnapshot(WorkspaceSettingsExpectation expectation, WorkspaceSettingsValues values) {
        ArgumentNullException.ThrowIfNull(expectation);
        ArgumentNullException.ThrowIfNull(values);
        Expectation = expectation;
        Values = values;
    }

    [JsonInclude, JsonRequired, JsonPropertyName("expectation")]
    public WorkspaceSettingsExpectation Expectation { get; private init; }

    [JsonInclude, JsonRequired, JsonPropertyName("values")]
    public WorkspaceSettingsValues Values { get; private init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReplaceWorkspaceSettingsCommand {
    public ReplaceWorkspaceSettingsCommand(WorkspaceSettingsExpectation expected, WorkspaceSettingsValues values) {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(values);
        Expected = expected;
        Values = values;
    }

    [JsonInclude, JsonRequired, JsonPropertyName("expected")]
    public WorkspaceSettingsExpectation Expected { get; private init; }

    [JsonInclude, JsonRequired, JsonPropertyName("values")]
    public WorkspaceSettingsValues Values { get; private init; }
}

public enum WorkspaceSettingsRejectionReason { InvalidValues = 1, HeadChanged = 2, DatasetChanged = 3, TransactionConflict = 4, OwnerRetired = 5 }
public enum WorkspaceSettingsSecondaryDiagnostic { ActivityPending = 1, CurrencyPublicationPending = 2, LoggingPending = 3, CleanupPending = 4 }

[JsonPolymorphic(TypeDiscriminatorPropertyName = "status")]
[JsonDerivedType(typeof(WorkspaceSettingsSaveOutcome.Accepted), "accepted")]
[JsonDerivedType(typeof(WorkspaceSettingsSaveOutcome.Rejected), "rejected")]
[JsonDerivedType(typeof(WorkspaceSettingsSaveOutcome.Unconfirmed), "unconfirmed")]
public abstract record WorkspaceSettingsSaveOutcome {
    private WorkspaceSettingsSaveOutcome() {
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record Accepted : WorkspaceSettingsSaveOutcome {
        public Accepted(WorkspaceSettingsSnapshot snapshot, WorkspaceSettingsSecondaryDiagnostic? diagnostic = null) {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (snapshot.Expectation.State != WorkspaceSettingsHeadState.Existing) {
                throw new ArgumentException("An accepted replacement must identify the exact existing committed head.", nameof(snapshot));
            }
            if (diagnostic is { } value && !Enum.IsDefined(value)) {
                throw new ArgumentOutOfRangeException(nameof(diagnostic));
            }
            Snapshot = snapshot;
            Diagnostic = diagnostic;
        }

        [JsonInclude, JsonRequired, JsonPropertyName("snapshot")]
        public WorkspaceSettingsSnapshot Snapshot { get; private init; }

        [JsonInclude, JsonPropertyName("diagnostic"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkspaceSettingsSecondaryDiagnostic? Diagnostic { get; private init; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record Rejected : WorkspaceSettingsSaveOutcome {
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
    public sealed record Unconfirmed : WorkspaceSettingsSaveOutcome;
}
