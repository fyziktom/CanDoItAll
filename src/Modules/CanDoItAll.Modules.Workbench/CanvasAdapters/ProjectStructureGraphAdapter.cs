using CanDoItAll.Components.CanvasLib;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench.CanvasAdapters;

public sealed class ProjectStructureGraphAdapter
{
    public CanvasWorkbenchSurface BuildSurface(
        ProjectStructureSurface surface,
        CanvasWorkbenchUiState uiState,
        CanvasWorkbenchChrome chrome,
        ProjectStructureActionCatalogAdapter actionCatalog,
        Func<ProjectStructureNode, bool>? canLaunchRuntime = null,
        Func<ProjectStructureNode, bool>? canOpenInFileExplorer = null,
        Func<ProjectStructureNode, bool>? canOpenInNewTab = null)
    {
        ConfigureChrome(surface, chrome);
        return new CanvasWorkbenchSurface
        {
            SurfaceId = $"project-structure:{surface.ProjectId:N}",
            Mode = "authoring",
            UiState = uiState,
            Chrome = chrome,
            Nodes = surface.Nodes
                .Select(node => MapCanvasNode(surface.Nodes, node, uiState.SelectedNodeIds, actionCatalog, canLaunchRuntime, canOpenInFileExplorer, canOpenInNewTab))
                .ToList(),
            Links = surface.Links.Select(link => new CanvasWorkbenchLink
            {
                SourceId = link.SourceId,
                TargetId = link.TargetId,
                Kind = link.Kind.ToString(),
                IsUserAuthored = link.IsUserAuthored
            }).ToList()
        };
    }

    public void ExpandSelectionAncestors(IReadOnlyList<ProjectStructureNode> nodes, CanvasWorkbenchUiState uiState, IReadOnlyList<string> selectedIds)
    {
        if (selectedIds.Count == 0)
        {
            return;
        }

        var collapsedIds = uiState.CollapsedNodeIds.ToHashSet(StringComparer.Ordinal);
        var nodesById = nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);

        foreach (var selectedId in selectedIds)
        {
            if (!nodesById.TryGetValue(selectedId, out var current))
            {
                continue;
            }

            while (!string.IsNullOrWhiteSpace(current.ParentId) &&
                   nodesById.TryGetValue(current.ParentId, out var parent))
            {
                collapsedIds.Remove(parent.Id);
                current = parent;
            }
        }

        uiState.CollapsedNodeIds = [.. collapsedIds];
    }

    public bool TryAddSelectionFrame(
        IReadOnlyList<ProjectStructureNode> nodes,
        CanvasWorkbenchUiState uiState,
        IReadOnlyList<string> selectedNodeIds,
        string label)
    {
        var normalizedSelection = selectedNodeIds
            .Where(id => nodes.Any(node => string.Equals(node.Id, id, StringComparison.Ordinal)))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (normalizedSelection.Count < 2)
        {
            return false;
        }

        var frames = uiState.GroupFrames
            .Where(frame => !FrameMatchesSelection(frame, normalizedSelection, nodes))
            .ToList();
        frames.Add(new CanvasWorkbenchGroupFrame
        {
            Id = $"frame-{Guid.NewGuid():N}",
            Label = label,
            Tone = "sky",
            AnchorNodeIds = normalizedSelection
        });

        uiState.GroupFrames = frames;
        return true;
    }

    public bool TryClearSelectionFrames(
        IReadOnlyList<ProjectStructureNode> nodes,
        CanvasWorkbenchUiState uiState,
        IReadOnlyList<string> selectedNodeIds)
    {
        if (selectedNodeIds.Count == 0)
        {
            return false;
        }

        var retainedFrames = uiState.GroupFrames
            .Where(frame => !FrameMatchesSelection(frame, selectedNodeIds, nodes))
            .ToList();
        if (retainedFrames.Count == uiState.GroupFrames.Count)
        {
            return false;
        }

        uiState.GroupFrames = retainedFrames;
        return true;
    }

    private static CanvasWorkbenchNode MapCanvasNode(
        IReadOnlyList<ProjectStructureNode> nodes,
        ProjectStructureNode node,
        IReadOnlyList<string> selectedNodeIds,
        ProjectStructureActionCatalogAdapter actionCatalog,
        Func<ProjectStructureNode, bool>? canLaunchRuntime,
        Func<ProjectStructureNode, bool>? canOpenInFileExplorer,
        Func<ProjectStructureNode, bool>? canOpenInNewTab)
    {
        var hasChildren = nodes.Any(candidate => string.Equals(candidate.ParentId, node.Id, StringComparison.Ordinal));
        var inlineText = string.IsNullOrWhiteSpace(node.Notes) ? node.Title : node.Notes;
        var isInlineTextNode = node.ObjectType == ProjectObjectType.Note && string.IsNullOrWhiteSpace(node.Subtitle);
        var progress = ResolveProgress(node);
        var leadPresentation = ProjectStructureNodeDescriptor.BuildLeadPresentation(node);

        return new CanvasWorkbenchNode
        {
            Id = node.Id,
            ParentId = node.ParentId,
            Family = ResolveFamily(node.ObjectType),
            Kind = ProjectStructureCanvasCatalog.ResolveNodeLabel(node),
            Icon = node.VisualProfile.Icon,
            Title = node.Title,
            Subtitle = node.Subtitle,
            LeadText = leadPresentation.LeadText,
            CompactPath = MapCompactPath(leadPresentation.CompactPath),
            Status = node.Status,
            BranchLabel = node.ObjectType switch
            {
                ProjectObjectType.Phase or ProjectObjectType.PromptSession => "Branch",
                ProjectObjectType.ProcessDefinition => "Runs",
                _ => string.Empty
            },
            AccentColor = node.VisualProfile.AccentColor,
            PaletteKey = ResolvePalette(node),
            DurationLabel = node.Badges.Contains("Scheduled", StringComparer.Ordinal) ? "Scheduled" : string.Empty,
            StatusPill = node.Status,
            ProgressMode = progress.Mode,
            ProgressPercent = progress.Percent,
            MarkerIcon = node.MarkerIcon,
            MarkerTone = node.MarkerTone,
            MarkerLabel = node.MarkerLabel,
            Markers = node.Markers
                .Select(marker => new CanvasWorkbenchMarker
                {
                    Icon = marker.Icon,
                    Tone = marker.Tone,
                    Label = marker.Label
                })
                .ToList(),
            Priority = node.Priority,
            IsRequired = node.ObjectType is ProjectObjectType.ProjectRoot or ProjectObjectType.Phase or ProjectObjectType.PromptSession or ProjectObjectType.ProcessDefinition,
            IsCollapsible = hasChildren,
            IsReadOnly = node.ProjectRole == ProjectStructureProjectRole.AdditionalParentProject,
            IsPreviewOnly = node.ProjectRole == ProjectStructureProjectRole.AdditionalParentProject,
            IsInlineTextNode = isInlineTextNode,
            InlineText = inlineText,
            InlineTextPlaceholder = "Write note",
            MediaKind = string.Empty,
            MediaPreviewUrl = string.Empty,
            MediaPreviewAlt = node.Title,
            MediaContentType = node.MediaContentType,
            MediaFileName = node.MediaOriginalFileName,
            X = node.X,
            Y = node.Y,
            Chips = BuildHeaderChips(node),
            FooterChips = BuildFooterChips(node),
            Annotations = ProjectStructureNodeAnnotationBuilder.Build(node).ToList(),
            ContextActions = actionCatalog.BuildNodeContextActions(
                node,
                canLaunchRuntime?.Invoke(node) == true,
                canOpenInFileExplorer?.Invoke(node) == true,
                canOpenInNewTab?.Invoke(node) == true,
                CountSelectedContextTargets(node.Id, selectedNodeIds)).ToList()
        };
    }

    private static int CountSelectedContextTargets(string nodeId, IReadOnlyList<string> selectedNodeIds)
    {
        if (selectedNodeIds.Count <= 1 ||
            !selectedNodeIds.Contains(nodeId, StringComparer.Ordinal))
        {
            return 0;
        }

        return selectedNodeIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    private static void ConfigureChrome(ProjectStructureSurface surface, CanvasWorkbenchChrome chrome)
    {
        chrome.HintText = string.IsNullOrWhiteSpace(chrome.HintText)
            ? "Select a project object, drag to reorganize, use the minimap for navigation, and toggle diagnostics when alignment or routing feels off."
            : chrome.HintText;
        chrome.EmptyStateKicker = string.IsNullOrWhiteSpace(chrome.EmptyStateKicker) ? "Project structure" : chrome.EmptyStateKicker;
        chrome.EmptyStateTitle = string.IsNullOrWhiteSpace(chrome.EmptyStateTitle) ? $"Map {surface.ProjectName}" : chrome.EmptyStateTitle;
        chrome.EmptyStateDescription = string.IsNullOrWhiteSpace(chrome.EmptyStateDescription)
            ? "Use quick create to add phases, prompts, references, and validation artifacts to this project."
            : chrome.EmptyStateDescription;
        chrome.Diagnostics ??= new CanvasWorkbenchDiagnosticsOptions();
        chrome.Diagnostics.IsEnabled = true;
        chrome.Minimap ??= new CanvasWorkbenchMinimapOptions();
        chrome.Minimap.IsEnabled = true;
        chrome.Minimap.Title = "Project overview";
        chrome.Clipboard ??= new CanvasWorkbenchClipboardOptions();
        chrome.Clipboard.IsEnabled = true;
    }

    private static string ResolveFamily(ProjectObjectType objectType) => objectType switch
    {
        ProjectObjectType.ProjectRoot => "root",
        ProjectObjectType.Phase or ProjectObjectType.PromptSession or ProjectObjectType.PromptFlow or ProjectObjectType.ProjectBlock or ProjectObjectType.ProcessDefinition => "group",
        ProjectObjectType.ProcessRun or ProjectObjectType.ValidationRun or ProjectObjectType.TestPlan or ProjectObjectType.Decision or ProjectObjectType.SecretReference => "special",
        _ => "item"
    };

    private static string ResolvePalette(ProjectStructureNode node)
        => node.ProjectRole switch
        {
            ProjectStructureProjectRole.Subproject => ProjectObjectPaletteKeys.Info,
            ProjectStructureProjectRole.ParentProject => ProjectObjectPaletteKeys.Neutral,
            ProjectStructureProjectRole.AdditionalParentProject => ProjectObjectPaletteKeys.Neutral,
            _ => string.IsNullOrWhiteSpace(node.VisualProfile.PaletteKey)
                ? ProjectObjectPaletteKeys.Neutral
                : node.VisualProfile.PaletteKey
        };

    private static string BuildLeadText(ProjectStructureNode node)
        => ProjectStructureNodeDescriptor.BuildLeadText(node);

    private static CanvasWorkbenchCompactPath? MapCompactPath(ProjectStructureCompactPathPresentation? compactPath)
        => compactPath is null
            ? null
            : new CanvasWorkbenchCompactPath
            {
                Label = compactPath.Label,
                DisplayText = compactPath.DisplayText,
                FullPath = compactPath.FullPath,
                PromotedText = compactPath.PromotedText
            };

    private static List<CanvasWorkbenchChip> BuildHeaderChips(ProjectStructureNode node)
        => node.Badges.Select(badge => new CanvasWorkbenchChip { Text = badge, Tone = "accent" }).ToList();

    private static List<CanvasWorkbenchChip> BuildFooterChips(ProjectStructureNode node)
    {
        var chips = new List<CanvasWorkbenchChip>();
        if (!string.IsNullOrWhiteSpace(node.Route))
        {
            chips.Add(new CanvasWorkbenchChip { Text = "Routed", Tone = "neutral" });
        }

        if (node.ArtifactId.HasValue)
        {
            chips.Add(new CanvasWorkbenchChip { Text = "Artifact", Tone = "success" });
        }

        if (!string.IsNullOrWhiteSpace(node.MediaOriginalFileName))
        {
            chips.Add(new CanvasWorkbenchChip { Text = "Uploaded", Tone = "accent" });
        }

        return chips;
    }

    private static (string Mode, int Percent) ResolveProgress(ProjectStructureNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.ProgressMode) && node.ProgressPercent >= 0)
        {
            var normalizedMode = node.ProgressMode.Trim().ToLowerInvariant();
            var normalizedPercent = Math.Clamp(node.ProgressPercent, 0, 100);
            return normalizedMode switch
            {
                "complete" => ("complete", 100),
                "started" => ("started", 0),
                "na" => ("na", 0),
                _ => ("progress", normalizedPercent)
            };
        }

        var status = node.Status?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(status) ||
            status.Contains("n/a", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("not applicable", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("skip", StringComparison.OrdinalIgnoreCase))
        {
            return ("na", 0);
        }

        if (status.Contains("done", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("complete", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("approved", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("used", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("ready", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("final", StringComparison.OrdinalIgnoreCase))
        {
            return ("complete", 100);
        }

        if (status.Contains("review", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("validation", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("testing", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("qa", StringComparison.OrdinalIgnoreCase))
        {
            return ("progress", 78);
        }

        if (status.Contains("active", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("in progress", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("running", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("blocked", StringComparison.OrdinalIgnoreCase))
        {
            return ("progress", 62);
        }

        if (status.Contains("planned", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("draft", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("pending", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("queued", StringComparison.OrdinalIgnoreCase))
        {
            return ("progress", 28);
        }

        return ("progress", 48);
    }

    private static bool FrameMatchesSelection(CanvasWorkbenchGroupFrame frame, IReadOnlyList<string> selectedIds, IReadOnlyList<ProjectStructureNode> nodes)
    {
        if (selectedIds.Count == 0 || frame.AnchorNodeIds.Count == 0)
        {
            return false;
        }

        var frameMembers = ExpandFrameMembers(frame.AnchorNodeIds, nodes);
        return selectedIds.All(frameMembers.Contains);
    }

    private static HashSet<string> ExpandFrameMembers(IReadOnlyList<string> anchorNodeIds, IReadOnlyList<ProjectStructureNode> nodes)
    {
        var childrenByParent = nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.ParentId))
            .GroupBy(node => node.ParentId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(node => node.Id).ToList(), StringComparer.Ordinal);
        var expanded = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>(anchorNodeIds.Where(id => !string.IsNullOrWhiteSpace(id)));

        while (queue.Count > 0)
        {
            var nodeId = queue.Dequeue();
            if (!expanded.Add(nodeId))
            {
                continue;
            }

            if (!childrenByParent.TryGetValue(nodeId, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                queue.Enqueue(child);
            }
        }

        return expanded;
    }
}


