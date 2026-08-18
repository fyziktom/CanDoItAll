using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Composition;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Drivers.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "ProcessCapabilityPortability")]
[Trait("Category", "UnixRuntimePortability")]
public sealed class ProcessCapabilityPortabilityIntegrationTests
{
    [Fact]
    public async Task Production_process_capability_source_reports_current_actual_host_without_side_effects()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddSingleton<IWorkspaceProcessHost, NonExecutingWorkspaceProcessHost>();
        services.AddSingleton<WorkspaceExecutableLocator>();
        services.AddRuntimeHostPlatformComposition(
            configuration,
            new ActualHostEnvironment(Environment.CurrentDirectory));
        services.AddSingleton<IHostCapabilitySnapshotProvider>(
            new CurrentHostCapabilitySnapshotProvider());
        services.AddProcessHostCapabilityRuntime();
        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var provider = scope.ServiceProvider.GetRequiredService<IProcessHostCapabilitySnapshotProvider>();

        var snapshot = await provider.GetAsync();

        Assert.Equal(ExpectedCurrentProfile(), snapshot.ProfileId);
        Assert.Equal(snapshot.Capabilities.Count, snapshot.Capabilities.Select(fact => fact.Id).Distinct().Count());
        Assert.Contains(snapshot.Capabilities, fact =>
            fact.Id == ProcessHostCapabilityIds.DirectExecution &&
            fact.IsAvailable &&
            fact.ExecutionPort == ProcessHostExecutionPort.ManagedProcessHost);
        Assert.Contains(snapshot.Capabilities, fact =>
            fact.Id == ProcessHostCapabilityIds.DotNetRuntime &&
            fact.IsAvailable &&
            fact.ExecutionPort == ProcessHostExecutionPort.ManagedProcessHost);
        Assert.Contains(snapshot.Capabilities, fact =>
            fact.Id == ProcessHostCapabilityIds.LocalStdioMcp &&
            fact.Availability == ProcessHostCapabilityAvailability.Unavailable &&
            fact.Reason == ProcessHostCapabilityReason.NotRegistered);
    }

    private sealed class ActualHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "CanDoItAll.B06.Integration";

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } =
            new PhysicalFileProvider(contentRootPath);
    }

    private static ProcessHostProfileId ExpectedCurrentProfile()
        => new(
            OperatingSystem.IsWindows()
                ? "windows"
                : OperatingSystem.IsLinux()
                    ? "linux"
                    : OperatingSystem.IsMacOS()
                        ? "macos"
                        : "unknown");

    private sealed class NonExecutingWorkspaceProcessHost : IWorkspaceProcessHost
    {
        public ExecutionBoundaryDescriptor DescribeBoundary() => ExecutionBoundaryDescriptor.Unknown;

        public Task<WorkspaceProcessExecutionResult> ExecuteAsync(
            WorkspaceProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Capability discovery must not start a process.");
    }

    private sealed class CurrentHostCapabilitySnapshotProvider : IHostCapabilitySnapshotProvider
    {
        public HostCapabilitySnapshot GetSnapshot() => new(
            RuntimeHostProfileKind.Test,
            OperatingSystem.IsWindows()
                ? RuntimeHostOperatingSystem.Windows
                : OperatingSystem.IsLinux()
                    ? RuntimeHostOperatingSystem.Linux
                    : OperatingSystem.IsMacOS()
                        ? RuntimeHostOperatingSystem.MacOs
                        : throw new PlatformNotSupportedException("The integration profile requires Windows, Linux, or macOS."),
            IsInteractive: false,
            IsReady: true,
            DateTimeOffset.UtcNow,
            [],
            []);
    }
}
