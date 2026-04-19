using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class AgentFrameworkRuntimeSwitchingIntegrationTests
{
    [Fact]
    public async Task Agentframework_workspace_service_tracks_the_current_profile_after_runtime_switch()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-runtime-switch");
        await using var provider = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(testEnvironment);
        await using var scope = provider.CreateAsyncScope();

        var bootstrapper = scope.ServiceProvider.GetRequiredService<IAppDatabaseBootstrapper>();
        var profileService = scope.ServiceProvider.GetRequiredService<IDatabaseProfileService>();
        var runtimeAccessor = scope.ServiceProvider.GetRequiredService<IDatabaseProfileRuntimeAccessor>();
        var switchCoordinator = scope.ServiceProvider.GetRequiredService<IDatabaseSwitchCoordinator>();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var aiAgentService = scope.ServiceProvider.GetRequiredService<AiAgentService>();

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

        var saveResult = await profileService.SaveAsync(new DatabaseProfileEditorModel
        {
            DisplayName = "Managed sqlite switch target",
            ProviderKind = DatabaseProviderKind.Sqlite,
            SourceKind = DatabaseProfileSourceKind.ManagedSqlite
        });

        Assert.True(saveResult.IsSuccess);

        var targetProfile = runtimeAccessor.ResolveProfile(saveResult.Value);
        await bootstrapper.EnsureProfileReadyAsync(targetProfile);

        var switchResult = await switchCoordinator.SwitchAsync(targetProfile.Profile.Id);
        Assert.True(switchResult.IsSuccess);

        var currentWorkspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var targetEditor = await currentWorkspaceService.GetAgentEditorAsync();
        targetEditor.Name = "Target Switch Agent";
        targetEditor.RoleTitle = "Target engineer";
        targetEditor.Summary = "Exists only in the switched profile.";
        targetEditor.Instructions = "Stay in the switched profile only.";
        targetEditor.Status = AgentLifecycleStatus.Active;
        targetEditor.IsTemplate = false;
        targetEditor.TemplateKey = string.Empty;

        var targetAgentId = await currentWorkspaceService.SaveAgentAsync(targetEditor);

        var resolvedAgents = await workspaceService.ListAgentsAsync(includeTemplates: false);

        Assert.DoesNotContain(resolvedAgents, item => item.Id == primaryAgentId);
        Assert.Contains(resolvedAgents, item => item.Id == targetAgentId && item.Name == "Target Switch Agent");

        var crmRoster = await aiAgentService.ListAgentDirectoryAsync();

        Assert.DoesNotContain(crmRoster, item => item.TechnicalAgentId == primaryAgentId);
        Assert.Contains(crmRoster, item => item.TechnicalAgentId == targetAgentId && item.DisplayName == "Target Switch Agent");
    }
}
