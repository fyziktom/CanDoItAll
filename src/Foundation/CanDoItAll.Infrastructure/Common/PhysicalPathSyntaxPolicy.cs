using CanDoItAll.SharedKernel;

namespace CanDoItAll.Infrastructure;

public static class PhysicalPathSyntaxPolicy
{
    public static PhysicalPathSyntax Classify(string path)
    {
        return PhysicalPathSyntaxClassifier.Classify(path);
    }

    public static void EnsureNativeOrRelative(string path, string description)
    {
        var syntax = Classify(path);
        if (IsNativeOrRelative(syntax, OperatingSystem.IsWindows()))
        {
            return;
        }

        throw new InvalidOperationException(
            $"The {description} uses {syntax} syntax that is not valid on this host. The path is host-bound and requires explicit rebind or migration.");
    }

    internal static bool IsNativeOrRelative(PhysicalPathSyntax syntax, bool isWindowsHost)
    {
        return syntax switch
        {
            PhysicalPathSyntax.Relative => true,
            PhysicalPathSyntax.UnixAbsolute => !isWindowsHost,
            PhysicalPathSyntax.WindowsDriveAbsolute or
                PhysicalPathSyntax.WindowsUnc or
                PhysicalPathSyntax.WindowsDevice => isWindowsHost,
            _ => false
        };
    }

}
