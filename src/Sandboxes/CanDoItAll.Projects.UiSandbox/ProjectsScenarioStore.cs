using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages.Components;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Projects.UiSandbox;

public enum ProjectsScenario { Portfolio, LargePortfolio, Empty, ReadFailure, MissingReference, RejectedSave, UnknownSeed, PartialDeletion, CleanupHistory }

public sealed class ProjectsScenarioStore {
    public static readonly Guid ProfileId = Guid.Parse("01010101-1111-2222-3333-010101010101");
    public static readonly Guid RootId = Guid.Parse("02020202-1111-2222-3333-020202020202");
    public static readonly Guid ChildId = Guid.Parse("03030303-1111-2222-3333-030303030303");
    private static readonly DateTimeOffset ScenarioTime = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly Dictionary<Guid, ProjectEditorModel> projects = [];
    private TaskCompletionSource? gate;
    private TaskCompletionSource? readGate;

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
        return id is { } value ? new(new ProjectEditorDraft(projects[value]).Capture().Model) : new(new() {
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
        if (gate is not null) {
            await gate.Task;
        }
        if (Scenario == ProjectsScenario.RejectedSave) {
            return Result<ProjectEditorAcknowledgement>.Failure(Error.Validation("Synthetic owner rejected this save."));
        }
        Guid id = model.Id ?? Guid.NewGuid();
        var admission = model.ExpectedProjectAdmission ?? new(ProfileId, id, Guid.NewGuid());
        var ack = new ProjectEditorAcknowledgement(admission, model.Name.Trim(), model.Description.Trim(), model.Objective.Trim(),
            model.Status, model.CurrentPhase.Trim(), model.TargetDateUtc,
            model.Phases.Select((row, index) => new ProjectPhaseAcknowledgement(index, row.Id ?? Guid.NewGuid(), row.Name.Trim(), row.Goal.Trim(), row.Status, row.StartDateUtc, row.EndDateUtc)).ToArray(),
            model.Options.Select((row, index) => (row, index)).Where(item => !string.IsNullOrWhiteSpace(item.row.OptionName) || !string.IsNullOrWhiteSpace(item.row.Notes))
                .Select(item => new ProjectOptionAcknowledgement(item.index, item.row.Id ?? Guid.NewGuid(), item.row.Category, item.row.OptionName.Trim(), item.row.Notes.Trim())).ToArray());
        var stored = new ProjectEditorDraft(new ProjectEditorDraft(model).Capture().Model);
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
        Name = name,
        Description = "A synthetic portfolio used to exercise the shared Projects renderers.",
        Objective = "Keep ownership and editing state explicit.",
        Status = status,
        CurrentPhase = "Discovery",
        TargetDateUtc = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc),
        Phases = [new() { Id = Guid.NewGuid(), Name = "Discovery", Goal = "Agree on scope", Status = ProjectPhaseStatus.Active },
            new() { Id = Guid.NewGuid(), Name = "Delivery", Goal = "Validate the result" }],
        Options = Enum.GetValues<ProjectOptionCategory>().Where(category => category != ProjectOptionCategory.Other)
            .Select(category => new ProjectOptionEditorModel { Category = category, OptionName = category == ProjectOptionCategory.Language ? "C#" : string.Empty }).ToList()
    };
}
