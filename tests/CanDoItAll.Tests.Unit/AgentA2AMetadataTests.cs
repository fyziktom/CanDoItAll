using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentA2AMetadataTests
{
    [Fact]
    public void WriteAndReadRoundTripRemoteEndpoint()
    {
        var settings = new AgentA2ASettings
        {
            RemoteEndpoints =
            [
                new AgentA2ARemoteEndpointSettings
                {
                    EndpointId = "delivery_qa",
                    DisplayName = "Delivery QA",
                    BaseUri = "https://agents.example.test/a2a/",
                    AgentCardPath = ".well-known/agent-card.json",
                    ProtocolBinding = AgentA2AProtocolBindingPreference.JsonRpc,
                    Authentication = AgentA2AAuthenticationKind.BearerToken,
                    AuthSecretConfigurationKey = "AgentFramework:A2A:DeliveryQaToken",
                    ToolNamePrefix = "qa",
                    AllowedSkillNames = ["Review", "Review", "Validate"],
                    TimeoutSeconds = 45
                }
            ]
        };

        var configurationJson = AgentA2AMetadata.Write("""{"workspaceTools":{"canReadFiles":true}}""", settings);
        var roundTrip = AgentA2AMetadata.Read(configurationJson);

        Assert.Single(roundTrip.RemoteEndpoints);
        var endpoint = roundTrip.RemoteEndpoints[0];
        Assert.Equal("delivery_qa", endpoint.EndpointId);
        Assert.Equal("https://agents.example.test/a2a", endpoint.BaseUri);
        Assert.Equal("/.well-known/agent-card.json", endpoint.AgentCardPath);
        Assert.Equal(AgentA2AProtocolBindingPreference.JsonRpc, endpoint.ProtocolBinding);
        Assert.Equal(AgentA2AAuthenticationKind.BearerToken, endpoint.Authentication);
        Assert.Equal("AgentFramework:A2A:DeliveryQaToken", endpoint.AuthSecretConfigurationKey);
        Assert.Equal("qa", endpoint.ToolNamePrefix);
        Assert.Equal(["Review", "Validate"], endpoint.AllowedSkillNames);
        Assert.Contains("workspaceTools", configurationJson, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteRemovesDefaultA2AConfiguration()
    {
        var configurationJson = AgentA2AMetadata.Write("""{"a2a":{"remoteEndpoints":[]},"x":1}""", new AgentA2ASettings());

        Assert.Equal("""{"x":1}""", configurationJson);
    }

    [Fact]
    public void ValidateRejectsEnabledInvalidUriRawSecretAndDuplicateIds()
    {
        var settings = new AgentA2ASettings
        {
            RemoteEndpoints =
            [
                new AgentA2ARemoteEndpointSettings
                {
                    EndpointId = "qa",
                    BaseUri = "file:///tmp/agent",
                    Authentication = AgentA2AAuthenticationKind.BearerToken,
                    AuthSecretConfigurationKey = string.Concat("s", "k-", "this-is-not-a-secret-reference-1234567890")
                },
                new AgentA2ARemoteEndpointSettings
                {
                    EndpointId = "qa",
                    BaseUri = "https://agents.example.test"
                }
            ]
        };

        var result = AgentA2AMetadata.Validate(settings);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("duplicated", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("absolute http or https", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("raw secret", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateIgnoresDisabledIncompleteEndpoint()
    {
        var settings = new AgentA2ASettings
        {
            RemoteEndpoints =
            [
                new AgentA2ARemoteEndpointSettings
                {
                    EndpointId = "draft_remote_agent",
                    Enabled = false,
                    BaseUri = "not-a-uri"
                }
            ]
        };

        var result = AgentA2AMetadata.Validate(settings);

        Assert.True(result.Succeeded);
    }
}
