using CanDoItAll.Modules.Resources;

namespace CanDoItAll.Modules.Factory.Pages;

public partial class PromptFactoryPage
{
    private const string CustomGovernanceGroupKey = "__custom";
    private const string ContextViewOverview = "overview";
    private const string ContextViewLibrary = "library";
    private const string ContextViewStarters = "starters";
    private const string GovernanceViewSelection = "selection";
    private const string GovernanceViewLibrary = "library";
    private const string GovernanceViewBlocks = "blocks";
    private const string GovernanceViewTemplates = "templates";
    private const string AssemblyViewInputs = "inputs";
    private const string AssemblyViewResources = "resources";
    private const string ReviewViewReadiness = "readiness";
    private const string ReviewViewPreview = "preview";
    private const string ReviewViewDelivery = "delivery";

    private string blockSearchText = string.Empty;
    private bool showSelectedBlocksOnly;
    private string resourceSearchText = string.Empty;
    private bool showSelectedResourcesOnly;
    private bool blockAdminExpanded;
    private bool templateAdminExpanded;
    private string contextWorkspaceView = ContextViewOverview;
    private string governanceWorkspaceView = GovernanceViewSelection;
    private string assemblyWorkspaceView = AssemblyViewResources;
    private string reviewWorkspaceView = ReviewViewPreview;
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
            2 => $"{VisibleSessionAttachments.Count} inputs | {editor.SelectedResourceIds.Count} resources",
            3 => $"{editor.Nodes.Count} steps | {editor.Warnings.Count} warnings",
            _ => string.Empty
        };

    private string ResolveWizardStepTooltip(int stepIndex)
        => $"{wizardSteps[stepIndex]}: {ResolveWizardStepDescription(stepIndex)} ({ResolveWizardStepMeta(stepIndex)})";

    private string ResolveActiveWorkflowBadge()
        => CurrentWizardStep switch
        {
            0 => $"{(editor.ProjectId.HasValue ? "Project ready" : "Project optional")} | {blueprints.Count} blueprints",
            1 => $"{editor.SelectedBlockIds.Count}/{blocks.Count} blocks",
            2 => $"{VisibleSessionAttachments.Count} inputs | {editor.SelectedResourceIds.Count} resources",
            3 => $"{editor.Nodes.Count} steps | {editor.Warnings.Count} warnings",
            _ => string.Empty
        };

    private string ResolveInspectorTitle()
        => selectedCanvasNodeIds.Count > 1
            ? "Batch selection"
            : selectedSetupNode is not null
                ? "Session setup node"
                : selectedBlueprintNode is not null
                    ? "Blueprint node"
                    : selectedFlowNode is not null
                        ? "Flow template node"
                        : selectedComponentNode is not null
                            ? "Prompt component"
                            : selectedComponentGroupNode is not null
                                ? "Component group"
                                : selectedAttachmentNode is not null
                                    ? "Prompt input"
                                    : selectedPromptNode is not null
                                        ? "Prompt step"
                                        : selectedBranchLabel is not null
                                            ? "Branch overview"
                                            : "Session root";

    private string ResolveInspectorCopy()
        => selectedCanvasNodeIds.Count > 1
            ? "Review the selected set here, then use the canvas for spatial edits. Page tabs handle wider stage work."
            : selectedSetupNode is not null
                ? "This inspector stays brief on purpose. Open the Setup tab for full editing and use this panel for status plus quick actions."
                : selectedBlueprintNode is not null
                    ? "Blueprint choices belong to the session frame. Use this panel for the selected node, and the Setup or Governance tabs for broader adjustments."
                    : selectedFlowNode is not null
                        ? "Flow choices shape the delivery path. Use the node actions here, and open Review later when the prompt is ready to assemble."
                        : selectedComponentNode is not null
                            ? "Component-specific actions stay in the inspector so the canvas remains the control surface for prompt composition."
                            : selectedComponentGroupNode is not null
                                ? "Group details are contextual to the selected cluster. Open Governance when you need the larger library and filtering workspace."
                                : selectedAttachmentNode is not null
                                    ? "Inputs stay lightweight in the inspector. Open Assembly for the full context-packing workspace."
                                    : selectedPromptNode is not null
                                        ? "Prompt steps are edited directly from the inspector because they are node-specific actions."
                                        : selectedBranchLabel is not null
                                            ? "Branch review is contextual here. Use Review for the full finishing workspace."
                                            : "Select a node to inspect it here. Use the page tabs for setup, governance, assembly, and review instead of a duplicate side workspace.";

    private string ResolveInspectorSelectionPill()
        => selectedCanvasNodeIds.Count > 1
            ? $"{selectedCanvasNodeIds.Count} selected"
            : selectedSetupNode is not null
                ? "Setup"
                : selectedBlueprintNode is not null
                    ? "Blueprint"
                    : selectedFlowNode is not null
                        ? "Flow"
                        : selectedComponentNode is not null
                            ? "Component"
                            : selectedComponentGroupNode is not null
                                ? "Group"
                                : selectedAttachmentNode is not null
                                    ? "Input"
                                    : selectedPromptNode is not null
                                        ? "Prompt step"
                                        : selectedBranchLabel is not null
                                            ? "Branch"
                                            : "Session";

    private string ResolveWizardStepHelpTip(int stepIndex)
        => stepIndex switch
        {
            0 => "Pick the project and defaults first so later tabs start from a real delivery frame.",
            1 => "Stay with the working set here; open deeper tabs only when you need library or authoring actions.",
            2 => "Use this stage to decide what context is worth carrying into the prompt before you build.",
            3 => "Treat review as the finish lane: check risks, read the prompt, then save or publish.",
            _ => "Move one decision layer at a time."
        };

    private string ResolveCanvasHeaderHelpBody()
        => "The canvas is the structural workspace for the prompt flow. Use it for graph shape, node order, branching, and prompt-step edits. Setup, governance, assembly, and review now live in their own page tabs instead of stacking under the canvas.";

    private string ResolveCanvasHeaderHelpTip()
        => "Tip: stay on the Canvas tab for node work. Open the other page tabs only when you need the full setup, governance, assembly, or review workspace.";

    private string ResolveSessionInspectorCopy()
        => CurrentWizardStep switch
        {
            1 => "The session root gives quick navigation and save actions. Open the Governance tab for the full component workspace.",
            2 => "Use the session root for quick status, then open Assembly for the full input and resource workspace.",
            3 => "Use the session root for quick status, then open Review for the full prompt preview and delivery workspace.",
            _ => "The session root gives quick navigation while the full setup editing now lives in the Setup tab."
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

    private bool IsGovernanceView(string value)
        => string.Equals(governanceWorkspaceView, value, StringComparison.OrdinalIgnoreCase);

    private void SetGovernanceWorkspaceView(string value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        governanceWorkspaceView = normalized switch
        {
            GovernanceViewLibrary => GovernanceViewLibrary,
            GovernanceViewBlocks => GovernanceViewBlocks,
            GovernanceViewTemplates => GovernanceViewTemplates,
            _ => GovernanceViewSelection
        };
    }

    private string ResolveGovernanceWorkspaceSummary()
        => governanceWorkspaceView switch
        {
            GovernanceViewLibrary => "Browse grouped components in one bounded explorer instead of stretching the page with every checklist at once.",
            GovernanceViewBlocks => "Block authoring stays behind an explicit tab so normal governance work stays short and focused.",
            GovernanceViewTemplates => "Template curation and editing stay together in one tab instead of adding another stack of cards to the page.",
            _ => "Keep the current selection, filters, and template signal in one place while you curate the active governance set."
        };

    private bool IsAssemblyView(string value)
        => string.Equals(assemblyWorkspaceView, value, StringComparison.OrdinalIgnoreCase);

    private void SetAssemblyWorkspaceView(string value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        assemblyWorkspaceView = normalized switch
        {
            AssemblyViewInputs => AssemblyViewInputs,
            _ => AssemblyViewResources
        };
    }

    private string ResolveAssemblyWorkspaceSummary()
        => assemblyWorkspaceView switch
        {
            AssemblyViewInputs => "Inspect the prompt-session attachments already added from the canvas without hunting through the graph.",
            _ => "Search, filter, and confirm project resources in one bounded panel while keeping the selected working set visible."
        };

    private bool IsReviewView(string value)
        => string.Equals(reviewWorkspaceView, value, StringComparison.OrdinalIgnoreCase);

    private void SetReviewWorkspaceView(string value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        reviewWorkspaceView = normalized switch
        {
            ReviewViewReadiness => ReviewViewReadiness,
            ReviewViewDelivery => ReviewViewDelivery,
            _ => ReviewViewPreview
        };
    }

    private string ResolveReviewWorkspaceSummary()
        => reviewWorkspaceView switch
        {
            ReviewViewReadiness => "Surface warnings and readiness first so the finish lane starts with risks, not another long page scan.",
            ReviewViewDelivery => "Keep the shipping context visible without displacing the generated prompt preview.",
            _ => "Inspect and refine the generated prompt in the main review tab while delivery actions stay close by."
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


