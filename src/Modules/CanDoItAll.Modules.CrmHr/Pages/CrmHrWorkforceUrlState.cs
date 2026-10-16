using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.CrmHr.UI.Pickers;

namespace CanDoItAll.Modules.CrmHr.Pages;

// The sections of the workforce record dialog, in tab order.
public enum WorkforceSection {
    Overview,
    Profile,
    Skills,
    Allocations,
    History
}

// The Workforce location: the open workforce record, its section and the committed catalog query behind the dialog.
public sealed record WorkforceUrlState(
    Guid? PartyId,
    WorkforceSection Section,
    CrmHrCatalogUrlState<WorkforceRecordViewFilter> Catalog) {
    // The catalog behind the dialog with no record open.
    public WorkforceUrlState WithoutRecord() => this with { PartyId = null, Section = WorkforceSection.Overview };
}

public sealed class WorkforceUrlCodec : IUrlStateCodec<WorkforceUrlState> {
    public const string PartyKey = "partyId";
    public const string SectionKey = "section";
    public const string SearchKey = CrmHrCatalogUrlKeys.Search;
    public const string KindKey = "kind";
    public const string PageKey = CrmHrCatalogUrlKeys.Page;

    public static UrlTokenMap<WorkforceSection> Sections { get; } = new(
        (WorkforceSection.Overview, "overview"),
        (WorkforceSection.Profile, "profile"),
        (WorkforceSection.Skills, "skills"),
        (WorkforceSection.Allocations, "allocations"),
        (WorkforceSection.History, "history"));

    public static UrlTokenMap<WorkforceRecordViewFilter> Kinds { get; } = new(
        (WorkforceRecordViewFilter.All, "all"),
        (WorkforceRecordViewFilter.Employee, "employee"),
        (WorkforceRecordViewFilter.Contractor, "contractor"),
        (WorkforceRecordViewFilter.Freelancer, "freelancer"),
        (WorkforceRecordViewFilter.ExternalContact, "external-contact"),
        (WorkforceRecordViewFilter.DeliveryUnit, "delivery-unit"));

    public static CrmHrCatalogUrlCodec<WorkforceRecordViewFilter> Catalog { get; } = CrmHrCatalogUrlCodec<WorkforceRecordViewFilter>.Filtered(
        KindKey,
        Kinds,
        WorkforceRecordViewFilter.All,
        WorkforceRecordQueryLimits.MaximumSearchLength);

    public static WorkforceUrlState Default { get; } = new(null, WorkforceSection.Overview, Catalog.Default);

    // Static initializers run in declaration order: the instance reads the catalog keys, so it comes last.
    public static WorkforceUrlCodec Instance { get; } = new();

    public IReadOnlyList<string> Keys { get; } = [PartyKey, SectionKey, .. Catalog.Keys];

    // A section without its record is dropped.
    public WorkforceUrlState Read(UrlQuery query) {
        ArgumentNullException.ThrowIfNull(query);
        var partyId = query.GetId(PartyKey);
        var section = partyId is null
            ? WorkforceSection.Overview
            : query.GetToken(SectionKey, Sections, WorkforceSection.Overview);
        return new(partyId, section, Catalog.Read(query));
    }

    public void Write(WorkforceUrlState state, UrlQueryWriter query) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(query);
        query.Id(PartyKey, state.PartyId);
        if (state.PartyId is not null) {
            query.Token(SectionKey, state.Section, Sections, WorkforceSection.Overview);
        }

        Catalog.Write(state.Catalog, query);
    }
}
