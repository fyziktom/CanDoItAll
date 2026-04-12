using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessDeletionIntegrationTests
{
    [Fact]
    public async Task DeleteAsync_removes_the_persisted_process_graph_and_search_document()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var seedService = scope.ServiceProvider.GetRequiredService<ProcessDevelopmentSeedService>();
        var projectId = await CreateProjectAsync(projectsService, "Process deletion project");
        var seedResult = await seedService.SeedBaselineAsync(projectId);

        Assert.True(seedResult.IsSuccess);

        var definitionId = seedResult.Value!.PrimaryDefinitionId;
        List<Guid> versionIds;
        List<Guid> roleIds;
        List<Guid> stepIds;
        List<Guid> runIds;

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            versionIds = await dbContext.Set<ProcessDefinitionVersion>()
                .Where(item => item.ProcessDefinitionId == definitionId)
                .Select(item => item.Id)
                .ToListAsync();
            roleIds = await dbContext.Set<ProcessRoleRequirement>()
                .Where(item => versionIds.Contains(item.ProcessDefinitionVersionId))
                .Select(item => item.Id)
                .ToListAsync();
            stepIds = await dbContext.Set<ProcessStepDefinition>()
                .Where(item => versionIds.Contains(item.ProcessDefinitionVersionId))
                .Select(item => item.Id)
                .ToListAsync();
            runIds = await dbContext.Set<ProcessRun>()
                .Where(item => item.ProcessDefinitionId == definitionId)
                .Select(item => item.Id)
                .ToListAsync();

            Assert.NotEmpty(versionIds);
            Assert.NotEmpty(roleIds);
            Assert.NotEmpty(stepIds);
            Assert.NotEmpty(runIds);
            Assert.True(await dbContext.Set<SearchDocument>()
                .AnyAsync(item => item.SourceType == "process-definition" && item.SourceKey == definitionId.ToString()));
        }

        await processesService.DeleteAsync(definitionId);

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        Assert.False(await verificationContext.Set<ProcessDefinition>().AnyAsync(item => item.Id == definitionId));
        Assert.False(await verificationContext.Set<ProcessDefinitionVersion>().AnyAsync(item => versionIds.Contains(item.Id)));
        Assert.False(await verificationContext.Set<ProcessRoleRequirement>().AnyAsync(item => roleIds.Contains(item.Id)));
        Assert.False(await verificationContext.Set<ProcessRoleSkillRequirement>().AnyAsync(item => roleIds.Contains(item.RoleRequirementId)));
        Assert.False(await verificationContext.Set<ProcessStepDefinition>().AnyAsync(item => stepIds.Contains(item.Id)));
        Assert.False(await verificationContext.Set<ProcessStepDependencyDefinition>().AnyAsync(item => stepIds.Contains(item.StepDefinitionId)));
        Assert.False(await verificationContext.Set<ProcessStepRoleAssignmentRequirement>().AnyAsync(item => stepIds.Contains(item.StepDefinitionId)));
        Assert.False(await verificationContext.Set<ProcessArtifactExpectation>().AnyAsync(item => stepIds.Contains(item.StepDefinitionId)));
        Assert.False(await verificationContext.Set<ProcessStepArtifactInputDefinition>().AnyAsync(item => stepIds.Contains(item.StepDefinitionId)));
        Assert.False(await verificationContext.Set<ProcessStepBranchOutcomeDefinition>().AnyAsync(item => stepIds.Contains(item.StepDefinitionId)));
        Assert.False(await verificationContext.Set<ProcessRun>().AnyAsync(item => runIds.Contains(item.Id)));
        Assert.False(await verificationContext.Set<ProcessStepRun>().AnyAsync(item => runIds.Contains(item.ProcessRunId)));
        Assert.False(await verificationContext.Set<ProcessRunAssignment>().AnyAsync(item => runIds.Contains(item.ProcessRunId)));
        Assert.False(await verificationContext.Set<ProcessWorkBrief>().AnyAsync(item => runIds.Contains(item.ProcessRunId)));
        Assert.False(await verificationContext.Set<ProcessDecisionRecord>().AnyAsync(item => runIds.Contains(item.ProcessRunId)));
        Assert.False(await verificationContext.Set<ProcessArtifactRecord>().AnyAsync(item => runIds.Contains(item.ProcessRunId)));
        Assert.False(await verificationContext.Set<ProcessJournalEntry>().AnyAsync(item => runIds.Contains(item.ProcessRunId)));
        Assert.False(await verificationContext.Set<ProcessConformanceObservation>().AnyAsync(item => runIds.Contains(item.ProcessRunId)));
        Assert.False(await verificationContext.Set<ProcessImprovementCandidate>().AnyAsync(item => item.ProcessDefinitionId == definitionId));
        Assert.False(await verificationContext.Set<SearchDocument>()
            .AnyAsync(item => item.SourceType == "process-definition" && item.SourceKey == definitionId.ToString()));
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = $"{name} description",
            Objective = $"{name} objective",
            CurrentPhase = "Execution"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
