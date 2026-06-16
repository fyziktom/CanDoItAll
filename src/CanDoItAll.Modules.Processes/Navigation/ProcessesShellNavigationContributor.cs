using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessesShellNavigationContributor : IShellNavigationContributor
{
    private static readonly ShellNavigationContribution ProcessesContribution = new(
        ModuleId: "processes",
        ParentRoute: "/agents",
        Item: new ShellNavigationItem(
            "Processes",
            "/processes",
            "account_tree",
            "Projection-first process definitions, launch planning, live runs, and history.",
            PinnedByDefault: false),
        IsSubItem: false,
        Order: 20,
        DesignNote: "Processes are shown beside Agents until nested module navigation is introduced.");

    public IEnumerable<ShellNavigationContribution> GetShellNavigationContributions()
        => [ProcessesContribution];
}
