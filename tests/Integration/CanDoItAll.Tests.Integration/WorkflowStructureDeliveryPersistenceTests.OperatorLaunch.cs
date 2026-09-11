using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed partial class WorkflowStructureDeliveryPersistenceTests {
    [Theory]
    [InlineData(WorkflowStructureOperatorSurface.UserInterface)]
    [InlineData(WorkflowStructureOperatorSurface.Api)]
    public async Task Operator_admission_recovers_into_one_real_run_without_impersonating_an_agent(WorkflowStructureOperatorSurface surface) {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app, prepareOutput: false, sourceSurface: surface);
        var template = Definition();
        var definition = await fixture.Services.GetRequiredService<IWorkflowCatalogService>().SaveDefinitionAsync(
            new(null, null, "Operator admission", "Synthetic operator recovery", WorkflowLifecycleStatus.Active,
                template.Graph, template.RuntimePolicy));
        var node = await fixture.Owner.CreateObjectAsync(fixture.ProjectId,
            new(ProjectObjectType.WorkflowDefinition, "Operator workflow", "", "", fixture.Request.ParentNodeKey,
                MetadataJson: ProjectObjectMetadataSerializer.Serialize(new() {
                    Workflow = new() { WorkflowId = definition.Id, WorkflowVersionId = definition.VersionId }
                })));
        var source = fixture.Services.GetRequiredService<ProjectStructureWorkflowAuthorityService>();
        var authoritySource = ProjectStructureWorkflowAuthoritySource.LocalOperator(surface);
        var authority = await source.PrepareLaunchAsync(await source.CaptureAsync(fixture.ProjectId, authoritySource), definition);
        var actor = new ProjectStructureAgentContext("operator-view", "Operator", "fixture", "", "", "operator-session") {
            WorkflowAuthority = authoritySource
        };
        var admission = await fixture.Owner.PrepareWorkflowAdmissionAsync(fixture.ProjectId, node.Id, Guid.NewGuid(), true,
            definition, "{\"message\":\"original operator input\"}", null, WorkflowPreviewSimulationPlan.Empty, authority, actor);
        var origin = Assert.IsType<WorkflowLaunchOrigin.ProjectStructureNode>(admission.LaunchIntent.Origin);
        Assert.Equal(WorkflowLaunchActorKind.User, origin.RequestingActor.Kind);
        Assert.Null(await fixture.Services.GetRequiredService<IWorkflowRunStore>().GetRunAsync(admission.Binding.RunId));

        await using var recoveredScope = app.Services.CreateAsyncScope();
        var recoveredOwner = recoveredScope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var persisted = Assert.IsType<ProjectWorkflowAdmission>(await recoveredOwner.FindWorkflowAdmissionAsync(admission.Binding.IntentId));
        Assert.Equal(admission.Binding.RunId, persisted.Binding.RunId);
        var nodes = recoveredScope.ServiceProvider.GetRequiredService<ProjectStructureWorkflowNodeService>();
        Assert.Equal(ProjectWorkflowDeliveryState.Applied, await nodes.ReconcileAsync(persisted.Binding.IntentId));
        var run = await recoveredScope.ServiceProvider.GetRequiredService<IWorkflowRunStore>().GetRunAsync(persisted.Binding.RunId);
        Assert.NotNull(run);
        Assert.Equal(WorkflowRunState.Completed, run.State);
        var retained = Assert.IsType<WorkflowLaunchOrigin.ProjectStructureNode>(run.Origin);
        Assert.Equal(origin.RequestingActor, retained.RequestingActor);
        Assert.Equal(origin.ProjectId, retained.ProjectId);
        Assert.Equal(origin.NodeId, retained.NodeId);
        Assert.Equal(origin.SessionId, retained.SessionId);
        Assert.Equal(admission.Binding.RunId, retained.StructureAdmission!.RunId);
        Assert.Equal(ProjectWorkflowDeliveryState.Applied, await nodes.ReconcileAsync(persisted.Binding.IntentId));
        Assert.Equal(admission.Binding.RunId, (await nodes.GetStatusAsync(fixture.ProjectId, node.Id)).RunId);
        await using var database = await recoveredScope.ServiceProvider.GetRequiredService<IDbContextFactory<WorkflowDbContext>>().CreateDbContextAsync();
        Assert.Single(await database.Set<WorkflowRunRecordEntity>().Where(row => row.WorkflowId == definition.Id.Value).ToListAsync());
        var projection = await ReadWorkflowNodeAsync(fixture.Factory, fixture.ProjectId, node.Id);
        Assert.Equal(admission.Binding.RunId, ProjectObjectMetadataSerializer.Parse(projection.MetadataJson).Workflow!.LastRunId);
        Assert.Equal(100, projection.ProgressPercent);
    }
}
