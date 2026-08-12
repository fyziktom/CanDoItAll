using CanDoItAll.AgentFramework.Core;
using CanDoItAll.FileTools.Desktop;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "UnixRuntimePortability")]
public sealed class PluginDesktopPortabilityIntegrationTests
{
    [Fact]
    [Trait("Category", "PluginPortability")]
    public async Task Docker_probe_uses_the_real_canonical_host_and_reports_each_dependency()
    {
        string repositoryRoot = FindRepositoryRoot();
        var service = new DockerHostToolService(
            new StaticWorkspacePathResolver(repositoryRoot),
            new LocalWorkspaceProcessHost(),
            new WorkspaceExecutableLocator(),
            new WorkspaceCommandEnvironmentPolicy(),
            new PhysicalFileSystemPathPolicyFactory(),
            NullLogger<DockerHostToolService>.Instance);

        DockerHostCapabilitySnapshot snapshot = await service.ProbeAsync();

        Assert.True(Enum.IsDefined(snapshot.Executable));
        Assert.True(Enum.IsDefined(snapshot.Context));
        Assert.True(Enum.IsDefined(snapshot.Daemon));
        Assert.True(Enum.IsDefined(snapshot.EndpointKind));
        Assert.False(string.IsNullOrWhiteSpace(snapshot.Message));
        if (string.Equals(
                Environment.GetEnvironmentVariable("CANDOITALL_REQUIRE_DOCKER_INTEGRATION"),
                "1",
                StringComparison.Ordinal))
        {
            Assert.True(snapshot.IsReady, snapshot.Message);
        }
    }

    [Fact]
    [Trait("Category", "DesktopPortability")]
    public async Task Headless_host_policy_disables_desktop_launch_before_the_package_adapter_runs()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileTools:DesktopLaunch:Enabled"] = "true"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddCanDoItAllFileToolsIntegration();
        services.PostConfigure<FileToolsDesktopLaunchOptions>(options =>
            options.HostProfileAllowsDesktop = false);
        await using ServiceProvider provider = services.BuildServiceProvider();
        IDesktopFileLauncher launcher = provider.GetRequiredService<IDesktopFileLauncher>();
        var request = new DesktopFileLaunchRequest(
            Path.Combine(Path.GetTempPath(), "headless-no-launch.txt"));

        DesktopFileLaunchResult result = await launcher.LaunchAsync(request);

        Assert.False(launcher.IsAvailable);
        Assert.False(result.Succeeded);
        Assert.Equal(DesktopFileLaunchFailureCode.DesktopUnavailable, result.Failure?.Code);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private sealed class StaticWorkspacePathResolver(string workspaceRoot) : IWorkspacePathResolver
    {
        public string ResolveWorkspaceRoot() => workspaceRoot;

        public string ResolveManagedFilesRoot() => workspaceRoot;

        public string ResolveExportsRoot() => workspaceRoot;

        public string ResolveEvidenceRoot() => workspaceRoot;

        public string ResolveManagerArtifactsRoot() => workspaceRoot;
    }
}
