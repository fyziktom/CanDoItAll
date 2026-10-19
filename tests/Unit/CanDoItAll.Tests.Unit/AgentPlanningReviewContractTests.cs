using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentPlanningReviewContractTests {
    [Theory]
    [InlineData(9_007_199_254_740_993L)]
    [InlineData(long.MaxValue)]
    public void Planning_action_revisions_round_trip_without_number_conversion(long revision) {
        var expected = new AgentPlanningDecisionExpectation(Guid.NewGuid(), revision, revision, Guid.NewGuid(), 1);
        var request = new AgentPlanningActionRequest(AgentPlanningAction.Cancel, Guid.NewGuid(), expected);
        var json = JsonSerializer.Serialize(request, JsonSerializerOptions.Web);
        Assert.Equal(request, JsonSerializer.Deserialize<AgentPlanningActionRequest>(json, JsonSerializerOptions.Web));
        using var document = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.String, document.RootElement.GetProperty("expected").GetProperty("runRevision").ValueKind);
        Assert.Equal(JsonValueKind.String, document.RootElement.GetProperty("expected").GetProperty("journalRevision").ValueKind);
    }

    [Fact]
    public void Planning_action_rejects_numeric_noncanonical_and_unknown_expectations() {
        var request = new AgentPlanningActionRequest(AgentPlanningAction.Refresh, Guid.NewGuid(),
            new(Guid.NewGuid(), 1, 2, Guid.NewGuid(), 1));
        var json = JsonSerializer.Serialize(request, JsonSerializerOptions.Web);
        foreach (var name in new[] { "runRevision", "journalRevision" }) {
            foreach (var token in new[] { "1", "null", "true", "\"0\"", "\"01\"", "\"+1\"", "\"-1\"",
                "\" 1\"", "\"1.0\"", "\"1e0\"", "\"9223372036854775808\"" }) {
                var invalid = JsonNode.Parse(json)!;
                invalid["expected"]![name] = JsonNode.Parse(token);
                Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AgentPlanningActionRequest>(invalid.ToJsonString(), JsonSerializerOptions.Web));
            }
        }
        var unknown = JsonNode.Parse(json)!;
        unknown["expected"]!["currentRevision"] = "2";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AgentPlanningActionRequest>(unknown.ToJsonString(), JsonSerializerOptions.Web));
    }
}
