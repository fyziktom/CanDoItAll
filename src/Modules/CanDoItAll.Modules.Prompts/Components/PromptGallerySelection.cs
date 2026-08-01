namespace CanDoItAll.Modules.Prompts.Components;

public sealed record PromptGallerySelection(
    Guid ArtifactId,
    Guid? VersionId,
    int? VersionNumber,
    string Title,
    string Summary,
    PromptGalleryItemKind Kind,
    string Content,
    IReadOnlyList<string> Tags,
    IReadOnlyList<PromptProviderModel> SupportedModels,
    PromptModelRecommendations Recommendations);

public enum PromptCompatibilityWarningDecision
{
    Cancel,
    InsertAnyway,
    InsertAndSuppress
}
