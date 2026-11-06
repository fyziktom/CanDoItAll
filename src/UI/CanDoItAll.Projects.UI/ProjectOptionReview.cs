using CanDoItAll.Editing;

namespace CanDoItAll.Modules.Projects.Pages.Components;

public sealed class ProjectOptionFields(ProjectOptionValues original, ProjectOptionValues local, ProjectOptionValues current) {
    public MergeField<ProjectOptionCategory> Category { get; } = new(original.Category, local.Category, current.Category);
    public MergeField<string> Name { get; } = new(original.Name, local.Name, current.Name, StringComparer.Ordinal);
    public MergeField<string> Notes { get; } = TextMerge.Compare(original.Notes, local.Notes, current.Notes).Field;
    public bool CanApply => Category.IsResolved && Name.IsResolved && Notes.IsResolved;
    public ProjectOptionValues Candidate(ProjectDefinitionRowKey key) => new(key, Category.Result, Name.Result, Notes.Result);
}

public sealed class ProjectOptionReview {
    private readonly ProjectDefinitionRowKey restoredKey = ProjectDefinitionRowKey.Local();
    public ProjectOptionReview(KeyedMergeRow<ProjectDefinitionRowKey, ProjectOptionValues> row) {
        Key = row.Key;
        Row = row.Field;
        if (Row.Original.Exists && Row.Local.Exists && Row.Current.Exists) {
            Fields = new(Row.Original.Value!, Row.Local.Value!, Row.Current.Value!);
        }
    }

    public ProjectDefinitionRowKey Key { get; }
    public MergeField<MergeEntry<ProjectOptionValues>> Row { get; }
    public ProjectOptionFields? Fields { get; }
    public bool CanApply => Fields?.CanApply ?? Row.IsResolved;
    public bool IsChanged => Row.LocalChanged || Row.CurrentChanged;
    public string Label => Row.Local.Value?.Name ?? Row.Current.Value?.Name ?? Row.Original.Value?.Name ?? "Unnamed option";
    public bool RecreatesRemovedRow => Key.IsStored && !Row.Current.Exists && Row.Local.Exists;
    public ProjectOptionValues? Candidate() {
        if (Fields is not null) {
            return Fields.Candidate(Key);
        }
        if (!Row.Result.Exists) {
            return null;
        }
        var value = Row.Result.Value ?? throw new InvalidOperationException("A retained option requires its values.");
        if (value.Key != Key) {
            throw new InvalidOperationException("The chosen option has a different comparison identity.");
        }
        return RecreatesRemovedRow ? value with { Key = restoredKey } : value;
    }
}
