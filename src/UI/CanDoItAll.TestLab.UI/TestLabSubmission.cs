using CanDoItAll.Modules.TestLab;

namespace CanDoItAll.TestLab.UI;

public sealed class TestLabSubmission {
    private readonly TestLabDraft origin;
    private readonly long targetVersion;
    private readonly TestPlanEditorModel baseline;
    public TestPlanEditorModel Model { get; }
    public Guid? CommittedId { get; private set; }
    private readonly Row<TestCaseEditorModel>[] cases;
    private readonly Row<TestEvidenceEditorModel>[] evidence;
    private readonly Row<TestRunEditorModel>[] runs;

    internal TestLabSubmission(TestLabDraft draft) {
        origin = draft;
        targetVersion = draft.TargetVersion;
        baseline = Clone(draft.Model);
        Model = Clone(baseline);
        cases = draft.Model.Cases.Select((row, index) => new Row<TestCaseEditorModel>(row, baseline.Cases[index], Model.Cases[index])).ToArray();
        evidence = draft.Model.Evidence.Select((row, index) => new Row<TestEvidenceEditorModel>(row, baseline.Evidence[index], Model.Evidence[index])).ToArray();
        runs = draft.Model.Runs.Select((row, index) => new Row<TestRunEditorModel>(row, baseline.Runs[index], Model.Runs[index])).ToArray();
    }

    public bool BelongsTo(TestLabDraft? draft) => ReferenceEquals(origin, draft) && origin.TargetVersion == targetVersion &&
        origin.Model.ProjectId == baseline.ProjectId && origin.Model.ExpectedProjectAdmission == baseline.ExpectedProjectAdmission;

    public void RetainIdentities(TestLabDraft draft, Guid id) {
        CommittedId = id;
        if (!BelongsTo(draft)) {
            return;
        }
        draft.Model.Id = id;
        foreach (var row in cases.Where(row => row.IsPresent(draft.Model.Cases))) {
            row.Origin.Id = row.Submitted.Id;
        }
        foreach (var row in evidence.Where(row => row.IsPresent(draft.Model.Evidence))) {
            row.Origin.Id = row.Submitted.Id;
        }
        foreach (var row in runs.Where(row => row.IsPresent(draft.Model.Runs))) {
            row.Origin.Id = row.Submitted.Id;
        }
    }

    public void Reconcile(TestLabDraft draft, TestPlanEditorModel accepted) {
        if (!BelongsTo(draft) || accepted.Id != CommittedId) {
            return;
        }
        var live = draft.Model;
        if (live.ResponsiblePartyId == baseline.ResponsiblePartyId) {
            live.ResponsiblePartyId = accepted.ResponsiblePartyId;
        }
        if (live.Title == baseline.Title) {
            live.Title = accepted.Title;
        }
        if (live.Phase == baseline.Phase) {
            live.Phase = accepted.Phase;
        }
        if (live.CoverageGoal == baseline.CoverageGoal) {
            live.CoverageGoal = accepted.CoverageGoal;
        }
        if (live.PlaywrightSpecPath == baseline.PlaywrightSpecPath) {
            live.PlaywrightSpecPath = accepted.PlaywrightSpecPath;
        }
        foreach (var row in cases.Where(row => row.IsPresent(live.Cases))) {
            var saved = accepted.Cases.SingleOrDefault(item => item.Id == row.Submitted.Id);
            if (saved is null) {
                continue;
            }
            if (row.Origin.Name == row.Baseline.Name) {
                row.Origin.Name = saved.Name;
            }
            if (row.Origin.StoryOrFeature == row.Baseline.StoryOrFeature) {
                row.Origin.StoryOrFeature = saved.StoryOrFeature;
            }
            if (row.Origin.Status == row.Baseline.Status) {
                row.Origin.Status = saved.Status;
            }
            if (row.Origin.Notes == row.Baseline.Notes) {
                row.Origin.Notes = saved.Notes;
            }
        }
        foreach (var row in evidence.Where(row => row.IsPresent(live.Evidence))) {
            var saved = accepted.Evidence.SingleOrDefault(item => item.Id == row.Submitted.Id);
            if (saved is null) {
                continue;
            }
            if (row.Origin.EvidenceLabel == row.Baseline.EvidenceLabel) {
                row.Origin.EvidenceLabel = saved.EvidenceLabel;
            }
            if (row.Origin.ArtifactPath == row.Baseline.ArtifactPath) {
                row.Origin.ArtifactPath = saved.ArtifactPath;
            }
            if (row.Origin.EvidenceKind == row.Baseline.EvidenceKind) {
                row.Origin.EvidenceKind = saved.EvidenceKind;
            }
            if (row.Origin.Notes == row.Baseline.Notes) {
                row.Origin.Notes = saved.Notes;
            }
        }
        foreach (var row in runs.Where(row => row.IsPresent(live.Runs))) {
            var saved = accepted.Runs.SingleOrDefault(item => item.Id == row.Submitted.Id);
            if (saved is null) {
                continue;
            }
            if (row.Origin.ExecutedAtUtc == row.Baseline.ExecutedAtUtc && !draft.HasRawTimestamp(row.Origin)) {
                row.Origin.ExecutedAtUtc = saved.ExecutedAtUtc;
            }
            if (row.Origin.Runner == row.Baseline.Runner) {
                row.Origin.Runner = saved.Runner;
            }
            if (row.Origin.Result == row.Baseline.Result) {
                row.Origin.Result = saved.Result;
            }
            if (row.Origin.Summary == row.Baseline.Summary) {
                row.Origin.Summary = saved.Summary;
            }
        }
    }

    private sealed record Row<T>(T Origin, T Baseline, T Submitted) where T : class, ITestPlanChildEditor {
        public bool IsPresent(ICollection<T> current) => current.Any(item => ReferenceEquals(item, Origin)) &&
            (Origin.Id == Baseline.Id || Origin.Id == Submitted.Id);
    }

    public static TestPlanEditorModel Clone(TestPlanEditorModel source) => new() {
        Id = source.Id,
        ProjectId = source.ProjectId,
        ExpectedProjectAdmission = source.ExpectedProjectAdmission,
        ResponsiblePartyId = source.ResponsiblePartyId,
        Title = source.Title,
        Phase = source.Phase,
        CoverageGoal = source.CoverageGoal,
        PlaywrightSpecPath = source.PlaywrightSpecPath,
        Cases = source.Cases.Select(item => new TestCaseEditorModel {
            Id = item.Id,
            Name = item.Name,
            StoryOrFeature = item.StoryOrFeature,
            Status = item.Status,
            Notes = item.Notes,
        }).ToList(),
        Evidence = source.Evidence.Select(item => new TestEvidenceEditorModel {
            Id = item.Id,
            EvidenceLabel = item.EvidenceLabel,
            ArtifactPath = item.ArtifactPath,
            EvidenceKind = item.EvidenceKind,
            Notes = item.Notes,
        }).ToList(),
        Runs = source.Runs.Select(item => new TestRunEditorModel {
            Id = item.Id,
            ExecutedAtUtc = item.ExecutedAtUtc,
            Runner = item.Runner,
            Result = item.Result,
            Summary = item.Summary,
        }).ToList(),
    };
}
