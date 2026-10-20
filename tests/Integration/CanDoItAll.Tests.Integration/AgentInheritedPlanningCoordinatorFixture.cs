using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration.Runtime;

internal sealed class AgentInheritedPlanningCoordinatorFixture : IAsyncDisposable {
    private readonly HttpClient client;
    private AgentInheritedPlanningCoordinatorFixture(AgentInheritedPlanningFixture source, ProviderProfile provider,
        MafPlanningProviderFixture.Wire wire, HttpClient client, MafPlanningStepRuntime runtime,
        Participant participant, ExecutionRunRecord original) {
        Source = source;
        Provider = provider;
        Wire = wire;
        this.client = client;
        Runtime = runtime;
        Module = participant;
        Original = original;
    }

    internal AgentInheritedPlanningFixture Source { get; }
    internal AgentDefinition Agent => Source.Files.Agent;
    internal ProviderProfile Provider { get; }
    internal MafPlanningProviderFixture.Wire Wire { get; }
    internal MafPlanningStepRuntime Runtime { get; }
    internal Participant Module { get; }
    internal ExecutionRunRecord Original { get; }
    internal AgentPlanningDecisionExpectation Expected => AgentPlanningDecisionExpectation.From(Original);

    internal static async Task<AgentInheritedPlanningCoordinatorFixture> CreateAsync(ReadBasisMode mode = ReadBasisMode.RunSnapshot) {
        var source = await AgentInheritedPlanningFixture.CreateAsync(mode,
            scope: new(WorkspaceScopeKind.Project, Guid.NewGuid().ToString("D")));
        var wire = new MafPlanningProviderFixture.Wire();
        var client = new HttpClient(wire);
        wire.Client = client;
        try {
            var provider = source.Files.Provider with { Kind = ProviderKind.OpenAi, Transport = ProviderTransportKind.ChatCompletions };
            await source.Files.Store.UpdateCatalogAsync(catalog => catalog with {
                Providers = catalog.Providers.Select(item => item.Id == provider.Id ? provider : item).ToArray()
            });
            var factory = new MafProviderAgentFactory(wire, NoOpMafProviderStreamingDispatchGate.Instance, wire, httpClientSelector: wire);
            var runtime = new MafPlanningStepRuntime(factory, NullLogger<MafPlanningStepRuntime>.Instance);
            var participant = new Participant(source.Basis);
            var id = Guid.NewGuid();
            var store = source.Files.NewStore();
            var admission = new AgentInheritedPlanningRunAdmission(store, store, source.NewJournal(store));
            var original = source.Original.ToolAdmission!.PlanningDecision!;
            var saved = await admission.AdmitAsync(id, (run, _) => {
                Assert.Equal(original.InheritedOrigin! with { Source = run.ToolAdmission!.Session.Reference.BackgroundSource! },
                    run.ToolAdmission.PlanningDecision!.InheritedOrigin);
                return Task.CompletedTask;
            }, _ => Task.FromResult(new AgentInheritedPlanningRunPreparation(
                source.Original with { Id = id, MetadataJson = "{}", ToolAdmission = null }, "Prepare the inherited task plan.",
                original.Owner, runtime.CaptureRuntimeContract(source.Files.Agent, provider, participant), source.Files.StorageScope,
                original.InheritedOrigin!.ParentBinding,
                (binding, _) => Task.FromResult(new AgentInheritedPlanningBinding(original.OriginalTarget,
                    original.InheritedOrigin! with { Source = binding.Session.Reference.BackgroundSource! })))));
            return new(source, provider, wire, client, runtime, participant, saved.Run);
        } catch {
            client.Dispose();
            await source.DisposeAsync();
            throw;
        }
    }

    internal AgentInheritedPlanningCoordinator Coordinator(bool registered = true) {
        var store = Source.Files.NewStore();
        return new(store, Source.NewJournal(store), Runtime, registered ? [Module] : []);
    }

    internal Task<AgentPlanningExecutionResult> RunAsync(AgentPlanningDecisionExpectation? expected = null)
        => Coordinator().RunAsync(Agent, Provider, expected ?? Expected);

    internal async Task<ExecutionRunRecord> ReadAsync()
        => await Source.Files.NewStore().GetExecutionRunAsync(Original.Id)
            ?? throw new InvalidOperationException("The inherited coordinator fixture lost its original run.");

    public async ValueTask DisposeAsync() {
        client.Dispose();
        await Source.DisposeAsync();
    }

    internal sealed class Participant(AgentPlanningBasisRecord original) : IAgentInheritedPlanningParticipant {
        internal AgentPlanningBasisRecord Basis { get; set; } = original;
        internal int Reads { get; private set; }
        internal int Preparations { get; private set; }
        internal int ContinueSteps { get; set; }
        internal bool DenyRead { get; set; }
        public AgentPlanningOwner Owner => AgentPlanningOwner.ProjectTaskPlan;
        public AgentPlanningPromptContract Prompt { get; set; } = new("Use only the original request and supplied material basis.", "task_plan",
            "{\"type\":\"object\",\"properties\":{\"tasks\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}}},\"required\":[\"tasks\"],\"additionalProperties\":false}");
        public Task<AgentPlanningBasisRecord> ReadOriginalBasisAsync(AgentPlanningInheritedOrigin original, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            Reads++;
            return Task.FromResult(Basis);
        }
        public Task RequireCurrentReadAsync(AgentPlanningDecisionRecord decision, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            return DenyRead ? Task.FromException(new UnauthorizedAccessException("The original material is no longer readable.")) : Task.CompletedTask;
        }
        public AgentPlanningResponsePreparation PreparePublication(AgentPlanningDecisionRecord decision,
            AgentPlanningBasisRecord basis, string responseText) {
            Preparations++;
            return Preparations <= ContinueSteps ? new AgentPlanningResponsePreparation.Continue() :
                new AgentPlanningResponsePreparation.Publication(AgentPlanningLiveOwnerFixture.Publication());
        }
    }
}
