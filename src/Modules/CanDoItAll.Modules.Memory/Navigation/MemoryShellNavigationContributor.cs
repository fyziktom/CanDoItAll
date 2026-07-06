using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Memory;

public sealed class MemoryShellNavigationContributor : IShellNavigationContributor
{
    private static readonly ShellNavigationContribution MemoryContribution = new(
        ModuleId: "memory",
        ParentRoute: "/",
        Item: new ShellNavigationItem(
            "Memory",
            "/memory",
            "memory",
            "Provider profiles, health, capabilities, and generic memory operations.",
            PinnedByDefault: false),
        IsSubItem: false,
        Order: 15,
        DesignNote: "Generic memory is contributed as an independent module entry before provider-specific memory pages.");

    public IEnumerable<ShellNavigationContribution> GetShellNavigationContributions()
        => [MemoryContribution];
}
