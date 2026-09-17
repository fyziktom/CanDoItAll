namespace CanDoItAll.CrmHr.UI.Accounts;

// Status tones the account summary renders; the host maps its own domain enums to them.
public enum CrmHrAccountTone
{
    Neutral,
    Info,
    Success,
    Warning,
    Danger
}

// The account record as the summary surface shows it: labels and tones already resolved by the host, counts already
// computed. No domain enum, editor model or workspace model crosses this boundary.
public sealed record CrmHrAccountSummary(
    Guid AccountPartyId,
    string DisplayName,
    string? Summary,
    string RelationshipStageLabel,
    CrmHrAccountTone RelationshipStageTone,
    string LifecycleLabel,
    CrmHrAccountTone LifecycleTone,
    string? PrimaryEmail,
    string? PrimaryPhone,
    IReadOnlyList<string> Roles,
    int ConnectionCount,
    int OpportunityCount,
    int TagCount,
    bool CanConvertToActiveCustomer)
{
    public IReadOnlyList<string> Roles { get; init; } = Roles?.ToArray() ?? throw new ArgumentNullException(nameof(Roles));
}

// Everything the account summary can ask its host to do. Navigation and the conversion mutation stay with the host.
// Each intent carries the exact account record that was rendered when its action was created (null for the
// no-account state), so a host can recognize an action whose render has since been replaced: an action created for
// one record never acquires the identity of the record shown later, whatever the surface displays by then.
public abstract record CrmHrAccountSummaryIntent
{
    private CrmHrAccountSummaryIntent()
    {
    }

    public sealed record OpenDirectory(CrmHrAccountSummary? Account) : CrmHrAccountSummaryIntent;

    public sealed record ConvertToActiveCustomer(CrmHrAccountSummary Account) : CrmHrAccountSummaryIntent;
}

public static class CrmHrAccountText
{
    public const string NotSetValue = "Not set";
    public const string NoRolesValue = "None";

    public static string FormatContact(string? value)
        => string.IsNullOrWhiteSpace(value) ? NotSetValue : value;

    public static string FormatRoles(IReadOnlyList<string> roles)
        => roles.Count == 0 ? NoRolesValue : string.Join(", ", roles);

    public static string FormatCount(int count)
        => count.ToString();

    public static string ToneToken(CrmHrAccountTone tone)
        => tone switch
        {
            CrmHrAccountTone.Info => "info",
            CrmHrAccountTone.Success => "success",
            CrmHrAccountTone.Warning => "warning",
            CrmHrAccountTone.Danger => "danger",
            _ => "neutral"
        };
}
