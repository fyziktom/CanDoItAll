using CanDoItAll.AgentFramework.Llm.SimpleChats.Components;
using CanDoItAll.AgentFramework.Usage;
using Microsoft.AspNetCore.WebUtilities;

namespace CanDoItAll.Modules.AgentFramework.Pages;

public sealed record AgentWorkspaceRouteState(
    string Tab,
    Guid? AgentId,
    Guid? TeamId,
    SimpleChatWorkspaceRouteState SimpleChat,
    ProviderUsageWorkloadSelection UsageSelection)
{
    public const string SimpleChatViewQueryKey = "simpleChatView";
    public const string DefinitionIdQueryKey = "definitionId";
    public const string ConversationIdQueryKey = "conversationId";
    public const string UsageScopeQueryKey = "usageScope";

    public static AgentWorkspaceRouteState Parse(
        string? tab,
        Guid? agentId,
        Guid? teamId,
        string? simpleChatView,
        string? definitionId,
        string? conversationId,
        string? usageScope)
    {
        var resolvedTab = !string.IsNullOrWhiteSpace(tab) && AgentWorkspaceTabs.All.Contains(tab)
            ? tab
            : AgentWorkspaceTabs.Overview;
        var parsedDefinitionId = ParseId(definitionId);
        var parsedConversationId = ParseId(conversationId);
        var view = ParseSimpleChatView(simpleChatView, parsedDefinitionId, parsedConversationId);
        SimpleChatWorkspaceRouteState simpleChat = view switch
        {
            SimpleChatWorkspaceView.Conversations => new(view, null, parsedConversationId),
            SimpleChatWorkspaceView.Definitions => new(view, parsedDefinitionId, null),
            _ => throw new ArgumentOutOfRangeException(nameof(simpleChatView), simpleChatView, "Unknown Simple Chat view.")
        };

        return new(
            resolvedTab,
            agentId,
            resolvedTab == AgentWorkspaceTabs.Agents ? teamId : null,
            simpleChat,
            ParseUsageSelection(usageScope));
    }

    public static string Build(AgentWorkspaceRouteState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!AgentWorkspaceTabs.All.Contains(state.Tab))
        {
            throw new ArgumentOutOfRangeException(nameof(state), state.Tab, "Unknown Agent workspace tab.");
        }

        var query = new List<KeyValuePair<string, string>>();
        if (state.Tab != AgentWorkspaceTabs.Overview)
        {
            query.Add(new("tab", state.Tab));
        }

        if (state.AgentId is { } agentId)
        {
            query.Add(new("agentId", agentId.ToString("D")));
        }

        if (state.Tab == AgentWorkspaceTabs.Agents && state.TeamId is { } teamId)
        {
            query.Add(new("teamId", teamId.ToString("D")));
        }

        if (state.Tab == AgentWorkspaceTabs.SimpleChats)
        {
            if (state.SimpleChat.View != SimpleChatWorkspaceView.Definitions ||
                state.SimpleChat.DefinitionId.HasValue)
            {
                query.Add(new(SimpleChatViewQueryKey, FormatSimpleChatView(state.SimpleChat.View)));
            }

            if (state.SimpleChat.DefinitionId is { } definitionId)
            {
                query.Add(new(DefinitionIdQueryKey, definitionId.ToString("D")));
            }

            if (state.SimpleChat.ConversationId is { } conversationId)
            {
                query.Add(new(ConversationIdQueryKey, conversationId.ToString("D")));
            }
        }

        if (state.Tab == AgentWorkspaceTabs.Overview && state.UsageSelection != ProviderUsageWorkloadSelection.Both)
        {
            query.Add(new(UsageScopeQueryKey, FormatUsageSelection(state.UsageSelection)));
        }

        return query.Count == 0
            ? "/agents"
            : $"/agents?{string.Join('&', query.Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value)}"))}";
    }

    public static string BuildCompatibilityRedirect(Uri legacyUri)
    {
        ArgumentNullException.ThrowIfNull(legacyUri);
        var query = QueryHelpers.ParseQuery(legacyUri.Query);
        var state = Parse(
            AgentWorkspaceTabs.SimpleChats,
            null,
            null,
            Read(query, SimpleChatViewQueryKey),
            Read(query, DefinitionIdQueryKey),
            Read(query, ConversationIdQueryKey),
            null);
        return Build(state);
    }

    public static ProviderUsageWorkloadSelection ParseUsageSelection(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            "agents" => ProviderUsageWorkloadSelection.Agents,
            "simple-chats" => ProviderUsageWorkloadSelection.SimpleChats,
            "both" or null or "" => ProviderUsageWorkloadSelection.Both,
            _ => ProviderUsageWorkloadSelection.Both
        };

    private static SimpleChatWorkspaceView ParseSimpleChatView(
        string? value,
        Guid? definitionId,
        Guid? conversationId)
        => value?.Trim().ToLowerInvariant() switch
        {
            "conversations" => SimpleChatWorkspaceView.Conversations,
            "definitions" => SimpleChatWorkspaceView.Definitions,
            null or "" when conversationId.HasValue && !definitionId.HasValue => SimpleChatWorkspaceView.Conversations,
            _ => SimpleChatWorkspaceView.Definitions
        };

    private static Guid? ParseId(string? value)
        => Guid.TryParse(value, out var id) && id != Guid.Empty ? id : null;

    private static string FormatSimpleChatView(SimpleChatWorkspaceView view)
        => view switch
        {
            SimpleChatWorkspaceView.Conversations => "conversations",
            SimpleChatWorkspaceView.Definitions => "definitions",
            _ => throw new ArgumentOutOfRangeException(nameof(view), view, "Unknown Simple Chat view.")
        };

    private static string FormatUsageSelection(ProviderUsageWorkloadSelection selection)
        => selection switch
        {
            ProviderUsageWorkloadSelection.Agents => "agents",
            ProviderUsageWorkloadSelection.SimpleChats => "simple-chats",
            ProviderUsageWorkloadSelection.Both => "both",
            _ => throw new ArgumentOutOfRangeException(nameof(selection), selection, "Unknown usage scope.")
        };

    private static string? Read(
        IDictionary<string, Microsoft.Extensions.Primitives.StringValues> query,
        string key)
        => query.TryGetValue(key, out var value) ? value.ToString() : null;
}
