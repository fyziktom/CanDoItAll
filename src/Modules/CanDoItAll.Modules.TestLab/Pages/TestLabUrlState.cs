using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.TestLab.UI;

namespace CanDoItAll.Modules.TestLab.Pages;

// /test-lab: the selected plan and its editor section, the project that seeds new drafts, and the applied
// plan list filters. Drafts and unsaved edits never appear here.
public sealed record TestLabUrlState(
    Guid? PlanId = null,
    TestLabSection Section = TestLabSection.Overview,
    Guid? ProjectId = null,
    string Search = "",
    Guid? ProjectFilter = null,
    string Phase = "",
    TestCaseStatus? Result = null) {
    public TestLabPlanFilter Filter => new(Search, ProjectFilter, Phase, Result);

    public static TestLabUrlState From(Guid? planId, TestLabSection section, Guid? projectId, TestLabPlanFilter filter)
        => new(planId, section, projectId, filter.Search, filter.ProjectId, filter.Phase, filter.Result);
}

public sealed class TestLabUrlCodec : IUrlStateCodec<TestLabUrlState> {
    public const string PlanKey = "planId";
    public const string SectionKey = "section";
    public const string ProjectKey = "projectId";
    public const string SearchKey = "q";
    public const string ProjectFilterKey = "project";
    public const string PhaseKey = "phase";
    public const string ResultKey = "result";
    public const int MaximumTextLength = 200;

    public static TestLabUrlCodec Instance { get; } = new();

    public static UrlTokenMap<TestLabSection> Sections { get; } = new(
        (TestLabSection.Overview, "overview"),
        (TestLabSection.Cases, "cases"),
        (TestLabSection.Evidence, "evidence"),
        (TestLabSection.Runs, "runs"));

    public static UrlTokenMap<TestCaseStatus> Results { get; } = new(
        (TestCaseStatus.Planned, "planned"),
        (TestCaseStatus.Implemented, "implemented"),
        (TestCaseStatus.Passed, "passed"),
        (TestCaseStatus.Failed, "failed"),
        (TestCaseStatus.Blocked, "blocked"));

    public IReadOnlyList<string> Keys { get; } = [PlanKey, SectionKey, ProjectKey, SearchKey, ProjectFilterKey, PhaseKey, ResultKey];

    public TestLabUrlState Read(UrlQuery query) => new(
        query.GetId(PlanKey),
        query.GetToken(SectionKey, Sections, TestLabSection.Overview),
        query.GetId(ProjectKey),
        query.GetText(SearchKey, MaximumTextLength) ?? string.Empty,
        query.GetId(ProjectFilterKey),
        query.GetText(PhaseKey, MaximumTextLength) ?? string.Empty,
        query.GetToken(ResultKey, Results));

    public void Write(TestLabUrlState state, UrlQueryWriter query) {
        query.Id(PlanKey, state.PlanId);
        query.Token(SectionKey, state.Section, Sections, TestLabSection.Overview);
        query.Id(ProjectKey, state.ProjectId);
        query.Text(SearchKey, Bounded(state.Search));
        query.Id(ProjectFilterKey, state.ProjectFilter);
        query.Text(PhaseKey, Bounded(state.Phase));
        query.Token(ResultKey, state.Result, Results);
    }

    // Text that Read would drop is never written, so the written form always reads back.
    private static string? Bounded(string? text)
        => text?.Trim() is { Length: > 0 and <= MaximumTextLength } bounded ? bounded : null;
}
