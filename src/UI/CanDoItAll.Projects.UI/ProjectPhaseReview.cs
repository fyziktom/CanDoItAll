using CanDoItAll.Editing;

namespace CanDoItAll.Modules.Projects.Pages.Components;

public sealed class ProjectPhaseFields(ProjectPhaseValues original, ProjectPhaseValues local, ProjectPhaseValues current) {
    public MergeField<string> Name { get; } = new(original.Name, local.Name, current.Name, StringComparer.Ordinal);
    public MergeField<string> Goal { get; } = TextMerge.Compare(original.Goal, local.Goal, current.Goal).Field;
    public MergeField<ProjectPhaseStatus> Status { get; } = new(original.Status, local.Status, current.Status);
    public MergeField<ProjectPhaseDates> Dates { get; } = new(original.Dates, local.Dates, current.Dates);
    public bool CanApply => Name.IsResolved && Goal.IsResolved && Status.IsResolved && Dates.IsResolved;

    public ProjectPhaseValues Candidate(ProjectDefinitionRowKey key) => new(key, Name.Result, Goal.Result, Status.Result, Dates.Result);
}

public sealed class ProjectPhaseReview {
    private readonly ProjectDefinitionRowKey restoredKey = ProjectDefinitionRowKey.Local();
    public ProjectPhaseReview(KeyedMergeRow<ProjectDefinitionRowKey, ProjectPhaseValues> row) {
        Key = row.Key;
        Row = row.Field;
        if (Row.Original.Exists && Row.Local.Exists && Row.Current.Exists) {
            Fields = new(Row.Original.Value!, Row.Local.Value!, Row.Current.Value!);
        }
    }

    public ProjectDefinitionRowKey Key { get; }
    public MergeField<MergeEntry<ProjectPhaseValues>> Row { get; }
    public ProjectPhaseFields? Fields { get; }
    public bool CanApply => Fields?.CanApply ?? Row.IsResolved;
    public bool IsChanged => Row.LocalChanged || Row.CurrentChanged;
    public string Label => Row.Local.Value?.Name ?? Row.Current.Value?.Name ?? Row.Original.Value?.Name ?? "Unnamed phase";
    public bool RecreatesRemovedRow => Key.IsStored && !Row.Current.Exists && Row.Local.Exists;
    public ProjectPhaseValues? Candidate() {
        if (Fields is not null) {
            return Fields.Candidate(Key);
        }
        if (!Row.Result.Exists) {
            return null;
        }
        var value = Row.Result.Value ?? throw new InvalidOperationException("A retained phase requires its values.");
        if (value.Key != Key) {
            throw new InvalidOperationException("The chosen phase has a different comparison identity.");
        }
        return RecreatesRemovedRow ? value with { Key = restoredKey } : value;
    }
}
