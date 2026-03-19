namespace CanDoItAll.SharedKernel;

public sealed record ShellNavigationItem(
    string Title,
    string Route,
    string Icon,
    string Description,
    bool PinnedByDefault = true);
