using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Manager;

internal static class ManagerProcessDiscoveryFactory
{
    public static IManagerProcessDiscovery Create(IWorkspaceProcessHost processHost)
    {
        ArgumentNullException.ThrowIfNull(processHost);
        if (OperatingSystem.IsWindows())
        {
            return new WindowsManagerProcessDiscovery();
        }

        if (OperatingSystem.IsLinux())
        {
            return new LinuxManagerProcessDiscovery();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacOsManagerProcessDiscovery(
                new B01MacProcessCommandRunner(processHost),
                new LibProcMacProcessIdentityReader());
        }

        throw new PlatformNotSupportedException(
            "Manager process recovery is supported only on Windows, Linux, and macOS.");
    }
}
