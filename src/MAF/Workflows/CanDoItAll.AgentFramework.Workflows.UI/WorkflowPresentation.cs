using System.Collections.Immutable;

namespace CanDoItAll.AgentFramework.Workflows.UI;

public enum WorkflowTab { Dashboard, Catalog, Editor, History, Analytics }
public enum WorkflowAction {
    Refresh, CreateStarter, OpenAgents, ChangeTab, SelectDefinition, ToggleTree, OpenTemplates, Publish,
    TestInputChanged, RunTest, CancelRun, SelectRun, RunDetails, EventDetails, RunPage, EventPage,
    ResponseChanged, Respond, TemplateSearch, SelectTemplate, PreviewTemplate
}
public readonly record struct WorkflowTreeKey(string Value);
public readonly record struct WorkflowTemplateKey(string Value);
public sealed record WorkflowIntent(long Revision, WorkflowAction Action, Guid? TargetId = null,
    string? Text = null, int Delta = 0, WorkflowTab Tab = WorkflowTab.Dashboard,
    WorkflowTreeKey? TreeKey = null, WorkflowTemplateKey? TemplateKey = null);
public sealed record WorkflowBadge(string Text, string Tone);
public sealed record WorkflowMetric(string Label, string Value, string TestId);
public sealed record WorkflowTreeEntry(WorkflowTreeKey Key, string Text, string Icon, bool Expanded, bool Selected,
    ImmutableArray<WorkflowTreeEntry> Children, string Tooltip = "", bool Disabled = false, bool Selectable = true,
    string Badge = "", string TestId = "", string ChildrenTestId = "");
public sealed record WorkflowDefinitionView(Guid Id, string Name, string Description, string Status, string Backend,
    int NodeCount, int EdgeCount, bool CanPublish);
public sealed record WorkflowValidationView(string Code, string Message);
public sealed record WorkflowRunView(Guid Id, string State, string Tone, string Backend, string UpdatedAt,
    string Summary, bool IsSelected = false, bool CanCancel = false, string Duration = "");
public sealed record WorkflowEventView(Guid Id, string Kind, string Tone, string CreatedAt, string Summary);
public sealed record WorkflowArtifactView(string Name, string Kind, string ContentType, string Summary);
public sealed record WorkflowRequestView(Guid Id, string Kind, string EventName, string RequestJson, string ResponseJson, bool IsBusy) {
    public const int MaximumJsonLength = 16_384;
}
public sealed record WorkflowPager(int Index, int TotalPages, int TotalCount, string Text) {
    public bool HasPrevious => Index > 0;
    public bool HasNext => Index + 1 < TotalPages;
}

public sealed record WorkflowCatalogPresentation {
    public long Revision { get; init; }
    public bool IsBusy { get; init; }
    public int DefinitionCount { get; init; }
    public ImmutableArray<WorkflowTreeEntry> Tree { get; init; } = [];
    public WorkflowDefinitionView? Definition { get; init; }
    public bool DetailLoading { get; init; }
    public bool DetailUnavailable { get; init; }
    public string ValidationText { get; init; } = "Deferred";
    public string ValidationTone { get; init; } = "neutral";
    public ImmutableArray<WorkflowValidationView> Issues { get; init; } = [];
    public string SettingsSummary { get; init; } = "";
}

public sealed record WorkflowHistoryPresentation {
    public long Revision { get; init; }
    public bool IsBusy { get; init; }
    public bool IsLoading { get; init; }
    public bool RunUnavailable { get; init; }
    public bool CanTest { get; init; }
    public bool IsRunningTest { get; init; }
    public string TestInputJson { get; init; } = "{}";
    public bool? TestSucceeded { get; init; }
    public string TestMessage { get; init; } = "";
    public string RunText { get; init; } = "No run selected";
    public string RunTone { get; init; } = "neutral";
    public WorkflowRunView? SelectedRun { get; init; }
    public ImmutableArray<WorkflowRunView> Runs { get; init; } = [];
    public ImmutableArray<WorkflowEventView> Events { get; init; } = [];
    public ImmutableArray<WorkflowArtifactView> Artifacts { get; init; } = [];
    public ImmutableArray<WorkflowRequestView> Requests { get; init; } = [];
    public WorkflowPager RunPage { get; init; } = new(0, 1, 0, "");
    public WorkflowPager EventPage { get; init; } = new(0, 1, 0, "");
}

public sealed record WorkflowShellPresentation {
    public long Revision { get; init; }
    public WorkflowTab ActiveTab { get; init; }
    public bool IsLoading { get; init; }
    public bool IsBusy { get; init; }
    public string ErrorMessage { get; init; } = "";
    public string DefinitionCount { get; init; } = "0";
    public string ComponentCount { get; init; } = "-";
    public string RunCount { get; init; } = "-";
    public string PendingCount { get; init; } = "-";
    public string Backend { get; init; } = "";
    public string EditorTitle { get; init; } = "Workflow detail";
    public bool EditorLoading { get; init; }
    public bool EditorUnavailable { get; init; }
}

public sealed record WorkflowTemplateInput(string Label, string Description, string Key, string Kind, bool Required);
public sealed record WorkflowTemplateView(WorkflowTemplateKey Key, string Name, string Description, string NodeKinds,
    int NodeCount, int EdgeCount, int InputCount, string Backend, ImmutableArray<WorkflowTemplateInput> Inputs, bool IsSelected);
public sealed record WorkflowTemplatePresentation {
    public long Revision { get; init; }
    public bool IsLoading { get; init; }
    public string ErrorMessage { get; init; } = "";
    public string Search { get; init; } = "";
    public string Seed { get; init; } = "-";
    public int TotalCount { get; init; }
    public ImmutableArray<WorkflowTemplateView> Templates { get; init; } = [];
    public WorkflowTemplateView? Selected { get; init; }
}
