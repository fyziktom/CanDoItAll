using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit;

/// <summary>
/// The versioned fingerprint model: tool contracts (not just names) drive the
/// v2 toolset fingerprint, the authority-policy dimension is independent of
/// the model-context digest, schema-v1 envelopes restore through the explicit
/// names-only rule, and the adapter package range is an explicit gate.
/// </summary>
public sealed class RuntimeStateFingerprintModelTests
{
    [Fact]
    public void Contract_fingerprint_changes_when_a_tool_schema_changes_with_the_same_name()
    {
        var firstTool = AIFunctionFactory.Create(
            (string path) => "content",
            "workspace_read_file",
            "Reads a file.");
        var secondTool = AIFunctionFactory.Create(
            (string path, int maxBytes) => "content",
            "workspace_read_file",
            "Reads a file.");

        var firstFingerprint = MafToolsetFingerprint.ComputeContractFingerprint([firstTool]);
        var secondFingerprint = MafToolsetFingerprint.ComputeContractFingerprint([secondTool]);

        Assert.NotEqual(firstFingerprint, secondFingerprint);
        // The names-only digest cannot see the schema change — that is exactly
        // why the contract fingerprint exists.
        Assert.Equal(
            MafToolsetFingerprint.Compute([firstTool.Name]),
            MafToolsetFingerprint.Compute([secondTool.Name]));
    }

    [Fact]
    public void Schema_v1_envelope_restores_through_the_names_only_rule()
    {
        var policy = new MafRuntimeStateCompatibilityPolicy();
        var providerId = Guid.NewGuid();
        var envelope = new RuntimeStateEnvelope(
            RuntimeStateAdapterIds.Maf,
            schemaVersion: 1,
            adapterPackageVersion: "1.0.0",
            providerId,
            ProviderTransportKind.Responses,
            "gpt-5.4-mini",
            toolsetFingerprint: "names-digest",
            contextPolicyFingerprint: "digest-a",
            DateTimeOffset.UtcNow,
            payloadJson: """{"messages":[]}""");

        var decision = policy.Evaluate(new RuntimeStateCompatibilityRequest(
            envelope,
            IsLegacyUnversionedState: false,
            HasUnparseableStoredState: false,
            CurrentProviderProfileId: providerId,
            CurrentProviderTransport: ProviderTransportKind.Responses,
            CurrentModel: "gpt-5.4-mini",
            CurrentToolsetFingerprint: "contract-digest-current",
            CurrentContextPolicyFingerprint: "digest-a",
            CurrentHistoryMode: null)
        {
            CurrentLegacyToolsetNameFingerprint = "names-digest",
            CurrentAdapterPackageVersion = "1.9.4"
        });

        Assert.Equal(RuntimeStateCompatibilityOutcome.CompatibleRestore, decision.Outcome);
        Assert.Equal(MafRuntimeStateCompatibilityPolicy.EnvelopeV1ReadMigrationId, decision.MigrationId);
    }

    [Fact]
    public void Authority_policy_change_invalidates_v2_state_even_with_unchanged_model_context()
    {
        var policy = new MafRuntimeStateCompatibilityPolicy();
        var providerId = Guid.NewGuid();
        var envelope = new RuntimeStateEnvelope(
            RuntimeStateAdapterIds.Maf,
            RuntimeStateEnvelope.CurrentSchemaVersion,
            adapterPackageVersion: "1.0.0",
            providerId,
            ProviderTransportKind.Responses,
            "gpt-5.4-mini",
            toolsetFingerprint: "contract-digest",
            contextPolicyFingerprint: "digest-a",
            DateTimeOffset.UtcNow,
            payloadJson: """{"messages":[]}""")
        {
            AuthorityPolicyFingerprint = "authority-old"
        };

        var decision = policy.Evaluate(new RuntimeStateCompatibilityRequest(
            envelope,
            IsLegacyUnversionedState: false,
            HasUnparseableStoredState: false,
            CurrentProviderProfileId: providerId,
            CurrentProviderTransport: ProviderTransportKind.Responses,
            CurrentModel: "gpt-5.4-mini",
            CurrentToolsetFingerprint: "contract-digest",
            CurrentContextPolicyFingerprint: "digest-a",
            CurrentHistoryMode: null)
        {
            CurrentAuthorityPolicyFingerprint = "authority-new"
        });

        Assert.Equal(RuntimeStateCompatibilityOutcome.Incompatible, decision.Outcome);
        Assert.Contains("authority", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Adapter_package_major_change_is_outside_the_explicit_compatibility_range()
    {
        Assert.True(MafRuntimeStateCompatibilityPolicy.IsAdapterPackageWithinCompatibilityRange(
            "1.2.3+build", "1.9.0", out _));
        Assert.False(MafRuntimeStateCompatibilityPolicy.IsAdapterPackageWithinCompatibilityRange(
            "1.2.3", "2.0.0", out var reason));
        Assert.Contains("major version", reason, StringComparison.OrdinalIgnoreCase);
        // Unavailable versions skip the range check; fingerprints stay decisive.
        Assert.True(MafRuntimeStateCompatibilityPolicy.IsAdapterPackageWithinCompatibilityRange(
            "", "2.0.0", out _));
    }
}
