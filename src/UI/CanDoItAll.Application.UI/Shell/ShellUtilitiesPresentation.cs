using CanDoItAll.Application.UI.Profiles;

namespace CanDoItAll.Application.UI.Shell;

public sealed record ShellUtilitiesPresentation(
    Guid OpeningId,
    string CurrentProfileName,
    DatabaseFlyoutDetails Details,
    string SafeSummary,
    IReadOnlyList<DatabaseProfileDisplay> RecentProfiles,
    bool IsBusy,
    bool CanManageDatabases);
