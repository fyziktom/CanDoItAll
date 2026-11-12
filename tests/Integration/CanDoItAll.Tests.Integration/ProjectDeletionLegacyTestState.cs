using System.Text.Json;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration;

internal static class ProjectDeletionLegacyTestState {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static async Task<ProjectDeletionPreparationContext> SeedAsync(DatasetActivationTestFixture fixture, bool completed = false) {
        var binding = await fixture.Coordinator.EnsureReadyAsync(fixture.Profile);
        var original = new ProjectDeletionPreparationContext(new(fixture.Profile.Profile.Id, binding.Namespace, new(Guid.NewGuid())),
            new(fixture.Profile.Profile.Id, Guid.NewGuid(), Guid.NewGuid()));
        await using var context = fixture.Projects();
        await SeedAsync(context, original, completed);
        return original;
    }

    internal static async Task SeedAsync(DbContext context, ProjectDeletionPreparationContext original, bool completed = false) {
        var now = DateTimeOffset.UtcNow;
        context.Add(new ProjectRetirementRecord { ProjectId = original.Project.ProjectId, LifetimeId = original.Project.LifetimeId, RetiredAtUtc = now });
        context.Add(new AgentProjectStructureAccessRevocationRecord {
            Id = Guid.NewGuid(), ProjectId = original.Project.ProjectId, DatabaseProfileId = original.Project.DatabaseProfileId,
            ProjectLifetimeId = original.Project.LifetimeId, Status = completed ? AgentProjectStructureAccessRevocationStatus.Completed : AgentProjectStructureAccessRevocationStatus.Processing,
            AttemptCount = 3, CreatedAtUtc = now, UpdatedAtUtc = now, LastAttemptAtUtc = now,
            CompletedAtUtc = completed ? now : null, LastFailureCode = "retained-native-code"
        });
        context.Add(new ProjectCrossModuleMutationRecord {
            ProjectId = original.Project.ProjectId, MutationKind = ProjectCrossModuleMutationKind.DeleteProject,
            ScopeNodeKey = ProjectWorkbenchDeletionParticipant.ProjectDeletionScopeNodeKey,
            Status = completed ? ProjectCrossModuleMutationStatus.Completed : ProjectCrossModuleMutationStatus.Processing,
            PayloadJson = JsonSerializer.Serialize(new DeleteProjectMutationPayload([], [], SourceReference: ProjectAssignmentReference.From(original.Project)), Json),
            ErrorMessage = "processing:retained-claim", AttemptCount = 7, CreatedAtUtc = now, UpdatedAtUtc = now,
            LastAttemptAtUtc = now, CompletedAtUtc = completed ? now : null
        });
        await context.SaveChangesAsync();
    }

    internal static async Task<string> NativeStateAsync(DatasetActivationTestFixture fixture) {
        await using var context = fixture.Projects();
        return await NativeStateAsync(context);
    }

    internal static async Task<string> NativeStateAsync(DbContext context) {
        var agents = await context.Set<AgentProjectStructureAccessRevocationRecord>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync();
        var work = await context.Set<ProjectCrossModuleMutationRecord>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync();
        foreach (var row in agents) {
            row.CompletionId = null;
            row.CompletionNamespaceId = null;
        }
        foreach (var row in work) {
            row.CompletionId = null;
            row.CompletionNamespaceId = null;
        }
        return JsonSerializer.Serialize(new { agents, work }, Json);
    }

}
