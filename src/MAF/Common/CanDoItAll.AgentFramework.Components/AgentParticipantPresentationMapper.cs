using CanDoItAll.Conversations.Components.Presentation;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Components;

public static class AgentParticipantPresentationMapper
{
    internal static ConversationParticipantPresentation MapCard(
        AgentDefinition agent,
        AgentParticipantCardProjectionOptions options)
    {
        var visibleTags = VisibleTags(agent);
        var badges = BuildCardBadges(agent, options);
        var metadata = BuildCardMetadata(agent, options);
        var displayName = string.IsNullOrWhiteSpace(agent.Name) ? "Agent" : agent.Name;
        var role = ResolveRole(agent);
        var summary = options.ShowSummary ? ResolveSummary(agent) : null;
        var isFavorite = options.IsFavorite;

        return new(
            key: AgentKey(agent.Id),
            displayName: displayName,
            subtitle: role,
            summary: summary,
            avatarImageUrl: agent.AvatarImageUrl,
            avatarSeed: ResolveAvatarSeed(agent, "Sandbox Agent"),
            avatarFallbackText: BuildInitials(ResolveAvatarSeed(agent, "Sandbox Agent")),
            searchText: string.Join(' ', agent.Name, agent.RoleTitle, agent.Summary, agent.Model),
            detailsText: options.ShowDetailsTooltip ? BuildCardDetails(agent, visibleTags, role) : null,
            detailsAriaLabel: "Show agent details",
            selectLabel: string.IsNullOrWhiteSpace(agent.Name) ? "Select agent" : $"Select {agent.Name}",
            badges: badges,
            tags: options.ShowTags ? visibleTags : [],
            metadata: metadata,
            ribbon: options.ShowStatusRibbon
                ? new(agent.Status.ToString(), ResolveStatusTone(agent.Status), testId: "agent-status-ribbon")
                : null,
            favorite: options.ShowFavorite
                ? new(
                    isFavorite,
                    options.FavoriteDisabled,
                    "Mark favorite",
                    "Remove favorite",
                    options.FavoriteTestId)
                : null,
            isSelected: options.IsSelected,
            isFavorite: isFavorite);
    }

    public static ConversationParticipantCompactItemPresentation MapCompactItem(
        AgentDefinition agent,
        bool isSelected,
        bool isBusy,
        string? shellTestId,
        string? selectTestId,
        IReadOnlyList<ParticipantActionPresentation> actions,
        ConversationPresentationKey? key = null,
        ProviderProfile? provider = null) {
        var displayName = string.IsNullOrWhiteSpace(agent.Name) ? "Agent" : agent.Name.Trim();
        var role = ResolveRole(agent);
        var model = string.IsNullOrWhiteSpace(agent.Model)
            ? "No model configured"
            : provider?.GetModelDisplayName(agent.Model) ?? agent.Model.Trim();
        var workload = AgentWorkloadDisplay.ResolveLabel(agent.Workload);
        var tags = VisibleTags(agent);
        var participant = new ConversationParticipantPresentation(
            key: key ?? AgentKey(agent.Id),
            displayName: displayName,
            subtitle: role,
            avatarImageUrl: agent.AvatarImageUrl,
            avatarSeed: ResolveAvatarSeed(agent, "Agent"),
            avatarFallbackText: BuildInitials(ResolveAvatarSeed(agent, "Agent")),
            searchText: string.Join(' ', agent.Name, agent.RoleTitle, agent.Summary, model),
            detailsText: $"{role}. {model}. {workload}. {(tags.Count == 0 ? "No visible tags" : $"Tags: {string.Join(", ", tags)}")}.",
            badges: [new(isBusy ? "Preparing" : workload, isBusy ? PresentationTone.Default : PresentationTone.Info)],
            tags: tags,
            metadata: [new(model)],
            isSelected: isSelected,
            isBusy: isBusy);

        return new(participant, actions, shellTestId, selectTestId);
    }

    public static ConversationPresentationKey AgentKey(Guid agentId)
        => new(agentId.ToString("N"));

    public static bool IsFavorite(AgentDefinition agent)
        => agent.Tags.Any(AgentSpecialTags.IsFavorite);

    public static IReadOnlyList<string> VisibleTags(AgentDefinition agent)
        => agent.Tags
            .Where(item => !AgentSpecialTags.IsFavorite(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static IReadOnlyList<PresentationBadge> BuildCardBadges(
        AgentDefinition agent,
        AgentParticipantCardProjectionOptions options)
    {
        var badges = new List<PresentationBadge>();
        if (options.ShowKindBadge)
        {
            badges.Add(new(options.KindLabel, PresentationTone.Info));
        }

        if (!options.ShowStatusRibbon)
        {
            badges.Add(new(agent.Status.ToString(), ResolveStatusTone(agent.Status)));
        }

        if (options.ShowWorkload)
        {
            badges.Add(new(AgentWorkloadDisplay.ResolveLabel(agent.Workload), PresentationTone.Info));
        }

        if (options.IsPrivateProvider)
        {
            badges.Add(new("Private", PresentationTone.Warning, testId: "agent-private-provider-badge"));
        }

        if (options.ShowChatHistory)
        {
            badges.Add(new(agent.ChatHistoryMode.ToString()));
        }

        if (options.IsSelected)
        {
            badges.Add(new(options.SelectedLabel, PresentationTone.Info));
        }

        return badges;
    }

    private static IReadOnlyList<PresentationMetaItem> BuildCardMetadata(
        AgentDefinition agent,
        AgentParticipantCardProjectionOptions options)
    {
        var metadata = new List<PresentationMetaItem>();
        if (!string.IsNullOrWhiteSpace(agent.Model))
        {
            metadata.Add(new(agent.Model));
        }

        if (options.ShowCapabilityCount)
        {
            metadata.Add(new(FormatCapabilityCount(agent)));
        }

        if (options.ShowUpdatedAt)
        {
            metadata.Add(new($"Updated {agent.UpdatedAtUtc.LocalDateTime:g}"));
        }

        return metadata;
    }

    private static string BuildCardDetails(
        AgentDefinition agent,
        IReadOnlyList<string> visibleTags,
        string role)
    {
        var provider = string.IsNullOrWhiteSpace(agent.Model) ? "No model configured" : agent.Model;
        var tagText = visibleTags.Count == 0
            ? "No visible tags."
            : $"Tags: {string.Join(", ", visibleTags)}.";

        return $"{role}. {provider}. {agent.Workload}. {FormatCapabilityCount(agent)}. {tagText} {ResolveSummary(agent)}";
    }

    private static string ResolveRole(AgentDefinition agent)
        => string.IsNullOrWhiteSpace(agent.RoleTitle) ? "Technical agent" : agent.RoleTitle.Trim();

    private static string ResolveSummary(AgentDefinition agent)
        => string.IsNullOrWhiteSpace(agent.Summary) ? "No short summary is configured yet." : agent.Summary.Trim();

    private static string ResolveAvatarSeed(AgentDefinition agent, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(agent.Name))
        {
            return agent.Name;
        }

        return string.IsNullOrWhiteSpace(agent.RoleTitle) ? fallback : agent.RoleTitle;
    }

    private static string FormatCapabilityCount(AgentDefinition agent)
        => agent.Capabilities.Count == 1 ? "1 capability" : $"{agent.Capabilities.Count} capabilities";

    private static string BuildInitials(string value)
    {
        var segments = value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .ToList();
        return segments.Count == 0
            ? "AG"
            : string.Concat(segments.Select(segment => char.ToUpperInvariant(segment[0])));
    }

    private static PresentationTone ResolveStatusTone(AgentLifecycleStatus status)
    {
        return status switch
        {
            AgentLifecycleStatus.Active => PresentationTone.Success,
            AgentLifecycleStatus.Suspended => PresentationTone.Warning,
            AgentLifecycleStatus.Archived => PresentationTone.Default,
            _ => PresentationTone.Info
        };
    }
}

internal sealed record AgentParticipantCardProjectionOptions(
    bool IsSelected,
    bool ShowStatusRibbon,
    bool ShowKindBadge,
    string KindLabel,
    bool ShowSummary,
    bool ShowWorkload,
    bool ShowChatHistory,
    bool ShowCapabilityCount,
    bool ShowUpdatedAt,
    bool ShowTags,
    bool ShowFavorite,
    bool IsFavorite,
    bool FavoriteDisabled,
    string FavoriteTestId,
    bool ShowDetailsTooltip,
    bool IsPrivateProvider,
    string SelectedLabel);
