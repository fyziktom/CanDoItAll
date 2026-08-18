using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessRuntimeOwnedStepCoordinatorTests
{
    [Fact]
    public async Task TryExecuteRuntimeOwnedStepAsync_returns_null_when_no_executor_is_selected()
    {
        var coordinator = CreateCoordinator();

        var result = await coordinator.TryExecuteRuntimeOwnedStepAsync(CreateAssignment(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task TryExecuteRuntimeOwnedStepAsync_returns_generic_manager_result_when_selected_executor_is_not_registered()
    {
        var coordinator = CreateCoordinator();

        var result = await coordinator.TryExecuteRuntimeOwnedStepAsync(
            CreateAssignment("unregistered.driver"),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(StrategyOutcome.NeedsManager, result!.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == "process.adapter.runtime_owned_executor_unavailable");
        Assert.Contains(result.ManagerSignals, signal =>
            signal.Code.Value == "process.adapter.runtime_owned_executor_unavailable");
    }

    [Fact]
    public async Task TryExecuteRuntimeOwnedStepAsync_returns_generic_manager_result_when_selected_executor_declines()
    {
        var executor = new TestRuntimeOwnedStepExecutor("test.declines", result: null);
        var coordinator = CreateCoordinator(executor);

        var result = await coordinator.TryExecuteRuntimeOwnedStepAsync(
            CreateAssignment(executor.ExecutorKey),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(StrategyOutcome.NeedsManager, result!.Outcome);
        Assert.Equal(1, executor.CallCount);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == "process.adapter.runtime_owned_executor_declined");
        Assert.Contains(result.ManagerSignals, signal =>
            signal.Code.Value == "process.adapter.runtime_owned_executor_declined");
    }

    [Fact]
    public void Constructor_rejects_duplicate_executor_keys()
    {
        var first = new TestRuntimeOwnedStepExecutor("test.duplicate", result: null);
        var second = new TestRuntimeOwnedStepExecutor("TEST.DUPLICATE", result: null);

        var exception = Assert.Throws<InvalidOperationException>(() => CreateCoordinator(first, second));

        Assert.Contains("Duplicate runtime-owned step executor key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryExecuteRuntimeOwnedStepAsync_dispatches_only_the_executor_selected_by_key()
    {
        var selected = new TestRuntimeOwnedStepExecutor(
            "test.selected",
            new ProcessRuntimeOwnedStepExecutionResult(
                false,
                null,
                [],
                Guid.NewGuid(),
                "The selected test driver failed.",
                "selected-driver-failure"));
        var unselected = new TestRuntimeOwnedStepExecutor("test.unselected", result: null);
        var coordinator = CreateCoordinator(selected, unselected);

        var result = await coordinator.TryExecuteRuntimeOwnedStepAsync(
            CreateAssignment("TEST.SELECTED"),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(StrategyOutcome.NeedsManager, result!.Outcome);
        Assert.Equal(1, selected.CallCount);
        Assert.Equal(0, unselected.CallCount);
        var diagnostic = Assert.Single(
            result.Diagnostics,
            diagnostic => diagnostic.Code.Value == "process.adapter.runtime_owned_step_failed");
        Assert.Equal(ProcessDiagnosticRetrySafety.UnsafeToRetry, diagnostic.RetrySafety);
        Assert.Equal(ProcessDiagnosticIdempotencyClassification.Unknown, diagnostic.Idempotency);
    }

    private static ProcessRuntimeOwnedStepCoordinator CreateCoordinator(
        params IProcessRuntimeOwnedStepExecutor[] executors)
        => new(executors, null!, new ProcessToolReceiptPolicyCatalog([]));

    private static ProcessRuntimeStepAssignment CreateAssignment(string? executorKey = null)
    {
        var launchVariables = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(executorKey))
        {
            launchVariables[ProcessRuntimeLaunchVariables.ProcessStepRuntimeOwnedExecutorKey] = executorKey;
        }

        return new ProcessRuntimeStepAssignment(
            ProcessRunId.New(),
            ProcessInstancePlanId.New(),
            ProcessStepInstanceId.New(),
            "template-owned-step",
            "test-role",
            "test-role",
            "Test role",
            ProcessLaunchExecutorKinds.Agent,
            Guid.NewGuid().ToString("D"),
            "Test executor",
            "Test runtime-owned assignment.",
            "sha256:test",
            "Test assignment.",
            [],
            [],
            [],
            ProcessOperationContractNames.ExternalProductTargetMutable,
            launchVariables,
            BranchGate: null,
            DateTimeOffset.UtcNow);
    }

    private sealed class TestRuntimeOwnedStepExecutor(
        string executorKey,
        ProcessRuntimeOwnedStepExecutionResult? result) : IProcessRuntimeOwnedStepExecutor
    {
        public string ExecutorKey { get; } = executorKey;

        public int CallCount { get; private set; }

        public ValueTask<ProcessRuntimeOwnedStepExecutionResult?> TryExecuteAsync(
            ProcessRuntimeStepAssignment assignment,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(result);
        }
    }
}
