using CanDoItAll.FileTools.Desktop;
using Microsoft.Extensions.Options;

namespace CanDoItAll.FileTools.Integration;

public sealed class FileToolsDesktopLaunchOptions
{
    public const string SectionName = "FileTools:DesktopLaunch";

    public bool Enabled { get; set; }
}

internal sealed class ConfiguredDesktopFileLauncher(
    IOptions<FileToolsDesktopLaunchOptions> options) : IDesktopFileLauncher
{
    private readonly DesktopFileLauncher launcher = new();

    public bool IsAvailable => options.Value.Enabled && launcher.IsAvailable;

    public ValueTask<DesktopFileLaunchResult> LaunchAsync(
        DesktopFileLaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!options.Value.Enabled)
        {
            return ValueTask.FromResult(DesktopFileLaunchResult.Failed(
                DesktopFileLaunchFailureCode.DesktopUnavailable,
                "Desktop file launching is disabled by the host."));
        }

        return launcher.LaunchAsync(request, cancellationToken);
    }
}
