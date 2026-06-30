using System.Net.Http.Headers;
using System.Text.Json;
using A2A;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using A2AAgentCard = A2A.AgentCard;
using A2AAgentSkill = A2A.AgentSkill;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class A2ARemoteAgentToolFactory(
    IConfiguration? configuration,
    ILoggerFactory? loggerFactory)
{
    private readonly ILogger? resolverLogger = loggerFactory?.CreateLogger("CanDoItAll.AgentFramework.Maf.A2A");

    public async Task<A2ARemoteAgentToolBuildResult> CreateSkillToolsAsync(
        IReadOnlyList<AgentA2ARemoteEndpointSettings> endpoints,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var normalizedSettings = AgentA2AMetadata.Normalize(new AgentA2ASettings
        {
            RemoteEndpoints = endpoints.ToList()
        });
        var validation = AgentA2AMetadata.Validate(normalizedSettings);
        if (!validation.Succeeded)
        {
            throw new InvalidOperationException("A2A remote endpoint configuration is invalid: " + string.Join(" ", validation.Errors));
        }

        var tools = new List<AITool>();
        var disposables = new List<IDisposable>();
        var toolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var endpoint in normalizedSettings.RemoteEndpoints.Where(endpoint => endpoint.Enabled && endpoint.ExposeSkillsAsTools))
            {
                var endpointResult = await CreateEndpointToolsAsync(
                    endpoint,
                    toolNames,
                    cancellationToken).ConfigureAwait(false);
                tools.AddRange(endpointResult.Tools);
                disposables.AddRange(endpointResult.Disposables);
            }
        }
        catch
        {
            foreach (var disposable in disposables)
            {
                disposable.Dispose();
            }

            throw;
        }

        return new A2ARemoteAgentToolBuildResult(tools, disposables);
    }

    private async Task<A2ARemoteAgentToolBuildResult> CreateEndpointToolsAsync(
        AgentA2ARemoteEndpointSettings endpoint,
        HashSet<string> toolNames,
        CancellationToken cancellationToken)
    {
        var httpClient = CreateHttpClient(endpoint);
        var resolver = new A2ACardResolver(
            new Uri(endpoint.BaseUri, UriKind.Absolute),
            httpClient,
            endpoint.AgentCardPath,
            resolverLogger);
        var agentCard = await resolver.GetAgentCardAsync(cancellationToken).ConfigureAwait(false);
        if (agentCard.Skills.Count == 0)
        {
            throw new InvalidOperationException($"A2A endpoint '{endpoint.EndpointId}' did not publish any skills in its agent card.");
        }

        var a2aAgent = agentCard.AsAIAgent(
            httpClient,
            CreateClientOptions(endpoint),
            loggerFactory);
        var disposables = new List<IDisposable> { httpClient };
        if (a2aAgent is IDisposable disposableAgent)
        {
            disposables.Add(disposableAgent);
        }

        var allowedSkillNames = endpoint.AllowedSkillNames.Count == 0
            ? null
            : endpoint.AllowedSkillNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tools = new List<AITool>();
        foreach (var skill in agentCard.Skills.Where(skill => allowedSkillNames is null || allowedSkillNames.Contains(skill.Name)))
        {
            tools.Add(CreateSkillTool(endpoint, agentCard, skill, a2aAgent, toolNames));
        }

        if (tools.Count == 0)
        {
            throw new InvalidOperationException($"A2A endpoint '{endpoint.EndpointId}' did not expose any skill matching the configured allow-list.");
        }

        return new A2ARemoteAgentToolBuildResult(tools, disposables);
    }

    private HttpClient CreateHttpClient(AgentA2ARemoteEndpointSettings endpoint)
    {
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(endpoint.TimeoutSeconds)
        };

        if (endpoint.Authentication == AgentA2AAuthenticationKind.BearerToken)
        {
            var token = configuration?[endpoint.AuthSecretConfigurationKey];
            if (string.IsNullOrWhiteSpace(token))
            {
                httpClient.Dispose();
                throw new InvalidOperationException($"A2A endpoint '{endpoint.EndpointId}' requires bearer auth, but configuration key '{endpoint.AuthSecretConfigurationKey}' is not resolved.");
            }

            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        }

        return httpClient;
    }

    private static A2AClientOptions? CreateClientOptions(AgentA2ARemoteEndpointSettings endpoint)
    {
        var preferredBindings = endpoint.ProtocolBinding switch
        {
            AgentA2AProtocolBindingPreference.HttpJson => [ProtocolBindingNames.HttpJson],
            AgentA2AProtocolBindingPreference.JsonRpc => [ProtocolBindingNames.JsonRpc],
            _ => new List<string>()
        };

        return preferredBindings.Count == 0
            ? null
            : new A2AClientOptions
            {
                PreferredBindings = preferredBindings
            };
    }

    private static AITool CreateSkillTool(
        AgentA2ARemoteEndpointSettings endpoint,
        A2AAgentCard agentCard,
        A2AAgentSkill skill,
        AIAgent a2aAgent,
        HashSet<string> toolNames)
    {
        if (string.IsNullOrWhiteSpace(skill.Name))
        {
            throw new InvalidOperationException($"A2A endpoint '{endpoint.EndpointId}' returned a skill without a name.");
        }

        var toolName = AgentA2AMetadata.NormalizeToolNamePrefix($"{endpoint.ToolNamePrefix}_{skill.Name}");
        if (!toolNames.Add(toolName))
        {
            throw new InvalidOperationException($"A2A skill tool name '{toolName}' is duplicated after sanitization. Configure a unique toolNamePrefix for each endpoint.");
        }

        var options = new AIFunctionFactoryOptions
        {
            Name = toolName,
            Description = CreateSkillDescription(endpoint, agentCard, skill)
        };

        return AIFunctionFactory.Create(RunRemoteSkillAsync, options);

        async Task<string> RunRemoteSkillAsync(string input, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new InvalidOperationException($"A2A skill tool '{toolName}' requires non-empty input.");
            }

            var delegatedInput =
                $"Use A2A skill '{skill.Name}' from remote agent '{agentCard.Name}' for this request." +
                Environment.NewLine +
                Environment.NewLine +
                input.Trim();
            var response = await a2aAgent.RunAsync(
                delegatedInput,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return string.IsNullOrWhiteSpace(response.Text)
                ? $"Remote A2A skill '{skill.Name}' completed without text output."
                : response.Text.Trim();
        }
    }

    private static string CreateSkillDescription(
        AgentA2ARemoteEndpointSettings endpoint,
        A2AAgentCard agentCard,
        A2AAgentSkill skill)
    {
        return JsonSerializer.Serialize(new
        {
            remoteEndpoint = endpoint.EndpointId,
            remoteAgent = agentCard.Name,
            skill = skill.Name,
            description = skill.Description,
            tags = skill.Tags ?? [],
            examples = skill.Examples ?? [],
            inputModes = skill.InputModes ?? agentCard.DefaultInputModes ?? [],
            outputModes = skill.OutputModes ?? agentCard.DefaultOutputModes ?? []
        });
    }
}

internal sealed record A2ARemoteAgentToolBuildResult(
    IReadOnlyList<AITool> Tools,
    IReadOnlyList<IDisposable> Disposables);
