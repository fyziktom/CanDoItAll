using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Capabilities;
using CanDoItAll.AgentFramework.UiSandbox;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class CapabilitiesSandboxContextTests {
    [Theory]
    [InlineData(null, SandboxSpecimen.Catalog)]
    [InlineData("", SandboxSpecimen.Catalog)]
    [InlineData("unknown", SandboxSpecimen.Catalog)]
    [InlineData("0", SandboxSpecimen.Catalog)]
    [InlineData("catalog", SandboxSpecimen.Catalog)]
    [InlineData(" CAPABILITIES ", SandboxSpecimen.Capabilities)]
    public void Specimen_tokens_preserve_catalog_default(string? token, SandboxSpecimen expected) {
        Assert.Equal(expected, SandboxSpecimens.Parse(token));
    }

    public static TheoryData<string, CapabilitiesSandboxScenario> Scenarios {
        get {
            var data = new TheoryData<string, CapabilitiesSandboxScenario>();
            foreach (var item in CapabilitiesSandboxContext.Scenarios) {
                data.Add(item.Token, item.Scenario);
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Every_named_scenario_round_trips_without_enum_ordinals(string token, CapabilitiesSandboxScenario scenario) {
        var context = CapabilitiesSandboxContext.Parse(token, "flexible", CapabilitiesSandboxFixture.BetaId.ToString());
        Assert.Equal(scenario, context.Scenario);
        var query = context.ToQuery();
        Assert.Equal("capabilities", query["specimen"]);
        Assert.Null(query["teamId"]);
        Assert.Equal(context, CapabilitiesSandboxContext.Parse(query["scenario"]?.ToString(),
            query["layout"]?.ToString(), query["agentId"]?.ToString()));
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("ffffffff-ffff-ffff-ffff-ffffffffffff")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void Invalid_targets_fail_closed_instead_of_selecting_another_agent(string token) {
        var context = CapabilitiesSandboxContext.Parse("selected", null, token);
        var fixture = CapabilitiesSandboxFixture.Create(context);
        Assert.Equal(AgentCapabilitiesLoadState.Failed, fixture.LoadState);
        Assert.NotNull(fixture.Snapshot.LoadError);
        Assert.NotEqual(CapabilitiesSandboxFixture.AlphaId, context.AgentId);
    }

    [Fact]
    public void Kind_and_proof_fixture_covers_current_public_contract_with_stable_ids() {
        var context = CapabilitiesSandboxContext.Parse("kinds-and-proof", null, null);
        var first = CapabilitiesSandboxFixture.Create(context);
        var second = CapabilitiesSandboxFixture.Create(context);
        Assert.Equal(Enum.GetValues<CapabilityKind>().Order(), first.Snapshot.Capabilities.Select(item => item.Kind).Distinct().Order());
        Assert.Equal(Enum.GetValues<CapabilityProofStatus>().Order(), first.Snapshot.Capabilities.Select(item => item.ProofStatus).Distinct().Order());
        Assert.Equal(first.Snapshot.Capabilities.Select(item => item.Id), second.Snapshot.Capabilities.Select(item => item.Id));
        Assert.NotEmpty(first.Snapshot.SelectedCapabilityIds);
        Assert.Contains(first.Snapshot.Capabilities, item => !first.Snapshot.SelectedCapabilityIds.Contains(item.Id));
        Assert.All(first.Snapshot.Capabilities, item => Assert.StartsWith("render-only:", item.EndpointOrPath));
    }

    [Fact]
    public void Pending_assignment_keeps_the_authoritative_before_set() {
        var before = CapabilitiesSandboxFixture.Create(CapabilitiesSandboxContext.Parse("selected", null, null)).Snapshot;
        var pending = CapabilitiesSandboxFixture.Create(CapabilitiesSandboxContext.Parse("assignment-pending", null, null)).Snapshot;
        Assert.Equal(before.SelectedCapabilityIds.ToArray(), pending.SelectedCapabilityIds.ToArray());
        Assert.True(pending.IsBusy);
        Assert.NotNull(pending.Operation);
        Assert.False(pending.Operation.CanRetry);
    }

    [Fact]
    public void Receiptless_and_receipt_backed_presentations_expose_distinct_actions() {
        var receipt = CapabilitiesSandboxFixture.Create(CapabilitiesSandboxContext.Parse("verification-recovery", null, null)).Snapshot.Operation!;
        var unknown = CapabilitiesSandboxFixture.Create(CapabilitiesSandboxContext.Parse("diagnostic-acknowledgement", null, null)).Snapshot.Operation!;
        Assert.True(receipt.CanVerify);
        Assert.False(receipt.CanAcknowledgeDiagnostic);
        Assert.False(unknown.CanVerify);
        Assert.True(unknown.CanAcknowledgeDiagnostic);
    }

    [Fact]
    public void Baseline_retains_the_pre_extraction_measurement_cards_and_selection() {
        var context = CapabilitiesSandboxContext.Parse("baseline", null, null);
        var fixture = CapabilitiesSandboxFixture.Create(context);
        Assert.Equal(AgentCapabilitiesLoadState.Ready, fixture.LoadState);
        Assert.Contains(fixture.Snapshot.Agents, item => item.Id == CapabilitiesSandboxFixture.AlphaId);
        var cards = fixture.Snapshot.Capabilities.Where(item => item.Tags.Contains("capa03-benchmark")).ToArray();
        Assert.Contains(cards, item => item.Kind == CapabilityKind.McpServer &&
            item.EndpointOrPath == "render-only:capa03-mcp" && item.ProofStatus == CapabilityProofStatus.Verified);
        Assert.Contains(cards, item => item.Kind == CapabilityKind.Skill);
        Assert.Contains(cards, item => item.Kind == CapabilityKind.Tool);
        Assert.Contains(fixture.Snapshot.SelectedCapabilityIds, id => cards.Any(item => item.Id == id));
    }
}

