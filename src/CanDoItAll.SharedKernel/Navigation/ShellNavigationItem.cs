namespace CanDoItAll.SharedKernel;

public sealed record ShellNavigationItem(
    string Title,
    string Route,
    string Icon,
    string Description,
    bool PinnedByDefault = true,
    string? BadgeText = null);

public sealed record ShellWorkspaceItem(
    string Id,
    string Title,
    string Description,
    string DefaultRoute);
