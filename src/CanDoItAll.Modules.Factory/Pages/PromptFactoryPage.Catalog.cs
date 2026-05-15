using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Factory.CanvasAdapters;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Factory.Pages;

public partial class PromptFactoryPage
{
    private const string BlueprintCanvasNodeId = "selection:blueprint";
    private const string FlowCanvasNodeId = "selection:flow";
    private const string ComponentsRootCanvasNodeId = "selection:components";
    private const string InputsRootCanvasNodeId = "selection:inputs";
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

    private PromptSessionComponentCustomization? selectedComponentCustomization
        => selectedComponentNode is null
            ? null
            : editor.ComponentCustomizations.LastOrDefault(item => item.BlockId == selectedComponentNode.Id);

    private string SelectedComponentRenderedContent
    {
        get => selectedComponentNode is null
            ? string.Empty
            : ResolveComponentRenderedContent(selectedComponentNode, selectedComponentCustomization);
        set => UpdateSelectedComponentRenderedContent(value);
    }

    private string SelectedComponentBaseContent
        => selectedComponentNode is null
            ? string.Empty
            : ResolveComponentBaseContent(selectedComponentNode, selectedComponentCustomization);

    private bool SelectedComponentIsModified
        => selectedComponentNode is not null &&
           !string.Equals(SelectedComponentRenderedContent, SelectedComponentBaseContent, StringComparison.Ordinal);

    private PromptSessionAttachmentSummary? selectedAttachmentNode
        => TryParseSelectedInputNodeId(selectedCanvasNodeId, out var attachmentId)
            ? VisibleSessionAttachments.FirstOrDefault(item => string.Equals(item.Id, attachmentId, StringComparison.OrdinalIgnoreCase))
            : null;

    private PromptSessionAttachmentSummary? selectedSetupNode
        => string.Equals(selectedCanvasNodeId, SetupCanvasNodeId, StringComparison.Ordinal)
            ? SetupAttachment
            : null;

    private PromptLibraryGroupSummary? selectedComponentGroupNode
        => TryParseComponentGroupNodeId(selectedCanvasNodeId, out var groupKey)
            ? libraryCatalog.Groups.FirstOrDefault(item => string.Equals(item.Key, groupKey, StringComparison.OrdinalIgnoreCase))
            : null;

    private async Task<bool> HandleCatalogContextActionAsync(string actionId)
    {
        if (string.Equals(actionId, "catalog-components", StringComparison.Ordinal))
        {
            await OpenComponentsToolboxAsync();
            return true;
        }

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
                if (editor.SelectedBlockIds.Count > 0)
                {
                    OpenImpactDialog(
                        "Clear prompt components?",
                        $"This will remove {editor.SelectedBlockIds.Count} selected component(s) from the session.",
                        "Clear components",
                        ClearComponentsAsync);
                    return true;
                }

                await ClearComponentsAsync();
                return true;
            case "clear:flow":
                RememberHistoryCheckpoint();
                editor.FlowTemplateId = null;
                await FocusCanvasNodeAsync("session-root", "Flow selection cleared.");
                return true;
            case "clear:blueprint":
                RememberHistoryCheckpoint();
                editor.BlueprintId = null;
                await FocusCanvasNodeAsync("session-root", "Blueprint selection cleared.");
                return true;
            case "clear:inputs":
                if (VisibleSessionAttachments.Count > 0)
                {
                    OpenImpactDialog(
                        "Clear prompt inputs?",
                        $"This will remove {VisibleSessionAttachments.Count} attached prompt input(s) from the current session.",
                        "Clear inputs",
                        ClearInputsAsync);
                    return true;
                }

                await ClearInputsAsync();
                return true;
            case "reset:session":
                if (editor.BlueprintId.HasValue ||
                    editor.FlowTemplateId.HasValue ||
                    editor.SelectedBlockIds.Count > 0 ||
                    VisibleSessionAttachments.Count > 0 ||
                    editor.Nodes.Count > 0)
                {
                    OpenImpactDialog(
                        "Reset prompt session?",
                        "This will clear the blueprint, flow, selected components, inputs, and prompt steps. Setup will be preserved only from known project context.",
                        "Reset session",
                        ResetSessionAsync);
                    return true;
                }

                await ResetSessionAsync();
                return true;
            case "setup:open":
                await SelectSetupNodeAsync(openSetupTab: true);
                return true;
        }

        return false;
    }

    private async Task<bool> HandleCatalogCreateActionAsync(CanvasWorkbenchCreateActionRequest request)
    {
        if (!createActionDeduplicator.ShouldProcess(request, DateTimeOffset.UtcNow))
        {
            return true;
        }

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

        RememberHistoryCheckpoint();
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

        RememberHistoryCheckpoint();
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
        RememberHistoryCheckpoint();
        var removed = editor.SessionAttachments.RemoveAll(item => string.Equals(item.Id, attachmentId, StringComparison.OrdinalIgnoreCase)) > 0;
        if (!removed)
        {
            return;
        }

        var nextSelection = VisibleSessionAttachments.Count == 0 ? "session-root" : InputsRootCanvasNodeId;
        await FocusCanvasNodeAsync(nextSelection, "Prompt input removed.");
    }

    private async Task AddInputAsync(string inputKind, CanvasWorkbenchCreateActionRequest request)
    {
        RememberHistoryCheckpoint();
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

    private async Task ClearComponentsAsync()
    {
        RememberHistoryCheckpoint();
        editor.SelectedBlockIds.Clear();
        editor.ComponentCustomizations.Clear();
        editor.HasCustomizedBlocks = false;
        await FocusCanvasNodeAsync("session-root", "Prompt components cleared.");
    }

    private async Task ClearInputsAsync()
    {
        RememberHistoryCheckpoint();
        editor.SessionAttachments.RemoveAll(item => !IsSetupAttachment(item));
        await FocusCanvasNodeAsync("session-root", "Prompt inputs cleared.");
    }

    private async Task ResetSessionAsync()
    {
        RememberHistoryCheckpoint();
        editor.BlueprintId = null;
        editor.FlowTemplateId = null;
        editor.SelectedBlockIds.Clear();
        editor.ComponentCustomizations.Clear();
        editor.SessionAttachments.Clear();
        editor.HasCustomizedBlocks = false;
        sessionSetup = new PromptSessionSetupProfile();
        PrefillSetupProfile();
        UpsertSetupAttachment();
        supportLaneTab = SupportLaneTabCanvas;
        await FocusCanvasNodeAsync(SetupCanvasNodeId, "Prompt session reset. Complete session setup to continue.");
    }

    private async Task SelectBlueprintByKeyAsync(string blueprintKey)
    {
        var blueprint = blueprints.FirstOrDefault(item => string.Equals(item.Key, blueprintKey, StringComparison.OrdinalIgnoreCase));
        if (blueprint is null)
        {
            SetError([Error.Failure($"Blueprint '{blueprintKey}' could not be found.")]);
            return;
        }

        var targetFlowTemplateId = blueprint.RecommendedFlowTemplateId ?? editor.FlowTemplateId;
        var recommendedBlockIds = (await PromptFactoryService.GetRecommendedBlockIdsAsync(blueprint.Id, targetFlowTemplateId, editor.Phase)).ToList();
        if (TryBuildSelectionImpactMessage(recommendedBlockIds, $"Selecting blueprint '{blueprint.Name}'", out var dialogBody))
        {
            OpenImpactDialog(
                $"Use blueprint '{blueprint.Name}'?",
                dialogBody,
                "Use blueprint",
                () => ApplyBlueprintSelectionAsync(blueprint, recommendedBlockIds));
            return;
        }

        await ApplyBlueprintSelectionAsync(blueprint, recommendedBlockIds);
    }

    private async Task SelectFlowByKeyAsync(string flowKey)
    {
        var flow = templates.FirstOrDefault(item => string.Equals(item.Key, flowKey, StringComparison.OrdinalIgnoreCase));
        if (flow is null)
        {
            SetError([Error.Failure($"Flow '{flowKey}' could not be found.")]);
            return;
        }

        var recommendedBlockIds = (await PromptFactoryService.GetRecommendedBlockIdsAsync(editor.BlueprintId, flow.Id, editor.Phase)).ToList();
        if (TryBuildSelectionImpactMessage(recommendedBlockIds, $"Selecting flow '{flow.Name}'", out var dialogBody))
        {
            OpenImpactDialog(
                $"Use flow '{flow.Name}'?",
                dialogBody,
                "Use flow",
                () => ApplyFlowSelectionAsync(flow, recommendedBlockIds));
            return;
        }

        await ApplyFlowSelectionAsync(flow, recommendedBlockIds);
    }

    private async Task ApplyBlueprintSelectionAsync(PromptBlueprintSummary blueprint, IReadOnlyList<Guid> recommendedBlockIds)
    {
        RememberHistoryCheckpoint();
        editor.BlueprintId = blueprint.Id;
        if (blueprint.RecommendedFlowTemplateId.HasValue)
        {
            editor.FlowTemplateId = blueprint.RecommendedFlowTemplateId.Value;
        }

        editor.SelectedBlockIds = recommendedBlockIds.ToList();
        editor.HasCustomizedBlocks = false;
        TrimComponentCustomizations();
        await FocusCanvasNodeAsync(BlueprintCanvasNodeId, $"Blueprint '{blueprint.Name}' selected.");
    }

    private async Task ApplyFlowSelectionAsync(PromptFlowTemplateSummary flow, IReadOnlyList<Guid> recommendedBlockIds)
    {
        RememberHistoryCheckpoint();
        editor.FlowTemplateId = flow.Id;
        editor.SelectedBlockIds = recommendedBlockIds.ToList();
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
        editor.SelectedNodeId = PromptFactoryPageHelpers.TryParsePromptCanvasNodeId(canvasNodeId, out var promptNodeId) ? promptNodeId : null;
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

    private void UpdateSelectedComponentRenderedContent(string? value)
    {
        if (selectedComponentNode is null)
        {
            return;
        }

        var nextValue = value ?? string.Empty;
        var currentValue = ResolveComponentRenderedContent(selectedComponentNode, selectedComponentCustomization);
        if (string.Equals(currentValue, nextValue, StringComparison.Ordinal))
        {
            return;
        }

        RememberHistoryCheckpoint();
        UpsertSelectedComponentCustomization(selectedComponentNode, nextValue);
        editor.HasCustomizedBlocks = editor.ComponentCustomizations.Count > 0 || editor.SelectedBlockIds.Count > 0;
    }

    private void ResetSelectedComponentRenderedContent()
    {
        if (selectedComponentNode is null)
        {
            return;
        }

        var baseContent = ResolveComponentBaseContent(selectedComponentNode, selectedComponentCustomization);
        UpdateSelectedComponentRenderedContent(baseContent);
        SetMessage("Component content reset to the current template baseline.");
    }

    private void UpsertSelectedComponentCustomization(PromptBlockSummary block, string renderedContent)
    {
        var customization = editor.ComponentCustomizations.LastOrDefault(item => item.BlockId == block.Id);
        var baseContent = ResolveComponentBaseContent(block, customization);
        if (string.Equals(renderedContent, baseContent, StringComparison.Ordinal))
        {
            if (customization is null)
            {
                return;
            }

            if (customization.TemplateValues.Count > 0)
            {
                customization.BlockKey = block.Key;
                customization.Name = block.Name;
                customization.RenderedContent = baseContent;
            }
            else
            {
                editor.ComponentCustomizations.Remove(customization);
            }

            return;
        }

        if (customization is null)
        {
            editor.ComponentCustomizations.Add(new PromptSessionComponentCustomization
            {
                BlockId = block.Id,
                BlockKey = block.Key,
                Name = block.Name,
                RenderedContent = renderedContent
            });
            return;
        }

        customization.BlockKey = block.Key;
        customization.Name = block.Name;
        customization.RenderedContent = renderedContent;
    }

    private static string ResolveComponentBaseContent(PromptBlockSummary block, PromptSessionComponentCustomization? customization)
    {
        if (customization?.TemplateValues.Count > 0)
        {
            return RenderComponentContent(block.Content, customization.TemplateValues);
        }

        return block.Content ?? string.Empty;
    }

    private static string ResolveComponentRenderedContent(PromptBlockSummary block, PromptSessionComponentCustomization? customization)
    {
        if (!string.IsNullOrWhiteSpace(customization?.RenderedContent))
        {
            return customization.RenderedContent;
        }

        return ResolveComponentBaseContent(block, customization);
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

}


