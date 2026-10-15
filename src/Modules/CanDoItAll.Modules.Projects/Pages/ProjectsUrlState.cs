using CanDoItAll.AppComponents.Navigation;

namespace CanDoItAll.Modules.Projects.Pages;

public enum ProjectsPortfolioView {
    Cards,
    Files
}

public enum ProjectsModalMode {
    Overview,
    Editor
}

public enum ProjectsEditorStep {
    Identity,
    Dates,
    Stack,
    Links,
    Review
}

// One overlay at a time: the project modal wins over the hierarchy dialog, which wins over the files dialog.
public sealed record ProjectsUrlState(
    Guid? ProjectId = null,
    ProjectsModalMode Mode = ProjectsModalMode.Overview,
    ProjectsEditorStep Step = ProjectsEditorStep.Identity,
    string? Search = null,
    ProjectStatus? Status = null,
    ProjectPartyPortfolioCategory? PartyCategory = null,
    string? Party = null,
    Guid? ParentId = null,
    ProjectHierarchyFilterMode Scope = ProjectHierarchyFilterMode.Descendants,
    bool IncludeSubprojects = true,
    ProjectsPortfolioView View = ProjectsPortfolioView.Cards,
    Guid? HierarchyId = null,
    Guid? FilesId = null) {
    public bool HasOverlay => ProjectId is not null || HierarchyId is not null || FilesId is not null;

    public ProjectsUrlState WithoutOverlay() => this with {
        ProjectId = null,
        Mode = ProjectsModalMode.Overview,
        Step = ProjectsEditorStep.Identity,
        HierarchyId = null,
        FilesId = null
    };

    public ProjectFileFilter ToFilter() => new(Search, Status, PartyCategory, Party, ParentId, Scope, IncludeSubprojects);
}

public sealed class ProjectsUrlCodec : IUrlStateCodec<ProjectsUrlState> {
    public const string ProjectKey = "projectId";
    public const string ModeKey = "mode";
    public const string StepKey = "step";
    public const string SearchKey = "q";
    public const string StatusKey = "status";
    public const string PartyCategoryKey = "partyCategory";
    public const string PartyKey = "party";
    public const string ParentKey = "parentId";
    public const string ScopeKey = "scope";
    public const string SubprojectsKey = "subprojects";
    public const string ViewKey = "view";
    public const string HierarchyKey = "hierarchyId";
    public const string FilesKey = "filesId";
    public const string Path = "/projects";

    public static ProjectsUrlCodec Instance { get; } = new();

    public static UrlTokenMap<ProjectsModalMode> Modes { get; } = new(
        (ProjectsModalMode.Overview, "overview"),
        (ProjectsModalMode.Editor, "editor"));

    public static UrlTokenMap<ProjectsEditorStep> Steps { get; } = new(
        (ProjectsEditorStep.Identity, "identity"),
        (ProjectsEditorStep.Dates, "dates"),
        (ProjectsEditorStep.Stack, "stack"),
        (ProjectsEditorStep.Links, "links"),
        (ProjectsEditorStep.Review, "review"));

    public static UrlTokenMap<ProjectStatus> Statuses { get; } = new(
        (ProjectStatus.Draft, "draft"),
        (ProjectStatus.Active, "active"),
        (ProjectStatus.OnHold, "on-hold"),
        (ProjectStatus.Completed, "completed"),
        (ProjectStatus.Archived, "archived"));

    public static UrlTokenMap<ProjectPartyPortfolioCategory> PartyCategories { get; } = new(
        (ProjectPartyPortfolioCategory.Customer, "customer"),
        (ProjectPartyPortfolioCategory.DeliveryUnit, "delivery-unit"),
        (ProjectPartyPortfolioCategory.Owner, "owner"),
        (ProjectPartyPortfolioCategory.Stakeholder, "stakeholder"),
        (ProjectPartyPortfolioCategory.Partner, "partner"),
        (ProjectPartyPortfolioCategory.AiAgent, "ai-agent"));

    public static UrlTokenMap<ProjectHierarchyFilterMode> Scopes { get; } = new(
        (ProjectHierarchyFilterMode.Descendants, "descendants"),
        (ProjectHierarchyFilterMode.Children, "children"),
        (ProjectHierarchyFilterMode.Parents, "parents"),
        (ProjectHierarchyFilterMode.Related, "related"));

    public static UrlTokenMap<ProjectsPortfolioView> Views { get; } = new(
        (ProjectsPortfolioView.Cards, "cards"),
        (ProjectsPortfolioView.Files, "files"));

    public IReadOnlyList<string> Keys { get; } = [
        ProjectKey, ModeKey, StepKey, SearchKey, StatusKey, PartyCategoryKey, PartyKey, ParentKey, ScopeKey, SubprojectsKey, ViewKey,
        HierarchyKey, FilesKey
    ];

    public ProjectsUrlState Read(UrlQuery query) {
        var projectId = query.GetId(ProjectKey);
        var mode = projectId is null ? ProjectsModalMode.Overview : query.GetToken(ModeKey, Modes, ProjectsModalMode.Overview);
        var hierarchyId = projectId is null ? query.GetId(HierarchyKey) : null;
        return new(
            projectId,
            mode,
            mode == ProjectsModalMode.Editor ? query.GetToken(StepKey, Steps, ProjectsEditorStep.Identity) : ProjectsEditorStep.Identity,
            query.GetText(SearchKey, ProjectFileFilter.MaximumTextLength),
            query.GetToken(StatusKey, Statuses),
            query.GetToken(PartyCategoryKey, PartyCategories),
            query.GetText(PartyKey, ProjectFileFilter.MaximumTextLength),
            query.GetId(ParentKey),
            query.GetToken(ScopeKey, Scopes, ProjectHierarchyFilterMode.Descendants),
            query.GetFlag(SubprojectsKey, defaultValue: true),
            query.GetToken(ViewKey, Views, ProjectsPortfolioView.Cards),
            hierarchyId,
            projectId is null && hierarchyId is null ? query.GetId(FilesKey) : null);
    }

    public void Write(ProjectsUrlState state, UrlQueryWriter query) {
        query.Id(ProjectKey, state.ProjectId);
        if (state.ProjectId is not null) {
            query.Token(ModeKey, state.Mode, Modes, ProjectsModalMode.Overview);
            if (state.Mode == ProjectsModalMode.Editor) {
                query.Token(StepKey, state.Step, Steps, ProjectsEditorStep.Identity);
            }
        }
        query.Text(SearchKey, state.Search);
        query.Token(StatusKey, state.Status, Statuses);
        query.Token(PartyCategoryKey, state.PartyCategory, PartyCategories);
        query.Text(PartyKey, state.Party);
        query.Id(ParentKey, state.ParentId);
        query.Token(ScopeKey, state.Scope, Scopes, ProjectHierarchyFilterMode.Descendants);
        query.Flag(SubprojectsKey, state.IncludeSubprojects, defaultValue: true);
        query.Token(ViewKey, state.View, Views, ProjectsPortfolioView.Cards);
        if (state.ProjectId is null) {
            query.Id(HierarchyKey, state.HierarchyId);
            if (state.HierarchyId is null) {
                query.Id(FilesKey, state.FilesId);
            }
        }
    }

    public static string Dashboard(Guid projectId) {
        if (projectId == Guid.Empty) {
            throw new ArgumentException("A project dashboard route needs a project identifier.", nameof(projectId));
        }
        return Instance.Href(Path, new(projectId));
    }
}
