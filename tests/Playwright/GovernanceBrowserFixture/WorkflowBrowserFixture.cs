using System.Text.Json;
using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.SharedKernel;
using CanDoItAll.Modules.Workbench;

internal enum WorkflowBrowserMode { Normal, HoldDefinition, HoldRun, HoldEvents, HoldTest, HoldResponse, HoldCurator, Failed, HumanInput, ProjectInput }

internal sealed class WorkflowBrowserFixture {
    public static readonly WorkflowId A = new(Guid.Parse("53000000-0000-0000-0000-000000000001"));
    public static readonly WorkflowId B = new(Guid.Parse("53000000-0000-0000-0000-000000000002"));
    public static readonly Guid ProjectId = Guid.Parse("53000000-0000-0000-0000-000000000003");
    public static readonly WorkflowRunId FirstRun = new(Guid.Parse("53000000-0000-0000-0000-000000000101"));
    private readonly Dictionary<WorkflowId, WorkflowDefinition> definitions = [];
    private readonly Dictionary<WorkflowRunId, WorkflowRunSnapshot> runs = [];
    private readonly Dictionary<WorkflowComponentId, LlmCallComponent> components = [];
    private readonly List<WorkflowExternalResponseCommand> responses = [];
    private readonly List<WorkflowTestRunRequest> tests = [];
    private TaskCompletionSource held = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int definitionReads, runReads, eventReads, pages, saves, publishes, cancellations, curatorStarts, overviewReads, analyticsReads;
    public WorkflowBrowserMode Mode { get; set; }
    public object Counters => new { definitionReads, runReads, eventReads, pages, saves, publishes, cancellations, curatorStarts,
        overviewReads, analyticsReads, tests = tests.Count, responses = responses.Count,
        lastTestWorkflow = tests.LastOrDefault()?.WorkflowId?.Value, lastTestInput = tests.LastOrDefault()?.InputJson,
        lastResponseRequest = responses.LastOrDefault()?.RequestId.Value, lastResponse = responses.LastOrDefault()?.Response.GetRawText() };
    private static DateTimeOffset Now => new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);
    private static WorkflowRuntimePolicy Policy => new(WorkflowRuntimeBackendKind.InProcess, true, false, false, false);

    public WorkflowBrowserFixture() {
        var component = new LlmCallComponent(WorkflowComponentId.New(), "Browser sample component", null, "fixture-model", WorkflowModality.Text,
            new(0.2, 800, false, ""), "Return a sample result.", WorkflowValueShape.Text, WorkflowValueShape.Text, AgentPermissionsPolicy.Default, Now, Now);
        components.Add(component.Id, component);
        definitions.Add(A, Definition(A, "Browser workflow A", component.Id));
        definitions.Add(B, Definition(B, "Browser workflow B", component.Id));
        for (var index = 0; index < 12; index++) {
            var id = new WorkflowRunId(Guid.Parse($"53000000-0000-0000-0000-{101 + index:000000000000}"));
            runs.Add(id, Run(id, definitions[A], $"Browser run A{index + 1}", Now.AddMinutes(-index)));
        }
        var bRun = new WorkflowRunId(Guid.Parse("53000000-0000-0000-0000-000000000201"));
        runs.Add(bRun, Run(bRun, definitions[B], "Browser run B", Now));
    }

    public static void Register(IServiceCollection services, WorkflowBrowserFixture fixture) {
        Add<IWorkflowCatalogService>(services, fixture);
        Add<IWorkflowComponentLibraryService>(services, fixture);
        Add<IWorkflowSettingsService>(services, fixture);
        Add<IWorkflowRuntimeManager>(services, fixture);
        Add<IWorkflowRunStore>(services, fixture);
        Add<IWorkflowTestRunner>(services, fixture);
        Add<IWorkflowExternalResponseService>(services, fixture);
        Add<IWorkflowOverviewQueryService>(services, fixture);
        Add<IWorkflowAnalyticsQueryService>(services, fixture);
        Add<IProjectStructureRuntimeGateway>(services, fixture);
        Decorate<IAgentFrameworkWorkspaceService>(services, fixture);
        Decorate<IAgentChatLauncher>(services, fixture);
    }

    private static void Add<T>(IServiceCollection services, WorkflowBrowserFixture fixture) where T : class
        => services.AddSingleton(Create<T>(fixture));

    private static T Create<T>(WorkflowBrowserFixture fixture, object? inner = null) where T : class {
        var service = DispatchProxy.Create<T, WorkflowBrowserProxy>();
        var proxy = (WorkflowBrowserProxy)(object)service;
        proxy.Fixture = fixture;
        proxy.Inner = inner;
        return service;
    }

    private static void Decorate<T>(IServiceCollection services, WorkflowBrowserFixture fixture) where T : class {
        var original = services.Last(item => item.ServiceType == typeof(T));
        services.AddScoped<T>(provider => Create<T>(fixture, original.ImplementationInstance
            ?? original.ImplementationFactory?.Invoke(provider)
            ?? ActivatorUtilities.CreateInstance(provider, original.ImplementationType!)));
    }

    public void Release() {
        var previous = held;
        held = new(TaskCreationOptions.RunContinuationsAsynchronously);
        previous.TrySetResult();
    }

    private static WorkflowDefinition Definition(WorkflowId id, string name, WorkflowComponentId component) {
        var start = new WorkflowNodeId("start");
        var call = new WorkflowNodeId("call");
        var end = new WorkflowNodeId("end");
        return new(id, WorkflowVersionId.New(), name, "Synthetic browser workflow. No provider or project command is executed.",
            WorkflowLifecycleStatus.Draft, new(start,
                [Node(start, WorkflowNodeKind.Start), Node(call, WorkflowNodeKind.LlmCall, component), Node(end, WorkflowNodeKind.End)],
                [new(new("start-call"), start, null, call, null, WorkflowEdgeKind.Direct, ""), new(new("call-end"), call, null, end, null, WorkflowEdgeKind.Direct, "")]),
            Policy, Now, Now);
    }

    private static WorkflowNode Node(WorkflowNodeId id, WorkflowNodeKind kind, WorkflowComponentId? component = null)
        => new(id, kind, id.Value, [], new(component, null, null, null, "Sample instructions", WorkflowValueShape.Text, WorkflowValueShape.Text), kind == WorkflowNodeKind.End ? 500 : 100, 150);
    private static WorkflowRunSnapshot Run(WorkflowRunId id, WorkflowDefinition definition, string summary, DateTimeOffset at)
        => new(id, definition.Id, definition.VersionId, WorkflowRunState.Completed, WorkflowRuntimeBackendKind.InProcess, "fixture-" + id.Value, summary, at.AddSeconds(-3), at) { TerminalAtUtc = at };
    private IReadOnlyList<WorkflowCatalogItem> Catalog() => definitions.Values.Select(item => new WorkflowCatalogItem(item.Id, item.VersionId,
        item.Name, item.Description, item.Status, item.RuntimePolicy.PreferredBackend, item.UpdatedAtUtc)).ToArray();

    private async Task<WorkflowDefinitionDetail?> Detail(WorkflowId id) {
        definitionReads++;
        var mode = Mode;
        if (mode == WorkflowBrowserMode.HoldDefinition && id == A) {
            await held.Task;
        }
        if (mode == WorkflowBrowserMode.Failed && id == A) {
            throw new InvalidOperationException("PRIVATE_WORKFLOW_PROVIDER_PAYLOAD");
        }
        if (!definitions.TryGetValue(id, out var definition)) {
            return null;
        }
        if (mode == WorkflowBrowserMode.ProjectInput && id == A) {
            definition = definition with { Graph = definition.Graph with { Nodes = definition.Graph.Nodes.Select(node => node.Id.Value == "call"
                ? node with { Kind = WorkflowNodeKind.Executor, Name = "Save project output", Settings = node.Settings with { ComponentId = null,
                    ExecutorId = WorkflowExecutorIds.ProjectStructure, ExecutorSettingsJson = "{\"operation\":\"CreateAsset\",\"title\":\"Sample\",\"contentFromInput\":true}" } } : node).ToArray() } };
        }
        return new(definition, new([]));
    }

    private Task<WorkflowDefinition> Save(WorkflowDefinitionSaveRequest request) {
        saves++;
        var id = request.Id ?? WorkflowId.New();
        if (request.Id.HasValue && definitions.TryGetValue(id, out var current) && current.VersionId != request.ExpectedVersionId) {
            throw new InvalidOperationException("The fixture definition version changed.");
        }
        var definition = new WorkflowDefinition(id, WorkflowVersionId.New(), request.Name, request.Description, request.Status,
            request.Graph, request.RuntimePolicy, Now, Now) { InputParameters = request.InputParameters };
        definitions[id] = definition;
        return Task.FromResult(definition);
    }

    private Task<WorkflowDefinition> Publish(WorkflowDefinitionStatusChangeRequest request) {
        publishes++;
        var current = definitions[request.WorkflowId];
        var published = current with { Status = request.Status, VersionId = WorkflowVersionId.New() };
        definitions[current.Id] = published;
        return Task.FromResult(published);
    }

    private Task<LlmCallComponent> SaveComponent(LlmCallComponentSaveRequest request) {
        var component = new LlmCallComponent(request.Id ?? WorkflowComponentId.New(), request.Name, request.ProviderProfileId, request.Model,
            request.Modality, request.ModelSettings, request.Instructions, request.InputShape, request.ResultShape, request.Permissions, Now, Now) {
            PromptArtifactId = request.PromptArtifactId, PromptVersionId = request.PromptVersionId
        };
        components[component.Id] = component;
        return Task.FromResult(component);
    }

    private async Task<WorkflowRunSnapshot?> GetRun(WorkflowRunId id) {
        runReads++;
        if (Mode == WorkflowBrowserMode.HoldRun && id == FirstRun) {
            await held.Task;
        }
        var run = runs.GetValueOrDefault(id);
        return run is not null && run.State != WorkflowRunState.Cancelled && Mode == WorkflowBrowserMode.HumanInput ? run with { State = WorkflowRunState.WaitingForInput } : run;
    }

    private Task<WorkflowListPage<WorkflowRunSnapshot>> RunPage(WorkflowRunPageRequest request) {
        pages++;
        var all = runs.Values.Where(run => !request.WorkflowId.HasValue || run.WorkflowId == request.WorkflowId).OrderByDescending(run => run.UpdatedAtUtc).ToArray();
        return Task.FromResult(new WorkflowListPage<WorkflowRunSnapshot>(all.Skip(request.PageIndex * request.PageSize).Take(request.PageSize).ToArray(), request.PageIndex, request.PageSize, all.Length));
    }

    private IReadOnlyList<WorkflowEventRecord> Events(WorkflowRunId id) => Enumerable.Range(0, 12).Select(index => {
        var kind = index switch { 0 => WorkflowEventKind.Output, 1 or 2 => WorkflowEventKind.ExecutorFailed, 3 => WorkflowEventKind.Cancelled, _ => WorkflowEventKind.SuperStep };
        var message = index is 1 or 2 ? "InvalidOperationException: PRIVATE_BROWSER_SECRET_482\n   at PRIVATE_BROWSER_STACK_482() in C:\\PRIVATE_BROWSER_PATH_482\\file.cs:line 7"
            : $"Browser event {index + 1} " + new string('x', 150) + " FULL_EVENT_TAIL";
        var payload = index switch {
            0 => "{\"result\":\"Useful <script>encoded workflow output</script>\",\"payload\":\"FULL_PAYLOAD\"}",
            1 => "{\"exception\":\"PRIVATE_BROWSER_ENVELOPE_482\"}",
            2 => JsonSerializer.Serialize(new WorkflowEventPayloadEnvelope(WorkflowEventPayloadSource.Runtime, "WorkflowExecutorFailed", null, null, null, null,
                JsonSerializer.Serialize(new WorkflowFailureDiagnosticEnvelope(WorkflowFailureKind.Executor, WorkflowFailureRetryability.RetryableAfterRepair,
                    "Settings are invalid.", "Repair the sample settings.", "Safe diagnostic: token=[REDACTED]", "PRIVATE_BROWSER_CORRELATION_482",
                    null, null, id, null, null, WorkflowFailureSourceContext.ForTemplate("private", "C:\\PRIVATE_BROWSER_TEMPLATE_482"), Now), JsonOptions),
                null, false, "C:\\PRIVATE_BROWSER_REFERENCE_482"), JsonOptions),
            3 => JsonSerializer.Serialize(new WorkflowEventPayloadEnvelope(WorkflowEventPayloadSource.Runtime, "WorkflowCancelled", null, null, null, null,
                JsonSerializer.Serialize(new WorkflowFailureDiagnosticEnvelope(WorkflowFailureKind.Cancellation, WorkflowFailureRetryability.NotRetryable,
                    "Workflow run was cancelled.", "Start a new workflow run if execution is still required.", "Workflow runtime cancellation was requested.", "PRIVATE_BROWSER_CORRELATION_482",
                    null, null, id, null, null, WorkflowFailureSourceContext.ForTemplate("private", "test-only-relative.json"), Now), JsonOptions),
                null, false, "PRIVATE_BROWSER_REFERENCE_482"), JsonOptions),
            _ => JsonSerializer.Serialize(new WorkflowEventPayloadEnvelope(WorkflowEventPayloadSource.Runtime, "UnknownInternal", null, null, null, null,
                "PRIVATE_BROWSER_INTERNAL_482", null, false, "C:\\PRIVATE_BROWSER_REFERENCE_482"), JsonOptions)
        };
        return new WorkflowEventRecord(Guid.Parse($"54000000-0000-0000-0000-{index + 1:000000000000}"), id, kind, new("call"), message, payload, Now.AddSeconds(index));
    }).ToArray();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private async Task<WorkflowListPage<WorkflowEventRecord>> EventPage(WorkflowEventPageRequest request) {
        eventReads++;
        if (Mode == WorkflowBrowserMode.HoldEvents && request.RunId == FirstRun) {
            await held.Task;
        }
        var all = Events(request.RunId);
        return new(all.Skip(request.PageIndex * request.PageSize).Take(request.PageSize).ToArray(), request.PageIndex, request.PageSize, all.Count);
    }
    private IReadOnlyList<WorkflowArtifactRecord> Artifacts(WorkflowRunId id)
        => [new(new(Guid.NewGuid()), id, WorkflowArtifactKind.Text, new("call"), "browser-review.txt", "text/plain", "  fixture\\browser-review.txt  ", "Synthetic artifact", Now),
            new(new(Guid.NewGuid()), id, WorkflowArtifactKind.Text, new("call"), "internal-review.txt", "text/plain", " C:\\PRIVATE_BROWSER_STORAGE_482\\review.txt ", "Retained artifact summary", Now)];
    private IReadOnlyList<WorkflowExternalRequestRecord> Pending(WorkflowRunId id) => Mode == WorkflowBrowserMode.HumanInput && runs[id].State != WorkflowRunState.Cancelled
        ? [new(new(Guid.Parse($"53000000-0000-0000-0000-{901 + responses.Count:000000000000}")), id, WorkflowExternalRequestKind.HumanInput, new("review"), "review", "{\"question\":\"<b>Approve sample?</b>\"}", "", Now, null)] : [];
    private async Task<WorkflowTestRunResult> Test(WorkflowTestRunRequest request) {
        tests.Add(request);
        var mode = Mode;
        if (mode == WorkflowBrowserMode.HoldTest) {
            await held.Task;
        }
        var definition = request.DraftDefinition ?? definitions[request.WorkflowId!.Value];
        var run = Run(new(Guid.NewGuid()), definition, "Browser synthetic test completed", Now.AddMinutes(tests.Count));
        runs[run.RunId] = run;
        return new(true, new([]), run, Events(run.RunId), Artifacts(run.RunId), [], "");
    }
    private async Task<WorkflowExternalResponseServiceResult> Respond(WorkflowExternalResponseCommand command) {
        responses.Add(command);
        var mode = Mode;
        if (mode == WorkflowBrowserMode.HoldResponse) {
            await held.Task;
        }
        var run = runs[FirstRun] with { State = WorkflowRunState.WaitingForInput };
        runs[FirstRun] = run;
        return new(WorkflowExternalResponseServiceOutcome.WaitingAgain, null, run, null, Pending(FirstRun).FirstOrDefault(), false, "Synthetic response accepted");
    }
    private Task<WorkflowRunSnapshot> Cancel(WorkflowRunId id) {
        cancellations++;
        var run = runs[id] with { State = WorkflowRunState.Cancelled };
        runs[id] = run;
        return Task.FromResult(run);
    }

    private Task<WorkflowOverviewSnapshot> Overview() {
        overviewReads++;
        var rows = runs.Values.ToArray();
        return Task.FromResult(new WorkflowOverviewSnapshot(Now, definitions.Count, definitions.Values.Count(item => item.Status == WorkflowLifecycleStatus.Active),
            rows.Length, 0, 0, rows.Length, 0, 100, definitions.Values.GroupBy(item => item.Status).ToDictionary(group => group.Key, group => group.Count()),
            new Dictionary<WorkflowRunState, int> { [WorkflowRunState.Completed] = rows.Length },
            new Dictionary<WorkflowRuntimeBackendKind, int> { [WorkflowRuntimeBackendKind.InProcess] = rows.Length },
            [new(A, definitions[A].Name, definitions[A].Status, 12, 0, Now)], Catalog().Take(6).ToArray(),
            rows.Take(6).Select(run => new WorkflowOverviewRecentRunRow(run, definitions[run.WorkflowId].Name)).ToArray()));
    }
    private Task<WorkflowAnalyticsSnapshot> Analytics(WorkflowAnalyticsQuery query) {
        analyticsReads++;
        var rows = runs.Values.Where(run => !query.WorkflowId.HasValue || run.WorkflowId == query.WorkflowId).ToArray();
        return Task.FromResult(new WorkflowAnalyticsSnapshot(Now, definitions.Count, 1, new Dictionary<WorkflowLifecycleStatus, int>(), rows.Length, 0, 0, 0,
            new Dictionary<WorkflowRunState, int> { [WorkflowRunState.Completed] = rows.Length }, new Dictionary<WorkflowRuntimeBackendKind, int> { [WorkflowRuntimeBackendKind.InProcess] = rows.Length },
            WorkflowUsageAnalyticsTotals.Empty, WorkflowDurationAnalyticsSummary.Empty, [], [], [], rows.Take(8).ToArray()));
    }

    private IReadOnlyList<ProjectStructureRuntimeProjectSummary> Projects() => [new(ProjectId, "Workflow browser project",
        ProjectStructureRuntimeProjectStatus.Active, "Execution", 1, 0, 0, Now)];
    private Task<ProjectStructureRuntimeReadResponse> Structure(Guid projectId) {
        var nodes = new[] { A, B }.Select(id => new ProjectStructureRuntimeNodeSummary(id.Value.ToString("N"), null,
            ProjectObjectType.WorkflowDefinition, "", definitions[id].Name, "", "Ready", null,
            $"/agents/workflows?projectId={ProjectId:D}&workflowId={id.Value:D}", "workflow-definition", id.Value, null, null, null, [], "", 0,
            "", "", "", 0, 0, null, null, ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope {
                Workflow = new ProjectWorkflowNodeMetadata { WorkflowId = id, WorkflowName = definitions[id].Name }
            }), ProjectStructureRuntimeProjectRole.ActiveProject, null, 0, null, null)).ToArray();
        return Task.FromResult(new ProjectStructureRuntimeReadResponse(projectId, "Workflow browser project", nodes, [], []));
    }

    private async Task<IReadOnlyList<AgentDefinition>> Agents(IAgentFrameworkWorkspaceService inner, bool templates, CancellationToken token) {
        var agents = await inner.ListAgentsAsync(templates, token);
        return agents.Concat([agents[0] with { Id = WorkflowCuratorAgentIdentity.AgentId, TemplateKey = WorkflowCuratorAgentIdentity.TemplateKey,
            Name = WorkflowCuratorAgentIdentity.DefaultDisplayName, AvatarImageUrl = WorkflowCuratorAgentIdentity.DefaultAvatarImageUrl }]).ToArray();
    }
    private async Task<ActiveAgentChat> Curator(Guid id) {
        if (id != WorkflowCuratorAgentIdentity.AgentId) {
            throw new InvalidOperationException("Unexpected curator identity.");
        }
        curatorStarts++;
        if (Mode == WorkflowBrowserMode.HoldCurator) {
            await held.Task;
        }
        return new(AgentChatHandleId.Create(), new(id, "Workflow Curator Agent", "Workflow specialist", null), null,
            ActiveAgentChatVisibility.Visible, ActiveAgentChatRunState.Idle, Now, Now, null);
    }

    public class WorkflowBrowserProxy : DispatchProxy {
        public WorkflowBrowserFixture Fixture { get; set; } = default!;
        public object? Inner { get; set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? arguments) {
            var method = targetMethod ?? throw new InvalidOperationException("Missing fixture method.");
            var args = arguments ?? [];
            var name = method.Name;
            var fixture = Fixture;
            if (Inner is IAgentFrameworkWorkspaceService workspace && name == nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync)) {
                return fixture.Agents(workspace, (bool)args[0]!, (CancellationToken)args[1]!);
            }
            if (Inner is IAgentChatLauncher && name == nameof(IAgentChatLauncher.StartNewChatAsync) && (Guid)args[0]! == WorkflowCuratorAgentIdentity.AgentId) {
                return fixture.Curator((Guid)args[0]!);
            }
            if (Inner is not null) {
                return method.Invoke(Inner, args);
            }
            return name switch {
                nameof(IWorkflowCatalogService.ListDefinitionsAsync) => Task.FromResult(fixture.Catalog()),
                nameof(IWorkflowCatalogService.GetDefinitionAsync) => fixture.Detail((WorkflowId)args[0]!),
                nameof(IWorkflowCatalogService.SaveDefinitionAsync) => fixture.Save((WorkflowDefinitionSaveRequest)args[0]!),
                nameof(IWorkflowCatalogService.ChangeDefinitionStatusAsync) => fixture.Publish((WorkflowDefinitionStatusChangeRequest)args[0]!),
                nameof(IWorkflowCatalogService.ValidateDefinitionAsync) => Task.FromResult(new WorkflowValidationResult([])),
                nameof(IWorkflowSettingsService.GetSettingsAsync) => Task.FromResult(WorkflowSettings.Default),
                nameof(IWorkflowComponentLibraryService.ListComponentsAsync) => Task.FromResult<IReadOnlyList<LlmCallComponent>>(fixture.components.Values.ToArray()),
                nameof(IWorkflowComponentLibraryService.ListProviderOptionsAsync) => Task.FromResult<IReadOnlyList<WorkflowProviderOption>>([]),
                nameof(IWorkflowComponentLibraryService.SaveComponentAsync) => fixture.SaveComponent((LlmCallComponentSaveRequest)args[0]!),
                nameof(IWorkflowRuntimeManager.GetRunAsync) => fixture.GetRun((WorkflowRunId)args[0]!),
                nameof(IWorkflowRuntimeManager.ListRunsAsync) => Task.FromResult<IReadOnlyList<WorkflowRunSnapshot>>(fixture.runs.Values.ToArray()),
                nameof(IWorkflowRunStore.ListRunPageAsync) => fixture.RunPage((WorkflowRunPageRequest)args[0]!),
                nameof(IWorkflowRuntimeManager.ListEventsAsync) => Task.FromResult(fixture.Events((WorkflowRunId)args[0]!)),
                nameof(IWorkflowRunStore.ListEventPageAsync) => fixture.EventPage((WorkflowEventPageRequest)args[0]!),
                nameof(IWorkflowRunStore.ListArtifactsAsync) => Task.FromResult(fixture.Artifacts((WorkflowRunId)args[0]!)),
                nameof(IWorkflowRunStore.ListPendingExternalRequestsAsync) => Task.FromResult(fixture.Pending((WorkflowRunId)args[0]!)),
                nameof(IWorkflowRuntimeManager.CancelAsync) => fixture.Cancel((WorkflowRunId)args[0]!),
                nameof(IWorkflowTestRunner.RunAsync) => fixture.Test((WorkflowTestRunRequest)args[0]!),
                nameof(IWorkflowExternalResponseService.SubmitAsync) => fixture.Respond((WorkflowExternalResponseCommand)args[0]!),
                nameof(IWorkflowOverviewQueryService.QueryAsync) when method.DeclaringType == typeof(IWorkflowOverviewQueryService) => fixture.Overview(),
                nameof(IWorkflowAnalyticsQueryService.QueryAsync) => fixture.Analytics((WorkflowAnalyticsQuery)args[0]!),
                nameof(IProjectStructureRuntimeGateway.ListProjectsAsync) => Task.FromResult(fixture.Projects()),
                nameof(IProjectStructureRuntimeGateway.ReadStructureAsync) => fixture.Structure((Guid)args[0]!),
                _ => throw new NotSupportedException($"The synthetic workflow fixture does not permit {method.DeclaringType?.Name}.{name}.")
            };
        }
    }
}
