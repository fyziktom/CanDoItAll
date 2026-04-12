using CanDoItAll.Components.BaseLib;
using System.Text;
using Markdig;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Processes;

public partial class ProcessTemplateLibraryDialog : ComponentBase
{
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private static readonly IReadOnlyList<SecondaryTabItem> CategoryTabs =
    [
        new("processes", "Processes", Description: "Import full process definitions into your process library."),
        new("roles", "Roles", Description: "Add role templates into the currently open definition."),
        new("artifacts", "Artifacts", Description: "Add artifact templates into a selected process step.")
    ];

    [Inject]
    private ProcessTemplateLibraryService TemplateLibraryService { get; set; } = default!;

    [Parameter]
    public bool IsOpen { get; set; }

    [Parameter]
    public string ScopeLabel { get; set; } = string.Empty;

    [Parameter]
    public IReadOnlyList<ProcessTemplateArtifactTargetOption> ArtifactTargets { get; set; } = [];

    [Parameter]
    public Guid? SelectedArtifactTargetStepId { get; set; }

    [Parameter]
    public EventCallback<Guid?> SelectedArtifactTargetStepIdChanged { get; set; }

    [Parameter]
    public EventCallback Close { get; set; }

    [Parameter]
    public EventCallback<string> AddProcessTemplate { get; set; }

    [Parameter]
    public EventCallback<string> AddRoleTemplate { get; set; }

    [Parameter]
    public EventCallback<string> AddArtifactTemplate { get; set; }

    private string selectedCategoryKey = "processes";
    private string selectedPreviewTabKey = "overview";
    private string searchText = string.Empty;
    private string? selectedItemId;
    private ProcessTemplateLibraryPreview? selectedPreview;
    private IReadOnlyList<ProcessTemplateLibraryListItem> processItems = [];
    private IReadOnlyList<ProcessTemplateLibraryListItem> roleItems = [];
    private IReadOnlyList<ProcessTemplateLibraryListItem> artifactItems = [];

    private IReadOnlyList<ProcessTemplateLibraryListItem> CurrentItems
        => FilterItems(GetItems(ParseCategory(selectedCategoryKey)), searchText);

    private IReadOnlyList<SecondaryTabItem> PreviewTabs
    {
        get
        {
            var tabs = new List<SecondaryTabItem>
            {
                new("overview", "Overview", Description: "Summary, facts, and related resources.")
            };

            if (selectedPreview?.MarkdownDocuments.Count > 0)
            {
                tabs.Add(new SecondaryTabItem("markdown", "Markdown", Description: "Rendered authoring notes and definition guidance."));
            }

            if (selectedPreview?.MermaidDiagrams.Count > 0)
            {
                tabs.Add(new SecondaryTabItem("diagrams", "Diagrams", Description: "Mermaid previews with pan and zoom."));
            }

            if (selectedPreview?.JsonDocuments.Count > 0)
            {
                tabs.Add(new SecondaryTabItem("json", "JSON", Description: "Raw JSON definitions with structured inspection."));
            }

            return tabs;
        }
    }

    private bool HasArtifactTarget => SelectedArtifactTargetStepId.HasValue && ArtifactTargets.Any(item => item.StepId == SelectedArtifactTargetStepId.Value);

    private bool CanAddCurrentSelection
        => selectedPreview is not null &&
           (selectedPreview.Category != ProcessTemplateLibraryCategory.Artifacts || HasArtifactTarget);

    private bool ShowArtifactTargetPicker
        => ArtifactTargets.Count > 0 &&
           selectedPreview is not null &&
           (selectedPreview.Category == ProcessTemplateLibraryCategory.Artifacts || selectedPreview.RelatedArtifacts.Count > 0);

    private string CurrentAddButtonText
        => selectedPreview?.Category switch
        {
            ProcessTemplateLibraryCategory.Processes => "Add to my processes",
            ProcessTemplateLibraryCategory.Roles => "Add to my roles",
            ProcessTemplateLibraryCategory.Artifacts => "Add to my artifacts",
            _ => "Add"
        };

    protected override void OnParametersSet()
    {
        if (!IsOpen)
        {
            return;
        }

        EnsureCatalogLoaded();
        EnsureSelection();
        EnsurePreviewTab();
    }

    private void EnsureCatalogLoaded()
    {
        if (processItems.Count == 0)
        {
            processItems = TemplateLibraryService.ListItems(ProcessTemplateLibraryCategory.Processes);
        }

        if (roleItems.Count == 0)
        {
            roleItems = TemplateLibraryService.ListItems(ProcessTemplateLibraryCategory.Roles);
        }

        if (artifactItems.Count == 0)
        {
            artifactItems = TemplateLibraryService.ListItems(ProcessTemplateLibraryCategory.Artifacts);
        }
    }

    private void EnsureSelection()
    {
        var items = CurrentItems;
        if (items.Count == 0)
        {
            selectedItemId = null;
            selectedPreview = null;
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedItemId) ||
            items.All(item => !string.Equals(item.ItemId, selectedItemId, StringComparison.Ordinal)))
        {
            selectedItemId = items[0].ItemId;
        }

        selectedPreview = TemplateLibraryService.GetPreview(ParseCategory(selectedCategoryKey), selectedItemId);
    }

    private void EnsurePreviewTab()
    {
        if (PreviewTabs.All(item => !string.Equals(item.Key, selectedPreviewTabKey, StringComparison.Ordinal)))
        {
            selectedPreviewTabKey = PreviewTabs[0].Key;
        }
    }

    private IReadOnlyList<ProcessTemplateLibraryListItem> GetItems(ProcessTemplateLibraryCategory category)
    {
        return category switch
        {
            ProcessTemplateLibraryCategory.Processes => processItems,
            ProcessTemplateLibraryCategory.Roles => roleItems,
            ProcessTemplateLibraryCategory.Artifacts => artifactItems,
            _ => []
        };
    }

    private static IReadOnlyList<ProcessTemplateLibraryListItem> FilterItems(
        IReadOnlyList<ProcessTemplateLibraryListItem> items,
        string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return items;
        }

        return items
            .Where(item =>
                item.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                item.Summary.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                item.ScopeLabel.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                item.Eyebrow.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                item.SourceProcessTitle.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                item.Facts.Any(fact =>
                    fact.Label.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    fact.Value.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static ProcessTemplateLibraryCategory ParseCategory(string key)
    {
        return key switch
        {
            "roles" => ProcessTemplateLibraryCategory.Roles,
            "artifacts" => ProcessTemplateLibraryCategory.Artifacts,
            _ => ProcessTemplateLibraryCategory.Processes
        };
    }

    private async Task HandleCategoryChangedAsync(string key)
    {
        selectedCategoryKey = key;
        selectedItemId = null;
        selectedPreviewTabKey = "overview";
        EnsureSelection();
        EnsurePreviewTab();
        await InvokeAsync(StateHasChanged);
    }

    private async Task HandlePreviewTabChangedAsync(string key)
    {
        selectedPreviewTabKey = key;
        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleSearchChangedAsync(string? value)
    {
        searchText = value?.Trim() ?? string.Empty;
        EnsureSelection();
        EnsurePreviewTab();
        await InvokeAsync(StateHasChanged);
    }

    private async Task SelectItemAsync(string itemId)
    {
        selectedItemId = itemId;
        selectedPreview = TemplateLibraryService.GetPreview(ParseCategory(selectedCategoryKey), itemId);
        EnsurePreviewTab();
        await InvokeAsync(StateHasChanged);
    }

    private Task HandleCloseAsync()
    {
        return Close.InvokeAsync();
    }

    private Task HandleArtifactTargetChangedAsync(Guid? stepId)
    {
        return SelectedArtifactTargetStepIdChanged.InvokeAsync(stepId);
    }

    private Task AddCurrentSelectionAsync()
    {
        if (selectedPreview is null)
        {
            return Task.CompletedTask;
        }

        return selectedPreview.Category switch
        {
            ProcessTemplateLibraryCategory.Processes => AddProcessTemplate.InvokeAsync(selectedPreview.ItemId),
            ProcessTemplateLibraryCategory.Roles => AddRoleTemplate.InvokeAsync(selectedPreview.ItemId),
            ProcessTemplateLibraryCategory.Artifacts => AddArtifactTemplate.InvokeAsync(selectedPreview.ItemId),
            _ => Task.CompletedTask
        };
    }

    private Task OpenRelatedRoleAsync(string itemId)
    {
        selectedCategoryKey = "roles";
        searchText = string.Empty;
        selectedItemId = itemId;
        selectedPreview = TemplateLibraryService.GetPreview(ProcessTemplateLibraryCategory.Roles, itemId);
        selectedPreviewTabKey = "overview";
        EnsurePreviewTab();
        return InvokeAsync(StateHasChanged);
    }

    private Task OpenRelatedArtifactAsync(string itemId)
    {
        selectedCategoryKey = "artifacts";
        searchText = string.Empty;
        selectedItemId = itemId;
        selectedPreview = TemplateLibraryService.GetPreview(ProcessTemplateLibraryCategory.Artifacts, itemId);
        selectedPreviewTabKey = "overview";
        EnsurePreviewTab();
        return InvokeAsync(StateHasChanged);
    }

    private static string BuildListMeta(ProcessTemplateLibraryListItem item)
    {
        var builder = new StringBuilder(item.ScopeLabel);
        foreach (var fact in item.Facts.Take(2))
        {
            if (builder.Length > 0)
            {
                builder.Append(" / ");
            }

            builder.Append(fact.Label);
            builder.Append(": ");
            builder.Append(fact.Value);
        }

        return builder.ToString();
    }

    private static MarkupString RenderMarkdown(string content)
    {
        return new MarkupString(Markdown.ToHtml(content ?? string.Empty, MarkdownPipeline));
    }

    private static string BuildItemTestId(ProcessTemplateLibraryListItem item)
    {
        return $"processes-template-library-item-{SanitizeForTestId(item.ItemId)}";
    }

    private static string BuildLinkedResourceTestId(string prefix, ProcessTemplateLibraryLinkedResource resource)
    {
        return $"{prefix}-{SanitizeForTestId(resource.ItemId)}";
    }

    private static string SanitizeForTestId(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character)
                ? char.ToLowerInvariant(character)
                : '-');
        }

        return builder.ToString();
    }

    private Task IgnoreTreeInteractionAsync(string _)
    {
        return Task.CompletedTask;
    }
}
