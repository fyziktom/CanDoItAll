using System.Globalization;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace CanDoItAll.Mcp.DotNetWatch.Backend;

[SupportedOSPlatform("windows")]
internal static class WindowsDetachedBackendBootstrap
{
    private const uint CreateNewProcessGroup = 0x00000200;
    private const uint DetachedProcess = 0x00000008;
    private const uint CreateBreakawayFromJob = 0x01000000;

    public static void Launch(LaunchContext launchContext)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("The detached backend bootstrapper is only used on Windows.");
        }

        if (string.IsNullOrWhiteSpace(launchContext.BackendToken))
        {
            throw new InvalidOperationException("Backend launcher mode requires --backend-token.");
        }

        var entryAssemblyPath = Path.GetFullPath(Assembly.GetEntryAssembly()?.Location ?? Environment.ProcessPath ?? throw new InvalidOperationException("Could not resolve the launcher assembly path."));
        var (applicationPath, arguments) = ResolveBackendCommand(entryAssemblyPath, launchContext);
        var commandLine = BuildCommandLine(applicationPath, arguments);

        using var processClass = new ManagementClass("Win32_Process");
        using var startupClass = new ManagementClass("Win32_ProcessStartup");
        using var startupConfiguration = startupClass.CreateInstance() ?? throw new InvalidOperationException("Could not create Win32_ProcessStartup.");
        using var inputParameters = processClass.GetMethodParameters("Create");

        startupConfiguration["CreateFlags"] = DetachedProcess | CreateNewProcessGroup | CreateBreakawayFromJob;
        startupConfiguration["ShowWindow"] = 0;

        inputParameters["CommandLine"] = commandLine;
        inputParameters["CurrentDirectory"] = Environment.CurrentDirectory;
        inputParameters["ProcessStartupInformation"] = startupConfiguration;

        using var outputParameters = processClass.InvokeMethod("Create", inputParameters, null);
        var returnValue = Convert.ToUInt32(outputParameters?["returnValue"] ?? 0u, CultureInfo.InvariantCulture);
        if (returnValue != 0)
        {
            throw new InvalidOperationException($"Win32_Process.Create failed with return value {returnValue}.");
        }
    }

    private static (string ApplicationPath, IReadOnlyList<string> Arguments) ResolveBackendCommand(string entryAssemblyPath, LaunchContext launchContext)
    {
        if (entryAssemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            var dotnetPath = Path.GetFullPath(Environment.ProcessPath ?? throw new InvalidOperationException("Could not resolve the dotnet host path."));
            return (dotnetPath,
            [
                entryAssemblyPath,
                "--backend",
                "--settings",
                Path.GetFullPath(launchContext.SettingsPath),
                "--backend-token",
                launchContext.BackendToken!
            ]);
        }

        return (entryAssemblyPath,
        [
            "--backend",
            "--settings",
            Path.GetFullPath(launchContext.SettingsPath),
            "--backend-token",
            launchContext.BackendToken!
        ]);
    }

    private static string BuildCommandLine(string applicationPath, IReadOnlyList<string> arguments)
    {
        return string.Join(
            ' ',
            new[] { QuoteWindowsArgument(applicationPath) }.Concat(arguments.Select(QuoteWindowsArgument)));
    }

    private static string QuoteWindowsArgument(string argument)
    {
        if (argument.Length == 0)
        {
            return "\"\"";
        }

        var needsQuotes = argument.Any(static character => char.IsWhiteSpace(character) || character is '"' or '\\');
        if (!needsQuotes)
        {
            return argument;
        }

        var builder = new StringBuilder("\"");
        var backslashCount = 0;
        foreach (var character in argument)
        {
            if (character == '\\')
            {
                backslashCount++;
                continue;
            }

            if (character == '"')
            {
                builder.Append('\\', backslashCount * 2 + 1);
                builder.Append('"');
                backslashCount = 0;
                continue;
            }

            if (backslashCount > 0)
            {
                builder.Append('\\', backslashCount);
                backslashCount = 0;
            }

            builder.Append(character);
        }

        if (backslashCount > 0)
        {
            builder.Append('\\', backslashCount * 2);
        }

        builder.Append('"');
        return builder.ToString();
    }

}
