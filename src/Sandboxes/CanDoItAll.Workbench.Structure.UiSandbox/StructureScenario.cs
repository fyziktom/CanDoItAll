using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Workbench.Structure.UI;

namespace CanDoItAll.Workbench.Structure.UiSandbox;

public enum StructureScenarioKind { Nested, Empty, Wide, Loading, Unavailable, Failed }
public enum StructureScenarioOutcome { Accepted, Rejected, Unknown, AcceptedWithReadbackWarning }

public sealed class StructureScenario {
    public const string NoteAction = "scenario:create-note";
    public const string HierarchyAction = "scenario:hierarchy";
    public const string ConversionAction = "scenario:conversion";
    public const string TransferAction = "scenario:transfer";
    public const string ReadonlyNode = "managed";
    private const string DependencyCreateAction = "dependency:create";
    private const string DependencyDeleteAction = "delete-link";
    private const string DependencyKind = "DependsOn";
    public static readonly Guid ChildProjectId = Guid.Parse("cb5ca2b0-a6c9-44be-82f2-e14e9b2f7051");
    public static readonly Guid NestedProjectId = Guid.Parse("2cd8e917-e9db-489d-811b-958f5f4c4899");
    public StructurePresentation Presentation { get; private set; } = default!;
    public StructureDialogsPresentation Dialogs { get; private set; } = new();
    public List<string> Receipts { get; } = [];
    public IReadOnlyList<CanvasWorkbenchInputValue> LastInputs { get; private set; } = [];
    public Guid? NestedOpening { get; private set; }
    public bool HoldCompletion { get; set; }
    public StructureScenarioOutcome Outcome { get; set; }
    private readonly List<TaskCompletionSource> pendingOperations = [];
    public TaskCompletionSource? Pending => pendingOperations.FirstOrDefault();
    public int View { get; set; }
    private CanvasWorkbenchSurface graph = new();
    private ClipboardBuffer? clipboard;
    private sealed record ClipboardBuffer(CanvasWorkbenchClipboardAction Action, Guid ContextId, IReadOnlyList<string> Roots) {
        public bool IsBusy { get; set; }
        public bool RequiresObservation { get; set; }
    }
    private readonly string name;

    public StructureScenario(string name) {
        this.name = name;
        Reset(StructureScenarioKind.Nested);
    }

    public CanvasWorkbenchAction CreateAction { get; } = new() {
        ActionId = NoteAction, Label = "Note", Description = "Create a note with typed configuration fields.", Icon = "edit_note",
        RequiresInput = true, CreateMode = "dialog", InputFields = [
            new() { Key = "amount", Label = "Amount", InputMode = "number", SectionKey = "configuration", SectionTitle = "Configuration" },
            new() { Key = "instant", Label = "Timestamp", InputMode = "datetime-local", SectionKey = "configuration", SectionTitle = "Configuration" },
            new() { Key = "extension", Label = "Extension JSON", InputMode = "textarea", SectionKey = "configuration", SectionTitle = "Configuration" }
        ]
    };

    public void Reset(StructureScenarioKind kind) {
        Dialogs = new();
        clipboard = null;
        View = 0;
        NestedOpening = null;
        graph = new() {
            SurfaceId = Guid.NewGuid().ToString("N"),
            Nodes = kind == StructureScenarioKind.Empty ? [] : [
                Node("root", name, null, 40, 100, false),
                Node("note", "A note with a long label that remains readable while the inspector and standard blocks are open", "root", 430, 60, false),
                Node("branch", "A nested branch", "note", 760, 80, false),
                Node(ReadonlyNode, "Owner-managed task (read only)", "root", 430, 400, true)
            ],
            UiState = new() { SelectedNodeIds = kind == StructureScenarioKind.Empty ? [] : ["root"] },
            Chrome = new() { QuickCreateActions = [CreateAction], ChildNoteActionId = NoteAction,
                HintText = "Select, Ctrl+Shift+click to multi-select, Alt-drag to marquee, drag to move; right-click for structural dialogs." }
        };
        if (kind == StructureScenarioKind.Wide) {
            for (var index = 0; index < 18; index++) {
                graph.Nodes.Add(Node($"wide-{index}", $"Wide branch {index + 1}", "root", 300 + index * 80, 220 + index % 3 * 180, false));
            }
        }
        graph.Links = graph.Nodes.Where(node => node.ParentId is not null)
            .Select(node => new CanvasWorkbenchLink { SourceId = node.ParentId!, TargetId = node.Id, Kind = "contains" }).ToList();
        Presentation = new(Guid.NewGuid(), SkeletonStateOverlayFactory.CreateLoadingSnapshot("Loading Structure", "Reading the selected scenario.")) {
            Canvas = kind is StructureScenarioKind.Loading or StructureScenarioKind.Unavailable or StructureScenarioKind.Failed ? null : graph,
            UnavailableTitle = kind == StructureScenarioKind.Unavailable ? "Project unavailable" : null,
            UnavailableDescription = "The selected project belongs to a different lifetime.",
            Error = kind == StructureScenarioKind.Failed ? "The scenario owner could not load this graph." : null,
            SelectionCount = graph.UiState.SelectedNodeIds.Count,
            ShowFeedback = true,
            Toolbar = new() { CanConnect = graph.Nodes.Count > 0, CanRecompose = graph.Nodes.Count > 1 },
            Toolbox = new() { WindowId = $"toolbox-{graph.SurfaceId}", State = new() { IsVisible = false }, SourceLabel = name,
                Groups = [new("notes", "Notes and decisions", "Ordinary structural nodes", true, [CreateAction])]
            }
        };
    }

    private CanvasWorkbenchNode Node(string id, string title, string? parent, double x, double y, bool readOnly) => new() {
        Id = id, ParentId = parent, Title = title, Subtitle = readOnly ? "Managed projection" : "Editable scenario node", Icon = "account_tree",
        X = x, Y = y, IsReadOnly = readOnly, IsCollapsible = true, Kind = "block", Family = "structure",
        ContextActions = readOnly ? [] : [CreateAction,
            new() { ActionId = HierarchyAction, Label = "Add subproject", Icon = "account_tree" },
            new() { ActionId = ConversionAction, Label = "Convert note", Icon = "transform" },
            new() { ActionId = TransferAction, Label = "Move descendants", Icon = "drive_file_move" }]
    };

    public void Echo() => Presentation = Presentation with { };

    public void HideToolbox() {
        var toolbox = Presentation.Toolbox.State.Clone();
        toolbox.IsVisible = false;
        Presentation = Presentation with { Toolbox = Presentation.Toolbox with { State = toolbox } };
    }

    public async Task ApplyAsync(StructureIntent intent) {
        if (intent.ContextId != Presentation.ContextId) {
            return;
        }
        switch (intent) {
            case StructureSelectionIntent selection:
                graph.UiState.SelectedNodeIds = selection.Selection.SelectedNodeIds.ToList();
                Presentation = Presentation with { SelectionCount = selection.Selection.SelectedNodeIds.Count,
                    Toolbar = Presentation.Toolbar with { CanConnect = graph.Nodes.Any(node => selection.Selection.SelectedNodeIds.Contains(node.Id) && !node.IsReadOnly) } };
                break;
            case StructureNodesMovedIntent moved:
                foreach (var position in moved.Move.Positions) {
                    if (graph.Nodes.Find(node => node.Id == position.NodeId && !node.IsReadOnly) is { } node) {
                        node.X = position.X;
                        node.Y = position.Y;
                    }
                }
                break;
            case StructureStateIntent state:
                graph.UiState = CanvasWorkbenchUiState.Parse(state.StateJson);
                break;
            case StructureViewIntent view:
                View = view.Index;
                break;
            case StructureToolbarIntent { Command: StructureToolbarCommand.Select }:
                SetMode(CanvasWorkbenchModes.Authoring);
                break;
            case StructureToolbarIntent { Command: StructureToolbarCommand.Dependency }:
                SetMode(CanvasWorkbenchModes.Dependency);
                break;
            case StructureToolbarIntent { Command: StructureToolbarCommand.Delete }:
                SetMode(CanvasWorkbenchModes.Delete);
                break;
            case StructureToolbarIntent { Command: StructureToolbarCommand.Toolbox }:
                var toolbox = Presentation.Toolbox.State.Clone();
                toolbox.IsVisible = !toolbox.IsVisible;
                Presentation = Presentation with { Toolbox = Presentation.Toolbox with { State = toolbox } };
                break;
            case StructureToolbarIntent { Command: StructureToolbarCommand.Retry or StructureToolbarCommand.Projects }:
                Reset(StructureScenarioKind.Nested);
                break;
            case StructureToolboxWindowIntent window:
                Presentation = Presentation with { Toolbox = Presentation.Toolbox with { State = window.State.Clone() } };
                break;
            case StructureToolboxSearchIntent search:
                Presentation = Presentation with { Toolbox = Presentation.Toolbox with { Search = search.Search } };
                break;
            case StructureToolboxGroupIntent group:
                Presentation = Presentation with { Toolbox = Presentation.Toolbox with { Groups = Presentation.Toolbox.Groups
                    .Select(item => item.Key == group.GroupId ? item with { IsOpen = !item.IsOpen } : item).ToArray() } };
                break;
            case StructureContextIntent context:
                if (context.Request.ActionId is DependencyCreateAction or DependencyDeleteAction) {
                    await ApplyGraphAsync(context.Request.ActionId, original => {
                        var source = context.Request.LinkSourceId;
                        var target = context.Request.LinkTargetId ?? context.Request.NodeId;
                        if (source is null || target is null || source == target ||
                            original.Nodes.All(node => node.Id != source || node.IsReadOnly) || original.Nodes.All(node => node.Id != target || node.IsReadOnly)) {
                            throw new InvalidOperationException("Read-only or unavailable dependency target was refused.");
                        }
                        original.Links.RemoveAll(link => link.SourceId == source && link.TargetId == target && link.Kind == DependencyKind);
                        if (context.Request.ActionId == DependencyCreateAction) {
                            original.Links.Add(new() { SourceId = source, TargetId = target, Kind = DependencyKind, IsUserAuthored = true });
                        }
                        return $"Dependency {source} to {target}.";
                    });
                } else {
                    OpenDialog(context.Request.ActionId, context.Request.NodeId ?? "root");
                }
                break;
            case StructureClipboardIntent clipboardIntent:
                await ApplyClipboardAsync(clipboardIntent.Request);
                break;
            case StructureNoteEditIntent edit:
                await ApplyGraphAsync("Edit note", original => {
                    var node = original.Nodes.Find(node => node.Id == edit.Request.NodeId && !node.IsReadOnly);
                    if (node is null) {
                        throw new InvalidOperationException("The original editable note is unavailable.");
                    }
                    node.Title = edit.Request.Title;
                    node.InlineText = edit.Request.Notes;
                    return $"Retained {node.Id}.";
                });
                break;
            case StructureCreateIntent create:
                var originalGraph = graph;
                var originalContext = Presentation.ContextId;
                var outcome = Outcome;
                var inputs = create.Request.InputValues?.Select(value => new CanvasWorkbenchInputValue { Key = value.Key, Value = value.Value }).ToArray() ?? [];
                await WaitForCompletionAsync();
                var message = $"{outcome}: original composer {create.Request.ComposerOpeningId:D}.";
                if (outcome is StructureScenarioOutcome.Accepted or StructureScenarioOutcome.AcceptedWithReadbackWarning) {
                    var accepted = Guid.NewGuid().ToString("N");
                    originalGraph.Nodes.Add(Node(accepted, create.Request.Title, create.Request.ParentNodeId, create.Request.X, create.Request.Y, false));
                    message += $" Created {accepted} beneath {create.Request.ParentNodeId ?? "canvas"}.";
                }
                LastInputs = inputs;
                Receipts.Add(message);
                if (Presentation.ContextId == originalContext) {
                    Presentation = Presentation with { Feedback = message, FeedbackTone = outcome == StructureScenarioOutcome.Accepted ? "mint" : "warn" };
                }
                break;
            default:
                Receipts.Add($"Received {intent.GetType().Name} on {name}.");
                break;
        }
    }

    private void SetMode(string mode) {
        graph.Mode = mode;
        graph.DependencySourceId = mode == CanvasWorkbenchModes.Dependency ? graph.UiState.SelectedNodeIds.FirstOrDefault() ?? string.Empty : string.Empty;
        Presentation = Presentation with { Toolbar = Presentation.Toolbar with { Mode = mode } };
    }

    private async Task ApplyGraphAsync(string operation, Func<CanvasWorkbenchSurface, string> apply) {
        var original = graph;
        var context = Presentation.ContextId;
        var outcome = Outcome;
        await WaitForCompletionAsync();
        var message = $"{outcome}: {operation}.";
        if (outcome is StructureScenarioOutcome.Accepted or StructureScenarioOutcome.AcceptedWithReadbackWarning) {
            try {
                message += " " + apply(original);
            } catch (InvalidOperationException failure) {
                outcome = StructureScenarioOutcome.Rejected;
                message = $"{outcome}: {failure.Message}";
            }
        }
        Receipts.Add(message);
        if (context == Presentation.ContextId) {
            Presentation = Presentation with { Feedback = message, FeedbackTone = outcome == StructureScenarioOutcome.Accepted ? "mint" : "warn" };
        }
    }

    private async Task ApplyClipboardAsync(CanvasWorkbenchClipboardRequest request) {
        if (request.SurfaceId != graph.SurfaceId || request.Action == CanvasWorkbenchClipboardAction.Duplicate) {
            Receipts.Add("Unsupported clipboard action or retired surface was refused.");
            return;
        }
        if (request.Action is CanvasWorkbenchClipboardAction.Copy or CanvasWorkbenchClipboardAction.Cut) {
            var selected = graph.Nodes.Where(node => request.SelectedNodeIds.Contains(node.Id)).ToArray();
            clipboard = selected.Length == request.SelectedNodeIds.Count && selected.All(node => !node.IsReadOnly)
                ? new(request.Action, Presentation.ContextId, selected.Where(node => !HasSelectedAncestor(node, request.SelectedNodeIds)).Select(node => node.Id).ToArray()) : null;
            Receipts.Add(clipboard is null ? "Read-only or missing clipboard sources were refused." : $"{request.Action}: captured {clipboard.Roots.Count} branch roots.");
            return;
        }
        var buffer = clipboard;
        var target = request.SelectedNodeIds.Count == 1 ? graph.Nodes.Find(node => node.Id == request.SelectedNodeIds[0] && !node.IsReadOnly) : null;
        if (buffer is null || target is null || buffer.ContextId != Presentation.ContextId || buffer.IsBusy || buffer.RequiresObservation ||
            buffer.Roots.Contains(target.Id) || HasSelectedAncestor(target, buffer.Roots)) {
            Receipts.Add("The clipboard destination, ownership or pending outcome was refused.");
            return;
        }
        buffer.IsBusy = true;
        var outcome = Outcome;
        try {
            await ApplyGraphAsync(buffer.Action.ToString(), original => {
                var roots = original.Nodes.Where(node => buffer.Roots.Contains(node.Id)).ToArray();
                if (buffer.Action == CanvasWorkbenchClipboardAction.Cut) {
                    foreach (var root in roots) {
                        root.ParentId = target.Id;
                    }
                    original.Links.RemoveAll(link => buffer.Roots.Contains(link.TargetId) && link.Kind == "contains");
                    original.Links.AddRange(roots.Select(root => new CanvasWorkbenchLink { SourceId = target.Id, TargetId = root.Id, Kind = "contains" }));
                    if (ReferenceEquals(clipboard, buffer)) {
                        clipboard = null;
                    }
                    return $"Retained roots {string.Join(", ", buffer.Roots)} under {target.Id}.";
                }
                var nodes = original.Nodes.Where(node => !node.IsReadOnly && (buffer.Roots.Contains(node.Id) || HasSelectedAncestor(node, buffer.Roots, original))).ToArray();
                var map = nodes.ToDictionary(node => node.Id, _ => Guid.NewGuid().ToString("N"), StringComparer.Ordinal);
                var links = original.Links.Where(link => map.ContainsKey(link.SourceId) && map.ContainsKey(link.TargetId)).ToArray();
                foreach (var source in nodes) {
                    original.Nodes.Add(Node(map[source.Id], source.Title, buffer.Roots.Contains(source.Id) ? target.Id : map[source.ParentId!], source.X + 80, source.Y + 80, false));
                }
                original.Links.AddRange(links.Select(link => new CanvasWorkbenchLink { SourceId = map[link.SourceId], TargetId = map[link.TargetId], Kind = link.Kind, IsUserAuthored = link.IsUserAuthored }));
                original.Links.AddRange(buffer.Roots.Select(root => new CanvasWorkbenchLink { SourceId = target.Id, TargetId = map[root], Kind = "contains" }));
                return "New identities: " + string.Join(", ", map.Select(pair => $"{pair.Key} → {pair.Value}"));
            });
            buffer.RequiresObservation = outcome == StructureScenarioOutcome.Unknown;
        } finally {
            buffer.IsBusy = false;
        }
    }

    private bool HasSelectedAncestor(CanvasWorkbenchNode node, IReadOnlyList<string> roots, CanvasWorkbenchSurface? source = null) {
        var parent = node.ParentId;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (parent is not null && visited.Add(parent)) {
            if (roots.Contains(parent)) {
                return true;
            }
            parent = (source ?? graph).Nodes.Find(candidate => candidate.Id == parent)?.ParentId;
        }
        return false;
    }

    public void OpenDialog(string action, string node) {
        var opening = Guid.NewGuid();
        Dialogs = action switch {
            HierarchyAction => new(new(opening, $"Add subproject under {name}", "Choose an available project.", "Add subproject", "",
                [new(ChildProjectId, "Available child")], ChildProjectId, "", false, false)),
            ConversionAction => new(Conversion: new(opening, node, "Convert selected node", "Keep the original text and identity.", "Convert", "Target kind",
                [new("decision", "decision", "Decision", "A recorded decision", "diamond", "info")], "decision", "", false, false)),
            TransferAction => new(Transfer: new(opening, node, "Move descendants", "Create a child project for this subtree.", "Create subproject", 2, "Extracted branch", "", false, false)),
            _ => Dialogs
        };
    }

    public async Task HandleDialogAsync(StructureDialogIntent intent) {
        var original = Dialogs;
        var live = original.Hierarchy?.OpeningId == intent.OpeningId || original.Conversion?.OpeningId == intent.OpeningId || original.Transfer?.OpeningId == intent.OpeningId;
        if (!live) {
            return;
        }
        switch (intent) {
            case StructureHierarchySelection selection:
                Dialogs = Dialogs with { Hierarchy = original.Hierarchy! with { SelectedProjectId = selection.ProjectId } };
                return;
            case StructureConversionSelection selection:
                Dialogs = Dialogs with { Conversion = original.Conversion! with { SelectedActionId = selection.ActionId } };
                return;
            case StructureTransferName name:
                Dialogs = Dialogs with { Transfer = original.Transfer! with { ProjectName = name.Name } };
                return;
            case StructureDialogCommand { Operation: StructureDialogOperation.CreateProject }:
                NestedOpening = intent.OpeningId;
                return;
            case StructureDialogCommand { Operation: StructureDialogOperation.CloseHierarchy or StructureDialogOperation.CloseConversion or StructureDialogOperation.CloseTransfer }:
                Dialogs = new();
                return;
        }
        if (original.Hierarchy is { IsBusy: true } or { RequiresObservation: true } ||
            original.Conversion is { IsBusy: true } or { RequiresObservation: true } ||
            original.Transfer is { IsBusy: true } or { RequiresObservation: true }) {
            return;
        }
        var outcome = Outcome;
        Dialogs = original with {
            Hierarchy = original.Hierarchy is { } hierarchy ? hierarchy with { IsBusy = true } : null,
            Conversion = original.Conversion is { } conversion ? conversion with { IsBusy = true } : null,
            Transfer = original.Transfer is { } transfer ? transfer with { IsBusy = true } : null
        };
        await WaitForCompletionAsync();
        var message = $"{outcome}: original opening {intent.OpeningId:D}";
        Receipts.Add(message);
        Presentation = Presentation with { Feedback = message, FeedbackTone = outcome == StructureScenarioOutcome.Accepted ? "mint" : "warn" };
        if (Dialogs.Hierarchy?.OpeningId != intent.OpeningId && Dialogs.Conversion?.OpeningId != intent.OpeningId && Dialogs.Transfer?.OpeningId != intent.OpeningId) {
            return;
        }
        Dialogs = outcome is StructureScenarioOutcome.Accepted or StructureScenarioOutcome.AcceptedWithReadbackWarning ? new() : original with {
            Hierarchy = original.Hierarchy is { } rejectedHierarchy ? rejectedHierarchy with { Error = message, RequiresObservation = outcome == StructureScenarioOutcome.Unknown } : null,
            Conversion = original.Conversion is { } rejectedConversion ? rejectedConversion with { Error = message, RequiresObservation = outcome == StructureScenarioOutcome.Unknown } : null,
            Transfer = original.Transfer is { } rejectedTransfer ? rejectedTransfer with { Error = message, RequiresObservation = outcome == StructureScenarioOutcome.Unknown } : null
        };
    }

    public void ReturnNestedProject() {
        if (NestedOpening == Dialogs.Hierarchy?.OpeningId && Dialogs.Hierarchy is { } hierarchy) {
            Dialogs = Dialogs with { Hierarchy = hierarchy with { AvailableProjects = [.. hierarchy.AvailableProjects, new(NestedProjectId, "Nested project")], SelectedProjectId = NestedProjectId } };
        }
        NestedOpening = null;
    }

    private async Task WaitForCompletionAsync() {
        if (!HoldCompletion) {
            return;
        }
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        pendingOperations.Add(pending);
        await pending.Task;
        pendingOperations.Remove(pending);
    }

    public void ReleaseAll() {
        foreach (var pending in pendingOperations.ToArray()) {
            pending.TrySetResult();
        }
    }
}
