using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AgentFrameworkShellNavigationContributor : IShellNavigationContributor
{
    private static readonly ShellNavigationContribution WorkflowsContribution = new(
        ModuleId: "agent-framework",
        ParentRoute: "/agents",
        Item: new ShellNavigationItem(
            "Workflows",
            "/agents/workflows",
            "account_tree",
            "Workflow definitions, components, test runs, and runtime execution.",
            PinnedByDefault: false),
        IsSubItem: true,
        Order: 10,
        DesignNote: "Rendered as a flat main menu item for now; the subitem marker prepares the later nested menu treatment.");

    public IEnumerable<ShellNavigationContribution> GetShellNavigationContributions()
        => [WorkflowsContribution];
}
