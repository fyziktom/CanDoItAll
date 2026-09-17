using CanDoItAll.CrmHr.UI.Accounts;

namespace CanDoItAll.Modules.CrmHr.Components;

// Projects the CRM account workspace model into the account summary presentation. Labels are the enum names the
// module always showed; tones follow the module's own mapping; the conversion offer depends on the relationship stage.
public static class CrmHrAccountSummaryMapper
{
    public static CrmHrAccountSummary ToSummary(CrmAccountWorkspaceModel account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return new CrmHrAccountSummary(
            account.AccountPartyId,
            account.DisplayName,
            string.IsNullOrWhiteSpace(account.Summary) ? null : account.Summary,
            account.Profile.RelationshipStage.ToString(),
            ToTone(account.Profile.RelationshipStage),
            account.LifecycleStatus.ToString(),
            ToTone(account.LifecycleStatus),
            string.IsNullOrWhiteSpace(account.PrimaryEmail) ? null : account.PrimaryEmail,
            string.IsNullOrWhiteSpace(account.PrimaryPhone) ? null : account.PrimaryPhone,
            account.Roles.Select(role => role.ToString()).ToArray(),
            account.ConnectedRecords.Count,
            account.OpportunityCount,
            account.Tags.Count,
            account.Profile.RelationshipStage != CrmAccountRelationshipStage.ActiveCustomer);
    }

    public static CrmHrAccountTone ToTone(PartyLifecycleStatus lifecycleStatus)
        => lifecycleStatus switch
        {
            PartyLifecycleStatus.Active => CrmHrAccountTone.Success,
            PartyLifecycleStatus.Prospect => CrmHrAccountTone.Info,
            PartyLifecycleStatus.Inactive or PartyLifecycleStatus.Former => CrmHrAccountTone.Warning,
            _ => CrmHrAccountTone.Neutral
        };

    public static CrmHrAccountTone ToTone(CrmAccountRelationshipStage stage)
        => stage switch
        {
            CrmAccountRelationshipStage.ActiveCustomer => CrmHrAccountTone.Success,
            CrmAccountRelationshipStage.Prospect => CrmHrAccountTone.Info,
            CrmAccountRelationshipStage.DormantCustomer => CrmHrAccountTone.Warning,
            CrmAccountRelationshipStage.LostCustomer => CrmHrAccountTone.Danger,
            _ => CrmHrAccountTone.Neutral
        };
}
