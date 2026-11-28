using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Workbench.Planning.UI;

public sealed record PlanningCreateSignalsDraft(string Priority = "", string Progress = "", string Markers = "") {
    public const string PriorityKey = "initialPriority";
    public const string ProgressKey = "initialProgress";
    public const string MarkersKey = "initialMarkers";

    public string Get(string key) => key switch {
        PriorityKey => Priority,
        ProgressKey => Progress,
        MarkersKey => Markers,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown initial node signal.")
    };

    public PlanningCreateSignalsDraft With(string key, string value) => key switch {
        PriorityKey => this with { Priority = value },
        ProgressKey => this with { Progress = value },
        MarkersKey => this with { Markers = value },
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown initial node signal.")
    };

    public static PlanningCreateSignalsDraft From(IReadOnlyList<CanvasWorkbenchInputValue>? values) {
        var draft = new PlanningCreateSignalsDraft();
        foreach (var entry in values ?? []) {
            if (IsSignal(entry.Key)) {
                draft = draft.With(entry.Key, entry.Value);
            }
        }
        return draft;
    }

    public IReadOnlyList<CanvasWorkbenchInputValue> ApplyTo(IReadOnlyList<CanvasWorkbenchInputValue>? values) =>
        [.. (values ?? []).Where(entry => !IsSignal(entry.Key)),
         .. new[] { PriorityKey, ProgressKey, MarkersKey }.Where(key => Get(key).Length > 0)
             .Select(key => new CanvasWorkbenchInputValue { Key = key, Value = Get(key) })];

    private static bool IsSignal(string key) => key is PriorityKey or ProgressKey or MarkersKey;
}
