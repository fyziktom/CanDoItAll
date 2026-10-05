using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Workbench.Insights.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage {
    [Inject] private ProjectWriteAdmissionService InsightsAdmissions { get; set; } = default!;
    [CascadingParameter] public Task<AuthenticationState>? InsightsAuthentication { get; set; }
    private long insightsSelectionRevision;
    private InsightsContext? insightsContext;
    private InsightsMenu? insightsMenu;
    private readonly Queue<InsightsEffect> insightsEffects = new();
    private readonly Queue<(ProjectWriteAdmission Admission, string NodeId, ProjectStructureCommandKind Command, ArtifactReference? Artifact)> committedInsightCommands = new();

    private sealed record InsightsContext(InsightsOrigin Origin, ProjectStructureActionContext Native,
        Task<AuthenticationState>? Actor, ProjectStructureSelectionPanelState Selection,
        IReadOnlyList<ProjectStructureNode> Nodes, IReadOnlyList<ProjectStructureSignalSection> Signals);
    private sealed record InsightsEffect(InsightsOrigin Origin, string Operation, IReadOnlyList<string> Targets,
        IReadOnlyList<string>? ConfirmedIds, string Message);

    private InsightsContext CurrentInsights {
        get {
            var selection = BuildSelectionPanelState();
            if (insightsContext is { } current && ReferenceEquals(current.Native.Surface, surface) &&
                IsCurrentAction(current.Native) && current.Origin.SelectionRevision == insightsSelectionRevision &&
                ReferenceEquals(current.Actor, InsightsAuthentication) && current.Selection.RenderKey == selection.RenderKey) {
                return current;
            }
            var native = CaptureActionContext();
            insightsMenu = null;
            return insightsContext = new(new(native.Admission.ProjectId, native.Admission.DatabaseProfileId,
                native.Admission.LifetimeId, Guid.NewGuid(), insightsSelectionRevision), native, InsightsAuthentication,
                selection, selectedNodes.ToArray(), BuildSignalsWindowSections());
        }
    }

    private bool IsCurrentInsights(InsightsContext context) => IsCurrentAction(context.Native) &&
        ReferenceEquals(context.Actor, InsightsAuthentication) && insightsSelectionRevision == context.Origin.SelectionRevision;

    private async Task<InsightsContext?> AdmitInsightsAsync(InsightsOrigin origin) {
        if (surface is null || deferredCompletionCts.IsCancellationRequested || CurrentInsights is not { } context || context.Origin != origin) {
            return null;
        }
        try {
            await InsightsAdmissions.RequireCurrentAsync(context.Native.Admission, deferredCompletionCts.Token);
            return IsCurrentInsights(context) && CurrentInsights.Origin == origin ? context : null;
        } catch (Exception exception) {
            RecordInsightsEffect(context, "Admission", [], null, exception.Message, exception);
            return null;
        }
    }

    private void RecordInsightsEffect(InsightsContext context, string operation, IReadOnlyList<string> targets,
        IReadOnlyList<string>? confirmed, string message, Exception? exception = null) {
        insightsEffects.Enqueue(new(context.Origin, operation, targets.ToArray(), confirmed?.ToArray(), message));
        while (insightsEffects.Count > 16) {
            insightsEffects.Dequeue();
        }
        if (exception is not null) {
            Logger.LogWarning(exception, "Insights {Operation} for original project {ProjectId}, lifetime {LifetimeId}, targets {NodeIds}: {Outcome}.",
                operation, context.Origin.ProjectId, context.Origin.LifetimeId, targets, message);
        }
        if (IsCurrentInsights(context)) {
            workflowFeedback = message;
            workflowFeedbackTone = exception is null ? "mint" : "warn";
        }
    }

    private async Task ApplyInsightsNodesAsync(InsightsContext context, string operation, IReadOnlyList<string> targets,
        Func<Task<IReadOnlyList<ProjectStructureNode>>> write) {
        IReadOnlyList<ProjectStructureNode> changed;
        try {
            changed = await write();
        } catch (Exception exception) {
            RecordInsightsEffect(context, operation, targets, null,
                "No write result was confirmed. Inspect the original project's state before repeating the action.", exception);
            return;
        }
        var confirmed = changed.Select(node => node.Id).ToArray();
        RecordInsightsEffect(context, operation, targets, confirmed, $"Updated {confirmed.Length} original node(s).");
        if (!IsCurrentAction(context.Native) || !ReferenceEquals(context.Actor, InsightsAuthentication)) {
            return;
        }
        try {
            await ApplySurfaceNodeUpdatesAsync(changed);
        } catch (Exception exception) {
            RecordInsightsEffect(context, operation, targets, confirmed,
                $"Updated {confirmed.Length} original node(s), but the view could not refresh. Reload the view; do not repeat the write.", exception);
        }
    }

    private async Task HandleInsightsSelectionAsync(InsightsSelectionIntent intent) {
        var context = await AdmitInsightsAsync(intent.Origin);
        if (context is null || !intent.NodeIds.SequenceEqual(context.Nodes.Select(node => node.Id), StringComparer.Ordinal)) {
            return;
        }
        var ids = intent.NodeIds.ToArray();
        var admission = context.Native.Admission;
        switch (intent.Command) {
            case InsightsSelectionCommand.Clear:
                await SelectInsightsAsync(context, []);
                break;
            case InsightsSelectionCommand.Focus focus when ids.Contains(focus.NodeId, StringComparer.Ordinal):
                await FocusInsightsAsync(context, focus.NodeId);
                break;
            case InsightsSelectionCommand.Status status when context.Selection.CanApplySelectionStatus:
                var value = status.Value switch {
                    InsightsStatus.InProgress => "In progress",
                    InsightsStatus.Done => "Done",
                    InsightsStatus.NotApplicable => "N/A",
                    _ => throw new ArgumentOutOfRangeException(nameof(intent))
                };
                await ApplyInsightsNodesAsync(context, "Status", ids, () => ProjectWorkbenchService.UpdateObjectStatusesDetailedAsync(
                    admission.ProjectId, ids, value, expectedProjectAdmission: admission));
                break;
            case InsightsSelectionCommand.Progress progress:
                await ApplyInsightsNodesAsync(context, "Progress", ids, () => ProjectWorkbenchService.UpdateObjectProgressDetailedAsync(
                    admission.ProjectId, ids, "progress", progress.Percent, expectedProjectAdmission: admission));
                break;
            case InsightsSelectionCommand.Priority priority:
                await ApplyInsightsNodesAsync(context, "Priority", ids, () => ProjectWorkbenchService.UpdateObjectPriorityDetailedAsync(
                    admission.ProjectId, ids, priority.Value, expectedProjectAdmission: admission));
                break;
            case InsightsSelectionCommand.Marker marker:
                await ApplyInsightsMarkerAsync(context, ids, marker.Value, toggle: false);
                break;
            case InsightsSelectionCommand.BorderName name:
                selectionBorderName = name.Value;
                break;
            case InsightsSelectionCommand.CreateBorder border when ids.Length >= 2:
                var framed = ResolveEditableUiState();
                if (graphAdapter.TryAddSelectionFrame(context.Native.Surface.Nodes, framed, ids,
                    string.IsNullOrWhiteSpace(border.Name) ? $"{ids.Length} nodes" : border.Name.Trim())) {
                    await PersistInsightsStateAsync(context, framed, "Create border");
                }
                break;
            case InsightsSelectionCommand.ClearBorders:
                var cleared = ResolveEditableUiState();
                if (graphAdapter.TryClearSelectionFrames(context.Native.Surface.Nodes, cleared, ids)) {
                    await PersistInsightsStateAsync(context, cleared, "Clear borders");
                }
                break;
            case InsightsSelectionCommand.Inspector action when context.Nodes.Count == 1 &&
                context.Selection.SelectedNode!.Actions.Any(offered => offered.ActionId == action.ActionId):
                await ExecuteInspectorActionAsync(context.Nodes[0], action.ActionId, context.Native);
                break;
            case InsightsSelectionCommand.Open open when context.Nodes.Count == 1:
                var node = context.Nodes[0];
                var detail = context.Selection.SelectedNode!;
                switch (open.View) {
                    case InsightsNativeView.Toolbox:
                        var toolbox = ResolveEditableUiState();
                        var window = ResolveToolboxWindowState().Clone();
                        window.IsVisible = true;
                        window.IsMinimized = false;
                        toolbox.WindowStates[ToolboxWindowKey] = window;
                        await PersistInsightsStateAsync(context, toolbox, "Open toolbox");
                        break;
                    case InsightsNativeView.AttachmentLocal when detail.AttachmentPreview?.CanShowLocalOpen == true:
                        await OpenAttachmentLocallyAsync(node);
                        break;
                    case InsightsNativeView.AttachmentPreview when detail.AttachmentPreview?.CanRenderPreview == true:
                        await OpenAttachmentPreviewAsync(node);
                        break;
                    case InsightsNativeView.Mermaid when detail.HasMermaidViewer:
                        await OpenMermaidViewerAsync(node);
                        break;
                }
                break;
        }
    }

    private async Task HandleInsightsSignalAsync(InsightsSignalIntent intent) {
        var context = await AdmitInsightsAsync(intent.Origin);
        if (context is null || !intent.NodeIds.SequenceEqual(context.Nodes.Select(node => node.Id), StringComparer.Ordinal) ||
            !context.Signals.SelectMany(section => section.Actions).Any(action => action.ActionId == intent.ActionId)) {
            return;
        }
        var ids = intent.NodeIds.ToArray();
        var admission = context.Native.Admission;
        if (actionCatalog.TryResolveMarkerAction(intent.ActionId, out var icon, out var tone, out var label)) {
            await ApplyInsightsMarkerAsync(context, ids, new(icon, tone, label), toggle: true);
        } else if (actionCatalog.TryResolveProgressAction(intent.ActionId, out var mode, out var percent)) {
            await ApplyInsightsNodesAsync(context, "Progress", ids, () => ProjectWorkbenchService.UpdateObjectProgressDetailedAsync(
                admission.ProjectId, ids, mode, percent, expectedProjectAdmission: admission));
        } else if (actionCatalog.TryResolvePriorityAction(intent.ActionId, out var priority)) {
            await ApplyInsightsNodesAsync(context, "Priority", ids, () => ProjectWorkbenchService.UpdateObjectPriorityDetailedAsync(
                admission.ProjectId, ids, priority, expectedProjectAdmission: admission));
        }
    }

    private Task ApplyInsightsMarkerAsync(InsightsContext context, string[] ids, ProjectStructureSelectionMarkerRequest marker, bool toggle) {
        var admission = context.Native.Admission;
        var remove = toggle && context.Nodes.All(node => node.Markers.Any(value => value.Icon.Equals(marker.Badge, StringComparison.OrdinalIgnoreCase)));
        return ApplyInsightsNodesAsync(context, "Marker", ids, () => string.IsNullOrWhiteSpace(marker.Badge)
            ? ProjectWorkbenchService.ClearObjectMarkersDetailedAsync(admission.ProjectId, ids, expectedProjectAdmission: admission)
            : remove ? ProjectWorkbenchService.RemoveObjectMarkerDetailedAsync(admission.ProjectId, ids, marker.Badge, marker.Tone, marker.Label, expectedProjectAdmission: admission)
            : ProjectWorkbenchService.AddObjectMarkerDetailedAsync(admission.ProjectId, ids, marker.Badge, marker.Tone, marker.Label, expectedProjectAdmission: admission));
    }

    private async Task<bool> PersistInsightsStateAsync(InsightsContext context, CanvasWorkbenchUiState state, string operation) {
        CancelPendingCanvasViewStatePersistence();
        var json = NormalizePersistedCanvasUiState(state).ToJson();
        try {
            await ProjectWorkbenchService.SaveViewStateAsync(context.Native.Admission, "structure", json, deferredCompletionCts.Token);
        } catch (Exception exception) {
            RecordInsightsEffect(context, operation, [], null, "The view-state result was not confirmed. Reload before repeating it.", exception);
            return false;
        }
        RecordInsightsEffect(context, operation, [], [], "View state saved for the original project.");
        if (IsCurrentInsights(context)) {
            currentViewStateJson = json;
            surface = surface! with { ViewStateJson = json };
            RefreshCanvasSurface();
        }
        return true;
    }

    private async Task HandleInsightsWindowAsync(InsightsWindowIntent intent) {
        var context = await AdmitInsightsAsync(intent.Origin);
        if (context is null) {
            return;
        }
        var key = intent.Window switch {
            InsightsWindow.Selection => SelectionWindowKey,
            InsightsWindow.ObjectIndex => ObjectIndexWindowKey,
            InsightsWindow.Signals => SignalsWindowKey,
            InsightsWindow.Health => HealthWindowKey,
            _ => throw new ArgumentOutOfRangeException(nameof(intent))
        };
        var state = ResolveEditableUiState();
        state.WindowStates[key] = CanvasWorkbenchWindowState.Normalize(intent.State.Clone());
        await PersistInsightsStateAsync(context, state, "Window placement");
    }

    private Task HandleInsightsSearchAsync(InsightsSearchIntent intent) {
        if (surface is not null && CurrentInsights.Origin == intent.Origin) {
            objectIndexSearchText = intent.Text;
        }
        return Task.CompletedTask;
    }

    private async Task HandleInsightsFocusAsync(InsightsFocusIntent intent) {
        if (await AdmitInsightsAsync(intent.Origin) is { } context && context.Native.Surface.Nodes.Any(node => node.Id == intent.NodeId)) {
            await FocusInsightsAsync(context, intent.NodeId);
        }
    }

    private async Task FocusInsightsAsync(InsightsContext context, string nodeId) {
        if (IsDependencyMode && !string.IsNullOrWhiteSpace(linkModeSourceId) && linkModeSourceId != nodeId) {
            var sourceId = linkModeSourceId;
            await ProjectWorkbenchService.LinkObjectsAsync(context.Origin.ProjectId, sourceId, nodeId,
                ProjectObjectLinkKind.DependsOn, expectedProjectAdmission: context.Native.Admission);
            RecordInsightsEffect(context, "Dependency", [sourceId, nodeId], [sourceId, nodeId], "The dependency was added.");
            if (IsCurrentInsights(context)) {
                await SetCanvasToolModeAsync(CanvasAuthoringMode.Select);
                await ReloadSurfaceAsync(nodeId);
            }
            return;
        }
        await SelectInsightsAsync(context, [nodeId]);
    }

    private async Task SelectInsightsAsync(InsightsContext context, IReadOnlyList<string> ids) {
        var state = ResolveEditableUiState();
        state.SelectedNodeIds = ids.ToList();
        graphAdapter.ExpandSelectionAncestors(context.Native.Surface.Nodes, state, ids);
        if (!await PersistInsightsStateAsync(context, state, "Selection") || !IsCurrentInsights(context)) {
            return;
        }
        selectedNodeIds = ids.ToList();
        selectedWorkflowStatus = null;
        RefreshCanvasSurface();
        var revision = insightsSelectionRevision;
        if (ids.Count == 1) {
            await RefreshSelectedWorkflowStatusAsync(ids[0]);
        }
        if (IsCurrentAction(context.Native) && revision == insightsSelectionRevision && ReferenceEquals(context.Actor, InsightsAuthentication)) {
            await LoadPartyEditorAsync();
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task<InsightsMenu?> OpenInsightsMenuAsync(InsightsMenuRequest request) {
        var context = await AdmitInsightsAsync(request.Origin);
        var node = context?.Native.Surface.Nodes.FirstOrDefault(node => node.Id == request.NodeId);
        if (context is null || node is null) {
            return null;
        }
        var targets = context.Nodes.Count > 1 && context.Nodes.Any(selected => selected.Id == node.Id)
            ? context.Nodes.Select(selected => selected.Id).ToArray() : [node.Id];
        if (targets.Length == 1 && !context.Nodes.Select(selected => selected.Id).SequenceEqual(targets)) {
            await SelectInsightsAsync(context, targets);
            if (!IsCurrentAction(context.Native) || !ReferenceEquals(context.Actor, InsightsAuthentication) ||
                insightsSelectionRevision != context.Origin.SelectionRevision + 1 || !selectedNodeIds.SequenceEqual(targets)) {
                return null;
            }
        }
        var current = CurrentInsights;
        var actions = ResolveSupportPanelContextActions(node).Where(action => targets.Length == 1 || action.ActionId == "delete")
            .Select(action => targets.Length == 1 ? action : action with { Label = "Delete selected" }).ToArray();
        insightsMenu = new(current.Origin, request.OpeningId, node.Id, targets.Length == 1 ? node.Title : $"{targets.Length} selected nodes",
            targets, request.ClientX, request.ClientY, actions);
        await InvokeAsync(StateHasChanged);
        return insightsMenu;
    }

    private async Task ExecuteInsightsMenuAsync(InsightsMenuIntent intent) {
        var expected = insightsMenu;
        if (!ReferenceEquals(expected, intent.Menu) || !intent.Menu.Actions.Any(action => action.ActionId == intent.ActionId) ||
            await AdmitInsightsAsync(intent.Menu.Origin) is not { } context) {
            return;
        }
        insightsMenu = null;
        if (intent.ActionId == "delete") {
            await DeleteNodesAsync(intent.Menu.NodeIds.ToArray(), context.Native);
        } else if (context.Native.Surface.Nodes.FirstOrDefault(node => node.Id == intent.Menu.NodeId) is { } node) {
            await ExecuteInspectorActionAsync(node, intent.ActionId, context.Native);
        }
    }
}
