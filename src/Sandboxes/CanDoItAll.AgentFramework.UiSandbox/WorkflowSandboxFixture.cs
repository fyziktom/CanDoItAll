using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Workflows.UI;

namespace CanDoItAll.AgentFramework.UiSandbox;

public enum WorkflowScenario {
    Loading, EmptyDefinitions, CatalogReady, DefinitionLoading, DefinitionUnavailable, ValidationIssues,
    PublishedDefinition, EmptyHistory, RunHistory, PendingInput, Templates, DashboardAnalytics
}

public sealed class WorkflowSandboxFixture {
    public WorkflowScenario Scenario { get; private set; }
    public WorkflowShellPresentation Shell { get; private set; } = new();
    public WorkflowCatalogPresentation Catalog { get; private set; } = new();
    public WorkflowHistoryPresentation History { get; private set; } = new();
    public WorkflowTemplatePresentation Templates { get; private set; } = new();
    public WorkflowOverviewPresentation Overview { get; private set; } = new();
    public WorkflowAnalyticsPresentation Analytics { get; private set; } = new();
    public bool TemplatesOpen { get; private set; }
    public string IntentLog { get; private set; } = "No intent";
    private long revision;
    private static readonly Guid DefinitionId = Guid.Parse("52000000-0000-0000-0000-000000000001");
    private static readonly Guid RunId = Guid.Parse("52000000-0000-0000-0000-000000000002");
    private static readonly Guid RequestId = Guid.Parse("52000000-0000-0000-0000-000000000003");

    public WorkflowSandboxFixture() => SetScenario(WorkflowScenario.CatalogReady);

    public void SetScenario(WorkflowScenario scenario) {
        Scenario = scenario;
        var timestamp = WorkflowPresentationTime.Format(new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero));
        revision++;
        TemplatesOpen = scenario == WorkflowScenario.Templates;
        var history = scenario is WorkflowScenario.EmptyHistory or WorkflowScenario.RunHistory or WorkflowScenario.PendingInput;
        Shell = new() {
            Revision = revision, ActiveTab = history ? WorkflowTab.History : scenario == WorkflowScenario.DashboardAnalytics ? WorkflowTab.Dashboard : WorkflowTab.Catalog,
            DefinitionCount = "1", ComponentCount = "2", RunCount = "1", PendingCount = scenario == WorkflowScenario.PendingInput ? "1" : "0", Backend = "InProcess",
            IsLoading = scenario == WorkflowScenario.Loading
        };
        Catalog = new() {
            Revision = revision, DefinitionCount = 1,
            Definition = new(DefinitionId, "Review incoming material", "A sample workflow with clear human review.", "Draft", "InProcess", 3, 2, true),
            Tree = [new(new("sample-workflow"), "Review incoming material", "account_tree", true, true, [])],
            ValidationText = "Valid", ValidationTone = "success", SettingsSummary = "In-process preview is enabled."
        };
        if (scenario == WorkflowScenario.EmptyDefinitions) {
            Catalog = Catalog with { DefinitionCount = 0, Definition = null, Tree = [] };
            Shell = Shell with { DefinitionCount = "0" };
        }
        if (scenario == WorkflowScenario.DefinitionLoading) {
            Catalog = Catalog with { DetailLoading = true };
        }
        if (scenario == WorkflowScenario.DefinitionUnavailable) {
            Catalog = Catalog with { DetailUnavailable = true };
        }
        if (scenario == WorkflowScenario.ValidationIssues) {
            Catalog = Catalog with { ValidationText = "1 issue", ValidationTone = "danger",
                Issues = [new("MissingComponent", "Choose a component for the review node.")] };
        }
        if (scenario == WorkflowScenario.PublishedDefinition) {
            Catalog = Catalog with { Definition = Catalog.Definition! with { Status = "Active", CanPublish = false } };
        }
        var run = new WorkflowRunView(RunId, scenario == WorkflowScenario.PendingInput ? "WaitingForInput" : "Completed",
            "success", "InProcess", timestamp, "Review completed with an encoded <script>untrusted()</script> sample.", true, scenario == WorkflowScenario.PendingInput);
        History = new() {
            Revision = revision, CanTest = true, TestInputJson = "{\"topic\":\"sample\"}", RunText = run.State, RunTone = run.Tone,
            SelectedRun = scenario == WorkflowScenario.EmptyHistory ? null : run,
            Runs = scenario == WorkflowScenario.EmptyHistory ? [] : [run],
            Events = scenario == WorkflowScenario.EmptyHistory ? [] : [new(Guid.Parse("52000000-0000-0000-0000-000000000004"), "Completed", "success", timestamp, "Review result is available.")],
            Artifacts = scenario == WorkflowScenario.EmptyHistory ? [] : [new("review.txt", "Text", "text/plain", "A sample review artifact.")],
            Requests = scenario == WorkflowScenario.PendingInput ? [new(RequestId, "HumanInput", "review", "{\"question\":\"Continue?\"}", "{\"approved\":true}", false)] : [],
            RunPage = new(0, 2, 9, "Page 1 of 2 - 9 runs"), EventPage = new(0, 2, 9, "Page 1 of 2 - 9 events")
        };
        var template = new WorkflowTemplateView(new("review-template"), "Human review", "Review an incoming document.", "Start → Human review → End",
            3, 2, 1, "InProcess", [new("Document", "Text to review", "document", "Text", true)], true);
        Templates = new() { Revision = revision, TotalCount = 1, Seed = "sample", Templates = [template], Selected = template };
        Overview = new() { Revision = revision, State = WorkflowQueryState.Ready, AsOf = timestamp,
            Metrics = [new("Definitions", "1", "workflow-overview-definition-count"), new("Runs", "9", "workflow-overview-run-count")],
            TopWorkflows = [new("Review incoming material", "Active", "success", timestamp, "9", "0 failures")],
            RecentRuns = [new("Review incoming material", "Review completed.", timestamp, "Completed", "success")],
            Lifecycle = [new("Active: 1", "success")], RunStates = [new("Completed", 9)], Backends = [new("In process", 9)] };
        Analytics = new() { Revision = revision, State = WorkflowQueryState.Ready, AsOf = timestamp, ScopeDescription = "All workflows",
            Workflows = [new(DefinitionId, "Review incoming material")], SelectedWorkflow = DefinitionId,
            RuntimeMetrics = [new("Runs", "9", "workflow-analytics-run-count")], UsageMetrics = [new("Total tokens", "1200", "workflow-analytics-total-tokens")],
            DurationMetrics = [new("Average", "00:00:03", "workflow-analytics-duration-average")],
            Distribution = [new("State", "Completed", "9", "success")], Providers = [new("Sample provider", "sample-model", "Sample", "9", "1200", "$0.002000", "0")],
            RecentRuns = [run with { Duration = "00:00:03" }] };
    }

    public void Apply(WorkflowIntent intent) {
        IntentLog = $"{intent.Action}: {intent.TargetId}";
        switch (intent.Action) {
            case WorkflowAction.ChangeTab:
                Shell = Shell with { ActiveTab = intent.Tab };
                break;
            case WorkflowAction.TestInputChanged:
                History = History with { TestInputJson = intent.Text ?? "" };
                break;
            case WorkflowAction.ResponseChanged:
                History = History with { Requests = History.Requests.Select(request => request.Id == intent.TargetId
                    ? request with { ResponseJson = intent.Text ?? "" } : request).ToImmutableArray() };
                break;
            case WorkflowAction.Publish:
                Catalog = Catalog with { Definition = Catalog.Definition! with { Status = "Active", CanPublish = false } };
                break;
            case WorkflowAction.OpenTemplates:
                TemplatesOpen = true;
                break;
            case WorkflowAction.TemplateSearch:
                Templates = Templates with { Search = intent.Text ?? "" };
                break;
            case WorkflowAction.RunTest:
                History = History with { TestSucceeded = true, TestMessage = "Sample intent received." };
                break;
        }
    }

    public void Query(WorkflowQueryIntent intent) {
        IntentLog = $"{intent.Action}: {intent.Scope}";
        Analytics = Analytics with { Scope = intent.Scope, SelectedWorkflow = intent.WorkflowId ?? Analytics.SelectedWorkflow };
    }

    public void CloseTemplates() => TemplatesOpen = false;
}
