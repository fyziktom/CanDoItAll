using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages.Components;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Projects.UiSandbox;

public enum ProjectsScenario { Portfolio, LargePortfolio, Empty, ReadFailure, MissingReference, RejectedSave, UnknownSeed, PartialDeletion, CleanupHistory, DefinitionConflict, PendingRecovery }

public sealed class ProjectsScenarioStore {
    public static readonly Guid ProfileId = Guid.Parse("01010101-1111-2222-3333-010101010101");
    public static readonly Guid RootId = Guid.Parse("02020202-1111-2222-3333-020202020202");
    public static readonly Guid ChildId = Guid.Parse("03030303-1111-2222-3333-030303030303");
    private static readonly DateTimeOffset ScenarioTime = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly Dictionary<Guid, ProjectEditorModel> projects = [];
    private TaskCompletionSource? gate;
    private TaskCompletionSource? readGate;
    private bool conflictPrepared;

    public ProjectsScenarioStore(ProjectsScenario scenario) {
        Scenario = scenario;
        if (scenario == ProjectsScenario.Empty) {
            return;
        }
        projects.Add(RootId, Sample(RootId, "Aurora delivery", ProjectStatus.Active));
        projects.Add(ChildId, Sample(ChildId, "Aurora discovery", ProjectStatus.Draft));
        Hierarchy = [new(RootId, ChildId, ScenarioTime)];
        if (scenario == ProjectsScenario.LargePortfolio) {
            for (int index = 0; index < 200; index++) {
                Guid id = Guid.NewGuid();
                projects.Add(id, Sample(id, $"Portfolio project {index + 1:D3}", (ProjectStatus)(index % 5)));
            }
        }
        if (scenario == ProjectsScenario.MissingReference) {
            Hierarchy = [.. Hierarchy, new(RootId, Guid.NewGuid(), ScenarioTime)];
        }
    }

    public ProjectsScenario Scenario { get; }
    public int Writes { get; private set; }
    public int SeedBatches { get; private set; }
    public bool IsHeld => gate is not null;
    public bool IsReadHeld => readGate is not null;
    public IReadOnlyList<ProjectHierarchyLinkSummary> Hierarchy { get; private set; } = [];
    public IReadOnlyList<ProjectSummary> Summaries => projects.Values.Select(model => new ProjectSummary(
        model.Id!.Value, model.Name, model.Status, model.CurrentPhase, model.Phases.Count,
        Hierarchy.Count(link => link.ChildProjectId == model.Id), Hierarchy.Count(link => link.ParentProjectId == model.Id), ScenarioTime,
        PrimaryCustomerName: "Synthetic customer", RelatedParties: [new(ProjectPartyPortfolioCategory.Customer, "Customer", "Synthetic customer", true)]) {
        ExpectedProjectAdmission = model.ExpectedProjectAdmission
    }).ToArray();

    public ProjectEditorDraft Acquire(Guid? id) {
        if (Scenario == ProjectsScenario.ReadFailure && id.HasValue) {
            throw new InvalidOperationException("The synthetic project read was rejected. Reset the scenario to acquire an editor.");
        }
        if (id is { } value) {
            var acquired = new ProjectEditorDraft(new ProjectEditorDraft(projects[value]).Capture().Model);
            if (Scenario == ProjectsScenario.DefinitionConflict && !conflictPrepared) {
                conflictPrepared = true;
                var current = projects[value];
                current.ExpectedDefinition = new(current.ExpectedDefinition!.Project, Guid.NewGuid());
                current.Name = "Changed by another synthetic editor";
                current.Phases[0].Goal = "Concurrent phase goal";
                current.Options.Single(row => row.Category == ProjectOptionCategory.Language).OptionName = "Concurrent language";
            }
            return acquired;
        }
        return new(new() {
            Options = Enum.GetValues<ProjectOptionCategory>().Where(category => category != ProjectOptionCategory.Other)
                .Select(category => new ProjectOptionEditorModel { Category = category }).ToList()
        });
    }

    public async Task<ProjectEditorDraft> AcquireAsync(Guid? id) {
        if (readGate is not null) {
            await readGate.Task;
        }
        return Acquire(id);
    }

    public async Task<ProjectDefinitionSnapshot> ReadDefinitionAsync(Guid id) {
        var model = (await AcquireAsync(id)).Model;
        return new(model.ExpectedDefinition!,
            new(model.Name, model.Description, model.Objective, model.Status, model.CurrentPhase, model.TargetDateUtc),
            model.ExternalNamespace is not null && model.ExternalKey is not null ? new(model.ExternalNamespace, model.ExternalKey) : null,
            model.Phases.Select(row => new ProjectDefinitionPhase(row.Id!.Value, row.Name, row.Goal, row.Status, row.StartDateUtc, row.EndDateUtc)).ToArray(),
            model.Options.Where(row => !string.IsNullOrWhiteSpace(row.OptionName) || !string.IsNullOrWhiteSpace(row.Notes))
                .Select(row => new ProjectDefinitionOption(row.Id!.Value, row.Category, row.OptionName, row.Notes)).ToArray());
    }

    public void HoldNextRead() => readGate ??= new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void ReleaseRead() {
        var held = readGate;
        readGate = null;
        held?.TrySetResult();
    }

    public void HoldNextSave() => gate ??= new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Release() {
        var held = gate;
        gate = null;
        held?.TrySetResult();
    }

    public async Task<Result<ProjectEditorAcknowledgement>> SaveAsync(ProjectEditorModel model) {
        model = new ProjectEditorDraft(model).Capture().Model;
        if (gate is not null) {
            await gate.Task;
        }
        if (Scenario == ProjectsScenario.RejectedSave) {
            return Result<ProjectEditorAcknowledgement>.Failure(Error.Validation("Synthetic owner rejected this save."));
        }
        Guid id = model.Id ?? Guid.NewGuid();
        ProjectEditorModel? current = null;
        if (model.Id.HasValue) {
            if (!projects.TryGetValue(id, out current)) {
                return Result<ProjectEditorAcknowledgement>.Failure(Error.Failure("The synthetic project no longer exists.", ProjectErrorCodes.NotFound));
            }
            if (model.ExpectedDefinition is null) {
                return Result<ProjectEditorAcknowledgement>.Failure(Error.Validation("The original definition expectation is required.", ProjectErrorCodes.DefinitionExpectationRequired));
            }
            if (current.ExpectedDefinition != model.ExpectedDefinition) {
                return Result<ProjectEditorAcknowledgement>.Failure(Error.Validation("The synthetic project definition changed.", ProjectErrorCodes.DefinitionConflict));
            }
        }
        var admission = current is null ? new ProjectWriteAdmission(ProfileId, id, Guid.NewGuid()) : current.ExpectedDefinition!.Project;
        var externalNamespace = model.ExternalNamespace ?? current?.ExternalNamespace;
        var externalKey = model.ExternalKey ?? current?.ExternalKey;
        var externalIdentity = externalNamespace is not null && externalKey is not null
            ? new ProjectDefinitionExternalIdentity(externalNamespace.Trim().ToLowerInvariant(), externalKey.Trim().ToLowerInvariant())
            : null;
        var ack = new ProjectEditorAcknowledgement(new(admission, Guid.NewGuid()), model.Name.Trim(), model.Description.Trim(), model.Objective.Trim(),
            model.Status, model.CurrentPhase.Trim(), model.TargetDateUtc,
            model.Phases.Select((row, index) => new ProjectPhaseAcknowledgement(index, row.Id ?? Guid.NewGuid(), row.Name.Trim(), row.Goal.Trim(), row.Status, row.StartDateUtc, row.EndDateUtc)).ToArray(),
            model.Options.Select((row, index) => (row, index)).Where(item => !string.IsNullOrWhiteSpace(item.row.OptionName) || !string.IsNullOrWhiteSpace(item.row.Notes))
                .Select(item => new ProjectOptionAcknowledgement(item.index, item.row.Id ?? Guid.NewGuid(), item.row.Category, item.row.OptionName.Trim(), item.row.Notes.Trim())).ToArray(), externalIdentity);
        var stored = new ProjectEditorDraft(model);
        stored.Acknowledge(stored.Capture(), ack);
        projects[id] = stored.Model;
        Writes++;
        return Result<ProjectEditorAcknowledgement>.Success(ack);
    }

    public void Seed() => SeedBatches++;

    public void Delete(Guid id) {
        projects.Remove(id);
        Hierarchy = Hierarchy.Where(link => link.ParentProjectId != id && link.ChildProjectId != id).ToArray();
        Writes++;
    }

    private static ProjectEditorModel Sample(Guid id, string name, ProjectStatus status) => new() {
        Id = id,
        ExpectedLifetimeId = id,
        ExpectedProjectAdmission = new(ProfileId, id, id),
        ExpectedDefinition = new(new(ProfileId, id, id), Guid.NewGuid()),
        Name = name,
        Description = "A synthetic portfolio used to exercise the shared Projects renderers.",
        Objective = "Keep ownership and editing state explicit.",
        Status = status,
        CurrentPhase = "Discovery",
        TargetDateUtc = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc),
        Phases = [new() { Id = Guid.NewGuid(), Name = "Discovery", Goal = "Agree on scope", Status = ProjectPhaseStatus.Active },
            new() { Id = Guid.NewGuid(), Name = "Delivery", Goal = "Validate the result" }],
        Options = Enum.GetValues<ProjectOptionCategory>().Where(category => category != ProjectOptionCategory.Other)
            .Select(category => new ProjectOptionEditorModel { Id = category == ProjectOptionCategory.Language ? Guid.NewGuid() : null,
                Category = category, OptionName = category == ProjectOptionCategory.Language ? "C#" : string.Empty }).ToList()
    };
}
