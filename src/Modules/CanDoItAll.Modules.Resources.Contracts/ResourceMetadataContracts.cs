using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Modules.Resources;

public sealed record ResourceMetadataRevision {
    public ResourceMetadataRevision(Guid value) {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    public Guid Value { get; }
}

public sealed record ResourceMetadataContext {
    public ResourceMetadataContext(Guid databaseProfileId, DatasetNamespaceId @namespace) {
        ArgumentOutOfRangeException.ThrowIfEqual(databaseProfileId, Guid.Empty);
        ArgumentNullException.ThrowIfNull(@namespace);
        DatabaseProfileId = databaseProfileId;
        Namespace = @namespace;
    }

    public Guid DatabaseProfileId { get; }
    public DatasetNamespaceId Namespace { get; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ResourceProjectBinding.Bound), "bound")]
[JsonDerivedType(typeof(ResourceProjectBinding.RetainedProject), "retainedProject")]
[JsonDerivedType(typeof(ResourceProjectBinding.RetainedUnbound), "retainedUnbound")]
public abstract record ResourceProjectBinding {
    private ResourceProjectBinding() {
    }

    public sealed record Bound : ResourceProjectBinding {
        public Bound(Guid projectId, Guid lifetimeId) {
            ArgumentOutOfRangeException.ThrowIfEqual(projectId, Guid.Empty);
            ArgumentOutOfRangeException.ThrowIfEqual(lifetimeId, Guid.Empty);
            ProjectId = projectId;
            LifetimeId = lifetimeId;
        }

        public Guid ProjectId { get; }
        public Guid LifetimeId { get; }
    }

    public sealed record RetainedProject : ResourceProjectBinding {
        public RetainedProject(Guid projectId) {
            ArgumentOutOfRangeException.ThrowIfEqual(projectId, Guid.Empty);
            ProjectId = projectId;
        }

        public Guid ProjectId { get; }
    }

    public sealed record RetainedUnbound : ResourceProjectBinding;
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ResourceMetadataExpectation.Absent), "absent")]
[JsonDerivedType(typeof(ResourceMetadataExpectation.Existing), "existing")]
public abstract record ResourceMetadataExpectation {
    private ResourceMetadataExpectation(ResourceMetadataContext context, Guid resourceId) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfEqual(resourceId, Guid.Empty);
        Context = context;
        ResourceId = resourceId;
    }

    public ResourceMetadataContext Context { get; }
    public Guid ResourceId { get; }

    public sealed record Absent : ResourceMetadataExpectation {
        public Absent(ResourceMetadataContext context, Guid resourceId) : base(context, resourceId) {
        }
    }

    public sealed record Existing : ResourceMetadataExpectation {
        public Existing(ResourceMetadataContext context, Guid resourceId, ResourceMetadataRevision revision,
            ResourceProjectBinding project) : base(context, resourceId) {
            ArgumentNullException.ThrowIfNull(revision);
            ArgumentNullException.ThrowIfNull(project);
            Revision = revision;
            Project = project;
        }

        public ResourceMetadataRevision Revision { get; }
        public ResourceProjectBinding Project { get; }
    }
}

public sealed record ResourceMetadataValues {
    private ResourceMetadataValues(ResourceEditorModel editor) {
        Name = editor.Name;
        Description = editor.Description;
        ConnectorPluginKey = editor.ConnectorPluginKey;
        ConfigSchemaVersion = editor.ConfigSchemaVersion;
        LocationOrIdentifier = editor.LocationOrIdentifier;
        ConfigJson = editor.ConfigJson;
        OwnerPartyId = editor.OwnerPartyId;
        MaintainerPartyId = editor.MaintainerPartyId;
        LinkedSecretId = editor.LinkedSecretId;
        ValidationStatus = editor.ValidationStatus;
        Sensitivity = editor.Sensitivity;
        SupportsPreview = editor.SupportsPreview;
        SupportsIndexing = editor.SupportsIndexing;
        if (OwnerPartyId == Guid.Empty || MaintainerPartyId == Guid.Empty || LinkedSecretId == Guid.Empty ||
            !Enum.IsDefined(ValidationStatus) || !Enum.IsDefined(Sensitivity)) {
            throw new ArgumentException("Resource metadata contains an invalid reference or status.", nameof(editor));
        }
        var scalarBytes = ResourceMetadataLimits.RawScalars(Name, Description, ConnectorPluginKey,
            ConfigSchemaVersion, LocationOrIdentifier, ConfigJson);
        ArgumentNullException.ThrowIfNull(editor.Configuration);
        var entries = editor.Configuration.Values;
        if (entries.Count > ResourceMetadataLimits.ConfigurationEntries) {
            throw new ArgumentException("The Resource configuration has too many entries.", nameof(editor));
        }
        var count = 0;
        var bytes = scalarBytes;
        foreach (var entry in entries) {
            ResourceMetadataLimits.AddEntry(entry.Key, entry.Value, ref count, ref bytes);
        }
        var copied = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.OrdinalIgnoreCase);
        count = 0;
        bytes = scalarBytes;
        foreach (var entry in entries) {
            ResourceMetadataLimits.AddEntry(entry.Key, entry.Value, ref count, ref bytes);
            copied.Add(entry.Key, entry.Value);
        }
        Configuration = copied.ToImmutable();
    }

    public string Name { get; }
    public string Description { get; }
    public string ConnectorPluginKey { get; }
    public string ConfigSchemaVersion { get; }
    public string LocationOrIdentifier { get; }
    public string ConfigJson { get; }
    public Guid? OwnerPartyId { get; }
    public Guid? MaintainerPartyId { get; }
    public Guid? LinkedSecretId { get; }
    public ResourceValidationStatus ValidationStatus { get; }
    public ResourceSensitivity Sensitivity { get; }
    public bool SupportsPreview { get; }
    public bool SupportsIndexing { get; }
    public ImmutableDictionary<string, string> Configuration { get; }

    public static ResourceMetadataValues Capture(ResourceEditorModel editor) {
        ArgumentNullException.ThrowIfNull(editor);
        return new(editor);
    }

    public ResourceEditorModel ToEditorModel() => new() {
        Name = Name,
        Description = Description,
        ConnectorPluginKey = ConnectorPluginKey,
        ConfigSchemaVersion = ConfigSchemaVersion,
        LocationOrIdentifier = LocationOrIdentifier,
        ConfigJson = ConfigJson,
        OwnerPartyId = OwnerPartyId,
        MaintainerPartyId = MaintainerPartyId,
        LinkedSecretId = LinkedSecretId,
        ValidationStatus = ValidationStatus,
        Sensitivity = Sensitivity,
        SupportsPreview = SupportsPreview,
        SupportsIndexing = SupportsIndexing,
        Configuration = new ConnectorConfigState(Configuration)
    };
}

public sealed record ReplaceResourceMetadataCommand {
    public ReplaceResourceMetadataCommand(ResourceMetadataExpectation expected, ProjectWriteAdmission targetProject,
        ResourceMetadataValues values) {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(targetProject);
        ArgumentNullException.ThrowIfNull(values);
        if (targetProject.DatabaseProfileId != expected.Context.DatabaseProfileId) {
            throw new ArgumentException("The selected project belongs to a different Resource owner profile.", nameof(targetProject));
        }
        Expected = expected;
        TargetProject = targetProject;
        Values = values;
    }

    public ResourceMetadataExpectation Expected { get; }
    public ProjectWriteAdmission TargetProject { get; }
    public ResourceMetadataValues Values { get; }
}

public sealed record DeleteResourceMetadataCommand {
    public DeleteResourceMetadataCommand(ResourceMetadataExpectation.Existing expected) {
        ArgumentNullException.ThrowIfNull(expected);
        Expected = expected;
    }

    public ResourceMetadataExpectation.Existing Expected { get; }
}

public sealed record ResourceMetadataStoredValues(ResourceKind? LegacyResourceKind, Guid? OwnerPartyId,
    Guid? MaintainerPartyId, string Name, string Description, string ConnectorPluginKey, string ConfigSchemaVersion,
    string LocationOrIdentifier, string ConfigJson, string LinkedSecretIdsJson, ResourceValidationStatus ValidationStatus,
    ResourceSensitivity Sensitivity, bool SupportsPreview, bool SupportsIndexing);

public sealed record ResourceMetadataSnapshot {
    public ResourceMetadataSnapshot(ResourceMetadataExpectation.Existing expected, ResourceMetadataStoredValues values,
        DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc) {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(values.LinkedSecretIdsJson);
        ResourceMetadataLimits.RawScalars(values.Name, values.Description, values.ConnectorPluginKey,
            values.ConfigSchemaVersion, values.LocationOrIdentifier, values.ConfigJson, values.LinkedSecretIdsJson);
        if (values.OwnerPartyId == Guid.Empty || values.MaintainerPartyId == Guid.Empty ||
            !Enum.IsDefined(values.ValidationStatus) || !Enum.IsDefined(values.Sensitivity) ||
            values.LegacyResourceKind is { } kind && !Enum.IsDefined(kind)) {
            throw new ArgumentException("The stored Resource metadata references or status are invalid.", nameof(values));
        }
        Expected = expected;
        Values = values;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        if (JsonSerializer.SerializeToUtf8Bytes(this).Length > ResourceMetadataLimits.EncodedSnapshotBytes) {
            throw new ArgumentException("The encoded Resource snapshot exceeds its supported byte limit.", nameof(values));
        }
    }

    public ResourceMetadataExpectation.Existing Expected { get; }
    public ResourceMetadataStoredValues Values { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset UpdatedAtUtc { get; }
}

public enum ResourceMetadataReadOnlyReason { GovernedStorageObject = 1, UnavailableConnector, UnsupportedSecretReferences, InvalidConfiguration, TooLarge, InvalidMetadata }
public sealed record ResourceMetadataLengths(long? Name, long? Description, long? ConnectorPluginKey,
    long? ConfigSchemaVersion, long? LocationOrIdentifier, long? ConfigJson, long? LinkedSecretIdsJson);

public abstract record ResourceMetadataReadResult {
    private ResourceMetadataReadResult() {
    }

    public sealed record Found : ResourceMetadataReadResult {
        public Found(ResourceMetadataSnapshot snapshot, ResourceMetadataReadOnlyReason? readOnlyReason = null) {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (readOnlyReason is { } value && !Enum.IsDefined(value)) {
                throw new ArgumentOutOfRangeException(nameof(readOnlyReason));
            }
            Snapshot = snapshot;
            ReadOnlyReason = readOnlyReason;
        }

        public ResourceMetadataSnapshot Snapshot { get; }
        public ResourceMetadataReadOnlyReason? ReadOnlyReason { get; }
    }

    public sealed record Missing : ResourceMetadataReadResult {
        public Missing(ResourceMetadataContext context, Guid resourceId) {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentOutOfRangeException.ThrowIfEqual(resourceId, Guid.Empty);
            Context = context;
            ResourceId = resourceId;
        }

        public ResourceMetadataContext Context { get; }
        public Guid ResourceId { get; }
    }

    public sealed record MetadataOnly : ResourceMetadataReadResult {
        public MetadataOnly(ResourceMetadataExpectation.Existing expected, ResourceMetadataLengths lengths,
            ResourceMetadataReadOnlyReason reason) {
            ArgumentNullException.ThrowIfNull(expected);
            ArgumentNullException.ThrowIfNull(lengths);
            if (!Enum.IsDefined(reason)) {
                throw new ArgumentOutOfRangeException(nameof(reason));
            }
            Expected = expected;
            Lengths = lengths;
            Reason = reason;
        }

        public ResourceMetadataExpectation.Existing Expected { get; }
        public ResourceMetadataLengths Lengths { get; }
        public ResourceMetadataReadOnlyReason Reason { get; }
    }
}

public enum ResourceMetadataRejectionReason {
    InvalidValues = 1,
    ResourceMissing,
    ResourceChanged,
    ProjectUnavailable,
    DatasetChanged,
    OwnerRetired,
    UnsupportedMetadata,
    GovernedResource,
    DuplicatePromotion,
    TransactionConflict,
    PersistenceUnavailable,
    Cancelled
}

public enum ResourceMetadataSecondaryDiagnostic { CleanupPending = 1, ActivityPending, CatalogPublicationPending, LoggingPending }

public abstract record ResourceMetadataSaveOutcome {
    private ResourceMetadataSaveOutcome() {
    }

    public sealed record Accepted : ResourceMetadataSaveOutcome {
        public Accepted(ResourceMetadataSnapshot snapshot, ResourceMetadataSecondaryDiagnostic? diagnostic = null) {
            ArgumentNullException.ThrowIfNull(snapshot);
            ResourceMetadataLimits.RequireDiagnostic(diagnostic);
            Snapshot = snapshot;
            Diagnostic = diagnostic;
        }

        public ResourceMetadataSnapshot Snapshot { get; }
        public ResourceMetadataSecondaryDiagnostic? Diagnostic { get; }
    }

    public sealed record Rejected : ResourceMetadataSaveOutcome {
        public Rejected(ResourceMetadataRejectionReason reason, string? message = null) {
            ResourceMetadataLimits.RequireRejection(reason);
            Reason = reason;
            Message = message;
        }

        public ResourceMetadataRejectionReason Reason { get; }
        public string? Message { get; }
    }

    public sealed record Unconfirmed : ResourceMetadataSaveOutcome {
        public Unconfirmed(ReplaceResourceMetadataCommand command) {
            ArgumentNullException.ThrowIfNull(command);
            Command = command;
        }

        public ReplaceResourceMetadataCommand Command { get; }
    }
}

public abstract record ResourceMetadataDeleteOutcome {
    private ResourceMetadataDeleteOutcome() {
    }

    public sealed record Accepted : ResourceMetadataDeleteOutcome {
        public Accepted(ResourceMetadataExpectation.Existing deleted, ResourceMetadataSecondaryDiagnostic? diagnostic = null) {
            ArgumentNullException.ThrowIfNull(deleted);
            ResourceMetadataLimits.RequireDiagnostic(diagnostic);
            Deleted = deleted;
            Diagnostic = diagnostic;
        }

        public ResourceMetadataExpectation.Existing Deleted { get; }
        public ResourceMetadataSecondaryDiagnostic? Diagnostic { get; }
    }

    public sealed record Rejected : ResourceMetadataDeleteOutcome {
        public Rejected(ResourceMetadataRejectionReason reason, string? message = null) {
            ResourceMetadataLimits.RequireRejection(reason);
            Reason = reason;
            Message = message;
        }

        public ResourceMetadataRejectionReason Reason { get; }
        public string? Message { get; }
    }

    public sealed record Unconfirmed : ResourceMetadataDeleteOutcome {
        public Unconfirmed(DeleteResourceMetadataCommand command) {
            ArgumentNullException.ThrowIfNull(command);
            Command = command;
        }

        public DeleteResourceMetadataCommand Command { get; }
    }
}

public static class ResourceMetadataLimits {
    public const int ScalarBytes = 8 * 1024;
    public const int DescriptionBytes = 64 * 1024;
    public const int ConfigJsonBytes = 256 * 1024;
    public const int LinkedSecretIdsJsonBytes = 4 * 1024;
    public const int ConfigurationEntries = 128;
    public const int ConfigurationKeyBytes = 256;
    public const int ConfigurationValueBytes = 64 * 1024;
    public const int RawAggregateBytes = 512 * 1024;
    public const int EncodedSnapshotBytes = 1024 * 1024;
    public const int SearchBytes = 128 * 1024;

    public static int RequireText(string value, int maximumBytes, string field) {
        ArgumentNullException.ThrowIfNull(value, field);
        if (value.Length > maximumBytes) {
            throw new ArgumentException($"Resource {field} exceeds its supported byte limit.", field);
        }
        var bytes = Encoding.UTF8.GetByteCount(value);
        if (bytes > maximumBytes) {
            throw new ArgumentException($"Resource {field} exceeds its supported byte limit.", field);
        }
        return bytes;
    }

    internal static long RawScalars(string name, string description, string connectorPluginKey,
        string configSchemaVersion, string locationOrIdentifier, string configJson, string? linkedSecretIdsJson = null) {
        var bytes = (long)RequireText(name, ScalarBytes, nameof(name)) +
            RequireText(description, DescriptionBytes, nameof(description)) +
            RequireText(connectorPluginKey, ScalarBytes, nameof(connectorPluginKey)) +
            RequireText(configSchemaVersion, ScalarBytes, nameof(configSchemaVersion)) +
            RequireText(locationOrIdentifier, ScalarBytes, nameof(locationOrIdentifier)) +
            RequireText(configJson, ConfigJsonBytes, nameof(configJson));
        if (linkedSecretIdsJson is not null) {
            bytes += RequireText(linkedSecretIdsJson, LinkedSecretIdsJsonBytes, nameof(linkedSecretIdsJson));
        }
        RequireAggregate(bytes);
        return bytes;
    }

    private static void RequireAggregate(long bytes) {
        if (bytes > RawAggregateBytes) {
            throw new ArgumentException("The combined Resource metadata exceeds its supported byte limit.");
        }
    }

    internal static void AddEntry(string key, string value, ref int count, ref long bytes) {
        if (++count > ConfigurationEntries) {
            throw new ArgumentException("The Resource configuration has too many entries.");
        }
        bytes = checked(bytes + RequireText(key, ConfigurationKeyBytes, nameof(key)) +
            RequireText(value, ConfigurationValueBytes, nameof(value)));
        RequireAggregate(bytes);
    }

    internal static void RequireRejection(ResourceMetadataRejectionReason reason) {
        if (!Enum.IsDefined(reason)) {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }
    }

    internal static void RequireDiagnostic(ResourceMetadataSecondaryDiagnostic? diagnostic) {
        if (diagnostic is { } value && !Enum.IsDefined(value)) {
            throw new ArgumentOutOfRangeException(nameof(diagnostic));
        }
    }
}
