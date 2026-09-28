using System.ComponentModel;
namespace CanDoItAll.Processes.Projections;

[Description("Classification of catalog item kind. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessTemplateCatalogItemKind
{
    Process,
    Role,
    Artifact
}

[Description("Classification of catalog category kind. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessTemplateCatalogCategoryKind
{
    All,
    Processes,
    Roles,
    Artifacts
}

[Description("Classification of catalog preview tab kind. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessTemplateCatalogPreviewTabKind
{
    Overview,
    Markdown,
    Diagram,
    Json,
    Structure
}

[Description("Classification of import command kind. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessTemplateImportCommandKind
{
    ImportProcess,
    ImportRole,
    ImportArtifact
}

[Description("Classification of import command status. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessTemplateImportCommandStatus
{
    Accepted,
    Rejected
}

[Description("Classification of structure node kind. Numeric JSON values are listed by the OpenAPI schema.")]
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

[Description("Opaque catalog item key represented by its value member. Preserve the value; do not derive it from display text.")]
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

    [Description("Opaque identity or authoring-version text carried by this wrapper; preserve exactly.")]
    public string Value { get; }

    public override string ToString() => Value;
}

[Description("Opaque version of the catalog projection. This is an authoring concurrency token, not an authentication credential.")]
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

    [Description("Opaque identity or authoring-version text carried by this wrapper; preserve exactly.")]
    public string Value { get; }

    public override string ToString() => Value;
}

[Description("Search, category, selection and preview controls used to create this catalog projection.")]
public sealed record ProcessTemplateCatalogQueryProjection(
    [property: Description("Search text used for the returned template catalog projection.")]
    string? SearchText,
    [property: Description("Selected template catalog category filter.")]
    ProcessTemplateCatalogCategoryKind Category,
    [property: Description("Opaque identity of the selected template, when present.")]
    ProcessTemplateCatalogItemKey? SelectedItemKey,
    [property: Description("Selected representation of the template preview.")]
    ProcessTemplateCatalogPreviewTabKind PreviewTab,
    [property: Description("Maximum number of entries requested for the projected template catalog.")]
    int Take);

[Description("Template category, description, result count and selection state.")]
public sealed record ProcessTemplateCatalogCategoryProjection(
    [property: Description("Classification of this projected authoring item.")]
    ProcessTemplateCatalogCategoryKind Kind,
    [property: Description("Human-readable label for this projected authoring item.")]
    string Label,
    [property: Description("Human-readable explanation of this branch outcome or template category.")]
    string Description,
    [property: Description("Number of template entries in this category.")]
    int Count,
    [property: Description("Whether this item is selected in the returned projection.")]
    bool IsSelected);

[Description("One display fact about a process template, represented as a label and text.")]
public sealed record ProcessTemplateCatalogFactProjection(
    [property: Description("Human-readable label for this projected authoring item.")]
    string Label,
    [property: Description("Display text associated with this template fact.")]
    string Value);

[Description("A process, role or artifact template with stable source keys and display facts.")]
public sealed record ProcessTemplateCatalogItemProjection(
    [property: Description("Opaque catalog identity of this item; do not derive it from its display name.")]
    ProcessTemplateCatalogItemKey Key,
    [property: Description("Classification of this projected authoring item.")]
    ProcessTemplateCatalogItemKind Kind,
    [property: Description("Human-readable title of this authoring item.")]
    string Title,
    [property: Description("Human-readable explanation of this item; display text is not an executable contract.")]
    string Summary,
    [property: Description("Stable definition key within the source template pack.")]
    string SourceDefinitionKey,
    [property: Description("Stable component key within the source template pack.")]
    string SourceComponentKey,
    [property: Description("Human-readable category label of this template.")]
    string CategoryLabel,
    [property: Description("Display facts describing the selected canvas or template item.")]
    IReadOnlyList<ProcessTemplateCatalogFactProjection> Facts,
    [property: Description("Whether this item is selected in the returned projection.")]
    bool IsSelected);

[Description("Node in the template structure outline, linked to its parent and nesting depth.")]
public sealed record ProcessTemplateStructureNodeProjection(
    [property: Description("Identity of the node within this projected canvas or structure.")]
    string NodeKey,
    [property: Description("Identity of the parent structure node, or null for a root entry.")]
    string? ParentNodeKey,
    [property: Description("Classification of this projected authoring item.")]
    ProcessTemplateStructureNodeKind Kind,
    [property: Description("Human-readable title of this authoring item.")]
    string Title,
    [property: Description("Human-readable explanation of this item; display text is not an executable contract.")]
    string Summary,
    [property: Description("Nesting depth in the template structure outline; root entries have depth zero.")]
    int Depth);

[Description("Template component related to the selected preview and whether it has been imported.")]
public sealed record ProcessTemplateRelatedComponentProjection(
    [property: Description("Opaque catalog identity of this item; do not derive it from its display name.")]
    ProcessTemplateCatalogItemKey Key,
    [property: Description("Classification of this projected authoring item.")]
    ProcessTemplateCatalogItemKind Kind,
    [property: Description("Human-readable title of this authoring item.")]
    string Title,
    [property: Description("Human-readable explanation of this item; display text is not an executable contract.")]
    string Summary,
    [property: Description("Stable definition key within the source template pack.")]
    string SourceDefinitionKey,
    [property: Description("Stable component key within the source template pack.")]
    string SourceComponentKey,
    [property: Description("Whether the related template component has already been imported into the current definition.")]
    bool IsImported);

[Description("Step available as a destination for a template component import.")]
public sealed record ProcessTemplateImportTargetStepProjection(
    [property: Description("Opaque identity of the definition step referenced by this item, when applicable.")]
    ProcessDefinitionStepKey StepKey,
    [property: Description("Human-readable title of this authoring item.")]
    string Title,
    [property: Description("Human-readable explanation of this item; display text is not an executable contract.")]
    string Summary,
    [property: Description("Whether this step is the suggested default import destination.")]
    bool IsDefaultTarget);

[Description("Read-only template preview with canonical source provenance and generated presentation forms.")]
public sealed record ProcessTemplateCatalogPreviewProjection(
    [property: Description("Opaque identity of the template catalog item.")]
    ProcessTemplateCatalogItemKey ItemKey,
    [property: Description("Classification of this projected authoring item.")]
    ProcessTemplateCatalogItemKind Kind,
    [property: Description("Human-readable title of this authoring item.")]
    string Title,
    [property: Description("Human-readable explanation of this item; display text is not an executable contract.")]
    string Summary,
    [property: Description("Template-pack-relative JSON source path; not a host filesystem address.")]
    string SourceJsonRelativePath,
    [property: Description("Content hash of canonical template JSON for provenance; not a credential fingerprint.")]
    string SourceJsonHash,
    [property: Description("Explanation that preview forms are generated from canonical source.")]
    string GeneratedProjectionNotice,
    [property: Description("Generated Markdown preview derived from canonical template content.")]
    string GeneratedMarkdown,
    [property: Description("Generated Mermaid diagram text derived from canonical template content.")]
    string GeneratedMermaid,
    [property: Description("Canonical template source as a JSON text value; does not execute the template.")]
    string CanonicalJson,
    [property: Description("Hierarchical outline of the canonical template content.")]
    IReadOnlyList<ProcessTemplateStructureNodeProjection> Structure,
    [property: Description("Role or artifact templates related to the previewed template.")]
    IReadOnlyList<ProcessTemplateRelatedComponentProjection> RelatedComponents);

[Description("Availability of a template import command; this read API does not execute it.")]
public sealed record ProcessTemplateImportCommandProjection(
    [property: Description("Classification of this projected authoring item.")]
    ProcessTemplateImportCommandKind Kind,
    [property: Description("User-facing command label.")]
    string Text,
    [property: Description("Presentation icon name for the command or action.")]
    string Icon,
    [property: Description("Whether the authoring application currently allows this command; this flag does not grant HTTP authority.")]
    bool IsEnabled,
    [property: Description("Human-readable reason a command is unavailable, or null when it is available.")]
    string? DisabledReason);

[Description("Provenance of a template component already imported into this definition.")]
public sealed record ProcessTemplateImportedComponentProjection(
    [property: Description("Opaque identity of the template catalog item.")]
    ProcessTemplateCatalogItemKey ItemKey,
    [property: Description("Classification of this projected authoring item.")]
    ProcessTemplateCatalogItemKind Kind,
    [property: Description("Human-readable title of this authoring item.")]
    string Title,
    [property: Description("Stable definition key within the source template pack.")]
    string SourceDefinitionKey,
    [property: Description("Stable component key within the source template pack.")]
    string SourceComponentKey,
    [property: Description("Content hash of canonical template JSON for provenance; not a credential fingerprint.")]
    string SourceJsonHash,
    [property: Description("Step receiving the imported artifact component, when applicable.")]
    ProcessDefinitionStepKey? TargetStepKey,
    [property: Description("UTC instant when this template component was imported.")]
    DateTimeOffset ImportedAtUtc);

[Description("Most recent template import result at the observed template catalog version.")]
public sealed record ProcessTemplateImportCommandReceipt(
    [property: Description("Unique identifier of the recorded authoring command receipt.")]
    Guid ReceiptId,
    [property: Description("Authoring command whose outcome this receipt records.")]
    ProcessTemplateImportCommandKind CommandKind,
    [property: Description("Current lifecycle or command outcome of this projection.")]
    ProcessTemplateImportCommandStatus Status,
    [property: Description("Opaque version of this authoring projection; it is not an authentication token.")]
    ProcessTemplateCatalogVersionToken VersionToken,
    [property: Description("UTC instant when this command result was observed.")]
    DateTimeOffset ObservedAtUtc,
    [property: Description("Human-readable explanation of this item; display text is not an executable contract.")]
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

[Description("Template catalog for the selected definition, including previews and existing imported components.")]
public sealed record ProcessTemplateCatalogProjection(
    [property: Description("Opaque key of the definition receiving the projected template catalog.")]
    ProcessDefinitionCatalogItemKey TargetDefinitionKey,
    [property: Description("Opaque version of this authoring projection; it is not an authentication token.")]
    ProcessTemplateCatalogVersionToken VersionToken,
    [property: Description("Search and selection criteria used for this catalog projection.")]
    ProcessTemplateCatalogQueryProjection Query,
    [property: Description("Human-readable explanation of this item; display text is not an executable contract.")]
    string Summary,
    [property: Description("Version declared by the source template pack.")]
    string PackVersion,
    [property: Description("Human-readable explanation of canonical template source provenance.")]
    string CanonicalSourceSummary,
    [property: Description("Available template categories with counts and selection state.")]
    IReadOnlyList<ProcessTemplateCatalogCategoryProjection> Categories,
    [property: Description("Template catalog entries matching the current projected query.")]
    IReadOnlyList<ProcessTemplateCatalogItemProjection> Items,
    [property: Description("Selected template catalog entry, or null when none is selected.")]
    ProcessTemplateCatalogItemProjection? SelectedItem,
    [property: Description("Preview of the selected template, or null when no preview is available.")]
    ProcessTemplateCatalogPreviewProjection? Preview,
    [property: Description("Definition steps available as template import destinations.")]
    IReadOnlyList<ProcessTemplateImportTargetStepProjection> ImportTargets,
    [property: Description("Commands available in the authoring application; these read endpoints do not execute commands.")]
    IReadOnlyList<ProcessTemplateImportCommandProjection> Commands,
    [property: Description("Template components already imported into the current definition.")]
    IReadOnlyList<ProcessTemplateImportedComponentProjection> ImportedComponents,
    [property: Description("Most recent template import receipt, or null when none is recorded.")]
    ProcessTemplateImportCommandReceipt? LastImportReceipt);
