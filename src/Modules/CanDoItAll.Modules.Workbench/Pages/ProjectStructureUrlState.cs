using CanDoItAll.AppComponents.Navigation;

namespace CanDoItAll.Modules.Workbench.Pages;

public enum ProjectStructureView {
    Canvas,
    Gantt,
    ManagerSummary
}

public sealed record ProjectStructureUrlState(ProjectStructureView View = ProjectStructureView.Canvas);

public sealed class ProjectStructureUrlCodec : IUrlStateCodec<ProjectStructureUrlState> {
    public const string TabKey = "tab";

    public static ProjectStructureUrlCodec Instance { get; } = new();

    public static UrlTokenMap<ProjectStructureView> Views { get; } = new(
        (ProjectStructureView.Canvas, "canvas"),
        (ProjectStructureView.Gantt, "gantt"),
        (ProjectStructureView.ManagerSummary, "manager-summary"));

    public IReadOnlyList<string> Keys { get; } = [TabKey];

    public ProjectStructureUrlState Read(UrlQuery query)
        => new(query.GetToken(TabKey, Views, ProjectStructureView.Canvas));

    public void Write(ProjectStructureUrlState state, UrlQueryWriter query)
        => query.Token(TabKey, state.View, Views, ProjectStructureView.Canvas);

    public static string Route(Guid projectId, ProjectStructureView view = ProjectStructureView.Canvas) {
        if (projectId == Guid.Empty) {
            throw new ArgumentException("A project structure route needs a project identifier.", nameof(projectId));
        }
        return Instance.Href($"/projects/{projectId:D}/structure", new(view));
    }
}
