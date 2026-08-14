using CanDoItAll.FileTools.Desktop;
using Microsoft.Extensions.Options;

namespace CanDoItAll.FileTools.Integration;

public sealed class FileToolsDesktopLaunchOptions
{
    public const string SectionName = "FileTools:DesktopLaunch";

    public bool Enabled { get; set; }

    public bool HostProfileAllowsDesktop { get; set; }
}

internal sealed class ConfiguredDesktopFileLauncher : IDesktopFileLauncher
{
    private readonly IOptions<FileToolsDesktopLaunchOptions> options;
    private readonly IDesktopFileLauncher launcher;

    public ConfiguredDesktopFileLauncher(IOptions<FileToolsDesktopLaunchOptions> options)
        : this(options, new DesktopFileLauncher())
    {
    }

    internal ConfiguredDesktopFileLauncher(
        IOptions<FileToolsDesktopLaunchOptions> options,
        IDesktopFileLauncher launcher)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
    }

    public bool IsAvailable => IsEnabledForHost && launcher.IsAvailable;

    public ValueTask<DesktopFileLaunchResult> LaunchAsync(
        DesktopFileLaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsEnabledForHost)
        {
            return ValueTask.FromResult(DesktopFileLaunchResult.Failed(
                DesktopFileLaunchFailureCode.DesktopUnavailable,
                "Desktop file launching is disabled by the runtime host profile."));
        }

        return launcher.LaunchAsync(request, cancellationToken);
    }

    private bool IsEnabledForHost
        => options.Value.Enabled &&
           options.Value.HostProfileAllowsDesktop;
}
