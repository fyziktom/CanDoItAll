using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class AgentConfigurationVersion
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string Create(AgentDefinition agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        var material = new
        {
            agent.Name,
            agent.RoleTitle,
            agent.Summary,
            agent.Instructions,
            agent.AvatarImageUrl,
            agent.Status,
            agent.ProviderProfileId,
            agent.Model,
            agent.Workload,
            agent.ChatHistoryMode,
            agent.Temperature,
            agent.RequirePerServiceCallChatHistoryPersistence,
            agent.EnableBackgroundResponses,
            agent.ConfigurationJson,
            agent.IsTemplate,
            agent.TemplateKey,
            agent.Permissions,
            Capabilities = agent.Capabilities
                .OrderBy(item => item.CapabilityId)
                .ToList(),
            Tags = agent.Tags.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList()
        };
        return Convert.ToHexString(SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(material, SerializerOptions)));
    }
}
