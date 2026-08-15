using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace CanDoItAll.Tests.Unit.AgentFramework;

/// <summary>
/// Failing-first characterization: restore eligibility must be decided against
/// the inner adapter payload of a versioned runtime-state envelope, and the
/// compatibility policy must consider the effective history mode. These tests
/// assert the required behavior and are expected to fail until the envelope
/// restore semantics are corrected.
/// </summary>
public sealed class RuntimeStateEnvelopeRestoreDecisionBaselineTests
{
    private const string ToolsetFingerprintValue = "toolset-fingerprint-baseline";
    private const string ContextPolicyFingerprintValue = "context-policy-fingerprint-baseline";

    [Fact]
    public async Task Transient_context_turn_must_not_restore_enveloped_provider_managed_conversation()
    {
        var agent = CreateAgent(AgentChatHistoryMode.ProviderManaged);
        var provider = CreateProvider();
        var model = provider.DefaultModel;
        var envelope = CreateEnvelope(
            provider,
            model,
            payloadJson: """{"conversationId":"provider-conversation"}""",
            historyMode: AgentChatHistoryMode.ProviderManaged);
        var session = CreateSession(agent.Id, envelope.ToJson());
        var runtimeOptions = CreateRuntimeOptions(AgentChatHistoryMode.ProviderManaged) with
        {
            TransientContext = new AgentRuntimeTransientContext("Selected CRM partner: 42")
        };
        var runtimeAgent = new RecordingSessionAgent();

        await MafRuntimeSessionBuilder.RestoreOrCreateSessionAsync(
            runtimeAgent,
            agent,
            provider,
            model,
            session,
            runtimeOptions,
            CancellationToken.None,
            isApprovalContinuation: false);

        // The inner MAF payload carries a provider-managed conversation id, so a
        // transient-context ordinary send must start a fresh session instead of
        // silently reattaching to the provider conversation.
        Assert.Equal(0, runtimeAgent.DeserializeSessionCallCount);
        Assert.Equal(1, runtimeAgent.CreateSessionCallCount);
    }

    [Fact]
    public void History_mode_change_invalidates_enveloped_state()
    {
        var provider = CreateProvider();
        var model = provider.DefaultModel;
        var envelope = CreateEnvelope(
            provider,
            model,
            payloadJson: """{"messages":[]}""",
            historyMode: AgentChatHistoryMode.ProviderManaged);
        var runtimeOptions = CreateRuntimeOptions(AgentChatHistoryMode.FrameworkManaged);

        var decision = MafRuntimeSessionBuilder.EvaluateStoredRuntimeState(
            envelope.ToJson(),
            provider,
            model,
            runtimeOptions,
            out _);

        // State captured under a different effective history mode must not be
        // silently restored as-is.
        Assert.NotEqual(RuntimeStateCompatibilityOutcome.CompatibleRestore, decision.Outcome);
    }

    private static RuntimeStateEnvelope CreateEnvelope(
        ProviderProfile provider,
        string model,
        string payloadJson,
        AgentChatHistoryMode historyMode)
        => new(
            RuntimeStateAdapterIds.Maf,
            RuntimeStateEnvelope.CurrentSchemaVersion,
            adapterPackageVersion: "1.0.0-test",
            provider.Id,
            provider.Transport,
            model,
            ToolsetFingerprintValue,
            ContextPolicyFingerprintValue,
            DateTimeOffset.UtcNow,
            payloadJson)
        {
            HistoryMode = historyMode
        };

    private static AgentRuntimeExecutionOptions CreateRuntimeOptions(AgentChatHistoryMode historyMode)
        => new(
            StructuredOutput: null,
            FinalizerMode: AgentFinalizerMode.Disabled,
            RequireStructuredOutputValidation: false,
            MaxStructuredOutputRepairAttempts: 0)
        {
            ToolsetFingerprint = ToolsetFingerprintValue,
            ModelContextDigest = ContextPolicyFingerprintValue,
            HistoryMode = historyMode
        };

    private static ChatSessionRecord CreateSession(Guid agentId, string serializedStateJson)
        => new(
            Guid.NewGuid(),
            agentId,
            "Envelope restore baseline",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            RuntimeSessionKey: "runtime-session",
            SerializedSessionStateJson: serializedStateJson,
            Messages: [],
            PendingApprovals: []);

    private static AgentDefinition CreateAgent(AgentChatHistoryMode historyMode)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Id: Guid.NewGuid(),
            Name: "Envelope restore test agent",
            RoleTitle: "Assistant",
            Summary: "Envelope restore baseline test agent.",
            Instructions: "Test instructions.",
            Status: AgentLifecycleStatus.Active,
            ProviderProfileId: Guid.NewGuid(),
            Model: "gpt-5.4-mini",
            Workload: AgentWorkloadKind.General,
            ChatHistoryMode: historyMode,
            Temperature: 0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            Permissions: AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }

    private static ProviderProfile CreateProvider()
        => new(
            Guid.NewGuid(),
            "OpenAI",
            ProviderKind.OpenAi,
            "https://api.openai.com/v1",
            "OPENAI_API_KEY",
            "gpt-5.4-mini",
            ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: true,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: string.Empty,
            LastCheckedAtUtc: null,
            SuggestedModels: []);

    private sealed class RecordingSessionAgent : AIAgent
    {
        public int CreateSessionCallCount { get; private set; }

        public int DeserializeSessionCallCount { get; private set; }

        protected override ValueTask<AgentSession> CreateSessionCoreAsync(
            CancellationToken cancellationToken = default)
        {
            CreateSessionCallCount++;
            return ValueTask.FromResult<AgentSession>(new RecordingSession());
        }

        protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
            AgentSession session,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
            JsonElement serializedState,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
        {
            DeserializeSessionCallCount++;
            return ValueTask.FromResult<AgentSession>(new RecordingSession());
        }

        protected override Task<AgentResponse> RunCoreAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        protected override IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        private sealed class RecordingSession : AgentSession;
    }
}
