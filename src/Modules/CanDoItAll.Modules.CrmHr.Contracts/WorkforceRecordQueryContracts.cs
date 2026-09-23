namespace CanDoItAll.Modules.CrmHr;

public enum WorkforceRecordClassification
{
    Employee,
    Contractor,
    Freelancer,
    ExternalContact,
    DeliveryUnit
}

public static class WorkforceRecordQueryLimits
{
    public const int DefaultPageSize = 12;
    public const int MaximumPageSize = 100;
    public const int MaximumSearchLength = 200;
}

public sealed record WorkforceRecordQuery(
    string SearchText = "",
    WorkforceRecordClassification? Classification = null,
    PartyLifecycleStatus? LifecycleStatus = null,
    int PageIndex = 0,
    int PageSize = WorkforceRecordQueryLimits.DefaultPageSize,
    bool IncludeArchived = false);

public sealed record WorkforceRecordAffiliationSummaryModel(
    Guid Id,
    PartyOrganizationAffiliationKind AffiliationKind,
    Guid OrganizationPartyId,
    string OrganizationName,
    string JobTitle,
    bool IsPrimary,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    string DisplayText);

public sealed record WorkforceRecordQueryItem(
    Guid PartyId,
    string DisplayName,
    PartyType PartyType,
    PartyLifecycleStatus LifecycleStatus,
    bool IsSensitive,
    string Summary,
    WorkforceRecordClassification Classification,
    bool HasWorkforceProfile,
    DateTimeOffset UpdatedAtUtc,
    WorkforceRecordAffiliationSummaryModel? PrimaryAffiliation,
    string PrimaryAffiliationText,
    IReadOnlyList<WorkforceRecordAffiliationSummaryModel> OtherCurrentAffiliations);

public sealed record WorkforceRecordPage(
    IReadOnlyList<WorkforceRecordQueryItem> Items,
    int PageIndex,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public static WorkforceRecordPage Empty(
        int pageSize = WorkforceRecordQueryLimits.DefaultPageSize)
        => new([], 0, pageSize, 0);
}

public interface IWorkforceRecordQueryService
{
    Task<WorkforceRecordPage> SearchAsync(
        WorkforceRecordQuery query,
        CancellationToken cancellationToken = default);
}

public static class WorkforceRecordClassificationPolicy
{
    public static WorkforceRecordClassification Resolve(
        PartyOrganizationAffiliationKind? currentAffiliationKind,
        WorkforceKind? legacyWorkforceKind,
        PartyType partyType,
        bool hasDeliveryUnitRole)
    {
        if (currentAffiliationKind.HasValue)
        {
            return currentAffiliationKind.Value switch
            {
                PartyOrganizationAffiliationKind.Employee =>
                    WorkforceRecordClassification.Employee,
                PartyOrganizationAffiliationKind.Contractor =>
                    WorkforceRecordClassification.Contractor,
                PartyOrganizationAffiliationKind.Freelancer =>
                    WorkforceRecordClassification.Freelancer,
                PartyOrganizationAffiliationKind.ExternalContact =>
                    WorkforceRecordClassification.ExternalContact,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(currentAffiliationKind),
                    currentAffiliationKind,
                    "Unsupported organization affiliation kind.")
            };
        }

        if (partyType == PartyType.Person && legacyWorkforceKind.HasValue)
        {
            return legacyWorkforceKind.Value switch
            {
                WorkforceKind.Employee => WorkforceRecordClassification.Employee,
                WorkforceKind.Contractor => WorkforceRecordClassification.Contractor,
                WorkforceKind.Freelancer => WorkforceRecordClassification.Freelancer,
                WorkforceKind.DeliveryUnit => WorkforceRecordClassification.DeliveryUnit,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(legacyWorkforceKind),
                    legacyWorkforceKind,
                    "Unsupported legacy workforce kind.")
            };
        }

        if (partyType == PartyType.OrganizationUnit ||
            (partyType == PartyType.Organization &&
             (legacyWorkforceKind == WorkforceKind.DeliveryUnit ||
              hasDeliveryUnitRole)))
        {
            return WorkforceRecordClassification.DeliveryUnit;
        }

        return WorkforceRecordClassification.ExternalContact;
    }
}
