using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Integration.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration;

internal sealed class ProjectTaskPlanProcessRuntimeFixture : IAsyncDisposable {
    private readonly HttpClient client;
    private readonly MafPlanningStepRuntime runtime;
    private readonly AgentToolSemanticDigest contract;
    private ProjectTaskPlanProcessRuntimeFixture(ProjectTaskPlanProcessFixture source, MafPlanningProviderFixture.Wire wire,
        HttpClient client, MafPlanningStepRuntime runtime, AgentToolSemanticDigest contract) {
        Source = source;
        Wire = wire;
        this.client = client;
        this.runtime = runtime;
        this.contract = contract;
        Participant = source.Sql.Services.GetServices<IAgentInheritedPlanningParticipant>()
            .Single(item => item.Owner == AgentPlanningOwner.ProjectTaskPlan);
    }

    internal ProjectTaskPlanProcessFixture Source { get; }
    internal MafPlanningProviderFixture.Wire Wire { get; }
    internal IAgentInheritedPlanningParticipant Participant { get; }
    internal AgentToolSessionReference Session => Source.Original.ToolAdmission!.Session.Reference;

    internal static async Task<ProjectTaskPlanProcessRuntimeFixture> CreateAsync(bool agentSource = true, bool sameExecutor = false,
        bool includeOperationContract = true) {
        var wire = new MafPlanningProviderFixture.Wire { ResponseText = ProjectTaskPlanAgentFixture.Draft };
        var client = new HttpClient(wire);
        wire.Client = client;
        var runtime = new MafPlanningStepRuntime(new MafProviderAgentFactory(wire,
            NoOpMafProviderStreamingDispatchGate.Instance, wire, httpClientSelector: wire), NullLogger<MafPlanningStepRuntime>.Instance);
        AgentToolSemanticDigest? contract = null;
        try {
            var source = await ProjectTaskPlanProcessFixture.CreateAsync(agentSource, sameExecutor, registeredSource: true,
                runtimeContract: (agent, provider, participant) => {
                    var captured = runtime.CaptureRuntimeContract(agent, provider, participant);
                    contract = captured;
                    return captured;
                }, includeOperationContract: includeOperationContract);
            return new(source, wire, client, runtime, contract!.Value);
        } catch {
            client.Dispose();
            throw;
        }
    }

    internal async Task<ExecutionRunRecord> PrepareAsync() {
        await using var lease = await Source.Owner.StartInheritedAsync(AgentPlanningDecisionExpectation.From(await Source.ReadAsync()), CanReadAsync, default);
        using var bound = lease.Bind();
        var work = lease.PlanningWork!;
        await Source.Owner.InheritBasisAsync(work, Participant.ReadOriginalBasisAsync, default);
        var response = await runtime.RunAsync(new(Source.Executor, Source.Provider, Source.Owner, work, Participant, contract));
        Assert.Null(response.OutputRefusal);
        Assert.Null(response.Admission.Refusal);
        var run = response.Admission.Run;
        var proposal = AgentToolJournalTransitions.RequireProposal(run.ToolAdmission!, ProjectTaskPlanAgentFixture.Binding(run));
        Source.Clock.Timestamp += TimeSpan.FromSeconds(1).Ticks;
        return await Source.Owner.PauseForApprovalAsync(work, new(Guid.NewGuid().ToString("D"), proposal.CallId,
            proposal.Payload.ToolName, "function", "Review the original inherited task plan.", proposal.Payload.ArgumentsJson), default);
    }

    internal async Task<AgentSavedEffectDispatchResult> PublishAsync(ExecutionRunRecord waiting) {
        var binding = ProjectTaskPlanAgentFixture.Binding(waiting);
        var origin = waiting.ToolAdmission!.PlanningDecision!.InheritedOrigin!;
        var request = new AgentSavedEffectContinuationRequest(waiting.Id, null, waiting.Revision, waiting.ToolAdmission.Revision,
            binding, waiting.PendingApprovals.Select(item => new PendingToolApprovalDecision(item.ApprovalId, true) {
                ToolAdmission = item.ToolAdmission
            }).ToArray());
        await using var lease = await Source.Owner.ResumeInheritedSavedEffectAsync(request,
            AgentPlanningDecisionExpectation.From(waiting), new(origin, RequireParentAsync), CanReadAsync, default);
        using var bound = lease.Bind();
        var result = await Dispatcher().DispatchAsync(AgentSavedEffectDispatchRequest.ForInherited(Session, binding), default);
        if (result.OwnerResult is null) {
            Assert.Equal(AgentToolEffectState.NotCommitted, result.EffectState);
            await Source.Owner.SettleTerminalAsync(lease.PlanningWork!, AgentPlanningGenerationState.Failed, default);
            return result;
        }
        var outcome = ProjectTaskPlanAgentFixture.Result(result);
        if (outcome.Refusal == ProjectTaskPlanRefusal.BasisChanged && result.EffectState == AgentToolEffectState.NotCommitted) {
            await Source.Owner.PauseForBasisChangeAsync(lease.PlanningWork!, result.Binding, default);
        } else {
            await Source.Owner.SettleTerminalAsync(lease.PlanningWork!, result.EffectState == AgentToolEffectState.Committed
                ? AgentPlanningGenerationState.Completed : AgentPlanningGenerationState.Failed, default);
        }
        return result;
    }

    internal MafSavedEffectDispatcher Dispatcher() {
        var services = Source.Sql.Services;
        var dependencies = MafAgentRuntimeDependencies.FromServices(services);
        return new(services.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot(), Source.Scope,
            services.GetRequiredService<CanDoItAll.Infrastructure.FileSystem.IPhysicalFileSystemPathPolicyFactory>(),
            services.GetRequiredService<IWorkspaceRuntimeServicesFactory>(), Source.Journal,
            services.GetRequiredService<IAgentCatalogReadLeaseStore>(), services.GetServices<IAgentRuntimeToolProvider>().ToArray(),
            services.GetServices<IAgentToolReceiptReconciliationProvider>().ToArray(), dependencies.ToolInvocationPolicyPipeline,
            dependencies.ToolPolicies, NullLogger<MafSavedEffectDispatcher>.Instance);
    }

    internal ValueTask<bool> CanReadAsync(AgentToolSessionObservation observation, CancellationToken cancellationToken)
        => new(Source.Sql.Services.GetRequiredService<ProjectTaskPlanSavedPublicationResolver>().CanReadAsync(observation, cancellationToken));

    private async Task RequireParentAsync(ExecutionRunRecord original, CancellationToken cancellationToken) {
        var observed = await Source.SourcePolicy.ObserveAsync(original, Source.Profile, cancellationToken);
        if (!observed.ReadAllowed || !observed.DispatchAllowed) {
            throw new AgentToolReceiptAccessDeniedException();
        }
    }

    public async ValueTask DisposeAsync() {
        client.Dispose();
        await Source.DisposeAsync();
    }
}
