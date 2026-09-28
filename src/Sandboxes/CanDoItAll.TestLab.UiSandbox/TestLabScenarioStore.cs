using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.TestLab.UI;

namespace CanDoItAll.TestLab.UiSandbox;

public sealed class TestLabScenarioStore {
    private readonly Dictionary<Guid, TestPlanEditorModel> plans = [];
    public IReadOnlyList<TestLabProjectOption> Projects { get; }
    public Guid PartyId { get; } = Guid.NewGuid();
    public int Commits { get; private set; }

    public TestLabScenarioStore(int count) {
        var profile = Guid.NewGuid();
        Projects = Enumerable.Range(1, 3).Select(index => {
            var id = Guid.NewGuid();
            return new TestLabProjectOption(id, $"Delivery project {index}", new ProjectWriteAdmission(profile, id, Guid.NewGuid()));
        }).ToArray();
        for (var index = 0; index < count; index++) {
            var project = Projects[index % Projects.Count];
            var plan = new TestPlanEditorModel {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                ExpectedProjectAdmission = project.Admission,
                ResponsiblePartyId = PartyId,
                Title = $"Release {index + 1:000} acceptance",
                Phase = index % 2 == 0 ? "Release" : "Discovery",
                CoverageGoal = "Verify the complete delivery flow, retained evidence, and explicit error recovery.",
                PlaywrightSpecPath = "tests/browser/release.spec.ts",
                Cases = [new() { Id = Guid.NewGuid(), Name = "Create and reopen", StoryOrFeature = "Delivery acceptance", Status = TestCaseStatus.Implemented, Notes = "Retain all identities." }],
                Evidence = [new() { Id = Guid.NewGuid(), EvidenceLabel = "Desktop viewport", ArtifactPath = "artifacts/release/desktop.png", Notes = "1600 × 1000" }],
                Runs = [new() { Id = Guid.NewGuid(), ExecutedAtUtc = new(2026, 9, 28, 10, 23, 45, TimeSpan.FromHours(-4)), Result = (TestCaseStatus)(index % 5), Summary = "Recorded acceptance run" }]
            };
            plans.Add(plan.Id.Value, plan);
        }
    }

    public IReadOnlyList<TestPlanSummary> List() => plans.Values.Select((plan, index) => new TestPlanSummary(plan.Id!.Value,
        plan.ProjectId, plan.Title, plan.Phase, plan.Cases.Count, plan.Evidence.Count,
        plan.Runs.OrderByDescending(run => run.ExecutedAtUtc).FirstOrDefault()?.Result, DateTimeOffset.UtcNow.AddMinutes(-index)) {
        ProjectLifetimeId = plan.ExpectedProjectAdmission?.LifetimeId
    }).ToArray();

    public TestPlanEditorModel? Read(Guid id) => plans.TryGetValue(id, out var plan) ? TestLabSubmission.Clone(plan) : null;

    public void MakeReferencesUnavailable() {
        foreach (var plan in plans.Values) {
            plan.ExpectedProjectAdmission = new(plan.ExpectedProjectAdmission!.DatabaseProfileId, plan.ProjectId!.Value, Guid.NewGuid());
        }
    }

    public Guid Commit(TestPlanEditorModel submitted) {
        submitted.Id ??= Guid.NewGuid();
        var previous = Read(submitted.Id.Value);
        SetIds(submitted.Cases, previous?.Cases);
        SetIds(submitted.Evidence, previous?.Evidence);
        SetIds(submitted.Runs, previous?.Runs);
        var saved = TestLabSubmission.Clone(submitted);
        saved.Title = saved.Title.Trim();
        saved.Phase = saved.Phase.Trim();
        saved.CoverageGoal = saved.CoverageGoal.Trim();
        saved.PlaywrightSpecPath = saved.PlaywrightSpecPath.Trim();
        foreach (var item in saved.Cases) {
            item.Name = item.Name.Trim();
            item.StoryOrFeature = item.StoryOrFeature.Trim();
            item.Notes = item.Notes.Trim();
        }
        foreach (var item in saved.Evidence) {
            item.EvidenceLabel = item.EvidenceLabel.Trim();
            item.ArtifactPath = item.ArtifactPath.Trim();
            item.EvidenceKind = string.IsNullOrWhiteSpace(item.EvidenceKind) ? "Screenshot" : item.EvidenceKind.Trim();
            item.Notes = item.Notes.Trim();
        }
        foreach (var item in saved.Runs) {
            item.Runner = string.IsNullOrWhiteSpace(item.Runner) ? "Playwright" : item.Runner.Trim();
            item.Summary = item.Summary.Trim();
        }
        plans[saved.Id!.Value] = saved;
        Commits++;
        return saved.Id.Value;
    }

    private static void SetIds<T>(IEnumerable<T> submitted, IEnumerable<T>? previous) where T : ITestPlanChildEditor {
        var existing = previous?.Select(item => item.Id).ToHashSet() ?? [];
        foreach (var row in submitted) {
            if (row.Id is null || !existing.Contains(row.Id)) {
                row.Id = Guid.NewGuid();
            }
        }
    }
}
