using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Templates;

public sealed record ProcessTemplateComponentDocument(
    string SchemaVersion,
    string ContentVersion,
    string Key,
    ProcessTemplateComponentType ComponentType,
    string ContentHash,
    ProcessTemplateComponentReference? BaseRef,
    ProcessTemplateCompatibility Compatibility,
    JsonElement Content);

public sealed record ProcessTemplateComponentReference(
    TemplateComponentId ComponentId,
    string Key,
    string ContentVersion,
    string ResolvedContentHash);

public sealed record ProcessTemplateCompatibility(
    string MinRuntimeSchema,
    string MaxRuntimeSchema);

public sealed record ProcessTemplateLocalOverridePatch(
    string SchemaVersion,
    ProcessTemplateComponentReference BaseRef,
    IReadOnlyList<ProcessTemplatePatchOperation> Operations);

public sealed record ProcessTemplatePatchOperation(
    ProcessTemplatePatchOperationKind Kind,
    string JsonPointer,
    JsonElement? Value);

public sealed record ProcessTemplateConflictRecord(
    string SchemaVersion,
    ProcessTemplateComponentReference BaseRef,
    string NewBaseContentHash,
    IReadOnlyList<ProcessTemplateConflictEntry> Conflicts);

public sealed record ProcessTemplateConflictEntry(
    string JsonPointer,
    JsonElement? OldBaseValue,
    JsonElement? NewBaseValue,
    JsonElement? LocalValue,
    IReadOnlyList<ProcessTemplateConflictResolutionKind> AllowedResolutions);

public sealed record ProcessTemplateProjectionMetadata(
    string SchemaVersion,
    ProcessTemplateProjectionKind ProjectionKind,
    string SourceJsonHash,
    string GeneratorVersion,
    DateTimeOffset GeneratedAtUtc);

public enum ProcessTemplateComponentType
{
    Role,
    Artifact,
    Step,
    Prompt,
    Validation,
    Checklist,
    BranchFamily,
    ManagerProfile,
    MonitoringProfile,
    RecoveryPolicy
}

public enum ProcessTemplatePatchOperationKind
{
    Add,
    Replace,
    Remove
}

public enum ProcessTemplateConflictResolutionKind
{
    UseGlobal,
    UseLocal,
    EditManually
}

public enum ProcessTemplateProjectionKind
{
    Markdown,
    Mermaid,
    CompatibilityReport,
    ImportEnvelope
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, WriteIndented = false)]
[JsonSerializable(typeof(ProcessTemplateComponentDocument))]
[JsonSerializable(typeof(ProcessTemplateLocalOverridePatch))]
[JsonSerializable(typeof(ProcessTemplateConflictRecord))]
[JsonSerializable(typeof(ProcessTemplateProjectionMetadata))]
[JsonSerializable(typeof(ProcessTemplateCompatibilityReport))]
[JsonSerializable(typeof(ProcessTemplateMigrationDryRunReport))]
[JsonSerializable(typeof(ProcessTemplateSidecarDriftReport))]
[JsonSerializable(typeof(ProcessBranchMigrationDiagnosticReport))]
[JsonSerializable(typeof(ProcessTemplatePackManifest))]
[JsonSerializable(typeof(ProcessTemplateDefinitionDocument))]
[JsonSerializable(typeof(ProcessTemplateRoleResourceDocument))]
[JsonSerializable(typeof(ProcessTemplateRoleTemplateActionDocument[]))]
public sealed partial class ProcessTemplateJsonContext : JsonSerializerContext;
