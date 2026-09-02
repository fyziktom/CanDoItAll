using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal static class MafChatClientAgentOptionsFactory
{
    public static ChatClientAgentOptions Create(ChatOptions chatOptions)
    {
        ArgumentNullException.ThrowIfNull(chatOptions);

        return new ChatClientAgentOptions
        {
            ChatOptions = chatOptions,
            UseProvidedChatClientAsIs = false,
            AllowConcurrentInvocation = false,
            DisableApprovalNotRequiredFunctionBypassing = true,
            DisableApprovalResponseBinding = false
        };
    }

    public static bool ResolvePerServiceCallHistoryPersistence(
        bool configured,
        bool frameworkManagedHistory,
        bool hasApprovalTools)
    {
        return configured || frameworkManagedHistory && hasApprovalTools;
    }
}
