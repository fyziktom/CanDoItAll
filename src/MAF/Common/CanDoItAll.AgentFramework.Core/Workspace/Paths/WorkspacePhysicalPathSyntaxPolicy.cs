using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Core;

internal static class WorkspacePhysicalPathSyntaxPolicy
{
    public static PhysicalPathSyntax Classify(string path)
    {
        return PhysicalPathSyntaxClassifier.Classify(path);
    }

    public static void EnsureNativeOrRelative(string path)
    {
        var syntax = Classify(path);
        var supported = syntax switch
        {
            PhysicalPathSyntax.Relative => true,
            PhysicalPathSyntax.UnixAbsolute => !OperatingSystem.IsWindows(),
            PhysicalPathSyntax.WindowsDriveAbsolute or
                PhysicalPathSyntax.WindowsUnc or
                PhysicalPathSyntax.WindowsDevice => OperatingSystem.IsWindows(),
            _ => false
        };
        if (supported)
        {
            return;
        }

        throw WorkspacePathResolutionException.ForeignHostPath(
            $"The requested path uses {syntax} syntax that is not valid on this host and requires explicit rebind or migration.");
    }

}
