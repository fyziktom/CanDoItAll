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

    private static readonly ShellNavigationContribution LiveProcessesContribution = new(
        ModuleId: "processes",
        ParentRoute: "/processes",
        Item: new ShellNavigationItem(
            "Live Processes",
            "/processes/live",
            "monitor_heart",
            "Runtime process runs, attention queues, projection freshness, and manager context.",
            PinnedByDefault: false),
        IsSubItem: true,
        Order: 21,
        DesignNote: "Rendered directly after the process module entry, matching the Agent Workflows navigation treatment.");

    public IEnumerable<ShellNavigationContribution> GetShellNavigationContributions()
        => [ProcessesContribution, LiveProcessesContribution];
}
