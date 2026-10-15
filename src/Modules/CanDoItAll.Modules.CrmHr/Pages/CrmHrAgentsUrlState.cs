using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.CrmHr.UI.Agents;

namespace CanDoItAll.Modules.CrmHr.Pages;

// The Agents location: the open agent record, its section and the committed agent catalog query behind the dialog.
public sealed record CrmHrAgentsUrlState(
    Guid? PartyId,
    CrmHrAgentRecordSection Section,
    CrmHrCatalogUrlState<AiAgentValidationFilter> Catalog) {
    // The catalog behind the dialog with no record open.
    public CrmHrAgentsUrlState WithoutRecord() => this with { PartyId = null, Section = CrmHrAgentRecordSection.Overview };
}

public sealed class CrmHrAgentsUrlCodec : IUrlStateCodec<CrmHrAgentsUrlState> {
    public const string PartyKey = "partyId";
    public const string SectionKey = "section";
    public const string SearchKey = CrmHrCatalogUrlKeys.Search;
    public const string ValidationKey = "validation";
    public const string PageKey = CrmHrCatalogUrlKeys.Page;

    public static UrlTokenMap<CrmHrAgentRecordSection> Sections { get; } = new(
        (CrmHrAgentRecordSection.Overview, "overview"),
        (CrmHrAgentRecordSection.CrmProfile, "crm-profile"),
        (CrmHrAgentRecordSection.TechnicalProjection, "technical-projection"),
        (CrmHrAgentRecordSection.Evidence, "evidence"));

    public static UrlTokenMap<AiAgentValidationFilter> Validations { get; } = new(
        (AiAgentValidationFilter.All, "all"),
        (AiAgentValidationFilter.Draft, "draft"),
        (AiAgentValidationFilter.ReviewRequired, "review-required"),
        (AiAgentValidationFilter.Approved, "approved"),
        (AiAgentValidationFilter.Suspended, "suspended"));

    public static CrmHrCatalogUrlCodec<AiAgentValidationFilter> Catalog { get; } = CrmHrCatalogUrlCodec<AiAgentValidationFilter>.Filtered(
        ValidationKey,
        Validations,
        AiAgentValidationFilter.All,
        AiAgentDirectoryQueryLimits.MaximumSearchLength);

    public static CrmHrAgentsUrlState Default { get; } = new(null, CrmHrAgentRecordSection.Overview, Catalog.Default);

    // Static initializers run in declaration order: the instance reads the catalog keys, so it comes last.
    public static CrmHrAgentsUrlCodec Instance { get; } = new();

    public IReadOnlyList<string> Keys { get; } = [PartyKey, SectionKey, .. Catalog.Keys];

    // A section without its record is dropped.
    public CrmHrAgentsUrlState Read(UrlQuery query) {
        ArgumentNullException.ThrowIfNull(query);
        var partyId = query.GetId(PartyKey);
        var section = partyId is null
            ? CrmHrAgentRecordSection.Overview
            : query.GetToken(SectionKey, Sections, CrmHrAgentRecordSection.Overview);
        return new(partyId, section, Catalog.Read(query));
    }

    public void Write(CrmHrAgentsUrlState state, UrlQueryWriter query) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(query);
        query.Id(PartyKey, state.PartyId);
        if (state.PartyId is not null) {
            query.Token(SectionKey, state.Section, Sections, CrmHrAgentRecordSection.Overview);
        }

        Catalog.Write(state.Catalog, query);
    }
}
