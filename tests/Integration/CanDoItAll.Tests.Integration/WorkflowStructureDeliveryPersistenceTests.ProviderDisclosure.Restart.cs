using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.AgentFramework.Workflows.Runtime;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed partial class WorkflowStructureDeliveryPersistenceTests {
    [Theory]
    [InlineData(false, false, NativeSimulationMode.None)]
    [InlineData(false, true, NativeSimulationMode.None)]
    [InlineData(true, false, NativeSimulationMode.None)]
    [InlineData(true, true, NativeSimulationMode.None)]
    [InlineData(true, false, NativeSimulationMode.Retained)]
    [InlineData(true, true, NativeSimulationMode.Retained)]
    [InlineData(true, false, NativeSimulationMode.MissingPrivateAdmission)]
    public async Task Native_checkpoint_restart_retains_original_reads_and_simulations_before_provider_dispatch(bool readAfterWait, bool revoke,
        NativeSimulationMode simulationMode) {
        await using var environment = CanDoItAllTestEnvironment.Create("workflow-disclosure-restart");
        var profile = environment.CreatePostgreSqlProfile("primary");
        var port = new DisclosurePort();
        var pause = new DisclosurePause();
        WorkflowRunSnapshot started;
        WorkflowExternalRequestRecord request;
        WorkflowNodeId readNodeId;
        ReadAgentSource source;
        Guid retainedProject;
        Guid targetProject;
        Guid? firstCompletion = null;
        WorkflowPreviewSimulationStep? simulation = null;
        void Configure(IServiceCollection services) {
            RemoveAutomaticDelivery(services);
            services.RemoveAll<ILlmInvocationPort>();
            services.AddSingleton<ILlmInvocationPort>(port);
            services.RemoveAll<IProviderRuntimeProfileSource>();
            services.AddSingleton<IProviderRuntimeProfileSource>(new DisclosureProviderSource());
            services.RemoveAll<IWorkflowProviderInputAdmission>();
            services.AddScoped<IWorkflowProviderInputAdmission>(provider => new DisclosureGate(new WorkflowProviderInputAdmission(
                provider.GetRequiredService<IWorkflowRunStore>(), provider.GetRequiredService<IWorkflowExecutorCatalog>(),
                provider.GetServices<IWorkflowProviderDisclosurePolicy>()), pause));
        }
        await using (var first = await TestApplication.CreateAsync(new() { TestEnvironment = environment, ActiveProfile = profile, ConfigureServices = Configure })) {
            await using var fixture = await CreateFixtureAsync(first);
            retainedProject = fixture.ProjectId;
            targetProject = (await CreateReadProjectAsync(fixture.Services, "Restart target B")).ProjectId;
            source = await CreateReadSourceAsync(fixture, [retainedProject, targetProject]);
            var component = DisclosureComponent();
            component = await fixture.Services.GetRequiredService<IWorkflowComponentLibraryService>().SaveComponentAsync(
                new(null, component.Name, null, component.Model, component.Modality, component.ModelSettings, component.Instructions,
                    component.InputShape, component.ResultShape, component.Permissions));
            var definition = ReadDefinition(fixture.Definition, ReadSettings(WorkflowProjectStructureOperation.ReadTree, targetProject), false);
            var startNode = definition.Graph.Nodes.Single(node => node.Kind == WorkflowNodeKind.Start);
            var readNode = definition.Graph.Nodes.Single(node => node.Settings.ExecutorId == WorkflowExecutorIds.ProjectStructure);
            readNodeId = readNode.Id;
            var endNode = definition.Graph.Nodes.Single(node => node.Kind == WorkflowNodeKind.End);
            var wait = new WorkflowNode(new("review"), WorkflowNodeKind.HumanInput, "Review before provider", [],
                new(null, null, null, WorkflowExternalRequestKind.HumanInput, "Provide reviewed JSON.", WorkflowValueShape.Text, WorkflowValueShape.Text));
            var llm = new WorkflowNode(new("provider"), WorkflowNodeKind.LlmCall, "Provider after recovery", [],
                new(component.Id, null, null, null, "Summarize the reviewed input.", WorkflowValueShape.Text, WorkflowValueShape.Text));
            var nodes = readAfterWait ? new[] { startNode, wait, readNode, llm, endNode } : new[] { startNode, readNode, wait, llm, endNode };
            var edges = nodes.Zip(nodes.Skip(1), (left, right) => new WorkflowEdge(new($"{left.Id.Value}-{right.Id.Value}"),
                left.Id, null, right.Id, null, WorkflowEdgeKind.Direct, "")).ToArray();
            definition = await fixture.Services.GetRequiredService<IWorkflowCatalogService>().SaveDefinitionAsync(new(null, null,
                "Native disclosure restart", "Actual saved graph and native checkpoint", WorkflowLifecycleStatus.Draft,
                new(startNode.Id, nodes.Select(node => node with {
                    Settings = node.Settings with { InputShape = DisclosureShape, ResultShape = DisclosureShape }
                }).ToArray(), edges), definition.RuntimePolicy));
            var authority = await fixture.Services.GetRequiredService<ProjectStructureWorkflowAuthorityService>().PrepareLaunchAsync(source.Authority, definition);
            var scope = authority.AgentGovernance!.WorkspaceScope;
            var origin = new WorkflowLaunchOrigin.AgentRuntimeInvocation(authority.Principal, new("native-disclosure"),
                AgentRuntimeToolProviderPurpose.InteractiveChat.ToString(), new(Guid.NewGuid())) {
                    StructureAuthority = authority, AuthorizationScope = scope,
                    AuthorizationPolicyFingerprint = WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint
                };
            simulation = simulationMode == NativeSimulationMode.None ? null : new(readNode.Id, WorkflowExecutorIds.ProjectStructure,
                "Retained synthetic read", "{\"syntheticAfterWait\":true}");
            started = await fixture.Services.GetRequiredService<IWorkflowRuntimeManager>().StartAsync(definition,
                new(definition.Id, definition.VersionId, "{}", WorkflowRuntimeBackendKind.InProcess, null, null) {
                    Origin = origin, PreviewSimulationPlan = simulation is null ? WorkflowPreviewSimulationPlan.Empty : new([simulation])
                });
            Assert.Equal(WorkflowRunState.WaitingForInput, started.State);
            var store = fixture.Services.GetRequiredService<IWorkflowRunStore>();
            request = Assert.Single(await store.ListPendingExternalRequestsAsync(started.RunId), item => item.EffectiveState == WorkflowExternalRequestState.Pending);
            Assert.Equal(WorkflowProviderDisclosureProtocol.Current, request.Continuation!.CompilerContractVersion);
            var history = await store.ReadProviderDisclosureAsync(started.RunId);
            if (simulation is not null) {
                Assert.Equal(simulation, Assert.Single(history.Declaration!.Simulations).Step);
            }
            if (readAfterWait) {
                Assert.DoesNotContain(history.Completions, item => item.Proof.NodeId == readNodeId);
            } else {
                firstCompletion = Assert.Single(history.Completions, item => item.Proof.NodeId == readNodeId).Proof.CompletionId;
            }
            Assert.Equal(0, port.Calls);
            if (simulationMode == NativeSimulationMode.MissingPrivateAdmission) {
                await using var database = await fixture.Services.GetRequiredService<IDbContextFactory<WorkflowDbContext>>().CreateDbContextAsync();
                var privateRows = await database.Set<WorkflowEventRecordEntity>().Where(row => row.RunId == started.RunId.Value &&
                    row.Kind == WorkflowEventKind.ProviderReadEvidence).ToListAsync();
                Assert.NotEmpty(privateRows);
                database.RemoveRange(privateRows);
                await database.SaveChangesAsync();
            }
        }
        await using var second = await TestApplication.CreateAsync(new() { TestEnvironment = environment, ActiveProfile = profile, ConfigureServices = Configure });
        await using var secondScope = second.Services.CreateAsyncScope();
        var services = secondScope.ServiceProvider;
        var actor = services.GetRequiredService<IWorkflowExternalResponseActorContextFactory>().CreateLocalOperator();
        using var response = JsonDocument.Parse("{\"reviewed\":true}");
        var pending = services.GetRequiredService<IWorkflowExternalResponseService>().SubmitAsync(new(actor, request.Id, request.Version,
            response.RootElement, new("disclosure-restart-response"), new("disclosure-restart")));
        if (simulationMode == NativeSimulationMode.MissingPrivateAdmission) {
            var missing = await pending.WaitAsync(TimeSpan.FromSeconds(45));
            Assert.NotEqual(WorkflowExternalResponseServiceOutcome.Completed, missing.Outcome);
            Assert.False(pause.Entered.Task.IsCompleted);
            Assert.Equal(0, port.Calls);
            Assert.DoesNotContain(await services.GetRequiredService<IWorkflowRunStore>().ListEventsAsync(started.RunId),
                item => item.Kind == WorkflowEventKind.ExecutorCompleted && item.NodeId == readNodeId);
            return;
        }
        try {
            await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(45));
            var history = await services.GetRequiredService<IWorkflowRunStore>().ReadProviderDisclosureAsync(started.RunId);
            var read = Assert.Single(history.Completions, item => item.Proof.NodeId == readNodeId);
            if (simulation is null) {
                Assert.Single(read.Evidence);
                Assert.Contains(targetProject.ToString("D"), read.Evidence[0].PayloadJson, StringComparison.OrdinalIgnoreCase);
            } else {
                Assert.Empty(read.Evidence);
                Assert.Equal(WorkflowProviderDisclosureContent.Simulation(simulation), read.Proof.SimulationHash);
                Assert.Equal(simulation, Assert.Single(history.Declaration!.Simulations).Step);
            }
            if (firstCompletion.HasValue) {
                Assert.Equal(firstCompletion.Value, read.Proof.CompletionId);
            }
            if (revoke) {
                await SetReadGrantsAsync(services, source, [retainedProject]);
            }
        } finally {
            pause.Release.TrySetResult();
        }
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(45));
        if (revoke && simulation is null) {
            Assert.NotEqual(WorkflowExternalResponseServiceOutcome.Completed, result.Outcome);
            Assert.Equal(0, port.Calls);
        } else {
            Assert.Equal(WorkflowExternalResponseServiceOutcome.Completed, result.Outcome);
            Assert.Equal(1, port.Calls);
        }
        Assert.Single((await services.GetRequiredService<IWorkflowRunStore>().ReadProviderDisclosureAsync(started.RunId)).Completions,
            item => item.Proof.NodeId == readNodeId);
        var publicEvents = await services.GetRequiredService<IWorkflowRunStore>().ListEventsAsync(started.RunId);
        Assert.DoesNotContain(publicEvents, item => item.Kind == WorkflowEventKind.ProviderReadEvidence);
        Assert.DoesNotContain("definitionHash", JsonSerializer.Serialize(publicEvents, WorkflowExecutorJson.Options), StringComparison.OrdinalIgnoreCase);
    }

    public enum NativeSimulationMode { None, Retained, MissingPrivateAdmission }
}
