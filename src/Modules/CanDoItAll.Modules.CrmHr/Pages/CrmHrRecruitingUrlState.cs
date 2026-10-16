using CanDoItAll.AppComponents.Navigation;

namespace CanDoItAll.Modules.CrmHr.Pages;

// The sections of the recruitment record dialog, in tab order.
public enum RecruitingSection {
    Application,
    Interviews,
    Assessments,
    Development,
    Conversion
}

// The record dialog without an application identity: the editor of a new application.
public enum RecruitingDialog {
    NewApplication
}

// The Recruiting location: the open application (or the candidate whose latest application opens), the
// new-application editor, the record section and the committed application catalog query behind the dialog.
public sealed record RecruitingUrlState(
    Guid? ApplicationId,
    Guid? PartyId,
    RecruitingDialog? Dialog,
    RecruitingSection Section,
    CrmHrCatalogUrlState<RecruitmentApplicationScope> Catalog) {
    public bool NamesApplication => ApplicationId is not null || PartyId is not null;

    public bool IsRecordOpen => NamesApplication || Dialog is not null;

    // The catalog behind the dialog with no record open.
    public RecruitingUrlState WithoutRecord() => this with {
        ApplicationId = null,
        PartyId = null,
        Dialog = null,
        Section = RecruitingSection.Application
    };
}

public sealed class RecruitingUrlCodec : IUrlStateCodec<RecruitingUrlState> {
    public const string ApplicationKey = "applicationId";
    public const string PartyKey = "partyId";
    public const string DialogKey = "dialog";
    public const string SectionKey = "section";
    public const string SearchKey = CrmHrCatalogUrlKeys.Search;
    public const string StageKey = "stage";
    public const string PageKey = CrmHrCatalogUrlKeys.Page;

    public static UrlTokenMap<RecruitingSection> Sections { get; } = new(
        (RecruitingSection.Application, "application"),
        (RecruitingSection.Interviews, "interviews"),
        (RecruitingSection.Assessments, "assessments"),
        (RecruitingSection.Development, "development"),
        (RecruitingSection.Conversion, "conversion"));

    public static UrlTokenMap<RecruitingDialog> Dialogs { get; } = new(
        (RecruitingDialog.NewApplication, "new-application"));

    public static UrlTokenMap<RecruitmentApplicationScope> Stages { get; } = new(
        (RecruitmentApplicationScope.All, "all"),
        (RecruitmentApplicationScope.Applied, "applied"),
        (RecruitmentApplicationScope.Screening, "screening"),
        (RecruitmentApplicationScope.Interviewing, "interviewing"),
        (RecruitmentApplicationScope.Offer, "offer"),
        (RecruitmentApplicationScope.Hired, "hired"),
        (RecruitmentApplicationScope.Rejected, "rejected"),
        (RecruitmentApplicationScope.Withdrawn, "withdrawn"));

    public static CrmHrCatalogUrlCodec<RecruitmentApplicationScope> Catalog { get; } = CrmHrCatalogUrlCodec<RecruitmentApplicationScope>.Filtered(
        StageKey,
        Stages,
        RecruitmentApplicationScope.All,
        RecruitmentApplicationQueryLimits.MaximumSearchLength);

    public static RecruitingUrlState Default { get; } = new(null, null, null, RecruitingSection.Application, Catalog.Default);

    // Static initializers run in declaration order: the instance reads the catalog keys, so it comes last.
    public static RecruitingUrlCodec Instance { get; } = new();

    public IReadOnlyList<string> Keys { get; } = [ApplicationKey, PartyKey, DialogKey, SectionKey, .. Catalog.Keys];

    // An application identity wins over the new-application editor, a section needs a saved application, and the new
    // application has only its application section.
    public RecruitingUrlState Read(UrlQuery query) {
        ArgumentNullException.ThrowIfNull(query);
        var applicationId = query.GetId(ApplicationKey);
        var partyId = query.GetId(PartyKey);
        var namesApplication = applicationId is not null || partyId is not null;
        var dialog = namesApplication ? null : query.GetToken(DialogKey, Dialogs);
        var section = namesApplication
            ? query.GetToken(SectionKey, Sections, RecruitingSection.Application)
            : RecruitingSection.Application;
        return new(applicationId, partyId, dialog, section, Catalog.Read(query));
    }

    public void Write(RecruitingUrlState state, UrlQueryWriter query) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(query);
        query.Id(ApplicationKey, state.ApplicationId);
        query.Id(PartyKey, state.PartyId);
        if (state.NamesApplication) {
            query.Token(SectionKey, state.Section, Sections, RecruitingSection.Application);
        } else {
            query.Token(DialogKey, state.Dialog, Dialogs);
        }

        Catalog.Write(state.Catalog, query);
    }
}
