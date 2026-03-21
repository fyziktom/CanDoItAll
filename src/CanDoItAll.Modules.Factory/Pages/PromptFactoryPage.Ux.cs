using CanDoItAll.Modules.Resources;

namespace CanDoItAll.Modules.Factory.Pages;

public partial class PromptFactoryPage
{
    private const string CustomGovernanceGroupKey = "__custom";
    private const string ContextViewOverview = "overview";
    private const string ContextViewLibrary = "library";
    private const string ContextViewStarters = "starters";

    private string blockSearchText = string.Empty;
    private bool showSelectedBlocksOnly;
    private string resourceSearchText = string.Empty;
    private bool showSelectedResourcesOnly;
    private bool blockAdminExpanded;
    private bool templateAdminExpanded;
    private string contextWorkspaceView = ContextViewOverview;
    private string catalogSearchText = string.Empty;
    private string starterSearchText = string.Empty;

    private int CurrentWizardStep => Math.Clamp(editor.WizardStepIndex, 0, wizardSteps.Length - 1);

    private bool IsContextStep => CurrentWizardStep == 0;

    private bool IsGovernanceStep => CurrentWizardStep == 1;

    private bool IsAssemblyStep => CurrentWizardStep == 2;

    private bool IsReviewStep => CurrentWizardStep == 3;

    private IReadOnlyList<PromptLibraryGroupSummary> OrderedLibraryGroups
        => libraryCatalog.Groups.OrderBy(item => item.Order).ToList();

    private IReadOnlyList<PromptLibraryGroupSummary> FilteredLibraryGroups => FilterLibraryGroups().ToList();

    private IReadOnlyList<PromptBlueprintSummary> FilteredBlueprintStarters => FilterBlueprintStarters().ToList();

    private IReadOnlyList<PromptFlowTemplateSummary> FilteredFlowStarters => FilterFlowStarters().ToList();

    private IReadOnlyList<PromptBlockSummary> SelectedGovernanceBlocks
        => blocks
            .Where(item => editor.SelectedBlockIds.Contains(item.Id))
            .OrderBy(item => ResolveComponentSectionOrder(item.GroupKey))
            .ThenBy(item => ResolveGroupOrder(item.GroupKey))
            .ThenBy(item => item.OrderIndex)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private IReadOnlyList<GovernanceGroupViewModel> GovernanceBlockGroups => BuildGovernanceBlockGroups();

    private IReadOnlyList<ResourceSummary> FilteredProjectResources => FilterProjectResources().ToList();

    private IReadOnlyList<ResourceSummary> SelectedProjectResources
        => projectResources
            .Where(item => editor.SelectedResourceIds.Contains(item.Id))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private string ResolveWizardStepDescription(int stepIndex)
        => stepIndex switch
        {
            0 => "Set the project, blueprint, flow, and provider without leaving the canvas.",
            1 => "Tune reusable blocks through grouped governance instead of one long checkbox dump.",
            2 => "Assemble resources and prompt inputs, then build from a compact working set.",
            3 => "Review warnings, inspect the generated prompt, and publish with confidence.",
            _ => "Advance through the prompt workflow one decision layer at a time."
        };

    private string ResolveWizardStepMeta(int stepIndex)
        => stepIndex switch
        {
            0 => $"{(editor.ProjectId.HasValue ? "Project linked" : "Project optional")} | {blueprints.Count} blueprints",
            1 => $"{editor.SelectedBlockIds.Count} selected | {blocks.Count} total blocks",
            2 => $"{editor.SessionAttachments.Count} inputs | {editor.SelectedResourceIds.Count} resources",
            3 => $"{editor.Nodes.Count} steps | {editor.Warnings.Count} warnings",
            _ => string.Empty
        };

    private bool IsContextView(string value)
        => string.Equals(contextWorkspaceView, value, StringComparison.OrdinalIgnoreCase);

    private void SetContextWorkspaceView(string value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        contextWorkspaceView = normalized switch
        {
            ContextViewLibrary => ContextViewLibrary,
            ContextViewStarters => ContextViewStarters,
            _ => ContextViewOverview
        };
    }

    private string ResolveContextWorkspaceSummary()
        => contextWorkspaceView switch
        {
            ContextViewLibrary => "Scan the imported prompt pack by logical group instead of absorbing every component at once.",
            ContextViewStarters => "Apply a blueprint or flow directly from the starter list, then continue into governance with defaults already loaded.",
            _ => "Start with the current frame, then open only the support view you need instead of stacking reference panels below the canvas."
        };

    private string ResolveSelectedProjectName()
        => projects.FirstOrDefault(item => item.Id == editor.ProjectId)?.Name ?? "No project selected";

    private string ResolveSelectedBlueprintName()
        => blueprints.FirstOrDefault(item => item.Id == editor.BlueprintId)?.Name ?? "No blueprint selected";

    private string ResolveSelectedFlowName()
        => templates.FirstOrDefault(item => item.Id == editor.FlowTemplateId)?.Name ?? "No flow selected";

    private string ResolveSelectedProviderName()
        => providers.FirstOrDefault(item => item.Id == editor.ProviderProfileId)?.Name ?? "No provider selected";

    private static string ResolveAttachmentDisplayTitle(PromptSessionAttachmentSummary attachment)
        => string.IsNullOrWhiteSpace(attachment.Title) ? "Untitled input" : attachment.Title;

    private static string ResolveAttachmentDisplaySubtitle(PromptSessionAttachmentSummary attachment)
    {
        if (!string.IsNullOrWhiteSpace(attachment.LinkUrl))
        {
            return attachment.LinkUrl;
        }

        if (!string.IsNullOrWhiteSpace(attachment.MediaOriginalFileName))
        {
            return attachment.MediaOriginalFileName;
        }

        if (!string.IsNullOrWhiteSpace(attachment.Subtitle))
        {
            return attachment.Subtitle;
        }

        if (!string.IsNullOrWhiteSpace(attachment.Notes))
        {
            return attachment.Notes;
        }

        return attachment.Kind;
    }

    private static string ResolveBlockListSummary(PromptBlockSummary block)
    {
        var details = new List<string> { block.BlockKind.ToString() };
        if (block.IsRecommendedByDefault)
        {
            details.Add("Recommended");
        }

        if (block.TemplateTokens.Count > 0)
        {
            details.Add($"{block.TemplateTokens.Count} token fields");
        }

        return string.Join(" | ", details);
    }

    private static string ResolveResourceListSummary(ResourceSummary resource)
        => $"{resource.ResourceKind} | {resource.ValidationStatus} | {resource.Sensitivity}";

    private bool ShouldExpandGovernanceGroup(GovernanceGroupViewModel group)
    {
        if (!string.IsNullOrWhiteSpace(blockSearchText))
        {
            return true;
        }

        return string.Equals(group.Key, GovernanceBlockGroups.FirstOrDefault()?.Key, StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveGovernanceGroupLabel(string? value)
    {
        var key = NormalizeGovernanceGroupKey(value);
        return string.Equals(key, CustomGovernanceGroupKey, StringComparison.Ordinal)
            ? "Custom blocks"
            : ResolveLibraryGroupLabel(key);
    }

    private string ResolveGovernanceGroupPurpose(string? value)
    {
        var key = NormalizeGovernanceGroupKey(value);
        if (string.Equals(key, CustomGovernanceGroupKey, StringComparison.Ordinal))
        {
            return "Blocks saved outside the imported prompt library pack.";
        }

        return OrderedLibraryGroups.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))?.Purpose
            ?? ResolveLibraryGroupSummary(key);
    }

    private IReadOnlyList<GovernanceGroupViewModel> BuildGovernanceBlockGroups()
    {
        var filteredBlocks = FilterGovernanceBlocks().ToList();
        if (filteredBlocks.Count == 0)
        {
            return [];
        }

        var groupedBlocks = filteredBlocks
            .GroupBy(item => NormalizeGovernanceGroupKey(item.GroupKey))
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var orderedKeys = OrderedLibraryGroups
            .Select(item => item.Key)
            .ToList();

        foreach (var extraKey in groupedBlocks.Keys
                     .Except(orderedKeys, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(key => ResolveGovernanceGroupLabel(key), StringComparer.OrdinalIgnoreCase))
        {
            orderedKeys.Add(extraKey);
        }

        return orderedKeys
            .Where(groupedBlocks.ContainsKey)
            .Select(key =>
            {
                var blocksInGroup = groupedBlocks[key];
                var selectedCount = blocksInGroup.Count(item => editor.SelectedBlockIds.Contains(item.Id));
                return new GovernanceGroupViewModel(
                    key,
                    ResolveGovernanceGroupLabel(key),
                    ResolveGovernanceGroupPurpose(key),
                    selectedCount,
                    blocksInGroup);
            })
            .ToList();
    }

    private IEnumerable<PromptBlockSummary> FilterGovernanceBlocks()
    {
        IEnumerable<PromptBlockSummary> query = blocks;
        if (showSelectedBlocksOnly)
        {
            query = query.Where(item => editor.SelectedBlockIds.Contains(item.Id));
        }

        var term = blockSearchText.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(item => MatchesBlockSearch(item, term));
        }

        return query
            .OrderBy(item => ResolveComponentSectionOrder(item.GroupKey))
            .ThenBy(item => ResolveGroupOrder(item.GroupKey))
            .ThenBy(item => item.OrderIndex)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static bool MatchesBlockSearch(PromptBlockSummary item, string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return true;
        }

        return Contains(item.Name)
               || Contains(item.Summary)
               || Contains(item.BlockKind.ToString())
               || item.Tags.Any(Contains)
               || item.TemplateTokens.Any(Contains);

        bool Contains(string? value)
            => !string.IsNullOrWhiteSpace(value) && value.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerable<PromptLibraryGroupSummary> FilterLibraryGroups()
    {
        var term = catalogSearchText.Trim();
        var query = OrderedLibraryGroups.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(item => MatchesLibraryGroupSearch(item, term));
        }

        return query;
    }

    private static bool MatchesLibraryGroupSearch(PromptLibraryGroupSummary group, string term)
        => Contains(group.Name, term)
           || Contains(group.Key, term)
           || Contains(group.Summary, term)
           || Contains(group.Purpose, term)
           || Contains(group.UiMode, term);

    private IEnumerable<PromptBlueprintSummary> FilterBlueprintStarters()
    {
        var term = starterSearchText.Trim();
        var query = libraryCatalog.Blueprints.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(item => MatchesBlueprintStarterSearch(item, term));
        }

        return query
            .OrderByDescending(item => item.Id == editor.BlueprintId)
            .ThenBy(item => item.OrderIndex)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase);
    }

    private IEnumerable<PromptFlowTemplateSummary> FilterFlowStarters()
    {
        var term = starterSearchText.Trim();
        var query = libraryCatalog.FlowTemplates.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(item => MatchesFlowStarterSearch(item, term));
        }

        return query
            .OrderByDescending(item => item.Id == editor.FlowTemplateId)
            .ThenBy(item => item.OrderIndex)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static bool MatchesBlueprintStarterSearch(PromptBlueprintSummary item, string term)
        => Contains(item.Name, term)
           || Contains(item.Key, term)
           || Contains(item.PromptType, term)
           || Contains(item.Summary, term);

    private static bool MatchesFlowStarterSearch(PromptFlowTemplateSummary item, string term)
        => Contains(item.Name, term)
           || Contains(item.Key, term)
           || item.AgentSequence.Any(agent =>
               Contains(agent.Phase, term)
               || Contains(agent.Goal, term)
               || Contains(agent.RoleComponentKey, term)
               || Contains(agent.BlueprintKey, term)
               || agent.BlockKeys.Any(blockKey => Contains(blockKey, term)));

    private IEnumerable<ResourceSummary> FilterProjectResources()
    {
        IEnumerable<ResourceSummary> query = projectResources;
        if (showSelectedResourcesOnly)
        {
            query = query.Where(item => editor.SelectedResourceIds.Contains(item.Id));
        }

        var term = resourceSearchText.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(item => MatchesResourceSearch(item, term));
        }

        return query.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static bool MatchesResourceSearch(ResourceSummary item, string term)
        => item.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
           || item.ResourceKind.ToString().Contains(term, StringComparison.OrdinalIgnoreCase)
           || item.ValidationStatus.ToString().Contains(term, StringComparison.OrdinalIgnoreCase)
           || item.Sensitivity.ToString().Contains(term, StringComparison.OrdinalIgnoreCase);

    private static bool Contains(string? value, string term)
        => !string.IsNullOrWhiteSpace(value) && value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeGovernanceGroupKey(string? value)
        => string.IsNullOrWhiteSpace(value) ? CustomGovernanceGroupKey : value.Trim();

    private sealed record GovernanceGroupViewModel(
        string Key,
        string Name,
        string Purpose,
        int SelectedCount,
        IReadOnlyList<PromptBlockSummary> Blocks);
}
