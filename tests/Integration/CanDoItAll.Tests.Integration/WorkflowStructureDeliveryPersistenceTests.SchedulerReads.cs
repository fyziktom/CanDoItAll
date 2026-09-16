using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed partial class WorkflowStructureDeliveryPersistenceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Scheduled_Workflow_read_keeps_original_fire_and_rechecks_current_plan_authority(bool replaceAuthority) {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        var services = fixture.Services;
        var catalog = new InMemoryWorkflowCatalogService(services.GetRequiredService<IWorkflowDefinitionValidator>());
        var definition = ReadDefinition(fixture.Definition, ReadSettings(WorkflowProjectStructureOperation.ReadTree, fixture.ProjectId), numericSettings: false);
        definition = await catalog.SaveDefinitionAsync(new(definition.Id, null, definition.Name, definition.Description,
            definition.Status, definition.Graph, definition.RuntimePolicy));
        var authority = await services.GetRequiredService<ProjectStructureWorkflowAuthorityService>()
            .PrepareLaunchAsync(fixture.Request.WorkflowMutationAdmission!.Authority, definition);
        var plan = new SchedulerPlan { Id = Guid.NewGuid(), Name = "Scheduled read", TargetKind = SchedulerPlanTargetKind.Workflow,
            TargetId = definition.Id.Value, TargetVersionId = definition.VersionId.Value, TargetNameSnapshot = definition.Name,
            SchedulerTriggerId = Guid.NewGuid(), SchedulerTriggerKey = $"read-fixture-{Guid.NewGuid():N}", CronExpression = "0 0/30 * * * ?",
            CronDescription = "Every thirty minutes", InputJson = "{}", StructureAuthorityJson = SchedulerFireSnapshot.SerializeAuthority(authority), IsEnabled = true };
        var factory = services.GetRequiredService<IDbContextFactory<SchedulerPlannerDbContext>>();
        await using (var database = await factory.CreateDbContextAsync()) {
            database.Add(plan);
            await database.SaveChangesAsync();
        }
        var admissions = new SchedulerFireAdmissionStore(factory, catalog, services.GetRequiredService<IWorkflowRuntimeManager>(), services.GetRequiredService<IClock>());
        var claim = Assert.IsType<SchedulerFireClaim>(await admissions.AcquireAsync(new(plan.Id, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, null)));
        var prepared = claim.Snapshot.ToContext(claim.PreparedRunId, true);
        var origin = new WorkflowLaunchOrigin.SchedulerPlanRun(prepared.PlanId, prepared.PlanRunId, prepared.SchedulerFireId,
            prepared.FiredAtUtc, prepared.CorrelationId) { PreparedRunId = prepared.PreparedRunId, StructureAuthority = prepared.StructureAuthority };
        var now = DateTimeOffset.UtcNow;
        var run = new WorkflowRunSnapshot(new(claim.PreparedRunId), definition.Id, definition.VersionId, WorkflowRunState.Running,
            WorkflowRuntimeBackendKind.InProcess, "scheduled-read", "Running", now, now) { Origin = origin };
        await services.GetRequiredService<IWorkflowRunStore>().CreateRunWithStartedEventAsync(run,
            new(Guid.NewGuid(), run.RunId, WorkflowEventKind.Started, null, "Scheduled read", "{}", now));
        var invocation = new ReadInvocation(run, definition, definition.Graph.Nodes.Single(node => node.Kind == WorkflowNodeKind.Executor));
        var gateway = services.GetRequiredService<IProjectStructureRuntimeGateway>();
        Assert.Contains(fixture.ProjectId.ToString("D"), (await InvokeReadAsync(gateway, invocation)).PayloadJson, StringComparison.OrdinalIgnoreCase);
        await using (var database = await factory.CreateDbContextAsync()) {
            var saved = await database.Set<SchedulerPlan>().SingleAsync(row => row.Id == plan.Id);
            if (replaceAuthority) {
                saved.StructureAuthorityJson = SchedulerFireSnapshot.SerializeAuthority(authority with { CanCreateAssets = false });
            } else {
                saved.IsEnabled = false;
            }
            await database.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<WorkflowExecutorInvocationException>(() => InvokeReadAsync(gateway, invocation));
        Assert.Equal(origin.StructureAuthority!.SchedulerAuthority, (await services.GetRequiredService<IWorkflowRunStore>()
            .GetRunAsync(run.RunId))!.Origin!.StructureAuthority!.SchedulerAuthority);
    }
}
