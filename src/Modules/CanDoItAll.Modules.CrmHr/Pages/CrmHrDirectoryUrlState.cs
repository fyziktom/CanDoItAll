using CanDoItAll.AppComponents.Navigation;

namespace CanDoItAll.Modules.CrmHr.Pages;

// The sections of the party record dialog, in tab order.
public enum DirectorySection {
    Profile,
    Contacts,
    Handling,
    Activity,
    Relationships
}

// The record dialog without a party identity: the editor of a new party.
public enum DirectoryDialog {
    NewParty
}

// The Directory location: the open party record or the new-party editor, the record section, and the committed
// catalog query behind the dialog.
public sealed record DirectoryUrlState(
    Guid? PartyId,
    DirectoryDialog? Dialog,
    DirectorySection Section,
    CrmHrCatalogUrlState<PartyRecordScope> Catalog) {
    public bool IsRecordOpen => PartyId is not null || Dialog is not null;

    // The catalog behind the dialog with no record open.
    public DirectoryUrlState WithoutRecord() => this with { PartyId = null, Dialog = null, Section = DirectorySection.Profile };
}

public sealed class DirectoryUrlCodec : IUrlStateCodec<DirectoryUrlState> {
    public const string PartyKey = "partyId";
    public const string DialogKey = "dialog";
    public const string SectionKey = "section";
    public const string SearchKey = CrmHrCatalogUrlKeys.Search;
    public const string TagsKey = CrmHrCatalogUrlKeys.Tags;
    public const string ScopeKey = "scope";
    public const string PageKey = CrmHrCatalogUrlKeys.Page;

    public static UrlTokenMap<DirectorySection> Sections { get; } = new(
        (DirectorySection.Profile, "profile"),
        (DirectorySection.Contacts, "contacts"),
        (DirectorySection.Handling, "handling"),
        (DirectorySection.Activity, "activity"),
        (DirectorySection.Relationships, "relationships"));

    public static UrlTokenMap<DirectoryDialog> Dialogs { get; } = new(
        (DirectoryDialog.NewParty, "new-party"));

    public static UrlTokenMap<PartyRecordScope> Scopes { get; } = new(
        (PartyRecordScope.All, "all"),
        (PartyRecordScope.People, "people"),
        (PartyRecordScope.Organizations, "organizations"),
        (PartyRecordScope.OrganizationUnits, "units"),
        (PartyRecordScope.AiAgents, "agents"));

    public static CrmHrCatalogUrlCodec<PartyRecordScope> Catalog { get; } = CrmHrCatalogUrlCodec<PartyRecordScope>.Filtered(
        ScopeKey,
        Scopes,
        PartyRecordScope.All,
        PartyRecordQueryLimits.MaximumSearchLength,
        PartyRecordQueryLimits.MaximumTagCount);

    public static DirectoryUrlState Default { get; } = new(null, null, DirectorySection.Profile, Catalog.Default);

    // Static initializers run in declaration order: the instance reads the catalog keys, so it comes last.
    public static DirectoryUrlCodec Instance { get; } = new();

    public IReadOnlyList<string> Keys { get; } = [PartyKey, DialogKey, SectionKey, .. Catalog.Keys];

    // A section without its record is dropped, an identity wins over the new-party editor, and a new party has no
    // saved history or relationships to open.
    public DirectoryUrlState Read(UrlQuery query) {
        ArgumentNullException.ThrowIfNull(query);
        var partyId = query.GetId(PartyKey);
        var dialog = partyId is null ? query.GetToken(DialogKey, Dialogs) : null;
        var section = partyId is null && dialog is null
            ? DirectorySection.Profile
            : query.GetToken(SectionKey, Sections, DirectorySection.Profile);
        if (dialog == DirectoryDialog.NewParty && !IsNewPartySection(section)) {
            section = DirectorySection.Profile;
        }

        return new(partyId, dialog, section, Catalog.Read(query));
    }

    public void Write(DirectoryUrlState state, UrlQueryWriter query) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(query);
        query.Id(PartyKey, state.PartyId);
        if (state.PartyId is null) {
            query.Token(DialogKey, state.Dialog, Dialogs);
        }

        if (state.PartyId is not null || (state.Dialog == DirectoryDialog.NewParty && IsNewPartySection(state.Section))) {
            query.Token(SectionKey, state.Section, Sections, DirectorySection.Profile);
        }

        Catalog.Write(state.Catalog, query);
    }

    private static bool IsNewPartySection(DirectorySection section)
        => section is DirectorySection.Profile or DirectorySection.Contacts or DirectorySection.Handling;
}
