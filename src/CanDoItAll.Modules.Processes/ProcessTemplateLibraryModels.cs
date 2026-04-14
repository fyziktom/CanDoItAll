using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Modules.Processes;

public enum ProcessTemplateLibraryCategory
{
    Processes,
    Roles,
    Artifacts
}

public sealed record ProcessTemplateLibraryFact(
    string Label,
    string Value);

public sealed record ProcessTemplateLibraryDocument(
    string Id,
    string Title,
    string Content,
    string SourcePath);

public sealed record ProcessTemplateLibraryMermaidDiagram(
    string Id,
    string Title,
    string Definition,
    string SourcePath);

public sealed record ProcessTemplateLibraryLinkedResource(
    string ItemId,
    string Key,
    string Title,
    string Summary,
    string ScopeLabel,
    string SourceProcessKey,
    string SourceProcessTitle);

public sealed record ProcessTemplateLibraryListItem(
    string ItemId,
    ProcessTemplateLibraryCategory Category,
    string Key,
    string Title,
    string Summary,
    string Eyebrow,
    string ScopeLabel,
    string SourceProcessKey,
    string SourceProcessTitle,
    IReadOnlyList<ProcessTemplateLibraryFact> Facts);

public sealed record ProcessTemplateLibraryPreview(
    string ItemId,
    ProcessTemplateLibraryCategory Category,
    string Key,
    string Title,
    string Summary,
    string Eyebrow,
    string ScopeLabel,
    IReadOnlyList<ProcessTemplateLibraryFact> Facts,
    IReadOnlyList<TreeViewNode> StructureNodes,
    IReadOnlyList<ProcessTemplateLibraryDocument> MarkdownDocuments,
    IReadOnlyList<ProcessTemplateLibraryDocument> JsonDocuments,
    IReadOnlyList<ProcessTemplateLibraryMermaidDiagram> MermaidDiagrams,
    IReadOnlyList<ProcessTemplateLibraryLinkedResource> RelatedRoles,
    IReadOnlyList<ProcessTemplateLibraryLinkedResource> RelatedArtifacts);

public sealed record ProcessTemplateArtifactTargetOption(
    Guid StepId,
    string StepKey,
    string Title);
