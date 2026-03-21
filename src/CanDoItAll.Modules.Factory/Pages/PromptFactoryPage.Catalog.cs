using CanDoItAll.ComponentKit.Canvas;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Factory.Pages;

public partial class PromptFactoryPage
{
    private const string BlueprintCanvasNodeId = "selection:blueprint";
    private const string FlowCanvasNodeId = "selection:flow";
    private const string ComponentsRootCanvasNodeId = "selection:components";
    private const string InputsRootCanvasNodeId = "selection:inputs";
    private const string ComponentSectionCanvasNodePrefix = "selection:component-section:";
    private const string ComponentGroupCanvasNodePrefix = "selection:component-group:";
    private const string ComponentCanvasNodePrefix = "selection:component:";
    private const string InputCanvasNodePrefix = "selection:input:";

    private PromptLibraryCatalogSummary libraryCatalog = new([], [], [], 0, 0, 0);

    private PromptBlueprintSummary? selectedBlueprintNode
        => string.Equals(selectedCanvasNodeId, BlueprintCanvasNodeId, StringComparison.Ordinal)
            ? blueprints.FirstOrDefault(item => item.Id == editor.BlueprintId)
            : null;

    private PromptFlowTemplateSummary? selectedFlowNode
        => string.Equals(selectedCanvasNodeId, FlowCanvasNodeId, StringComparison.Ordinal)
            ? templates.FirstOrDefault(item => item.Id == editor.FlowTemplateId)
            : null;

    private PromptBlockSummary? selectedComponentNode
        => TryParseSelectedComponentNodeId(selectedCanvasNodeId, out var blockKey)
            ? blocks.FirstOrDefault(item => string.Equals(item.Key, blockKey, StringComparison.OrdinalIgnoreCase))
            : null;

    private PromptSessionAttachmentSummary? selectedAttachmentNode
        => TryParseSelectedInputNodeId(selectedCanvasNodeId, out var attachmentId)
            ? editor.SessionAttachments.FirstOrDefault(item => string.Equals(item.Id, attachmentId, StringComparison.OrdinalIgnoreCase))
            : null;

    private PromptLibraryGroupSummary? selectedComponentGroupNode
        => TryParseComponentGroupNodeId(selectedCanvasNodeId, out var groupKey)
            ? libraryCatalog.Groups.FirstOrDefault(item => string.Equals(item.Key, groupKey, StringComparison.OrdinalIgnoreCase))
            : null;

    private (List<CanvasWorkbenchNode> Nodes, List<CanvasWorkbenchLink> Links) BuildSelectionGraph()
    {
        var nodes = new List<CanvasWorkbenchNode>();
        var links = new List<CanvasWorkbenchLink>();

        var blueprint = blueprints.FirstOrDefault(item => item.Id == editor.BlueprintId);
        if (blueprint is not null)
        {
            nodes.Add(new CanvasWorkbenchNode
            {
                Id = BlueprintCanvasNodeId,
                ParentId = "session-root",
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
                ContextActions = PromptFactoryCanvasCatalog.BuildBlueprintNodeActions(libraryCatalog).ToList()
            });
            links.Add(new CanvasWorkbenchLink { SourceId = "session-root", TargetId = BlueprintCanvasNodeId, Kind = "selection", IsUserAuthored = false });
        }

        var flow = templates.FirstOrDefault(item => item.Id == editor.FlowTemplateId);
        if (flow is not null)
        {
            nodes.Add(new CanvasWorkbenchNode
            {
                Id = FlowCanvasNodeId,
                ParentId = "session-root",
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
                ContextActions = PromptFactoryCanvasCatalog.BuildFlowNodeActions(libraryCatalog).ToList()
            });
            links.Add(new CanvasWorkbenchLink { SourceId = "session-root", TargetId = FlowCanvasNodeId, Kind = "selection", IsUserAuthored = false });
        }

        var selectedBlocks = blocks
            .Where(item => editor.SelectedBlockIds.Contains(item.Id))
            .OrderBy(item => ResolveComponentSectionOrder(item.GroupKey))
            .ThenBy(item => ResolveGroupOrder(item.GroupKey))
            .ThenBy(item => item.OrderIndex)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (selectedBlocks.Count > 0)
        {
            nodes.Add(new CanvasWorkbenchNode
            {
                Id = ComponentsRootCanvasNodeId,
                ParentId = "session-root",
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
                    new CanvasWorkbenchChip { Text = editor.ComponentCustomizations.Count(item => !string.IsNullOrWhiteSpace(item.RenderedContent)).ToString() + " customized", Tone = "success" }
                ],
                ContextActions = PromptFactoryCanvasCatalog.BuildSelectionContextActions(libraryCatalog, "components-root").ToList()
            });
            links.Add(new CanvasWorkbenchLink { SourceId = "session-root", TargetId = ComponentsRootCanvasNodeId, Kind = "selection", IsUserAuthored = false });

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
                    ContextActions = PromptFactoryCanvasCatalog.BuildSelectionContextActions(libraryCatalog, "component-section").ToList()
                });
                links.Add(new CanvasWorkbenchLink { SourceId = ComponentsRootCanvasNodeId, TargetId = sectionNodeId, Kind = "contains", IsUserAuthored = false });

                var groupedBlocks = section
                    .GroupBy(item => item.GroupKey)
                    .OrderBy(group => ResolveGroupOrder(group.Key))
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
                        Title = ResolveLibraryGroupLabel(blockGroup.Key),
                        Subtitle = $"{blockGroup.Count()} selected",
                        LeadText = ResolveLibraryGroupSummary(blockGroup.Key),
                        Status = "Group",
                        StatusPill = blockGroup.Count().ToString(),
                        AccentColor = ResolveGroupAccent(blockGroup.Key),
                        PaletteKey = ResolveGroupPalette(blockGroup.Key),
                        IsCollapsible = true,
                        X = 1160,
                        Y = 410 + (sectionIndex * 210) + (groupIndex * 120),
                        ContextActions = PromptFactoryCanvasCatalog.BuildSelectionContextActions(libraryCatalog, "component-group").ToList()
                    });
                    links.Add(new CanvasWorkbenchLink { SourceId = sectionNodeId, TargetId = groupNodeId, Kind = "contains", IsUserAuthored = false });

                    var blockIndex = 0;
                    foreach (var block in blockGroup.OrderBy(item => item.OrderIndex).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        var customization = editor.ComponentCustomizations.LastOrDefault(item => item.BlockId == block.Id);
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
                                new CanvasWorkbenchChip { Text = ResolveLibraryGroupLabel(block.GroupKey), Tone = "accent" },
                                new CanvasWorkbenchChip { Text = block.TemplateTokens.Count > 0 ? "Specified" : "Static", Tone = customization is null ? "neutral" : "success" }
                            ],
                            ContextActions = PromptFactoryCanvasCatalog.BuildComponentNodeActions(block).ToList()
                        });
                        links.Add(new CanvasWorkbenchLink { SourceId = groupNodeId, TargetId = componentNodeId, Kind = "contains", IsUserAuthored = false });
                        blockIndex++;
                    }
                }
            }
        }

        AppendInputSelectionGraph(nodes, links);
        return (nodes, links);
    }

    private void AppendInputSelectionGraph(List<CanvasWorkbenchNode> nodes, List<CanvasWorkbenchLink> links)
    {
        if (editor.SessionAttachments.Count == 0)
        {
            return;
        }

        nodes.Add(new CanvasWorkbenchNode
        {
            Id = InputsRootCanvasNodeId,
            ParentId = "session-root",
            Family = "group",
            Kind = "inputs",
            Icon = "IN",
            Title = "Prompt inputs",
            Subtitle = $"{editor.SessionAttachments.Count} attached",
            LeadText = "Files, media, links, and notes explicitly attached to the prompt session.",
            Status = "Inputs",
            StatusPill = editor.SessionAttachments.Count.ToString(),
            AccentColor = "#059669",
            PaletteKey = "mint",
            IsCollapsible = true,
            X = 520,
            Y = 860,
            ContextActions = PromptFactoryCanvasCatalog.BuildSelectionContextActions(libraryCatalog, "inputs-root").ToList()
        });
        links.Add(new CanvasWorkbenchLink { SourceId = "session-root", TargetId = InputsRootCanvasNodeId, Kind = "selection", IsUserAuthored = false });

        for (var index = 0; index < editor.SessionAttachments.Count; index++)
        {
            var attachment = editor.SessionAttachments[index];
            var inputNodeId = $"{InputCanvasNodePrefix}{attachment.Id}";
            nodes.Add(new CanvasWorkbenchNode
            {
                Id = inputNodeId,
                ParentId = InputsRootCanvasNodeId,
                Family = "special",
                Kind = "input",
                Icon = "AT",
                Title = string.IsNullOrWhiteSpace(attachment.Title) ? $"Input {index + 1}" : attachment.Title,
                Subtitle = ResolveAttachmentSubtitle(attachment),
                LeadText = ResolveAttachmentLeadText(attachment),
                Status = attachment.Kind,
                StatusPill = attachment.Kind.ToUpperInvariant(),
                AccentColor = ResolveAttachmentAccent(attachment.Kind),
                PaletteKey = ResolveAttachmentPalette(attachment.Kind),
                X = 860,
                Y = 780 + (index * 124),
                MediaKind = attachment.Kind,
                MediaPreviewUrl = attachment.MediaRoute,
                MediaPreviewAlt = string.IsNullOrWhiteSpace(attachment.Title) ? attachment.Kind : attachment.Title,
                MediaContentType = attachment.MediaContentType,
                MediaFileName = attachment.MediaOriginalFileName,
                FooterChips =
                [
                    new CanvasWorkbenchChip { Text = string.IsNullOrWhiteSpace(attachment.MediaOriginalFileName) ? attachment.Kind : attachment.MediaOriginalFileName, Tone = "neutral" }
                ],
                ContextActions = PromptFactoryCanvasCatalog.BuildInputNodeActions(attachment.Id).ToList()
            });
            links.Add(new CanvasWorkbenchLink { SourceId = InputsRootCanvasNodeId, TargetId = inputNodeId, Kind = "contains", IsUserAuthored = false });
        }
    }

    private async Task<bool> HandleCatalogContextActionAsync(string actionId)
    {
        if (actionId.StartsWith("blueprint:set:", StringComparison.Ordinal))
        {
            await SelectBlueprintByKeyAsync(actionId["blueprint:set:".Length..]);
            return true;
        }

        if (actionId.StartsWith("flow:set:", StringComparison.Ordinal))
        {
            await SelectFlowByKeyAsync(actionId["flow:set:".Length..]);
            return true;
        }

        if (actionId.StartsWith("component:remove:", StringComparison.Ordinal))
        {
            await RemoveComponentByKeyAsync(actionId["component:remove:".Length..]);
            return true;
        }

        if (actionId.StartsWith("input:remove:", StringComparison.Ordinal))
        {
            await RemoveInputByIdAsync(actionId["input:remove:".Length..]);
            return true;
        }

        switch (actionId)
        {
            case "clear:components":
                editor.SelectedBlockIds.Clear();
                editor.ComponentCustomizations.Clear();
                editor.HasCustomizedBlocks = false;
                await FocusCanvasNodeAsync("session-root", "Prompt components cleared.");
                return true;
            case "clear:flow":
                editor.FlowTemplateId = null;
                await FocusCanvasNodeAsync("session-root", "Flow selection cleared.");
                return true;
            case "clear:blueprint":
                editor.BlueprintId = null;
                await FocusCanvasNodeAsync("session-root", "Blueprint selection cleared.");
                return true;
            case "clear:inputs":
                editor.SessionAttachments.Clear();
                await FocusCanvasNodeAsync("session-root", "Prompt inputs cleared.");
                return true;
            case "reset:session":
                editor.BlueprintId = null;
                editor.FlowTemplateId = null;
                editor.SelectedBlockIds.Clear();
                editor.ComponentCustomizations.Clear();
                editor.SessionAttachments.Clear();
                editor.HasCustomizedBlocks = false;
                await FocusCanvasNodeAsync("session-root", "Prompt session reset.");
                return true;
        }

        return false;
    }

    private async Task<bool> HandleCatalogCreateActionAsync(CanvasWorkbenchCreateActionRequest request)
    {
        if (request.ActionId.StartsWith("component:add:", StringComparison.Ordinal))
        {
            await AddComponentByKeyAsync(request.ActionId["component:add:".Length..], request.InputValues ?? []);
            return true;
        }

        if (request.ActionId.StartsWith("input:add:", StringComparison.Ordinal))
        {
            await AddInputAsync(request.ActionId["input:add:".Length..], request);
            return true;
        }

        return false;
    }

    private async Task AddComponentByKeyAsync(string blockKey, IReadOnlyList<CanvasWorkbenchInputValue> inputValues)
    {
        var block = blocks.FirstOrDefault(item => string.Equals(item.Key, blockKey, StringComparison.OrdinalIgnoreCase));
        if (block is null)
        {
            SetError([Error.Failure($"Prompt component '{blockKey}' could not be found.")]);
            return;
        }

        if (!editor.SelectedBlockIds.Contains(block.Id))
        {
            editor.SelectedBlockIds.Add(block.Id);
        }

        editor.HasCustomizedBlocks = true;
        var existingCustomization = editor.ComponentCustomizations.LastOrDefault(item => item.BlockId == block.Id);
        if (block.TemplateTokens.Count > 0)
        {
            var blockEditorModel = await PromptFactoryService.GetBlockEditorAsync(block.Id);
            var templateValues = inputValues
                .Where(item => !string.IsNullOrWhiteSpace(item.Key))
                .Select(item => new PromptTemplateValue
                {
                    Key = item.Key.Trim(),
                    Value = item.Value?.Trim() ?? string.Empty
                })
                .ToList();
            var renderedContent = RenderComponentContent(blockEditorModel.Content, templateValues);

            if (existingCustomization is null)
            {
                editor.ComponentCustomizations.Add(new PromptSessionComponentCustomization
                {
                    BlockId = block.Id,
                    BlockKey = block.Key,
                    Name = block.Name,
                    TemplateValues = templateValues,
                    RenderedContent = renderedContent
                });
            }
            else
            {
                existingCustomization.BlockKey = block.Key;
                existingCustomization.Name = block.Name;
                existingCustomization.TemplateValues = templateValues;
                existingCustomization.RenderedContent = renderedContent;
            }
        }

        await FocusCanvasNodeAsync($"{ComponentCanvasNodePrefix}{block.Key}", $"Component '{block.Name}' added to the session.");
    }

    private async Task RemoveComponentByKeyAsync(string blockKey)
    {
        var block = blocks.FirstOrDefault(item => string.Equals(item.Key, blockKey, StringComparison.OrdinalIgnoreCase));
        if (block is null)
        {
            return;
        }

        editor.SelectedBlockIds.Remove(block.Id);
        editor.ComponentCustomizations.RemoveAll(item => item.BlockId == block.Id);
        if (editor.SelectedBlockIds.Count == 0)
        {
            editor.HasCustomizedBlocks = false;
        }

        var nextSelection = editor.SelectedBlockIds.Count == 0 ? "session-root" : ComponentsRootCanvasNodeId;
        await FocusCanvasNodeAsync(nextSelection, $"Component '{block.Name}' removed.");
    }

    private async Task RemoveInputByIdAsync(string attachmentId)
    {
        var removed = editor.SessionAttachments.RemoveAll(item => string.Equals(item.Id, attachmentId, StringComparison.OrdinalIgnoreCase)) > 0;
        if (!removed)
        {
            return;
        }

        var nextSelection = editor.SessionAttachments.Count == 0 ? "session-root" : InputsRootCanvasNodeId;
        await FocusCanvasNodeAsync(nextSelection, "Prompt input removed.");
    }

    private async Task AddInputAsync(string inputKind, CanvasWorkbenchCreateActionRequest request)
    {
        var draft = new PromptSessionAttachmentSummary
        {
            Kind = inputKind,
            Title = request.Title?.Trim() ?? string.Empty,
            Subtitle = request.Subtitle?.Trim() ?? string.Empty,
            Notes = request.Notes?.Trim() ?? string.Empty,
            LinkUrl = string.Equals(inputKind, "link", StringComparison.OrdinalIgnoreCase)
                ? request.Subtitle?.Trim() ?? string.Empty
                : string.Empty
        };

        var prepared = await PromptFactoryService.PrepareAttachmentAsync(draft, request.UploadedFile);
        editor.SessionAttachments.Add(prepared);
        await FocusCanvasNodeAsync($"{InputCanvasNodePrefix}{prepared.Id}", $"Prompt input '{prepared.Title}' added.");
    }

    private async Task SelectBlueprintByKeyAsync(string blueprintKey)
    {
        var blueprint = blueprints.FirstOrDefault(item => string.Equals(item.Key, blueprintKey, StringComparison.OrdinalIgnoreCase));
        if (blueprint is null)
        {
            SetError([Error.Failure($"Blueprint '{blueprintKey}' could not be found.")]);
            return;
        }

        editor.BlueprintId = blueprint.Id;
        if (blueprint.RecommendedFlowTemplateId.HasValue)
        {
            editor.FlowTemplateId = blueprint.RecommendedFlowTemplateId.Value;
        }

        editor.SelectedBlockIds = (await PromptFactoryService.GetRecommendedBlockIdsAsync(editor.BlueprintId, editor.FlowTemplateId, editor.Phase)).ToList();
        editor.HasCustomizedBlocks = false;
        TrimComponentCustomizations();
        await FocusCanvasNodeAsync(BlueprintCanvasNodeId, $"Blueprint '{blueprint.Name}' selected.");
    }

    private async Task SelectFlowByKeyAsync(string flowKey)
    {
        var flow = templates.FirstOrDefault(item => string.Equals(item.Key, flowKey, StringComparison.OrdinalIgnoreCase));
        if (flow is null)
        {
            SetError([Error.Failure($"Flow '{flowKey}' could not be found.")]);
            return;
        }

        editor.FlowTemplateId = flow.Id;
        editor.SelectedBlockIds = flow.BlockIds.ToList();
        editor.HasCustomizedBlocks = false;
        TrimComponentCustomizations();
        await FocusCanvasNodeAsync(FlowCanvasNodeId, $"Flow '{flow.Name}' selected.");
    }

    private void TrimComponentCustomizations()
    {
        var selectedIds = editor.SelectedBlockIds.ToHashSet();
        editor.ComponentCustomizations = editor.ComponentCustomizations
            .Where(item => selectedIds.Contains(item.BlockId))
            .ToList();
    }

    private async Task FocusCanvasNodeAsync(string canvasNodeId, string successMessage)
    {
        selectedCanvasNodeIds = [canvasNodeId];
        editor.SelectedNodeId = TryParsePromptCanvasNodeId(canvasNodeId, out var promptNodeId) ? promptNodeId : null;
        SyncSelectedPromptNodeDraft();
        await PersistCanvasUiStateAsync();
        SetMessage(successMessage);
    }

    private static string RenderComponentContent(string content, IReadOnlyList<PromptTemplateValue> templateValues)
    {
        var rendered = content ?? string.Empty;
        foreach (var value in templateValues)
        {
            rendered = rendered.Replace($"{{{{{value.Key}}}}}", value.Value ?? string.Empty, StringComparison.Ordinal);
        }

        return rendered;
    }

    private static bool TryParseSelectedComponentNodeId(string? canvasNodeId, out string blockKey)
    {
        blockKey = string.Empty;
        if (string.IsNullOrWhiteSpace(canvasNodeId) ||
            !canvasNodeId.StartsWith(ComponentCanvasNodePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        blockKey = canvasNodeId[ComponentCanvasNodePrefix.Length..];
        return !string.IsNullOrWhiteSpace(blockKey);
    }

    private static bool TryParseSelectedInputNodeId(string? canvasNodeId, out string attachmentId)
    {
        attachmentId = string.Empty;
        if (string.IsNullOrWhiteSpace(canvasNodeId) ||
            !canvasNodeId.StartsWith(InputCanvasNodePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        attachmentId = canvasNodeId[InputCanvasNodePrefix.Length..];
        return !string.IsNullOrWhiteSpace(attachmentId);
    }

    private static bool TryParseComponentGroupNodeId(string? canvasNodeId, out string groupKey)
    {
        groupKey = string.Empty;
        if (string.IsNullOrWhiteSpace(canvasNodeId) ||
            !canvasNodeId.StartsWith(ComponentGroupCanvasNodePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        groupKey = canvasNodeId[ComponentGroupCanvasNodePrefix.Length..];
        return !string.IsNullOrWhiteSpace(groupKey);
    }

    private string ResolveComponentLeadText(PromptBlockSummary block, PromptSessionComponentCustomization? customization)
    {
        if (customization?.TemplateValues.Count > 0)
        {
            return string.Join(" | ", customization.TemplateValues.Select(item => $"{item.Key}: {item.Value}"));
        }

        return block.Summary;
    }

    private string ResolveAttachmentSubtitle(PromptSessionAttachmentSummary attachment)
    {
        return attachment.Kind switch
        {
            "link" => attachment.LinkUrl,
            "image" or "video" or "file" => string.IsNullOrWhiteSpace(attachment.MediaOriginalFileName)
                ? attachment.Kind
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
            return attachment.Subtitle;
        }

        return attachment.Kind;
    }

    private string ResolveLibraryGroupLabel(string groupKey)
        => libraryCatalog.Groups.FirstOrDefault(item => string.Equals(item.Key, groupKey, StringComparison.OrdinalIgnoreCase))?.Name
            ?? "Custom components";

    private string ResolveLibraryGroupSummary(string groupKey)
        => libraryCatalog.Groups.FirstOrDefault(item => string.Equals(item.Key, groupKey, StringComparison.OrdinalIgnoreCase))?.Summary
            ?? "Custom prompt components added in this workspace.";

    private int ResolveGroupOrder(string groupKey)
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

    private static string ResolveAttachmentAccent(string kind)
        => kind switch
        {
            "image" => "#be123c",
            "video" => "#7c3aed",
            "link" => "#0284c7",
            "note" => "#4b5563",
            _ => "#059669"
        };

    private static string ResolveAttachmentPalette(string kind)
        => kind switch
        {
            "image" => "danger",
            "video" => "violet",
            "link" => "sky",
            "note" => "neutral",
            _ => "mint"
        };
}
