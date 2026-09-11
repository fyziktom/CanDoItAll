using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    [Fact]
    public async Task Registered_background_node_start_uses_distinct_dynamic_proposals_and_preserves_original_source_actor() {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(ToolProducerHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var original = await CreateClaimedExecutionAsync(services, fixture, clock, persistExecution: false);
        var store = Assert.IsType<FileSandboxWorkspaceStore>(services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>());
        var journal = new AgentToolAdmissionJournal(store, NativeProfile(services), backgroundSources: [NativeBackgroundPolicy(services, fixture, clock)]);
        original = original with { ToolAdmission = await journal.CreateForNewBackgroundRunAsync(original, "Start the selected node process twice deliberately.") };
        await store.SaveExecutionRunDetailAsync(new(original, null, [], []));
        var node = await services.GetRequiredService<ProjectWorkbenchService>().CreateObjectAsync(fixture.Project.ProjectId,
            new(ProjectObjectType.ProjectBlock, "Background launch target", "", "", $"project:{fixture.Project.ProjectId}"));
        var input = new ProjectStructureProcessStartProposal(fixture.Project.ProjectId, node.Id,
            new(ProcessDefinitionId: ProcessDefinitionCatalogProjectionService.CreateDefinitionId(new("software-delivery")).Value,
                RunHrMatch: false, Execute: false, IncludeLaunchPlan: true));
        var context = await ToolProducerContextAsync(fixture, original);
        Assert.Null(context.Governance);
        Assert.NotEqual(fixture.Agent.Id, context.Agent.Id);
        Assert.Empty(AgentProjectStructureAccessMetadata.Read(context.Agent.ConfigurationJson).AllowedProjectLifetimes);
        var provider = Assert.Single(services.GetServices<IAgentRuntimeToolProvider>().OfType<ProjectStructureAgentRuntimeToolProvider>());
        using var audit = WorkspaceExecutionAuditContext.BeginScope(original);
        await using var lease = await journal.AcquireRunAsync(original.ToolAdmission!.Session.Reference, default);
        using var bound = lease.Bind();
        var tools = await provider.CreateToolsAsync(context, default);
        var metadata = Assert.Single(provider.GetToolMetadata(context), item =>
            item.ToolName == ProjectStructureToolPolicy.ProjectStructureNodeProcessStart);
        var payload = metadata.PrepareAdmission!(JsonSerializer.SerializeToElement(input, ProjectStructureProcessProposalCodec.SerializerOptions));
        Assert.Equal(AgentToolProposalRecovery.OwnerReceipt, payload.Recovery);
        await journal.BeginSegmentAsync(lease, CanDoItAll.Tests.Integration.Runtime.AgentToolAdmissionJournalFixture.Envelope(), null, default);
        var batch = Assert.Single((await journal.AdmitBatchAsync(lease, payload.Digest,
            CanDoItAll.Tests.Integration.Runtime.AgentToolAdmissionJournalFixture.Envelope(),
            [new("first", payload, false), new("second", payload, false)], default)).Batches);
        var tool = Assert.Single(tools.OfType<AIFunction>(), item => item.Name == ProjectStructureToolPolicy.ProjectStructureNodeProcessStart);
        var results = new List<ProjectStructureProcessNodeStartResult>();
        foreach (var call in new[] { "first", "second" }) {
            var claim = await journal.ClaimInvocationAsync(lease, batch.Id, call, payload, default);
            using var dispatch = claim.Bind();
            using var effect = AgentToolInvocationEffectScope.Begin();
            var wireResult = Assert.IsType<JsonElement>(await tool.InvokeAsync(new AIFunctionArguments {
                ["projectId"] = input.ProjectId, ["nodeId"] = input.NodeId, ["request"] = input.Request
            }));
            var result = wireResult.Deserialize<ProjectStructureProcessNodeStartResult>(ProjectStructureProcessProposalCodec.ResultSerializerOptions)!;
            Assert.NotNull(result);
            results.Add(result);
            var saved = (await services.GetRequiredService<IProcessPreparedLaunchStore>().FindByIntentAsync(new(claim.Proposal.IntentId.Value)))!;
            Assert.NotNull(saved.AcceptedAtUtc);
            Assert.Equal(saved.Preparation.AdmissionId, result.Observation!.AdmissionId);
            Assert.Equal(saved.Preparation.InitialCommit.Mutation.State.RunId.Value, result.RunId);
            Assert.Equal(original.Id, saved.Preparation.ToolSource!.Execution.Evidence.ExecutionRunId);
            Assert.Equal(context.Agent.Id, saved.Preparation.ToolSource.Execution.Evidence.ExecutorAgentId);
            Assert.Equal(fixture.Agent.Id, Assert.IsType<ProcessLaunchPrincipal.AgentExecution>(saved.Preparation.Authority!.Principal).Ceiling.AgentId);
            Assert.Equal(claim.Proposal.IntentId.Value, saved.Preparation.ToolSource.IntentId.Value);
            Assert.Equal(payload.Digest.Value, saved.Preparation.ToolSource.ProposalFingerprint);
            Assert.Equal(original.ToolAdmission.Session.Reference.BackgroundSource!.ExecutionFingerprint.Value,
                saved.Preparation.ToolSource.ExecutionFingerprint);
            Assert.Equal(new AgentToolCommittedEffect(ProjectStructureProcessToolAdmission.EffectSourceKind,
                saved.Preparation.AdmissionId.Value.ToString("D")), effect.CommittedEffect);
            await journal.CompleteInvocationAsync(claim, CanDoItAll.Tests.Integration.Runtime.AgentToolAdmissionJournalFixture.Envelope(),
                AgentToolEffectState.Committed, default);
        }
        Assert.NotEqual(batch.Proposals[0].IntentId, batch.Proposals[1].IntentId);
        Assert.NotEqual(results[0].RunId, results[1].RunId);
        clock.Now = clock.Now.AddHours(2);
        await fixture.RevokeAsync(Revocation.Write);
        var adapter = services.GetRequiredService<ProjectStructureProcessToolAdmission>();
        await using (var read = await adapter.AuthorizeResultDisclosureAsync(context,
            new(batch.Proposals[0].IntentId, payload, AgentToolEffectState.Committed,
                JsonSerializer.SerializeToElement(results[0], ProjectStructureProcessProposalCodec.SerializerOptions)), default)) {
            Assert.NotNull(read);
        }
        await using var persisted = fixture.Context();
        var runIds = results.Select(result => result.RunId!.Value).ToArray();
        Assert.Equal(2, await persisted.PreparedLaunches.CountAsync(row => runIds.Contains(row.RunId)));
        Assert.Equal(2, await persisted.RuntimeStates.CountAsync(row => runIds.Contains(row.RunId)));
    }

    [Fact]
    public async Task Background_node_start_rejects_a_different_executor_before_allocating_a_Process_preparation() {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(ToolProducerHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock, persistExecution: false);
        var store = Assert.IsType<FileSandboxWorkspaceStore>(services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>());
        var journal = new AgentToolAdmissionJournal(store, NativeProfile(services), backgroundSources: [NativeBackgroundPolicy(services, fixture, clock)]);
        run = run with { ToolAdmission = await journal.CreateForNewBackgroundRunAsync(run, "Original background node start.") };
        await store.SaveExecutionRunDetailAsync(new(run, null, [], []));
        var context = await ToolProducerContextAsync(fixture, run);
        context = context with { Agent = context.Agent with { Id = Guid.NewGuid() } };
        var input = new ProjectStructureProcessStartProposal(fixture.Project.ProjectId, "selected-node", new());
        var payload = new ProjectStructureProcessProposalCodec().Prepare(input);
        await using var lease = await journal.AcquireRunAsync(run.ToolAdmission!.Session.Reference, default);
        using var bound = lease.Bind();
        await journal.BeginSegmentAsync(lease, CanDoItAll.Tests.Integration.Runtime.AgentToolAdmissionJournalFixture.Envelope(), null, default);
        var batch = Assert.Single((await journal.AdmitBatchAsync(lease, payload.Digest,
            CanDoItAll.Tests.Integration.Runtime.AgentToolAdmissionJournalFixture.Envelope(), [new("start", payload, false)], default)).Batches);
        var claim = await journal.ClaimInvocationAsync(lease, batch.Id, "start", payload, default);
        using var dispatch = claim.Bind();
        var failure = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            services.GetRequiredService<ProjectStructureProcessToolAdmission>().RequireInvocationAsync(context, input, default));
        Assert.Equal("ProcessToolAdmissionDenied", failure.ErrorCode);
        Assert.Null(await services.GetRequiredService<IProcessPreparedLaunchStore>().FindByIntentAsync(new(claim.Proposal.IntentId.Value)));
    }

    private static async Task<AgentRuntimeToolProviderContext> ToolProducerContextAsync(Fixture fixture, ExecutionRunRecord run) {
        var provider = (await fixture.Writer.LoadCatalogAsync()).Providers.First();
        var executor = fixture.Agent with { Id = run.AgentId, Name = "Assigned background executor", ConfigurationJson = "{}" };
        return new(executor, provider, [], false, AgentRuntimeToolProviderPurpose.GovernedProcessAutomation, "background-producer",
            AgentRuntimeContextIntent.Empty with {
                SourceKind = run.SourceKind, SourceId = run.SourceId, ProcessRunId = run.ProcessRunId, ProcessStepId = run.ProcessStepId,
                IsGovernedProcessStep = true, AllowsProductMutation = true,
                Purpose = AgentRuntimeContextPurpose.GovernedProcessAutomation,
                AllowedOperations = [ProcessOperationContractNames.ReadProjectStructure, ProcessOperationContractNames.ExecuteExternalAction]
            }, new Dictionary<string, string>()) {
            AdmittedToolSession = run.ToolAdmission!.Session.Reference, ToolAdmissionSupport = AgentToolAdmissionSupport.Recoverable
        };
    }

    private static TestHarnessOptions ToolProducerHarness(NativeClock clock) {
        var native = NativeHarness(clock);
        return new() { ConfigureServices = services => {
            native.ConfigureServices!(services);
            services.AddScoped<IProcessExecutionDispatchAuthorityReader>(provider => NativeReader(provider, clock));
            services.AddScoped<IProcessToolLaunchAdmissionPolicy>(provider => new ProjectProcessToolLaunchAdmissionPolicy(
                NativeReader(provider, clock), new EfProcessExecutionMutationGuard(NativeOptions<ProcessPersistenceDbContext>(provider),
                    provider.GetRequiredService<CoordinatedDatabaseTransaction>(), clock), NativeAuthority(provider, clock)));
            services.AddScoped<ProjectStructureProcessToolAdmission>();
            services.Replace(ServiceDescriptor.Singleton<IProcessLaunchArtifactInitializer>(new ToolNoArtifacts()));
        } };
    }

    private sealed class ToolNoArtifacts : IProcessLaunchArtifactInitializer {
        public Task InitializeAsync(ProcessLaunchArtifactInitializationRequest request, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
