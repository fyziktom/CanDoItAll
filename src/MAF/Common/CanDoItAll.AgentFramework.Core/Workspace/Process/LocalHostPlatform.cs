namespace CanDoItAll.AgentFramework.Core;

internal enum LocalHostPlatform
{
    Windows,
    Linux,
    MacOS
}

internal static class LocalHostPlatformExtensions
{
    public static LocalHostPlatform CaptureCurrent()
        => OperatingSystem.IsWindows()
            ? LocalHostPlatform.Windows
            : OperatingSystem.IsLinux()
                ? LocalHostPlatform.Linux
                : OperatingSystem.IsMacOS()
                    ? LocalHostPlatform.MacOS
                    : throw new PlatformNotSupportedException(
                        "Local process execution is supported only on Windows, Linux, and macOS.");

    public static StringComparer EnvironmentNameComparer(this LocalHostPlatform platform)
        => platform == LocalHostPlatform.Windows
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
}
