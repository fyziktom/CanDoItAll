using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class BrowserToolInputSchemaTests {
    private static JsonElement Original => JsonSerializer.Deserialize<JsonElement>("""
        {"type":"object","properties":{"depth":{"type":"number"},"boxes":{"type":"boolean"},"fullPage":{"type":"boolean"}}}
        """);

    [Fact]
    public void Advertised_snapshot_schema_uses_owner_integer_required_and_boxed_bounds() {
        var constraints = ProcessBrowserToolConstraints.For(ToolContractCatalog.BrowserSnapshot);
        var schema = MafToolInputSchema.Apply(Original, constraints);
        var depth = schema.GetProperty("properties").GetProperty("depth");
        Assert.Equal("integer", depth.GetProperty("type").GetString());
        Assert.Equal(4, depth.GetProperty("maximum").GetInt32());
        Assert.Contains(schema.GetProperty("required").EnumerateArray(), item => item.GetString() == "depth");
        Assert.Contains("at most 2 when boxes=true", depth.GetProperty("description").GetString());
        Assert.Contains("at most 2 when boxes=true", schema.GetProperty("properties").GetProperty("boxes").GetProperty("description").GetString());
        Assert.Equal("number", Original.GetProperty("properties").GetProperty("depth").GetProperty("type").GetString());
    }

    [Fact]
    public void Advertised_screenshot_schema_allows_only_viewport_capture() {
        var schema = MafToolInputSchema.Apply(Original, ProcessBrowserToolConstraints.For(ToolContractCatalog.BrowserTakeScreenshot));
        Assert.False(Assert.Single(schema.GetProperty("properties").GetProperty("fullPage").GetProperty("enum").EnumerateArray()).GetBoolean());
    }

    [Theory]
    [InlineData(null, false, false)]
    [InlineData("2", true, true)]
    [InlineData("3", true, false)]
    [InlineData("4", false, true)]
    [InlineData("5", false, false)]
    [InlineData("2.5", false, false)]
    [InlineData("-1", false, true)]
    public void Actual_scope_policy_enforces_the_advertised_snapshot_bounds(string? depth, bool boxes, bool allowed) {
        var arguments = new Dictionary<string, string> { ["boxes"] = boxes.ToString() };
        if (depth is not null) {
            arguments["depth"] = depth;
        }
        var context = Context(ToolContractCatalog.BrowserSnapshot, arguments);
        var decision = ProcessToolInvocationScopePolicy.Instance.EvaluateRestrictions(context, "signature",
            ToolInvocationScopePolicyPhase.BeforeExternalTargetBoundary);
        Assert.Equal(allowed, decision is null);
    }

    [Fact]
    public void Changed_policy_invalidates_prepared_contract_without_changing_arguments() {
        const string arguments = "{\"depth\":2,\"boxes\":false}";
        var payload = new AgentToolPreparedPayload(ToolContractCatalog.BrowserSnapshot, 1,
            AgentToolProtocolEnvelope.ComputeDigest(arguments), arguments, AgentToolProposalEffect.Read,
            AgentToolProposalRecovery.RevalidateAndRead);
        var first = MafToolInputSchema.Bind(payload, MafToolInputSchema.Apply(Original,
            [new ToolIntegerInputConstraint("depth", 4, true)]));
        var changed = MafToolInputSchema.Bind(payload, MafToolInputSchema.Apply(Original,
            [new ToolIntegerInputConstraint("depth", 3, true)]));
        Assert.NotEqual(first.Digest, changed.Digest);
        Assert.Equal(arguments, first.ArgumentsJson);
        Assert.Equal(arguments, changed.ArgumentsJson);
    }

    [Fact]
    public void Upstream_stricter_bound_is_never_widened() {
        var schema = MafToolInputSchema.Apply(MafToolInputSchema.Apply(Original,
            [new ToolIntegerInputConstraint("depth", 1, true)]), ProcessBrowserToolConstraints.For(ToolContractCatalog.BrowserSnapshot));
        Assert.Equal(1, schema.GetProperty("properties").GetProperty("depth").GetProperty("maximum").GetInt32());
    }

    private static ToolInvocationPolicyContext Context(string tool, IReadOnlyDictionary<string, string> arguments) =>
        new(Guid.NewGuid(), "Browser tester", tool, arguments, ToolInvocationClassification.Read,
            true, true, false, "execution", "process-step", "run", "step") {
            ScopePolicy = ProcessToolInvocationScopePolicy.Instance,
            PathArguments = ToolInvocationPathArgumentResolver.Resolve(tool, arguments.Select(item => new KeyValuePair<string, object?>(item.Key, item.Value)))
        };
}
