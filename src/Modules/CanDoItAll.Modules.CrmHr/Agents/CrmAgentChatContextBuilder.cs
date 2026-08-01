using System.Text;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.CrmHr;

public sealed record CrmAgentChatAccountContext
{
    public CrmAgentChatAccountContext(
        Guid accountId,
        string displayLabel,
        PartyLifecycleStatus lifecycleStatus,
        CrmAccountRelationshipStage relationshipStage,
        IEnumerable<PartyRoleKind> roles)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("A CRM account id is required.", nameof(accountId));
        }

        ArgumentNullException.ThrowIfNull(roles);
        AccountId = accountId;
        DisplayLabel = CrmAgentChatContextBuilder.NormalizeDisplayLabel(displayLabel);
        CrmAgentChatContextBuilder.ValidateDefined(lifecycleStatus, nameof(lifecycleStatus));
        CrmAgentChatContextBuilder.ValidateDefined(relationshipStage, nameof(relationshipStage));
        LifecycleStatus = lifecycleStatus;
        RelationshipStage = relationshipStage;
        var normalizedRoles = roles
            .Distinct()
            .OrderBy(role => role)
            .ToArray();
        if (normalizedRoles.Any(role => !Enum.IsDefined(role)))
        {
            throw new ArgumentOutOfRangeException(nameof(roles), "CRM account roles contain an undefined value.");
        }

        Roles = Array.AsReadOnly(normalizedRoles);
    }

    public Guid AccountId { get; }

    public string DisplayLabel { get; }

    public PartyLifecycleStatus LifecycleStatus { get; }

    public CrmAccountRelationshipStage RelationshipStage { get; }

    public IReadOnlyList<PartyRoleKind> Roles { get; }
}

public sealed record CrmAgentChatOpportunityContext
{
    public CrmAgentChatOpportunityContext(
        Guid accountId,
        Guid opportunityId,
        string displayLabel,
        OpportunityStage stage,
        OpportunitySource source,
        IEnumerable<OpportunityPartyRole> partyRoles)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("A CRM account id is required.", nameof(accountId));
        }

        if (opportunityId == Guid.Empty)
        {
            throw new ArgumentException("A CRM opportunity id is required.", nameof(opportunityId));
        }

        ArgumentNullException.ThrowIfNull(partyRoles);
        CrmAgentChatContextBuilder.ValidateDefined(stage, nameof(stage));
        CrmAgentChatContextBuilder.ValidateDefined(source, nameof(source));
        AccountId = accountId;
        OpportunityId = opportunityId;
        DisplayLabel = CrmAgentChatContextBuilder.NormalizeDisplayLabel(displayLabel);
        Stage = stage;
        Source = source;
        var normalizedRoles = partyRoles
            .Distinct()
            .OrderBy(role => role)
            .ToArray();
        if (normalizedRoles.Any(role => !Enum.IsDefined(role)))
        {
            throw new ArgumentOutOfRangeException(nameof(partyRoles), "CRM opportunity roles contain an undefined value.");
        }

        PartyRoles = Array.AsReadOnly(normalizedRoles);
    }

    public Guid AccountId { get; }

    public Guid OpportunityId { get; }

    public string DisplayLabel { get; }

    public OpportunityStage Stage { get; }

    public OpportunitySource Source { get; }

    public IReadOnlyList<OpportunityPartyRole> PartyRoles { get; }
}

public sealed record CrmAgentChatInteractionContext
{
    public CrmAgentChatInteractionContext(
        Guid accountId,
        Guid interactionId,
        string displayLabel,
        InteractionType interactionType,
        Guid? relatedOpportunityId)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("A CRM account id is required.", nameof(accountId));
        }

        if (interactionId == Guid.Empty)
        {
            throw new ArgumentException("A CRM interaction id is required.", nameof(interactionId));
        }

        if (relatedOpportunityId == Guid.Empty)
        {
            throw new ArgumentException(
                "A related CRM opportunity id cannot be empty.",
                nameof(relatedOpportunityId));
        }

        CrmAgentChatContextBuilder.ValidateDefined(interactionType, nameof(interactionType));
        AccountId = accountId;
        InteractionId = interactionId;
        DisplayLabel = CrmAgentChatContextBuilder.NormalizeDisplayLabel(displayLabel);
        InteractionType = interactionType;
        RelatedOpportunityId = relatedOpportunityId;
    }

    public Guid AccountId { get; }

    public Guid InteractionId { get; }

    public string DisplayLabel { get; }

    public InteractionType InteractionType { get; }

    public Guid? RelatedOpportunityId { get; }
}

public static class CrmAgentChatContextBuilder
{
    public const string SourceKind = "crm-account";
    public const string WorkspaceSourceKind = "crm-workspace";
    public const string WorkspaceSourceId = "crm";
    public const string WorkspaceContributorId = "crm.workspace";
    public const string AccountContributorId = "crm.account-selection";
    public const string OpportunityContributorId = "crm.opportunity-selection";
    public const string InteractionContributorId = "crm.interaction-selection";
    private const int MaximumDisplayLabelLength = 160;

    public static AgentChatContextSource BuildSource(Guid accountId)
    {
        ValidateAccountId(accountId);
        return new AgentChatContextSource(
            new AgentChatContextSourceKind(SourceKind),
            new AgentChatContextSourceId(accountId.ToString("D")));
    }

    public static AgentChatContextSource BuildWorkspaceSource()
    {
        return new AgentChatContextSource(
            new AgentChatContextSourceKind(WorkspaceSourceKind),
            new AgentChatContextSourceId(WorkspaceSourceId));
    }

    public static AgentChatContextScope BuildWorkspaceScope(
        AgentChatContextScopeId scopeId,
        AgentChatContextAccessState accessState = AgentChatContextAccessState.Ready)
    {
        ValidateAccessState(accessState);
        return new AgentChatContextScope(
            scopeId,
            BuildWorkspaceSource(),
            "CRM workspace",
            workspaceScope: null,
            agentAccess: [],
            accessMode: AgentChatContextScopeAccessMode.Unrestricted,
            accessState: accessState,
            surfacePosition: CrmHrAgentChatSurfaceBuilder.BuildCrmPosition(null, null, null));
    }

    public static AgentChatContextFragment BuildWorkspaceFragment(int accountCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(accountCount);
        var content = $"""
CRM workspace (sanitized)
Subview: CRM
AccountCount: {accountCount}
SelectedAccount: None
""".ReplaceLineEndings("\n");
        return new AgentChatContextFragment(
            new AgentChatContextContributorId(WorkspaceContributorId),
            order: 50,
            content);
    }

    public static AgentChatContextScope BuildScope(
        AgentChatContextScopeId scopeId,
        CrmAgentChatAccountContext account,
        CrmAgentChatOpportunityContext? opportunity,
        AgentChatContextAccessState accessState = AgentChatContextAccessState.Ready,
        CrmAgentChatInteractionContext? interaction = null)
    {
        ArgumentNullException.ThrowIfNull(account);
        ValidateAccessState(accessState);
        ValidateSelection(account, opportunity, interaction);

        // CRM has no canonical agent-access projection yet. Unrestricted applies only to this
        // bounded summary; it does not grant CRM, workspace, or tool authorization.
        return new AgentChatContextScope(
            scopeId,
            BuildSource(account.AccountId),
            $"CRM account · {account.DisplayLabel}",
            workspaceScope: null,
            agentAccess: [],
            accessMode: AgentChatContextScopeAccessMode.Unrestricted,
            accessState: accessState,
            surfacePosition: CrmHrAgentChatSurfaceBuilder.BuildCrmPosition(
                account,
                opportunity,
                interaction),
            completionRefreshMode: AgentChatContextCompletionRefreshMode.OnSuccessfulRun);
    }

    private static void ValidateAccessState(AgentChatContextAccessState accessState)
    {
        if (!Enum.IsDefined(accessState))
        {
            throw new ArgumentOutOfRangeException(
                nameof(accessState),
                accessState,
                "The CRM agent chat context access state is undefined.");
        }
    }

    public static AgentChatContextFragment BuildAccountFragment(
        CrmAgentChatAccountContext account)
    {
        ArgumentNullException.ThrowIfNull(account);
        var roles = account.Roles.Count == 0
            ? "None"
            : string.Join(", ", account.Roles);
        var content = $"""
CRM account selection (sanitized)
AccountId: {account.AccountId:D}
DisplayLabel: {account.DisplayLabel}
LifecycleStatus: {account.LifecycleStatus}
RelationshipStage: {account.RelationshipStage}
Roles: {roles}
""".ReplaceLineEndings("\n");
        return new AgentChatContextFragment(
            new AgentChatContextContributorId(AccountContributorId),
            order: 100,
            content);
    }

    public static AgentChatContextFragment BuildOpportunityFragment(
        CrmAgentChatOpportunityContext opportunity)
    {
        ArgumentNullException.ThrowIfNull(opportunity);
        var roles = opportunity.PartyRoles.Count == 0
            ? "None"
            : string.Join(", ", opportunity.PartyRoles);
        var content = $"""
CRM opportunity selection (sanitized)
AccountId: {opportunity.AccountId:D}
OpportunityId: {opportunity.OpportunityId:D}
DisplayLabel: {opportunity.DisplayLabel}
Stage: {opportunity.Stage}
Source: {opportunity.Source}
PartyRoles: {roles}
""".ReplaceLineEndings("\n");
        return new AgentChatContextFragment(
            new AgentChatContextContributorId(OpportunityContributorId),
            order: 200,
            content);
    }

    public static AgentChatContextFragment BuildInteractionFragment(
        CrmAgentChatInteractionContext interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);
        var relatedOpportunityId = interaction.RelatedOpportunityId?.ToString("D") ?? "None";
        var content = $"""
CRM interaction selection (sanitized)
AccountId: {interaction.AccountId:D}
InteractionId: {interaction.InteractionId:D}
DisplayLabel: {interaction.DisplayLabel}
InteractionType: {interaction.InteractionType}
RelatedOpportunityId: {relatedOpportunityId}
""".ReplaceLineEndings("\n");
        return new AgentChatContextFragment(
            new AgentChatContextContributorId(InteractionContributorId),
            order: 225,
            content);
    }

    public static void ValidateSelection(
        CrmAgentChatAccountContext account,
        CrmAgentChatOpportunityContext? opportunity,
        CrmAgentChatInteractionContext? interaction = null)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (opportunity is not null && opportunity.AccountId != account.AccountId)
        {
            throw new InvalidOperationException(
                $"CRM opportunity '{opportunity.OpportunityId:D}' does not belong to context account '{account.AccountId:D}'.");
        }

        if (interaction is not null && interaction.AccountId != account.AccountId)
        {
            throw new InvalidOperationException(
                $"CRM interaction '{interaction.InteractionId:D}' does not belong to context account '{account.AccountId:D}'.");
        }
    }

    internal static string NormalizeDisplayLabel(string displayLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayLabel);
        var builder = new StringBuilder(Math.Min(displayLabel.Length, MaximumDisplayLabelLength));
        var previousWasWhitespace = false;
        foreach (var character in displayLabel.Trim())
        {
            if (char.IsControl(character) || char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                previousWasWhitespace = true;
                continue;
            }

            if (builder.Length >= MaximumDisplayLabelLength)
            {
                break;
            }

            builder.Append(character);
            previousWasWhitespace = false;
        }

        var normalized = builder.ToString().Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("A CRM context display label is required.", nameof(displayLabel));
        }

        return normalized;
    }

    internal static void ValidateDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "The CRM context value is undefined.");
        }
    }

    private static void ValidateAccountId(Guid accountId)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("A CRM account id is required.", nameof(accountId));
        }
    }
}
