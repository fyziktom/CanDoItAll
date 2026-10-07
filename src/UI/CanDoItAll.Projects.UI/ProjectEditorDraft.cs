using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Modules.Projects.Pages.Components;

public enum ProjectEditorMutationState {
    Ready,
    Saving,
    Seeding,
    SaveOutcomeUnknown,
    SeedOutcomeUnknown,
    Deleting,
    DeleteOutcomeUnknown,
    Deleted,
    AdmissionRefused
}

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
    public bool CanMutate => Mutation == ProjectEditorMutationState.Ready;
    public string? Message { get; set; }
    public bool IsError { get; set; }
    public long EditRevision { get; private set; }

    public void RecordStructureEdit() => EditRevision++;

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

    public void Acknowledge(ProjectEditorSubmission submitted, ProjectEditorAcknowledgement accepted) {
        if (submitted.Model.Id is { } id && accepted.Project.ProjectId != id ||
            submitted.Model.ExpectedLifetimeId is { } lifetime && accepted.Project.LifetimeId != lifetime) {
            throw new InvalidOperationException("The editor acknowledgement does not belong to the submitted project lifetime.");
        }
        Acknowledgement = accepted;
        Model.Id = accepted.Project.ProjectId;
        Model.ExpectedLifetimeId = accepted.Project.LifetimeId;
        Model.ExpectedProjectAdmission = accepted.Project;
        Merge(Model, nameof(Model.Name), Model.Name, submitted.Model.Name, accepted.Name, value => Model.Name = value);
        Merge(Model, nameof(Model.Description), Model.Description, submitted.Model.Description, accepted.Description, value => Model.Description = value);
        Merge(Model, nameof(Model.Objective), Model.Objective, submitted.Model.Objective, accepted.Objective, value => Model.Objective = value);
        Merge(Model, nameof(Model.Status), Model.Status, submitted.Model.Status, accepted.Status, value => Model.Status = value);
        Merge(Model, nameof(Model.CurrentPhase), Model.CurrentPhase, submitted.Model.CurrentPhase, accepted.CurrentPhase, value => Model.CurrentPhase = value);
        Merge(Model, nameof(Model.TargetDateUtc), Model.TargetDateUtc, submitted.Model.TargetDateUtc, accepted.TargetDateUtc, value => Model.TargetDateUtc = value);
        foreach (var phase in accepted.Phases) {
            var live = submitted.PhaseRows[phase.SubmittedIndex];
            var sent = submitted.Model.Phases[phase.SubmittedIndex];
            if (!Model.Phases.Contains(live)) {
                continue;
            }
            live.Id = phase.Id;
            Merge(live, nameof(live.Name), live.Name, sent.Name, phase.Name, value => live.Name = value);
            Merge(live, nameof(live.Goal), live.Goal, sent.Goal, phase.Goal, value => live.Goal = value);
            Merge(live, nameof(live.Status), live.Status, sent.Status, phase.Status, value => live.Status = value);
            Merge(live, nameof(live.StartDateUtc), live.StartDateUtc, sent.StartDateUtc, phase.StartDateUtc, value => live.StartDateUtc = value);
            Merge(live, nameof(live.EndDateUtc), live.EndDateUtc, sent.EndDateUtc, phase.EndDateUtc, value => live.EndDateUtc = value);
        }
        foreach (var option in accepted.Options) {
            var live = submitted.OptionRows[option.SubmittedIndex];
            var sent = submitted.Model.Options[option.SubmittedIndex];
            if (!Model.Options.Contains(live)) {
                continue;
            }
            live.Id = option.Id;
            Merge(live, nameof(live.Category), live.Category, sent.Category, option.Category, value => live.Category = value);
            Merge(live, nameof(live.OptionName), live.OptionName, sent.OptionName, option.OptionName, value => live.OptionName = value);
            Merge(live, nameof(live.Notes), live.Notes, sent.Notes, option.Notes, value => live.Notes = value);
        }

        void Merge<T>(object row, string field, T live, T sent, T value, Action<T> apply) {
            var identity = new FieldIdentifier(row, field);
            if (Revision(identity) == submitted.Revisions.GetValueOrDefault(identity) && EqualityComparer<T>.Default.Equals(live, sent)) {
                apply(value);
            }
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

    private static ProjectEditorModel Clone(ProjectEditorModel source) => new() {
        Id = source.Id,
        ExpectedLifetimeId = source.ExpectedLifetimeId,
        ExpectedProjectAdmission = source.ExpectedProjectAdmission,
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
