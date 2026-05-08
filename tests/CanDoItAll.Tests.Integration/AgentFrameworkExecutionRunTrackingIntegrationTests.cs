using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
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
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
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
    public async Task ContinueExecutionRunAsync_preserves_structured_output_contract_after_pending_approval()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-structured-output-continuation");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
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
    public async Task ExecuteRunAsync_fails_governed_run_when_structured_output_is_invalid()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-invalid-structured-output");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
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
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
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
    public async Task ExecuteRunAsync_required_finalizer_overrides_assistant_text()
    {
        const string longReason = "The required finalizer result is authoritative and intentionally long so the persisted execution result remains a parseable process-step outcome with all blocker, evidence, and next-action details preserved.";
        var outcome = CreateCompletedOutcome(longReason);
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-required-finalizer");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
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
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
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
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
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
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
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
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
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

    private static ExecutionInvocationContext CreateProcessStepContext(string metadataJson = "{}")
    {
        return new ExecutionInvocationContext(
            SourceKind: "process-step",
            SourceId: "step-001",
            CorrelationId: "corr-001",
            CausationId: "cause-001",
            RequestedBy: "process-automation-dispatch",
            RequestedByKind: "system",
            MetadataJson: metadataJson,
            ProcessRunId: "run-001",
            ProcessStepId: "step-001");
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

        public Task<OllamaModelfileResult> CreateOrUpdateOllamaModelAsync(
            ProviderProfile provider,
            OllamaModelfileRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new OllamaModelfileResult(request.TargetModel, request.BaseModel, request.SystemPrompt, request.ContextLength, string.Empty, "ok"));
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

        public List<Guid> ObservedExecutionRunIds { get; } = [];

        public string InitialResponseText { get; init; } = "Pending approval.";

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

        public Task<OllamaModelfileResult> CreateOrUpdateOllamaModelAsync(
            ProviderProfile provider,
            OllamaModelfileRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new OllamaModelfileResult(request.TargetModel, request.BaseModel, request.SystemPrompt, request.ContextLength, string.Empty, "ok"));
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
            if (!session.LatestExecutionRunId.HasValue)
            {
                throw new InvalidOperationException("Expected the runtime session to carry the execution run id.");
            }

            ObservedExecutionRunIds.Add(session.LatestExecutionRunId.Value);
            return Task.FromResult(new AgentRuntimeResponse(
                ResponseText: InitialResponseText,
                InputTokens: 7,
                OutputTokens: 11,
                ToolCalls: InitialPendingApprovals.Count,
                RuntimeSessionKey: "runtime-session-key",
                SerializedSessionStateJson: """{"state":"pending"}""",
                PendingApprovals: InitialPendingApprovals)
            {
                FinalizerInvocations = InitialFinalizerInvocations,
                ToolInvocationTraces = InitialToolInvocationTraces.Count == 0
                    ? CreateFinalizerToolInvocationTraces(InitialFinalizerInvocations)
                    : InitialToolInvocationTraces
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
}
