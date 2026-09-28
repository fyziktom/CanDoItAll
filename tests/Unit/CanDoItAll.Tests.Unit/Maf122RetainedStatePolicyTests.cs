using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class Maf122RetainedStatePolicyTests {
    [Theory]
    [InlineData("1.20.0", true)]
    [InlineData("1.20.0+recorded-build", true)]
    [InlineData("1.22.0", true)]
    [InlineData("1.21.0", false)]
    [InlineData("1.23.0", false)]
    [InlineData("", false)]
    [InlineData("unknown", false)]
    public void Unsettled_work_requires_a_verified_native_writer_version(string writerVersion, bool compatible) {
        var envelope = new RuntimeStateEnvelope(RuntimeStateAdapterIds.Maf, RuntimeStateEnvelope.CurrentSchemaVersion,
            writerVersion, Guid.NewGuid(), ProviderTransportKind.Responses, "fixture-model", "tools", "context",
            DateTimeOffset.UtcNow, "{}");
        var request = new RuntimeStateCompatibilityRequest(envelope, false, false,
            envelope.ProviderProfileId, envelope.ProviderTransport, envelope.Model, envelope.ToolsetFingerprint,
            envelope.ContextPolicyFingerprint, envelope.HistoryMode) {
            CurrentAdapterPackageVersion = "1.22.0",
            RequiresVerifiedNativeAuthority = true
        };

        var decision = new MafRuntimeStateCompatibilityPolicy().Evaluate(request);

        Assert.Equal(compatible ? RuntimeStateCompatibilityOutcome.CompatibleRestore : RuntimeStateCompatibilityOutcome.Incompatible,
            decision.Outcome);
        if (!compatible) {
            Assert.Contains("reconcile outstanding effects", decision.Reason, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Parseable_unversioned_json_cannot_supply_pending_native_authority() {
        var request = new RuntimeStateCompatibilityRequest(null, true, false, Guid.NewGuid(),
            ProviderTransportKind.Responses, "fixture-model", "tools", "context", AgentChatHistoryMode.FrameworkManaged) {
            CurrentAdapterPackageVersion = "1.22.0",
            RequiresVerifiedNativeAuthority = true
        };

        Assert.Equal(RuntimeStateCompatibilityOutcome.Incompatible, new MafRuntimeStateCompatibilityPolicy().Evaluate(request).Outcome);
        Assert.Equal(RuntimeStateCompatibilityOutcome.RegisteredMigration,
            new MafRuntimeStateCompatibilityPolicy().Evaluate(request with { RequiresVerifiedNativeAuthority = false }).Outcome);
    }
}
