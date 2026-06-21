namespace CanDoItAll.Processes.Projections;

public enum ProcessTemplateCatalogItemKind
{
    Process,
    Role,
    Artifact
}

public enum ProcessTemplateCatalogCategoryKind
{
    All,
    Processes,
    Roles,
    Artifacts
}

public enum ProcessTemplateCatalogPreviewTabKind
{
    Overview,
    Markdown,
    Diagram,
    Json,
    Structure
}

public enum ProcessTemplateImportCommandKind
{
    ImportProcess,
    ImportRole,
    ImportArtifact
}

public enum ProcessTemplateImportCommandStatus
{
    Accepted,
    Rejected
}

public enum ProcessTemplateStructureNodeKind
{
    Root,
    Section,
    Process,
    Step,
    Branch,
    Role,
    Artifact
}

public readonly record struct ProcessTemplateCatalogItemKey
{
    public ProcessTemplateCatalogItemKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Template catalog item key is required.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct ProcessTemplateCatalogVersionToken
{
    public ProcessTemplateCatalogVersionToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Template catalog version token is required.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record ProcessTemplateCatalogQueryProjection(
    string? SearchText,
    ProcessTemplateCatalogCategoryKind Category,
    ProcessTemplateCatalogItemKey? SelectedItemKey,
    ProcessTemplateCatalogPreviewTabKind PreviewTab,
    int Take);

public sealed record ProcessTemplateCatalogCategoryProjection(
    ProcessTemplateCatalogCategoryKind Kind,
    string Label,
    string Description,
    int Count,
    bool IsSelected);

public sealed record ProcessTemplateCatalogFactProjection(
    string Label,
    string Value);

public sealed record ProcessTemplateCatalogItemProjection(
    ProcessTemplateCatalogItemKey Key,
    ProcessTemplateCatalogItemKind Kind,
    string Title,
    string Summary,
    string SourceDefinitionKey,
    string SourceComponentKey,
    string CategoryLabel,
    IReadOnlyList<ProcessTemplateCatalogFactProjection> Facts,
    bool IsSelected);

public sealed record ProcessTemplateStructureNodeProjection(
    string NodeKey,
    string? ParentNodeKey,
    ProcessTemplateStructureNodeKind Kind,
    string Title,
    string Summary,
    int Depth);

public sealed record ProcessTemplateRelatedComponentProjection(
    ProcessTemplateCatalogItemKey Key,
    ProcessTemplateCatalogItemKind Kind,
    string Title,
    string Summary,
    string SourceDefinitionKey,
    string SourceComponentKey,
    bool IsImported);

public sealed record ProcessTemplateImportTargetStepProjection(
    ProcessDefinitionStepKey StepKey,
    string Title,
    string Summary,
    bool IsDefaultTarget);

public sealed record ProcessTemplateCatalogPreviewProjection(
    ProcessTemplateCatalogItemKey ItemKey,
    ProcessTemplateCatalogItemKind Kind,
    string Title,
    string Summary,
    string SourceJsonRelativePath,
    string SourceJsonHash,
    string GeneratedProjectionNotice,
    string GeneratedMarkdown,
    string GeneratedMermaid,
    string CanonicalJson,
    IReadOnlyList<ProcessTemplateStructureNodeProjection> Structure,
    IReadOnlyList<ProcessTemplateRelatedComponentProjection> RelatedComponents);

public sealed record ProcessTemplateImportCommandProjection(
    ProcessTemplateImportCommandKind Kind,
    string Text,
    string Icon,
    bool IsEnabled,
    string? DisabledReason);

public sealed record ProcessTemplateImportedComponentProjection(
    ProcessTemplateCatalogItemKey ItemKey,
    ProcessTemplateCatalogItemKind Kind,
    string Title,
    string SourceDefinitionKey,
    string SourceComponentKey,
    string SourceJsonHash,
    ProcessDefinitionStepKey? TargetStepKey,
    DateTimeOffset ImportedAtUtc);

public sealed record ProcessTemplateImportCommandReceipt(
    Guid ReceiptId,
    ProcessTemplateImportCommandKind CommandKind,
    ProcessTemplateImportCommandStatus Status,
    ProcessTemplateCatalogVersionToken VersionToken,
    DateTimeOffset ObservedAtUtc,
    string Summary);

public sealed record ProcessTemplateImportCommand(
    ProcessWorkspaceShellScope Scope,
    ProcessDefinitionCatalogItemKey TargetDefinitionKey,
    ProcessTemplateImportCommandKind CommandKind,
    ProcessTemplateCatalogItemKey ItemKey,
    ProcessTemplateCatalogVersionToken? ExpectedVersionToken,
    ProcessTemplateCatalogQueryProjection Query,
    ProcessDefinitionStepKey? TargetStepKey);

public sealed record ProcessTemplateImportCommandResult(
    ProcessTemplateImportCommandReceipt Receipt,
    ProcessTemplateCatalogProjection Projection);

public sealed record ProcessTemplateCatalogProjection(
    ProcessDefinitionCatalogItemKey TargetDefinitionKey,
    ProcessTemplateCatalogVersionToken VersionToken,
    ProcessTemplateCatalogQueryProjection Query,
    string Summary,
    string PackVersion,
    string CanonicalSourceSummary,
    IReadOnlyList<ProcessTemplateCatalogCategoryProjection> Categories,
    IReadOnlyList<ProcessTemplateCatalogItemProjection> Items,
    ProcessTemplateCatalogItemProjection? SelectedItem,
    ProcessTemplateCatalogPreviewProjection? Preview,
    IReadOnlyList<ProcessTemplateImportTargetStepProjection> ImportTargets,
    IReadOnlyList<ProcessTemplateImportCommandProjection> Commands,
    IReadOnlyList<ProcessTemplateImportedComponentProjection> ImportedComponents,
    ProcessTemplateImportCommandReceipt? LastImportReceipt);
