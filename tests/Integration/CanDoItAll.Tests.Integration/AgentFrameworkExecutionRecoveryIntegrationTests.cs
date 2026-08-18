using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class AgentFrameworkExecutionRecoveryIntegrationTests
{
    [Fact]
    public async Task Startup_recovery_marks_non_resumable_execution_runs_as_cancelled()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-execution-recovery");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                var recoveryWorkerType = typeof(AgentFrameworkModuleAssemblyMarker).Assembly.GetType(
                    "CanDoItAll.Modules.AgentFramework.AgentFrameworkExecutionRecoveryWorker",
                    throwOnError: true)
                    ?? throw new InvalidOperationException("AgentFrameworkExecutionRecoveryWorker type was not found.");
                services.RemoveAll<IHostedService>();
                services.AddSingleton<IHostedService>(serviceProvider =>
                    (IHostedService)ActivatorUtilities.CreateInstance(serviceProvider, recoveryWorkerType));
            });

        var runId = Guid.Empty;
        await using (var scope = provider.CreateAsyncScope())
        {
            var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
            var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
            var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false)).First();
            var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);
            var createdAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
            runId = Guid.NewGuid();

            var persistedSession = session with
            {
                UpdatedAtUtc = createdAtUtc,
                LatestExecutionRunId = runId,
                Compatibility = ChatSessionRuntimeCompatibilityRecord.Create(
                    "runtime-session-key",
                    """{"kind":"partial"}""",
                    [],
                    autoApprovePendingToolCalls: true)
            };
            var run = new ExecutionRunRecord(
                Id: runId,
                AgentId: agent.Id,
                ChatSessionId: persistedSession.Id,
                Title: "Interrupted execution",
                SourceKind: "process-step",
                SourceId: "step-001",
                CorrelationId: "corr-001",
                CausationId: "cause-001",
                RequestedBy: "process-automation-dispatch",
                RequestedByKind: "system",
                MetadataJson: "{}",
                InputSummary: "Implement the feature.",
                ResultSummary: string.Empty,
                ProviderName: "OpenAI",
                Model: "gpt-5.4",
                State: ExecutionState.Running,
                Outcome: null,
                CreatedAtUtc: createdAtUtc,
                UpdatedAtUtc: createdAtUtc,
                StartedAtUtc: createdAtUtc,
                CompletedAtUtc: null,
                RuntimeSessionKey: "runtime-session-key",
                SerializedSessionStateJson: """{"kind":"partial"}""",
                PendingApprovals: [],
                AutoApprovePendingToolCalls: true,
                Revision: 1);
            var detail = new ExecutionRunDetail(
                run,
                persistedSession,
                [],
                [])
            {
                Approvals = [],
                Artifacts = [],
                Checkpoints = [],
                ToolReceipts = []
            };

            await executionRunStore.SaveExecutionRunDetailAsync(detail);
        }

        var lifetime = provider.GetRequiredService<IHostApplicationLifetime>() as TestHostApplicationLifetime;
        var hostedServices = provider.GetServices<IHostedService>().ToList();
        lifetime?.NotifyStarted();
        try
        {
            foreach (var hostedService in hostedServices)
            {
                await hostedService.StartAsync(CancellationToken.None);
            }

            await WaitForAsync(async () =>
            {
                await using var scope = provider.CreateAsyncScope();
                var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
                var detail = await executionRunStore.GetExecutionRunDetailAsync(runId);
                return detail?.Run.Outcome == RunOutcome.Cancelled;
            }, TimeSpan.FromSeconds(5));

            await using var verificationScope = provider.CreateAsyncScope();
            var verificationStore = verificationScope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
            var repairedDetail = await verificationStore.GetExecutionRunDetailAsync(runId);

            Assert.NotNull(repairedDetail);
            Assert.Equal(ExecutionState.Failed, repairedDetail.Run.State);
            Assert.Equal(RunOutcome.Cancelled, repairedDetail.Run.Outcome);
            Assert.NotNull(repairedDetail.Run.CompletedAtUtc);
            Assert.Equal(string.Empty, repairedDetail.Run.RuntimeSessionKey);
            Assert.Null(repairedDetail.Run.SerializedSessionStateJson);
            Assert.Empty(repairedDetail.Run.PendingApprovals);
            Assert.NotNull(repairedDetail.ChatSession);
            Assert.Null(repairedDetail.ChatSession.Compatibility);
            Assert.Contains(
                repairedDetail.ExecutionLog,
                entry => entry.ExecutionRunId == runId &&
                         entry.Phase == "startup-recovery" &&
                         entry.State == ExecutionState.Failed);
        }
        finally
        {
            lifetime?.NotifyStopping();
            foreach (var hostedService in hostedServices.AsEnumerable().Reverse())
            {
                await hostedService.StopAsync(CancellationToken.None);
            }

            lifetime?.NotifyStopped();
        }
    }

    [Fact]
    public async Task Startup_recovery_skips_execution_runs_created_after_recovery_worker_started()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-execution-recovery-fresh-run");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services => services.RemoveAll<IHostedService>());

        var startupCutoffUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
        var runId = Guid.Empty;
        await using (var scope = provider.CreateAsyncScope())
        {
            var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
            var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
            var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false)).First();
            var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);
            var createdAtUtc = DateTimeOffset.UtcNow;
            runId = Guid.NewGuid();

            var persistedSession = session with
            {
                UpdatedAtUtc = createdAtUtc,
                LatestExecutionRunId = runId,
                Compatibility = ChatSessionRuntimeCompatibilityRecord.Create(
                    "runtime-session-key",
                    """{"kind":"partial"}""",
                    [],
                    autoApprovePendingToolCalls: true)
            };
            var run = new ExecutionRunRecord(
                Id: runId,
                AgentId: agent.Id,
                ChatSessionId: persistedSession.Id,
                Title: "Fresh execution",
                SourceKind: "process-step",
                SourceId: "step-001",
                CorrelationId: "corr-001",
                CausationId: "cause-001",
                RequestedBy: "process-automation-dispatch",
                RequestedByKind: "system",
                MetadataJson: "{}",
                InputSummary: "Implement the feature.",
                ResultSummary: string.Empty,
                ProviderName: "OpenAI",
                Model: "gpt-4.1",
                State: ExecutionState.Running,
                Outcome: null,
                CreatedAtUtc: createdAtUtc,
                UpdatedAtUtc: createdAtUtc,
                StartedAtUtc: createdAtUtc,
                CompletedAtUtc: null,
                RuntimeSessionKey: "runtime-session-key",
                SerializedSessionStateJson: """{"kind":"partial"}""",
                PendingApprovals: [],
                AutoApprovePendingToolCalls: true,
                Revision: 1);
            var detail = new ExecutionRunDetail(
                run,
                persistedSession,
                [],
                [])
            {
                Approvals = [],
                Artifacts = [],
                Checkpoints = [],
                ToolReceipts = []
            };

            await executionRunStore.SaveExecutionRunDetailAsync(detail);
        }

        await using var recoveryScope = provider.CreateAsyncScope();
        var recoveryServiceType = typeof(AgentFrameworkModuleAssemblyMarker).Assembly.GetType(
            "CanDoItAll.Modules.AgentFramework.AgentFrameworkExecutionRecoveryService",
            throwOnError: true)
            ?? throw new InvalidOperationException("AgentFrameworkExecutionRecoveryService type was not found.");
        var recoveryService = recoveryScope.ServiceProvider.GetRequiredService(recoveryServiceType);
        var recoverMethod = recoveryServiceType.GetMethod(
            "RecoverInterruptedRunsAsync",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            [typeof(DateTimeOffset), typeof(CancellationToken)])
            ?? throw new InvalidOperationException("RecoverInterruptedRunsAsync overload was not found.");
        var recoverTask = recoverMethod.Invoke(recoveryService, [startupCutoffUtc, CancellationToken.None])
            as Task<int>;
        Assert.NotNull(recoverTask);
        var recoveredCount = await recoverTask!;

        var verificationStore = recoveryScope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var freshDetail = await verificationStore.GetExecutionRunDetailAsync(runId);

        Assert.Equal(0, recoveredCount);
        Assert.NotNull(freshDetail);
        Assert.Equal(ExecutionState.Running, freshDetail.Run.State);
        Assert.Null(freshDetail.Run.Outcome);
    }

    private static async Task WaitForAsync(
        Func<Task<bool>> condition,
        TimeSpan timeout,
        TimeSpan? pollInterval = null)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var delay = pollInterval ?? TimeSpan.FromMilliseconds(100);
        while (DateTimeOffset.UtcNow - startedAtUtc < timeout)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(delay);
        }

        Assert.Fail($"Timed out after {timeout} waiting for the expected condition.");
    }
}
