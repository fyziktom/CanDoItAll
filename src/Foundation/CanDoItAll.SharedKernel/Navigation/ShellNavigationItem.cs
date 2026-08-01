namespace CanDoItAll.SharedKernel;

public sealed record ShellNavigationItem(
    string Title,
    string Route,
    string Icon,
    string Description,
    bool PinnedByDefault = true,
    string? BadgeText = null);

public sealed record ShellNavigationContribution(
    string ModuleId,
    string ParentRoute,
    ShellNavigationItem Item,
    bool IsSubItem = true,
    int Order = 0,
    string? DesignNote = null);

public interface IShellNavigationContributor
{
    IEnumerable<ShellNavigationContribution> GetShellNavigationContributions();
}

public sealed record ShellWorkspaceItem(
    string Id,
    string Title,
    string Description,
    string DefaultRoute);
