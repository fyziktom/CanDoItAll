using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.AgentFramework.Workflows.Runtime;
using CanDoItAll.Modules.Workbench;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed partial class WorkflowHttpSecretAdmissionIntegrationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Deferred_mapped_child_rechecks_receiving_parent_before_provider_disclosure(bool cancelParent) {
        var shape = new WorkflowValueShape(WorkflowValueShapeKind.Object, "{}", "Mapped input");
        var component = new LlmCallComponent(WorkflowComponentId.New(), "Mapped disclosure", null, "mapped-disclosure", WorkflowModality.Text,
            new(0, 100, false, ""), "Summarize reviewed input.", shape, shape, AgentPermissionsPolicy.Default, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        await using var fixture = await CreateMappedReadFixtureAsync(deferred: true, legacy: false, disclosureComponent: component);
        var store = fixture.Services.GetRequiredService<IWorkflowRunStore>();
        var executor = new ProjectStructureWorkflowExecutor(fixture.Services.GetRequiredService<IProjectStructureRuntimeGateway>());
        var catalog = new WorkflowExecutorCatalog([executor]);
        var port = new MappedDisclosurePort();
        var gate = new WorkflowProviderInputAdmission(store, catalog,
            [new WorkflowStructureProviderDisclosurePolicy(fixture.Services.GetRequiredService<ProjectStructureWorkflowAuthorityService>())]);
        var llm = new WorkflowLlmComponentInvoker(port, new MappedDisclosureProvider(), new ProviderProfileService(), gate);
        var binding = new MafWorkflowNodeExecutionBindingFactory(new WorkflowExecutorInvoker(catalog, [executor]), llm);
        using var audit = WorkflowExecutorExecutionAuditScope.Push(fixture.Run.RunId, fixture.Run.Origin);
        using var progress = WorkflowNodeExecutionProgressScope.Push(new MappedDisclosureProgress(store));
        var input = new WorkflowNodeInput(fixture.Parent.InputJson) { ExecutionOccurrence = WorkflowExecutionOccurrence.Start(fixture.Run.RunId) };
        var output = await binding.ExecuteAsync(fixture.Definition, fixture.Node, input, new Dictionary<WorkflowComponentId, LlmCallComponent>(),
            new Dictionary<WorkflowNodeId, WorkflowPreviewSimulationStep>(), WorkflowExecutorInvocationContext.Empty);
        var read = Assert.Single((await store.ReadProviderDisclosureAsync(fixture.Run.RunId)).Completions);
        Assert.Contains(fixture.Project.LifetimeId.ToString("D"), Assert.Single(read.Evidence).PayloadJson, StringComparison.OrdinalIgnoreCase);
        if (cancelParent) {
            await fixture.Parent.CancelAsync();
        }
        var node = fixture.Definition.Graph.Nodes.Single(item => item.Kind == WorkflowNodeKind.LlmCall);
        var pending = binding.ExecuteAsync(fixture.Definition, node, output, new Dictionary<WorkflowComponentId, LlmCallComponent> { [component.Id] = component },
            new Dictionary<WorkflowNodeId, WorkflowPreviewSimulationStep>(), WorkflowExecutorInvocationContext.Empty).AsTask();
        if (cancelParent) {
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() => pending);
            Assert.Equal(0, port.Calls);
        } else {
            _ = await pending;
            Assert.Equal(1, port.Calls);
        }
        Assert.Equal(read.Proof.CompletionId, Assert.Single((await store.ReadProviderDisclosureAsync(fixture.Run.RunId)).Completions,
            item => item.Proof.NodeId == fixture.Node.Id).Proof.CompletionId);
    }

    private sealed class MappedDisclosureProgress(IWorkflowRunStore store) : IWorkflowNodeExecutionProgressObserver {
        public WorkflowReadEvidenceDurability ReadEvidenceDurability => WorkflowReadEvidenceDurability.Persisted;
        public ValueTask RecordAsync(WorkflowNodeExecutionProgress progress, CancellationToken cancellationToken = default) {
            if (progress.State != WorkflowNodeExecutionProgressState.Completed) {
                return ValueTask.CompletedTask;
            }
            return new(store.SaveEventAsync(new(Guid.NewGuid(), progress.RunId!.Value, WorkflowEventKind.ExecutorCompleted, progress.NodeId,
                "Actual mapped completion", WorkflowEventPayloads.Serialize(WorkflowEventPayloadSource.CanDoItAllProgress,
                    "WorkflowNodeCompleted", progress.NodeId, progress.ExecutorId, inlineJson: progress.PayloadJson), progress.OccurredAtUtc) {
                        CompletionProof = progress.CompletionProof, ProviderReadEvidence = progress.ProviderReadEvidence
                    }, cancellationToken));
        }
    }

    private sealed class MappedDisclosurePort : ILlmInvocationPort {
        public int Calls { get; private set; }
        public Task<LlmInvocationResult> InvokeAsync(LlmInvocationRequest request, CancellationToken cancellationToken = default) {
            Calls++;
            return Task.FromResult(new LlmInvocationResult(request.Model, "Mapped summary", new(1, 1, 0)));
        }
    }

    private sealed class MappedDisclosureProvider : IProviderRuntimeProfileSource {
        private readonly ProviderProfile provider = new(Guid.NewGuid(), "Mapped synthetic provider", ProviderKind.OpenAi,
            "https://example.invalid/v1", "MAPPED_DISCLOSURE_TEST_KEY", "mapped-disclosure", ProviderTransportKind.ChatCompletions,
            true, false, false, true, false, "{}", "", "Not checked", null, ["mapped-disclosure"]);
        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProviderProfile>>([provider]);
        public Task<ProviderProfile?> GetProviderAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<ProviderProfile?>(id == provider.Id ? provider : null);
    }
}
