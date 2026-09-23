using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.UI.Overview;
using CanDoItAll.AgentFramework.UiSandbox;
using CanDoItAll.AgentFramework.Usage;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class OverviewSandboxContextTests {
    [Theory]
    [InlineData(null, SandboxSpecimen.Catalog)]
    [InlineData("", SandboxSpecimen.Catalog)]
    [InlineData("unknown", SandboxSpecimen.Catalog)]
    [InlineData("0", SandboxSpecimen.Catalog)]
    [InlineData(" OVERVIEW ", SandboxSpecimen.Overview)]
    [InlineData("capabilities", SandboxSpecimen.Capabilities)]
    public void Overview_token_and_unknown_tokens_preserve_existing_defaults(string? token, SandboxSpecimen expected) {
        Assert.Equal(expected, SandboxSpecimens.Parse(token));
    }

    public static TheoryData<string, OverviewSandboxScenario> Scenarios {
        get {
            var rows = new TheoryData<string, OverviewSandboxScenario>();
            foreach (var item in OverviewSandboxContext.Scenarios) {
                rows.Add(item.Token, item.Scenario);
            }
            return rows;
        }
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Every_overview_scenario_round_trips_without_enum_ordinals(string token, OverviewSandboxScenario scenario) {
        var context = OverviewSandboxContext.Parse(token, "flexible", "chats");
        Assert.Equal(scenario, context.Scenario);
        Assert.Equal(CatalogSandboxLayout.Flexible, context.Layout);
        var query = context.ToQuery();
        Assert.Equal("overview", query["specimen"]);
        Assert.Null(query["agentId"]);
        Assert.Null(query["teamId"]);
        Assert.Equal(context, OverviewSandboxContext.Parse(query["scenario"]?.ToString(), query["layout"]?.ToString(), query["usageScope"]?.ToString()));
    }

    [Theory]
    [InlineData("both", ProviderUsageWorkloadSelection.Both)]
    [InlineData("agents", ProviderUsageWorkloadSelection.Agents)]
    [InlineData("chats", ProviderUsageWorkloadSelection.SimpleChats)]
    public void All_three_scopes_have_matching_accepted_sample_identity(string token, ProviderUsageWorkloadSelection scope) {
        var state = OverviewSandboxFixture.Create(OverviewSandboxContext.Parse("ready", null, token));
        Assert.Equal(scope, state.DesiredScope);
        Assert.Equal(scope, state.AcceptedScope);
        Assert.True(state.HasUsage);
        Assert.True(state.CanOpen(AgentsOverviewDetail.Models));
        Assert.All(state.Consumers, row => Assert.Equal(scope == ProviderUsageWorkloadSelection.SimpleChats
            ? ProviderUsageConsumerKind.SimpleChatDefinition : ProviderUsageConsumerKind.Agent, row.ConsumerKind));
    }

    [Theory]
    [InlineData("scope-pending")]
    [InlineData("wrong-scope")]
    public void Mismatched_samples_do_not_enable_detail_actions(string scenario) {
        var state = OverviewSandboxFixture.Create(OverviewSandboxContext.Parse(scenario, null, "chats"));
        Assert.NotEqual(state.DesiredScope, state.AcceptedScope);
        Assert.False(state.HasUsage);
        Assert.Empty(state.Consumers);
        Assert.Empty(state.Providers);
        Assert.False(state.CanOpen(AgentsOverviewDetail.Consumers));
        Assert.False(state.CanOpen(AgentsOverviewDetail.Providers));
        Assert.False(state.CanOpen(AgentsOverviewDetail.Models));
    }

    [Fact]
    public void Baseline_is_the_frozen_canonical_projection() {
        using var stream = typeof(OverviewSandboxFixture).Assembly.GetManifestResourceStream("OverviewFixture.json")!;
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        var expected = JsonSerializer.Deserialize<AgentsOverviewState>(stream, options)!;
        var actual = OverviewSandboxFixture.Create(new());
        Assert.Equal(expected.Totals, actual.Totals);
        Assert.Equal(expected.UsageTotals, actual.UsageTotals);
        Assert.Equal(expected.Consumers.ToArray(), actual.Consumers.ToArray());
        Assert.Equal(expected.Providers.ToArray(), actual.Providers.ToArray());
        Assert.Equal(expected.Teams.ToArray(), actual.Teams.ToArray());
        Assert.Equal(6, actual.UsageTotals!.UsageObservationCount);
        Assert.True(actual.HasOverview);
        Assert.True(actual.HasUsage);
    }
}
