using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Mcp;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Tools.Documents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class AgentToolEffectOnlyActivationBoundaryTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Effects_only_execute_and_continuation_refuse_before_provider_construction_or_effect_admission(bool continuation) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var source = fixture.Detail.Run.ToolAdmission! with {
            SchemaVersion = AgentToolJournalRecord.EffectJournalSchemaVersion,
            Support = AgentToolAdmissionSupport.EffectJournalOnly
        };
        await using var restored = await AgentToolAdmissionJournalFixture.RestoreRetainedAsync(fixture.Agent, fixture.Provider,
            fixture.Profile, fixture.Detail.Run with { ToolAdmission = source });
        var provider = new RefusingProviderFactory();
        var gateway = new RefusingProviderGateway();
        using var services = CreateServices(provider, gateway);
        var journal = restored.NewJournal(restored.NewStore());
        var runtime = new MafAgentRuntime(restored.WorkspaceRoot, services, restored.StorageScope, toolAdmissionJournal: journal);
        var original = (await restored.NewStore().GetExecutionRunAsync(restored.Session.ExecutionRunId))!;
        var before = JsonSerializer.Serialize(original.ToolAdmission);
        var options = new AgentRuntimeExecutionOptions(null, AgentFinalizerMode.Disabled, true, 0) {
            AdmittedToolSession = restored.Session, ToolAdmissionSupport = AgentToolAdmissionSupport.EffectJournalOnly
        };
        var progress = 0;
        Task ReportAsync(ExecutionState state, string phase, string message) {
            progress++;
            return Task.CompletedTask;
        }
        async Task InvokeAsync(AgentRuntimeExecutionOptions supplied) {
            if (continuation) {
                await runtime.ContinuationPort.ContinueAsync(new(restored.Agent, restored.Provider, restored.Detail.ChatSession!, [], [],
                    [new("original-approval", true)], null, ReportAsync, ExecutionOptions: supplied));
            } else {
                await runtime.ExecutionPort.ExecuteAsync(new(restored.Agent, restored.Provider, restored.Detail.ChatSession!, [], [],
                    "Original live request", null, ReportAsync, ExecutionOptions: supplied));
            }
        }

        var refusal = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => InvokeAsync(options));
        Assert.Equal(MafToolRunContext.EffectOnlyUnavailableCode, refusal.Code);
        Assert.Equal(before, JsonSerializer.Serialize((await restored.NewStore().GetExecutionRunAsync(original.Id))!.ToolAdmission));

        var disguised = await Assert.ThrowsAsync<AgentToolAdmissionException>(() =>
            InvokeAsync(options with { ToolAdmissionSupport = AgentToolAdmissionSupport.RequestScopedInput }));
        Assert.Equal(MafToolRunContext.EffectOnlyUnavailableCode, disguised.Code);
        Assert.Equal(0, provider.Calls);
        Assert.Equal(0, gateway.Calls);
        Assert.Equal(0, progress);
        var retained = (await restored.NewStore().GetExecutionRunAsync(original.Id))!.ToolAdmission!;
        Assert.Equal(source.Session, retained.Session);
        Assert.Equal(AgentToolAdmissionSupport.EffectJournalOnly, retained.Support);
        Assert.Empty(retained.Segments);
        Assert.Empty(retained.Batches);
        Assert.NotNull(retained.ActiveDispatchLeaseId);
        Assert.Null(AgentToolRunLease.Current);
        await using var next = await restored.NewJournal(restored.NewStore()).AcquireRunAsync(restored.Session, default);
        Assert.NotEqual(retained.ActiveDispatchLeaseId, next.Id);
    }

    private static ServiceProvider CreateServices(RefusingProviderFactory provider, RefusingProviderGateway gateway) {
        var services = new ServiceCollection();
        var paths = new PhysicalFileSystemPathPolicyFactory();
        services.AddMafRuntimeArchitectureServices();
        services.AddSingleton<IMafProviderRuntimeGateway>(gateway);
        services.AddSingleton<IMafProviderStreamingDispatchGate>(NoOpMafProviderStreamingDispatchGate.Instance);
        services.AddSingleton<IAgentImageAnalysisService, UnavailableAgentImageAnalysisService>();
        services.AddSingleton<IMafProviderAgentFactory>(provider);
        services.AddSingleton<IMcpClientFactory>(new FakeMcpClientFactory(new FakeMcpServerScript(Tools: [])));
        services.AddSingleton<IPhysicalFileSystemPathPolicyFactory>(paths);
        services.AddSingleton<IWorkspaceRuntimeServicesFactory>(new WorkspaceRuntimeServicesFactory([],
            new ManagedCodeMarkItDownDocumentMarkdownConverter(), paths, new ExternalTargetPathRegistryFactory()));
        services.AddSingleton(TimeProvider.System);
        return services.BuildServiceProvider();
    }

    private sealed class RefusingProviderFactory : IMafProviderAgentFactory {
        internal int Calls { get; private set; }

        public AIAgent CreateFrameworkAgent(ProviderProfile provider, string model, ChatClientAgentOptions options,
            bool frameworkManagedHistory, bool allowBackgroundResponses) {
            Calls++;
            throw new InvalidOperationException("The effects-only guard must run before provider construction.");
        }
    }

    private sealed class RefusingProviderGateway : IMafProviderRuntimeGateway {
        internal int Calls { get; private set; }

        public Task<ProviderHealthResult> TestProviderAsync(ProviderProfile provider, CancellationToken cancellationToken = default)
            => throw Refused();

        public Task<ProviderTestChatResult> RunProviderTestChatAsync(ProviderProfile provider, ProviderTestChatRequest request,
            string model, CancellationToken cancellationToken = default) => throw Refused();

        public Task<ProviderTestChatResult> RunProviderImageChatAsync(ProviderProfile provider, ProviderTestChatRequest request,
            string model, IReadOnlyList<ProviderChatAttachment> attachments, string modelParameterConfigurationJson = "",
            CancellationToken cancellationToken = default) => throw Refused();

        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(ProviderProfile provider,
            ProviderModelMaintenanceEditorRequest request, CancellationToken cancellationToken = default) => throw Refused();

        private InvalidOperationException Refused() {
            Calls++;
            return new("The effects-only guard must run before provider or attachment preparation.");
        }
    }
}
