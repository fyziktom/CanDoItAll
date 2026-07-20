using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration;

public sealed class AgentFrameworkExecutionRunTrackingIntegrationTests
{
    [Fact]
    public async Task ExecuteRunAsync_refreshes_run_header_while_progress_logs_are_streaming()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-run-tracking");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IAgentRuntime>();
                services.AddSingleton<FakeProgressAgentRuntime>();
                services.AddSingleton<IAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<FakeProgressAgentRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<FakeProgressAgentRuntime>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);
        var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);

        var executionTask = workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agent.Id,
                "Implement the current process step.",
                session.Id,
                new ExecutionInvocationContext(
                    SourceKind: "process-step",
                    SourceId: "step-001",
                    CorrelationId: "corr-001",
                    CausationId: "cause-001",
                    RequestedBy: "process-automation-dispatch",
                    RequestedByKind: "system",
                    MetadataJson: "{}",
                    ProcessRunId: "run-001",
                    ProcessStepId: "step-001"),
                AutoApprovePendingToolCalls: true));

        var executionRunId = await WaitForExecutionRunIdAsync(runtime, executionTask);
        await runtime.ProgressPersisted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var detail = await executionRunStore.GetExecutionRunDetailAsync(executionRunId);

        Assert.NotNull(detail);
        Assert.Equal(ExecutionState.Running, detail.Run.State);
        Assert.NotEmpty(detail.ExecutionLog);
        Assert.True(detail.Run.UpdatedAtUtc >= detail.ExecutionLog.Max(item => item.CreatedAtUtc));
        Assert.NotNull(detail.ChatSession);
        Assert.Equal(executionRunId, detail.ChatSession!.LatestExecutionRunId);
        Assert.Contains(
            detail.ExecutionLog,
            entry => entry.ExecutionRunId == executionRunId &&
                     entry.State == ExecutionState.Running &&
                     entry.Phase == "Implementation");

        runtime.AllowCompletion.TrySetResult(true);

        var result = await executionTask;
        Assert.Equal(executionRunId, result.ExecutionRunId);
    }

    [Fact]
    public async Task ExecuteRunAsync_finalizes_run_when_caller_cancels_after_runtime_started()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-run-caller-cancel");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IAgentRuntime>();
                services.AddSingleton<FakeProgressAgentRuntime>();
                services.AddSingleton<IAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<FakeProgressAgentRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<FakeProgressAgentRuntime>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);
        var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);
        using var callerCancellation = new CancellationTokenSource();

        var executionTask = workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agent.Id,
                "Run a slow provider-backed validation.",
                session.Id,
                new ExecutionInvocationContext(
                    SourceKind: "chat-session",
                    SourceId: session.Id.ToString("N"),
                    CorrelationId: "corr-caller-cancel",
                    CausationId: "cause-caller-cancel",
                    RequestedBy: "integration-test",
                    RequestedByKind: "test",
                    MetadataJson: "{}"),
                AutoApprovePendingToolCalls: true),
            callerCancellation.Token);

        var executionRunId = await WaitForExecutionRunIdAsync(runtime, executionTask);
        await runtime.ProgressPersisted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        callerCancellation.Cancel();

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => executionTask);
        var detail = await executionRunStore.GetExecutionRunDetailAsync(executionRunId);

        Assert.Contains("caller request was cancelled", exception.Message, StringComparison.Ordinal);
        Assert.NotNull(detail);
        Assert.Equal(ExecutionState.Failed, detail.Run.State);
        Assert.Equal(RunOutcome.Cancelled, detail.Run.Outcome);
        Assert.Contains("caller request was cancelled", detail.Run.ResultSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            detail.ExecutionLog,
            entry => entry.ExecutionRunId == executionRunId &&
                     entry.State == ExecutionState.Failed &&
                     entry.Phase == "Cancelled" &&
                     entry.Message.Contains("caller request was cancelled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteRunAsync_starts_chat_backed_run_without_loading_unrelated_run_slices()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-chat-split-store");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IAgentRuntime>();
                services.AddSingleton<FakeProgressAgentRuntime>();
                services.AddSingleton<IAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<FakeProgressAgentRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<FakeProgressAgentRuntime>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var workspaceRoot = scope.ServiceProvider.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();
        var workspaceScope = ResolveWorkspaceScope(scope.ServiceProvider);
        var layout = new FileSandboxWorkspaceStorageLayout(workspaceRoot, workspaceScope);
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);

        var unrelatedRunId = Guid.NewGuid();
        await executionRunStore.SaveExecutionRunDetailAsync(CreateCompletedRunDetail(unrelatedRunId, agent.Id, "Unrelated corrupt receipt run"));
        var unrelatedReceiptsRoot = layout.RunReceiptsRoot(unrelatedRunId);
        Directory.CreateDirectory(unrelatedReceiptsRoot);
        await File.WriteAllTextAsync(
            Path.Combine(unrelatedReceiptsRoot, "corrupt-receipt.json"),
            "{ this is not valid json",
            CancellationToken.None);

        var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);
        runtime.AllowCompletion.TrySetResult(true);

        var result = await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agent.Id,
                "Confirm that chat start avoids unrelated run slices.",
                session.Id,
                new ExecutionInvocationContext(
                    SourceKind: "chat-session",
                    SourceId: session.Id.ToString("N"),
                    CorrelationId: Guid.NewGuid().ToString("N"),
                    CausationId: session.Id.ToString("N"),
                    RequestedBy: "integration-test",
                    RequestedByKind: "test",
                    MetadataJson: "{}")));

        var detail = await executionRunStore.GetExecutionRunDetailAsync(result.ExecutionRunId);

        Assert.NotNull(detail);
        Assert.Equal(ExecutionState.Completed, detail!.Run.State);
        Assert.NotNull(detail.ChatSession);
        Assert.Equal(result.ExecutionRunId, detail.ChatSession!.LatestExecutionRunId);
        Assert.Contains(detail.ChatSession.Messages, message => message.Role == ChatMessageRole.Assistant);
    }

    [Fact]
    public async Task SendMessageAsync_starts_chat_run_without_loading_unrelated_run_slices()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-send-message-split-store");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IAgentRuntime>();
                services.AddSingleton<FakeProgressAgentRuntime>();
                services.AddSingleton<IAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<FakeProgressAgentRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<FakeProgressAgentRuntime>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var workspaceRoot = scope.ServiceProvider.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();
        var workspaceScope = ResolveWorkspaceScope(scope.ServiceProvider);
        var layout = new FileSandboxWorkspaceStorageLayout(workspaceRoot, workspaceScope);
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);

        var unrelatedRunId = Guid.NewGuid();
        await executionRunStore.SaveExecutionRunDetailAsync(CreateCompletedRunDetail(unrelatedRunId, agent.Id, "Unrelated corrupt send receipt run"));
        var unrelatedReceiptsRoot = layout.RunReceiptsRoot(unrelatedRunId);
        Directory.CreateDirectory(unrelatedReceiptsRoot);
        await File.WriteAllTextAsync(
            Path.Combine(unrelatedReceiptsRoot, "corrupt-receipt.json"),
            "{ this is not valid json",
            CancellationToken.None);

        var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);
        runtime.AllowCompletion.TrySetResult(true);

        var result = await workspaceService.SendMessageAsync(
            agent.Id,
            session.Id,
            "Confirm that SendMessageAsync avoids unrelated run slices.");
        var detail = await executionRunStore.GetExecutionRunDetailAsync(result.ExecutionRunId);

        Assert.NotNull(detail);
        Assert.Equal(ExecutionState.Completed, detail!.Run.State);
        Assert.Equal(session.Id, result.ChatSessionId);
        Assert.Equal(result.ExecutionRunId, detail.ChatSession!.LatestExecutionRunId);
        Assert.Equal(ChatMessageRole.Assistant, result.AssistantMessage.Role);
        Assert.Contains(detail.ChatSession.Messages, message => message.Role == ChatMessageRole.Assistant);
    }

    [Fact]
    public async Task RespondToPendingApprovalsAsync_continues_chat_run_without_loading_unrelated_run_slices()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-approval-split-store");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IAgentRuntime>();
                services.AddSingleton<StructuredOutputApprovalRuntime>();
                services.AddSingleton<IAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var workspaceRoot = scope.ServiceProvider.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();
        var workspaceScope = ResolveWorkspaceScope(scope.ServiceProvider);
        var layout = new FileSandboxWorkspaceStorageLayout(workspaceRoot, workspaceScope);
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);

        var unrelatedRunId = Guid.NewGuid();
        await executionRunStore.SaveExecutionRunDetailAsync(CreateCompletedRunDetail(unrelatedRunId, agent.Id, "Unrelated corrupt approval receipt run"));
        var unrelatedReceiptsRoot = layout.RunReceiptsRoot(unrelatedRunId);
        Directory.CreateDirectory(unrelatedReceiptsRoot);
        await File.WriteAllTextAsync(
            Path.Combine(unrelatedReceiptsRoot, "corrupt-receipt.json"),
            "{ this is not valid json",
            CancellationToken.None);

        var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);
        var pendingResult = await workspaceService.SendMessageAsync(
            agent.Id,
            session.Id,
            "Request approval before completing this chat run.");
        var pendingDetail = await executionRunStore.GetExecutionRunDetailAsync(pendingResult.ExecutionRunId);

        Assert.NotNull(pendingDetail);
        Assert.Equal(ExecutionState.WaitingOnTool, pendingDetail!.Run.State);
        Assert.NotEmpty(pendingDetail.Run.PendingApprovals);

        var completedResult = await workspaceService.RespondToPendingApprovalsAsync(
            agent.Id,
            session.Id,
            approved: true,
            autoApprovePendingToolCalls: false);
        var completedDetail = await executionRunStore.GetExecutionRunDetailAsync(completedResult.ExecutionRunId);

        Assert.NotNull(completedDetail);
        Assert.Equal(pendingResult.ExecutionRunId, completedResult.ExecutionRunId);
        Assert.Equal(ExecutionState.Completed, completedDetail!.Run.State);
        Assert.Empty(completedDetail.Run.PendingApprovals);
        Assert.Contains(completedDetail.ChatSession!.Messages, message => message.Role == ChatMessageRole.Assistant);
    }

    [Fact]
    public async Task ContinueExecutionRunAsync_preserves_structured_output_contract_after_pending_approval()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-structured-output-continuation");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IAgentRuntime>();
                services.AddSingleton<StructuredOutputApprovalRuntime>();
                services.AddSingleton<IAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<StructuredOutputApprovalRuntime>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);
        var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);

        var initialResult = await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agent.Id,
                "Implement the process step and request approval for the write.",
                session.Id,
                CreateProcessStepContext(),
                AutoApprovePendingToolCalls: false,
                StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult));

        var pendingDetail = await executionRunStore.GetExecutionRunDetailAsync(initialResult.ExecutionRunId);
        Assert.NotNull(pendingDetail);
        Assert.Equal(ExecutionState.WaitingOnTool, pendingDetail.Run.State);
        Assert.NotEmpty(pendingDetail.Run.PendingApprovals);
        Assert.Equal(AgentStructuredOutputContracts.ProcessStepOutcomeResultKey, pendingDetail.Run.StructuredOutputContractKey);
        Assert.NotEmpty(pendingDetail.Run.StructuredOutputTypeName);

        var continuationResult = await workspaceService.ContinueExecutionRunAsync(
            initialResult.ExecutionRunId,
            approved: true,
            autoApprovePendingToolCalls: false);

        var completedDetail = await executionRunStore.GetExecutionRunDetailAsync(continuationResult.ExecutionRunId);
        Assert.NotNull(completedDetail);
        Assert.Equal(ExecutionState.Completed, completedDetail.Run.State);
        Assert.Equal(RunOutcome.Succeeded, completedDetail.Run.Outcome);
        Assert.Single(runtime.RunStructuredOutputs);
        Assert.Single(runtime.ContinuationStructuredOutputs);
        Assert.Equal(
            AgentStructuredOutputContracts.ProcessStepOutcomeResultKey,
            runtime.RunStructuredOutputs[0]?.ContractKey);
        Assert.Equal(
            AgentStructuredOutputContracts.ProcessStepOutcomeResultKey,
            runtime.ContinuationStructuredOutputs[0]?.ContractKey);
        Assert.Contains(
            completedDetail.ExecutionLog,
            entry => entry.Phase == "Output validation" &&
                     entry.Message.Contains("Validated structured output contract", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteRunAsync_persists_provider_usage_without_prompt_double_counting()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-provider-usage-metrics");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IAgentRuntime>();
                services.AddSingleton(new StructuredOutputApprovalRuntime
                {
                    InitialCachedInputTokens = 3,
                    ContinuationCachedInputTokens = 2
                });
                services.AddSingleton<IAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);
        var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);

        var result = await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agent.Id,
                "Complete the process step after the approval is automatically granted.",
                session.Id,
                CreateProcessStepContext(),
                AutoApprovePendingToolCalls: true,
                StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult));
        var detail = await executionRunStore.GetExecutionRunDetailAsync(result.ExecutionRunId);

        Assert.NotNull(detail);
        Assert.Equal(ExecutionState.Completed, detail.Run.State);

        var metric = Assert.Single(detail.Metrics);
        Assert.Equal(12, metric.InputTokens);
        Assert.Equal(5, metric.CachedInputTokens);
        Assert.Equal(24, metric.OutputTokens);
        Assert.Equal(1, metric.ToolCalls);
        Assert.Equal(result.ExecutionRunId, metric.ExecutionRunId);

        var usage = Assert.Single(detail.UsageObservations);
        Assert.Equal(ProviderUsageObservationStatus.ObservedFromMetric, usage.UsageStatus);
        Assert.Equal(ProviderUsageSourcePhases.AgentRuntime, usage.SourcePhase);
        Assert.Equal(12, usage.InputTokens);
        Assert.Equal(5, usage.CachedInputTokens);
        Assert.Equal(24, usage.OutputTokens);
        Assert.Equal(36, usage.TotalTokens);
        Assert.Equal("run-001", usage.ProcessRunId);
        Assert.Equal("step-001", usage.ProcessStepId);
        Assert.Equal(result.ExecutionRunId, usage.ExecutionRunId);
    }

    [Fact]
    public async Task ExecuteRunAsync_persists_context_manifest_in_usage_diagnostics()
    {
        var contextManifest = CreateContextManifest();
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-context-manifest");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IAgentRuntime>();
                services.AddSingleton(new StructuredOutputApprovalRuntime
                {
                    InitialPendingApprovals = [],
                    InitialResponseText = "Completed successfully.",
                    InitialContextAssemblyManifest = contextManifest
                });
                services.AddSingleton<IAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);
        var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);

        var result = await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agent.Id,
                "Run with context manifest.",
                session.Id,
                CreateProcessStepContext(),
                AutoApprovePendingToolCalls: true));

        var detail = await executionRunStore.GetExecutionRunDetailAsync(result.ExecutionRunId);
        Assert.NotNull(detail);
        var usage = Assert.Single(detail.UsageObservations);
        using var diagnostics = JsonDocument.Parse(usage.DiagnosticsJson);
        var manifest = diagnostics.RootElement.GetProperty("contextAssemblyManifest");
        Assert.Equal(contextManifest.Id, manifest.GetProperty("id").GetGuid());
        Assert.Equal(7, manifest.GetProperty("totals").GetProperty("estimatedInputTokens").GetInt32());
        Assert.Equal("workspace-tools", manifest.GetProperty("sources")[0].GetProperty("category").GetString());
    }

    [Fact]
    public async Task ExecuteRunAsync_passes_process_context_intent_for_two_agent_steps()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-two-step-context-intent");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IAgentRuntime>();
                services.AddSingleton(new StructuredOutputApprovalRuntime
                {
                    InitialPendingApprovals = [],
                    InitialResponseText = "Completed successfully."
                });
                services.AddSingleton<IAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<StructuredOutputApprovalRuntime>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);
        var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);

        await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agent.Id,
                "Read the process context.",
                session.Id,
                CreateProcessStepContext(
                    CreateProcessStepMetadata(
                        ProcessOperationContractNames.ExternalProductTargetReadOnly,
                        allowsProductMutation: false,
                        ProcessOperationContractNames.ReadProcessContext),
                    sourceId: "step-read",
                    processRunId: "run-two-step",
                    processStepId: "step-read"),
                AutoApprovePendingToolCalls: true));
        await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agent.Id,
                "Run validation.",
                session.Id,
                CreateProcessStepContext(
                    CreateProcessStepMetadata(
                        ProcessOperationContractNames.ExternalProductTargetReadOnly,
                        allowsProductMutation: false,
                        ProcessOperationContractNames.RunValidation),
                    sourceId: "step-validate",
                    processRunId: "run-two-step",
                    processStepId: "step-validate"),
                AutoApprovePendingToolCalls: true));

        Assert.Equal(2, runtime.RunExecutionOptions.Count);
        var readIntent = runtime.RunExecutionOptions[0]?.ContextIntent;
        var validationIntent = runtime.RunExecutionOptions[1]?.ContextIntent;
        Assert.NotNull(readIntent);
        Assert.NotNull(validationIntent);
        Assert.True(readIntent!.IsGovernedProcessStep);
        Assert.True(validationIntent!.IsGovernedProcessStep);
        Assert.Equal("step-read", readIntent.SourceId);
        Assert.Equal("step-validate", validationIntent.SourceId);
        Assert.Contains(ProcessOperationContractNames.ReadProcessContext, readIntent.AllowedOperations);
        Assert.DoesNotContain(ProcessOperationContractNames.RunValidation, readIntent.AllowedOperations);
        Assert.Contains(ProcessOperationContractNames.RunValidation, validationIntent.AllowedOperations);
        Assert.False(readIntent.AllowsProductMutation);
        Assert.False(validationIntent.AllowsProductMutation);
    }

    [Fact]
    public async Task ExecuteRunAsync_preserves_usage_when_runtime_fails_after_provider_call()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-runtime-failure-usage");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IAgentRuntime>();
                services.AddSingleton(new StructuredOutputApprovalRuntime
                {
                    ThrowUsageExceptionOnRun = true,
                    InitialPendingApprovals = [],
                    InitialUsageObservations =
                    [
                        CreateUsageObservation(ProviderUsageSourcePhases.AgentRuntime, 31, 4, 9)
                    ]
                });
                services.AddSingleton<IAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<StructuredOutputApprovalRuntime>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);

        await Assert.ThrowsAsync<AgentRunFailedException>(() =>
            workspaceService.ExecuteRunAsync(
                new ExecutionRunRequest(
                    agent.Id,
                    "Fail after provider usage is available.",
                    ChatSessionId: null,
                    Context: CreateProcessStepContext())));

        var executionRunId = Assert.Single(runtime.ObservedExecutionRunIds);
        var failedDetail = await executionRunStore.GetExecutionRunDetailAsync(executionRunId);

        Assert.NotNull(failedDetail);
        Assert.Equal(ExecutionState.Failed, failedDetail.Run.State);
        var usage = Assert.Single(failedDetail.UsageObservations);
        Assert.Equal(ProviderUsageObservationStatus.Observed, usage.UsageStatus);
        Assert.Equal(31, usage.InputTokens);
        Assert.Equal(4, usage.CachedInputTokens);
        Assert.Equal(9, usage.OutputTokens);
        Assert.Equal(executionRunId, usage.ExecutionRunId);
        Assert.Equal("run-001", usage.ProcessRunId);
    }

    [Fact]
    public async Task ExecuteRunAsync_links_structured_output_repair_usage_to_execution_run()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-repair-usage");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IAgentRuntime>();
                services.AddSingleton(new StructuredOutputApprovalRuntime
                {
                    InitialResponseText = "not machine json",
                    InitialPendingApprovals = []
                });
                services.AddSingleton<IAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                services.RemoveAll<IAgentOutputRepairService>();
                services.AddSingleton<IAgentOutputRepairService>(new UsageReportingRepairService());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);

        var result = await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agent.Id,
                "Repair invalid structured output.",
                ChatSessionId: null,
                Context: CreateProcessStepContext(),
                StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult));
        var detail = await executionRunStore.GetExecutionRunDetailAsync(result.ExecutionRunId);

        Assert.NotNull(detail);
        Assert.Equal(ExecutionState.Completed, detail.Run.State);
        Assert.Contains(detail.UsageObservations, item => item.SourcePhase == ProviderUsageSourcePhases.AgentRuntime);
        var repairUsage = Assert.Single(detail.UsageObservations, item => item.SourcePhase == ProviderUsageSourcePhases.StructuredOutputRepair);
        Assert.Equal(ProviderUsageObservationStatus.Observed, repairUsage.UsageStatus);
        Assert.Equal(3, repairUsage.InputTokens);
        Assert.Equal(1, repairUsage.CachedInputTokens);
        Assert.Equal(2, repairUsage.OutputTokens);
        Assert.Equal(result.ExecutionRunId, repairUsage.ExecutionRunId);
        Assert.Equal("run-001", repairUsage.ProcessRunId);
    }

    [Fact]
    public async Task ExecuteRunAsync_fails_governed_run_when_structured_output_is_invalid()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-invalid-structured-output");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IAgentRuntime>();
                services.AddSingleton(new StructuredOutputApprovalRuntime
                {
                    InitialResponseText = "This is prose, not machine JSON.",
                    InitialPendingApprovals = []
                });
                services.AddSingleton<IAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<StructuredOutputApprovalRuntime>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);

        var exception = await Assert.ThrowsAsync<AgentRunFailedException>(() =>
            workspaceService.ExecuteRunAsync(
                new ExecutionRunRequest(
                    agent.Id,
                    "Return invalid machine output for the process step.",
                    ChatSessionId: null,
                    Context: CreateProcessStepContext(),
                    AutoApprovePendingToolCalls: false,
                    StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult)));

        var executionRunId = Assert.Single(runtime.ObservedExecutionRunIds);
        var failedDetail = await executionRunStore.GetExecutionRunDetailAsync(executionRunId);

        Assert.Contains("failed validation", exception.Message, StringComparison.Ordinal);
        Assert.NotNull(failedDetail);
        Assert.Equal(ExecutionState.Failed, failedDetail.Run.State);
        Assert.Equal(RunOutcome.Failed, failedDetail.Run.Outcome);
        var usage = Assert.Single(failedDetail.UsageObservations);
        Assert.Equal(ProviderUsageObservationStatus.ObservedFromMetric, usage.UsageStatus);
        Assert.Equal(7, usage.InputTokens);
        Assert.Equal(11, usage.OutputTokens);
        Assert.Equal(executionRunId, usage.ExecutionRunId);
        Assert.Contains(
            failedDetail.ExecutionLog,
            entry => entry.Phase == "Output validation" &&
                     entry.Message.Contains("failed validation", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteRunAsync_records_shadow_finalizer_status_when_present()
    {
        var outcome = CreateCompletedOutcome("The finalizer and structured output agree.");
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-shadow-finalizer");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IAgentRuntime>();
                services.AddSingleton(new StructuredOutputApprovalRuntime
                {
                    InitialResponseText = SerializeOutcome(outcome),
                    InitialPendingApprovals = [],
                    InitialFinalizerInvocations = [CreateFinalizerInvocation(outcome)]
                });
                services.AddSingleton<IAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);

        var result = await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agent.Id,
                "Complete the process step and submit the shadow finalizer.",
                ChatSessionId: null,
                Context: CreateProcessStepContext(),
                AutoApprovePendingToolCalls: false,
                StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult));
        var detail = await executionRunStore.GetExecutionRunDetailAsync(result.ExecutionRunId);

        Assert.NotNull(detail);
        Assert.Equal(ExecutionState.Completed, detail.Run.State);
        Assert.Contains(
            detail.ExecutionLog,
            entry => entry.Phase == "Finalizer validation" &&
                     entry.Message.Contains("Shadow finalizer tool", StringComparison.Ordinal) &&
                     entry.Message.Contains("matched structured output", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteRunAsync_records_finalizer_short_circuit_usage_when_metrics_are_zero()
    {
        var outcome = CreateCompletedOutcome("The finalizer short-circuit has no metric tokens but still records provider activity.");
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-finalizer-short-circuit-usage");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IAgentRuntime>();
                services.AddSingleton(new StructuredOutputApprovalRuntime
                {
                    InitialResponseText = SerializeOutcome(outcome),
                    InitialPendingApprovals = [],
                    InitialInputTokens = 0,
                    InitialOutputTokens = 0,
                    InitialFinalizerInvocations = [CreateFinalizerInvocation(outcome)],
                    InitialUsageObservations =
                    [
                        CreateUsageObservation(
                            ProviderUsageSourcePhases.FinalizerShortCircuit,
                            0,
                            0,
                            0,
                            ProviderUsageObservationStatus.MissingAfterProviderActivity)
                    ]
                });
                services.AddSingleton<IAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);

        var result = await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agent.Id,
                "Complete through a finalizer short-circuit with unavailable usage.",
                ChatSessionId: null,
                Context: CreateProcessStepContext(),
                AutoApprovePendingToolCalls: false,
                StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult));
        var detail = await executionRunStore.GetExecutionRunDetailAsync(result.ExecutionRunId);

        Assert.NotNull(detail);
        Assert.Equal(ExecutionState.Completed, detail.Run.State);
        var metric = Assert.Single(detail.Metrics);
        Assert.Equal(0, metric.InputTokens);
        Assert.Equal(0, metric.OutputTokens);
        var usage = Assert.Single(detail.UsageObservations);
        Assert.Equal(ProviderUsageObservationStatus.MissingAfterProviderActivity, usage.UsageStatus);
        Assert.Equal(ProviderUsageSourcePhases.FinalizerShortCircuit, usage.SourcePhase);
        Assert.Equal(result.ExecutionRunId, usage.ExecutionRunId);
        Assert.Equal("run-001", usage.ProcessRunId);
        Assert.Equal("step-001", usage.ProcessStepId);
    }

    [Fact]
    public async Task ExecuteRunAsync_required_finalizer_overrides_assistant_text()
    {
        const string longReason = "The required finalizer result is authoritative and intentionally long so the persisted execution result remains a parseable process-step outcome with all blocker, evidence, and next-action details preserved.";
        var outcome = CreateCompletedOutcome(longReason);
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-required-finalizer");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IAgentRuntime>();
                services.AddSingleton(new StructuredOutputApprovalRuntime
                {
                    InitialResponseText = "Display-only assistant text.",
                    InitialPendingApprovals = [],
                    InitialFinalizerInvocations = [CreateFinalizerInvocation(outcome)]
                });
                services.AddSingleton<IAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);

        var result = await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agent.Id,
                "Complete the process step through the required finalizer.",
                ChatSessionId: null,
                Context: CreateProcessStepContext(CreateRequiredFinalizerMetadata()),
                AutoApprovePendingToolCalls: false,
                StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult));
        var detail = await executionRunStore.GetExecutionRunDetailAsync(result.ExecutionRunId);

        Assert.StartsWith("{", result.ResponseText);
        Assert.Contains("authoritative", result.ResponseText, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(detail);
        Assert.Equal(ExecutionState.Completed, detail.Run.State);
        Assert.Equal(result.ResponseText, detail.Run.ResultSummary);
        Assert.Contains("parseable process-step outcome", detail.Run.ResultSummary, StringComparison.Ordinal);
        Assert.Contains(
            detail.ExecutionLog,
            entry => entry.Phase == "Finalizer validation" &&
                     entry.Message.Contains("Required finalizer tool", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteRunAsync_required_finalizer_fails_when_validation_tool_runs_after_finalizer()
    {
        var outcome = CreateCompletedOutcome("The required finalizer cannot precede later validation.");
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-required-finalizer-sequence");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IAgentRuntime>();
                services.AddSingleton(new StructuredOutputApprovalRuntime
                {
                    InitialResponseText = "Display-only assistant text.",
                    InitialPendingApprovals = [],
                    InitialFinalizerInvocations = [CreateFinalizerInvocation(outcome)],
                    InitialToolInvocationTraces =
                    [
                        CreateToolInvocationTrace(
                            AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName,
                            ToolInvocationClassification.Read,
                            sequence: 1),
                        CreateToolInvocationTrace(
                            "workspace_dotnet_test",
                            ToolInvocationClassification.Validation,
                            sequence: 2)
                    ]
                });
                services.AddSingleton<IAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var runtime = scope.ServiceProvider.GetRequiredService<StructuredOutputApprovalRuntime>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);

        var exception = await Assert.ThrowsAsync<AgentRunFailedException>(() =>
            workspaceService.ExecuteRunAsync(
                new ExecutionRunRequest(
                    agent.Id,
                    "Complete the process step through the required finalizer.",
                    ChatSessionId: null,
                    Context: CreateProcessStepContext(CreateRequiredFinalizerMetadata()),
                    AutoApprovePendingToolCalls: false,
                    StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult)));
        var detail = await executionRunStore.GetExecutionRunDetailAsync(Assert.Single(runtime.ObservedExecutionRunIds));

        Assert.Contains("last significant tool invocation", exception.Message, StringComparison.Ordinal);
        Assert.NotNull(detail);
        Assert.Equal(ExecutionState.Failed, detail.Run.State);
        Assert.Contains(
            detail.ExecutionLog,
            entry => entry.Phase == "Finalizer sequencing" &&
                     entry.Message.Contains("workspace_dotnet_test#2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteRunAsync_persists_required_finalizer_output_as_assistant_transcript()
    {
        var outcome = CreateCompletedOutcome("The transcript must persist the authoritative finalizer output.");
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-required-finalizer-transcript");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IAgentRuntime>();
                services.AddSingleton(new StructuredOutputApprovalRuntime
                {
                    InitialResponseText = "Display-only assistant text.",
                    InitialPendingApprovals = [],
                    InitialFinalizerInvocations = [CreateFinalizerInvocation(outcome)]
                });
                services.AddSingleton<IAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);
        var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);

        var result = await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agent.Id,
                "Complete the process step through the required finalizer.",
                ChatSessionId: session.Id,
                Context: CreateProcessStepContext(CreateRequiredFinalizerMetadata()),
                AutoApprovePendingToolCalls: false,
                StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult));
        var detail = await executionRunStore.GetExecutionRunDetailAsync(result.ExecutionRunId);

        Assert.NotNull(detail?.ChatSession);
        var assistantMessage = Assert.Single(detail.ChatSession!.Messages, message => message.Role == ChatMessageRole.Assistant);
        Assert.Equal(result.ResponseText, assistantMessage.Content);
        Assert.Contains("authoritative", assistantMessage.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Display-only assistant text", assistantMessage.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteRunAsync_repairs_wrapped_structured_output_before_completion()
    {
        var outcome = CreateCompletedOutcome("The wrapped structured output was repaired before persistence.");
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-output-repair");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IAgentRuntime>();
                services.AddSingleton(new StructuredOutputApprovalRuntime
                {
                    InitialResponseText = $"The result follows:{Environment.NewLine}{SerializeOutcome(outcome)}",
                    InitialPendingApprovals = []
                });
                services.AddSingleton<IAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);

        var result = await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agent.Id,
                "Complete the process step with repairable machine output.",
                ChatSessionId: null,
                Context: CreateProcessStepContext(),
                AutoApprovePendingToolCalls: false,
                StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult));
        var detail = await executionRunStore.GetExecutionRunDetailAsync(result.ExecutionRunId);

        Assert.StartsWith("{", result.ResponseText, StringComparison.Ordinal);
        Assert.Contains("repaired before persistence", result.ResponseText, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(detail);
        Assert.Equal(ExecutionState.Completed, detail.Run.State);
        Assert.Contains(
            detail.ExecutionLog,
            entry => entry.Phase == "Output repair" &&
                     entry.Message.Contains("succeeded", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteRunAsync_required_finalizer_missing_prevents_completion()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-required-finalizer-missing");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IAgentRuntime>();
                services.AddSingleton(new StructuredOutputApprovalRuntime
                {
                    InitialResponseText = SerializeOutcome(CreateCompletedOutcome("Structured output alone is not enough in required mode.")),
                    InitialPendingApprovals = []
                });
                services.AddSingleton<IAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<StructuredOutputApprovalRuntime>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);

        var exception = await Assert.ThrowsAsync<AgentRunFailedException>(() =>
            workspaceService.ExecuteRunAsync(
                new ExecutionRunRequest(
                    agent.Id,
                    "Return structured output without the required finalizer.",
                    ChatSessionId: null,
                    Context: CreateProcessStepContext(CreateRequiredFinalizerMetadata()),
                    AutoApprovePendingToolCalls: false,
                    StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult)));
        var executionRunId = Assert.Single(runtime.ObservedExecutionRunIds);
        var failedDetail = await executionRunStore.GetExecutionRunDetailAsync(executionRunId);

        Assert.Contains("failed validation", exception.Message, StringComparison.Ordinal);
        Assert.NotNull(failedDetail);
        Assert.Equal(ExecutionState.Failed, failedDetail.Run.State);
        Assert.Contains(
            failedDetail.ExecutionLog,
            entry => entry.Phase == "Finalizer validation" &&
                     entry.Message.Contains("failed validation", StringComparison.Ordinal));
    }

    private static ExecutionInvocationContext CreateProcessStepContext(
        string metadataJson = "{}",
        string sourceId = "step-001",
        string processRunId = "run-001",
        string processStepId = "step-001")
    {
        return new ExecutionInvocationContext(
            SourceKind: "process-step",
            SourceId: sourceId,
            CorrelationId: "corr-001",
            CausationId: processStepId,
            RequestedBy: "process-automation-dispatch",
            RequestedByKind: "system",
            MetadataJson: metadataJson,
            ProcessRunId: processRunId,
            ProcessStepId: processStepId);
    }

    private static string CreateProcessStepMetadata(
        string targetScope,
        bool allowsProductMutation,
        params string[] allowedOperations)
    {
        var metadata = new Dictionary<string, object?>
        {
            [ExecutionInvocationMetadata.ProcessStepTargetScopeMetadataKey] = targetScope,
            [ExecutionInvocationMetadata.ProcessStepAllowsProductMutationMetadataKey] = allowsProductMutation,
            [ExecutionInvocationMetadata.ProcessStepAllowedOperationsMetadataKey] = allowedOperations
        };
        return JsonSerializer.Serialize(metadata, AgentOutputJson.SerializerOptions);
    }

    private static string CreateRequiredFinalizerMetadata()
    {
        return $$"""{"{{AgentFinalizerPolicies.FinalizerModeMetadataKey}}":"{{AgentFinalizerPolicies.RequiredFinalizerModeValue}}"}""";
    }

    private static ProcessStepOutcomeResult CreateCompletedOutcome(string reason)
    {
        return new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = reason,
            EvidenceRefs = ["execution://run-001"],
            NextActions = [],
            HumanReadableSummaryMarkdown = "Completed."
        };
    }

    private static ProviderUsageObservation CreateUsageObservation(
        string sourcePhase,
        int inputTokens,
        int cachedInputTokens,
        int outputTokens,
        ProviderUsageObservationStatus status = ProviderUsageObservationStatus.Observed)
    {
        return new ProviderUsageObservation(
            Id: Guid.NewGuid(),
            CreatedAtUtc: DateTimeOffset.UtcNow,
            ProviderName: "OpenAI default",
            ProviderKind: ProviderKind.OpenAi,
            Model: "gpt-5.4-mini",
            TransportKind: ProviderTransportKind.Responses,
            SourcePhase: sourcePhase,
            UsageStatus: status,
            InputTokens: inputTokens,
            CachedInputTokens: cachedInputTokens,
            OutputTokens: outputTokens,
            ReasoningTokens: 0,
            TotalTokens: inputTokens + outputTokens,
            ToolCallCount: 0);
    }

    private static AgentRuntimeContextAssemblyManifest CreateContextManifest()
    {
        return new AgentRuntimeContextAssemblyManifest(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            "test-agent",
            "OpenAI default",
            ProviderKind.OpenAi,
            "gpt-5.4-mini",
            ProviderTransportKind.Responses,
            AgentRuntimeContextIntent.Empty with
            {
                SourceKind = "process-step",
                SourceId = "step-001",
                ProcessRunId = "run-001",
                ProcessStepId = "step-001",
                IsGovernedProcessStep = true,
                AllowedOperations = [ProcessOperationContractNames.RunValidation]
            },
            new AgentRuntimeContextManifestTotals(
                InputMessageCount: 1,
                InputMessageChars: 12,
                InputMessageEstimatedTokens: 3,
                ToolCount: 2,
                ToolSchemaEstimatedChars: 16,
                ToolSchemaEstimatedTokens: 4,
                ContextProviderCount: 0,
                FrameworkToolCount: 0,
                RuntimeToolProviderCount: 0,
                EstimatedInputTokens: 7),
            [
                AgentRuntimeContextManifestSource.Included(
                    AgentRuntimeContextSourceCategories.WorkspaceTools,
                    "configured-workspace-tools",
                    "validation operation requires test tools",
                    itemCount: 2,
                    estimatedChars: 16)
            ]);
    }

    private static AgentFinalizerInvocation CreateFinalizerInvocation(ProcessStepOutcomeResult outcome)
    {
        return new AgentFinalizerInvocation(
            AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName,
            SerializeOutcome(outcome),
            Sequence: 1);
    }

    private static AgentToolInvocationTrace CreateToolInvocationTrace(
        string toolName,
        ToolInvocationClassification classification,
        int sequence)
    {
        var timestamp = DateTimeOffset.UtcNow;
        return new AgentToolInvocationTrace(
            toolName,
            classification,
            sequence,
            StartedAtUtc: timestamp,
            CompletedAtUtc: timestamp,
            Succeeded: true,
            FailureMessage: string.Empty);
    }

    private static string SerializeOutcome(ProcessStepOutcomeResult outcome)
    {
        return JsonSerializer.Serialize(outcome, AgentOutputJson.SerializerOptions);
    }

    private static void UseDirectWorkspaceService(IServiceCollection services)
    {
        services.RemoveAll<IAgentFrameworkWorkspaceService>();
        services.RemoveAll<IAgentPackageService>();
        services.RemoveAll<IProviderDiagnosticsService>();
        services.RemoveAll<IAgentExecutionCheckpointBridge>();
        services.RemoveAll<IAgentExecutionGovernanceBridge>();
        services.RemoveAll<IAgentExecutionEventSink>();
        services.AddScoped<IAgentPackageService>(serviceProvider => new ZipAgentPackageService(
            serviceProvider.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot(),
            ResolveWorkspaceScope(serviceProvider)));
        services.AddScoped<IProviderDiagnosticsService>(serviceProvider => new ProviderDiagnosticsService(
            serviceProvider.GetRequiredService<IAgentRuntime>()));
        services.AddScoped<IAgentExecutionCheckpointBridge>(serviceProvider => new WorkflowBackedAgentExecutionCheckpointBridge(
            serviceProvider.GetRequiredService<ISandboxWorkspaceStore>(),
            serviceProvider.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot(),
            ResolveWorkspaceScope(serviceProvider)));
        services.AddScoped<IAgentExecutionGovernanceBridge>(serviceProvider => new DurableAgentExecutionGovernanceBridge(
            serviceProvider.GetRequiredService<IAgentExecutionCheckpointBridge>()));
        services.AddScoped<IAgentExecutionEventSink, NullAgentExecutionEventSink>();
        services.AddScoped<IAgentFrameworkWorkspaceService, AgentFrameworkWorkspaceService>();
    }

    private static WorkspaceScopeDescriptor ResolveWorkspaceScope(IServiceProvider serviceProvider)
    {
        var profile = serviceProvider.GetRequiredService<IDatabaseProfileRuntimeAccessor>().ResolveCurrentProfile();
        return WorkspaceScopeDescriptor.Organization(profile.Profile.Id.ToString("N"));
    }

    private static async Task<Guid> WaitForExecutionRunIdAsync(
        FakeProgressAgentRuntime runtime,
        Task<ExecutionRunResult> executionTask)
    {
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
        var completedTask = await Task.WhenAny(runtime.ExecutionRunIdObserved.Task, executionTask, timeoutTask);
        if (completedTask == executionTask)
        {
            await executionTask;
        }

        if (completedTask == timeoutTask)
        {
            throw new TimeoutException("Timed out waiting for the fake runtime to observe the execution run id.");
        }

        return await runtime.ExecutionRunIdObserved.Task;
    }

    private static ExecutionRunDetail CreateCompletedRunDetail(Guid executionRunId, Guid agentId, string title)
    {
        var createdAtUtc = DateTimeOffset.UtcNow;
        return new ExecutionRunDetail(
            new ExecutionRunRecord(
                executionRunId,
                agentId,
                null,
                title,
                "manual",
                "split-store-regression",
                Guid.NewGuid().ToString("N"),
                string.Empty,
                "integration-test",
                "integration-test",
                "{}",
                "Verify chat-backed run creation does not load unrelated run slices.",
                "Completed",
                "OpenAI default",
                "gpt-4.1",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                createdAtUtc,
                createdAtUtc,
                createdAtUtc,
                createdAtUtc,
                string.Empty,
                null,
                []),
            null,
            [
                new ExecutionLogEntry(
                    Guid.NewGuid(),
                    agentId,
                    null,
                    createdAtUtc,
                    ExecutionState.Completed,
                    "validation",
                    "Unrelated run slice should not be loaded.")
                {
                    ExecutionRunId = executionRunId
                }
            ],
            []);
    }

    private sealed class FakeProgressAgentRuntime : IAgentRuntime
    {
        public TaskCompletionSource<Guid> ExecutionRunIdObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> ProgressPersisted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> AllowCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ProviderHealthResult> TestProviderAsync(
            ProviderProfile provider,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderHealthResult(true, "ok", []));
        }

        public Task<ProviderTestChatResult> RunProviderTestChatAsync(
            ProviderProfile provider,
            ProviderTestChatRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderTestChatResult(provider.DefaultModel, "ok", 1, 1));
        }

        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
            ProviderProfile provider,
            ProviderModelMaintenanceEditorRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderModelMaintenanceEditorResult(request.TargetModel, request.BaseModel, request.SystemPrompt, request.ContextLength, string.Empty, "ok"));
        }

        public async Task<AgentRuntimeResponse> RunAsync(
            AgentDefinition agent,
            ProviderProfile provider,
            ChatSessionRecord session,
            IReadOnlyList<CapabilityCatalogItem> capabilities,
            IReadOnlyList<AgentMemoryRecord> memory,
            string prompt,
            string? runtimeSessionKey,
            Func<ExecutionState, string, string, Task> progressCallback,
            CancellationToken cancellationToken = default,
            bool suppressApprovalRequirements = false,
            AgentStructuredOutputContract? structuredOutput = null,
            AgentRuntimeExecutionOptions? executionOptions = null)
        {
            if (!session.LatestExecutionRunId.HasValue)
            {
                throw new InvalidOperationException("Expected a chat-backed execution run to populate the latest execution run id.");
            }

            ExecutionRunIdObserved.TrySetResult(session.LatestExecutionRunId.Value);
            await progressCallback(ExecutionState.Running, "Implementation", "Applying the current implementation plan.");
            ProgressPersisted.TrySetResult(true);
            await AllowCompletion.Task.WaitAsync(cancellationToken);

            return new AgentRuntimeResponse(
                ResponseText: "Completed successfully.",
                InputTokens: 10,
                OutputTokens: 20,
                ToolCalls: 0,
                RuntimeSessionKey: string.Empty,
                SerializedSessionStateJson: null,
                PendingApprovals: []);
        }

        public Task<AgentRuntimeResponse> RespondToPendingApprovalsAsync(
            AgentDefinition agent,
            ProviderProfile provider,
            ChatSessionRecord session,
            IReadOnlyList<CapabilityCatalogItem> capabilities,
            IReadOnlyList<AgentMemoryRecord> memory,
            bool approved,
            string? runtimeSessionKey,
            Func<ExecutionState, string, string, Task> progressCallback,
            CancellationToken cancellationToken = default,
            bool suppressApprovalRequirements = false,
            AgentStructuredOutputContract? structuredOutput = null,
            AgentRuntimeExecutionOptions? executionOptions = null)
        {
            throw new NotSupportedException("Pending approval continuation is not used by this regression test.");
        }
    }

    private sealed class StructuredOutputApprovalRuntime : IAgentRuntime
    {
        public List<AgentStructuredOutputContract?> RunStructuredOutputs { get; } = [];

        public List<AgentStructuredOutputContract?> ContinuationStructuredOutputs { get; } = [];

        public List<AgentRuntimeExecutionOptions?> RunExecutionOptions { get; } = [];

        public List<Guid> ObservedExecutionRunIds { get; } = [];

        public string InitialResponseText { get; init; } = "Pending approval.";

        public int InitialInputTokens { get; init; } = 7;

        public int InitialOutputTokens { get; init; } = 11;

        public int InitialCachedInputTokens { get; init; }

        public IReadOnlyList<PendingToolApprovalRecord> InitialPendingApprovals { get; init; } =
        [
            new PendingToolApprovalRecord(
                "approval-001",
                "call-001",
                "workspace_write_file",
                "function",
                "Write artifacts/result.md.",
                """{"path":"artifacts/result.md"}""")
        ];

        public IReadOnlyList<AgentFinalizerInvocation> InitialFinalizerInvocations { get; init; } = [];

        public IReadOnlyList<AgentToolInvocationTrace> InitialToolInvocationTraces { get; init; } = [];

        public IReadOnlyList<ProviderUsageObservation> InitialUsageObservations { get; init; } = [];

        public AgentRuntimeContextAssemblyManifest? InitialContextAssemblyManifest { get; init; }

        public bool ThrowUsageExceptionOnRun { get; init; }

        public string ContinuationResponseText { get; init; } = JsonSerializer.Serialize(
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "The process step implementation was completed after approval.",
                EvidenceRefs = ["execution://run-001"],
                NextActions = [],
                HumanReadableSummaryMarkdown = "Completed."
            },
            AgentOutputJson.SerializerOptions);

        public int ContinuationCachedInputTokens { get; init; }

        public IReadOnlyList<AgentFinalizerInvocation> ContinuationFinalizerInvocations { get; init; } = [];

        public IReadOnlyList<AgentToolInvocationTrace> ContinuationToolInvocationTraces { get; init; } = [];

        public Task<ProviderHealthResult> TestProviderAsync(
            ProviderProfile provider,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderHealthResult(true, "ok", []));
        }

        public Task<ProviderTestChatResult> RunProviderTestChatAsync(
            ProviderProfile provider,
            ProviderTestChatRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderTestChatResult(provider.DefaultModel, "ok", 1, 1));
        }

        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
            ProviderProfile provider,
            ProviderModelMaintenanceEditorRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderModelMaintenanceEditorResult(request.TargetModel, request.BaseModel, request.SystemPrompt, request.ContextLength, string.Empty, "ok"));
        }

        public Task<AgentRuntimeResponse> RunAsync(
            AgentDefinition agent,
            ProviderProfile provider,
            ChatSessionRecord session,
            IReadOnlyList<CapabilityCatalogItem> capabilities,
            IReadOnlyList<AgentMemoryRecord> memory,
            string prompt,
            string? runtimeSessionKey,
            Func<ExecutionState, string, string, Task> progressCallback,
            CancellationToken cancellationToken = default,
            bool suppressApprovalRequirements = false,
            AgentStructuredOutputContract? structuredOutput = null,
            AgentRuntimeExecutionOptions? executionOptions = null)
        {
            RunStructuredOutputs.Add(structuredOutput);
            RunExecutionOptions.Add(executionOptions);
            if (!session.LatestExecutionRunId.HasValue)
            {
                throw new InvalidOperationException("Expected the runtime session to carry the execution run id.");
            }

            ObservedExecutionRunIds.Add(session.LatestExecutionRunId.Value);
            if (ThrowUsageExceptionOnRun)
            {
                throw new AgentRuntimeUsageException(
                    "Fake runtime failed after provider usage.",
                    new InvalidOperationException("Fake provider failure."),
                    InitialUsageObservations);
            }

            return Task.FromResult(new AgentRuntimeResponse(
                ResponseText: InitialResponseText,
                InputTokens: InitialInputTokens,
                OutputTokens: InitialOutputTokens,
                ToolCalls: InitialPendingApprovals.Count,
                RuntimeSessionKey: "runtime-session-key",
                SerializedSessionStateJson: """{"state":"pending"}""",
                PendingApprovals: InitialPendingApprovals)
            {
                CachedInputTokens = InitialCachedInputTokens,
                FinalizerInvocations = InitialFinalizerInvocations,
                ToolInvocationTraces = InitialToolInvocationTraces.Count == 0
                    ? CreateFinalizerToolInvocationTraces(InitialFinalizerInvocations)
                    : InitialToolInvocationTraces,
                UsageObservations = InitialUsageObservations,
                ContextAssemblyManifest = InitialContextAssemblyManifest
            });
        }

        public Task<AgentRuntimeResponse> RespondToPendingApprovalsAsync(
            AgentDefinition agent,
            ProviderProfile provider,
            ChatSessionRecord session,
            IReadOnlyList<CapabilityCatalogItem> capabilities,
            IReadOnlyList<AgentMemoryRecord> memory,
            bool approved,
            string? runtimeSessionKey,
            Func<ExecutionState, string, string, Task> progressCallback,
            CancellationToken cancellationToken = default,
            bool suppressApprovalRequirements = false,
            AgentStructuredOutputContract? structuredOutput = null,
            AgentRuntimeExecutionOptions? executionOptions = null)
        {
            if (!approved)
            {
                throw new InvalidOperationException("This fake runtime expects the continuation approval to be granted.");
            }

            ContinuationStructuredOutputs.Add(structuredOutput);
            return Task.FromResult(new AgentRuntimeResponse(
                ResponseText: ContinuationResponseText,
                InputTokens: 5,
                OutputTokens: 13,
                ToolCalls: 0,
                RuntimeSessionKey: string.Empty,
                SerializedSessionStateJson: null,
                PendingApprovals: [])
            {
                CachedInputTokens = ContinuationCachedInputTokens,
                FinalizerInvocations = ContinuationFinalizerInvocations,
                ToolInvocationTraces = ContinuationToolInvocationTraces.Count == 0
                    ? CreateFinalizerToolInvocationTraces(ContinuationFinalizerInvocations)
                    : ContinuationToolInvocationTraces
            });
        }

        private static IReadOnlyList<AgentToolInvocationTrace> CreateFinalizerToolInvocationTraces(
            IReadOnlyList<AgentFinalizerInvocation> finalizerInvocations)
        {
            var timestamp = DateTimeOffset.UtcNow;
            return finalizerInvocations
                .Select(invocation => new AgentToolInvocationTrace(
                    invocation.ToolName,
                    ToolInvocationClassification.Read,
                    invocation.Sequence,
                    StartedAtUtc: timestamp,
                    CompletedAtUtc: timestamp,
                    Succeeded: true,
                    FailureMessage: string.Empty))
                .ToList();
        }
    }

    private sealed class UsageReportingRepairService : IAgentOutputRepairService
    {
        public Task<AgentOutputRepairAttemptResult> TryRepairAsync(
            AgentOutputRepairRequest repairRequest,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new AgentOutputRepairAttemptResult
            {
                Succeeded = true,
                RepairedRawOutput = SerializeOutcome(CreateCompletedOutcome("The repair service produced valid machine output.")),
                RemainingErrors = [],
                UsageObservations =
                [
                    CreateUsageObservation(ProviderUsageSourcePhases.StructuredOutputRepair, 3, 1, 2)
                ]
            });
        }
    }
}
