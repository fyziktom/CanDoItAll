using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed partial class WorkflowProcessStepExecutorTests {
    [Fact]
    public async Task Mapped_claim_round_trip_keeps_real_nondefault_identity_and_old_reader_rejects_new_authority() {
        var assignment = CreateAssignment(new(new(Guid.NewGuid()), new(Guid.NewGuid())));
        WorkflowLaunchOrigin origin = await new MappedSource(assignment).CaptureAsync(new(assignment.RunId.Value),
            new(assignment.StepInstanceId.Value), ClaimToken, ProcessStepExecutionContract.Empty.ContractHash);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize(origin, options);
        var restored = Assert.IsType<WorkflowLaunchOrigin.ProcessDispatchAssignment>(JsonSerializer.Deserialize<WorkflowLaunchOrigin>(json, options));
        Assert.Equal(origin, restored);
        Assert.Equal(ClaimToken, restored.Dispatch.ClaimToken);
        Assert.NotEqual(Guid.Empty, restored.Dispatch.Assignment.Value);
        Assert.NotEqual(Guid.Empty, restored.Dispatch.ProfileId);
        Assert.Null(restored.StructureAuthority);
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info => {
            if (info.Type == typeof(WorkflowLaunchOrigin)) {
                var added = info.PolymorphismOptions!.DerivedTypes.Single(item => item.DerivedType == typeof(WorkflowLaunchOrigin.ProcessDispatchAssignment));
                info.PolymorphismOptions.DerivedTypes.Remove(added);
            }
        });
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorkflowLaunchOrigin>(json,
            new JsonSerializerOptions(options) { TypeInfoResolver = resolver }));
    }

    [Fact]
    public async Task Mapped_claim_replacement_is_a_fence_and_keeps_the_same_business_intent_and_scope() {
        var assignment = CreateAssignment(new(new(Guid.NewGuid())));
        var source = new MappedSource(assignment);
        var first = await source.CaptureAsync(new(assignment.RunId.Value), new(assignment.StepInstanceId.Value), ClaimToken, ProcessStepExecutionContract.Empty.ContractHash);
        var second = await source.CaptureAsync(first.Dispatch.ProcessRun, first.Dispatch.Assignment, Guid.NewGuid(), first.Dispatch.ContractHash);
        Assert.NotEqual(first.Dispatch.ClaimToken, second.Dispatch.ClaimToken);
        Assert.True(first.Dispatch.HasSameIntent(second.Dispatch));
        var key = new WorkflowLaunchIdempotencyKey("mapped-step");
        WorkflowLaunchIntent Intent(WorkflowLaunchOrigin origin) => new(new WorkflowDefinitionSelection.LatestActive(first.Dispatch.WorkflowId),
            WorkflowLaunchMode.Production, origin, "{}", WorkflowLaunchCompletionPolicy.WaitForStopped, new WorkflowLaunchIdempotency.CallerSupplied(key));
        Assert.Equal(WorkflowLaunchIdempotencyRequestFactory.CreateScope(Intent(first), key),
            WorkflowLaunchIdempotencyRequestFactory.CreateScope(Intent(second), key));
    }

    [Fact]
    public async Task Mapped_renewed_claim_observes_the_same_child_and_new_step_instance_remains_fresh() {
        var assignment = CreateAssignment(new(new(Guid.NewGuid())));
        var source = new MappedSource(assignment);
        var origin = await source.CaptureAsync(new(assignment.RunId.Value), new(assignment.StepInstanceId.Value), ClaimToken, ProcessStepExecutionContract.Empty.ContractHash);
        var child = CreateWorkflowRun(origin.Dispatch.WorkflowId, WorkflowVersionId.New(), WorkflowRunState.Completed, origin);
        var runtime = new RecordingWorkflowRuntimeManager { Runs = [child], Events = [CreateOutputEvent(child.RunId, CreateCompletedOutput())] };
        var launch = new RecordingWorkflowLaunchService();
        var result = await CreateExecutor(launch, runtime, assignment).ExecuteAsync(assignment, ProcessStepExecutionContract.Empty,
            dispatchClaimIdentity: new(Guid.NewGuid()));
        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.Empty(launch.Intents);
        var next = CreateAssignment(assignment.WorkflowBinding!);
        var nextRun = CreateWorkflowRun(origin.Dispatch.WorkflowId, WorkflowVersionId.New(), WorkflowRunState.Completed, CreateProcessOrigin(next));
        var freshLaunch = new RecordingWorkflowLaunchService { Run = nextRun };
        runtime.Events = [CreateOutputEvent(nextRun.RunId, CreateCompletedOutput())];
        await CreateExecutor(freshLaunch, runtime, next).ExecuteAsync(next, ProcessStepExecutionContract.Empty, dispatchClaimIdentity: new(Guid.NewGuid()));
        var newIntent = Assert.Single(freshLaunch.Intents);
        Assert.NotEqual(origin.Dispatch.Assignment, Assert.IsType<WorkflowLaunchOrigin.ProcessDispatchAssignment>(newIntent.Origin).Dispatch.Assignment);
    }

    [Fact]
    public async Task Mapped_missing_actual_claim_fails_before_launch_or_receipt_observation() {
        var assignment = CreateAssignment(new(new(Guid.NewGuid())));
        var runtime = new RecordingWorkflowRuntimeManager();
        var launch = new RecordingWorkflowLaunchService();
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateExecutor(launch, runtime, assignment).ExecuteAsync(
            assignment, ProcessStepExecutionContract.Empty).AsTask());
        Assert.Empty(launch.Intents);
        Assert.Null(runtime.RequestedAssignment);
    }

    [Fact]
    public async Task Mapped_runtime_rejects_changed_input_before_backend_lookup_or_owner_admission() {
        var assignment = CreateAssignment(new(new(Guid.NewGuid()), new(Guid.NewGuid())));
        var origin = await new MappedSource(assignment).CaptureAsync(new(assignment.RunId.Value), new(assignment.StepInstanceId.Value),
            ClaimToken, ProcessStepExecutionContract.Empty.ContractHash);
        var definition = new WorkflowDefinition(origin.Dispatch.WorkflowId, origin.Dispatch.RequestedVersionId!.Value, "Mapped", "Input binding",
            WorkflowLifecycleStatus.Active, new(new("start"), [], []), new(WorkflowRuntimeBackendKind.InProcess, true, false, false, false), Now, Now);
        var store = new InMemoryWorkflowRunStore();
        var runtime = WorkflowRuntimeManager.CreateInMemory([], store);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.StartAsync(definition,
            new(definition.Id, definition.VersionId, "{\"retargeted\":true}", null, null, null) { Origin = origin }));
        Assert.Contains("differs from its original Process owner admission", error.Message, StringComparison.Ordinal);
        Assert.Empty(await store.ListRunsAsync());
    }

    [Fact]
    public async Task Mapped_changed_repair_content_does_not_retarget_an_existing_owner_receipt() {
        var assignment = CreateAssignment(new(new(Guid.NewGuid())));
        var origin = await new MappedSource(assignment).CaptureAsync(new(assignment.RunId.Value), new(assignment.StepInstanceId.Value), ClaimToken, ProcessStepExecutionContract.Empty.ContractHash);
        var child = CreateWorkflowRun(origin.Dispatch.WorkflowId, WorkflowVersionId.New(), WorkflowRunState.Completed, origin);
        var changed = assignment with { Prompt = "Changed semantic input" };
        var runtime = new RecordingWorkflowRuntimeManager { Runs = [child] };
        var launch = new RecordingWorkflowLaunchService();
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateExecutor(launch, runtime, changed).ExecuteAsync(changed,
            ProcessStepExecutionContract.Empty, dispatchClaimIdentity: new(Guid.NewGuid())).AsTask());
        Assert.Empty(launch.Intents);
        Assert.Equal(child.RunId, Assert.Single(runtime.Runs).RunId);
    }
}
