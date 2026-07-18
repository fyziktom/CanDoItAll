namespace CanDoItAll.Modules.Projects.Pages;

internal enum ProjectsPageLoadKind
{
    Route,
    Preview,
    NewProject,
    CompletionRefresh,
    Save,
    Delete,
    Import,
    Files,
    Hierarchy
}

internal readonly record struct ProjectsPageLoadKey(
    ProjectsPageLoadKind Kind,
    Guid? ProjectId)
{
    public bool RequiresRouteMatch => Kind == ProjectsPageLoadKind.Route;
}

internal readonly record struct ProjectsPageLoadStamp(
    long Generation,
    ProjectsPageLoadKey Key);

internal sealed class ProjectsPageLoadGeneration
{
    private readonly Lock sync = new();
    private long generation;
    private ProjectsPageLoadKey currentKey;

    public ProjectsPageLoadStamp Begin(ProjectsPageLoadKey key)
    {
        lock (sync)
        {
            currentKey = key;
            return new ProjectsPageLoadStamp(++generation, key);
        }
    }

    public bool IsCurrent(ProjectsPageLoadStamp stamp, Guid? currentRouteProjectId)
    {
        lock (sync)
        {
            return IsCurrentCore(stamp, currentRouteProjectId);
        }
    }

    public bool TryCommit(
        ProjectsPageLoadStamp stamp,
        Guid? currentRouteProjectId,
        Action commit)
    {
        ArgumentNullException.ThrowIfNull(commit);

        lock (sync)
        {
            if (!IsCurrentCore(stamp, currentRouteProjectId))
            {
                return false;
            }

            commit();
            return true;
        }
    }

    public void Invalidate()
    {
        lock (sync)
        {
            generation++;
        }
    }

    private bool IsCurrentCore(ProjectsPageLoadStamp stamp, Guid? currentRouteProjectId)
    {
        return stamp.Generation == generation &&
               stamp.Key == currentKey &&
               (!stamp.Key.RequiresRouteMatch || stamp.Key.ProjectId == currentRouteProjectId);
    }
}
