using CanDoItAll.Editing;

namespace CanDoItAll.Modules.Projects.Pages.Components;

public sealed class ProjectDefinitionReview {
    public const int MaximumRows = 1024;
    private readonly HashSet<ProjectMergeInput> pending = [];

    public ProjectDefinitionReview(ProjectDefinitionSnapshot original, ProjectDefinitionScalars local,
        ProjectDefinitionExternalIdentity? localIdentity, IReadOnlyList<ProjectPhaseValues> localPhases,
        IReadOnlyList<ProjectOptionValues> localOptions, ProjectDefinitionSnapshot current) {
        if (original.Expectation.Project != current.Expectation.Project) {
            throw new InvalidOperationException("A definition comparison cannot cross project lifetimes or database profiles.");
        }
        var phaseIds = original.Phases.Select(row => row.Id).ToHashSet();
        var optionIds = original.Options.Select(row => row.Id).ToHashSet();
        if (localPhases.Any(row => row.Key.StoredId is { } id && !phaseIds.Contains(id)) ||
            localOptions.Any(row => row.Key.StoredId is { } id && !optionIds.Contains(id))) {
            throw new InvalidOperationException("The local draft contains a saved child identity outside its original observation.");
        }
        Current = current;
        Name = new(original.Scalars.Name, local.Name, current.Scalars.Name, StringComparer.Ordinal);
        Description = TextMerge.Compare(original.Scalars.Description, local.Description, current.Scalars.Description).Field;
        Objective = TextMerge.Compare(original.Scalars.Objective, local.Objective, current.Scalars.Objective).Field;
        Status = new(original.Scalars.Status, local.Status, current.Scalars.Status);
        CurrentPhase = new(original.Scalars.CurrentPhase, local.CurrentPhase, current.Scalars.CurrentPhase, StringComparer.Ordinal);
        TargetDate = new(original.Scalars.TargetDateUtc, local.TargetDateUtc, current.Scalars.TargetDateUtc);
        ExternalIdentity = new(original.ExternalIdentity, localIdentity, current.ExternalIdentity);
        var originalPhases = original.Phases.Select(ProjectPhaseValues.From).ToArray();
        var currentPhases = current.Phases.Select(ProjectPhaseValues.From).ToArray();
        Phases = CollectionMerge.ByKey(originalPhases, localPhases, currentPhases, row => row.Key, maximumRows: MaximumRows)
            .Select(row => new ProjectPhaseReview(row)).ToArray();
        Options = CollectionMerge.ByKey(original.Options.Select(ProjectOptionValues.From).ToArray(), localOptions,
                current.Options.Select(ProjectOptionValues.From).ToArray(), row => row.Key, maximumRows: MaximumRows)
            .Select(row => new ProjectOptionReview(row)).ToArray();
        PhaseOrder = new(originalPhases.Select(row => row.Key).ToArray(), localPhases.Select(row => row.Key).ToArray(),
            currentPhases.Select(row => row.Key).ToArray(), new PhaseOrderComparer());
    }

    public ProjectDefinitionSnapshot Current { get; }
    public MergeField<string> Name { get; }
    public MergeField<string> Description { get; }
    public MergeField<string> Objective { get; }
    public MergeField<ProjectStatus> Status { get; }
    public MergeField<string> CurrentPhase { get; }
    public MergeField<DateTime?> TargetDate { get; }
    public MergeField<ProjectDefinitionExternalIdentity?> ExternalIdentity { get; }
    public IReadOnlyList<ProjectPhaseReview> Phases { get; }
    public IReadOnlyList<ProjectOptionReview> Options { get; }
    public MergeField<IReadOnlyList<ProjectDefinitionRowKey>> PhaseOrder { get; }
    public bool HasPendingInput => pending.Count > 0;
    public long InputRevision { get; private set; }
    public void RecordInput() => InputRevision++;
    public bool OrderNeedsReview => PhaseOrder.IsResolved && PhaseOrder.Choice == MergeChoice.Automatic && Phases.All(row => row.CanApply) &&
        Phases.Any(row => row.Candidate() is not null && !PhaseOrder.Result.Contains(row.Key));
    public bool CanApply => Name.IsResolved && Description.IsResolved && Objective.IsResolved && Status.IsResolved && CurrentPhase.IsResolved &&
        TargetDate.IsResolved && ExternalIdentity.IsResolved && PhaseOrder.IsResolved && Phases.All(row => row.CanApply) && Options.All(row => row.CanApply) &&
        !HasPendingInput && !OrderNeedsReview && !string.IsNullOrWhiteSpace(Name.Result) &&
        (Current.ExternalIdentity is null || Current.ExternalIdentity == ExternalIdentity.Result);

    public void PendingInput(ProjectMergeInput input, bool isPending) {
        if (isPending) {
            pending.Add(input);
        } else {
            pending.Remove(input);
        }
    }

    public void PreserveChoices(ProjectDefinitionReview previous) {
        Keep(previous.Name, Name);
        Keep(previous.Description, Description);
        Keep(previous.Objective, Objective);
        Keep(previous.Status, Status);
        Keep(previous.CurrentPhase, CurrentPhase);
        Keep(previous.TargetDate, TargetDate);
        Keep(previous.ExternalIdentity, ExternalIdentity);
        Keep(previous.PhaseOrder, PhaseOrder, new PhaseOrderComparer());
        var phases = previous.Phases.ToDictionary(row => row.Key);
        foreach (var row in Phases) {
            if (!phases.TryGetValue(row.Key, out var before)) {
                continue;
            }
            Keep(before.Row, row.Row);
            if (before.Fields is { } oldFields && row.Fields is { } fields) {
                Keep(oldFields.Name, fields.Name);
                Keep(oldFields.Goal, fields.Goal);
                Keep(oldFields.Status, fields.Status);
                Keep(oldFields.Dates, fields.Dates);
            }
        }
        var options = previous.Options.ToDictionary(row => row.Key);
        foreach (var row in Options) {
            if (!options.TryGetValue(row.Key, out var before)) {
                continue;
            }
            Keep(before.Row, row.Row);
            if (before.Fields is { } oldFields && row.Fields is { } fields) {
                Keep(oldFields.Name, fields.Name);
                Keep(oldFields.Category, fields.Category);
                Keep(oldFields.Notes, fields.Notes);
            }
        }
    }

    public ProjectEditorModel Candidate() {
        if (!CanApply) {
            throw new InvalidOperationException("Resolve the Project fields, row membership and phase order before applying.");
        }
        var model = ProjectEditorDraft.CreateModel(Current);
        model.Name = Name.Result;
        model.Description = Description.Result;
        model.Objective = Objective.Result;
        model.Status = Status.Result;
        model.CurrentPhase = CurrentPhase.Result;
        model.TargetDateUtc = TargetDate.Result;
        model.ExternalNamespace = ExternalIdentity.Result?.Namespace;
        model.ExternalKey = ExternalIdentity.Result?.Key;
        model.Phases = PhaseCandidates().Select(row => row.Model()).ToList();
        model.Options = Options.Select(row => row.Candidate()).OfType<ProjectOptionValues>().OrderBy(row => row.Category)
            .ThenBy(row => row.Key.Value).Select(row => row.Model()).ToList();
        return model;
    }

    public IReadOnlyList<ProjectPhaseValues> PhaseCandidates() {
        var phases = Phases.Select(row => (row.Key, Value: row.Candidate())).Where(row => row.Value is not null)
            .ToDictionary(row => row.Key, row => row.Value!);
        return PhaseOrder.Result.Concat(PhaseOrder.Current).Concat(PhaseOrder.Local).Concat(PhaseOrder.Original).Distinct()
            .Where(phases.ContainsKey).Select(key => phases[key]).ToArray();
    }

    private static void Keep<T>(MergeField<T> before, MergeField<T> current, IEqualityComparer<T>? comparer = null) {
        var equal = comparer ?? EqualityComparer<T>.Default;
        if (!before.IsResolved || before.Choice == MergeChoice.Automatic || !equal.Equals(before.Original, current.Original) ||
            !equal.Equals(before.Local, current.Local) || !equal.Equals(before.Current, current.Current)) {
            return;
        }
        switch (before.Choice) {
            case MergeChoice.Local:
                current.UseLocal();
                break;
            case MergeChoice.Current:
                current.UseCurrent();
                break;
            case MergeChoice.Custom:
                current.SetResult(before.Result);
                break;
        }
    }

    private sealed class PhaseOrderComparer : IEqualityComparer<IReadOnlyList<ProjectDefinitionRowKey>> {
        public bool Equals(IReadOnlyList<ProjectDefinitionRowKey>? first, IReadOnlyList<ProjectDefinitionRowKey>? second)
            => ReferenceEquals(first, second) || first is not null && second is not null && first.SequenceEqual(second);
        public int GetHashCode(IReadOnlyList<ProjectDefinitionRowKey> value) {
            var hash = new HashCode();
            foreach (var key in value) {
                hash.Add(key);
            }
            return hash.ToHashCode();
        }
    }
}

public enum ProjectMergeInputField { TargetDate, PhaseStart, PhaseEnd }
public readonly record struct ProjectMergeInput(ProjectMergeInputField Field, ProjectDefinitionRowKey? Row = null);
public readonly record struct ProjectMergePendingInput(ProjectMergeInput Input, bool Pending);
