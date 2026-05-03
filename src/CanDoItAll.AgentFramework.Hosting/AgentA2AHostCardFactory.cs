using A2A;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using A2AAgentSkill = A2A.AgentSkill;

namespace CanDoItAll.AgentFramework.Hosting;

public interface IAgentA2AHostCardFactory
{
    AgentCard CreateAgentCard(AgentDefinition agent);
}

public sealed class AgentA2AHostCardFactory : IAgentA2AHostCardFactory
{
    public AgentCard CreateAgentCard(AgentDefinition agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        var settings = AgentA2AMetadata.Read(agent.ConfigurationJson);
        var validation = AgentA2AMetadata.Validate(settings);
        if (!validation.Succeeded)
        {
            throw new InvalidOperationException("Agent A2A hosting configuration is invalid: " + string.Join(" ", validation.Errors));
        }

        var hosting = settings.Hosting;
        if (!hosting.Enabled)
        {
            throw new InvalidOperationException($"Agent '{agent.Name}' is not configured for A2A hosting.");
        }

        if (string.IsNullOrWhiteSpace(hosting.PublicBaseUri))
        {
            throw new InvalidOperationException($"Agent '{agent.Name}' is configured for A2A hosting but does not define publicBaseUri.");
        }

        var skillName = string.IsNullOrWhiteSpace(hosting.SkillName)
            ? AgentA2AMetadata.NormalizeToolNamePrefix(agent.Name)
            : AgentA2AMetadata.NormalizeToolNamePrefix(hosting.SkillName);
        var skillDescription = string.IsNullOrWhiteSpace(hosting.SkillDescription)
            ? agent.Summary
            : hosting.SkillDescription;

        return new AgentCard
        {
            Name = agent.Name,
            Description = agent.Summary,
            Version = hosting.Version,
            DefaultInputModes = ["text"],
            DefaultOutputModes = ["text"],
            Capabilities = new AgentCapabilities
            {
                Streaming = agent.EnableBackgroundResponses,
                PushNotifications = false
            },
            Skills =
            [
                new A2AAgentSkill
                {
                    Id = agent.Id.ToString("N"),
                    Name = skillName,
                    Description = skillDescription,
                    Tags = hosting.Tags.Count == 0 ? agent.Tags.ToList() : hosting.Tags.ToList(),
                    Examples =
                    [
                        $"Ask {agent.Name} to perform a task aligned with its role: {agent.RoleTitle}."
                    ]
                }
            ],
            SupportedInterfaces = CreateSupportedInterfaces(hosting)
        };
    }

    private static List<AgentInterface> CreateSupportedInterfaces(AgentA2AHostingSettings hosting)
    {
        var endpointUri = CreateEndpointUri(hosting.PublicBaseUri, hosting.PathPrefix);
        var protocolBindings = hosting.ProtocolBindings.Count == 0
            ? [AgentA2AProtocolBindingPreference.HttpJson, AgentA2AProtocolBindingPreference.JsonRpc]
            : hosting.ProtocolBindings;

        return protocolBindings
            .Where(binding => binding != AgentA2AProtocolBindingPreference.Auto)
            .Distinct()
            .Select(binding => new AgentInterface
            {
                Url = endpointUri,
                ProtocolBinding = MapProtocolBinding(binding),
                ProtocolVersion = "1.0"
            })
            .ToList();
    }

    private static string CreateEndpointUri(string publicBaseUri, string pathPrefix)
    {
        var baseUri = publicBaseUri.Trim().TrimEnd('/');
        var path = pathPrefix.Trim();
        if (!path.StartsWith("/", StringComparison.Ordinal))
        {
            path = "/" + path;
        }

        return baseUri + path;
    }

    private static string MapProtocolBinding(AgentA2AProtocolBindingPreference binding)
    {
        return binding switch
        {
            AgentA2AProtocolBindingPreference.JsonRpc => ProtocolBindingNames.JsonRpc,
            _ => ProtocolBindingNames.HttpJson
        };
    }
}

public static class AgentFrameworkA2AHostingServiceCollectionExtensions
{
    public static IServiceCollection AddAgentFrameworkA2AHosting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IAgentA2AHostCardFactory, AgentA2AHostCardFactory>();
        return services;
    }

    public static IServiceCollection AddAgentFrameworkA2AServer(
        this IServiceCollection services,
        AIAgent agent)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(agent);

        services.AddA2AServer(agent);
        return services.AddAgentFrameworkA2AHosting();
    }
}
