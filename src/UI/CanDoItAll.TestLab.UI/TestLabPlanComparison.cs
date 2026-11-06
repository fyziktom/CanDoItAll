using CanDoItAll.Editing;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.TestLab;

namespace CanDoItAll.TestLab.UI;

public sealed class TestLabPlanComparison {
    private readonly RowReview<TestCaseEditorModel, CaseValue> cases;
    private readonly RowReview<TestEvidenceEditorModel, EvidenceValue> evidence;
    private readonly RowReview<TestRunEditorModel, RunValue> runs;

    public TestLabPlanComparison(TestLabDraft draft, TestPlanEditorModel current) {
        var original = draft.Baseline;
        var local = draft.Model;
        Revision = draft.Revision;
        Current = current;
        Title = new(original.Title, local.Title, current.Title);
        Phase = new(original.Phase, local.Phase, current.Phase);
        Coverage = TextMerge.Compare(original.CoverageGoal, local.CoverageGoal, current.CoverageGoal).Field;
        Spec = new(original.PlaywrightSpecPath, local.PlaywrightSpecPath, current.PlaywrightSpecPath);
        Project = new(ProjectValue.From(original), ProjectValue.From(local), ProjectValue.From(current));
        cases = new(original.Cases, local.Cases, current.Cases, CaseValue.From, value => value.Key);
        evidence = new(original.Evidence, local.Evidence, current.Evidence, EvidenceValue.From, value => value.Key);
        runs = new(original.Runs, local.Runs, current.Runs,
            (key, row) => RunValue.From(key, row, draft.HasRawTimestamp(row) ? draft.Timestamp(row) : null), value => value.Key);
    }

    public long Revision { get; }
    public TestPlanEditorModel Current { get; }
    public MergeField<string> Title { get; }
    public MergeField<string> Phase { get; }
    public MergeField<string> Coverage { get; }
    public MergeField<string> Spec { get; }
    public MergeField<ProjectValue> Project { get; }
    public IReadOnlyList<KeyedMergeRow<Guid, CaseValue>> Cases => cases.Rows;
    public IReadOnlyList<KeyedMergeRow<Guid, EvidenceValue>> Evidence => evidence.Rows;
    public IReadOnlyList<KeyedMergeRow<Guid, RunValue>> Runs => runs.Rows;
    public bool IsResolved => Title.IsResolved && Phase.IsResolved && Coverage.IsResolved && Spec.IsResolved && Project.IsResolved &&
        cases.IsResolved && evidence.IsResolved && runs.IsResolved;

    internal void Apply(TestPlanEditorModel model) {
        var acceptedCases = cases.Resolve(value => value.ToModel());
        var acceptedEvidence = evidence.Resolve(value => value.ToModel());
        var acceptedRuns = runs.Resolve(value => value.ToModel());
        var project = Project.Result;
        model.Id = Current.Id;
        model.ExpectedPlan = Current.ExpectedPlan;
        model.OriginalRuns = Current.OriginalRuns;
        model.ProjectId = project.Id;
        model.ExpectedProjectAdmission = project.Admission;
        model.ResponsiblePartyId = project.PartyId;
        model.Title = Title.Result;
        model.Phase = Phase.Result;
        model.CoverageGoal = Coverage.Result;
        model.PlaywrightSpecPath = Spec.Result;
        model.Cases = acceptedCases;
        model.Evidence = acceptedEvidence;
        model.Runs = acceptedRuns;
    }

    public sealed record ProjectValue(Guid? Id, ProjectWriteAdmission? Admission, Guid? PartyId) {
        internal static ProjectValue From(TestPlanEditorModel model) => new(model.ProjectId, model.ExpectedProjectAdmission, model.ResponsiblePartyId);
    }

    public sealed record CaseValue(Guid Key, Guid? Id, string Name, string Feature, TestCaseStatus Status, string Notes) {
        internal static CaseValue From(Guid key, TestCaseEditorModel row) => new(key, row.Id, row.Name, row.StoryOrFeature, row.Status, row.Notes);
        internal TestCaseEditorModel ToModel() => new() { Id = Id, Name = Name, StoryOrFeature = Feature, Status = Status, Notes = Notes };
    }

    public sealed record EvidenceValue(Guid Key, Guid? Id, string Label, string Path, string Kind, string Notes) {
        internal static EvidenceValue From(Guid key, TestEvidenceEditorModel row) => new(key, row.Id, row.EvidenceLabel, row.ArtifactPath, row.EvidenceKind, row.Notes);
        internal TestEvidenceEditorModel ToModel() => new() { Id = Id, EvidenceLabel = Label, ArtifactPath = Path, EvidenceKind = Kind, Notes = Notes };
    }

    public sealed record RunValue(Guid Key, Guid? Id, DateTimeOffset ExecutedAt, string? RawTime, string Runner, TestCaseStatus Result, string Summary) {
        internal static RunValue From(Guid key, TestRunEditorModel row, string? rawTime) => new(key, row.Id, row.ExecutedAtUtc, rawTime, row.Runner, row.Result, row.Summary);
        internal TestRunEditorModel ToModel() => new() { Id = Id, ExecutedAtUtc = ExecutedAt, Runner = Runner, Result = Result, Summary = Summary };
    }

    private sealed class RowReview<TModel, TValue> where TModel : class, ITestPlanChildEditor where TValue : class {
        private readonly Dictionary<Guid, (TModel Model, TValue Value)> draft;

        public RowReview(IReadOnlyList<TModel> original, IReadOnlyList<TModel> local, IReadOnlyList<TModel> current,
            Func<Guid, TModel, TValue> capture, Func<TValue, Guid> key) {
            draft = local.Select(row => (Model: row, Value: capture(row.Id ?? Guid.NewGuid(), row))).ToDictionary(row => key(row.Value));
            Rows = CollectionMerge.ByKey(original.Select(row => capture(row.Id ?? Guid.NewGuid(), row)).ToArray(),
                draft.Values.Select(row => row.Value).ToArray(), current.Select(row => capture(row.Id ?? Guid.NewGuid(), row)).ToArray(), key);
        }

        public IReadOnlyList<KeyedMergeRow<Guid, TValue>> Rows { get; }
        public bool IsResolved => Rows.All(row => row.Field.IsResolved);

        public List<TModel> Resolve(Func<TValue, TModel> create) {
            var result = new List<TModel>();
            foreach (var row in Rows) {
                if (row.Field.Result.Value is not { } value) {
                    continue;
                }
                var model = draft.TryGetValue(row.Key, out var existing) && EqualityComparer<TValue>.Default.Equals(value, existing.Value)
                    ? existing.Model : create(value);
                if (!row.Field.Current.Exists) {
                    model.Id = null;
                }
                result.Add(model);
            }
            return result;
        }
    }
}
