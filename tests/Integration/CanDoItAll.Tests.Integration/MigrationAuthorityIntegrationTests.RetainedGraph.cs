using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Persistence;

public sealed partial class MigrationAuthorityIntegrationTests {
    private static readonly JsonSerializerOptions GraphJson = new(JsonSerializerDefaults.Web);
    private static readonly byte[] SavedFileBytes = Encoding.UTF8.GetBytes("Original project evidence.\r\nSecond line: café.\n");

    private static async Task<RetainedGraph> SeedRetainedGraphAsync(
        IServiceProvider services, AppDbContext context, TestDatabaseProfile profile) {
        var projectId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agentRunId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var workflowVersionId = Guid.NewGuid();
        var workflowRunId = Guid.NewGuid();
        var processRunId = Guid.NewGuid();
        var workflowNodeId = Guid.NewGuid();
        var fileNodeId = Guid.NewGuid();
        var workflowNodeKey = $"workflow:{workflowNodeId:N}";
        var fileNodeKey = $"file:{fileNodeId:N}";
        var fileName = "saved-evidence.txt";
        var filePath = Path.Combine(profile.WorkspaceRootPath, fileName);
        await File.WriteAllBytesAsync(filePath, SavedFileBytes);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Projects_Projects"
                ("Id", "Name", "Slug", "Description", "Objective", "Status", "CurrentPhase", "TargetDateUtc", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ({projectId}, 'Saved reference graph', {projectId.ToString("N")}, 'Original description', 'Original objective',
                {(int)ProjectStatus.OnHold}, 'Review', NULL, {SavedAt}, {SavedAt});
            """);

        var workspace = new FileSandboxWorkspaceStore(profile.WorkspaceRootPath,
            WorkspaceScopeDescriptor.Project(projectId.ToString("D")));
        var catalog = await workspace.LoadCatalogAsync();
        var agent = catalog.Agents[0] with {
            Id = agentId, Name = "Saved graph agent", IsTemplate = false, TemplateKey = string.Empty,
            ConfigurationJson = AgentProjectStructureAccessMetadata.Write(
                """{"customSavedField":"retain exact text","opaque":{"array":[1,null,true]}}""",
                new() { CanRead = true, AllowedProjectIds = [projectId] })
        };
        var savedCatalog = await workspace.UpdateCatalogAsync(current => current with {
            Agents = current.Agents.Append(agent).ToArray()
        });
        agent = Assert.Single(savedCatalog.Agents, item => item.Id == agentId);
        var detail = await workspace.SaveExecutionRunDetailAsync(new(
            new ExecutionRunRecord(agentRunId, agentId, null, "Saved completed execution", "manual", projectId.ToString("D"),
                workflowRunId.ToString("D"), string.Empty, "fixture-operator", "integration-test",
                JsonSerializer.Serialize(new { projectId, workflowRunId, fileNodeKey }, GraphJson),
                "Original input", "Original result", "Stored provider label", "stored-model",
                ExecutionState.Completed, RunOutcome.Succeeded, SavedAt, SavedAt, SavedAt, SavedAt,
                "saved-runtime-session", """{"legacySdkState":"preserve"}""", [], ProcessRunId: processRunId.ToString("D")),
            null, [new ExecutionLogEntry(Guid.NewGuid(), agentId, null, SavedAt,
                ExecutionState.Completed, "migration-reference", "Original execution history") { ExecutionRunId = agentRunId }], []));

        var processPlanId = new ProcessInstancePlanId(Guid.NewGuid());
        var processPlan = new ProcessInstancePlan(
            new(processPlanId, processPlanId, null, null, "processes.instance-plan.v1", SavedAt, 0),
            new(new(Guid.NewGuid()), new(Guid.NewGuid()), "sha256:saved-definition", "runtime/1.0", "runtime/1.0", [], [], []),
            new([]), new([], [], [], []), [], new([], []), new([]), [],
            new("sha256:saved-manager", null, [], []), new([]), new(false, "sha256:saved-projection"),
            new("sha256:saved-governance", []), string.Empty);
        processPlan = processPlan with { PlanHash = ProcessPlanHasher.Compute(processPlan) };
        await services.GetRequiredService<IProcessInstancePlanStore>().PersistAsync(processPlan);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "process_runtime_states"
                ("RunId", "RootRunId", "PlanId", "PlanHash", "Status", "UpdatedAtUtc", "ConcurrencyToken", "BlockedRecoveryActionsJson")
            VALUES ({processRunId}, {processRunId}, {processPlan.Header.PlanId.Value}, {processPlan.PlanHash},
                {nameof(ProcessRuntimeStatus.Completed)}, {SavedAt}, {Guid.NewGuid()}, '[]');
            """);

        var definition = CreateSavedWorkflow(workflowId, workflowVersionId, agentId);
        var origin = new WorkflowLaunchOrigin.ProjectStructureNode(projectId, new(workflowNodeKey),
            new(WorkflowLaunchActorKind.User, "fixture-operator"), new("saved-project-session"), new("saved-project-correlation"));
        var originJson = WorkflowRunRecordEntity.SerializeOrigin(origin);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AgentFramework_WorkflowRuns"
                ("RunId", "WorkflowId", "VersionId", "State", "Backend", "BackendRunId", "Summary", "CreatedAtUtc",
                    "UpdatedAtUtc", "TerminalAtUtc", "ReportingActivityAtUtc", "OriginJson", "OriginKind", "OriginProjectId", "OriginProcessRunId")
            VALUES ({workflowRunId}, {workflowId}, {workflowVersionId}, {(int)WorkflowRunState.Completed},
                {(int)WorkflowRuntimeBackendKind.InProcess}, 'saved-workflow-session', 'Original workflow result',
                {SavedAt}, {SavedAt}, {SavedAt}, {SavedAt}, {originJson}, {(int)WorkflowLaunchOriginKind.ProjectStructureNode}, {projectId}, NULL);
            """);
        var storage = new StorageCatalogRecord {
            Name = "Original offline file catalog", ProviderKind = StorageProviderKind.FileSystem,
            IsEnabled = false, IsReadOnly = true, EndpointOrRoot = profile.WorkspaceRootPath,
            ConfigJson = """{ "savedConfiguration" : [ 1, null, true ] }""",
            CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt
        };
        StorageCatalogHostBindingPolicy.ImportLegacy(storage, profile.WorkspaceRootPath);
        Assert.Equal(HostBoundPathRecord.CurrentFormatVersion, storage.RootBindingFormatVersion);
        Assert.Equal(HostBoundPathState.NeedsRebind, storage.RootPathState);
        Assert.Empty(storage.RootHostBindingId);
        Assert.Null(storage.RootLastValidatedAtUtc);
        Assert.False(storage.IsEnabled);
        Assert.True(storage.IsReadOnly);
        var reference = new StorageObjectReference(storage.Id, StorageProviderKind.FileSystem, StorageLocatorKind.RelativePath,
            fileName, fileName, "text/plain", SavedFileBytes.Length);
        var workflowNode = new ProjectObjectRecord {
            Id = workflowNodeId, ProjectId = projectId, NodeKey = workflowNodeKey,
            ObjectType = ProjectObjectType.WorkflowDefinition, Title = definition.Name,
            Notes = "Original human workflow notes", CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt,
            MetadataJson = ProjectObjectMetadataSerializer.Serialize(new() {
                Workflow = new() {
                    WorkflowId = definition.Id, WorkflowVersionId = definition.VersionId, WorkflowName = definition.Name,
                    LastRunId = new(workflowRunId), LastRunState = WorkflowRunState.Completed,
                    LastRunSummary = "Original workflow result", LastCreatedNodeIds = [fileNodeKey],
                    LastCreatedFilePaths = [fileName], LastUpdatedAtUtc = SavedAt
                }
            })
        };
        var fileNode = new ProjectObjectRecord {
            Id = fileNodeId, ProjectId = projectId, NodeKey = fileNodeKey, ParentNodeKey = workflowNodeKey,
            ObjectType = ProjectObjectType.File, Title = fileName, Notes = "Original human file notes",
            CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt
        };
        context.AddRange(storage, workflowNode, fileNode,
            new ProjectNodeBindingRecord {
                ProjectObjectId = fileNodeId, MediaRelativePath = fileName, MediaOriginalFileName = fileName,
                MediaContentType = reference.ContentType, StorageObjectReferenceJson = StorageJson.SerializeReference(reference),
                CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt
            },
            new ProjectObjectLinkRecord {
                ProjectId = projectId, SourceNodeKey = workflowNodeKey, TargetNodeKey = fileNodeKey,
                LinkKind = ProjectObjectLinkKind.DerivedFrom, CreatedAtUtc = SavedAt
            },
            WorkflowDefinitionRecord.FromDefinition(definition, 7),
            new WorkflowDefinitionHeadRecord { WorkflowId = workflowId, VersionId = workflowVersionId },
            new WorkflowEventRecordEntity {
                Id = Guid.NewGuid(), RunId = workflowRunId, Kind = WorkflowEventKind.Completed,
                NodeId = "end", Message = "Original workflow completion", PayloadJson = """{ "retained" : [1,null,true] }""", CreatedAtUtc = SavedAt
            },
            new WorkflowArtifactRecordEntity {
                Id = Guid.NewGuid(), RunId = workflowRunId, NodeId = "agent", Name = fileName,
                ContentType = "text/plain", StoragePath = fileName, Summary = "Original saved artifact", CreatedAtUtc = SavedAt
            });
        await context.SaveChangesAsync();
        return new(projectId, agentId, agentRunId, workflowId, workflowVersionId, workflowRunId,
            processPlan.Header.PlanId.Value, processRunId, workflowNodeId, fileNodeId, reference, filePath,
            JsonSerializer.Serialize(agent), JsonSerializer.Serialize(detail), JsonSerializer.Serialize(processPlan, GraphJson));
    }

    private static WorkflowDefinition CreateSavedWorkflow(Guid workflowId, Guid versionId, Guid agentId) {
        var nodes = new[] {
            Node("start", WorkflowNodeKind.Start), Node("agent", WorkflowNodeKind.AgentStep, agentId), Node("end", WorkflowNodeKind.End)
        };
        return new(new(workflowId), new(versionId), "Saved Agent workflow", "Original saved workflow description",
            WorkflowLifecycleStatus.Draft, new(nodes[0].Id, nodes, [
                new(new("start-agent"), nodes[0].Id, null, nodes[1].Id, null, WorkflowEdgeKind.Direct, string.Empty),
                new(new("agent-end"), nodes[1].Id, null, nodes[2].Id, null, WorkflowEdgeKind.Direct, string.Empty)
            ]), new(WorkflowRuntimeBackendKind.InProcess, true, false, false, false), SavedAt, SavedAt);

        static WorkflowNode Node(string id, WorkflowNodeKind kind, Guid? agent = null) => new(new(id), kind, id, [],
            new(null, agent, null, null, "Original saved instructions", WorkflowValueShape.Text, WorkflowValueShape.Text));
    }

    private static async Task<string[]> ReadRetainedGraphRowsAsync(AppDbContext context, RetainedGraph graph) =>
        await context.Database.SqlQuery<string>($"""
            SELECT 'project:' || (to_jsonb(row) - ARRAY['LifetimeId','LegacyAgentAccessBindingEligible'])::text AS "Value"
                FROM "Projects_Projects" row WHERE "Id" = {graph.ProjectId}
            UNION ALL SELECT 'workflow-definition:' || to_jsonb(row)::text
                FROM "AgentFramework_WorkflowDefinitions" row WHERE "VersionId" = {graph.WorkflowVersionId}
            UNION ALL SELECT 'workflow-head:' || to_jsonb(row)::text
                FROM "AgentFramework_WorkflowDefinitionHeads" row WHERE "WorkflowId" = {graph.WorkflowId}
            UNION ALL SELECT 'workflow-run:' || (to_jsonb(row) - 'OriginProcessAssignmentId')::text
                FROM "AgentFramework_WorkflowRuns" row WHERE "RunId" = {graph.WorkflowRunId}
            UNION ALL SELECT 'workflow-event:' || to_jsonb(row)::text
                FROM "AgentFramework_WorkflowEvents" row WHERE "RunId" = {graph.WorkflowRunId}
            UNION ALL SELECT 'workflow-artifact:' || to_jsonb(row)::text
                FROM "AgentFramework_WorkflowArtifacts" row WHERE "RunId" = {graph.WorkflowRunId}
            UNION ALL SELECT 'process-plan:' || to_jsonb(row)::text
                FROM "process_instance_plans" row WHERE "PlanId" = {graph.ProcessPlanId}
            UNION ALL SELECT 'process-run:' || (to_jsonb(row) - ARRAY['LaunchAdmissionId','ProjectAdmissionDatabaseProfileId',
                    'ProjectAdmissionProjectId','ProjectAdmissionLifetimeId'])::text
                FROM "process_runtime_states" row WHERE "RunId" = {graph.ProcessRunId}
            UNION ALL SELECT 'native-node:' || to_jsonb(row)::text
                FROM "Workbench_ProjectObjects" row WHERE "ProjectId" = {graph.ProjectId}
            UNION ALL SELECT 'native-binding:' || to_jsonb(row)::text
                FROM "Workbench_ProjectNodeBindings" row WHERE "ProjectObjectId" = {graph.FileNodeId}
            UNION ALL SELECT 'native-link:' || to_jsonb(row)::text
                FROM "Workbench_ProjectObjectLinks" row WHERE "ProjectId" = {graph.ProjectId}
            UNION ALL SELECT 'storage:' || to_jsonb(row)::text
                FROM "Storage_Catalog" row WHERE "Id" = {graph.Reference.StorageId}
            """).OrderBy(value => value).ToArrayAsync();

    private static async Task<Guid> AssertRetainedGraphAsync(
        ServiceProvider provider, TestDatabaseProfile profile, RetainedGraph graph, Guid? previousLifetime) {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var projects = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        var project = await projects.Set<Project>().AsNoTracking().SingleAsync(row => row.Id == graph.ProjectId);
        Assert.NotEqual(Guid.Empty, project.LifetimeId);
        Assert.True(project.LegacyAgentAccessBindingEligible);
        if (previousLifetime.HasValue) {
            Assert.Equal(previousLifetime.Value, project.LifetimeId);
        }
        var workspace = new FileSandboxWorkspaceStore(profile.WorkspaceRootPath,
            WorkspaceScopeDescriptor.Project(graph.ProjectId.ToString("D")));
        var agent = Assert.Single((await workspace.LoadCatalogAsync()).Agents, row => row.Id == graph.AgentId);
        Assert.Equal(graph.AgentJson, JsonSerializer.Serialize(agent));
        var access = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
        Assert.Equal([graph.ProjectId], access.AllowedProjectIds);
        Assert.Empty(access.AllowedProjectLifetimes);
        var agentRun = await workspace.GetExecutionRunDetailAsync(graph.AgentRunId);
        Assert.NotNull(agentRun);
        Assert.Equal(graph.AgentRunJson, JsonSerializer.Serialize(agentRun));
        Assert.Equal(graph.ProcessRunId.ToString("D"), agentRun.Run.ProcessRunId);
        Assert.Null(agentRun.Run.ToolAdmission);

        var workflowFactory = services.GetRequiredService<IDbContextFactory<WorkflowDbContext>>();
        await using var workflows = await workflowFactory.CreateDbContextAsync();
        var definition = await workflows.Set<WorkflowDefinitionRecord>().AsNoTracking()
            .SingleAsync(row => row.VersionId == graph.WorkflowVersionId);
        var decoded = JsonSerializer.Deserialize<WorkflowDefinition>(definition.DefinitionJson, GraphJson);
        Assert.NotNull(decoded);
        Assert.Equal(graph.AgentId, Assert.Single(decoded.Graph.Nodes, node => node.Kind == WorkflowNodeKind.AgentStep).Settings.AgentId);
        var runStore = new PersistentWorkflowRunStore(workflowFactory);
        var workflowRun = await runStore.GetRunAsync(new(graph.WorkflowRunId));
        Assert.NotNull(workflowRun);
        Assert.Equal(WorkflowRunState.Completed, workflowRun.State);
        var origin = Assert.IsType<WorkflowLaunchOrigin.ProjectStructureNode>(workflowRun.Origin);
        Assert.Equal(graph.ProjectId, origin.ProjectId);
        Assert.Null(origin.StructureAuthority);
        Assert.Single(await runStore.ListEventsAsync(new(graph.WorkflowRunId)), row => row.Kind == WorkflowEventKind.Completed);
        Assert.Equal(graph.Reference.Locator, Assert.Single(await runStore.ListArtifactsAsync(new(graph.WorkflowRunId))).StoragePath);

        var processPlan = await services.GetRequiredService<IProcessInstancePlanStore>().LoadAsync(new(graph.ProcessPlanId));
        Assert.NotNull(processPlan);
        Assert.Equal(graph.ProcessPlanJson, JsonSerializer.Serialize(processPlan, GraphJson));
        var processDatabase = services.GetRequiredService<ProcessPersistenceDbContext>();
        var processRun = await processDatabase.Set<ProcessRuntimeStateEntity>().AsNoTracking()
            .SingleAsync(row => row.RunId == graph.ProcessRunId);
        Assert.Equal(graph.ProcessPlanId, processRun.PlanId);
        Assert.Equal(processPlan.PlanHash, processRun.PlanHash);
        Assert.Equal(ProcessRuntimeStatus.Completed, processRun.Status);
        Assert.Null(processRun.LaunchAdmissionId);
        Assert.Null(processRun.ProjectAdmissionDatabaseProfileId);
        Assert.Null(processRun.ProjectAdmissionProjectId);
        Assert.Null(processRun.ProjectAdmissionLifetimeId);

        await using var workbench = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        var node = await workbench.Set<ProjectObjectRecord>().AsNoTracking().SingleAsync(row => row.Id == graph.WorkflowNodeId);
        var metadata = ProjectObjectMetadataSerializer.Parse(node.MetadataJson);
        Assert.NotNull(metadata.Workflow);
        Assert.Equal(new WorkflowId(graph.WorkflowId), metadata.Workflow.WorkflowId);
        Assert.Equal(new WorkflowRunId(graph.WorkflowRunId), metadata.Workflow.LastRunId);
        Assert.Equal(graph.ProjectId, node.ProjectId);
        var file = await workbench.Set<ProjectObjectRecord>().AsNoTracking().SingleAsync(row => row.Id == graph.FileNodeId);
        Assert.Equal(node.NodeKey, file.ParentNodeKey);
        Assert.Equal(graph.ProjectId, file.ProjectId);
        var binding = await workbench.Set<ProjectNodeBindingRecord>().AsNoTracking().SingleAsync(row => row.ProjectObjectId == graph.FileNodeId);
        Assert.Equal(graph.Reference, StorageJson.ParseReference(binding.StorageObjectReferenceJson));
        var storage = await services.GetRequiredService<StorageCatalogService>().GetAsync(graph.Reference.StorageId!.Value);
        Assert.NotNull(storage);
        Assert.Equal(graph.Reference.ProviderKind, storage.ProviderKind);
        Assert.Equal(HostBoundPathRecord.CurrentFormatVersion, storage.RootBindingFormatVersion);
        Assert.Equal(HostBoundPathState.NeedsRebind, storage.RootPathState);
        Assert.Empty(storage.RootHostBindingId);
        Assert.Null(storage.RootLastValidatedAtUtc);
        Assert.False(storage.IsEnabled);
        Assert.True(storage.IsReadOnly);
        Assert.Equal(SavedFileBytes, await File.ReadAllBytesAsync(graph.FilePath));
        await using var scheduler = await services.GetRequiredService<IDbContextFactory<SchedulerPlannerDbContext>>().CreateDbContextAsync();
        Assert.Null((await scheduler.Set<SchedulerPlan>().SingleAsync(row => row.TargetId == graph.WorkflowId)).StructureAuthorityJson);
        return project.LifetimeId;
    }

    private sealed record RetainedGraph(
        Guid ProjectId, Guid AgentId, Guid AgentRunId, Guid WorkflowId, Guid WorkflowVersionId, Guid WorkflowRunId,
        Guid ProcessPlanId, Guid ProcessRunId, Guid WorkflowNodeId, Guid FileNodeId,
        StorageObjectReference Reference, string FilePath, string AgentJson, string AgentRunJson, string ProcessPlanJson);
}
