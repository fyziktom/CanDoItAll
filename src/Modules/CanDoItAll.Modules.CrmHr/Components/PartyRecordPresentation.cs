using CanDoItAll.AppComponents;

namespace CanDoItAll.Modules.CrmHr.Components;

internal static class PartyRecordPresentation
{
    private static readonly IReadOnlyList<PagedRecordFilterOption<PartyRecordScope>> IndividualScopeOptions =
    [
        new(PartyRecordScope.People, "People", "party-scope-people"),
        new(PartyRecordScope.Organizations, "Organizations", "party-scope-organizations"),
        new(PartyRecordScope.OrganizationUnits, "Units", "party-scope-units"),
        new(PartyRecordScope.AiAgents, "AI agents", "party-scope-agents")
    ];

    public static IReadOnlyList<PagedRecordFilterOption<PartyRecordScope>> BuildScopeOptions(
        PartyRecordScope allowedScope,
        PartyRecordScope initialScope)
    {
        ValidateScope(allowedScope, nameof(allowedScope));
        if (!IsContained(initialScope, allowedScope))
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialScope),
                initialScope,
                "The initial party scope must be contained by the allowed scope.");
        }

        var options = new List<PagedRecordFilterOption<PartyRecordScope>>
        {
            new(allowedScope, "All allowed", "party-scope-all")
        };
        options.AddRange(
            IndividualScopeOptions.Where(option => IsContained(option.Value, allowedScope)));
        if (!options.Any(option => option.Value == initialScope))
        {
            options.Insert(
                0,
                new PagedRecordFilterOption<PartyRecordScope>(
                    initialScope,
                    "Default scope",
                    "party-scope-default"));
        }

        return options
            .DistinctBy(option => option.Value)
            .ToList();
    }

    public static PagedRecordOption<Guid> ToOption(PartyRecordQueryItem party)
    {
        return new PagedRecordOption<Guid>(
            party.Id,
            party.DisplayName,
            ResolvePartyTypeLabel(party.PartyType))
        {
            Subtitle = party.ExternalCode,
            Description = party.Summary,
            Meta = party.LifecycleStatus.ToString(),
            Icon = ResolvePartyIcon(party.PartyType),
            Tags = party.Tags,
            TestId = $"crmhr-party-option-{party.Id:N}"
        };
    }

    public static PartyRecordScope ConstrainScope(
        PartyRecordScope requestedScope,
        PartyRecordScope allowedScope)
    {
        var scope = requestedScope & allowedScope;
        return scope == PartyRecordScope.None
            ? throw new InvalidOperationException("The selected party scope is not allowed by this browser.")
            : scope;
    }

    private static bool IsContained(
        PartyRecordScope scope,
        PartyRecordScope allowedScope)
    {
        return scope != PartyRecordScope.None &&
               (scope & ~allowedScope) == PartyRecordScope.None;
    }

    private static void ValidateScope(
        PartyRecordScope scope,
        string parameterName)
    {
        if (scope == PartyRecordScope.None ||
            (scope & ~PartyRecordScope.All) != PartyRecordScope.None)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                scope,
                "At least one supported party type must be allowed.");
        }
    }

    private static string ResolvePartyTypeLabel(PartyType partyType)
    {
        return partyType switch
        {
            PartyType.OrganizationUnit => "Organization unit",
            PartyType.AiAgent => "AI agent",
            _ => partyType.ToString()
        };
    }

    private static string ResolvePartyIcon(PartyType partyType)
    {
        return partyType switch
        {
            PartyType.Person => "person",
            PartyType.Organization => "business",
            PartyType.OrganizationUnit => "account_tree",
            PartyType.AiAgent => "smart_toy",
            _ => "category"
        };
    }
}
