namespace CanDoItAll.Modules.Projects.Pages.Components;

public readonly record struct ProjectDefinitionRowKey {
    private ProjectDefinitionRowKey(Guid value, bool stored) {
        if (value == Guid.Empty) {
            throw new ArgumentException("A comparison row requires a nonempty identity.", nameof(value));
        }
        Value = value;
        IsStored = stored;
    }

    public Guid Value { get; }
    public bool IsStored { get; }
    public Guid? StoredId => IsStored ? Value : null;
    public static ProjectDefinitionRowKey Stored(Guid id) => new(id, true);
    public static ProjectDefinitionRowKey Local() => new(Guid.NewGuid(), false);
}

public sealed record ProjectPhaseDates(DateTime? Start, DateTime? End);

public sealed record ProjectPhaseValues(ProjectDefinitionRowKey Key, string Name, string Goal,
    ProjectPhaseStatus Status, ProjectPhaseDates Dates) {
    public static ProjectPhaseValues From(ProjectDefinitionPhase row) => new(ProjectDefinitionRowKey.Stored(row.Id),
        row.Name, row.Goal, row.Status, new(row.StartDateUtc, row.EndDateUtc));

    public ProjectPhaseEditorModel Model() => new() {
        Id = Key.StoredId, Name = Name, Goal = Goal, Status = Status, StartDateUtc = Dates.Start, EndDateUtc = Dates.End
    };
}

public sealed record ProjectOptionValues(ProjectDefinitionRowKey Key, ProjectOptionCategory Category, string Name, string Notes) {
    public static ProjectOptionValues From(ProjectDefinitionOption row) => new(ProjectDefinitionRowKey.Stored(row.Id), row.Category, row.OptionName, row.Notes);
    public ProjectOptionEditorModel Model() => new() { Id = Key.StoredId, Category = Category, OptionName = Name, Notes = Notes };
}
