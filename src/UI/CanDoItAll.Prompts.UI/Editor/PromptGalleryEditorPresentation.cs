using CanDoItAll.Modules.Prompts;

namespace CanDoItAll.Prompts.UI.Editor;

public enum PromptGalleryEditorPhase
{
    New,
    Loading,
    Ready,
    Failed
}

// Immutable seed for the editor draft. The surface resets its form only when this reference changes.
public sealed record PromptGalleryEditorSource(
    string Title,
    string Summary,
    PromptGalleryItemKind Kind,
    string Phase,
    string Content,
    IReadOnlyList<string> Tags,
    IReadOnlyList<PromptProviderModel> SupportedModels,
    IReadOnlyList<PromptGalleryConsumer> SupportedConsumers,
    PromptModelRecommendations Recommendations)
{
    public static PromptGalleryEditorSource CreateEmpty()
        => new(
            string.Empty,
            string.Empty,
            PromptGalleryItemKind.FullPrompt,
            string.Empty,
            string.Empty,
            [],
            [],
            [],
            new PromptModelRecommendations());

    public static PromptGalleryEditorSource FromDetails(PromptGalleryItemDetails item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new PromptGalleryEditorSource(
            item.Title,
            item.Summary,
            item.Kind,
            item.Phase,
            item.DraftContent,
            [.. item.Tags],
            [.. item.SupportedModels],
            [.. item.SupportedConsumers],
            item.Recommendations);
    }
}

// Snapshot captured when the user submits; later form edits never change it.
public sealed record PromptGalleryEditorSubmission(
    string Title,
    string Summary,
    PromptGalleryItemKind Kind,
    string Phase,
    string Content,
    IReadOnlyList<string> Tags,
    IReadOnlyList<PromptProviderModel> SupportedModels,
    IReadOnlyList<PromptGalleryConsumer> SupportedConsumers,
    PromptModelRecommendations Recommendations);

public sealed record PromptGalleryEditorPresentation(
    long Generation,
    Guid? Target,
    PromptGalleryEditorPhase Phase,
    PromptGalleryEditorSource? Source,
    bool IsArchived,
    IReadOnlyList<PromptGalleryVersionInfo> Versions,
    IReadOnlyList<PromptWarningSuppression> WarningSuppressions,
    bool IsBusy,
    string? FailureMessage,
    string? Warning)
{
    public static PromptGalleryEditorPresentation CreateNew(long generation)
        => new(
            generation,
            Target: null,
            PromptGalleryEditorPhase.New,
            PromptGalleryEditorSource.CreateEmpty(),
            IsArchived: false,
            Versions: [],
            WarningSuppressions: [],
            IsBusy: false,
            FailureMessage: null,
            Warning: null);

    public static PromptGalleryEditorPresentation CreateLoading(long generation, Guid target)
        => new(
            generation,
            target,
            PromptGalleryEditorPhase.Loading,
            Source: null,
            IsArchived: false,
            Versions: [],
            WarningSuppressions: [],
            IsBusy: false,
            FailureMessage: null,
            Warning: null);

    public bool IsPersisted => Target.HasValue && Phase == PromptGalleryEditorPhase.Ready;

    public bool ShowsForm => Phase is PromptGalleryEditorPhase.New or PromptGalleryEditorPhase.Ready && Source is not null;

    public string StatusLabel => IsPersisted
        ? IsArchived ? "Archived Gallery item" : "Saved Gallery item"
        : "New Gallery item";
}

public abstract record PromptGalleryEditorIntent
{
    private PromptGalleryEditorIntent()
    {
    }

    public sealed record SaveDraft(long Generation, PromptGalleryEditorSubmission Submission) : PromptGalleryEditorIntent;

    public sealed record CreateVersion(long Generation, PromptGalleryEditorSubmission Submission) : PromptGalleryEditorIntent;

    public sealed record ToggleArchive(long Generation) : PromptGalleryEditorIntent;

    public sealed record SetWarningSuppression(long Generation, PromptWarningSuppression Preference, bool Suppressed) : PromptGalleryEditorIntent;

    public sealed record Retry(long Generation) : PromptGalleryEditorIntent;

    public sealed record Cancel : PromptGalleryEditorIntent;
}
