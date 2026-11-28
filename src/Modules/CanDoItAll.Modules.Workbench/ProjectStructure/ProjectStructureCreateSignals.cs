using System.Text.Json;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using CanDoItAll.Workbench.Planning.UI;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureCreateSignals {
    private static readonly ProjectStructureActionCatalogAdapter Presets = new();
    private static readonly CanvasWorkbenchInputField Priority = Field(PlanningCreateSignalsDraft.PriorityKey, "Priority",
        "Priority 1 is highest. Leave the priority unset if the node does not need ranking.",
        ProjectStructureActionCatalogAdapter.BuildPriorityAction(), action => action.ActionId == "priority:0" ? "No priority" : $"Priority {action.Label}",
        action => action.Label);
    private static readonly CanvasWorkbenchInputField Progress = Field(PlanningCreateSignalsDraft.ProgressKey, "Progress",
        "New nodes start at 0%. Choose a different starting point if work is already underway.",
        ProjectStructureActionCatalogAdapter.BuildProgressAction(), action => action.Label, action => ProgressGlyph(action.ActionId));
    private static readonly CanvasWorkbenchInputField Markers = Field(PlanningCreateSignalsDraft.MarkersKey, "Markers",
        "Choose any markers that apply. Select a marker again to remove it.",
        ProjectStructureActionCatalogAdapter.BuildMarkerAction(), action => action.Label, action => MarkerGlyph(action.ActionId["marker:".Length..]),
        multiple: true);

    internal static IReadOnlyList<CanvasWorkbenchInputField> Fields { get; } = [Priority, Progress, Markers];

    private static CanvasWorkbenchInputField Field(string key, string title, string description, CanvasWorkbenchAction group,
        Func<CanvasWorkbenchAction, string> label, Func<CanvasWorkbenchAction, string> glyph, bool multiple = false) => new() {
        Key = key, SectionKey = key, SectionTitle = title, Label = title, SectionDescription = description,
        InputMode = multiple ? CanvasWorkbenchInputField.MultipleChoice : CanvasWorkbenchInputField.SingleChoice,
        Options = group.Children.Where(action => action.ActionId != "marker:none")
            .Select(action => new CanvasWorkbenchInputOption { Value = action.ActionId, Label = label(action), Glyph = glyph(action) }).ToList()
    };

    internal static ProjectNodeInitialSignals? Read(IReadOnlyDictionary<string, string> values) {
        var priority = values.GetValueOrDefault(Priority.Key, string.Empty);
        var progress = values.GetValueOrDefault(Progress.Key, string.Empty);
        var markers = values.GetValueOrDefault(Markers.Key, string.Empty);
        if (priority.Length == 0 && progress.Length == 0 && markers.Length == 0) {
            return null;
        }
        var level = 0;
        if (priority.Length > 0 && (!Offered(Priority, priority) || !Presets.TryResolvePriorityAction(priority, out level))) {
            throw new ArgumentException("Choose an available priority.");
        }
        ProjectNodeInitialProgress? initialProgress = null;
        if (progress.Length > 0) {
            if (!Offered(Progress, progress) || !Presets.TryResolveProgressAction(progress, out var mode, out var percent)) {
                throw new ArgumentException("Choose an available progress preset.");
            }
            initialProgress = ProjectNodeInitialProgress.FromPreset(mode, percent);
        }
        return new(level, initialProgress, ReadMarkers(markers));
    }

    private static bool Offered(CanvasWorkbenchInputField field, string value) => field.Options.Any(option => option.Value == value);

    private static IReadOnlyList<ProjectNodeMarker> ReadMarkers(string raw) {
        if (raw.Length == 0) {
            return [];
        }
        string[] keys;
        try {
            keys = JsonSerializer.Deserialize<string[]>(raw) ?? throw new ArgumentException("Choose markers from the list.");
        } catch (JsonException exception) {
            throw new ArgumentException("Choose markers from the list.", exception);
        }
        var markers = new List<ProjectNodeMarker>(keys.Length);
        foreach (var key in keys.Distinct(StringComparer.Ordinal)) {
            if (!Offered(Markers, key) || !Presets.TryResolveMarkerAction(key, out var icon, out var tone, out var label)) {
                throw new ArgumentException("Choose an available marker.");
            }
            markers.Add(new(icon, tone, label));
        }
        return markers;
    }

    internal static string ProgressGlyph(string actionId) {
        if (!Presets.TryResolveProgressAction(actionId, out var mode, out var percent)) {
            throw new ArgumentOutOfRangeException(nameof(actionId), actionId, "Unknown progress preset.");
        }
        return (mode, percent) switch {
            ("na", _) => "⊘",
            ("started", _) => "▶",
            (_, 0) => "○",
            (_, <= 30) => "◔",
            (_, <= 60) => "◑",
            (_, < 100) => "◕",
            _ => "●"
        };
    }

    internal static string MarkerGlyph(string marker) => marker switch {
        "question" => "?",
        "alert" => "!",
        "thumbs-up" => "👍",
        "thumbs-down" => "👎",
        "pause" => "⏸",
        "stop" => "■",
        "money" => "$",
        "car" => "🚗",
        "idea" => "✦",
        "risk" => "⚠",
        "none" => "×",
        _ => "•"
    };
}
