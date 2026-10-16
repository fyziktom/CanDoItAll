using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Modules.Projects.Pages.Components;

public enum ProjectEditorMutationState {
    Ready,
    Saving,
    Seeding,
    SaveOutcomeUnknown,
    SaveReconciliationFailed,
    SeedOutcomeUnknown,
    Deleting,
    DeleteOutcomeUnknown,
    Deleted,
    AdmissionRefused,
    Conflict
}

public enum ProjectEditorConflictReadState { NotRequested, Loading, Ready, Failed }

public sealed record ProjectEditorConflict(
    ProjectEditorSubmission Submitted,
    ProjectEditorConflictReadState ReadState = ProjectEditorConflictReadState.NotRequested,
    ProjectDefinitionSnapshot? Current = null,
    ProjectEditorModel? CurrentModel = null,
    string? ReadError = null);

public sealed record ProjectEditorAcceptedOutcome(ProjectEditorSubmission Submitted, ProjectEditorAcknowledgement Accepted);

public sealed class ProjectEditorDraft {
    private readonly Dictionary<FieldIdentifier, long> revisions = [];
    private readonly ValidationMessageStore validation;

    public ProjectEditorDraft(ProjectEditorModel model) {
        Model = model;
        EditContext = new(model);
        validation = new(EditContext);
        EditContext.OnFieldChanged += (_, args) => {
            revisions[args.FieldIdentifier] = Revision(args.FieldIdentifier) + 1;
            RecordStructureEdit();
        };
    }

    public ProjectEditorModel Model { get; }
    public EditContext EditContext { get; }
    public List<StarterObjectDraft> StarterObjects { get; } = [];
    public ProjectEditorMutationState Mutation { get; set; }
    public ProjectEditorAcknowledgement? Acknowledgement { get; private set; }
    public ProjectEditorAcceptedOutcome? AcceptedOutcome { get; private set; }
    public ProjectEditorConflict? Conflict { get; private set; }
    public ProjectEditorDraft? PreviousDraft { get; private set; }
    public ProjectEditorModel? ComparisonModel => Conflict?.CurrentModel ?? PreviousDraft?.Model;
    public bool CanMutate => Mutation == ProjectEditorMutationState.Ready;
    public string? Message { get; set; }
    public bool IsError { get; set; }
    public long EditRevision { get; private set; }

    public void RecordStructureEdit() => EditRevision++;

    public void RecordAcceptedOutcome(ProjectEditorSubmission submitted, ProjectEditorAcknowledgement accepted)
        => AcceptedOutcome = new(submitted, accepted);

    public bool Validate() {
        validation.Clear();
        if (string.IsNullOrWhiteSpace(Model.Name)) {
            validation.Add(new FieldIdentifier(Model, nameof(Model.Name)), "Project name is required.");
        }
        EditContext.NotifyValidationStateChanged();
        return EditContext.Validate();
    }

    public ProjectEditorSubmission Capture() => new(
        Clone(Model), Model.Phases.ToArray(), Model.Options.ToArray(),
        StarterObjects.Where(row => !row.IsSeeded && !string.IsNullOrWhiteSpace(row.Title))
            .Select(row => new ProjectStarterSubmission(row, new(row.ObjectType, row.Title, row.Subtitle, row.Subtitle)))
            .ToArray(), new Dictionary<FieldIdentifier, long>(revisions), EditRevision);

    public void RecordConflict(ProjectEditorSubmission submitted) {
        Conflict = new(submitted);
        Mutation = ProjectEditorMutationState.Conflict;
        Message = "This project changed after the original read. Your draft is preserved. Read the current definition to compare or reconcile.";
        IsError = true;
    }

    public void BeginConflictRead() {
        var conflict = Conflict ?? throw new InvalidOperationException("There is no conflicting definition to inspect.");
        Conflict = new(conflict.Submitted, ProjectEditorConflictReadState.Loading);
    }

    public void CompleteConflictRead(ProjectDefinitionSnapshot current) {
        var conflict = Conflict ?? throw new InvalidOperationException("There is no conflicting definition to inspect.");
        var original = conflict.Submitted.Model.ExpectedDefinition
            ?? throw new InvalidOperationException("The conflicting submission has no original definition expectation.");
        if (current.Expectation.Project.ProjectId != original.Project.ProjectId ||
            current.Expectation.Project.DatabaseProfileId != original.Project.DatabaseProfileId) {
            throw new InvalidOperationException("The current definition belongs to a different project or database profile.");
        }
        Conflict = new(conflict.Submitted, ProjectEditorConflictReadState.Ready, current, CreateModel(current));
    }

    public void FailConflictRead(string error) {
        var conflict = Conflict ?? throw new InvalidOperationException("There is no conflicting definition to inspect.");
        Conflict = new(conflict.Submitted, ProjectEditorConflictReadState.Failed, ReadError: error);
    }

    public ProjectEditorDraft StartFromCurrent(bool preservePrevious) {
        if (Conflict is not { ReadState: ProjectEditorConflictReadState.Ready, Current: { } current }) {
            throw new InvalidOperationException("Read the current definition before replacing the editor.");
        }
        return new(CreateModel(current)) {
            PreviousDraft = preservePrevious ? this : null,
            Message = preservePrevious
                ? "Reconcile selected changes in this current-based draft. The previous draft remains beside it for comparison and copying."
                : "The current saved definition is loaded."
        };
    }

    public void Acknowledge(ProjectEditorSubmission submitted, ProjectEditorAcknowledgement accepted) {
        ValidateAcknowledgement(submitted, accepted);
        Acknowledgement = accepted;
        PreviousDraft = null;
        Model.Id = accepted.Project.ProjectId;
        Model.ExpectedLifetimeId = accepted.Project.LifetimeId;
        Model.ExpectedProjectAdmission = accepted.Project;
        Model.ExpectedDefinition = accepted.Definition;
        if (Unchanged(Model, nameof(Model.ExternalNamespace), Model.ExternalNamespace, submitted.Model.ExternalNamespace) &&
            Unchanged(Model, nameof(Model.ExternalKey), Model.ExternalKey, submitted.Model.ExternalKey)) {
            Model.ExternalNamespace = accepted.ExternalIdentity?.Namespace;
            Model.ExternalKey = accepted.ExternalIdentity?.Key;
        }
        Merge(Model, nameof(Model.Name), Model.Name, submitted.Model.Name, accepted.Name, value => Model.Name = value);
        Merge(Model, nameof(Model.Description), Model.Description, submitted.Model.Description, accepted.Description, value => Model.Description = value);
        Merge(Model, nameof(Model.Objective), Model.Objective, submitted.Model.Objective, accepted.Objective, value => Model.Objective = value);
        Merge(Model, nameof(Model.Status), Model.Status, submitted.Model.Status, accepted.Status, value => Model.Status = value);
        Merge(Model, nameof(Model.CurrentPhase), Model.CurrentPhase, submitted.Model.CurrentPhase, accepted.CurrentPhase, value => Model.CurrentPhase = value);
        Merge(Model, nameof(Model.TargetDateUtc), Model.TargetDateUtc, submitted.Model.TargetDateUtc, accepted.TargetDateUtc, value => Model.TargetDateUtc = value);
        var livePhases = Model.Phases.ToHashSet();
        foreach (var phase in accepted.Phases) {
            var live = submitted.PhaseRows[phase.SubmittedIndex];
            var sent = submitted.Model.Phases[phase.SubmittedIndex];
            if (!livePhases.Contains(live)) {
                continue;
            }
            live.Id = phase.Id;
            Merge(live, nameof(live.Name), live.Name, sent.Name, phase.Name, value => live.Name = value);
            Merge(live, nameof(live.Goal), live.Goal, sent.Goal, phase.Goal, value => live.Goal = value);
            Merge(live, nameof(live.Status), live.Status, sent.Status, phase.Status, value => live.Status = value);
            Merge(live, nameof(live.StartDateUtc), live.StartDateUtc, sent.StartDateUtc, phase.StartDateUtc, value => live.StartDateUtc = value);
            Merge(live, nameof(live.EndDateUtc), live.EndDateUtc, sent.EndDateUtc, phase.EndDateUtc, value => live.EndDateUtc = value);
        }
        var liveOptions = Model.Options.ToHashSet();
        var acceptedOptionIndices = accepted.Options.Select(option => option.SubmittedIndex).ToHashSet();
        for (var index = 0; index < submitted.OptionRows.Count; index++) {
            var live = submitted.OptionRows[index];
            var sent = submitted.Model.Options[index];
            if (!acceptedOptionIndices.Contains(index) && sent.Id is { } removedId && live.Id == removedId &&
                string.IsNullOrWhiteSpace(sent.OptionName) && string.IsNullOrWhiteSpace(sent.Notes) && liveOptions.Contains(live)) {
                live.Id = null;
            }
        }
        foreach (var option in accepted.Options) {
            var live = submitted.OptionRows[option.SubmittedIndex];
            var sent = submitted.Model.Options[option.SubmittedIndex];
            if (!liveOptions.Contains(live)) {
                continue;
            }
            live.Id = option.Id;
            Merge(live, nameof(live.Category), live.Category, sent.Category, option.Category, value => live.Category = value);
            Merge(live, nameof(live.OptionName), live.OptionName, sent.OptionName, option.OptionName, value => live.OptionName = value);
            Merge(live, nameof(live.Notes), live.Notes, sent.Notes, option.Notes, value => live.Notes = value);
        }

        void Merge<T>(object row, string field, T live, T sent, T value, Action<T> apply) {
            if (Unchanged(row, field, live, sent)) {
                apply(value);
            }
        }

        bool Unchanged<T>(object row, string field, T live, T sent) {
            var identity = new FieldIdentifier(row, field);
            return Revision(identity) == submitted.Revisions.GetValueOrDefault(identity) && EqualityComparer<T>.Default.Equals(live, sent);
        }
    }

    public void AcknowledgeSeeds(ProjectEditorSubmission submitted) {
        foreach (var seed in submitted.Seeds) {
            seed.Row.IsSeeded = true;
            if (seed.Row.ObjectType == seed.Value.ObjectType && seed.Row.Title == seed.Value.Title && seed.Row.Subtitle == seed.Value.Subtitle) {
                StarterObjects.Remove(seed.Row);
            }
        }
    }

    private long Revision(FieldIdentifier field) => revisions.GetValueOrDefault(field);

    private static ProjectEditorModel CreateModel(ProjectDefinitionSnapshot current) {
        var model = new ProjectEditorModel {
            Id = current.Expectation.Project.ProjectId,
            ExpectedLifetimeId = current.Expectation.Project.LifetimeId,
            ExpectedProjectAdmission = current.Expectation.Project,
            ExpectedDefinition = current.Expectation,
            ExternalNamespace = current.ExternalIdentity?.Namespace,
            ExternalKey = current.ExternalIdentity?.Key,
            Name = current.Scalars.Name,
            Description = current.Scalars.Description,
            Objective = current.Scalars.Objective,
            Status = current.Scalars.Status,
            CurrentPhase = current.Scalars.CurrentPhase,
            TargetDateUtc = current.Scalars.TargetDateUtc,
            Phases = current.Phases.Select(row => new ProjectPhaseEditorModel {
                Id = row.Id, Name = row.Name, Goal = row.Goal, Status = row.Status,
                StartDateUtc = row.StartDateUtc, EndDateUtc = row.EndDateUtc
            }).ToList(),
            Options = current.Options.Select(row => new ProjectOptionEditorModel {
                Id = row.Id, Category = row.Category, OptionName = row.OptionName, Notes = row.Notes
            }).ToList()
        };
        foreach (var category in Enum.GetValues<ProjectOptionCategory>().Where(category => category != ProjectOptionCategory.Other)) {
            if (model.Options.All(row => row.Category != category)) {
                model.Options.Add(new() { Category = category });
            }
        }
        return model;
    }

    private static void ValidateAcknowledgement(ProjectEditorSubmission submitted, ProjectEditorAcknowledgement accepted) {
        if (accepted.Definition is null ||
            submitted.Model.Id is { } id && accepted.Project.ProjectId != id ||
            submitted.Model.ExpectedLifetimeId is { } lifetime && accepted.Project.LifetimeId != lifetime ||
            submitted.Model.ExpectedDefinition is { } original && accepted.Project != original.Project ||
            submitted.Model.ExpectedProjectAdmission is { } admission && accepted.Project != admission ||
            submitted.Model.Id.HasValue && (submitted.Model.ExpectedDefinition is null ||
                accepted.Definition.Revision == submitted.Model.ExpectedDefinition.Revision)) {
            throw new InvalidOperationException("The editor acknowledgement does not advance the submitted project definition.");
        }
        if (submitted.PhaseRows.Count != submitted.Model.Phases.Count || submitted.OptionRows.Count != submitted.Model.Options.Count) {
            throw new InvalidOperationException("The editor submission's captured row mapping is invalid.");
        }
        ValidateRows(accepted.Phases, submitted.Model.Phases.Count,
            static row => (row.SubmittedIndex, row.Id), index => submitted.Model.Phases[index].Id, static _ => false);
        ValidateRows(accepted.Options, submitted.Model.Options.Count,
            static row => (row.SubmittedIndex, row.Id), index => submitted.Model.Options[index].Id,
            index => string.IsNullOrWhiteSpace(submitted.Model.Options[index].OptionName) &&
                string.IsNullOrWhiteSpace(submitted.Model.Options[index].Notes));
    }

    private static void ValidateRows<T>(IReadOnlyList<T>? accepted, int submittedCount,
        Func<T, (int Index, Guid Id)> identify, Func<int, Guid?> originalId, Func<int, bool> mayOmit) where T : class {
        if (accepted is null) {
            throw new InvalidOperationException("The editor acknowledgement has no accepted row mapping.");
        }
        var indices = new HashSet<int>();
        var ids = new HashSet<Guid>();
        foreach (var row in accepted) {
            if (row is null) {
                throw new InvalidOperationException("The editor acknowledgement contains a null row.");
            }
            var (index, id) = identify(row);
            if (index < 0 || index >= submittedCount || id == Guid.Empty || !indices.Add(index) || !ids.Add(id) ||
                originalId(index) is { } existingId && existingId != id) {
                throw new InvalidOperationException("The editor acknowledgement contains an invalid row identity or submitted index.");
            }
        }
        for (var index = 0; index < submittedCount; index++) {
            if (!indices.Contains(index) && !mayOmit(index)) {
                throw new InvalidOperationException("The editor acknowledgement omitted a required submitted row.");
            }
        }
    }

    private static ProjectEditorModel Clone(ProjectEditorModel source) => new() {
        Id = source.Id,
        ExpectedLifetimeId = source.ExpectedLifetimeId,
        ExpectedProjectAdmission = source.ExpectedProjectAdmission,
        ExpectedDefinition = source.ExpectedDefinition,
        ExternalNamespace = source.ExternalNamespace,
        ExternalKey = source.ExternalKey,
        Name = source.Name,
        Description = source.Description,
        Objective = source.Objective,
        Status = source.Status,
        CurrentPhase = source.CurrentPhase,
        TargetDateUtc = source.TargetDateUtc,
        Phases = source.Phases.Select(row => new ProjectPhaseEditorModel {
            Id = row.Id,
            Name = row.Name,
            Goal = row.Goal,
            Status = row.Status,
            StartDateUtc = row.StartDateUtc,
            EndDateUtc = row.EndDateUtc
        }).ToList(),
        Options = source.Options.Select(row => new ProjectOptionEditorModel {
            Id = row.Id,
            Category = row.Category,
            OptionName = row.OptionName,
            Notes = row.Notes
        }).ToList()
    };
}

public sealed record ProjectStarterSubmission(StarterObjectDraft Row, ProjectObjectSeedDraft Value);

public sealed record ProjectEditorSubmission(
    ProjectEditorModel Model,
    IReadOnlyList<ProjectPhaseEditorModel> PhaseRows,
    IReadOnlyList<ProjectOptionEditorModel> OptionRows,
    IReadOnlyList<ProjectStarterSubmission> Seeds,
    IReadOnlyDictionary<FieldIdentifier, long> Revisions,
    long EditRevision);
