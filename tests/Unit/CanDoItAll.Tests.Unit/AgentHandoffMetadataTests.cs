using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentHandoffMetadataTests
{
    [Fact]
    public void Validate_rejects_enabled_handoff_without_routes()
    {
        var result = AgentHandoffMetadata.Validate(new AgentHandoffSettings
        {
            Enabled = true
        });

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("no enabled routes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_rejects_same_agent_route()
    {
        var agentId = Guid.NewGuid();

        var result = AgentHandoffMetadata.Validate(new AgentHandoffSettings
        {
            Enabled = true,
            Routes =
            [
                new AgentHandoffRouteSettings
                {
                    SourceAgentId = agentId,
                    TargetAgentId = agentId,
                    Reason = "Review your own output."
                }
            ]
        });

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("cannot target the same agent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Write_and_read_roundtrip_preserves_routes_and_guard_settings()
    {
        var entryAgentId = Guid.NewGuid();
        var targetAgentId = Guid.NewGuid();
        var settings = new AgentHandoffSettings
        {
            Enabled = true,
            EntryAgentId = entryAgentId,
            ReturnToPrevious = true,
            MaxHandoffDepth = 3,
            HandoffInstructions = "Transfer only after enough implementation context is available.",
            EmitAgentResponseEvents = true,
            EmitAgentResponseUpdateEvents = true,
            Routes =
            [
                new AgentHandoffRouteSettings
                {
                    SourceAgentId = entryAgentId,
                    TargetAgentId = targetAgentId,
                    Reason = "QA validates implementation artifacts."
                }
            ]
        };

        var json = AgentHandoffMetadata.Write("{}", settings);
        var parsed = AgentHandoffMetadata.Read(json);

        Assert.True(parsed.Enabled);
        Assert.Equal(entryAgentId, parsed.EntryAgentId);
        Assert.True(parsed.ReturnToPrevious);
        Assert.Equal(3, parsed.MaxHandoffDepth);
        Assert.True(parsed.EmitAgentResponseEvents);
        var route = Assert.Single(parsed.Routes);
        Assert.Equal(entryAgentId, route.SourceAgentId);
        Assert.Equal(targetAgentId, route.TargetAgentId);
        Assert.Equal("QA validates implementation artifacts.", route.Reason);
    }
}
