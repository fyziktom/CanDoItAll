using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Tests.Integration.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class AgentFrameworkRuntimeSwitchingIntegrationTests
{
    [Fact]
    public async Task Agentframework_workspace_service_tracks_the_current_profile_after_runtime_switch()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-runtime-switch");
        var primaryTestProfile = testEnvironment.CreatePostgreSqlProfile("agentframework-primary");
        Guid primaryProfileId;
        await using (var setupProvider = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(testEnvironment))
        {
            var setupProfileService = setupProvider.GetRequiredService<IDatabaseProfileService>();
            var primarySaveResult = await setupProfileService.SaveAsync(
                TestDatabaseProfileEditorFactory.CreatePostgreSqlEditor(
                    primaryTestProfile,
                    "PostgreSQL primary switch source"));
            Assert.True(
                primarySaveResult.IsSuccess,
                string.Join(" ", primarySaveResult.Errors.Select(error => error.Message)));
            Assert.True((await setupProfileService.ActivateAsync(primarySaveResult.Value)).IsSuccess);
            primaryProfileId = primarySaveResult.Value;
        }

        await using var provider = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(testEnvironment);
        await using var scope = provider.CreateAsyncScope();

        var bootstrapper = scope.ServiceProvider.GetRequiredService<IAppDatabaseBootstrapper>();
        var profileService = scope.ServiceProvider.GetRequiredService<IDatabaseProfileService>();
        var runtimeAccessor = scope.ServiceProvider.GetRequiredService<IDatabaseProfileRuntimeAccessor>();
        var switchCoordinator = scope.ServiceProvider.GetRequiredService<IDatabaseSwitchCoordinator>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();

        var primaryResolvedProfile = runtimeAccessor.ResolveCurrentProfile();
        Assert.Equal(primaryProfileId, primaryResolvedProfile.Profile.Id);
        await bootstrapper.EnsureCurrentProfileReadyAsync();

        var primaryEditor = await workspaceService.GetAgentEditorAsync();
        primaryEditor.Name = "Primary Switch Agent";
        primaryEditor.RoleTitle = "Primary engineer";
        primaryEditor.Summary = "Exists only in the initial profile.";
        primaryEditor.Instructions = "Stay in the initial profile only.";
        primaryEditor.Status = AgentLifecycleStatus.Active;
        primaryEditor.IsTemplate = false;
        primaryEditor.TemplateKey = string.Empty;

        var primaryAgentId = await workspaceService.SaveAgentAsync(primaryEditor);

        var targetTestProfile = testEnvironment.CreatePostgreSqlProfile("agentframework-target");
        var saveResult = await profileService.SaveAsync(TestDatabaseProfileEditorFactory.CreatePostgreSqlEditor(
            targetTestProfile,
            "PostgreSQL switch target"));

        Assert.True(saveResult.IsSuccess);

        var targetProfile = runtimeAccessor.ResolveProfile(saveResult.Value);
        await bootstrapper.EnsureProfileReadyAsync(targetProfile);

        var switchResult = await switchCoordinator.SwitchAsync(targetProfile.Profile.Id);
        Assert.True(switchResult.IsSuccess);

        await using var restartedProvider = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(testEnvironment);
        await using var restartedScope = restartedProvider.CreateAsyncScope();
        var restartedBootstrapper = restartedScope.ServiceProvider.GetRequiredService<IAppDatabaseBootstrapper>();
        var restartedWorkspaceService = restartedScope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var restartedAiAgentService = restartedScope.ServiceProvider.GetRequiredService<AiAgentService>();

        await restartedBootstrapper.EnsureCurrentProfileReadyAsync();

        var targetEditor = await restartedWorkspaceService.GetAgentEditorAsync();
        targetEditor.Name = "Target Switch Agent";
        targetEditor.RoleTitle = "Target engineer";
        targetEditor.Summary = "Exists only in the switched profile.";
        targetEditor.Instructions = "Stay in the switched profile only.";
        targetEditor.Status = AgentLifecycleStatus.Active;
        targetEditor.IsTemplate = false;
        targetEditor.TemplateKey = string.Empty;

        var targetAgentId = await restartedWorkspaceService.SaveAgentAsync(targetEditor);

        var resolvedAgents = await restartedWorkspaceService.ListAgentsAsync(includeTemplates: false);

        Assert.DoesNotContain(resolvedAgents, item => item.Id == primaryAgentId);
        Assert.Contains(resolvedAgents, item => item.Id == targetAgentId && item.Name == "Target Switch Agent");

        var crmRoster = await restartedAiAgentService.ListAgentDirectoryAsync();

        Assert.DoesNotContain(crmRoster, item => item.TechnicalAgentId == primaryAgentId);
        Assert.Contains(crmRoster, item => item.TechnicalAgentId == targetAgentId && item.DisplayName == "Target Switch Agent");
    }
}
