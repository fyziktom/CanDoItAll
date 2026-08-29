using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit.AgentFramework;

/// <summary>
/// SB10: direct tests for the native MAF port adapters, the pure response mapper, the
/// composed-tool-name validation, and the thin-facade shape of MafAgentRuntime.
/// </summary>
public sealed class MafRuntimePortAdapterTests
{
    private static readonly Func<ExecutionState, string, string, Task> ProgressCallback =
        static (_, _, _) => Task.CompletedTask;

    [Fact]
    public void ResponseMapper_terminal_response_maps_usage_tool_calls_session_and_snapshots()
    {
        var response = new[]
        {
            new AgentResponseUpdate(ChatRole.Assistant, "terminal answer")
        }.ToAgentResponse();
        response.Usage = new UsageDetails
        {
            InputTokenCount = 11,
            OutputTokenCount = 7,
            CachedInputTokenCount = 3
        };
        var activityResponse = new[]
        {
            new AgentResponseUpdate(
                ChatRole.Assistant,
                [
                    new FunctionCallContent("call-1", "workspace_read_file"),
                    new FunctionCallContent("call-2", "workspace_write_file")
                ])
        }.ToAgentResponse();
        var pendingApprovals = new List<PendingToolApprovalRecord>
        {
            new(
                ApprovalId: "approval-1",
                CallId: "call-3",
                ToolName: "workspace_pwsh_run_script",
                ToolKind: "function",
                Details: string.Empty,
                ArgumentsJson: "{}")
        };
        var finalizerInvocations = new List<AgentFinalizerInvocation>
        {
            new("submit_process_step_outcome", "{}", Sequence: 1)
        };
        var timestamp = DateTimeOffset.UtcNow;
        var toolInvocationTraces = new List<AgentToolInvocationTrace>
        {
            new(
                "workspace_read_file",
                ToolInvocationClassification.Read,
                Sequence: 1,
                StartedAtUtc: timestamp,
                CompletedAtUtc: timestamp,
                Succeeded: true,
                FailureMessage: string.Empty)
        };
        var usageObservations = new List<ProviderUsageObservation>
        {
            CreateUsageObservation()
        };

        var mapped = MafRuntimeResponseMapper.CreateTerminalRuntimeResponse(
            "resolved response text",
            response,
            activityResponse,
            "runtime-session-key",
            "{\"state\":true}",
            pendingApprovals,
            finalizerInvocations,
            toolInvocationTraces,
            usageObservations);

        Assert.Equal("resolved response text", mapped.ResponseText);
        Assert.Equal(11, mapped.InputTokens);
        Assert.Equal(7, mapped.OutputTokens);
        Assert.Equal(3, mapped.CachedInputTokens);
        Assert.Equal(2, mapped.ToolCalls);
        Assert.Equal("runtime-session-key", mapped.RuntimeSessionKey);
        Assert.Equal("{\"state\":true}", mapped.SerializedSessionStateJson);
        Assert.Same(pendingApprovals, mapped.PendingApprovals);
        Assert.Same(finalizerInvocations, mapped.FinalizerInvocations);
        Assert.Same(toolInvocationTraces, mapped.ToolInvocationTraces);
        Assert.Same(usageObservations, mapped.UsageObservations);
    }

    [Fact]
    public void ResponseMapper_attaches_context_diagnostics_without_touching_other_fields()
    {
        var agent = CreateAgent();
        var provider = CreateProvider();
        var manifest = MafContextManifestBuilder.Create(
            agent,
            provider,
            "contract-model",
            MafRuntimeExecutionOptionsResolver.CreateDisabled(null),
            tools: [],
            contextSources: [],
            frameworkToolNames: [],
            contextProviderCount: 0,
            runtimeToolProviderCount: 0,
            inputMessages: []);
        var contributionTraces = new List<AgentContextContributionTrace>();
        var evidence = MafProviderRequestCompatibilityEvidenceFactory.Create(
            provider,
            "contract-model",
            "contract-model",
            tools: [],
            requestedEffort: null);
        var response = new AgentRuntimeResponse(
            "unchanged",
            InputTokens: 1,
            OutputTokens: 2,
            ToolCalls: 3,
            RuntimeSessionKey: "key",
            SerializedSessionStateJson: null,
            PendingApprovals: []);

        var mapped = MafRuntimeResponseMapper.AttachContextDiagnostics(
            response,
            manifest,
            contributionTraces,
            evidence);

        Assert.Same(manifest, mapped.ContextAssemblyManifest);
        Assert.Same(contributionTraces, mapped.ContextContributionTraces);
        Assert.Same(evidence, mapped.EntryAgentRequestCompatibilityEvidence);
        Assert.Equal("unchanged", mapped.ResponseText);
        Assert.Equal(1, mapped.InputTokens);
        Assert.Equal(2, mapped.OutputTokens);
        Assert.Equal(3, mapped.ToolCalls);
    }

    [Fact]
    public async Task Diagnostics_adapter_delegates_health_and_probe_to_the_gateway()
    {
        var gateway = new CapturingGateway();
        var adapter = new MafProviderDiagnosticsAdapter(gateway);
        var provider = CreateProvider();
        using var cancellation = new CancellationTokenSource();

        var health = await adapter.TestHealthAsync(provider, cancellation.Token);
        Assert.True(health.Success);
        Assert.Equal(1, gateway.HealthCalls);
        Assert.Same(provider, gateway.LastProvider);

        var probe = await adapter.RunProbeAsync(
            provider,
            new ProviderTestChatRequest(
                Model: string.Empty,
                SystemPrompt: "system",
                Messages: [],
                Prompt: "ping"),
            cancellation.Token);
        Assert.Equal(1, gateway.ChatCalls);
        // Current model resolution rules: an empty probe model falls back to the provider default.
        Assert.Equal("contract-model", gateway.LastChatModel);
        Assert.Equal("contract-model", probe.Model);
    }

    [Fact]
    public async Task Administration_adapter_delegates_model_maintenance_to_the_gateway()
    {
        var gateway = new CapturingGateway();
        var adapter = new MafProviderModelAdministrationAdapter(gateway);
        var provider = CreateProvider();
        var request = new ProviderModelMaintenanceEditorRequest(
            "base-model",
            "target-model",
            "system prompt",
            ContextLength: 4096);

        var result = await adapter.CreateOrUpdateModelAsync(provider, request);

        Assert.Equal(1, gateway.MaintenanceCalls);
        Assert.Same(provider, gateway.LastProvider);
        Assert.Same(request, gateway.LastMaintenanceRequest);
        Assert.Equal("target-model", result.ModelName);
    }

    [Fact]
    public async Task Continuation_adapter_passes_mixed_decisions_through_unmangled()
    {
        // Pre-SB15 the continuation adapter collapsed every request to a single agreed boolean
        // and rejected a mixed set outright before any provider work started. SB15 removes that
        // collapse: mixed decisions must reach the approval-continuation driver unmangled. This
        // fixture has no live provider credentials, so the request still fails — but it must fail
        // for that unrelated provider-configuration reason, never for the old "mixed decisions
        // unsupported" reason, proving the legacy short-circuit is gone.
        using var services = MafRuntimeTestServices.CreateProviderRuntimeServiceCollection().BuildServiceProvider();
        var runtime = new MafAgentRuntime(Path.GetTempPath(), services);
        var agent = CreateAgent();
        var request = new AgentRuntimeContinuationRequest(
            agent,
            CreateProvider(),
            CreateSession(agent.Id),
            Capabilities: [],
            Memory: [],
            Decisions:
            [
                new AgentRuntimeApprovalDecision("proposal-1", true),
                new AgentRuntimeApprovalDecision("proposal-2", false)
            ],
            RuntimeSessionKey: null,
            ProgressCallback);

        var exception = await Record.ExceptionAsync(
            () => runtime.ContinuationPort.ContinueAsync(request, CancellationToken.None));

        Assert.NotNull(exception);
        Assert.DoesNotContain(
            "Mixed per-proposal approval decisions are not supported",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_exposes_one_stable_native_adapter_set()
    {
        using var services = MafRuntimeTestServices.CreateProviderRuntimeServiceCollection().BuildServiceProvider();
        var runtime = new MafAgentRuntime(Path.GetTempPath(), services);

        Assert.Same(runtime.ExecutionPort, runtime.ExecutionPort);
        Assert.Same(runtime.ContinuationPort, runtime.ContinuationPort);
        Assert.Same(runtime.DiagnosticsPort, runtime.DiagnosticsPort);
        Assert.Same(runtime.ModelAdministrationPort, runtime.ModelAdministrationPort);
        Assert.IsType<HistoryAgentRuntime>(runtime.ExecutionPort);
        Assert.Same(runtime.ExecutionPort, runtime.ContinuationPort);
        Assert.IsType<MafProviderDiagnosticsAdapter>(runtime.DiagnosticsPort);
        Assert.IsType<MafProviderModelAdministrationAdapter>(runtime.ModelAdministrationPort);
    }

    [Fact]
    public void MafAgentRuntime_source_stays_a_thin_delegating_facade()
    {
        var runtimeSourcePath = Path.Combine(
            FindRepoRoot(),
            "src", "MAF", "Common", "CanDoItAll.AgentFramework.Maf", "Runtime", "MafAgentRuntime.cs");
        Assert.True(File.Exists(runtimeSourcePath), $"Missing source file: {runtimeSourcePath}");

        var source = File.ReadAllText(runtimeSourcePath);
        var lineCount = File.ReadAllLines(runtimeSourcePath).Length;

        Assert.True(
            lineCount < 400,
            $"MafAgentRuntime.cs must stay a thin delegating facade but has {lineCount} lines.");

        // Robust markers of the moved algorithms: streaming pump, streaming iteration, session
        // restore/run-option building, session persistence, and finalizer repair/recovery.
        string[] movedAlgorithmMarkers =
        [
            "MafProviderUpdatePump",
            "await foreach",
            "MafRuntimeSessionBuilder",
            "TrySerializePersistableRuntimeSession",
            "RequiredFinalizerCapturedException",
            "Finalizer repair"
        ];
        foreach (var marker in movedAlgorithmMarkers)
        {
            Assert.False(
                source.Contains(marker, StringComparison.Ordinal),
                $"MafAgentRuntime.cs must not contain moved-algorithm marker '{marker}'.");
        }
    }

    [Fact]
    public void Composed_tool_name_validation_collapses_repeated_instances_and_rejects_collisions()
    {
        var sharedTool = AIFunctionFactory.Create(() => "shared", "workspace_shared_tool");
        var repeated = new List<AITool> { sharedTool, sharedTool };
        RuntimeCapabilityComposer.ValidateComposedToolNames(repeated);
        Assert.Single(repeated);
        Assert.Same(sharedTool, repeated[0]);

        var colliding = new List<AITool>
        {
            AIFunctionFactory.Create(() => "one", "workspace_colliding_tool"),
            AIFunctionFactory.Create(() => "two", "workspace_colliding_tool"),
            AIFunctionFactory.Create(() => "other", "workspace_other_tool")
        };
        var exception = Assert.Throws<InvalidOperationException>(
            () => RuntimeCapabilityComposer.ValidateComposedToolNames(colliding));
        Assert.Contains("workspace_colliding_tool", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("workspace_other_tool", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, HistoryOutcome.Succeeded)]
    [InlineData(true, HistoryOutcome.Succeeded)]
    [InlineData(false, HistoryOutcome.Cancelled)]
    [InlineData(true, HistoryOutcome.Cancelled)]
    [InlineData(false, HistoryOutcome.Failed)]
    [InlineData(true, HistoryOutcome.Failed)]
    public async Task History_runtime_handoff_preserves_attempts_and_failure_classification(bool continuation, HistoryOutcome outcome) {
        var provider = CreateProvider();
        var agent = CreateAgent();
        var recorder = new RecordingProviderHistory();
        var context = HistoryInvocationContext.Create(HistoryWorkload.Agent,
            owner: new(HistorySourceKind.AgentConversation, new("run"), new("invocation")));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var native = new HistoryTestRuntime(async options => {
            Assert.Same(context, options.History);
            var start = await recorder.BeginAsync(new(new(new(provider.Id), provider.Name, provider.Kind.ToString(),
                new("model"), new("model")), HistoryOperation.CompleteChat, options.History), default);
            options.History.Attempts.Complete(start, new(outcome, start.StartedAtUtc.AddSeconds(1),
                new(HistoryUsageState.Complete, 7, 3), new(HistoryPriceState.Unpriced)));
            if (outcome == HistoryOutcome.Cancelled) {
                throw new OperationCanceledException(cancelled.Token);
            }
            if (outcome == HistoryOutcome.Failed) {
                throw new AgentRuntimeUsageException("provider failed", new InvalidOperationException("transport"),
                    [CreateUsageObservation(), CreateUsageObservation()]);
            }
            return new AgentRuntimeResponse("private response", 100, 200, 0, "", null, []) {
                UsageObservations = [CreateUsageObservation(), CreateUsageObservation()]
            };
        });
        var runtime = new HistoryAgentRuntime(native, native);
        var options = new AgentRuntimeExecutionOptions(null, AgentFinalizerMode.Disabled, true, 0) { History = context };
        Task<AgentRuntimeResponse> Invoke() => continuation
            ? runtime.ContinueAsync(new(agent, provider, CreateSession(agent.Id), [], [],
                [new("approval", true)], null, ProgressCallback, ExecutionOptions: options))
            : runtime.ExecuteAsync(new(agent, provider, CreateSession(agent.Id), [], [], "private prompt",
                null, ProgressCallback, ExecutionOptions: options));
        if (outcome == HistoryOutcome.Succeeded) {
            var response = await Invoke();
            Assert.Single(response.UsageObservations, item => item.HistoryEvidence!.IsPrimary);
            Assert.Equal(1, response.UsageObservations.Sum(item => item.HistoryEvidence!.Attempts.Count));
            Assert.Equal("private response", response.ResponseText);
            Assert.DoesNotContain("private response", System.Text.Json.JsonSerializer.Serialize(response.HistoryEvidence), StringComparison.Ordinal);
        } else if (outcome == HistoryOutcome.Cancelled) {
            var exception = await Assert.ThrowsAsync<AgentHistoryCancellationException>(Invoke);
            Assert.Equal(cancelled.Token, exception.CancellationToken);
            Assert.Equal(outcome, Assert.Single(exception.HistoryEvidence.Attempts).Outcome);
        } else {
            var exception = await Assert.ThrowsAsync<AgentRuntimeUsageException>(Invoke);
            Assert.Single(exception.UsageObservations, item => item.HistoryEvidence!.IsPrimary);
            Assert.Equal(outcome, Assert.Single(exception.HistoryEvidence!.Attempts).Outcome);
            Assert.IsType<AgentRuntimeUsageException>(exception.InnerException);
        }
        Assert.Equal(continuation ? 0 : 1, native.ExecutionCalls);
        Assert.Equal(continuation ? 1 : 0, native.ContinuationCalls);
    }

    private sealed class HistoryTestRuntime(Func<AgentRuntimeExecutionOptions, Task<AgentRuntimeResponse>> invoke)
        : IAgentExecutionRuntime, IAgentContinuationRuntime {
        public int ExecutionCalls { get; private set; }
        public int ContinuationCalls { get; private set; }

        public Task<AgentRuntimeResponse> ExecuteAsync(AgentRuntimeExecutionRequest request, CancellationToken cancellationToken = default) {
            ExecutionCalls++;
            return invoke(request.ExecutionOptions!);
        }

        public Task<AgentRuntimeResponse> ContinueAsync(AgentRuntimeContinuationRequest request, CancellationToken cancellationToken = default) {
            ContinuationCalls++;
            return invoke(request.ExecutionOptions!);
        }
    }

    private static ProviderUsageObservation CreateUsageObservation()
    {
        return new ProviderUsageObservation(
            Id: Guid.NewGuid(),
            CreatedAtUtc: DateTimeOffset.UtcNow,
            ProviderName: "Port Adapter Provider",
            ProviderKind: ProviderKind.OpenAi,
            Model: "contract-model",
            TransportKind: ProviderTransportKind.Responses,
            SourcePhase: "agent-runtime",
            UsageStatus: ProviderUsageObservationStatus.Observed,
            InputTokens: 11,
            CachedInputTokens: 3,
            OutputTokens: 7,
            ReasoningTokens: 0,
            TotalTokens: 18,
            ToolCallCount: 2);
    }

    private static AgentDefinition CreateAgent()
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Guid.NewGuid(),
            "Port Adapter Agent",
            "Adapter Reviewer",
            "Validates native runtime port adapters.",
            "Delegate faithfully.",
            AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            Model: "contract-model",
            AgentWorkloadKind.General,
            AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.1,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }

    private static ProviderProfile CreateProvider()
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            "Port Adapter Provider",
            ProviderKind.OpenAi,
            BaseUrl: "https://provider.example",
            ApiKeyEnvironmentVariable: "PORT_ADAPTER_KEY",
            DefaultModel: "contract-model",
            ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: string.Empty,
            LastCheckedAtUtc: null,
            SuggestedModels: []);
    }

    private static ChatSessionRecord CreateSession(Guid agentId)
    {
        var now = DateTimeOffset.UtcNow;
        return new ChatSessionRecord(
            Guid.NewGuid(),
            agentId,
            "Port adapter session",
            now,
            now,
            Messages: []);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }

    private sealed class CapturingGateway : IMafProviderRuntimeGateway
    {
        public int HealthCalls { get; private set; }

        public int ChatCalls { get; private set; }

        public int MaintenanceCalls { get; private set; }

        public ProviderProfile? LastProvider { get; private set; }

        public string? LastChatModel { get; private set; }

        public ProviderModelMaintenanceEditorRequest? LastMaintenanceRequest { get; private set; }

        public Task<ProviderHealthResult> TestProviderAsync(
            ProviderProfile provider,
            CancellationToken cancellationToken = default)
        {
            HealthCalls++;
            LastProvider = provider;
            return Task.FromResult(new ProviderHealthResult(true, "ok", [provider.DefaultModel]));
        }

        public Task<ProviderTestChatResult> RunProviderTestChatAsync(
            ProviderProfile provider,
            ProviderTestChatRequest request,
            string model,
            CancellationToken cancellationToken = default)
        {
            ChatCalls++;
            LastProvider = provider;
            LastChatModel = model;
            return Task.FromResult(new ProviderTestChatResult(model, "chat", 1, 2));
        }

        public Task<ProviderTestChatResult> RunProviderImageChatAsync(
            ProviderProfile provider,
            ProviderTestChatRequest request,
            string model,
            IReadOnlyList<ProviderChatAttachment> attachments,
            string modelParameterConfigurationJson = "",
            CancellationToken cancellationToken = default)
        {
            ChatCalls++;
            LastProvider = provider;
            LastChatModel = model;
            return Task.FromResult(new ProviderTestChatResult(model, "image chat", 1, 2));
        }

        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
            ProviderProfile provider,
            ProviderModelMaintenanceEditorRequest request,
            CancellationToken cancellationToken = default)
        {
            MaintenanceCalls++;
            LastProvider = provider;
            LastMaintenanceRequest = request;
            return Task.FromResult(new ProviderModelMaintenanceEditorResult(
                request.TargetModel,
                request.BaseModel,
                request.SystemPrompt,
                request.ContextLength,
                "definition",
                "ok"));
        }
    }
}
