using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.ControlPlane;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.AgentFramework;

/// <summary>
/// Validates <see cref="ProcessEnvironmentOptions"/> and applies them to every process launcher at
/// startup. Invalid names are logged and dropped; nothing here can stop the host from starting.
/// </summary>
internal sealed class ProcessEnvironmentSettingsInitializer(
    IOptions<ProcessEnvironmentOptions> options,
    IServiceProvider services,
    ILogger<ProcessEnvironmentSettingsInitializer> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = WorkspaceProcessEnvironmentSettings.FromOptions(
            options.Value,
            ResolveDefaultToolchainHome(),
            out var rejectedNames);
        foreach (var name in rejectedNames)
        {
            logger.LogError(
                "Process environment option '{Section}:AdditionalInheritedNames' contains '{Name}', which was not added: names that look like secrets or that inject code into a runtime are never passed to processes.",
                ProcessEnvironmentOptions.SectionName,
                name);
        }

        WorkspaceProcessEnvironmentSettings.Configure(settings);
        logger.LogInformation(
            "Process environment: network trust {NetworkTrust}, {AdditionalNameCount} additional inherited name(s), toolchain home '{ToolchainHome}', read-only .NET output redirect {RedirectReadOnlyDotnetOutput}.",
            settings.NetworkTrust,
            settings.AdditionalInheritedNames.Count,
            settings.ToolchainHomePath ?? "(none)",
            settings.RedirectReadOnlyDotnetOutput);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private string? ResolveDefaultToolchainHome()
    {
        if (services.GetService(typeof(IControlPlanePathResolver)) is not IControlPlanePathResolver paths)
        {
            return null;
        }

        try
        {
            return Path.Combine(paths.ResolveStateRootPath(), "toolchain-home");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            logger.LogWarning(exception, "The application state root could not be resolved, so processes get no toolchain home when the host has no HOME.");
            return null;
        }
    }
}
