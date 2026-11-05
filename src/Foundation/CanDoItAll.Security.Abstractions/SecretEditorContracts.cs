namespace CanDoItAll.Modules.Security;

public enum SecretKind {
    ApiKey,
    Password,
    Token,
    ConnectionString,
    SshKey,
    Generic
}

public sealed record SecretListItem(Guid Id, string Name, SecretKind Kind, string Scope, DateTimeOffset UpdatedAtUtc);

public readonly record struct SecretMetadataCondition(Guid SecretId, DateTimeOffset? UpdatedAtUtc);

public sealed class SecretEditorModel {
    public Guid? Id { get; set; }
    public DateTimeOffset? ExpectedUpdatedAtUtc { get; set; }

    public string Name { get; set; } = string.Empty;

    public SecretKind Kind { get; set; } = SecretKind.Generic;

    public string SecretValue { get; set; } = string.Empty;

    public string Scope { get; set; } = "workspace";

    public string? RotationNote { get; set; }

    public string MetadataJson { get; set; } = "{}";
}

public static class SecretEditorSnapshots {
    public static SecretEditorModel Copy(this SecretEditorModel value) => new() {
        Id = value.Id, ExpectedUpdatedAtUtc = value.ExpectedUpdatedAtUtc, Name = value.Name, Kind = value.Kind, SecretValue = value.SecretValue,
        Scope = value.Scope, RotationNote = value.RotationNote, MetadataJson = value.MetadataJson
    };
}

public enum SecretCommittedStage { Metadata, PayloadCleanup }
public sealed class SecretCommittedException(Guid secretId, bool deleted, SecretCommittedStage stage)
    : Exception("Secret metadata was committed; secondary work needs attention.") {
    public Guid SecretId { get; } = secretId;
    public bool Deleted { get; } = deleted;
    public SecretCommittedStage Stage { get; } = stage;
}

public sealed class SecretMutationUnknownException(Guid secretId)
    : Exception("Secret metadata outcome is unknown. The staged payload has been retained.") {
    public Guid SecretId { get; } = secretId;
}

public sealed class SecretMetadataConflictException()
    : InvalidOperationException("The secret changed after it was opened. Review the saved metadata before trying again.");
