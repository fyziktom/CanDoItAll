using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.OverlayLib;
using CanDoItAll.Modules.Factory.CanvasAdapters;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Globalization;

namespace CanDoItAll.Modules.Factory.Pages;

public partial class PromptFactoryPage
{
    private const string ComponentsToolboxWindowKey = "prompt-factory.components-toolbox";
    private const double LegacyComponentsToolboxWindowHeight = 620;
    private const double DefaultComponentsToolboxWindowHeight = 700;
    private const double ComponentPreviewPopoverPreferredWidth = 420;
    private const double ComponentPreviewPopoverPreferredHeight = 360;
    private const double ComponentPreviewViewportMargin = 24;
    private const double ComponentPreviewAnchorGap = 18;
    private readonly PromptFactoryCreateActionDeduplicator createActionDeduplicator = new();
    private CanvasWorkbench? workbenchRef;
    private string componentToolboxSearchText = string.Empty;
    private ComponentPreviewPopoverState? componentPreviewPopover;
    private int componentPreviewVersion;

    private CanvasWorkbenchWindowState ComponentsToolboxWindowState
        => UpgradeLegacyDefaultHeight(
            ResolveCanvasWindowState(ComponentsToolboxWindowKey, true),
            LegacyComponentsToolboxWindowHeight,
            DefaultComponentsToolboxWindowHeight);

    private IReadOnlyList<OverlayToolboxSection> ComponentToolboxSections
        => BuildComponentToolboxSections();

    private bool HasComponentPreview
        => componentPreviewPopover is not null;

    private PromptBlockSummary? PreviewedComponentBlock
        => componentPreviewPopover?.Block;

    private string PreviewedComponentContent
        => PreviewedComponentBlock is null
            ? string.Empty
            : ResolveComponentBaseContent(PreviewedComponentBlock, null);

    private string PreviewedComponentSummary
        => PreviewedComponentBlock is null
            ? string.Empty
            : string.IsNullOrWhiteSpace(PreviewedComponentBlock.ContentPreview)
                ? PreviewedComponentBlock.Summary
                : PreviewedComponentBlock.ContentPreview;

    private string ComponentPreviewPopoverStyle
        => componentPreviewPopover is null
            ? string.Empty
            : FormattableString.Invariant($"left:{componentPreviewPopover.Left}px;top:{componentPreviewPopover.Top}px;width:{componentPreviewPopover.Width}px;");

    private CanvasWorkbenchWindowState ResolveCanvasWindowState(string key, bool isVisibleByDefault = false)
    {
        var uiState = CanvasWorkbenchUiState.Parse(editor.CanvasUiStateJson);
        if (uiState.WindowStates.TryGetValue(key, out var state))
        {
            return CanvasWorkbenchWindowState.Normalize(state);
        }

        return CanvasWorkbenchWindowState.Normalize(new CanvasWorkbenchWindowState
        {
            IsVisible = isVisibleByDefault
        });
    }

    private Task HandleComponentsToolboxWindowStateChangedAsync(CanvasWorkbenchWindowState state)
        => PersistCanvasWindowStateAsync(ComponentsToolboxWindowKey, state);

    private async Task ToggleCanvasWindowAsync(string key)
    {
        var state = ResolveCanvasWindowState(key);
        if (!state.IsVisible)
        {
            state.IsVisible = true;
            state.IsMinimized = false;
        }
        else if (state.IsMinimized)
        {
            state.IsMinimized = false;
        }
        else
        {
            state.IsVisible = false;
            state.IsMinimized = false;
        }

        await PersistCanvasWindowStateAsync(key, state);
    }

    private async Task PersistCanvasWindowStateAsync(string key, CanvasWorkbenchWindowState state)
    {
        var uiState = CanvasWorkbenchUiState.Parse(editor.CanvasUiStateJson);
        uiState.WindowStates[key] = CanvasWorkbenchWindowState.Normalize(state);
        editor.CanvasUiStateJson = uiState.ToJson();
        if (!state.IsVisible)
        {
            componentPreviewPopover = null;
        }

        if (editor.SessionId.HasValue)
        {
            await PromptFactoryService.SaveCanvasUiStateAsync(editor.SessionId.Value, editor.CanvasUiStateJson, editor.SelectedNodeId);
        }

        RefreshCanvasSurface();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OpenComponentsToolboxAsync(string? previewBlockKey = null)
    {
        if (!string.IsNullOrWhiteSpace(previewBlockKey))
        {
            var previewBlock = blocks.FirstOrDefault(item => string.Equals(item.Key, previewBlockKey, StringComparison.OrdinalIgnoreCase));
            componentPreviewPopover = previewBlock is null
                ? null
                : await BuildComponentPreviewPopoverAsync(previewBlock, null, null);
        }

        var state = ResolveCanvasWindowState(ComponentsToolboxWindowKey, true);
        state.IsVisible = true;
        state.IsMinimized = false;
        await PersistCanvasWindowStateAsync(ComponentsToolboxWindowKey, state);
    }

    private async Task ShowComponentPreviewAsync(PromptBlockSummary block, MouseEventArgs args)
    {
        componentPreviewVersion += 1;
        componentPreviewPopover = await BuildComponentPreviewPopoverAsync(block, args.ClientX, args.ClientY);
    }

    private async Task ShowComponentPreviewAsync(PromptBlockSummary block, FocusEventArgs _)
    {
        componentPreviewVersion += 1;
        componentPreviewPopover = await BuildComponentPreviewPopoverAsync(block, null, null);
    }

    private Task HoldComponentPreviewAsync()
    {
        componentPreviewVersion += 1;
        return Task.CompletedTask;
    }

    private async Task HideComponentPreviewAsync()
    {
        var previewVersion = ++componentPreviewVersion;
        await Task.Delay(140);
        if (previewVersion != componentPreviewVersion || componentPreviewPopover is null)
        {
            return;
        }

        componentPreviewPopover = null;
        await InvokeAsync(StateHasChanged);
    }

    private async Task AddComponentFromToolboxAsync(PromptBlockSummary block)
    {
        componentPreviewVersion += 1;
        componentPreviewPopover = await BuildComponentPreviewPopoverAsync(block, null, null);
        if (block.TemplateTokens.Count == 0)
        {
            await AddComponentByKeyAsync(block.Key, []);
            return;
        }

        if (workbenchRef is null)
        {
            return;
        }

        var action = PromptFactoryCatalogToolbox.BuildComponentCreateAction(block);
        var request = new CanvasWorkbenchCreateActionRequest(
            action.ActionId,
            selectedCanvasNodeId ?? "session-root",
            0,
            0,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            "child",
            action.CreateMode,
            action.ObjectSubtype,
            null);

        await workbenchRef.OpenCreateDialogAsync(action, request);
    }

    private IReadOnlyList<OverlayToolboxSection> BuildComponentToolboxSections()
    {
        var eligibleBlocks = blocks
            .Where(MatchesComponentToolboxSearch)
            .OrderBy(item => ResolveComponentSectionOrder(item.GroupKey))
            .ThenBy(item => ResolveGroupOrder(item.GroupKey))
            .ThenBy(item => item.OrderIndex)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return eligibleBlocks
            .GroupBy(item => ResolveComponentSectionKey(item.GroupKey))
            .OrderBy(group => ResolveComponentSectionOrder(group.Key))
            .Select(section => new OverlayToolboxSection(
                section.Key,
                ResolveComponentSectionLabel(section.Key),
                ResolveComponentSectionDescription(section.Key),
                section
                    .GroupBy(item => item.GroupKey)
                    .OrderBy(group => ResolveGroupOrder(group.Key))
                    .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(group => new OverlayToolboxGroup(
                        group.Key,
                        ResolveLibraryGroupLabel(group.Key),
                        ResolveLibraryGroupSummary(group.Key),
                        group.Select(block => new OverlayToolboxItem(
                            block.Key,
                            block.Name,
                            block.Summary,
                            Glyph: ResolveToolboxBlockGlyph(block),
                            DataTestId: $"prompt-factory-component-{block.Key}")).ToList(),
                        IsExpanded: true))
                    .ToList()))
            .ToList();
    }

    private static CanvasWorkbenchWindowState UpgradeLegacyDefaultHeight(
        CanvasWorkbenchWindowState state,
        double legacyDefaultHeight,
        double defaultHeight)
    {
        var normalized = CanvasWorkbenchWindowState.Normalize(state);
        if (normalized.IsMinimized ||
            !normalized.Height.HasValue ||
            normalized.Height.Value > legacyDefaultHeight + 0.5)
        {
            return normalized;
        }

        var upgraded = normalized.Clone();
        upgraded.Height = defaultHeight;
        return CanvasWorkbenchWindowState.Normalize(upgraded);
    }

    private bool MatchesComponentToolboxSearch(PromptBlockSummary block)
    {
        if (string.IsNullOrWhiteSpace(componentToolboxSearchText))
        {
            return true;
        }

        var search = componentToolboxSearchText.Trim();
        return block.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               block.Summary.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               block.ContentPreview.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               ResolveLibraryGroupLabel(block.GroupKey).Contains(search, StringComparison.OrdinalIgnoreCase) ||
               block.Tags.Any(tag => tag.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
               block.StackTags.Any(tag => tag.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveToolboxBlockGlyph(PromptBlockSummary block)
    {
        var icon = PromptFactoryCatalogToolbox.BuildComponentCreateAction(block).Icon;
        if (!string.IsNullOrWhiteSpace(icon))
        {
            return icon.Trim()[0].ToString().ToUpperInvariant();
        }

        return string.IsNullOrWhiteSpace(block.Name)
            ? "+"
            : block.Name.Trim()[0].ToString().ToUpperInvariant();
    }

    private async Task<ComponentPreviewPopoverState> BuildComponentPreviewPopoverAsync(PromptBlockSummary block, double? clientX, double? clientY)
    {
        var viewport = await JS.InvokeAsync<ComponentPreviewViewportSnapshot>("CanDoItAll.canvasWorkbench.getViewportSnapshot");
        var viewportWidth = viewport.Width > 0 ? viewport.Width : 1440;
        var viewportHeight = viewport.Height > 0 ? viewport.Height : 900;
        var popoverWidth = Math.Min(ComponentPreviewPopoverPreferredWidth, Math.Max(240, viewportWidth - (ComponentPreviewViewportMargin * 2)));
        var popoverHeight = Math.Min(ComponentPreviewPopoverPreferredHeight, Math.Max(240, viewportHeight - (ComponentPreviewViewportMargin * 2)));
        var anchorX = clientX.GetValueOrDefault(Math.Min(viewportWidth * 0.34, viewportWidth - ComponentPreviewViewportMargin - popoverWidth - ComponentPreviewAnchorGap));
        var anchorY = clientY.GetValueOrDefault(Math.Min(208, viewportHeight * 0.3));
        var canPlaceRight = anchorX + ComponentPreviewAnchorGap + popoverWidth <= viewportWidth - ComponentPreviewViewportMargin;
        var canPlaceLeft = anchorX - ComponentPreviewAnchorGap - popoverWidth >= ComponentPreviewViewportMargin;

        double left;
        string placement;
        if (canPlaceRight || !canPlaceLeft)
        {
            placement = canPlaceRight ? "right" : "overlay";
            left = canPlaceRight
                ? Math.Min(viewportWidth - ComponentPreviewViewportMargin - popoverWidth, anchorX + ComponentPreviewAnchorGap)
                : ClampPopover(anchorX - (popoverWidth / 2), ComponentPreviewViewportMargin, viewportWidth - ComponentPreviewViewportMargin - popoverWidth);
        }
        else
        {
            placement = "left";
            left = Math.Max(ComponentPreviewViewportMargin, anchorX - ComponentPreviewAnchorGap - popoverWidth);
        }

        var top = ClampPopover(anchorY - 28, ComponentPreviewViewportMargin, viewportHeight - ComponentPreviewViewportMargin - popoverHeight);
        return new ComponentPreviewPopoverState(block, left, top, popoverWidth, placement);
    }

    private static double ClampPopover(double value, double min, double max)
        => min > max
            ? min
            : Math.Clamp(value, min, max);

    private Task HandleComponentToolboxSearchChangedAsync(string? value)
    {
        componentToolboxSearchText = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleComponentToolboxItemSelectedAsync(string blockKey)
    {
        var block = ResolveToolboxBlock(blockKey);
        return block is null
            ? Task.CompletedTask
            : AddComponentFromToolboxAsync(block);
    }

    private PromptBlockSummary? ResolveToolboxBlock(string blockKey)
        => blocks.FirstOrDefault(item => string.Equals(item.Key, blockKey, StringComparison.OrdinalIgnoreCase));

    private readonly record struct ComponentPreviewViewportSnapshot(double Width, double Height);

    private sealed record ComponentPreviewPopoverState(
        PromptBlockSummary Block,
        double Left,
        double Top,
        double Width,
        string Placement);
}
