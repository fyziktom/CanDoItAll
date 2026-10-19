using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectDeletionLegacyCommandIntegrationTests {
    [Fact]
    public async Task Real_offline_legacy_commands_hold_native_dispatch_until_release_then_complete_the_original_work() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await OfflineMaintenanceCommandFixture.PersistProfileAsync(services);
        var profile = services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        var selected = await services.GetRequiredService<DatasetActivationService>().ObserveActiveBindingAsync(profile.Profile.Id);
        var original = new ProjectDeletionPreparationContext(new(profile.Profile.Id, selected.Record.Plan.Binding.Namespace, new(Guid.NewGuid())),
            new(profile.Profile.Id, Guid.NewGuid(), Guid.NewGuid()));
        string root = profile.Profile.Storage.WorkspaceRoot;
        Assert.False(string.IsNullOrWhiteSpace(root));
        string relative = Path.GetRelativePath(application.RootPath, root);
        Assert.False(Path.IsPathFullyQualified(relative) || relative.StartsWith("..", StringComparison.Ordinal));
        Directory.CreateDirectory(WorkspaceScopeDescriptor.Organization(profile.Profile.Id.ToString("N")).ResolveDataRoot(root));
        var factory = services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        Guid agentId;
        Guid workbenchId;
        await using (var context = await factory.CreateDbContextAsync()) {
            await ProjectDeletionLegacyTestState.SeedAsync(context, original);
            var agent = await context.Set<AgentProjectStructureAccessRevocationRecord>().SingleAsync();
            agent.Status = AgentProjectStructureAccessRevocationStatus.Pending;
            agentId = agent.Id;
            var work = await context.Set<ProjectCrossModuleMutationRecord>().SingleAsync();
            work.Status = ProjectCrossModuleMutationStatus.WorkbenchCommitted;
            work.ErrorMessage = "retained-diagnostic";
            workbenchId = work.Id;
            await context.SaveChangesAsync();
        }
        string before = await NativeAsync();
        var inspected = await LegacyAsync("inspect-legacy");
        Assert.Equal("LegacyObserved", inspected.GetProperty("status").GetString());
        string digest = inspected.GetProperty("result").GetProperty("plan").GetProperty("digest").GetProperty("hex").GetString()!;
        var installed = await LegacyAsync("install-legacy", digest);
        Assert.Equal("InstalledAwaitingRelease", installed.GetProperty("status").GetString());
        Assert.Equal(before, await NativeAsync());
        Assert.Equal("InstalledAwaitingRelease", (await LegacyAsync("install-legacy", digest)).GetProperty("status").GetString());
        var participants = services.GetServices<IProjectDeletionParticipant>().ToDictionary(item => item.Id);
        var obligations = new[] {
            new ProjectDeletionObligation(new(AgentProjectStructureAccessDeletionParticipant.ParticipantIdValue), agentId),
            new ProjectDeletionObligation(new(ProjectWorkbenchDeletionParticipant.ParticipantIdValue), workbenchId)
        };
        var completion = new ProjectDeletionCompletionBinding(original, obligations);
        var query = services.GetRequiredService<IProjectDeletionCompletionQuery>();
        Assert.IsType<ProjectDeletionCompletionObservation.AwaitingOwnerRelease>(await query.ObserveAsync(completion));
        foreach (var obligation in obligations) {
            await Assert.ThrowsAsync<ProjectDeletionParticipantCleanupException>(() => participants[obligation.Participant]
                .CompleteAsync(new(original.Project.ProjectId, obligation.RecoveryId)));
        }
        await Assert.ThrowsAsync<ProjectDeletionParticipantCleanupException>(() => services.GetRequiredService<ProjectCrossModuleMutationProcessor>()
            .ProcessAsync(workbenchId));
        Assert.Equal(before, await NativeAsync());
        var releasePlan = await OfflineMaintenanceCommandFixture.RunAsync(application, original.Completion, "inspect");
        string releaseDigest = releasePlan.GetProperty("result").GetProperty("plan").GetProperty("digest").GetProperty("hex").GetString()!;
        Assert.Equal("Released", (await OfflineMaintenanceCommandFixture.RunAsync(application, original.Completion, "reconcile", releaseDigest))
            .GetProperty("status").GetString());
        Assert.IsType<ProjectDeletionCompletionObservation.Released>(await query.ObserveAsync(completion));
        Assert.Equal(before, await NativeAsync());
        foreach (var obligation in obligations) {
            var completed = await participants[obligation.Participant].CompleteAsync(new(original.Project.ProjectId, obligation.RecoveryId));
            Assert.Equal(obligation.RecoveryId, completed.RecoveryId);
        }
        await using (var context = await factory.CreateDbContextAsync()) {
            Assert.Equal(AgentProjectStructureAccessRevocationStatus.Completed,
                (await context.Set<AgentProjectStructureAccessRevocationRecord>().SingleAsync()).Status);
            Assert.Equal(ProjectCrossModuleMutationStatus.Completed,
                (await context.Set<ProjectCrossModuleMutationRecord>().SingleAsync()).Status);
            Assert.Single(await context.Set<ProjectDeletionCompletionRecord>().ToArrayAsync());
            Assert.Empty(await context.Set<Project>().Where(item => item.Id == original.Project.ProjectId).ToArrayAsync());
        }
        string completedState = await NativeAsync();
        Assert.Equal("Released", (await LegacyAsync("install-legacy", digest)).GetProperty("status").GetString());
        Assert.Equal(completedState, await NativeAsync());

        async Task<string> NativeAsync() {
            await using var context = await factory.CreateDbContextAsync();
            return await ProjectDeletionLegacyTestState.NativeStateAsync(context);
        }

        Task<JsonElement> LegacyAsync(string action, string? plan = null) {
            List<string> arguments = ["project-deletion-recovery", action, "--profile-id", original.Project.DatabaseProfileId.ToString("D"),
                "--expected-namespace", original.Completion.Namespace.Value.ToString("D"), "--completion-id", original.Completion.Id.Value.ToString("D"),
                "--project-id", original.Project.ProjectId.ToString("D"), "--lifetime-id", original.Project.LifetimeId.ToString("D")];
            if (plan is not null) {
                arguments.AddRange(["--expected-plan-digest", plan, "--writers-stopped", "--former-source-hosts-stopped"]);
            }
            return OfflineMaintenanceCommandFixture.RunAsync(application, original.Project.DatabaseProfileId, arguments);
        }
    }
}
