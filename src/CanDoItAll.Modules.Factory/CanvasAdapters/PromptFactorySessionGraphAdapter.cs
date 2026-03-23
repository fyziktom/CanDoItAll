using System.IO;
using CanDoItAll.ComponentKit.Canvas;

namespace CanDoItAll.Modules.Factory.CanvasAdapters;

public sealed class PromptFactorySessionGraphAdapter
{
    private const string SessionRootCanvasNodeId = "session-root";
    private const string SetupCanvasNodeId = "selection:setup";
    private const string BlueprintCanvasNodeId = "selection:blueprint";
    private const string FlowCanvasNodeId = "selection:flow";
    private const string ComponentsRootCanvasNodeId = "selection:components";
    private const string InputsRootCanvasNodeId = "selection:inputs";
    private const string ComponentSectionCanvasNodePrefix = "selection:component-section:";
    private const string ComponentGroupCanvasNodePrefix = "selection:component-group:";
    private const string ComponentCanvasNodePrefix = "selection:component:";
    private const string InputCanvasNodePrefix = "selection:input:";

    public CanvasWorkbenchUiState BuildUiState(
        string canvasUiStateJson,
        IReadOnlyList<string> selectedCanvasNodeIds,
        string activeInspectorTab)
    {
        var uiState = CanvasWorkbenchUiState.Parse(canvasUiStateJson);
        uiState.SelectedNodeIds = [.. selectedCanvasNodeIds];
        uiState.ActiveInspectorTab = activeInspectorTab;
        return uiState;
    }

    public CanvasWorkbenchSurface BuildSurface(PromptFactorySessionGraphRequest request)
    {
        var selectionGraph = BuildSelectionGraph(request);
        var chrome = new CanvasWorkbenchChrome
        {
            HintText = "Click to inspect a step, Alt-drag to multi-select, drag to move, and use the same shared canvas vocabulary as project structure.",
            FocusActionLabel = "Focus session",
            QuickCreateActions = PromptFactoryCanvasCatalog.BuildSessionContextActions(request.LibraryCatalog).ToList()
        };
        ConfigureChrome(chrome);
        return new CanvasWorkbenchSurface
        {
            SurfaceId = request.Editor.SessionId.HasValue
                ? $"prompt-session:{request.Editor.SessionId.Value:N}"
                : "prompt-session:draft",
            Mode = "authoring",
            UiState = request.UiState,
            Chrome = chrome,
            Nodes = BuildCanvasNodes(request, selectionGraph),
            Links = BuildCanvasLinks(request, selectionGraph)
        };
    }

    public (List<CanvasWorkbenchNode> Nodes, List<CanvasWorkbenchLink> Links) BuildSelectionGraph(PromptFactorySessionGraphRequest request)
    {
        var nodes = new List<CanvasWorkbenchNode>();
        var links = new List<CanvasWorkbenchLink>();

        AppendSetupSelectionGraph(request, nodes, links);

        var blueprint = request.Blueprints.FirstOrDefault(item => item.Id == request.Editor.BlueprintId);
        if (blueprint is not null)
        {
            nodes.Add(new CanvasWorkbenchNode
            {
                Id = BlueprintCanvasNodeId,
                ParentId = SessionRootCanvasNodeId,
                Family = "special",
                Kind = "blueprint",
                Icon = "BP",
                Title = blueprint.Name,
                Subtitle = blueprint.PromptType,
                LeadText = string.IsNullOrWhiteSpace(blueprint.Guidance) ? blueprint.Summary : blueprint.Guidance,
                Status = "Blueprint",
                StatusPill = "Selected",
                AccentColor = "#7c3aed",
                PaletteKey = "violet",
                IsRequired = true,
                X = 520,
                Y = 110,
                Chips =
                [
                    new CanvasWorkbenchChip { Text = blueprint.PromptType, Tone = "accent" }
                ],
                FooterChips =
                [
                    new CanvasWorkbenchChip { Text = string.IsNullOrWhiteSpace(blueprint.RecommendedFlowKey) ? "No flow" : blueprint.RecommendedFlowKey, Tone = "neutral" }
                ],
                Annotations = BuildBlueprintAnnotations(blueprint),
                ContextActions = PromptFactoryCanvasCatalog.BuildBlueprintNodeActions(request.LibraryCatalog).ToList()
            });
            links.Add(new CanvasWorkbenchLink { SourceId = SessionRootCanvasNodeId, TargetId = BlueprintCanvasNodeId, Kind = "selection", IsUserAuthored = false });
        }

        var flow = request.Templates.FirstOrDefault(item => item.Id == request.Editor.FlowTemplateId);
        if (flow is not null)
        {
            nodes.Add(new CanvasWorkbenchNode
            {
                Id = FlowCanvasNodeId,
                ParentId = SessionRootCanvasNodeId,
                Family = "special",
                Kind = "flow",
                Icon = "FL",
                Title = flow.Name,
                Subtitle = $"{flow.AgentSequence.Count} agent step(s)",
                LeadText = flow.Summary,
                Status = "Flow",
                StatusPill = "Selected",
                AccentColor = "#2563eb",
                PaletteKey = "sky",
                IsRequired = true,
                X = 520,
                Y = 300,
                Chips =
                [
                    new CanvasWorkbenchChip { Text = $"{flow.BlockKeys.Count} blocks", Tone = "accent" }
                ],
                FooterChips =
                [
                    new CanvasWorkbenchChip { Text = $"{flow.AgentSequence.Count} agent step(s)", Tone = "success" }
                ],
                Annotations = BuildFlowAnnotations(flow),
                ContextActions = PromptFactoryCanvasCatalog.BuildFlowNodeActions(request.LibraryCatalog).ToList()
            });
            links.Add(new CanvasWorkbenchLink { SourceId = SessionRootCanvasNodeId, TargetId = FlowCanvasNodeId, Kind = "selection", IsUserAuthored = false });
        }

        var selectedBlocks = request.Blocks
            .Where(item => request.Editor.SelectedBlockIds.Contains(item.Id))
            .OrderBy(item => ResolveComponentSectionOrder(item.GroupKey))
            .ThenBy(item => ResolveGroupOrder(request.LibraryCatalog, item.GroupKey))
            .ThenBy(item => item.OrderIndex)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (selectedBlocks.Count > 0)
        {
            nodes.Add(new CanvasWorkbenchNode
            {
                Id = ComponentsRootCanvasNodeId,
                ParentId = SessionRootCanvasNodeId,
                Family = "group",
                Kind = "components",
                Icon = "CP",
                Title = "Prompt components",
                Subtitle = $"{selectedBlocks.Count} selected",
                LeadText = "Prompt library components currently active in this session.",
                Status = "Components",
                StatusPill = $"{selectedBlocks.Count} active",
                AccentColor = "#7c3aed",
                PaletteKey = "violet",
                IsRequired = true,
                IsCollapsible = true,
                X = 520,
                Y = 560,
                Chips =
                [
                    new CanvasWorkbenchChip { Text = $"{selectedBlocks.Count} components", Tone = "accent" }
                ],
                FooterChips =
                [
                    new CanvasWorkbenchChip { Text = request.Editor.ComponentCustomizations.Count(item => !string.IsNullOrWhiteSpace(item.RenderedContent)).ToString() + " customized", Tone = "success" }
                ],
                ContextActions = PromptFactoryCanvasCatalog.BuildSelectionContextActions(request.LibraryCatalog, "components-root").ToList()
            });
            links.Add(new CanvasWorkbenchLink { SourceId = SessionRootCanvasNodeId, TargetId = ComponentsRootCanvasNodeId, Kind = "selection", IsUserAuthored = false });

            var sectionGroups = selectedBlocks
                .GroupBy(item => ResolveComponentSectionKey(item.GroupKey))
                .OrderBy(group => ResolveComponentSectionOrder(group.Key))
                .ToList();

            for (var sectionIndex = 0; sectionIndex < sectionGroups.Count; sectionIndex++)
            {
                var section = sectionGroups[sectionIndex];
                var sectionNodeId = $"{ComponentSectionCanvasNodePrefix}{section.Key}";
                nodes.Add(new CanvasWorkbenchNode
                {
                    Id = sectionNodeId,
                    ParentId = ComponentsRootCanvasNodeId,
                    Family = "group",
                    Kind = "component section",
                    Icon = "SC",
                    Title = ResolveComponentSectionLabel(section.Key),
                    Subtitle = $"{section.Count()} component(s)",
                    LeadText = ResolveComponentSectionDescription(section.Key),
                    Status = "Section",
                    StatusPill = section.Count().ToString(),
                    AccentColor = ResolveComponentSectionAccent(section.Key),
                    PaletteKey = ResolveComponentSectionPalette(section.Key),
                    IsCollapsible = true,
                    X = 850,
                    Y = 460 + (sectionIndex * 210),
                    ContextActions = PromptFactoryCanvasCatalog.BuildSelectionContextActions(request.LibraryCatalog, "component-section").ToList()
                });
                links.Add(new CanvasWorkbenchLink { SourceId = ComponentsRootCanvasNodeId, TargetId = sectionNodeId, Kind = "contains", IsUserAuthored = false });

                var groupedBlocks = section
                    .GroupBy(item => item.GroupKey)
                    .OrderBy(group => ResolveGroupOrder(request.LibraryCatalog, group.Key))
                    .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                for (var groupIndex = 0; groupIndex < groupedBlocks.Count; groupIndex++)
                {
                    var blockGroup = groupedBlocks[groupIndex];
                    var groupNodeId = $"{ComponentGroupCanvasNodePrefix}{blockGroup.Key}";
                    nodes.Add(new CanvasWorkbenchNode
                    {
                        Id = groupNodeId,
                        ParentId = sectionNodeId,
                        Family = "group",
                        Kind = "component group",
                        Icon = "GR",
                        Title = ResolveLibraryGroupLabel(request.LibraryCatalog, blockGroup.Key),
                        Subtitle = $"{blockGroup.Count()} selected",
                        LeadText = ResolveLibraryGroupSummary(request.LibraryCatalog, blockGroup.Key),
                        Status = "Group",
                        StatusPill = blockGroup.Count().ToString(),
                        AccentColor = ResolveGroupAccent(blockGroup.Key),
                        PaletteKey = ResolveGroupPalette(blockGroup.Key),
                        IsCollapsible = true,
                        X = 1160,
                        Y = 410 + (sectionIndex * 210) + (groupIndex * 120),
                        ContextActions = PromptFactoryCanvasCatalog.BuildSelectionContextActions(request.LibraryCatalog, "component-group").ToList()
                    });
                    links.Add(new CanvasWorkbenchLink { SourceId = sectionNodeId, TargetId = groupNodeId, Kind = "contains", IsUserAuthored = false });

                    var blockIndex = 0;
                    foreach (var block in blockGroup.OrderBy(item => item.OrderIndex).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        var customization = request.Editor.ComponentCustomizations.LastOrDefault(item => item.BlockId == block.Id);
                        var componentNodeId = $"{ComponentCanvasNodePrefix}{block.Key}";
                        nodes.Add(new CanvasWorkbenchNode
                        {
                            Id = componentNodeId,
                            ParentId = groupNodeId,
                            Family = "item",
                            Kind = "component",
                            Icon = "BL",
                            Title = block.Name,
                            Subtitle = block.BlockKind.ToString(),
                            LeadText = ResolveComponentLeadText(block, customization),
                            Status = "Component",
                            StatusPill = customization?.TemplateValues.Count > 0 ? $"{customization.TemplateValues.Count} inputs" : "Ready",
                            AccentColor = ResolveGroupAccent(block.GroupKey),
                            PaletteKey = ResolveGroupPalette(block.GroupKey),
                            IsRequired = true,
                            X = 1440,
                            Y = 385 + (sectionIndex * 210) + (groupIndex * 120) + (blockIndex * 92),
                            Chips = block.Tags.Take(3)
                                .Select(tag => new CanvasWorkbenchChip { Text = tag, Tone = "neutral" })
                                .ToList(),
                            FooterChips =
                            [
                                new CanvasWorkbenchChip { Text = ResolveLibraryGroupLabel(request.LibraryCatalog, block.GroupKey), Tone = "accent" },
                                new CanvasWorkbenchChip { Text = block.TemplateTokens.Count > 0 ? "Specified" : "Static", Tone = customization is null ? "neutral" : "success" }
                            ],
                            Annotations = BuildComponentAnnotations(block, customization),
                            ContextActions = PromptFactoryCanvasCatalog.BuildComponentNodeActions(block).ToList()
                        });
                        links.Add(new CanvasWorkbenchLink { SourceId = groupNodeId, TargetId = componentNodeId, Kind = "contains", IsUserAuthored = false });
                        blockIndex++;
                    }
                }
            }
        }

        AppendInputSelectionGraph(request, nodes, links);
        return (nodes, links);
    }

    private List<CanvasWorkbenchNode> BuildCanvasNodes(
        PromptFactorySessionGraphRequest request,
        (List<CanvasWorkbenchNode> Nodes, List<CanvasWorkbenchLink> Links) selectionGraph)
    {
        var nodes = new List<CanvasWorkbenchNode>
        {
            new()
            {
                Id = SessionRootCanvasNodeId,
                Family = "root",
                Kind = "session",
                Icon = "PF",
                Title = string.IsNullOrWhiteSpace(request.Editor.SessionName) ? "Prompt session" : request.Editor.SessionName,
                Subtitle = string.IsNullOrWhiteSpace(request.Editor.Phase) ? "Phase not selected" : request.Editor.Phase,
                LeadText = string.IsNullOrWhiteSpace(request.Editor.DraftTitle) ? "Build the session to materialize prompt steps." : request.Editor.DraftTitle,
                Status = "Session",
                StatusPill = string.IsNullOrWhiteSpace(request.Editor.Phase) ? "Draft" : request.Editor.Phase,
                AccentColor = "#111827",
                PaletteKey = "violet",
                IsRequired = true,
                IsCollapsible = true,
                X = 210,
                Y = 250,
                Chips =
                [
                    new CanvasWorkbenchChip { Text = request.Editor.ProjectId.HasValue ? "Project linked" : "No project", Tone = request.Editor.ProjectId.HasValue ? "success" : "warning" },
                    new CanvasWorkbenchChip { Text = request.Editor.ProviderProfileId.HasValue ? "Provider set" : "Provider pending", Tone = request.Editor.ProviderProfileId.HasValue ? "accent" : "warning" }
                ],
                FooterChips =
                [
                    new CanvasWorkbenchChip { Text = $"{request.Editor.SelectedBlockIds.Count} components", Tone = "accent" },
                    new CanvasWorkbenchChip { Text = $"{request.VisibleSessionAttachments.Count} inputs", Tone = "success" }
                ],
                Annotations = BuildSessionAnnotations(request),
                ContextActions = PromptFactoryCanvasCatalog.BuildSessionContextActions(request.LibraryCatalog).ToList()
            }
        };

        nodes.AddRange(selectionGraph.Nodes);

        var positions = new Dictionary<string, (double X, double Y)>
        {
            [SessionRootCanvasNodeId] = (210, 250)
        };

        var groupedNodes = request.Editor.Nodes.Count == 0
            ? [new { BranchKey = "main", BranchLabel = "Main", Items = new List<PromptRunNodeSummary>() }]
            : request.Editor.Nodes
                .GroupBy(node => node.BranchKey)
                .OrderBy(group => string.Equals(group.Key, "main", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => new
                {
                    BranchKey = group.Key,
                    BranchLabel = string.IsNullOrWhiteSpace(group.First().BranchLabel) ? group.Key : group.First().BranchLabel,
                    Items = group.OrderBy(item => item.Sequence).ToList()
                })
                .ToArray();

        var branchOffset = 0;
        foreach (var branch in groupedNodes)
        {
            var groupId = $"branch:{branch.BranchKey}";
            string parentCanvasId = SessionRootCanvasNodeId;
            double baseX = 1040;
            double baseY = 200 + (branchOffset * 140);

            var firstBranchItem = branch.Items.FirstOrDefault(item => item.ParentNodeId.HasValue);
            if (firstBranchItem?.ParentNodeId is Guid parentNodeId &&
                positions.TryGetValue($"node:{parentNodeId:N}", out var parentPosition))
            {
                parentCanvasId = $"node:{parentNodeId:N}";
                baseX = parentPosition.X + 120;
                baseY = parentPosition.Y + 190 + (branchOffset * 20);
            }
            else if (string.Equals(branch.BranchKey, "main", StringComparison.OrdinalIgnoreCase))
            {
                branchOffset = 0;
            }

            positions[groupId] = (baseX, baseY);
            nodes.Add(new CanvasWorkbenchNode
            {
                Id = groupId,
                ParentId = parentCanvasId,
                Family = "group",
                Kind = "branch",
                Icon = "BR",
                Title = branch.BranchLabel,
                Subtitle = branch.BranchKey,
                LeadText = branch.Items.Count == 0 ? "Build the prompt to materialize steps in this branch." : $"{branch.Items.Count} step(s) in this branch.",
                Status = "Branch",
                StatusPill = branch.Items.Count == 0 ? "Ready" : $"{branch.Items.Count} steps",
                AccentColor = "#8b5cf6",
                PaletteKey = "violet",
                IsRequired = true,
                IsCollapsible = branch.Items.Count > 0,
                X = baseX,
                Y = baseY,
                Chips =
                [
                    new CanvasWorkbenchChip { Text = string.Equals(branch.BranchKey, "main", StringComparison.OrdinalIgnoreCase) ? "Primary" : "Follow-up", Tone = "accent" }
                ],
                FooterChips =
                [
                    new CanvasWorkbenchChip { Text = branch.Items.Count == 0 ? "No steps yet" : $"{branch.Items.Count} steps", Tone = branch.Items.Count == 0 ? "warning" : "success" }
                ],
                Annotations = BuildBranchAnnotations(branch.BranchKey, branch.Items.Count),
                ContextActions =
                [
                    new CanvasWorkbenchAction { ActionId = "build-flow", Label = "Build", Icon = "flow", Tone = "mint" },
                    new CanvasWorkbenchAction { ActionId = "branch-selected", Label = "Branch", Icon = "fork", Tone = "warn" }
                ]
            });

            var stepIndex = 0;
            foreach (var node in branch.Items)
            {
                var nodeId = $"node:{node.Id:N}";
                var x = baseX + 290 + (stepIndex * 238);
                var y = baseY + (string.Equals(branch.BranchKey, "main", StringComparison.OrdinalIgnoreCase) ? 0 : stepIndex * 84);
                positions[nodeId] = (x, y);

                nodes.Add(new CanvasWorkbenchNode
                {
                    Id = nodeId,
                    ParentId = groupId,
                    Family = "item",
                    Kind = "prompt step",
                    Icon = "ST",
                    Title = node.Title,
                    Subtitle = $"{node.BranchLabel} · step {node.Sequence}",
                    LeadText = string.IsNullOrWhiteSpace(node.Notes) ? "Prompt step ready for review." : node.Notes,
                    Status = node.State.ToString(),
                    BranchLabel = node.BranchLabel,
                    AccentColor = ResolvePromptNodeAccent(node.State),
                    PaletteKey = ResolvePromptNodePalette(node.State),
                    StatusPill = node.State.ToString(),
                    IsRequired = true,
                    X = x,
                    Y = y,
                    Chips =
                    [
                        new CanvasWorkbenchChip { Text = node.BranchKey, Tone = "accent" }
                    ],
                    FooterChips =
                    [
                        new CanvasWorkbenchChip { Text = node.PromptArtifactId.HasValue ? "Artifact" : "Draft only", Tone = node.PromptArtifactId.HasValue ? "success" : "warning" },
                        new CanvasWorkbenchChip { Text = node.ParentNodeId.HasValue ? "Derived" : "Primary", Tone = "neutral" }
                    ],
                    Annotations = BuildPromptNodeAnnotations(node),
                    ContextActions = BuildPromptNodeActions()
                });

                stepIndex++;
            }

            branchOffset++;
        }

        return nodes;
    }

    private List<CanvasWorkbenchLink> BuildCanvasLinks(
        PromptFactorySessionGraphRequest request,
        (List<CanvasWorkbenchNode> Nodes, List<CanvasWorkbenchLink> Links) selectionGraph)
    {
        var links = selectionGraph.Links.ToList();
        links.Add(new CanvasWorkbenchLink { SourceId = SessionRootCanvasNodeId, TargetId = "branch:main", Kind = "contains", IsUserAuthored = false });

        foreach (var node in request.Editor.Nodes)
        {
            var nodeCanvasId = $"node:{node.Id:N}";
            var branchCanvasId = $"branch:{node.BranchKey}";
            links.Add(new CanvasWorkbenchLink
            {
                SourceId = branchCanvasId,
                TargetId = nodeCanvasId,
                Kind = "contains",
                IsUserAuthored = false
            });

            if (node.ParentNodeId.HasValue)
            {
                links.Add(new CanvasWorkbenchLink
                {
                    SourceId = $"node:{node.ParentNodeId.Value:N}",
                    TargetId = branchCanvasId,
                    Kind = "derived",
                    IsUserAuthored = true
                });
            }
        }

        return links
            .GroupBy(link => $"{link.SourceId}|{link.TargetId}|{link.Kind}", StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
    }

    private static void AppendSetupSelectionGraph(
        PromptFactorySessionGraphRequest request,
        List<CanvasWorkbenchNode> nodes,
        List<CanvasWorkbenchLink> links)
    {
        nodes.Add(new CanvasWorkbenchNode
        {
            Id = SetupCanvasNodeId,
            ParentId = SessionRootCanvasNodeId,
            Family = "special",
            Kind = "setup",
            Icon = "SET",
            Title = "Session setup",
            Subtitle = request.SetupSummaryLine,
            LeadText = request.SetupLeadCopy,
            Status = "Setup",
            StatusPill = request.SetupIsReady ? "Ready" : $"{request.MissingSetupFieldCount} missing",
            AccentColor = request.SetupIsReady ? "#0f766e" : "#0f172a",
            PaletteKey = request.SetupIsReady ? "mint" : "sky",
            IsRequired = true,
            X = 520,
            Y = -70,
            Chips =
            [
                new CanvasWorkbenchChip { Text = request.SetupStatusLabel, Tone = request.SetupIsReady ? "success" : "warn" }
            ],
            FooterChips =
            [
                new CanvasWorkbenchChip { Text = string.IsNullOrWhiteSpace(request.SessionSetup.WorkRepository) ? "Repository needed" : request.SessionSetup.WorkRepository, Tone = "neutral" }
            ],
            Annotations = BuildSetupAnnotations(request),
            ContextActions =
            [
                new CanvasWorkbenchAction
                {
                    ActionId = "setup:open",
                    Label = "Setup",
                    MenuLabel = "Setup",
                    Description = "Open the session setup guide.",
                    Icon = "session",
                    Tone = "sky"
                }
            ]
        });

        links.Add(new CanvasWorkbenchLink
        {
            SourceId = SessionRootCanvasNodeId,
            TargetId = SetupCanvasNodeId,
            Kind = "selection",
            IsUserAuthored = false
        });
    }

    private static void AppendInputSelectionGraph(
        PromptFactorySessionGraphRequest request,
        List<CanvasWorkbenchNode> nodes,
        List<CanvasWorkbenchLink> links)
    {
        if (request.VisibleSessionAttachments.Count == 0)
        {
            return;
        }

        nodes.Add(new CanvasWorkbenchNode
        {
            Id = InputsRootCanvasNodeId,
            ParentId = SessionRootCanvasNodeId,
            Family = "group",
            Kind = "inputs",
            Icon = "IN",
            Title = "Prompt inputs",
            Subtitle = $"{request.VisibleSessionAttachments.Count} attached",
            LeadText = "Files, media, links, and notes explicitly attached to the prompt session.",
            Status = "Inputs",
            StatusPill = request.VisibleSessionAttachments.Count.ToString(),
            AccentColor = "#059669",
            PaletteKey = "mint",
            IsCollapsible = true,
            X = 520,
            Y = 860,
            ContextActions = PromptFactoryCanvasCatalog.BuildSelectionContextActions(request.LibraryCatalog, "inputs-root").ToList()
        });
        links.Add(new CanvasWorkbenchLink { SourceId = SessionRootCanvasNodeId, TargetId = InputsRootCanvasNodeId, Kind = "selection", IsUserAuthored = false });

        for (var index = 0; index < request.VisibleSessionAttachments.Count; index++)
        {
            var attachment = request.VisibleSessionAttachments[index];
            var inputNodeId = $"{InputCanvasNodePrefix}{attachment.Id}";
            nodes.Add(new CanvasWorkbenchNode
            {
                Id = inputNodeId,
                ParentId = InputsRootCanvasNodeId,
                Family = "special",
                Kind = "input",
                Icon = ResolveAttachmentNodeIcon(attachment),
                Title = string.IsNullOrWhiteSpace(attachment.Title) ? $"Input {index + 1}" : attachment.Title,
                Subtitle = ResolveAttachmentSubtitle(attachment),
                LeadText = ResolveAttachmentLeadText(attachment),
                Status = ResolveAttachmentVisualKind(attachment),
                StatusPill = ResolveAttachmentStatusPill(attachment),
                AccentColor = ResolveAttachmentAccent(attachment),
                PaletteKey = ResolveAttachmentPalette(attachment),
                X = 860,
                Y = 780 + (index * 124),
                MediaKind = ResolveAttachmentVisualKind(attachment),
                MediaPreviewUrl = attachment.MediaRoute,
                MediaPreviewAlt = string.IsNullOrWhiteSpace(attachment.Title) ? attachment.Kind : attachment.Title,
                MediaContentType = attachment.MediaContentType,
                MediaFileName = attachment.MediaOriginalFileName,
                FooterChips =
                [
                    new CanvasWorkbenchChip
                    {
                        Text = string.IsNullOrWhiteSpace(attachment.MediaOriginalFileName)
                            ? ResolveAttachmentStatusPill(attachment)
                            : attachment.MediaOriginalFileName,
                        Tone = "neutral"
                    }
                ],
                ContextActions = PromptFactoryCanvasCatalog.BuildInputNodeActions(attachment.Id).ToList()
            });
            links.Add(new CanvasWorkbenchLink { SourceId = InputsRootCanvasNodeId, TargetId = inputNodeId, Kind = "contains", IsUserAuthored = false });
        }
    }

    private static string ResolveComponentLeadText(PromptBlockSummary block, PromptSessionComponentCustomization? customization)
        => customization?.TemplateValues.Count > 0
            ? string.Join(" | ", customization.TemplateValues.Select(item => $"{item.Key}: {item.Value}"))
            : block.Summary;

    private static string ResolveLibraryGroupLabel(PromptLibraryCatalogSummary libraryCatalog, string groupKey)
        => libraryCatalog.Groups.FirstOrDefault(item => string.Equals(item.Key, groupKey, StringComparison.OrdinalIgnoreCase))?.Name
            ?? "Custom components";

    private static string ResolveLibraryGroupSummary(PromptLibraryCatalogSummary libraryCatalog, string groupKey)
        => libraryCatalog.Groups.FirstOrDefault(item => string.Equals(item.Key, groupKey, StringComparison.OrdinalIgnoreCase))?.Summary
            ?? "Custom prompt components added in this workspace.";

    private static int ResolveGroupOrder(PromptLibraryCatalogSummary libraryCatalog, string groupKey)
        => libraryCatalog.Groups.FirstOrDefault(item => string.Equals(item.Key, groupKey, StringComparison.OrdinalIgnoreCase))?.Order ?? int.MaxValue;

    private static string ResolveComponentSectionKey(string groupKey)
        => groupKey switch
        {
            "session-framing" or "mission-scope" or "context-discovery" or "guardrails" => "foundation",
            "workflow-orchestration" or "architecture-analysis" or "planning-checklists" or "implementation-execution" => "delivery",
            "validation-review" or "output-handoff" => "validation",
            "stack-profiles" or "toolbox-snippets" => "environment",
            _ => "custom"
        };

    private static int ResolveComponentSectionOrder(string groupKey)
        => ResolveComponentSectionKey(groupKey) switch
        {
            "foundation" => 1,
            "delivery" => 2,
            "validation" => 3,
            "environment" => 4,
            _ => 5
        };

    private static string ResolveComponentSectionLabel(string sectionKey)
        => sectionKey switch
        {
            "foundation" => "Foundation",
            "delivery" => "Delivery",
            "validation" => "Validation",
            "environment" => "Environment",
            _ => "Custom"
        };

    private static string ResolveComponentSectionDescription(string sectionKey)
        => sectionKey switch
        {
            "foundation" => "Role, mission framing, context loading, and guardrails.",
            "delivery" => "Workflow, architecture, planning, and implementation execution.",
            "validation" => "Validation evidence and output handoff.",
            "environment" => "Stack profiles and toolbox snippets.",
            _ => "Custom components created in this workspace."
        };

    private static string ResolveComponentSectionAccent(string sectionKey)
        => sectionKey switch
        {
            "foundation" => "#7c3aed",
            "delivery" => "#2563eb",
            "validation" => "#d97706",
            "environment" => "#059669",
            _ => "#374151"
        };

    private static string ResolveComponentSectionPalette(string sectionKey)
        => sectionKey switch
        {
            "foundation" => "violet",
            "delivery" => "sky",
            "validation" => "warn",
            "environment" => "mint",
            _ => "neutral"
        };

    private static string ResolveGroupAccent(string groupKey)
        => ResolveComponentSectionAccent(ResolveComponentSectionKey(groupKey));

    private static string ResolveGroupPalette(string groupKey)
        => ResolveComponentSectionPalette(ResolveComponentSectionKey(groupKey));

    private static string ResolveAttachmentSubtitle(PromptSessionAttachmentSummary attachment)
    {
        return ResolveAttachmentVisualKind(attachment) switch
        {
            "link" => attachment.LinkUrl,
            "image" or "video" or "pdf" or "spreadsheet" or "document" or "text" or "archive" or "file" => string.IsNullOrWhiteSpace(attachment.MediaOriginalFileName)
                ? ResolveAttachmentStatusPill(attachment)
                : attachment.MediaOriginalFileName,
            _ => string.IsNullOrWhiteSpace(attachment.Subtitle) ? attachment.Kind : attachment.Subtitle
        };
    }

    private static string ResolveAttachmentLeadText(PromptSessionAttachmentSummary attachment)
    {
        if (!string.IsNullOrWhiteSpace(attachment.Notes))
        {
            return attachment.Notes;
        }

        if (!string.IsNullOrWhiteSpace(attachment.Subtitle))
        {
            return $"Use this input for: {attachment.Subtitle}";
        }

        return attachment.Kind;
    }

    private static string ResolveAttachmentVisualKind(PromptSessionAttachmentSummary attachment)
    {
        if (string.Equals(attachment.Kind, "image", StringComparison.OrdinalIgnoreCase))
        {
            return "image";
        }

        if (string.Equals(attachment.Kind, "video", StringComparison.OrdinalIgnoreCase))
        {
            return "video";
        }

        if (string.Equals(attachment.Kind, "link", StringComparison.OrdinalIgnoreCase))
        {
            return "link";
        }

        if (string.Equals(attachment.Kind, "note", StringComparison.OrdinalIgnoreCase))
        {
            return "note";
        }

        var extension = Path.GetExtension(attachment.MediaOriginalFileName)?.Trim().ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "pdf",
            ".xls" or ".xlsx" or ".csv" => "spreadsheet",
            ".doc" or ".docx" or ".ppt" or ".pptx" => "document",
            ".txt" or ".md" or ".json" or ".xml" or ".log" => "text",
            ".zip" or ".rar" or ".7z" => "archive",
            _ => "file"
        };
    }

    private static string ResolveAttachmentNodeIcon(PromptSessionAttachmentSummary attachment)
        => ResolveAttachmentVisualKind(attachment) switch
        {
            "image" => "IMG",
            "video" => "VID",
            "link" => "URL",
            "note" => "NOTE",
            "pdf" => "PDF",
            "spreadsheet" => "XLS",
            "document" => "DOC",
            "text" => "TXT",
            "archive" => "ZIP",
            _ => "FILE"
        };

    private static string ResolveAttachmentStatusPill(PromptSessionAttachmentSummary attachment)
        => ResolveAttachmentVisualKind(attachment) switch
        {
            "image" => "Image",
            "video" => "Video",
            "link" => "Link",
            "note" => "Note",
            "pdf" => "PDF",
            "spreadsheet" => "Spreadsheet",
            "document" => "Document",
            "text" => "Text",
            "archive" => "Archive",
            _ => "File"
        };

    private static string ResolveAttachmentAccent(PromptSessionAttachmentSummary attachment)
        => ResolveAttachmentVisualKind(attachment) switch
        {
            "image" => "#2563eb",
            "video" => "#7c3aed",
            "link" => "#0284c7",
            "note" => "#4b5563",
            "pdf" => "#dc2626",
            "spreadsheet" => "#15803d",
            "document" => "#ea580c",
            "text" => "#475569",
            "archive" => "#7c2d12",
            _ => "#059669"
        };

    private static string ResolveAttachmentPalette(PromptSessionAttachmentSummary attachment)
        => ResolveAttachmentVisualKind(attachment) switch
        {
            "image" => "sky",
            "video" => "violet",
            "link" => "sky",
            "note" => "neutral",
            "pdf" => "danger",
            "spreadsheet" => "mint",
            "document" => "warn",
            "text" => "neutral",
            "archive" => "warn",
            _ => "mint"
        };

    private static List<CanvasWorkbenchAction> BuildPromptNodeActions()
        =>
        [
            new() { ActionId = "branch", Label = "Branch", Icon = "fork", Tone = "accent" },
            new() { ActionId = "mark-used", Label = "Used", Icon = "use", Tone = "mint" },
            new() { ActionId = "validate-node", Label = "Validate", Icon = "qa", Tone = "mint" },
            new() { ActionId = "skip-node", Label = "Skip", Icon = "skip", Tone = "warn" },
            new() { ActionId = "open-prompt", Label = "Open", Icon = "open", Tone = "ghost" }
        ];

    private static string ResolvePromptNodeAccent(PromptRunNodeState state) => state switch
    {
        PromptRunNodeState.Validated => "#059669",
        PromptRunNodeState.Used => "#0284c7",
        PromptRunNodeState.Failed => "#e11d48",
        PromptRunNodeState.Skipped or PromptRunNodeState.Superseded => "#f97316",
        _ => "#7c3aed"
    };

    private static string ResolvePromptNodePalette(PromptRunNodeState state) => state switch
    {
        PromptRunNodeState.Validated => "mint",
        PromptRunNodeState.Used => "sky",
        PromptRunNodeState.Failed => "rose",
        PromptRunNodeState.Skipped or PromptRunNodeState.Superseded => "amber",
        _ => "violet"
    };

    private static void ConfigureChrome(CanvasWorkbenchChrome chrome)
    {
        chrome.EmptyStateKicker = "Prompt factory";
        chrome.EmptyStateTitle = "Assemble the prompt graph";
        chrome.EmptyStateDescription = "Choose a blueprint or flow, then add recommended blocks to materialize the session graph.";
        chrome.Diagnostics.IsEnabled = true;
        chrome.Minimap.IsEnabled = true;
        chrome.Minimap.Title = "Prompt overview";
        chrome.Clipboard.IsEnabled = true;
    }

    private static List<CanvasWorkbenchAnnotation> BuildSessionAnnotations(PromptFactorySessionGraphRequest request)
    {
        var annotations = new List<CanvasWorkbenchAnnotation>();
        if (request.Editor.Warnings.Count > 0)
        {
            annotations.Add(new CanvasWorkbenchAnnotation
            {
                Id = "session:warnings",
                Kind = "validation",
                Tone = "warn",
                Label = $"{request.Editor.Warnings.Count} warning(s)",
                Description = string.IsNullOrWhiteSpace(request.Editor.WarningSummary)
                    ? request.Editor.Warnings[0]
                    : request.Editor.WarningSummary,
                Icon = "!",
                ActionId = "apply-recommendations"
            });
        }

        return annotations;
    }

    private static List<CanvasWorkbenchAnnotation> BuildSetupAnnotations(PromptFactorySessionGraphRequest request)
    {
        if (request.SetupIsReady)
        {
            return [];
        }

        return
        [
            new CanvasWorkbenchAnnotation
            {
                Id = "setup:missing",
                Kind = "validation",
                Tone = "warn",
                Label = $"{request.MissingSetupFieldCount} missing",
                Description = "Complete the repository, provider, and project setup fields before building or sending the session.",
                Icon = "SET"
            }
        ];
    }

    private static List<CanvasWorkbenchAnnotation> BuildBlueprintAnnotations(PromptBlueprintSummary blueprint)
    {
        var annotations = new List<CanvasWorkbenchAnnotation>();
        if (!string.IsNullOrWhiteSpace(blueprint.RecommendedFlowKey))
        {
            annotations.Add(new CanvasWorkbenchAnnotation
            {
                Id = $"blueprint:{blueprint.Id:N}:recommendation",
                Kind = "recommendation",
                Tone = "accent",
                Label = "Recommended flow",
                Description = $"This blueprint prefers '{blueprint.RecommendedFlowKey}'. Apply recommendations to sync blocks and flow.",
                Icon = "REC",
                ActionId = "apply-recommendations"
            });
        }

        return annotations;
    }

    private static List<CanvasWorkbenchAnnotation> BuildFlowAnnotations(PromptFlowTemplateSummary flow)
    {
        if (flow.BlockKeys.Count > 0)
        {
            return [];
        }

        return
        [
            new CanvasWorkbenchAnnotation
            {
                Id = $"flow:{flow.Id:N}:empty",
                Kind = "validation",
                Tone = "warn",
                Label = "Missing blocks",
                Description = "This flow template has no linked blocks, so the run graph will stay sparse until you add content.",
                Icon = "QA"
            }
        ];
    }

    private static List<CanvasWorkbenchAnnotation> BuildComponentAnnotations(PromptBlockSummary block, PromptSessionComponentCustomization? customization)
    {
        var annotations = new List<CanvasWorkbenchAnnotation>();
        if (block.IsRecommendedByDefault)
        {
            annotations.Add(new CanvasWorkbenchAnnotation
            {
                Id = $"component:{block.Key}:recommended",
                Kind = "recommendation",
                Tone = "accent",
                Label = "Recommended",
                Description = "This block is part of the recommended baseline for the current blueprint or phase.",
                Icon = "REC"
            });
        }

        if (block.TemplateTokens.Count > 0 && customization?.TemplateValues.Count != block.TemplateTokens.Count)
        {
            annotations.Add(new CanvasWorkbenchAnnotation
            {
                Id = $"component:{block.Key}:inputs",
                Kind = "validation",
                Tone = "warn",
                Label = "Needs inputs",
                Description = "One or more template values are still missing for this prompt component.",
                Icon = "QA"
            });
        }

        return annotations;
    }

    private static List<CanvasWorkbenchAnnotation> BuildBranchAnnotations(string branchKey, int itemCount)
    {
        if (itemCount > 0)
        {
            return [];
        }

        return
        [
            new CanvasWorkbenchAnnotation
            {
                Id = $"branch:{branchKey}:empty",
                Kind = "recommendation",
                Tone = "accent",
                Label = "Build branch",
                Description = "Run the build flow to materialize prompt steps for this branch.",
                Icon = "BR",
                ActionId = "build-flow"
            }
        ];
    }

    private static List<CanvasWorkbenchAnnotation> BuildPromptNodeAnnotations(PromptRunNodeSummary node)
    {
        return node.State switch
        {
            PromptRunNodeState.Failed =>
            [
                new CanvasWorkbenchAnnotation
                {
                    Id = $"node:{node.Id:N}:failed",
                    Kind = "validation",
                    Tone = "danger",
                    Label = "Failed",
                    Description = "This prompt step failed and should be validated or branched before reuse.",
                    Icon = "QA",
                    ActionId = "validate-node"
                }
            ],
            PromptRunNodeState.Prepared =>
            [
                new CanvasWorkbenchAnnotation
                {
                    Id = $"node:{node.Id:N}:review",
                    Kind = "validation",
                    Tone = "warn",
                    Label = "Review",
                    Description = "Prepared steps are ready for validation, preview, or branching.",
                    Icon = "QA",
                    ActionId = "validate-node"
                }
            ],
            _ => []
        };
    }
}

public sealed record PromptFactorySessionGraphRequest(
    PromptFactoryEditorModel Editor,
    PromptLibraryCatalogSummary LibraryCatalog,
    IReadOnlyList<PromptBlueprintSummary> Blueprints,
    IReadOnlyList<PromptFlowTemplateSummary> Templates,
    IReadOnlyList<PromptBlockSummary> Blocks,
    IReadOnlyList<PromptSessionAttachmentSummary> VisibleSessionAttachments,
    PromptSessionSetupProfile SessionSetup,
    CanvasWorkbenchUiState UiState,
    string SetupSummaryLine,
    string SetupLeadCopy,
    string SetupStatusLabel,
    bool SetupIsReady,
    int MissingSetupFieldCount);
