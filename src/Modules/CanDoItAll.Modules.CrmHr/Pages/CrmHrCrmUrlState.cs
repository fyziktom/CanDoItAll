using CanDoItAll.AppComponents.Navigation;

namespace CanDoItAll.Modules.CrmHr.Pages;

// The sections of the account record dialog, in tab order.
public enum CrmSection {
    Overview,
    Financials,
    ConnectedRecords,
    Interactions,
    Opportunities
}

// The CRM location: the open account, the opportunity open over it, the interaction in context, the account section
// and the committed account catalog query. An opportunity or an interaction alone names its account; the page
// resolves it.
public sealed record CrmUrlState(
    Guid? AccountId,
    Guid? OpportunityId,
    Guid? InteractionId,
    CrmSection Section,
    CrmHrCatalogUrlState<PartyRecordScope> Catalog) {
    public bool HasAccountContext => AccountId is not null || OpportunityId is not null || InteractionId is not null;

    // The section a record opens on when the location names none: an opportunity opens on its pipeline and an
    // interaction on the account's interactions.
    public CrmSection DefaultSection => DefaultSectionFor(OpportunityId, InteractionId);

    public CrmUrlState WithoutRecord() => this with {
        AccountId = null,
        OpportunityId = null,
        InteractionId = null,
        Section = CrmSection.Overview
    };

    internal static CrmSection DefaultSectionFor(Guid? opportunityId, Guid? interactionId)
        => opportunityId is not null
            ? CrmSection.Opportunities
            : interactionId is not null ? CrmSection.Interactions : CrmSection.Overview;
}

public sealed class CrmUrlCodec : IUrlStateCodec<CrmUrlState> {
    public const string AccountKey = "accountId";
    public const string OpportunityKey = "opportunityId";
    public const string InteractionKey = "interactionId";
    public const string SectionKey = "section";
    public const string SearchKey = CrmHrCatalogUrlKeys.Search;
    public const string TagsKey = CrmHrCatalogUrlKeys.Tags;
    public const string PageKey = CrmHrCatalogUrlKeys.Page;

    public static UrlTokenMap<CrmSection> Sections { get; } = new(
        (CrmSection.Overview, "overview"),
        (CrmSection.Financials, "financials"),
        (CrmSection.ConnectedRecords, "connected-records"),
        (CrmSection.Interactions, "interactions"),
        (CrmSection.Opportunities, "opportunities"));

    // The account catalog lists organizations only: its scope is not navigation state.
    public static CrmHrCatalogUrlCodec<PartyRecordScope> Catalog { get; } = CrmHrCatalogUrlCodec<PartyRecordScope>.Fixed(
        PartyRecordScope.Organizations,
        PartyRecordQueryLimits.MaximumSearchLength,
        PartyRecordQueryLimits.MaximumTagCount);

    public static CrmUrlState Default { get; } = new(null, null, null, CrmSection.Overview, Catalog.Default);

    // Static initializers run in declaration order: the instance reads the catalog keys, so it comes last.
    public static CrmUrlCodec Instance { get; } = new();

    public IReadOnlyList<string> Keys { get; } = [AccountKey, OpportunityKey, InteractionKey, SectionKey, .. Catalog.Keys];

    // A section without an account context is dropped; with one, the omitted section is the context's default.
    public CrmUrlState Read(UrlQuery query) {
        ArgumentNullException.ThrowIfNull(query);
        var accountId = query.GetId(AccountKey);
        var opportunityId = query.GetId(OpportunityKey);
        var interactionId = query.GetId(InteractionKey);
        var hasAccountContext = accountId is not null || opportunityId is not null || interactionId is not null;
        var section = hasAccountContext
            ? query.GetToken(SectionKey, Sections) ?? CrmUrlState.DefaultSectionFor(opportunityId, interactionId)
            : CrmSection.Overview;
        return new(accountId, opportunityId, interactionId, section, Catalog.Read(query));
    }

    public void Write(CrmUrlState state, UrlQueryWriter query) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(query);
        query.Id(AccountKey, state.AccountId);
        query.Id(OpportunityKey, state.OpportunityId);
        query.Id(InteractionKey, state.InteractionId);
        if (state.HasAccountContext) {
            query.Token(SectionKey, state.Section, Sections, state.DefaultSection);
        }

        Catalog.Write(state.Catalog, query);
    }
}
