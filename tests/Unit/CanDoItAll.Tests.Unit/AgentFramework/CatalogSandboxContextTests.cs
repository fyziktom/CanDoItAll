using System.Text.Json;
using CanDoItAll.AgentFramework.UI.Catalog;
using CanDoItAll.AgentFramework.UiSandbox;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class CatalogSandboxContextTests {
    private static AgentCatalogSnapshot Fixture() {
        using var stream = typeof(CatalogAssets).Assembly.GetManifestResourceStream("CatalogFixture.json");
        Assert.NotNull(stream);
        return JsonSerializer.Deserialize<AgentCatalogSnapshot>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    [Fact]
    public void Defaults_are_stable() {
        Assert.Equal(new CatalogSandboxContext(), CatalogSandboxContext.Parse(null, null, null, null, Fixture()));
    }

    [Theory]
    [InlineData("normal", CatalogSandboxScenario.Normal)]
    [InlineData("loading", CatalogSandboxScenario.Loading)]
    [InlineData("empty", CatalogSandboxScenario.Empty)]
    [InlineData("card-states", CatalogSandboxScenario.CardStates)]
    [InlineData("avatar-fallback", CatalogSandboxScenario.AvatarFallback)]
    public void Explicit_context_round_trips(string token, CatalogSandboxScenario scenario) {
        var fixture = Fixture();
        var context = CatalogSandboxContext.Parse(token, "FLEXIBLE", fixture.Agents[0].Id.ToString(), fixture.Teams[0].Id.ToString(), fixture);
        Assert.Equal(scenario, context.Scenario);
        Assert.Equal(CatalogSandboxLayout.Flexible, context.Layout);
        Assert.Equal(new AgentCatalogSelection(fixture.Agents[0].Id, fixture.Teams[0].Id), context.Selection);
        var query = context.ToQuery();
        Assert.Equal(context, CatalogSandboxContext.Parse(query["scenario"]?.ToString(), query["layout"]?.ToString(), query["agentId"]?.ToString(), query["teamId"]?.ToString(), fixture));
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("9")]
    public void Invalid_tokens_use_defaults(string value) {
        Assert.Equal(new CatalogSandboxContext(), CatalogSandboxContext.Parse(value, value, null, null, Fixture()));
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("ffffffff-ffff-ffff-ffff-ffffffffffff")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void Unknown_or_malformed_ids_are_not_accepted(string value) {
        Assert.Equal(new CatalogSandboxContext(), CatalogSandboxContext.Parse(null, null, value, value, Fixture()));
    }

    [Fact]
    public void Selection_ids_are_validated_independently() {
        var fixture = Fixture();
        var agent = CatalogSandboxContext.Parse(null, null, fixture.Agents[0].Id.ToString(), "invalid", fixture);
        var team = CatalogSandboxContext.Parse(null, null, "invalid", fixture.Teams[0].Id.ToString(), fixture);
        Assert.Equal(new AgentCatalogSelection(fixture.Agents[0].Id, null), agent.Selection);
        Assert.Equal(new AgentCatalogSelection(null, fixture.Teams[0].Id), team.Selection);
    }
}
