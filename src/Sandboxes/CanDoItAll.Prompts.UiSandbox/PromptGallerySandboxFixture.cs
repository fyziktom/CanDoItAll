using CanDoItAll.Modules.Prompts;
using CanDoItAll.Prompts.UI.Compatibility;
using CanDoItAll.Prompts.UI.Editor;
using CanDoItAll.Prompts.UI.Gallery;

namespace CanDoItAll.Prompts.UiSandbox;

public enum PromptGallerySandboxScenario
{
    Normal,
    Loading,
    Empty,
    LongData,
    Error,
    FavoriteBusy,
    EditorNew,
    EditorEdit,
    EditorLoading,
    EditorMissing,
    EditorRejected,
    EditorBusy,
    EditorUnknownResult,
    Picker,
    Compatibility,
    CompatibilityBlocked
}

public enum PromptGallerySandboxLayout
{
    Matched,
    Narrow,
    Flexible
}

public sealed record PromptGallerySandboxContext(
    PromptGallerySandboxScenario Scenario = PromptGallerySandboxScenario.Normal,
    PromptGallerySandboxLayout Layout = PromptGallerySandboxLayout.Matched)
{
    public static PromptGallerySandboxContext Parse(string? scenario, string? layout)
        => new(ParseScenario(scenario), ParseLayout(layout));

    public static PromptGallerySandboxScenario ParseScenario(string? token)
        => Enum.GetValues<PromptGallerySandboxScenario>()
            .FirstOrDefault(scenario => string.Equals(Token(scenario), token?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static PromptGallerySandboxLayout ParseLayout(string? token)
        => token?.Trim().ToLowerInvariant() switch
        {
            "narrow" => PromptGallerySandboxLayout.Narrow,
            "flexible" => PromptGallerySandboxLayout.Flexible,
            _ => PromptGallerySandboxLayout.Matched
        };

    public static string Token(PromptGallerySandboxScenario scenario)
        => string.Concat(scenario.ToString().Select((character, index) =>
            char.IsUpper(character) && index > 0 ? $"-{char.ToLowerInvariant(character)}" : char.ToLowerInvariant(character).ToString()));

    public static string Token(PromptGallerySandboxLayout layout) => layout.ToString().ToLowerInvariant();

    public IReadOnlyDictionary<string, object?> ToQuery() => new Dictionary<string, object?>
    {
        ["scenario"] = Token(Scenario),
        ["layout"] = Token(Layout)
    };
}

// Deterministic local state for the real Prompt Gallery surfaces; no service, timer, or persistence is involved.
public sealed class PromptGallerySandboxFixture
{
    public static readonly Guid FirstItemId = Guid.Parse("41000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Epoch = new(2026, 1, 15, 8, 30, 0, TimeSpan.Zero);
    private static readonly string[] Providers = ["OpenAI", "Anthropic", "Ollama", "Azure OpenAI"];
    private static readonly string[] Models = ["gpt-5.4-mini", "claude-sonnet-5", "llama3.3:70b", "gpt-5.4"];
    private static readonly string[] Phases = ["design", "review", "delivery", "operations"];
    private static readonly string[] TagPool =
        ["architecture", "review", "workflow", "chat", "process", "summary", "long-form", "compliance", "onboarding", "qa"];

    private readonly LocalSearchState gallery = new(compact: false, pageSize: 25, provider: null, model: null, showActualChatModelFilter: false);
    private readonly LocalSearchState picker = new(compact: true, pageSize: 15, provider: "OpenAI", model: "gpt-5.4-mini", showActualChatModelFilter: true);
    private long editorGeneration;

    public PromptGallerySandboxScenario Scenario { get; private set; } = PromptGallerySandboxScenario.Normal;

    public PromptGallerySearchPresentation Search => gallery.Presentation;

    public PromptGallerySearchPresentation PickerSearch => picker.Presentation;

    public PromptGalleryEditorPresentation? Editor { get; private set; }

    public bool PickerOpen { get; private set; }

    public bool CompatibilityOpen { get; private set; }

    public PromptCompatibilityWarningPresentation? Compatibility { get; private set; }

    public string IntentLog { get; private set; } = "No intent yet.";

    public string EditorTitle => Editor?.Target.HasValue == true ? "Edit Gallery item" : "New Gallery item";

    public void SetScenario(PromptGallerySandboxScenario scenario)
    {
        Scenario = scenario;
        IntentLog = "No intent yet.";
        Editor = null;
        PickerOpen = false;
        CompatibilityOpen = false;
        Compatibility = null;
        var items = scenario switch
        {
            PromptGallerySandboxScenario.Empty => [],
            PromptGallerySandboxScenario.LongData => CreateItems(60, longText: true),
            _ => CreateItems(24, longText: false)
        };
        gallery.Reset(items);
        picker.Reset(items);
        switch (scenario)
        {
            case PromptGallerySandboxScenario.Loading:
                gallery.IsLoading = true;
                break;
            case PromptGallerySandboxScenario.Error:
                gallery.LoadError = "The prompt gallery search failed. Retry after checking the active data source.";
                break;
            case PromptGallerySandboxScenario.FavoriteBusy:
                gallery.FavoriteBusyItemId = FirstItemId;
                break;
            case PromptGallerySandboxScenario.EditorNew:
                Editor = PromptGalleryEditorPresentation.CreateNew(++editorGeneration);
                break;
            case PromptGallerySandboxScenario.EditorEdit:
                Editor = ReadyEditor(FirstItemId, versions: 2);
                break;
            case PromptGallerySandboxScenario.EditorLoading:
                Editor = PromptGalleryEditorPresentation.CreateLoading(++editorGeneration, FirstItemId);
                break;
            case PromptGallerySandboxScenario.EditorMissing:
                Editor = PromptGalleryEditorPresentation.CreateLoading(++editorGeneration, Guid.Parse("41000000-0000-0000-0000-00000000dead")) with
                {
                    Phase = PromptGalleryEditorPhase.Failed,
                    FailureMessage = "Prompt Gallery item '41000000-0000-0000-0000-00000000dead' was not found."
                };
                break;
            case PromptGallerySandboxScenario.EditorRejected:
                Editor = ReadyEditor(FirstItemId, versions: 1) with
                {
                    Warning = "The Gallery rejected the last draft: Title cannot exceed 200 characters. Correct the draft and save again."
                };
                break;
            case PromptGallerySandboxScenario.EditorBusy:
                Editor = ReadyEditor(FirstItemId, versions: 1) with { IsBusy = true };
                break;
            case PromptGallerySandboxScenario.EditorUnknownResult:
                Editor = PromptGalleryEditorPresentation.CreateNew(++editorGeneration) with
                {
                    Warning = "The last save did not complete with a known result. Check the Gallery before saving again."
                };
                break;
            case PromptGallerySandboxScenario.Picker:
                PickerOpen = true;
                break;
            case PromptGallerySandboxScenario.Compatibility:
                Compatibility = CreateCompatibility(blocked: false);
                CompatibilityOpen = true;
                break;
            case PromptGallerySandboxScenario.CompatibilityBlocked:
                Compatibility = CreateCompatibility(blocked: true);
                CompatibilityOpen = true;
                break;
        }

        gallery.Refresh();
        picker.Refresh();
    }

    public void ApplySearch(PromptGallerySearchIntent intent) => Apply(gallery, intent, "Gallery");

    public void ApplyPicker(PromptGallerySearchIntent intent) => Apply(picker, intent, "Picker");

    public void OpenNewEditor()
    {
        Editor = PromptGalleryEditorPresentation.CreateNew(++editorGeneration);
        IntentLog = "Gallery: New item";
    }

    public void CloseEditor()
    {
        Editor = null;
        IntentLog = "Editor: Closed";
    }

    public void ClosePicker()
    {
        PickerOpen = false;
        IntentLog = "Picker: Closed";
    }

    public void ApplyEditor(PromptGalleryEditorIntent intent)
    {
        if (Editor is not { } editor)
        {
            return;
        }

        switch (intent)
        {
            case PromptGalleryEditorIntent.SaveDraft save:
                var target = editor.Target ?? Guid.Parse("41000000-0000-0000-0000-0000000000aa");
                Editor = editor with
                {
                    Target = target,
                    Phase = PromptGalleryEditorPhase.Ready,
                    IsArchived = false,
                    Warning = null
                };
                IntentLog = $"Editor: Save draft '{save.Submission.Title.Trim()}' ({save.Submission.Tags.Count} tag(s), {save.Submission.SupportedModels.Count} model(s)) → {target:D}";
                break;
            case PromptGalleryEditorIntent.CreateVersion create:
                var versionTarget = editor.Target ?? Guid.Parse("41000000-0000-0000-0000-0000000000aa");
                var nextNumber = editor.Versions.Count + 1;
                Editor = editor with
                {
                    Target = versionTarget,
                    Phase = PromptGalleryEditorPhase.Ready,
                    IsArchived = false,
                    Warning = null,
                    Versions = [.. editor.Versions, new PromptGalleryVersionInfo(Guid.NewGuid(), nextNumber, "Ready for reuse", "Markdown", Epoch.AddDays(nextNumber))]
                };
                IntentLog = $"Editor: Create final version v{nextNumber} of '{create.Submission.Title.Trim()}'";
                break;
            case PromptGalleryEditorIntent.ToggleArchive:
                Editor = editor with { IsArchived = !editor.IsArchived };
                IntentLog = editor.IsArchived ? "Editor: Restore" : "Editor: Archive";
                break;
            case PromptGalleryEditorIntent.SetWarningSuppression suppression:
                Editor = editor with
                {
                    WarningSuppressions = suppression.Suppressed
                        ? [.. editor.WarningSuppressions.Append(suppression.Preference).Distinct()]
                        : [.. editor.WarningSuppressions.Where(item => item != suppression.Preference)]
                };
                IntentLog = $"Editor: {(suppression.Suppressed ? "Suppress" : "Show")} {suppression.Preference.IssueCode} for {suppression.Preference.Consumer}";
                break;
            case PromptGalleryEditorIntent.Retry:
                IntentLog = editor.Phase == PromptGalleryEditorPhase.Failed
                    ? $"Editor: Retry {editor.Target:D} (still missing in this scenario)"
                    : "Editor: Retry";
                break;
            case PromptGalleryEditorIntent.Cancel:
                CloseEditor();
                break;
        }
    }

    public void ApplyCompatibility(PromptCompatibilityWarningDecision decision)
    {
        CompatibilityOpen = false;
        IntentLog = $"Compatibility: {decision}";
    }

    private void Apply(LocalSearchState state, PromptGallerySearchIntent intent, string owner)
    {
        switch (intent)
        {
            case PromptGallerySearchIntent.ChangeFilters change:
                state.Filters = change.Filters;
                state.PageIndex = 0;
                IntentLog = $"{owner}: Filters changed ({(change.Debounce ? "debounced" : "immediate")})";
                break;
            case PromptGallerySearchIntent.ClearFilters:
                state.Filters = PromptGalleryFilterValues.Cleared;
                state.PageIndex = 0;
                IntentLog = $"{owner}: Clear filters";
                break;
            case PromptGallerySearchIntent.LoadPage load:
                state.PageIndex = load.PageIndex;
                IntentLog = $"{owner}: Page {load.PageIndex + 1}";
                break;
            case PromptGallerySearchIntent.Retry:
                state.LoadError = null;
                state.IsLoading = false;
                IntentLog = $"{owner}: Retry";
                break;
            case PromptGallerySearchIntent.ToggleFavorite favorite:
                state.ToggleFavorite(favorite.Item.Id);
                IntentLog = $"{owner}: {(favorite.Item.IsFavorite ? "Remove favorite" : "Mark favorite")} '{favorite.Item.Title}'";
                break;
            case PromptGallerySearchIntent.Select select:
                IntentLog = $"{owner}: Select '{select.Item.Title}' (v{select.Item.CurrentVersionNumber})";
                break;
            case PromptGallerySearchIntent.Edit edit:
                Editor = ReadyEditor(edit.ItemId, versions: state.Find(edit.ItemId)?.CurrentVersionNumber ?? 0);
                IntentLog = $"{owner}: Details {edit.ItemId:D}";
                break;
        }

        state.Refresh();
    }

    private PromptGalleryEditorPresentation ReadyEditor(Guid itemId, int versions)
    {
        var item = gallery.Find(itemId) ?? CreateItems(24, longText: false)[0];
        var source = new PromptGalleryEditorSource(
            item.Title,
            item.Summary,
            item.Kind,
            item.Phase,
            $"{item.ContentPreview}\n\nRespond with a numbered list. Keep each point under two sentences and cite the source section.",
            item.Tags,
            item.SupportedModels,
            [PromptGalleryConsumer.Chat, PromptGalleryConsumer.Workflow],
            item.Recommendations);
        return new PromptGalleryEditorPresentation(
            ++editorGeneration,
            itemId,
            PromptGalleryEditorPhase.Ready,
            source,
            item.IsArchived,
            Enumerable.Range(1, versions)
                .Select(number => new PromptGalleryVersionInfo(Guid.NewGuid(), number, number == 1 ? "Initial publication" : "Ready for reuse", "Markdown", Epoch.AddDays(number)))
                .ToArray(),
            [new PromptWarningSuppression(PromptGalleryConsumer.Chat, PromptCompatibilityIssueCode.ProviderModelNotSupported)],
            IsBusy: false,
            FailureMessage: null,
            Warning: null);
    }

    private static PromptCompatibilityWarningPresentation CreateCompatibility(bool blocked)
    {
        var issues = new List<PromptCompatibilityIssue>
        {
            new(
                PromptCompatibilityIssueCode.ProviderModelNotSupported,
                PromptCompatibilitySeverity.Warning,
                "The selected provider and model are not declared as supported by this Gallery item.",
                IsSuppressible: true,
                IsSuppressed: false),
            new(
                PromptCompatibilityIssueCode.ItemKindMismatch,
                PromptCompatibilitySeverity.Warning,
                "This consumer requires a FullPrompt item, but the selected item is Part.",
                IsSuppressible: true,
                IsSuppressed: false)
        };
        if (blocked)
        {
            issues.Insert(0, new PromptCompatibilityIssue(
                PromptCompatibilityIssueCode.ConsumerNotSupported,
                PromptCompatibilitySeverity.Error,
                "This Gallery item does not support the Chat consumer.",
                IsSuppressible: false,
                IsSuppressed: false));
        }

        return new PromptCompatibilityWarningPresentation(
            "Architecture review checklist with an intentionally long title that wraps inside the dialog card",
            "Reviews dependency direction, cyclic references and composition-root leakage before merge.",
            PromptGalleryItemKind.Part,
            blocked ? null : 3,
            new PromptCompatibilityResult(issues));
    }

    private static PromptGallerySearchItem[] CreateItems(int count, bool longText)
        => Enumerable.Range(0, count).Select(index =>
        {
            var id = index == 0 ? FirstItemId : Guid.Parse($"41000000-0000-0000-0000-{index + 1:000000000000}");
            var title = longText
                ? $"{index + 1:00} Extremely long reusable prompt title for the {Phases[index % Phases.Length]} phase that keeps going to exercise wrapping and clamping in the identity column of the gallery grid"
                : index switch
                {
                    0 => "Architecture review checklist",
                    1 => "Summarize a meeting transcript",
                    2 => "Extract action items as JSON",
                    3 => "Rewrite for a non-technical audience",
                    _ => $"{Phases[index % Phases.Length]} prompt {index + 1:00}"
                };
            var supportedModels = (index % 4) switch
            {
                0 => [new PromptProviderModel(Providers[0], Models[0], IsPreferred: true), new PromptProviderModel(Providers[1], Models[1])],
                1 => [new PromptProviderModel(Providers[index % Providers.Length], Models[index % Models.Length])],
                2 => Array.Empty<PromptProviderModel>(),
                _ => Enumerable.Range(0, 4).Select(model => new PromptProviderModel(Providers[model], Models[model], IsPreferred: model == 1)).ToArray()
            };
            var tagCount = longText ? TagPool.Length : 1 + (index % 5);
            return new PromptGallerySearchItem(
                id,
                title,
                $"Reusable {Phases[index % Phases.Length]} instruction {index + 1}.",
                longText
                    ? string.Join(' ', Enumerable.Repeat("Inspect dependency direction and report every violation with a suggested fix.", 6))
                    : "Inspect dependency direction, composition roots and public contracts; report violations.",
                index % 3 == 0 ? PromptGalleryItemKind.FullPrompt : PromptGalleryItemKind.Part,
                Phases[index % Phases.Length],
                index % 2 == 0 ? PromptArtifactStatus.Final : PromptArtifactStatus.Draft,
                IsArchived: index % 7 == 6,
                CollectionName: null,
                Tags: TagPool.Skip(index % 3).Take(tagCount).ToArray(),
                SupportedModels: supportedModels,
                Recommendations: index % 2 == 0 ? new PromptModelRecommendations(0.2, 1200, 0.9) : new PromptModelRecommendations(),
                CurrentVersionNumber: index % 2 == 0 ? 1 + (index % 3) : 0,
                UpdatedAtUtc: Epoch.AddHours(index),
                IsFavorite: index % 5 == 0);
        }).ToArray();

    private sealed class LocalSearchState
    {
        private readonly bool compact;
        private readonly int pageSize;
        private readonly string? provider;
        private readonly string? model;
        private readonly bool showActualChatModelFilter;
        private List<PromptGallerySearchItem> items = [];

        public LocalSearchState(bool compact, int pageSize, string? provider, string? model, bool showActualChatModelFilter)
        {
            this.compact = compact;
            this.pageSize = pageSize;
            this.provider = provider;
            this.model = model;
            this.showActualChatModelFilter = showActualChatModelFilter;
            Presentation = Build();
        }

        public PromptGalleryFilterValues Filters { get; set; } = PromptGalleryFilterValues.Initial;

        public int PageIndex { get; set; }

        public bool IsLoading { get; set; }

        public string? LoadError { get; set; }

        public Guid? FavoriteBusyItemId { get; set; }

        public PromptGallerySearchPresentation Presentation { get; private set; }

        public void Reset(IReadOnlyList<PromptGallerySearchItem> source)
        {
            items = [.. source];
            Filters = PromptGalleryFilterValues.Initial;
            PageIndex = 0;
            IsLoading = false;
            LoadError = null;
            FavoriteBusyItemId = null;
            Refresh();
        }

        public PromptGallerySearchItem? Find(Guid id) => items.FirstOrDefault(item => item.Id == id);

        public void ToggleFavorite(Guid id)
        {
            var index = items.FindIndex(item => item.Id == id);
            if (index >= 0)
            {
                items[index] = items[index] with { IsFavorite = !items[index].IsFavorite };
            }
        }

        public void Refresh()
        {
            Presentation = Build();
        }

        private PromptGallerySearchPresentation Build()
        {
            var shell = new PromptGallerySearchPresentation(
                Filters, [], 0, 0, 0, IsLoading, LoadError, [], provider, model, showActualChatModelFilter, FavoriteBusyItemId);
            var providerFilter = shell.UsesContextProviderModel
                ? string.IsNullOrWhiteSpace(provider) ? Filters.ProviderFilter : provider
                : compact ? string.Empty : Filters.ProviderFilter;
            var modelFilter = shell.UsesContextProviderModel
                ? string.IsNullOrWhiteSpace(model) ? Filters.ModelFilter : model
                : compact ? string.Empty : Filters.ModelFilter;
            var matches = items.Where(item =>
                    (Filters.IncludeArchived || !item.IsArchived) &&
                    (!Filters.FavoritesOnly || item.IsFavorite) &&
                    (Filters.Kind is null || item.Kind == Filters.Kind) &&
                    (Filters.Status is null || item.Status == Filters.Status) &&
                    Filters.Tags.All(tag => item.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)) &&
                    (string.IsNullOrWhiteSpace(Filters.Text) ||
                     item.Title.Contains(Filters.Text, StringComparison.OrdinalIgnoreCase) ||
                     item.ContentPreview.Contains(Filters.Text, StringComparison.OrdinalIgnoreCase) ||
                     item.Tags.Any(tag => tag.Contains(Filters.Text, StringComparison.OrdinalIgnoreCase))) &&
                    MatchesModel(item, providerFilter, modelFilter))
                .ToArray();
            var totalPages = matches.Length == 0 ? 0 : (int)Math.Ceiling(matches.Length / (double)pageSize);
            var pageIndex = totalPages == 0 ? 0 : Math.Clamp(PageIndex, 0, totalPages - 1);
            PageIndex = pageIndex;
            return shell with
            {
                Items = matches.Skip(pageIndex * pageSize).Take(pageSize).ToArray(),
                PageIndex = pageIndex,
                TotalPages = totalPages,
                TotalCount = matches.Length,
                AvailableTags = items.SelectMany(item => item.Tags).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase).ToArray()
            };
        }

        private static bool MatchesModel(PromptGallerySearchItem item, string providerFilter, string modelFilter)
        {
            if (string.IsNullOrWhiteSpace(providerFilter) && string.IsNullOrWhiteSpace(modelFilter))
            {
                return true;
            }

            return item.SupportedModels.Count == 0 || item.SupportedModels.Any(supported =>
                (string.IsNullOrWhiteSpace(providerFilter) || supported.Provider.Contains(providerFilter.Trim(), StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(modelFilter) || supported.Model.Contains(modelFilter.Trim(), StringComparison.OrdinalIgnoreCase)));
        }
    }
}
