using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.TestLab;

namespace CanDoItAll.TestLab.UI;

public enum TestLabSection { Overview, Cases, Evidence, Runs }
public enum TestLabReadState { NotLoaded, Loading, Ready, Empty, Missing, Unavailable, Stale }
public enum TestLabSaveState { Ready, Pending, Refused, Saved, SavedWithWarning, Unknown }

public sealed record TestLabProjectOption(Guid Id, string Name, ProjectWriteAdmission Admission);
public sealed record TestLabPartyOption(Guid PartyId, string DisplayName);
public sealed record TestLabReceipt(TestLabSaveState State, Guid? PlanId, string Message);

// The plan list filters; the host decides where an applied filter is recorded.
public sealed record TestLabPlanFilter(string Search = "", Guid? ProjectId = null, string Phase = "", TestCaseStatus? Result = null) {
    public bool IsEmpty => Search.Length == 0 && ProjectId is null && Phase.Length == 0 && Result is null;
}

public interface ITestLabWorkspaceView {
    TestLabWorkspaceState State { get; }
    Task NewAsync();
    Task SelectAsync(Guid id);
    void SelectSection(TestLabSection section) => State.Section = section;
    void ApplyFilter(TestLabPlanFilter filter) => State.Filter = filter;
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
    public TestLabPlanFilter Filter { get; set; } = new();
    public TestLabReceipt? Receipt { get; set; }

    public IReadOnlyList<TestPlanSummary> VisiblePlans => Plans.Where(plan =>
        (string.IsNullOrWhiteSpace(Filter.Search) || plan.Title.Contains(Filter.Search, StringComparison.OrdinalIgnoreCase) ||
            plan.Phase.Contains(Filter.Search, StringComparison.OrdinalIgnoreCase) || ProjectName(plan).Contains(Filter.Search, StringComparison.OrdinalIgnoreCase)) &&
        (Filter.ProjectId is null || plan.ProjectId == Filter.ProjectId && Projects.Any(project =>
            project.Id == plan.ProjectId && project.Admission.LifetimeId == plan.ProjectLifetimeId)) &&
        (string.IsNullOrWhiteSpace(Filter.Phase) || string.Equals(plan.Phase, Filter.Phase, StringComparison.OrdinalIgnoreCase)) &&
        (Filter.Result is null || plan.LatestResult == Filter.Result)).OrderByDescending(plan => plan.UpdatedAtUtc).ToArray();

    public string ProjectName(TestPlanSummary plan) => plan.ProjectId is null ? "No project" :
        Projects.FirstOrDefault(project => project.Id == plan.ProjectId && project.Admission.LifetimeId == plan.ProjectLifetimeId)?.Name ?? "Unavailable project";
}
