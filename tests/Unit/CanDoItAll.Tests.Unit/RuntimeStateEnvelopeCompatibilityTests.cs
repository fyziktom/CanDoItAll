using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;

namespace CanDoItAll.Tests.Unit;

/// <summary>
/// SB15: direct tests for the SDK-free runtime-state envelope round-trip, the native MAF
/// adapter's serialize/deserialize, the compatibility policy's explicit restore/migrate/
/// replay/fail decisions, and the toolset fingerprint calculator. Instantiates every extracted
/// owner directly (no delegation back through the streaming turn executor).
/// </summary>
public sealed class RuntimeStateEnvelopeCompatibilityTests
{
    private static readonly Guid ProviderProfileId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string Model = "gpt-5.4-mini";
    private const string ToolsetFingerprint = "toolset-fingerprint-abc";
    private const string ContextPolicyFingerprint = "context-policy-fingerprint-xyz";

    [Fact]
    public void RuntimeStateEnvelope_round_trips_through_ToJson_and_TryParse()
    {
        var envelope = new RuntimeStateEnvelope(
            RuntimeStateAdapterIds.Maf,
            RuntimeStateEnvelope.CurrentSchemaVersion,
            "1.2.3",
            ProviderProfileId,
            ProviderTransportKind.Responses,
            Model,
            ToolsetFingerprint,
            ContextPolicyFingerprint,
            new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero),
            """{"conversationId":"abc-123"}""")
        {
            HistoryMode = AgentChatHistoryMode.FrameworkManaged
        };

        var json = envelope.ToJson();
        Assert.True(RuntimeStateEnvelope.TryParse(json, out var restored));

        Assert.Equal(envelope.AdapterId, restored!.AdapterId);
        Assert.Equal(envelope.SchemaVersion, restored.SchemaVersion);
        Assert.Equal(envelope.AdapterPackageVersion, restored.AdapterPackageVersion);
        Assert.Equal(envelope.ProviderProfileId, restored.ProviderProfileId);
        Assert.Equal(envelope.ProviderTransport, restored.ProviderTransport);
        Assert.Equal(envelope.Model, restored.Model);
        Assert.Equal(envelope.ToolsetFingerprint, restored.ToolsetFingerprint);
        Assert.Equal(envelope.ContextPolicyFingerprint, restored.ContextPolicyFingerprint);
        Assert.Equal(envelope.CreatedAtUtc, restored.CreatedAtUtc);
        Assert.Equal(envelope.PayloadJson, restored.PayloadJson);
        Assert.Equal(envelope.HistoryMode, restored.HistoryMode);
    }

    [Fact]
    public void RuntimeStateEnvelope_TryParse_returns_false_for_legacy_shaped_json()
    {
        // A pre-SB15 raw MAF session payload has none of the envelope's required fields.
        const string legacyRawSessionJson = """{"conversationId":"legacy-conversation","messages":[]}""";

        Assert.False(RuntimeStateEnvelope.TryParse(legacyRawSessionJson, out var envelope));
        Assert.Null(envelope);
    }

    [Fact]
    public void RuntimeStateEnvelope_TryParse_returns_false_for_malformed_json()
    {
        Assert.False(RuntimeStateEnvelope.TryParse("{not-json", out var envelope));
        Assert.Null(envelope);
        Assert.False(RuntimeStateEnvelope.TryParse(string.Empty, out envelope));
        Assert.False(RuntimeStateEnvelope.TryParse(null, out envelope));
    }

    [Fact]
    public void MafRuntimeStateAdapter_CreateEnvelope_stamps_adapter_identity_and_schema()
    {
        var adapter = new MafRuntimeStateAdapter();
        var request = new AgentRuntimeStateCaptureRequest(
            ProviderProfileId,
            ProviderTransportKind.ChatCompletions,
            Model,
            ToolsetFingerprint,
            ContextPolicyFingerprint,
            """{"conversationId":"abc"}""",
            DateTimeOffset.UtcNow,
            AgentChatHistoryMode.ProviderManaged);

        var envelope = adapter.CreateEnvelope(request);

        Assert.Equal(RuntimeStateAdapterIds.Maf, envelope.AdapterId);
        Assert.Equal(adapter.AdapterId, envelope.AdapterId);
        Assert.Equal(RuntimeStateEnvelope.CurrentSchemaVersion, envelope.SchemaVersion);
        Assert.False(string.IsNullOrWhiteSpace(envelope.AdapterPackageVersion));
        Assert.Equal(request.PayloadJson, envelope.PayloadJson);
        Assert.Equal(AgentChatHistoryMode.ProviderManaged, envelope.HistoryMode);
    }

    [Fact]
    public void MafRuntimeStateAdapter_TryRestore_succeeds_for_its_own_compatible_envelope()
    {
        var adapter = new MafRuntimeStateAdapter();
        var envelope = adapter.CreateEnvelope(new AgentRuntimeStateCaptureRequest(
            ProviderProfileId,
            ProviderTransportKind.Responses,
            Model,
            ToolsetFingerprint,
            ContextPolicyFingerprint,
            """{"conversationId":"abc"}""",
            DateTimeOffset.UtcNow));

        var restoreResult = adapter.TryRestore(envelope);

        Assert.True(restoreResult.Succeeded);
        Assert.Equal(envelope.PayloadJson, restoreResult.PayloadJson);
        Assert.Equal(string.Empty, restoreResult.FailureReason);
    }

    [Fact]
    public void MafRuntimeStateAdapter_TryRestore_fails_closed_for_a_foreign_adapter_id()
    {
        var adapter = new MafRuntimeStateAdapter();
        var foreignEnvelope = new RuntimeStateEnvelope(
            "some-other-adapter",
            RuntimeStateEnvelope.CurrentSchemaVersion,
            "1.0.0",
            ProviderProfileId,
            ProviderTransportKind.Responses,
            Model,
            ToolsetFingerprint,
            ContextPolicyFingerprint,
            DateTimeOffset.UtcNow,
            """{"conversationId":"abc"}""");

        var restoreResult = adapter.TryRestore(foreignEnvelope);

        Assert.False(restoreResult.Succeeded);
        Assert.Equal(string.Empty, restoreResult.PayloadJson);
        Assert.Contains("not readable", restoreResult.FailureReason, StringComparison.Ordinal);
    }

    [Fact]
    public void MafRuntimeStateAdapter_TryRestore_fails_closed_for_a_schema_version_above_the_readable_range()
    {
        var adapter = new MafRuntimeStateAdapter();
        var futureEnvelope = new RuntimeStateEnvelope(
            RuntimeStateAdapterIds.Maf,
            RuntimeStateEnvelope.CurrentSchemaVersion + 1,
            "9.9.9",
            ProviderProfileId,
            ProviderTransportKind.Responses,
            Model,
            ToolsetFingerprint,
            ContextPolicyFingerprint,
            DateTimeOffset.UtcNow,
            """{"conversationId":"abc"}""");

        var restoreResult = adapter.TryRestore(futureEnvelope);

        Assert.False(restoreResult.Succeeded);
        Assert.Contains("not readable", restoreResult.FailureReason, StringComparison.Ordinal);
    }

    [Fact]
    public void CompatibilityPolicy_returns_SafeCanonicalReplay_when_no_payload_exists_at_all()
    {
        var policy = new MafRuntimeStateCompatibilityPolicy();
        var decision = policy.Evaluate(CreateCurrentRequest(envelope: null, isLegacy: false));

        Assert.Equal(RuntimeStateCompatibilityOutcome.SafeCanonicalReplay, decision.Outcome);
        Assert.Null(decision.MigrationId);
    }

    [Fact]
    public void CompatibilityPolicy_returns_RegisteredMigration_for_parseable_legacy_state()
    {
        var policy = new MafRuntimeStateCompatibilityPolicy();
        var decision = policy.Evaluate(CreateCurrentRequest(envelope: null, isLegacy: true));

        Assert.Equal(RuntimeStateCompatibilityOutcome.RegisteredMigration, decision.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(decision.MigrationId));
    }

    [Fact]
    public void CompatibilityPolicy_is_Incompatible_for_stored_state_that_is_not_valid_json()
    {
        // The explicit fail-closed rule: a stored-but-unparseable payload must never be treated
        // as "nothing was stored" (which would silently fall back to SafeCanonicalReplay).
        var policy = new MafRuntimeStateCompatibilityPolicy();
        var request = CreateCurrentRequest(envelope: null, isLegacy: false) with
        {
            HasUnparseableStoredState = true
        };

        var decision = policy.Evaluate(request);

        Assert.Equal(RuntimeStateCompatibilityOutcome.Incompatible, decision.Outcome);
        Assert.Contains("not valid JSON", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void EvaluateStoredRuntimeState_classifies_unparseable_non_empty_text_as_Incompatible()
    {
        // End-to-end through the actual MafRuntimeSessionBuilder caller (not just the policy
        // directly): proves the caller correctly threads HasUnparseableStoredState instead of
        // conflating "corrupt payload" with "no payload" (SafeCanonicalReplay).
        var provider = CreateProviderProfile();
        var runtimeOptions = new AgentRuntimeExecutionOptions(
            StructuredOutput: null,
            FinalizerMode: AgentFinalizerMode.Disabled,
            RequireStructuredOutputValidation: true,
            MaxStructuredOutputRepairAttempts: 0)
        {
            ToolsetFingerprint = ToolsetFingerprint,
            ModelContextDigest = ContextPolicyFingerprint
        };

        var decision = MafRuntimeSessionBuilder.EvaluateStoredRuntimeState(
            "{not-valid-json",
            provider,
            Model,
            runtimeOptions,
            out var parsedEnvelope);

        Assert.Equal(RuntimeStateCompatibilityOutcome.Incompatible, decision.Outcome);
        Assert.Null(parsedEnvelope);
    }

    [Fact]
    public void EvaluateStoredRuntimeState_classifies_legacy_raw_session_json_as_RegisteredMigration()
    {
        var provider = CreateProviderProfile();
        var runtimeOptions = new AgentRuntimeExecutionOptions(
            StructuredOutput: null,
            FinalizerMode: AgentFinalizerMode.Disabled,
            RequireStructuredOutputValidation: true,
            MaxStructuredOutputRepairAttempts: 0)
        {
            ToolsetFingerprint = ToolsetFingerprint,
            ModelContextDigest = ContextPolicyFingerprint
        };

        var decision = MafRuntimeSessionBuilder.EvaluateStoredRuntimeState(
            """{"conversationId":"legacy-conversation","messages":[]}""",
            provider,
            Model,
            runtimeOptions,
            out var parsedEnvelope);

        Assert.Equal(RuntimeStateCompatibilityOutcome.RegisteredMigration, decision.Outcome);
        Assert.Null(parsedEnvelope);
    }

    private static ProviderProfile CreateProviderProfile()
    {
        return new ProviderProfile(
            ProviderProfileId,
            "Runtime State Test Provider",
            ProviderKind.OpenAi,
            "https://api.openai.com/v1",
            "OPENAI_API_KEY",
            Model,
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
    }

    [Fact]
    public void CompatibilityPolicy_returns_CompatibleRestore_when_every_dimension_matches()
    {
        var policy = new MafRuntimeStateCompatibilityPolicy();
        var envelope = CreateEnvelope();

        var decision = policy.Evaluate(CreateCurrentRequest(envelope));

        Assert.Equal(RuntimeStateCompatibilityOutcome.CompatibleRestore, decision.Outcome);
    }

    [Fact]
    public void CompatibilityPolicy_is_Incompatible_for_a_foreign_adapter_or_out_of_range_schema()
    {
        var policy = new MafRuntimeStateCompatibilityPolicy();
        var envelope = new RuntimeStateEnvelope(
            "some-other-adapter",
            RuntimeStateEnvelope.CurrentSchemaVersion,
            "1.0.0",
            ProviderProfileId,
            ProviderTransportKind.Responses,
            Model,
            ToolsetFingerprint,
            ContextPolicyFingerprint,
            DateTimeOffset.UtcNow,
            """{"conversationId":"abc"}""");

        var decision = policy.Evaluate(CreateCurrentRequest(envelope));

        Assert.Equal(RuntimeStateCompatibilityOutcome.Incompatible, decision.Outcome);
        Assert.DoesNotContain("payload", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CompatibilityPolicy_is_Incompatible_when_the_provider_profile_changed()
    {
        var policy = new MafRuntimeStateCompatibilityPolicy();
        var envelope = CreateEnvelope();
        var request = CreateCurrentRequest(envelope) with { CurrentProviderProfileId = Guid.NewGuid() };

        var decision = policy.Evaluate(request);

        Assert.Equal(RuntimeStateCompatibilityOutcome.Incompatible, decision.Outcome);
        Assert.Contains("Provider identity", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void CompatibilityPolicy_is_Incompatible_when_the_provider_transport_changed()
    {
        var policy = new MafRuntimeStateCompatibilityPolicy();
        var envelope = CreateEnvelope();
        var request = CreateCurrentRequest(envelope) with { CurrentProviderTransport = ProviderTransportKind.ChatCompletions };

        var decision = policy.Evaluate(request);

        Assert.Equal(RuntimeStateCompatibilityOutcome.Incompatible, decision.Outcome);
        Assert.Contains("Provider identity", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void CompatibilityPolicy_is_Incompatible_when_the_model_changed()
    {
        var policy = new MafRuntimeStateCompatibilityPolicy();
        var envelope = CreateEnvelope();
        var request = CreateCurrentRequest(envelope) with { CurrentModel = "a-different-model" };

        var decision = policy.Evaluate(request);

        Assert.Equal(RuntimeStateCompatibilityOutcome.Incompatible, decision.Outcome);
        Assert.Contains("Model changed", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void CompatibilityPolicy_is_Incompatible_when_the_toolset_fingerprint_changed()
    {
        var policy = new MafRuntimeStateCompatibilityPolicy();
        var envelope = CreateEnvelope();
        var request = CreateCurrentRequest(envelope) with { CurrentToolsetFingerprint = "different-toolset" };

        var decision = policy.Evaluate(request);

        Assert.Equal(RuntimeStateCompatibilityOutcome.Incompatible, decision.Outcome);
        Assert.Contains("toolset", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CompatibilityPolicy_is_Incompatible_when_the_context_policy_fingerprint_changed()
    {
        var policy = new MafRuntimeStateCompatibilityPolicy();
        var envelope = CreateEnvelope();
        var request = CreateCurrentRequest(envelope) with { CurrentContextPolicyFingerprint = "different-context-policy" };

        var decision = policy.Evaluate(request);

        Assert.Equal(RuntimeStateCompatibilityOutcome.Incompatible, decision.Outcome);
        Assert.Contains("context-policy", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CompatibilityPolicy_decision_reasons_never_contain_the_payload()
    {
        var policy = new MafRuntimeStateCompatibilityPolicy();
        const string secretPayload = "super-secret-transcript-content";
        var envelope = new RuntimeStateEnvelope(
            RuntimeStateAdapterIds.Maf,
            RuntimeStateEnvelope.CurrentSchemaVersion,
            "1.0.0",
            ProviderProfileId,
            ProviderTransportKind.Responses,
            Model,
            ToolsetFingerprint,
            ContextPolicyFingerprint,
            DateTimeOffset.UtcNow,
            secretPayload);
        var request = CreateCurrentRequest(envelope) with { CurrentModel = "different-model" };

        var decision = policy.Evaluate(request);

        Assert.DoesNotContain(secretPayload, decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void MafToolsetFingerprint_is_order_independent()
    {
        var fingerprintA = MafToolsetFingerprint.Compute(["workspace_write_file", "workspace_read_file"]);
        var fingerprintB = MafToolsetFingerprint.Compute(["workspace_read_file", "workspace_write_file"]);

        Assert.Equal(fingerprintA, fingerprintB);
    }

    [Fact]
    public void MafToolsetFingerprint_differs_for_different_tool_sets_and_is_never_empty()
    {
        var fingerprintA = MafToolsetFingerprint.Compute(["tool_a", "tool_b"]);
        var fingerprintB = MafToolsetFingerprint.Compute(["tool_a", "tool_c"]);
        var emptyFingerprint = MafToolsetFingerprint.Compute([]);

        Assert.NotEqual(fingerprintA, fingerprintB);
        Assert.False(string.IsNullOrWhiteSpace(emptyFingerprint));
        Assert.False(string.IsNullOrWhiteSpace(fingerprintA));
    }

    [Fact]
    public void MafToolsetFingerprint_deduplicates_and_ignores_blank_names()
    {
        var withDuplicates = MafToolsetFingerprint.Compute(["tool_a", "tool_a", "", null, "  "]);
        var withoutDuplicates = MafToolsetFingerprint.Compute(["tool_a"]);

        Assert.Equal(withoutDuplicates, withDuplicates);
    }

    private static RuntimeStateEnvelope CreateEnvelope()
    {
        return new RuntimeStateEnvelope(
            RuntimeStateAdapterIds.Maf,
            RuntimeStateEnvelope.CurrentSchemaVersion,
            "1.0.0",
            ProviderProfileId,
            ProviderTransportKind.Responses,
            Model,
            ToolsetFingerprint,
            ContextPolicyFingerprint,
            DateTimeOffset.UtcNow,
            """{"conversationId":"abc"}""")
        {
            HistoryMode = AgentChatHistoryMode.FrameworkManaged
        };
    }

    private static RuntimeStateCompatibilityRequest CreateCurrentRequest(
        RuntimeStateEnvelope? envelope,
        bool isLegacy = false)
    {
        return new RuntimeStateCompatibilityRequest(
            envelope,
            isLegacy,
            HasUnparseableStoredState: false,
            ProviderProfileId,
            ProviderTransportKind.Responses,
            Model,
            ToolsetFingerprint,
            ContextPolicyFingerprint,
            AgentChatHistoryMode.FrameworkManaged);
    }
}
