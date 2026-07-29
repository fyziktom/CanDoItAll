using CanDoItAll.AppComponents;

namespace CanDoItAll.Modules.CrmHr.Components;

internal enum WorkforceRecordViewFilter
{
    All,
    Employee,
    Contractor,
    Freelancer,
    ExternalContact,
    DeliveryUnit
}

internal static class WorkforceRecordPresentation
{
    private static readonly IReadOnlyList<PagedRecordFilterOption<WorkforceRecordViewFilter>> FilterOptions =
    [
        new(WorkforceRecordViewFilter.All, "All", "crmhr-workforce-filter-all"),
        new(WorkforceRecordViewFilter.Employee, "Employee", "crmhr-workforce-filter-employee"),
        new(WorkforceRecordViewFilter.Contractor, "Contractor", "crmhr-workforce-filter-contractor"),
        new(WorkforceRecordViewFilter.Freelancer, "Freelancer", "crmhr-workforce-filter-freelancer"),
        new(WorkforceRecordViewFilter.ExternalContact, "External contact", "crmhr-workforce-filter-external-contact"),
        new(WorkforceRecordViewFilter.DeliveryUnit, "Delivery unit", "crmhr-workforce-filter-delivery-unit")
    ];

    public static IReadOnlyList<PagedRecordFilterOption<WorkforceRecordViewFilter>> BuildFilterOptions()
        => FilterOptions;

    public static WorkforceRecordClassification? ResolveClassificationFilter(
        WorkforceRecordViewFilter filter)
    {
        return filter switch
        {
            WorkforceRecordViewFilter.All => null,
            WorkforceRecordViewFilter.Employee => WorkforceRecordClassification.Employee,
            WorkforceRecordViewFilter.Contractor => WorkforceRecordClassification.Contractor,
            WorkforceRecordViewFilter.Freelancer => WorkforceRecordClassification.Freelancer,
            WorkforceRecordViewFilter.ExternalContact => WorkforceRecordClassification.ExternalContact,
            WorkforceRecordViewFilter.DeliveryUnit => WorkforceRecordClassification.DeliveryUnit,
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, "Unsupported workforce filter.")
        };
    }

    public static PagedRecordOption<Guid> ToOption(WorkforceRecordQueryItem item)
    {
        return new PagedRecordOption<Guid>(
            item.PartyId,
            item.DisplayName,
            ResolveClassificationLabel(item.Classification))
        {
            Subtitle = ResolvePrimaryAffiliationText(item),
            SubtitleTooltip = BuildAffiliationTooltip(item),
            Description = item.Summary,
            Meta = item.HasWorkforceProfile
                ? "Staffable profile"
                : item.Classification == WorkforceRecordClassification.ExternalContact
                    ? "No staffable profile"
                    : "Profile not created",
            Icon = item.Classification == WorkforceRecordClassification.DeliveryUnit
                ? "account_tree"
                : "person",
            KindTone = ResolveClassificationBadgeTone(item.Classification),
            CornerStatus = ResolveLifecycleStatus(item.LifecycleStatus),
            PinKindToUpperLeft = true,
            TestId = $"crmhr-workforce-option-{item.PartyId:N}"
        };
    }

    public static WorkforceRecordClassification ResolveSelectedClassification(
        WorkforceProfileWorkspaceModel workspace,
        IReadOnlyList<PartyOrganizationAffiliationListItemModel> affiliations)
    {
        if (workspace.PartyType != PartyType.Person)
        {
            return WorkforceRecordClassification.DeliveryUnit;
        }

        var currentAffiliation = affiliations
            .Where(item => item.IsCurrent)
            .OrderByDescending(item => item.IsPrimary)
            .ThenByDescending(item => item.ValidFrom)
            .ThenByDescending(item => item.UpdatedAtUtc)
            .ThenBy(item => item.Id)
            .FirstOrDefault();
        return WorkforceRecordClassificationPolicy.Resolve(
            currentAffiliation?.AffiliationKind,
            workspace.Profile.Id.HasValue
                ? workspace.Profile.WorkforceKind
                : null,
            workspace.PartyType,
            workspace.Roles.Contains(PartyRoleKind.DeliveryUnit));
    }

    public static string ResolveClassificationLabel(WorkforceRecordClassification classification)
    {
        return classification switch
        {
            WorkforceRecordClassification.ExternalContact => "External contact",
            WorkforceRecordClassification.DeliveryUnit => "Delivery unit",
            _ => classification.ToString()
        };
    }

    public static string ResolveClassificationTone(WorkforceRecordClassification classification)
    {
        return classification switch
        {
            WorkforceRecordClassification.Employee => "success",
            WorkforceRecordClassification.Contractor => "warning",
            WorkforceRecordClassification.Freelancer => "info",
            WorkforceRecordClassification.ExternalContact => "neutral",
            WorkforceRecordClassification.DeliveryUnit => "primary",
            _ => "neutral"
        };
    }

    private static string ResolvePrimaryAffiliationText(WorkforceRecordQueryItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.PrimaryAffiliationText))
        {
            return item.PrimaryAffiliationText;
        }

        return item.Classification == WorkforceRecordClassification.DeliveryUnit
            ? "Delivery unit"
            : "No current organization affiliation";
    }

    private static string BuildAffiliationTooltip(WorkforceRecordQueryItem item)
    {
        var otherAffiliations = item.OtherCurrentAffiliations
            .Select(affiliation => affiliation.DisplayText)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();
        return otherAffiliations.Count == 0
            ? ResolvePrimaryAffiliationText(item)
            : $"Other current affiliations: {string.Join("; ", otherAffiliations)}";
    }

    private static PagedRecordBadgeTone ResolveClassificationBadgeTone(
        WorkforceRecordClassification classification)
    {
        return classification switch
        {
            WorkforceRecordClassification.Employee => PagedRecordBadgeTone.Success,
            WorkforceRecordClassification.Contractor => PagedRecordBadgeTone.Warning,
            WorkforceRecordClassification.Freelancer => PagedRecordBadgeTone.Info,
            WorkforceRecordClassification.ExternalContact => PagedRecordBadgeTone.Neutral,
            WorkforceRecordClassification.DeliveryUnit => PagedRecordBadgeTone.Teal,
            _ => PagedRecordBadgeTone.Neutral
        };
    }

    private static PagedRecordCornerStatus ResolveLifecycleStatus(
        PartyLifecycleStatus lifecycleStatus)
    {
        var tone = lifecycleStatus switch
        {
            PartyLifecycleStatus.Active => PagedRecordStatusTone.Success,
            PartyLifecycleStatus.Inactive or PartyLifecycleStatus.Former => PagedRecordStatusTone.Danger,
            PartyLifecycleStatus.Candidate => PagedRecordStatusTone.Warning,
            PartyLifecycleStatus.Prospect => PagedRecordStatusTone.Info,
            PartyLifecycleStatus.Draft or PartyLifecycleStatus.Archived => PagedRecordStatusTone.Neutral,
            _ => PagedRecordStatusTone.Neutral
        };

        return new PagedRecordCornerStatus(lifecycleStatus.ToString(), tone);
    }
}
