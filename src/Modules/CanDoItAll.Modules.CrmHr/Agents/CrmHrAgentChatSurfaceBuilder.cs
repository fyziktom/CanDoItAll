using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.CrmHr;

public static class CrmHrAgentChatSurfaceBuilder
{
    public const string Module = "crm-hr";
    public const string SourceKind = "crm-hr";
    public const string HomeRoute = "/crm-hr";
    public const string DirectoryRoute = "/crm-hr/directory";
    public const string CrmRoute = "/crm-hr/crm";
    public const string WorkforceRoute = "/crm-hr/workforce";
    public const string RecruitingRoute = "/crm-hr/recruiting";
    public const string AssignmentsRoute = "/crm-hr/assignments";
    public const string AgentsRoute = "/crm-hr/agents";

    public static AgentChatContextSurface BuildHomeSurface()
        => BuildSurface(
            surface: "home",
            view: "overview",
            route: HomeRoute,
            displayName: "CRM / HR");

    public static AgentChatContextSurface BuildDirectorySurface(
        Guid? partyId = null,
        string? displayName = null,
        PartyLifecycleStatus? lifecycleStatus = null)
    {
        var selection = BuildSelection("party", partyId, displayName, nameof(partyId));
        return BuildSurface(
            surface: "directory",
            view: "party",
            route: DirectoryRoute,
            displayName: "CRM / HR · Directory",
            primarySelection: selection,
            facts: BuildFacts(selection, "lifecycle-status", lifecycleStatus));
    }

    public static AgentChatContextSurface BuildWorkforceSurface(
        Guid? partyId = null,
        string? displayName = null,
        PartyLifecycleStatus? lifecycleStatus = null,
        WorkforceAvailabilityState? availabilityStatus = null,
        bool hasWorkforceProfile = true)
    {
        var selection = BuildSelection("workforce-party", partyId, displayName, nameof(partyId));
        return BuildSurface(
            surface: "workforce",
            view: "profile",
            route: WorkforceRoute,
            displayName: "CRM / HR · Workforce",
            primarySelection: selection,
            facts: BuildWorkforceFacts(
                selection,
                lifecycleStatus,
                availabilityStatus,
                hasWorkforceProfile));
    }

    public static AgentChatContextSurface BuildRecruitingSurface(
        Guid? applicationId = null,
        RecruitmentStage? stage = null,
        RecruitmentDecision? decision = null,
        Guid? partyId = null,
        string? partyDisplayName = null)
    {
        var selection = BuildSelection(
            "recruitment-application",
            applicationId,
            applicationId.HasValue ? "Selected recruitment application" : null,
            nameof(applicationId));
        var selectedEntities = partyId.HasValue
            ?
            [
                new AgentChatContextEntityReference(
                    "candidate-party",
                    partyId.Value.ToString("D"),
                    string.IsNullOrWhiteSpace(partyDisplayName)
                        ? "Selected candidate"
                        : partyDisplayName)
            ]
            : Array.Empty<AgentChatContextEntityReference>();
        return BuildSurface(
            surface: "recruiting",
            view: "application",
            route: RecruitingRoute,
            displayName: "CRM / HR · Recruiting",
            primarySelection: selection,
            selectedEntities: selectedEntities,
            facts: BuildFacts(
                selection,
                "recruitment-stage",
                stage,
                "decision-status",
                decision));
    }

    public static AgentChatContextSurface BuildAssignmentsSurface(
        Guid? projectId = null,
        string? projectName = null,
        ProjectStatus? projectStatus = null)
    {
        var selection = BuildSelection("project", projectId, projectName, nameof(projectId));
        return BuildSurface(
            surface: "assignments",
            view: "project",
            route: AssignmentsRoute,
            displayName: "CRM / HR · Assignments",
            primarySelection: selection,
            facts: BuildFacts(selection, "project-status", projectStatus));
    }

    public static AgentChatContextSurface BuildAgentsSurface(
        Guid? partyId = null,
        string? displayName = null,
        PartyLifecycleStatus? lifecycleStatus = null,
        AiResourceBindingStatus? bindingStatus = null,
        AiValidationStatus? validationStatus = null)
    {
        var selection = BuildSelection("agent-party", partyId, displayName, nameof(partyId));
        return BuildSurface(
            surface: "agents",
            view: "agent",
            route: AgentsRoute,
            displayName: "CRM / HR · Agents",
            primarySelection: selection,
            facts: BuildFacts(
                selection,
                "lifecycle-status",
                lifecycleStatus,
                "binding-status",
                bindingStatus,
                "validation-status",
                validationStatus));
    }

    public static AgentChatSurfacePosition BuildCrmPosition(
        CrmAgentChatAccountContext? account,
        CrmAgentChatOpportunityContext? opportunity,
        CrmAgentChatInteractionContext? interaction = null)
    {
        if (account is null)
        {
            if (opportunity is not null || interaction is not null)
            {
                throw new ArgumentException(
                    "A CRM entity position requires an account selection.",
                    opportunity is not null ? nameof(opportunity) : nameof(interaction));
            }

            return new AgentChatSurfacePosition(Module, "crm", "accounts", CrmRoute);
        }

        CrmAgentChatContextBuilder.ValidateSelection(account, opportunity, interaction);
        var primarySelection = new AgentChatContextEntityReference(
            "crm-account",
            account.AccountId.ToString("D"),
            account.DisplayLabel);
        var selectedEntities = new List<AgentChatContextEntityReference>(2);
        var facts = new List<AgentChatContextPositionFact>
        {
            new("lifecycle-status", account.LifecycleStatus.ToString()),
            new("relationship-stage", account.RelationshipStage.ToString())
        };
        if (opportunity is not null)
        {
            selectedEntities.Add(new AgentChatContextEntityReference(
                "crm-opportunity",
                opportunity.OpportunityId.ToString("D"),
                opportunity.DisplayLabel));
            facts.Add(new AgentChatContextPositionFact(
                "opportunity-stage",
                opportunity.Stage.ToString()));
            facts.Add(new AgentChatContextPositionFact(
                "opportunity-source",
                opportunity.Source.ToString()));
        }

        if (interaction is not null)
        {
            selectedEntities.Add(new AgentChatContextEntityReference(
                "crm-interaction",
                interaction.InteractionId.ToString("D"),
                interaction.DisplayLabel));
            facts.Add(new AgentChatContextPositionFact(
                "interaction-type",
                interaction.InteractionType.ToString()));
        }

        return new AgentChatSurfacePosition(
            Module,
            "crm",
            "accounts",
            CrmRoute,
            primarySelection,
            selectedEntities,
            facts);
    }

    private static AgentChatContextSurface BuildSurface(
        string surface,
        string view,
        string route,
        string displayName,
        AgentChatContextEntityReference? primarySelection = null,
        IReadOnlyList<AgentChatContextEntityReference>? selectedEntities = null,
        IReadOnlyList<AgentChatContextPositionFact>? facts = null)
    {
        var sourceId = primarySelection is null
            ? surface
            : $"{surface}:{primarySelection.Kind}:{primarySelection.Id}";
        return new AgentChatContextSurface(
            new AgentChatContextSource(
                new AgentChatContextSourceKind(SourceKind),
                new AgentChatContextSourceId(sourceId)),
            displayName,
            new AgentChatSurfacePosition(
                Module,
                surface,
                view,
                route,
                primarySelection,
                selectedEntities,
                facts: facts),
            accessMode: AgentChatContextScopeAccessMode.Unrestricted);
    }

    private static AgentChatContextEntityReference? BuildSelection(
        string kind,
        Guid? id,
        string? displayName,
        string parameterName)
    {
        if (!id.HasValue)
        {
            return null;
        }

        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("A CRM / HR selection id cannot be empty.", parameterName);
        }

        return new AgentChatContextEntityReference(
            kind,
            id.Value.ToString("D"),
            displayName ?? string.Empty);
    }

    private static IReadOnlyList<AgentChatContextPositionFact> BuildFacts<TEnum>(
        AgentChatContextEntityReference? selection,
        string name,
        TEnum? value)
        where TEnum : struct, Enum
    {
        if (selection is null)
        {
            return [];
        }

        return [BuildFact(name, value)];
    }

    private static IReadOnlyList<AgentChatContextPositionFact> BuildFacts<TFirst, TSecond>(
        AgentChatContextEntityReference? selection,
        string firstName,
        TFirst? firstValue,
        string secondName,
        TSecond? secondValue)
        where TFirst : struct, Enum
        where TSecond : struct, Enum
    {
        if (selection is null)
        {
            return [];
        }

        return
        [
            BuildFact(firstName, firstValue),
            BuildFact(secondName, secondValue)
        ];
    }

    private static IReadOnlyList<AgentChatContextPositionFact> BuildWorkforceFacts(
        AgentChatContextEntityReference? selection,
        PartyLifecycleStatus? lifecycleStatus,
        WorkforceAvailabilityState? availabilityStatus,
        bool hasWorkforceProfile)
    {
        if (selection is null)
        {
            return [];
        }

        var lifecycleFact = BuildFact("lifecycle-status", lifecycleStatus);
        if (!hasWorkforceProfile)
        {
            if (availabilityStatus.HasValue)
            {
                throw new ArgumentException(
                    "A workforce party without a profile cannot publish availability state.",
                    nameof(availabilityStatus));
            }

            return [lifecycleFact];
        }

        return
        [
            lifecycleFact,
            BuildFact("availability-status", availabilityStatus)
        ];
    }

    private static IReadOnlyList<AgentChatContextPositionFact> BuildFacts<TFirst, TSecond, TThird>(
        AgentChatContextEntityReference? selection,
        string firstName,
        TFirst? firstValue,
        string secondName,
        TSecond? secondValue,
        string thirdName,
        TThird? thirdValue)
        where TFirst : struct, Enum
        where TSecond : struct, Enum
        where TThird : struct, Enum
    {
        if (selection is null)
        {
            return [];
        }

        return
        [
            BuildFact(firstName, firstValue),
            BuildFact(secondName, secondValue),
            BuildFact(thirdName, thirdValue)
        ];
    }

    private static AgentChatContextPositionFact BuildFact<TEnum>(
        string name,
        TEnum? value)
        where TEnum : struct, Enum
    {
        if (!value.HasValue)
        {
            throw new ArgumentException($"Selected CRM / HR context requires '{name}'.", name);
        }

        CrmAgentChatContextBuilder.ValidateDefined(value.Value, name);
        return new AgentChatContextPositionFact(name, value.Value.ToString());
    }
}
