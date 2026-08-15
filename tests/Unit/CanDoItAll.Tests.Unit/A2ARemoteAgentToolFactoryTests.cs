using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class A2ARemoteAgentToolFactoryTests
{
    [Fact]
    public async Task CreateSkillToolsAsyncSkipsDisabledEndpoints()
    {
        var factory = new A2ARemoteAgentToolFactory(configuration: null, loggerFactory: null);
        var result = await factory.CreateSkillToolsAsync(
            [
                new AgentA2ARemoteEndpointSettings
                {
                    EndpointId = "disabled",
                    Enabled = false,
                    BaseUri = "not-a-uri"
                }
            ],
            CancellationToken.None);

        Assert.Empty(result.Tools);
        Assert.Empty(result.Disposables);
    }

    [Fact]
    public async Task CreateSkillToolsAsyncFailsWhenBearerSecretIsMissing()
    {
        var factory = new A2ARemoteAgentToolFactory(configuration: null, loggerFactory: null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            factory.CreateSkillToolsAsync(
                [
                    new AgentA2ARemoteEndpointSettings
                    {
                        EndpointId = "secured",
                        BaseUri = "https://agents.example.test",
                        Authentication = AgentA2AAuthenticationKind.BearerToken,
                        AuthSecretConfigurationKey = "AgentFramework:A2A:SecuredToken"
                    }
                ],
                CancellationToken.None));

        Assert.Contains("SecuredToken", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateSkillToolsAsyncFailsBeforeNetworkForInvalidEnabledEndpoint()
    {
        var factory = new A2ARemoteAgentToolFactory(configuration: null, loggerFactory: null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            factory.CreateSkillToolsAsync(
                [
                    new AgentA2ARemoteEndpointSettings
                    {
                        EndpointId = "invalid",
                        BaseUri = "file:///tmp/agent"
                    }
                ],
                CancellationToken.None));

        Assert.Contains("absolute http or https", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
