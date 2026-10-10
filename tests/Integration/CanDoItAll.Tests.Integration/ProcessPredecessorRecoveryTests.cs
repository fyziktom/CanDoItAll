using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "HostPlatform")]
public sealed class ProcessPredecessorRecoveryTests {
    [Theory]
    [InlineData(false, ProcessRuntimeStatus.Active)]
    [InlineData(false, ProcessRuntimeStatus.Completed)]
    [InlineData(false, ProcessRuntimeStatus.Blocked)]
    [InlineData(false, ProcessRuntimeStatus.Failed)]
    [InlineData(false, ProcessRuntimeStatus.Cancelled)]
    [InlineData(true, ProcessRuntimeStatus.Active)]
    [InlineData(true, ProcessRuntimeStatus.Completed)]
    [InlineData(true, ProcessRuntimeStatus.Blocked)]
    [InlineData(true, ProcessRuntimeStatus.Failed)]
    public async Task Full_native_subprocess_reentry_reuses_proven_predecessors_and_refuses_unproven_origins_without_new_children(
        bool knownOrigin, ProcessRuntimeStatus status) {
        await using var app = await TestApplication.CreateAsync(new());
        Guid projectId = Guid.NewGuid();
        string nodeId;
        ProcessRuntimeStateSnapshot parent;
        ProcessRuntimeStateSnapshot child;
        ProcessStepInstanceId parentStep;
        string assignmentsBefore;
        string linksBefore;
        await using (var setup = app.Services.CreateAsyncScope()) {
            var services = setup.ServiceProvider;
            Assert.True((await services.GetRequiredService<ProjectsService>().CreateAsync(projectId, new() { Name = "Predecessor process recovery" })).IsSuccess);
            var node = await services.GetRequiredService<ProjectWorkbenchService>().CreateObjectAsync(projectId,
                new ProjectObjectCreateRequest(ProjectObjectType.ProjectBlock, "Retained source", "Recovery source", "Original context", $"project:{projectId:D}", 300, 200));
            nodeId = node.Id;
            var current = (await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(projectId))!;
            ProcessProjectAdmission? admission = knownOrigin ? new(current.DatabaseProfileId, current.ProjectId, current.LifetimeId) : null;
            await using var database = await services.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>().CreateDbContextAsync();
            var source = new Dictionary<string, string> {
                [ProcessRuntimeLaunchVariables.ProjectId] = projectId.ToString("D"),
                [ProcessRuntimeLaunchVariables.ProjectNodeId] = nodeId
            };
            parent = await ProcessPredecessorFixture.SeedRetainedAsync(database, true,
                new(current.DatabaseProfileId, current.ProjectId, current.LifetimeId), ProcessRuntimeStatus.Active, source);
            var assigned = await services.GetRequiredService<IProcessRuntimeStepAssignmentStore>().LoadByRunAsync(parent.RunId);
            parentStep = Assert.Single(assigned, item => item.AllowedOperations.Contains(ProcessOperationContractNames.ExecuteExternalAction)).StepInstanceId;
            source[ProcessRuntimeLaunchVariables.ParentProcessRunId] = parent.RunId.ToString();
            source[ProcessRuntimeLaunchVariables.ParentProcessStepId] = parentStep.ToString();
            source[ProcessRuntimeLaunchVariables.SubprocessDefinitionKey] = ProcessPredecessorFixture.DefinitionKey;
            child = await ProcessPredecessorFixture.SeedRetainedAsync(database, false, admission, status, source, parent.RootRunId);
            assignmentsBefore = await AssignmentJsonAsync(database);
            var workbench = services.GetRequiredService<ProjectWorkbenchService>();
            var childNode = ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(child.RunId.Value);
            await workbench.LinkObjectsAsync(projectId, nodeId, childNode, ProjectObjectLinkKind.Uses);
            await workbench.LinkObjectsAsync(projectId, ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(parent.RunId.Value), childNode, ProjectObjectLinkKind.Uses);
            linksBefore = await LinkJsonAsync(services, projectId);
        }

        for (var attempt = 0; attempt < 2; attempt++) {
            await using var independent = app.Services.CreateAsyncScope();
            var services = independent.ServiceProvider;
            var launch = services.GetRequiredService<ProjectStructureProcessNodeService>();
            var operation = launch.StartSubprocessAsync(projectId, parent.RunId.ToString(), parentStep.ToString(),
                new ProjectStructureProcessSubprocessLaunchInput("  DOTNET-RUNTIME-COMMAND-WRITEBACK  ", RunHrMatch: false, Execute: false, IncludeLaunchPlan: true),
                new("predecessor-fixture", "Native recovery operator", "fixture-host", string.Empty, string.Empty, "retained-reentry"));
            if (knownOrigin) {
                var recovered = await operation;
                Assert.Equal(child.RunId.Value, recovered.RunId);
                Assert.Equal(child.PlanId.Value, recovered.LaunchPlanId);
                Assert.Equal(ProcessPredecessorFixture.DefinitionKey, recovered.DefinitionKey);
                Assert.Contains(child.RunId.Value.ToString("D"), recovered.ChildLiveProcessesRoute, StringComparison.Ordinal);
            } else {
                var error = await Assert.ThrowsAsync<ProcessExistingLaunchReconciliationRequiredException>(() => operation);
                Assert.Contains("origin", error.Message, StringComparison.OrdinalIgnoreCase);
            }
            await using var database = await services.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>().CreateDbContextAsync();
            Assert.Equal(2, await database.RuntimeStates.CountAsync());
            Assert.Equal(2, await database.InstancePlans.CountAsync());
            Assert.Equal(ProcessPredecessorFixture.PlanJson, (await database.InstancePlans.SingleAsync(item => item.PlanId == child.PlanId.Value)).PayloadJson);
            Assert.Equal(ProcessPredecessorFixture.ParentPlanJson, (await database.InstancePlans.SingleAsync(item => item.PlanId == parent.PlanId.Value)).PayloadJson);
            Assert.Equal(assignmentsBefore, await AssignmentJsonAsync(database));
            Assert.Equal(linksBefore, await LinkJsonAsync(services, projectId));
            Assert.Equal(parent.RootRunId, (await services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(child.RunId))!.RootRunId);
            Assert.Empty(await database.PreparedLaunches.ToArrayAsync());
        }
    }

    [Fact]
    public async Task Native_lookup_preserves_known_predecessor_scope_after_independent_reads_and_project_recreation() {
        await using var app = await TestApplication.CreateAsync(new());
        var projectId = Guid.NewGuid();
        ProcessRuntimeStateSnapshot retained;
        await using (var seed = app.Services.CreateAsyncScope()) {
            Assert.True((await seed.ServiceProvider.GetRequiredService<ProjectsService>().CreateAsync(projectId, new() { Name = "Original legacy lifetime" })).IsSuccess);
            var current = (await seed.ServiceProvider.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(projectId))!;
            await using var database = await seed.ServiceProvider.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>().CreateDbContextAsync();
            retained = await ProcessPredecessorFixture.SeedRetainedAsync(database, false,
                new(current.DatabaseProfileId, current.ProjectId, current.LifetimeId), ProcessRuntimeStatus.Completed, new Dictionary<string, string>());
        }
        ProcessExistingLaunchLookupRequest lookup = new(" DOTNET-RUNTIME-COMMAND-WRITEBACK ", null, projectId,
            new Dictionary<string, string> { [ProcessPredecessorFixture.ProbeKey] = ProcessPredecessorFixture.ProbeValue });
        await using (var read = app.Services.CreateAsyncScope()) {
            var launch = read.ServiceProvider.GetRequiredService<ProcessLaunchApplicationService>();
            var found = await launch.FindExistingLaunchAsync(lookup);
            Assert.Equal(retained.RunId, found!.RunId);
            Assert.Equal(retained.PlanHash, found.LaunchPlan.PlanHash);
            Assert.Equal(ProcessPredecessorFixture.DefinitionKey, found.LaunchPlan.DefinitionName);
            Assert.Null(await launch.FindExistingLaunchAsync(lookup with { ProjectId = null }));
            var another = Guid.NewGuid();
            var projects = read.ServiceProvider.GetRequiredService<ProjectsService>();
            Assert.True((await projects.CreateAsync(another, new() { Name = "Different legacy scope" })).IsSuccess);
            Assert.Null(await launch.FindExistingLaunchAsync(lookup with { ProjectId = another }));
            await projects.DeleteAsync(projectId);
            Assert.True((await projects.CreateAsync(projectId, new() { Name = "Replacement legacy lifetime" })).IsSuccess);
        }
        await using var recreated = app.Services.CreateAsyncScope();
        Assert.Null(await recreated.ServiceProvider.GetRequiredService<ProcessLaunchApplicationService>().FindExistingLaunchAsync(lookup));
        await using var final = await recreated.ServiceProvider.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>().CreateDbContextAsync();
        Assert.Equal(ProcessPredecessorFixture.PlanJson, (await final.InstancePlans.SingleAsync()).PayloadJson);
    }

    private static async Task<string> AssignmentJsonAsync(ProcessPersistenceDbContext database)
        => JsonSerializer.Serialize(await database.RuntimeStepAssignments.AsNoTracking().OrderBy(item => item.RunId).ThenBy(item => item.StepInstanceId).ToArrayAsync());

    private static async Task<string> LinkJsonAsync(IServiceProvider services, Guid projectId) {
        await using var database = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        return JsonSerializer.Serialize(await database.Set<ProjectObjectLinkRecord>().AsNoTracking()
            .Where(item => item.ProjectId == projectId).OrderBy(item => item.Id).ToArrayAsync());
    }
}
