namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectNodeInitialSignals(
    int Priority = 0,
    ProjectNodeInitialProgress? Progress = null,
    IReadOnlyList<ProjectNodeMarker>? Markers = null) {
    public int Priority { get; } = Priority is >= 0 and <= 6
        ? Priority
        : throw new ArgumentOutOfRangeException(nameof(Priority), Priority, "Priority must be between 0 and 6.");

    public IReadOnlyList<ProjectNodeMarker> Markers { get; } = NormalizeMarkers(Markers);

    internal bool HasValues => Priority != 0 || Progress is not null || Markers.Count != 0;

    private static IReadOnlyList<ProjectNodeMarker> NormalizeMarkers(IReadOnlyList<ProjectNodeMarker>? markers) {
        var normalized = ProjectObjectMetadataSerializer.NormalizeMarkers(markers);
        if (normalized.Count != (markers?.Count ?? 0)) {
            throw new ArgumentException("Each initial marker must have a distinct, nonempty icon.", nameof(markers));
        }
        return Array.AsReadOnly(normalized.ToArray());
    }
}

public sealed record ProjectNodeInitialProgress {
    internal static ProjectNodeInitialProgress Zero { get; } = new("progress", 0);

    [System.Text.Json.Serialization.JsonConstructor]
    private ProjectNodeInitialProgress(string mode, int percent) {
        (Mode, Percent) = (mode, percent) switch {
            ("progress", >= 0 and < 100) => (mode, percent),
            ("progress" or "complete", 100) => ("complete", 100),
            ("started" or "na", 0) => (mode, 0),
            _ => throw new ArgumentException("The progress mode and percentage must identify a valid progress preset.")
        };
    }

    public string Mode { get; }
    public int Percent { get; }

    public static ProjectNodeInitialProgress FromPreset(string mode, int percent) => new(mode, percent);
}
