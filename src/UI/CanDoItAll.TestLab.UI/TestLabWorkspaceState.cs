using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.TestLab;

namespace CanDoItAll.TestLab.UI;

public enum TestLabSection { Overview, Cases, Evidence, Runs }
public enum TestLabReadState { NotLoaded, Loading, Ready, Empty, Missing, Unavailable, Stale }
public enum TestLabSaveState { Ready, Pending, Refused, Saved, SavedWithWarning, Unknown }

public sealed record TestLabProjectOption(Guid Id, string Name, ProjectWriteAdmission Admission);
public sealed record TestLabPartyOption(Guid PartyId, string DisplayName);
public sealed record TestLabReceipt(TestLabSaveState State, Guid? PlanId, string Message);

public interface ITestLabWorkspaceView {
    TestLabWorkspaceState State { get; }
    Task NewAsync();
    Task SelectAsync(Guid id);
    Task ChangeProjectAsync(TestLabDraft origin, Guid? projectId, long? targetVersion = null);
    Task ChangePartyAsync(TestLabDraft origin, Guid? partyId, long? targetVersion = null);
    Task SaveAsync(TestLabDraft origin, long? targetVersion = null);
    Task RetryAsync();
}

public sealed class TestLabWorkspaceState {
    public TestLabDraft? Draft { get; set; }
    public TestLabSection Section { get; set; }
    public IReadOnlyList<TestPlanSummary> Plans { get; set; } = [];
    public IReadOnlyList<TestLabProjectOption> Projects { get; set; } = [];
    public IReadOnlyList<TestLabPartyOption> Parties { get; set; } = [];
    public TestLabReadState PlansRead { get; set; }
    public TestLabReadState ProjectsRead { get; set; }
    public TestLabReadState EditorRead { get; set; }
    public TestLabReadState PartiesRead { get; set; }
    public string Search { get; set; } = string.Empty;
    public Guid? ProjectFilter { get; set; }
    public string PhaseFilter { get; set; } = string.Empty;
    public TestCaseStatus? ResultFilter { get; set; }
    public TestLabReceipt? Receipt { get; set; }

    public IReadOnlyList<TestPlanSummary> VisiblePlans => Plans.Where(plan =>
        (string.IsNullOrWhiteSpace(Search) || plan.Title.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
            plan.Phase.Contains(Search, StringComparison.OrdinalIgnoreCase) || ProjectName(plan).Contains(Search, StringComparison.OrdinalIgnoreCase)) &&
        (ProjectFilter is null || plan.ProjectId == ProjectFilter && Projects.Any(project =>
            project.Id == plan.ProjectId && project.Admission.LifetimeId == plan.ProjectLifetimeId)) &&
        (string.IsNullOrWhiteSpace(PhaseFilter) || string.Equals(plan.Phase, PhaseFilter, StringComparison.OrdinalIgnoreCase)) &&
        (ResultFilter is null || plan.LatestResult == ResultFilter)).OrderByDescending(plan => plan.UpdatedAtUtc).ToArray();

    public string ProjectName(TestPlanSummary plan) => plan.ProjectId is null ? "No project" :
        Projects.FirstOrDefault(project => project.Id == plan.ProjectId && project.Admission.LifetimeId == plan.ProjectLifetimeId)?.Name ?? "Unavailable project";

    public void ResetFilters() {
        Search = string.Empty;
        ProjectFilter = null;
        PhaseFilter = string.Empty;
        ResultFilter = null;
    }
}
