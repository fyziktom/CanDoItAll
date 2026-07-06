using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Memory;

public sealed class MemoryShellNavigationContributor : IShellNavigationContributor
{
    private static readonly ShellNavigationContribution MemoryContribution = new(
        ModuleId: "memory",
        ParentRoute: "/agents",
        Item: new ShellNavigationItem(
            "Memory Providers",
            "/memory",
            "psychology",
            "Provider profiles, health, capabilities, and generic memory operations.",
            PinnedByDefault: false),
        IsSubItem: false,
        Order: 30,
        DesignNote: "Generic memory providers are shown after live processes in the former Cognitive Memory navigation slot.");

    public IEnumerable<ShellNavigationContribution> GetShellNavigationContributions()
        => [MemoryContribution];
}
