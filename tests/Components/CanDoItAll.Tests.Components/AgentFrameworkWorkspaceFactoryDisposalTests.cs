using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentFrameworkWorkspaceFactoryDisposalTests
{
    [Fact]
    public async Task Dispose_releases_cached_workspaces_and_rejects_reuse()
    {
        await using var environment = CanDoItAllTestEnvironment.Create(
            "agent-workspace-factory-disposal");
        var profile = environment.CreateInMemoryProfile("primary");
        var configuration = TestApplicationBootstrap.BuildConfiguration(profile);
        var services = new ServiceCollection();
        TestApplicationBootstrap.ConfigureDefaultServices(
            services,
            configuration,
            environment.CreateHostEnvironment(
                "CanDoItAll.AgentFrameworkWorkspaceFactoryDisposalTests"));
        await using var serviceProvider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
        await using var serviceScope = serviceProvider.CreateAsyncScope();
        var workspaceFactory = serviceScope.ServiceProvider
            .GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceScope = workspaceFactory.GetOrganizationScope();
        var workspaceService = workspaceFactory.GetWorkspaceService(workspaceScope);
        EventHandler<ExecutionLogEntry> handler = static (_, _) => { };

        ((IDisposable)workspaceFactory).Dispose();

        Assert.Throws<ObjectDisposedException>(
            () => workspaceFactory.GetWorkspaceService(workspaceScope));
        Assert.Throws<ObjectDisposedException>(
            workspaceFactory.GetOrganizationWorkspaceService);
        Assert.Throws<ObjectDisposedException>(
            () => workspaceService.ExecutionUpdated += handler);

        ((IDisposable)workspaceFactory).Dispose();
    }

    [Fact]
    public async Task Profile_change_disposes_superseded_cached_workspace()
    {
        await using var environment = CanDoItAllTestEnvironment.Create(
            "agent-workspace-factory-profile-change");
        var profile = environment.CreateInMemoryProfile("primary");
        var configuration = TestApplicationBootstrap.BuildConfiguration(profile);
        var services = new ServiceCollection();
        TestApplicationBootstrap.ConfigureDefaultServices(
            services,
            configuration,
            environment.CreateHostEnvironment(
                "CanDoItAll.AgentFrameworkWorkspaceFactoryProfileChangeTests"));
        await using var serviceProvider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
        await using var serviceScope = serviceProvider.CreateAsyncScope();
        var workspaceFactory = serviceScope.ServiceProvider
            .GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetWorkspaceService(
            workspaceFactory.GetOrganizationScope());
        var runtimeSnapshot = serviceScope.ServiceProvider
            .GetRequiredService<IDatabaseRuntimeState>()
            .GetSnapshot();
        var notifications = serviceScope.ServiceProvider
            .GetRequiredService<IDatabaseSwitchNotificationService>();

        notifications.Publish(
            new DatabaseProfileChangedNotification(
                runtimeSnapshot.ActiveProfileId,
                runtimeSnapshot.ActiveFingerprint,
                Guid.NewGuid(),
                "replacement-profile",
                runtimeSnapshot.Generation + 1));

        EventHandler<ExecutionLogEntry> handler = static (_, _) => { };
        Assert.Throws<ObjectDisposedException>(
            () => workspaceService.ExecutionUpdated += handler);
    }
}
