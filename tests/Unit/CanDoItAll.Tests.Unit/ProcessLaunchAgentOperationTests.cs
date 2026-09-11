using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessLaunchAgentOperationTests {
    [Fact]
    public void Missing_legacy_operation_remains_unspecified_and_readable_without_fabricating_a_launch_grant() {
        var authority = Authority();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var document = JsonNode.Parse(JsonSerializer.Serialize(authority.Principal, options))!;
        Assert.True(document.AsObject().Remove("operation"));
        var principal = Assert.IsType<ProcessLaunchPrincipal.AgentExecution>(
            JsonSerializer.Deserialize<ProcessLaunchPrincipal>(document.ToJsonString(), options));
        Assert.Equal(ProcessLaunchAgentOperation.Unspecified, principal.Operation);
        Assert.Equal(Assert.IsType<ProcessLaunchPrincipal.AgentExecution>(authority.Principal).Ceiling.AgentId, principal.Ceiling.AgentId);
        (authority with { Principal = principal }).Validate();
    }

    [Fact]
    public void Agent_operation_is_bound_to_intent_content_while_preserving_the_same_actual_source_identity() {
        var authority = Authority();
        var original = ProcessPreparedLaunchFixture.Create(authority, new(Guid.NewGuid())).Request;
        var source = Assert.IsType<ProcessLaunchPrincipal.AgentExecution>(authority.Principal);
        var changed = authority with { Principal = source with { Operation = ProcessLaunchAgentOperation.SubprocessLaunch } };
        Assert.Equal(ProcessLaunchIntentFingerprint.CallerFingerprint(authority), ProcessLaunchIntentFingerprint.CallerFingerprint(changed));
        Assert.NotEqual(ProcessLaunchIntentFingerprint.Compute(original), ProcessLaunchIntentFingerprint.Compute(original with { Authority = changed }));
    }

    private static ProcessLaunchAuthority Authority() {
        var profile = Guid.NewGuid();
        var project = new ProcessProjectAdmission(profile, Guid.NewGuid(), Guid.NewGuid());
        var ceiling = new ProcessLaunchAgentCeiling(Guid.NewGuid(), Guid.NewGuid(), 1, ProcessLaunchSourceScopeKind.Organization,
            profile.ToString("N"), true, true, "fixture", "fixture", [], [], [], [], []);
        return new(new ProcessLaunchPrincipal.AgentExecution(ceiling, ProcessLaunchAgentOperation.StructureStart),
            profile, project, true, true, "fixture");
    }
}
