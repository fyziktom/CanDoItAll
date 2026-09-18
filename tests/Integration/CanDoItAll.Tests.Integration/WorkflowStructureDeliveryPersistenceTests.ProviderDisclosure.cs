using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Transforms;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.AgentFramework.Workflows.Runtime;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed partial class WorkflowStructureDeliveryPersistenceTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Actual_Workflow_provider_dispatch_rechecks_retained_cross_project_read_after_transform(bool revoke, bool transform) {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        var target = await CreateReadProjectAsync(fixture.Services, "Disclosure target B");
        var source = await CreateReadSourceAsync(fixture, [fixture.ProjectId, target.ProjectId]);
        var scenario = await CreateDisclosureScenarioAsync(fixture, source.Authority, target.ProjectId, transform);
        var pending = scenario.StartAsync();
        try {
            await scenario.Gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var history = await scenario.Store.ReadProviderDisclosureAsync(scenario.RunId);
            Assert.Equal(WorkflowProviderDisclosureContent.Definition(scenario.Definition), history.Declaration!.DefinitionHash);
            var read = Assert.Single(history.Completions, item => item.Proof.NodeId == scenario.ReadNode.Id);
            Assert.Single(read.Evidence);
            Assert.Contains(target.LifetimeId.ToString("D"), read.Evidence[0].PayloadJson, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, scenario.Port.Calls);
            if (transform) {
                Assert.Contains(history.Completions, item => item.Proof.NodeId.Value == "transform" && item.Evidence.Count == 0);
                Assert.DoesNotContain("Disclosure target B", scenario.Gate.Input!.PayloadJson, StringComparison.Ordinal);
            }
            if (revoke) {
                await SetReadGrantsAsync(fixture.Services, source, [fixture.ProjectId]);
            }
        } finally {
            scenario.Gate.Release.TrySetResult();
        }
        var failure = await Record.ExceptionAsync(() => pending.WaitAsync(TimeSpan.FromSeconds(30)));
        var saved = (await scenario.Store.GetRunAsync(scenario.RunId))!;
        if (revoke) {
            Assert.Equal(WorkflowRunState.Failed, saved.State);
            Assert.Equal(0, scenario.Port.Calls);
            var events = await scenario.Store.ListEventsAsync(saved.RunId);
            Assert.True((failure?.ToString() + string.Join("\n", events.Select(item => item.Message + item.PayloadJson)))
                .Contains("exact read grants", StringComparison.Ordinal));
        } else {
            Assert.Null(failure);
            Assert.Equal(WorkflowRunState.Completed, saved.State);
            Assert.Equal(1, scenario.Port.Calls);
        }
        Assert.Single((await scenario.Store.ReadProviderDisclosureAsync(saved.RunId)).Completions,
            item => item.Proof.NodeId == scenario.ReadNode.Id);
        Assert.DoesNotContain(await scenario.Store.ListEventsAsync(saved.RunId), item => item.Kind == WorkflowEventKind.ProviderReadEvidence);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Fanout_and_fanin_cannot_strip_retained_read_authority(bool revoke) {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        var target = await CreateReadProjectAsync(fixture.Services, "Fork target B");
        var source = await CreateReadSourceAsync(fixture, [fixture.ProjectId, target.ProjectId]);
        var scenario = await CreateDisclosureScenarioAsync(fixture, source.Authority, target.ProjectId, transform: true, fanOut: true);
        var pending = scenario.StartAsync();
        try {
            await scenario.Gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var history = await scenario.Store.ReadProviderDisclosureAsync(scenario.RunId);
            Assert.Single(history.Completions, item => item.Proof.NodeId == scenario.ReadNode.Id);
            if (revoke) {
                await SetReadGrantsAsync(fixture.Services, source, [fixture.ProjectId]);
            }
        } finally {
            scenario.Gate.Release.TrySetResult();
        }
        var failure = await Record.ExceptionAsync(() => pending.WaitAsync(TimeSpan.FromSeconds(30)));
        if (revoke) {
            Assert.Equal(0, scenario.Port.Calls);
            Assert.Equal(WorkflowRunState.Failed, (await scenario.Store.GetRunAsync(scenario.RunId))!.State);
        } else {
            Assert.Null(failure);
            Assert.Equal(2, scenario.Port.Calls);
            var calls = (await scenario.Store.ReadProviderDisclosureAsync(scenario.RunId)).Completions
                .Where(item => item.Proof.NodeId.Value == "provider").ToArray();
            Assert.Equal(2, calls.Length);
            Assert.Equal(2, calls.Select(item => item.Proof.Occurrence).Distinct().Count());
        }
    }

    [Fact]
    public async Task Retained_read_cannot_be_disclosed_after_same_ID_project_recreation() {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        var scenario = await CreateDisclosureScenarioAsync(fixture, fixture.Request.WorkflowMutationAdmission!.Authority, fixture.ProjectId, transform: false);
        var pending = scenario.StartAsync();
        try {
            await scenario.Gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            _ = await RecreateProjectAsync(fixture);
        } finally {
            scenario.Gate.Release.TrySetResult();
        }
        _ = await Record.ExceptionAsync(() => pending.WaitAsync(TimeSpan.FromSeconds(30)));
        Assert.Equal(WorkflowRunState.Failed, (await scenario.Store.GetRunAsync(scenario.RunId))!.State);
        Assert.Equal(0, scenario.Port.Calls);
        var evidence = Assert.Single(Assert.Single((await scenario.Store.ReadProviderDisclosureAsync(scenario.RunId)).Completions,
            item => item.Proof.NodeId == scenario.ReadNode.Id).Evidence);
        Assert.Contains(fixture.Plan.ProjectLifetime!.LifetimeId.ToString("D"), evidence.PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(DisclosureCorruption.MissingPrivatePart)]
    [InlineData(DisclosureCorruption.ChangedInvocationInput)]
    [InlineData(DisclosureCorruption.ChangedDefinition)]
    [InlineData(DisclosureCorruption.ChangedOccurrence)]
    [InlineData(DisclosureCorruption.ChangedNode)]
    public async Task Actual_provider_boundary_rejects_missing_or_caller_changed_evidence(DisclosureCorruption corruption) {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        var scenario = await CreateDisclosureScenarioAsync(fixture, fixture.Request.WorkflowMutationAdmission!.Authority, fixture.ProjectId, transform: false);
        var pending = scenario.StartAsync();
        try {
            await scenario.Gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            if (corruption == DisclosureCorruption.MissingPrivatePart) {
                await using var database = await fixture.Services.GetRequiredService<IDbContextFactory<WorkflowDbContext>>().CreateDbContextAsync();
                var rows = await database.Set<WorkflowEventRecordEntity>().Where(row => row.RunId == scenario.RunId.Value &&
                    row.Kind == WorkflowEventKind.ProviderReadEvidence).ToListAsync();
                var evidence = Assert.Single(rows, row => row.PayloadJson.Contains("workbench.structure", StringComparison.Ordinal));
                database.Remove(evidence);
                await database.SaveChangesAsync();
            } else {
                scenario.Gate.Corruption = corruption;
            }
        } finally {
            scenario.Gate.Release.TrySetResult();
        }
        _ = await Record.ExceptionAsync(() => pending.WaitAsync(TimeSpan.FromSeconds(30)));
        Assert.Equal(0, scenario.Port.Calls);
        Assert.Equal(WorkflowRunState.Failed, (await scenario.Store.GetRunAsync(scenario.RunId))!.State);
    }

    [Fact]
    public async Task Owner_read_collection_keeps_every_original_lifetime_across_bounded_private_parts() {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        var ids = new List<Guid> { fixture.ProjectId };
        for (var index = 0; index < 64; index++) {
            ids.Add((await CreateReadProjectAsync(fixture.Services, $"Disclosure list {index:D2}")).ProjectId);
        }
        var source = await CreateReadSourceAsync(fixture, ids);
        var scenario = await CreateDisclosureScenarioAsync(fixture, source.Authority, null, transform: false);
        var pending = scenario.StartAsync();
        try {
            await scenario.Gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(45));
            var read = Assert.Single((await scenario.Store.ReadProviderDisclosureAsync(scenario.RunId)).Completions,
                item => item.Proof.NodeId == scenario.ReadNode.Id);
            Assert.Equal(2, read.Evidence.Count);
            var retainedIds = read.Evidence.SelectMany(part => {
                using var json = JsonDocument.Parse(part.PayloadJson);
                return json.RootElement.GetProperty("targets").EnumerateArray().Select(target => target.GetProperty("projectId").GetGuid()).ToArray();
            }).ToArray();
            Assert.Equal(ids.Order(), retainedIds.Order());
            await SetReadGrantsAsync(fixture.Services, source, ids.Take(ids.Count - 1).ToArray());
        } finally {
            scenario.Gate.Release.TrySetResult();
        }
        _ = await Record.ExceptionAsync(() => pending.WaitAsync(TimeSpan.FromSeconds(30)));
        Assert.Equal(0, scenario.Port.Calls);
        Assert.Equal(WorkflowRunState.Failed, (await scenario.Store.GetRunAsync(scenario.RunId))!.State);
    }

    [Fact]
    public async Task Reviewed_simulation_output_remains_usable_without_a_real_Structure_read() {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        var scenario = await CreateDisclosureScenarioAsync(fixture, fixture.Request.WorkflowMutationAdmission!.Authority, fixture.ProjectId, transform: false);
        var simulation = new WorkflowPreviewSimulationStep(scenario.ReadNode.Id, WorkflowExecutorIds.ProjectStructure,
            "Reviewed synthetic preview", "{\"synthetic\":true}");
        var pending = scenario.StartAsync(new([simulation]));
        try {
            await scenario.Gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var history = await scenario.Store.ReadProviderDisclosureAsync(scenario.RunId);
            Assert.Equal(WorkflowProviderDisclosureContent.Simulation(simulation), Assert.Single(history.Declaration!.Simulations).Hash);
            var proof = Assert.Single(history.Completions, item => item.Proof.NodeId == scenario.ReadNode.Id);
            Assert.Equal(WorkflowProviderDisclosureContent.Simulation(simulation), proof.Proof.SimulationHash);
            Assert.Empty(proof.Evidence);
        } finally {
            scenario.Gate.Release.TrySetResult();
        }
        Assert.Equal(WorkflowRunState.Completed, (await pending.WaitAsync(TimeSpan.FromSeconds(30))).State);
        Assert.Equal(1, scenario.Port.Calls);
    }

    private static async Task<DisclosureScenario> CreateDisclosureScenarioAsync(Fixture fixture, WorkflowStructureAuthority original,
        Guid? target, bool transform, bool fanOut = false) {
        var services = fixture.Services;
        var component = DisclosureComponent();
        var definition = ReadDefinition(fixture.Definition, ReadSettings(target.HasValue
            ? WorkflowProjectStructureOperation.ReadTree : WorkflowProjectStructureOperation.ListProjects, target), numericSettings: false);
        var read = Assert.Single(definition.Graph.Nodes, item => item.Settings.ExecutorId == WorkflowExecutorIds.ProjectStructure);
        var end = definition.Graph.Nodes.Single(item => item.Kind == WorkflowNodeKind.End);
        var llm = new WorkflowNode(new("provider"), WorkflowNodeKind.LlmCall, "Provider disclosure", [],
            new(component.Id, null, null, null, "Summarize the reviewed Workflow input.", WorkflowValueShape.Text, WorkflowValueShape.Text));
        var nodes = definition.Graph.Nodes.ToList();
        nodes.Insert(nodes.Count - 1, llm);
        var edges = definition.Graph.Edges.Where(edge => edge.TargetNodeId != end.Id).ToList();
        if (transform) {
            var converted = new WorkflowNode(new("transform"), WorkflowNodeKind.Executor, "Drop data fields", [],
                new(null, null, null, null, "", WorkflowValueShape.Text, WorkflowValueShape.Text) {
                    ExecutorId = WorkflowExecutorIds.JsonTransform,
                    ExecutorSettingsJson = WorkflowExecutorJson.Serialize(new WorkflowJsonTransformExecutorSettings {
                        Operations = [new() { Operation = WorkflowJsonTransformOperation.Set, DestinationPath = "$", ValueJson = "{\"transformed\":true}" }]
                    })
                });
            nodes.Insert(nodes.Count - 2, converted);
            edges.Add(new(new("read-transform"), read.Id, null, converted.Id, null,
                fanOut ? WorkflowEdgeKind.FanOut : WorkflowEdgeKind.Direct, ""));
            edges.Add(new(new("transform-provider"), converted.Id, null, llm.Id, null,
                fanOut ? WorkflowEdgeKind.FanIn : WorkflowEdgeKind.Direct, ""));
            if (fanOut) {
                var second = converted with { Id = new("second-transform") };
                nodes.Insert(nodes.Count - 2, second);
                edges.Add(new(new("read-second"), read.Id, null, second.Id, null, WorkflowEdgeKind.FanOut, ""));
                edges.Add(new(new("second-provider"), second.Id, null, llm.Id, null, WorkflowEdgeKind.FanIn, ""));
            }
        } else {
            edges.Add(new(new("read-provider"), read.Id, null, llm.Id, null, WorkflowEdgeKind.Direct, ""));
        }
        edges.Add(new(new("provider-end"), llm.Id, null, end.Id, null, WorkflowEdgeKind.Direct, ""));
        definition = definition with { Graph = new(definition.Graph.StartNodeId, nodes.Select(node => node with {
            Settings = node.Settings with { InputShape = DisclosureShape, ResultShape = DisclosureShape }
        }).ToArray(), edges), Status = WorkflowLifecycleStatus.Draft };
        var authority = await services.GetRequiredService<ProjectStructureWorkflowAuthorityService>().PrepareLaunchAsync(original, definition);
        WorkflowLaunchOrigin origin = authority.Channel == WorkflowStructureAuthorityChannel.AgentExecution
            ? new WorkflowLaunchOrigin.AgentRuntimeInvocation(authority.Principal, new("provider-read-test"),
                AgentRuntimeToolProviderPurpose.InteractiveChat.ToString(), new(Guid.NewGuid()))
            : new WorkflowLaunchOrigin.Preview(authority.Principal, new("provider-read-test"));
        origin = origin with { StructureAuthority = authority };
        var store = services.GetRequiredService<IWorkflowRunStore>();
        var executor = new ProjectStructureWorkflowExecutor(services.GetRequiredService<IProjectStructureRuntimeGateway>());
        var transformExecutor = new JsonTransformWorkflowExecutor();
        var catalog = new WorkflowExecutorCatalog([executor, transformExecutor]);
        var gate = new DisclosureGate(new WorkflowProviderInputAdmission(store, catalog,
            [new WorkflowStructureProviderDisclosurePolicy(services.GetRequiredService<ProjectStructureWorkflowAuthorityService>())]));
        var port = new DisclosurePort();
        var llmInvoker = new WorkflowLlmComponentInvoker(port, new DisclosureProviderSource(), new ProviderProfileService(), gate);
        var compiler = new MafWorkflowCompiler(new WorkflowDefinitionValidator(catalog),
            new WorkflowExecutorInvoker(catalog, [executor, transformExecutor]), llmInvoker, executorCatalog: catalog);
        var backend = new MafInProcessWorkflowExecutionBackend(compiler, [component]);
        var runtime = new WorkflowRuntimeManager([backend], store, new WorkflowActiveRunRegistry(), TimeProvider.System,
            services.GetRequiredService<IWorkflowExternalRequestBoundaryStore>(), services.GetRequiredService<IWorkflowResumeBoundaryStore>(),
            usageStore: services.GetRequiredService<IWorkflowUsageObservationStore>());
        return new(runtime, store, definition, read, origin, WorkflowRunId.New(), gate, port);
    }

    private static readonly WorkflowValueShape DisclosureShape = new(WorkflowValueShapeKind.Object, "{}", "Workflow data");

    private static LlmCallComponent DisclosureComponent() => new(WorkflowComponentId.New(), "Synthetic provider component", null,
        "disclosure-test", WorkflowModality.Text, new(Temperature: 0, MaxOutputTokens: 100, RequireJsonOutput: false, ResponseFormatJsonSchema: ""), "Summarize input.",
        DisclosureShape, DisclosureShape, AgentPermissionsPolicy.Default, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    public enum DisclosureCorruption { MissingPrivatePart, ChangedInvocationInput, ChangedDefinition, ChangedOccurrence, ChangedNode }

    private sealed class DisclosurePause {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class DisclosureGate(IWorkflowProviderInputAdmission inner, DisclosurePause? sharedPause = null) : IWorkflowProviderInputAdmission {
        private readonly DisclosurePause pause = sharedPause ?? new();
        public TaskCompletionSource Entered => pause.Entered;
        public TaskCompletionSource Release => pause.Release;
        public WorkflowNodeInput? Input { get; private set; }
        public DisclosureCorruption? Corruption { get; set; }
        public async ValueTask RequireAsync(WorkflowDefinition definition, WorkflowNode node, WorkflowNodeInput input,
            CancellationToken cancellationToken = default) {
            Input = input;
            Entered.TrySetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(45), cancellationToken);
            await inner.RequireAsync(Corruption == DisclosureCorruption.ChangedDefinition ? definition with { Name = "Caller substituted graph" } : definition,
                Corruption == DisclosureCorruption.ChangedNode ? node with { Id = new("caller-selected") } : node,
                Corruption switch {
                    DisclosureCorruption.ChangedInvocationInput => new("{\"forged\":true}") { ExecutionOccurrence = input.ExecutionOccurrence },
                    DisclosureCorruption.ChangedOccurrence => input with { ExecutionOccurrence = WorkflowExecutionOccurrence.Start(WorkflowRunId.New()) },
                    _ => input
                }, cancellationToken);
        }
    }

    private sealed class DisclosurePort : ILlmInvocationPort {
        private int calls;
        public int Calls => Volatile.Read(ref calls);
        public Task<LlmInvocationResult> InvokeAsync(LlmInvocationRequest request, CancellationToken cancellationToken = default) {
            Interlocked.Increment(ref calls);
            return Task.FromResult(new LlmInvocationResult(request.Model, "Reviewed summary", new(1, 1, 0)));
        }
    }

    private sealed class DisclosureProviderSource : IProviderRuntimeProfileSource {
        private readonly ProviderProfile provider = new(Guid.NewGuid(), "Synthetic disclosure provider", ProviderKind.OpenAi,
            "https://example.invalid/v1", "SYNTHETIC_WORKFLOW_DISCLOSURE_KEY", "disclosure-test", ProviderTransportKind.ChatCompletions,
            true, false, false, true, false, "{}", "", "Not checked", null, ["disclosure-test"]);
        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProviderProfile>>([provider]);
        public Task<ProviderProfile?> GetProviderAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<ProviderProfile?>(id == provider.Id ? provider : null);
    }

    private sealed record DisclosureScenario(WorkflowRuntimeManager Runtime, IWorkflowRunStore Store, WorkflowDefinition Definition,
        WorkflowNode ReadNode, WorkflowLaunchOrigin Origin, WorkflowRunId RunId, DisclosureGate Gate, DisclosurePort Port) {
        public Task<WorkflowRunSnapshot> StartAsync(WorkflowPreviewSimulationPlan? simulations = null) => Runtime.StartAsync(Definition,
            new(Definition.Id, Definition.VersionId, "{}", WorkflowRuntimeBackendKind.InProcess, null, null) {
                Origin = Origin, RequestedRunId = RunId, PreviewSimulationPlan = simulations ?? WorkflowPreviewSimulationPlan.Empty
            });
    }

    private sealed class ReadProofProgressObserver(IWorkflowRunStore store) : IWorkflowNodeExecutionProgressObserver {
        public WorkflowReadEvidenceDurability ReadEvidenceDurability => WorkflowReadEvidenceDurability.Persisted;
        public ValueTask RecordAsync(WorkflowNodeExecutionProgress progress, CancellationToken cancellationToken = default) {
            if (progress.State != WorkflowNodeExecutionProgressState.Completed) {
                return ValueTask.CompletedTask;
            }
            return new(store.SaveEventAsync(new(Guid.NewGuid(), progress.RunId!.Value, WorkflowEventKind.ExecutorCompleted,
                progress.NodeId, "Actual SDK completion", WorkflowEventPayloads.Serialize(WorkflowEventPayloadSource.CanDoItAllProgress,
                    "WorkflowNodeCompleted", progress.NodeId, progress.ExecutorId, inlineJson: progress.PayloadJson), progress.OccurredAtUtc) {
                        CompletionProof = progress.CompletionProof, ProviderReadEvidence = progress.ProviderReadEvidence
                    }, cancellationToken));
        }
    }
}
