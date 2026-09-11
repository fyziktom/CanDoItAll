using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed partial class MafWorkflowHumanInLoopTests {
    [Fact]
    public async Task Original_v1_native_checkpoint_resumes_without_minting_private_read_evidence() {
        var marker = new CountingLlmInvoker();
        var component = CreateComponent();
        var definition = CreateHumanInputDefinition(component);
        var payloads = new InMemoryWorkflowBackendCheckpointPayloadStore(TimeProvider.System);
        var compiler = CreateCompiler(marker);
        var build = compiler.Compile(definition, [component], WorkflowPreviewSimulationPlan.Empty,
            new WorkflowExecutorInvocationContext { CompilerContractVersion = WorkflowProviderDisclosureProtocol.Legacy });
        var started = await StartVersionedNativeAsync(definition, build, payloads);
        var request = Assert.Single(started.ExternalRequests);
        Assert.Equal(WorkflowProviderDisclosureProtocol.Legacy, request.Continuation!.CompilerContractVersion);
        Assert.True(WorkflowExternalRequestBoundaryRecord.TryCreate(request, out var boundary));
        Assert.Equal(InMemoryWorkflowCheckpointRequestLinkOutcome.Linked, payloads.TryLinkExternalRequest(boundary!, started.Run));
        using var response = JsonDocument.Parse("{\"answer\":\"continue old workflow\"}");
        var restarted = CreateNativeBackend(definition, component, marker, payloads);
        var completed = await restarted.ResumeAsync(CreateAuthorizedResumeRequest(started.Run, request, response.RootElement));
        Assert.Equal(WorkflowRunState.Completed, completed.Run.State);
        Assert.Equal(1, marker.InvocationCount);
        Assert.All(started.Events.Concat(completed.Events), item => {
            Assert.Null(item.DisclosureDeclaration);
            Assert.Null(item.CompletionProof);
            Assert.Empty(item.ProviderReadEvidence);
        });
    }

    [Fact]
    public async Task Protected_v2_checkpoint_is_rejected_by_the_retained_v1_rehydration_contract_before_execution() {
        var marker = new CountingLlmInvoker();
        var component = CreateComponent();
        var definition = CreateHumanInputDefinition(component);
        var payloads = new InMemoryWorkflowBackendCheckpointPayloadStore(TimeProvider.System);
        var compiler = CreateCompiler(marker);
        var current = compiler.Compile(definition, [component]);
        Assert.Equal(WorkflowProviderDisclosureProtocol.Current, current.CompilerContractVersion);
        var started = await StartVersionedNativeAsync(definition, current, payloads);
        var request = Assert.Single(started.ExternalRequests);
        var oldBuild = compiler.Compile(definition, [component], WorkflowPreviewSimulationPlan.Empty,
            new WorkflowExecutorInvocationContext { CompilerContractVersion = WorkflowProviderDisclosureProtocol.Legacy });
        Assert.NotEqual(current.TopologyFingerprint, oldBuild.TopologyFingerprint);
        using var response = JsonDocument.Parse("{\"answer\":\"untrusted downgrade\"}");
        var failure = await Assert.ThrowsAsync<WorkflowBackendResumeException>(() => new MafWorkflowRehydrationVerifier().VerifyAsync(
            CreateAuthorizedResumeRequest(started.Run, request, response.RootElement), definition, oldBuild, payloads, CancellationToken.None));
        Assert.Equal(WorkflowBackendResumeFailureKind.CompilerContractMismatch, failure.Kind);
        Assert.Equal(1, marker.InvocationCount);
    }

    private static Task<WorkflowBackendStartResult> StartVersionedNativeAsync(WorkflowDefinition definition, MafWorkflowBuildResult build,
        IWorkflowBackendCheckpointPayloadStore payloads) {
        Assert.True(build.Compilation.Succeeded, build.Compilation.ErrorMessage);
        var mapper = new MafWorkflowTurnResultMapper(payloads, new MafWorkflowExternalRequestMapper(TimeProvider.System),
            new MafWorkflowEventNormalizer(), new WorkflowCheckpointFactory(), new WorkflowPayloadPolicyService(), TimeProvider.System);
        var driver = new MafWorkflowNativeStartDriver(payloads, new MafWorkflowStreamingRunDriver(), mapper, TimeProvider.System);
        return driver.StartAsync(definition, CreateStartRequest(definition), WorkflowRunId.New(), build, CancellationToken.None);
    }
}
