using System.Text.Json;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceSettingsV2OutcomeTests {
    [Fact]
    public void V2_acceptance_requires_committed_identity_and_complete_policy_and_roundtrips_exact_knowledge() {
        var existing = new WorkspaceSettingsExpectation(new(Guid.NewGuid()), WorkspaceSettingsHeadState.Existing, new(Guid.NewGuid(), Guid.NewGuid()));
        var empty = new WorkspaceSettingsExpectation(existing.Namespace, WorkspaceSettingsHeadState.Empty, null);
        var values = WorkspaceSettingsValues.Capture(new());
        var policy = DecisionDataPolicy.PlanningDefault(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2));
        Assert.Throws<ArgumentNullException>(() => new WorkspaceSettingsSaveOutcomeV2.Accepted(null!));
        Assert.Throws<ArgumentException>(() => new WorkspaceSettingsSaveOutcomeV2.Accepted(new(empty, values, null)));
        Assert.Throws<ArgumentException>(() => new WorkspaceSettingsSaveOutcomeV2.Accepted(new(existing, values, null)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkspaceSettingsSaveOutcomeV2.Accepted(new(existing, values, policy), (WorkspaceSettingsSecondaryDiagnostic)99));
        var accepted = new WorkspaceSettingsSaveOutcomeV2.Accepted(new(existing, values, policy), WorkspaceSettingsSecondaryDiagnostic.CleanupPending);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var wire = JsonSerializer.Serialize<WorkspaceSettingsSaveOutcomeV2>(accepted, options);
        Assert.Equal(accepted, JsonSerializer.Deserialize<WorkspaceSettingsSaveOutcomeV2>(wire, options));
        Assert.Contains("\"schemaVersion\":2", wire, StringComparison.Ordinal);
        Assert.Contains("\"decisionDataPolicy\":{", wire, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => JsonSerializer.Deserialize<WorkspaceSettingsSaveOutcomeV2>(
            wire.Replace(DecisionDataPolicyCodec.Serialize(policy), "null", StringComparison.Ordinal), options));
    }

    [Fact]
    public void V2_refusal_and_uncertainty_have_no_fabricated_policy_or_accepted_snapshot() {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        foreach (var reason in Enum.GetValues<WorkspaceSettingsRejectionReason>()) {
            WorkspaceSettingsSaveOutcomeV2 rejected = new WorkspaceSettingsSaveOutcomeV2.Rejected(reason);
            var wire = JsonSerializer.Serialize(rejected, options);
            Assert.Equal(rejected, JsonSerializer.Deserialize<WorkspaceSettingsSaveOutcomeV2>(wire, options));
            Assert.DoesNotContain("snapshot", wire, StringComparison.Ordinal);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkspaceSettingsSaveOutcomeV2.Rejected((WorkspaceSettingsRejectionReason)99));
        WorkspaceSettingsSaveOutcomeV2 unconfirmed = new WorkspaceSettingsSaveOutcomeV2.Unconfirmed();
        var unknownWire = JsonSerializer.Serialize(unconfirmed, options);
        Assert.Equal(unconfirmed, JsonSerializer.Deserialize<WorkspaceSettingsSaveOutcomeV2>(unknownWire, options));
        Assert.DoesNotContain("snapshot", unknownWire, StringComparison.Ordinal);
        Assert.DoesNotContain("decisionDataPolicy", unknownWire, StringComparison.Ordinal);
    }
}
