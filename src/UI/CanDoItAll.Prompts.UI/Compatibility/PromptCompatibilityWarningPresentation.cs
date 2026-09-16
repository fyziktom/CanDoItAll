using CanDoItAll.Modules.Prompts;

namespace CanDoItAll.Prompts.UI.Compatibility;

public enum PromptCompatibilityWarningDecision
{
    Cancel,
    InsertAnyway,
    InsertAndSuppress
}

public sealed record PromptCompatibilityWarningPresentation(
    string Title,
    string Summary,
    PromptGalleryItemKind Kind,
    int? VersionNumber,
    PromptCompatibilityResult Compatibility)
{
    public IReadOnlyList<PromptCompatibilityIssue> VisibleIssues
        => Compatibility.Issues.Where(issue => !issue.IsSuppressed).ToArray();

    public bool CanUse => Compatibility.CanUse;

    // Blocking errors are never suppressible; suppression is offered only when every visible issue allows it.
    public bool CanSuppressAll
        => VisibleIssues.Count > 0 && VisibleIssues.All(issue => issue.IsSuppressible);
}
