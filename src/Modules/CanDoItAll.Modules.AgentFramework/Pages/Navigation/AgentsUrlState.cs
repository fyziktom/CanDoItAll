using CanDoItAll.AgentFramework.Llm.SimpleChats.Components;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.AppComponents.Navigation;

namespace CanDoItAll.Modules.AgentFramework.Pages.Navigation;

public enum AgentsPageDialog {
    SharedConnections
}

public sealed record AgentsUrlState {
    public AgentWorkspaceSection Tab { get; init; } = AgentWorkspaceSection.Overview;
    public Guid? AgentId { get; init; }
    public AgentEditorSection AgentSection { get; init; } = AgentEditorSection.Identity;
    public Guid? TeamId { get; init; }
    public SimpleChatWorkspaceRouteState SimpleChat { get; init; } = SimpleChatWorkspaceRouteState.Default;
    public ProviderUsageWorkloadSelection UsageSelection { get; init; } = ProviderUsageWorkloadSelection.Both;
    public ProviderUsagePeriod UsagePeriod { get; init; } = ProviderUsagePeriod.SevenDays;
    public Guid? ProviderId { get; init; }
    public ProviderEditorSection ProviderSection { get; init; } = ProviderEditorSection.Connection;
    public AgentsPageDialog? Dialog { get; init; }
    public AgentsHistoryUrlState? History { get; init; }

    public Guid? OpenAgentId => Tab == AgentWorkspaceSection.Agents ? AgentId : null;
    public bool IsSharedConnectionsOpen => Tab == AgentWorkspaceSection.Providers && Dialog == AgentsPageDialog.SharedConnections;
}

public sealed class AgentsUrlCodec : IUrlStateCodec<AgentsUrlState> {
    public const string Path = "/agents";
    public const string TabKey = "tab";
    public const string AgentKey = "agentId";
    public const string AgentSectionKey = "agentSection";
    public const string TeamKey = "teamId";
    public const string SimpleChatViewKey = "simpleChatView";
    public const string DefinitionKey = "definitionId";
    public const string ConversationKey = "conversationId";
    public const string UsageScopeKey = ProviderUsagePeriods.ScopeQueryKey;
    public const string UsagePeriodKey = ProviderUsagePeriods.QueryKey;
    public const string ProviderKey = "providerId";
    public const string ProviderSectionKey = "providerSection";
    public const string DialogKey = "dialog";

    public static AgentsUrlCodec Instance { get; } = new();

    public static UrlTokenMap<AgentWorkspaceSection> Tabs { get; } = new(
        (AgentWorkspaceSection.Overview, AgentWorkspaceTabs.Overview),
        (AgentWorkspaceSection.Agents, AgentWorkspaceTabs.Agents),
        (AgentWorkspaceSection.SimpleChats, AgentWorkspaceTabs.SimpleChats),
        (AgentWorkspaceSection.Providers, AgentWorkspaceTabs.Providers),
        (AgentWorkspaceSection.RequestHistory, AgentWorkspaceTabs.RequestHistory),
        (AgentWorkspaceSection.Voice, AgentWorkspaceTabs.Voice),
        (AgentWorkspaceSection.FloatingChat, AgentWorkspaceTabs.FloatingChat),
        (AgentWorkspaceSection.Chat, AgentWorkspaceTabs.Chat),
        (AgentWorkspaceSection.Capabilities, AgentWorkspaceTabs.Capabilities),
        (AgentWorkspaceSection.Governance, AgentWorkspaceTabs.Governance),
        (AgentWorkspaceSection.Diagnostics, AgentWorkspaceTabs.Diagnostics));

    public static UrlTokenMap<AgentEditorSection> AgentSections { get; } = new(
        (AgentEditorSection.Identity, "identity"),
        (AgentEditorSection.Runtime, "runtime"),
        (AgentEditorSection.Memory, "memory"),
        (AgentEditorSection.Images, "images"),
        (AgentEditorSection.ProjectStructureAccess, "project-structure"),
        (AgentEditorSection.WorkspaceTools, "workspace-tools"),
        (AgentEditorSection.Secrets, "secrets"),
        (AgentEditorSection.ProcessAccess, "process-access"),
        (AgentEditorSection.Capabilities, "capabilities"),
        (AgentEditorSection.Voice, "voice"));

    public static UrlTokenMap<ProviderEditorSection> ProviderSections { get; } = new(
        ProviderEditorSections.All.Select(definition => (definition.Section, ProviderEditorSections.Token(definition.Section))).ToArray());

    public static UrlTokenMap<SimpleChatWorkspaceView> SimpleChatViews { get; } = new(
        (SimpleChatWorkspaceView.Definitions, "definitions"),
        (SimpleChatWorkspaceView.Conversations, "conversations"));

    public static UrlTokenMap<ProviderUsageWorkloadSelection> UsageScopes { get; } = new(
        (ProviderUsageWorkloadSelection.Agents, "agents"),
        (ProviderUsageWorkloadSelection.SimpleChats, "simple-chats"),
        (ProviderUsageWorkloadSelection.Both, "both"));

    public static UrlTokenMap<ProviderUsagePeriod> UsagePeriods { get; } = new(
        Enum.GetValues<ProviderUsagePeriod>().Select(period => (period, period.ToWireValue())).ToArray());

    public static UrlTokenMap<AgentsPageDialog> Dialogs { get; } = new((AgentsPageDialog.SharedConnections, "shared-connections"));

    public IReadOnlyList<string> Keys { get; } = [
        TabKey, AgentKey, AgentSectionKey, TeamKey, SimpleChatViewKey, DefinitionKey, ConversationKey,
        UsageScopeKey, UsagePeriodKey, ProviderKey, ProviderSectionKey, DialogKey, .. AgentsHistoryUrl.Keys
    ];

    public static bool UsesAgentSelection(AgentWorkspaceSection tab) => tab.UsesAgentSelection();

    public AgentsUrlState Read(UrlQuery query) {
        ArgumentNullException.ThrowIfNull(query);
        var tab = query.GetToken(TabKey, Tabs, AgentWorkspaceSection.Overview);
        var agentId = UsesAgentSelection(tab) ? query.GetId(AgentKey) : null;
        var providerId = tab == AgentWorkspaceSection.Providers ? query.GetId(ProviderKey) : null;
        var providerSection = providerId is null
            ? ProviderEditorSection.Connection
            : query.GetToken(ProviderSectionKey, ProviderSections, ProviderEditorSection.Connection);
        return new() {
            Tab = tab,
            AgentId = agentId,
            AgentSection = tab == AgentWorkspaceSection.Agents && agentId is not null
                ? query.GetToken(AgentSectionKey, AgentSections, AgentEditorSection.Identity)
                : AgentEditorSection.Identity,
            TeamId = tab == AgentWorkspaceSection.Agents ? query.GetId(TeamKey) : null,
            SimpleChat = tab == AgentWorkspaceSection.SimpleChats ? ReadSimpleChat(query) : SimpleChatWorkspaceRouteState.Default,
            UsageSelection = tab == AgentWorkspaceSection.Overview
                ? query.GetToken(UsageScopeKey, UsageScopes, ProviderUsageWorkloadSelection.Both)
                : ProviderUsageWorkloadSelection.Both,
            UsagePeriod = tab == AgentWorkspaceSection.Overview
                ? query.GetToken(UsagePeriodKey, UsagePeriods, ProviderUsagePeriod.SevenDays)
                : ProviderUsagePeriod.SevenDays,
            ProviderId = providerId,
            ProviderSection = providerSection,
            Dialog = tab == AgentWorkspaceSection.Providers ? query.GetToken(DialogKey, Dialogs) : null,
            History = tab switch {
                AgentWorkspaceSection.RequestHistory => AgentsHistoryUrl.Read(query, ProviderKey, allowProvider: true),
                AgentWorkspaceSection.Providers when providerId is not null && providerSection == ProviderEditorSection.History
                    => AgentsHistoryUrl.Read(query, ProviderKey, allowProvider: false),
                _ => null
            }
        };
    }

    public void Write(AgentsUrlState state, UrlQueryWriter query) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(query);
        query.Token(TabKey, state.Tab, Tabs, AgentWorkspaceSection.Overview);
        if (UsesAgentSelection(state.Tab)) {
            query.Id(AgentKey, state.AgentId);
        }
        if (state.Tab == AgentWorkspaceSection.Agents && state.AgentId is not null) {
            query.Token(AgentSectionKey, state.AgentSection, AgentSections, AgentEditorSection.Identity);
        }
        if (state.Tab == AgentWorkspaceSection.Agents) {
            query.Id(TeamKey, state.TeamId);
        }
        if (state.Tab == AgentWorkspaceSection.SimpleChats) {
            WriteSimpleChat(state.SimpleChat, query);
        }
        if (state.Tab == AgentWorkspaceSection.Overview) {
            query.Token(UsageScopeKey, state.UsageSelection, UsageScopes, ProviderUsageWorkloadSelection.Both);
            query.Token(UsagePeriodKey, state.UsagePeriod, UsagePeriods, ProviderUsagePeriod.SevenDays);
        }
        if (state.Tab == AgentWorkspaceSection.Providers) {
            query.Id(ProviderKey, state.ProviderId);
            if (state.ProviderId is not null) {
                query.Token(ProviderSectionKey, state.ProviderSection, ProviderSections, ProviderEditorSection.Connection);
            }
            query.Token(DialogKey, state.Dialog, Dialogs);
            if (state.ProviderId is not null && state.ProviderSection == ProviderEditorSection.History) {
                AgentsHistoryUrl.Write(state.History, query, ProviderKey, allowProvider: false);
            }
        }
        if (state.Tab == AgentWorkspaceSection.RequestHistory) {
            AgentsHistoryUrl.Write(state.History, query, ProviderKey, allowProvider: true);
        }
    }

    public static string BuildChatsCompatibilityRedirect(Uri legacyUri) {
        ArgumentNullException.ThrowIfNull(legacyUri);
        var query = UrlQuery.Parse(legacyUri.Query);
        return Instance.Href(Path, new AgentsUrlState {
            Tab = AgentWorkspaceSection.SimpleChats,
            SimpleChat = ReadSimpleChat(query)
        });
    }

    private static SimpleChatWorkspaceRouteState ReadSimpleChat(UrlQuery query) {
        var definitionId = query.GetId(DefinitionKey);
        var conversationId = query.GetId(ConversationKey);
        var view = query.GetToken(SimpleChatViewKey, SimpleChatViews)
            ?? (conversationId is not null && definitionId is null ? SimpleChatWorkspaceView.Conversations : SimpleChatWorkspaceView.Definitions);
        return view == SimpleChatWorkspaceView.Conversations
            ? new(view, null, conversationId)
            : new(view, definitionId, null);
    }

    private static void WriteSimpleChat(SimpleChatWorkspaceRouteState state, UrlQueryWriter query) {
        if (state.View != SimpleChatWorkspaceView.Definitions || state.DefinitionId is not null) {
            query.Token(SimpleChatViewKey, (SimpleChatWorkspaceView?)state.View, SimpleChatViews);
        }
        if (state.View == SimpleChatWorkspaceView.Definitions) {
            query.Id(DefinitionKey, state.DefinitionId);
        } else {
            query.Id(ConversationKey, state.ConversationId);
        }
    }
}
