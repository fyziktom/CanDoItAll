using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectStructureAgentApiIntegrationTests
{
    [Fact]
    public void ProjectStructureLeaseAcquireRequest_accepts_string_scope_kind()
    {
        var request = JsonSerializer.Deserialize<ProjectStructureLeaseAcquireRequest>(
            """
            {
              "scopeKind": "Project",
              "scopeKey": "project:alpha",
              "reason": "Agent-authored API lease",
              "durationMinutes": 15
            }
            """,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(request);
        Assert.Equal(ProjectStructureLeaseScopeKind.Project, request.ScopeKind);
        Assert.Equal("project:alpha", request.ScopeKey);
    }

    [Fact]
    public async Task ProjectStructureAgentApi_supports_delivery_block_asset_roundtrip_and_records_analytics()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();

        var project = await PostAndReadAsync<ProjectSummary>(
            host.Client,
            "/api/project-structure/projects",
            new ProjectStructureProjectSaveRequest(
                "API project",
                "HTTP roundtrip validation",
                "Create and read project structure over the central API.",
                "Execution",
                ProjectStatus.Active));

        var lease = await PostAndReadAsync<ProjectStructureLeaseSnapshot>(
            host.Client,
            "/api/project-structure/leases/acquire",
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.Project,
                project.Id.ToString(),
                "Create delivery assets",
                15));

        var deliveryBlock = await PostAndReadAsync<ProjectStructureNodeSummary>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes",
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.ProjectBlock,
                "Delivery block",
                "Validation",
                "Root delivery work for API validation.",
                $"project:{project.Id}",
                420,
                240,
                null,
                null,
                "delivery",
                null,
                null,
                lease.LeaseToken));

        var excelAsset = await PostAndReadAsync<ProjectStructureNodeSummary>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes",
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.File,
                "Delivery workbook",
                "Excel evidence",
                "Create an Excel asset through the API.",
                deliveryBlock.Id,
                620,
                360,
                null,
                null,
                "excel",
                CreateMediaPayload("delivery-workbook.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "excel payload"),
                null,
                lease.LeaseToken));

        var pdfAsset = await PostAndReadAsync<ProjectStructureNodeSummary>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes",
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.File,
                "Delivery packet",
                "PDF evidence",
                "Create a PDF asset through the API.",
                deliveryBlock.Id,
                760,
                360,
                null,
                null,
                "pdf",
                CreateMediaPayload("delivery-packet.pdf", "application/pdf", "%PDF-1.4 payload"),
                null,
                lease.LeaseToken));

        var readback = await PostAndReadAsync<ProjectStructureReadResponse>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/structure/read",
            new ProjectStructureReadRequest(
                IncludeLinks: true,
                IncludeAssets: true,
                IncludeNotes: true,
                IncludeMetadata: true));

        Assert.Contains(readback.Nodes, node => node.Id == deliveryBlock.Id && node.Title == "Delivery block");
        Assert.Contains(readback.Nodes, node => node.Id == excelAsset.Id && node.MediaOriginalFileName == "delivery-workbook.xlsx");
        Assert.Contains(readback.Nodes, node => node.Id == pdfAsset.Id && node.MediaOriginalFileName == "delivery-packet.pdf");
        Assert.Contains(readback.Links, link => link.SourceId == deliveryBlock.Id && link.TargetId == excelAsset.Id);
        Assert.Contains(readback.Links, link => link.SourceId == deliveryBlock.Id && link.TargetId == pdfAsset.Id);

        var analytics = await PostAndReadAsync<ProjectStructureAnalyticsResponse>(
            host.Client,
            "/api/project-structure/analytics/query",
            new ProjectStructureAnalyticsQueryRequest(project.Id, Take: 20));

        Assert.Contains(analytics.Entries, entry => entry.OperationName == "projects.create" && entry.Succeeded);
        Assert.Contains(analytics.Entries, entry => entry.OperationName == "structure.node-create" && entry.Succeeded);
        Assert.Contains(analytics.Entries, entry => entry.OperationName == "structure.read" && entry.Succeeded);
    }

    [Fact]
    public async Task ProjectStructureAgentApi_creates_workflow_node_with_typed_metadata()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();

        var project = await PostAndReadAsync<ProjectSummary>(
            host.Client,
            "/api/project-structure/projects",
            new ProjectStructureProjectSaveRequest(
                "Workflow API project",
                "HTTP workflow node validation",
                "Create workflow nodes from project structure.",
                "Execution",
                ProjectStatus.Active));

        var lease = await PostAndReadAsync<ProjectStructureLeaseSnapshot>(
            host.Client,
            "/api/project-structure/leases/acquire",
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.Project,
                project.Id.ToString(),
                "Create workflow node",
                15));

        var parent = await PostAndReadAsync<ProjectStructureNodeSummary>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes",
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.ProjectBlock,
                "Mouser order",
                "Procurement",
                "Parent node that supplies the workflow context.",
                $"project:{project.Id}",
                ObjectSubtype: "financial",
                LeaseToken: lease.LeaseToken));

        WorkflowDefinition definition;
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var catalogService = scope.ServiceProvider.GetRequiredService<IWorkflowCatalogService>();
            definition = await catalogService.SaveDefinitionAsync(CreateWorkflowDefinitionSaveRequest());
        }

        var inputSettings = ProjectStructureWorkflowInputSettings.Default();
        inputSettings.IncludeParentSubtree = true;
        inputSettings.ManualInputJson = "{\"check\":\"pdf-xlsx-match\"}";
        inputSettings.AdditionalSources =
        [
            new ProjectStructureWorkflowInputSource(
                ProjectStructureWorkflowInputSourceKind.FolderPath,
                "mouser-data",
                "Mouser order files",
                "C:\\programovani\\testdata\\testworkflows\\mouser-order")
        ];

        var created = await PostAndReadAsync<ProjectStructureWorkflowNodeCreateResult>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes/{parent.Id}/workflow-definition",
            new ProjectStructureWorkflowNodeCreateInput(
                definition.Id,
                definition.VersionId,
                "Mouser order reconciliation",
                InputSettings: inputSettings,
                X: 820,
                Y: 420,
                LeaseToken: lease.LeaseToken));

        Assert.Equal(project.Id, created.ProjectId);
        Assert.Equal(definition.Id, created.WorkflowId);
        Assert.Equal(definition.VersionId, created.WorkflowVersionId);
        Assert.Equal(ProjectObjectType.WorkflowDefinition, created.Node.ObjectType);
        Assert.Equal(parent.Id, created.Node.ParentId);
        Assert.Equal("workflow-definition", created.Node.ArtifactKind);
        Assert.Equal(definition.Id.Value, created.Node.ArtifactId);

        var readback = await PostAndReadAsync<ProjectStructureReadResponse>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/structure/read",
            new ProjectStructureReadRequest(IncludeLinks: true, IncludeMetadata: true, IncludeNotes: true));
        var workflowNode = Assert.Single(readback.Nodes, node => node.Id == created.Node.Id);
        var metadata = ProjectObjectMetadataSerializer.Parse(workflowNode.MetadataJson);

        Assert.Equal("Mouser order reconciliation", workflowNode.Title);
        Assert.Equal(definition.Id, metadata.Workflow?.WorkflowId);
        Assert.Equal(definition.VersionId, metadata.Workflow?.WorkflowVersionId);
        Assert.True(metadata.Workflow!.InputSettings.IncludeProject);
        Assert.True(metadata.Workflow.InputSettings.IncludeParentNode);
        Assert.True(metadata.Workflow.InputSettings.IncludeParentNodeDetails);
        Assert.True(metadata.Workflow.InputSettings.IncludeParentSubtree);
        Assert.Equal("{\"check\":\"pdf-xlsx-match\"}", metadata.Workflow.InputSettings.ManualInputJson);
        Assert.Contains(
            metadata.Workflow.InputSettings.AdditionalSources,
            source => source.Kind == ProjectStructureWorkflowInputSourceKind.FolderPath &&
                      source.Key == "mouser-data");

        var missingWorkflowResponse = await host.Client.PostAsJsonAsync(
            $"/api/project-structure/projects/{project.Id}/nodes/{parent.Id}/workflow-definition",
            new ProjectStructureWorkflowNodeCreateInput(WorkflowId.New(), LeaseToken: lease.LeaseToken));

        Assert.Equal(HttpStatusCode.NotFound, missingWorkflowResponse.StatusCode);
    }

    [Fact]
    public async Task ProjectStructureAgentApi_builds_workflow_input_preview_from_project_parent_and_sources()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();

        var project = await PostAndReadAsync<ProjectSummary>(
            host.Client,
            "/api/project-structure/projects",
            new ProjectStructureProjectSaveRequest(
                "SEAMARK xray review",
                "Workflow input preview validation",
                "Preview workflow input before creating a node.",
                "Discovery",
                ProjectStatus.Active));

        var parent = await PostAndReadAsync<ProjectStructureNodeSummary>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes",
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.Repository,
                "SEAMARK folder",
                "Local folder",
                "Folder with xray device PDFs and price lists.",
                $"project:{project.Id}",
                ObjectSubtype: "folder"));

        var child = await PostAndReadAsync<ProjectStructureNodeSummary>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes",
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.File,
                "SEAMARK price list",
                "PDF",
                "Price list extracted from the vendor folder.",
                parent.Id,
                ObjectSubtype: "pdf"));

        WorkflowDefinition activeDefinition;
        WorkflowDefinition draftDefinition;
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var catalogService = scope.ServiceProvider.GetRequiredService<IWorkflowCatalogService>();
            activeDefinition = await catalogService.SaveDefinitionAsync(CreateWorkflowDefinitionSaveRequest("SEAMARK folder summary"));
            draftDefinition = await catalogService.SaveDefinitionAsync(CreateWorkflowDefinitionSaveRequest("Draft SEAMARK workflow", WorkflowLifecycleStatus.Draft));
        }

        var inputSettings = ProjectStructureWorkflowInputSettings.Default();
        inputSettings.IncludeParentSubtree = true;
        inputSettings.ManualInputJson = "{\"task\":\"compare-devices\"}";
        inputSettings.AdditionalSources =
        [
            new ProjectStructureWorkflowInputSource(
                ProjectStructureWorkflowInputSourceKind.FolderPath,
                "seamark-folder",
                "SEAMARK source folder",
                "C:\\programovani\\testdata\\testworkflows\\SEAMARK")
        ];

        var options = await PostAndReadAsync<ProjectStructureWorkflowAddOptionsResult>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes/{parent.Id}/workflow-add-options",
            new ProjectStructureWorkflowAddOptionsInput(
                activeDefinition.Id,
                InputSettings: inputSettings,
                SelectedNodeIds: [child.Id]));

        Assert.Equal(activeDefinition.Id, options.SelectedWorkflowId);
        Assert.Equal(activeDefinition.VersionId, options.SelectedVersionId);
        Assert.Contains(options.Workflows, item => item.WorkflowId == activeDefinition.Id && item.IsSelectable);
        Assert.Contains(options.Workflows, item => item.WorkflowId == draftDefinition.Id && !item.IsSelectable);
        Assert.Contains("Project", options.Preview.Summary);
        Assert.Contains("Parent node", options.Preview.Summary);
        Assert.Contains("SEAMARK source folder", options.Preview.Summary);

        using var inputPayload = JsonDocument.Parse(options.Preview.InputJson);
        var root = inputPayload.RootElement;
        Assert.Equal(project.Id, root.GetProperty("project").GetProperty("id").GetGuid());
        Assert.Equal("SEAMARK xray review", root.GetProperty("project").GetProperty("name").GetString());
        Assert.Equal(parent.Id, root.GetProperty("parentNode").GetProperty("id").GetString());
        Assert.Equal("SEAMARK folder", root.GetProperty("parentNode").GetProperty("title").GetString());
        Assert.Equal("Folder with xray device PDFs and price lists.", root.GetProperty("parentNode").GetProperty("notes").GetString());
        Assert.Equal("SEAMARK price list", root.GetProperty("selectedNodes")[0].GetProperty("title").GetString());
        Assert.Equal("SEAMARK price list", root.GetProperty("parentSubtree")[0].GetProperty("title").GetString());
        Assert.Equal("C:\\programovani\\testdata\\testworkflows\\SEAMARK", root.GetProperty("sources")[0].GetProperty("value").GetString());
        Assert.Equal("compare-devices", root.GetProperty("manualInput").GetProperty("task").GetString());

        var invalidManualJson = await host.Client.PostAsJsonAsync(
            $"/api/project-structure/projects/{project.Id}/nodes/{parent.Id}/workflow-add-options",
            new ProjectStructureWorkflowAddOptionsInput(
                activeDefinition.Id,
                InputSettings: new ProjectStructureWorkflowInputSettings
                {
                    ManualInputJson = "{"
                }));

        Assert.Equal(HttpStatusCode.BadRequest, invalidManualJson.StatusCode);
    }

    [Fact]
    public async Task ProjectStructureAgentApi_starts_workflow_node_and_updates_summary()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        var project = await CreateWorkflowProjectAsync(host.Client, "Workflow run project");
        var lease = await AcquireProjectLeaseAsync(host.Client, project.Id, "Start workflow node");
        var parent = await CreateProjectBlockAsync(host.Client, project.Id, lease.LeaseToken, "Mouser order", "Order files and reconciliation notes.");

        WorkflowDefinition definition;
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var catalogService = scope.ServiceProvider.GetRequiredService<IWorkflowCatalogService>();
            definition = await catalogService.SaveDefinitionAsync(CreateWorkflowDefinitionSaveRequest("Mouser reconciliation workflow"));
        }

        var inputSettings = ProjectStructureWorkflowInputSettings.Default();
        inputSettings.ManualInputJson = "{\"task\":\"reconcile-order\"}";
        inputSettings.AdditionalSources =
        [
            new ProjectStructureWorkflowInputSource(
                ProjectStructureWorkflowInputSourceKind.FolderPath,
                "mouser-source",
                "Mouser source folder",
                "C:\\programovani\\testdata\\testworkflows\\mouser-order")
        ];
        var workflowNode = await PostAndReadAsync<ProjectStructureWorkflowNodeCreateResult>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes/{parent.Id}/workflow-definition",
            new ProjectStructureWorkflowNodeCreateInput(
                definition.Id,
                definition.VersionId,
                InputSettings: inputSettings,
                LeaseToken: lease.LeaseToken));

        var started = await PostAndReadAsync<ProjectStructureWorkflowNodeStartResult>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes/{workflowNode.Node.Id}/workflow/start",
            new ProjectStructureWorkflowNodeStartInput(WorkflowRuntimeBackendKind.InProcess, LeaseToken: lease.LeaseToken));

        Assert.Equal(WorkflowRunState.Completed, started.Status.State);
        Assert.Equal("complete", started.Status.ProgressMode);
        Assert.Equal(100, started.Status.ProgressPercent);
        Assert.Equal(3, started.Status.StepCount);
        Assert.Equal(3, started.Status.CurrentStepIndex);
        Assert.Contains(started.RunId.Value.ToString("D"), started.Route);

        var createdFilePath = "C:\\programovani\\testdata\\testworkflows\\mouser-order\\generated-summary.md";
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var runStore = scope.ServiceProvider.GetRequiredService<IWorkflowRunStore>();
            await runStore.SaveArtifactAsync(new WorkflowArtifactRecord(
                WorkflowArtifactId.New(),
                started.RunId,
                WorkflowArtifactKind.File,
                NodeId: null,
                "generated-summary.md",
                "text/markdown",
                createdFilePath,
                "Generated procurement summary.",
                DateTimeOffset.UtcNow));
        }

        var status = await GetAndReadAsync<ProjectStructureWorkflowRunStatus>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes/{workflowNode.Node.Id}/workflow/status");

        Assert.Equal(WorkflowRunState.Completed, status.State);
        Assert.Contains(createdFilePath, status.Summary.CreatedFilePaths);
        Assert.Contains(status.Summary.Artifacts, artifact => artifact.Name == "generated-summary.md");

        var readback = await PostAndReadAsync<ProjectStructureReadResponse>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/structure/read",
            new ProjectStructureReadRequest(IncludeMetadata: true));
        var updatedWorkflowNode = Assert.Single(readback.Nodes, node => node.Id == workflowNode.Node.Id);
        var metadata = ProjectObjectMetadataSerializer.Parse(updatedWorkflowNode.MetadataJson).Workflow;

        Assert.Equal("Completed", updatedWorkflowNode.Status);
        Assert.Equal("complete", updatedWorkflowNode.ProgressMode);
        Assert.Equal(100, updatedWorkflowNode.ProgressPercent);
        Assert.Equal(started.RunId, metadata?.LastRunId);
        Assert.Equal(WorkflowRunState.Completed, metadata?.LastRunState);
        Assert.Contains(createdFilePath, metadata?.LastCreatedFilePaths ?? []);
    }

    [Fact]
    public async Task ProjectStructureAgentApi_rejects_definition_that_prefers_unregistered_durable_backend()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();

        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var catalogService = scope.ServiceProvider.GetRequiredService<IWorkflowCatalogService>();
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await catalogService.SaveDefinitionAsync(CreateWorkflowDefinitionSaveRequest(
                    "Office365 local backend workflow",
                    runtimePolicy: new WorkflowRuntimePolicy(
                        WorkflowRuntimeBackendKind.DurableTask,
                        AllowInProcessPreviewRuns: true,
                        RequireDurableProductionRuns: true,
                        ExposeAzureFunctionsStatusEndpoint: false,
                        ExposeAzureFunctionsMcpTool: false))));

            Assert.Contains("DurableTask", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("not registered", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ProjectStructureAgentApi_projects_workflow_created_assets_under_workflow_node()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        var project = await CreateWorkflowProjectAsync(host.Client, "Workflow asset projection project");
        var lease = await AcquireProjectLeaseAsync(host.Client, project.Id, "Start workflow asset projection");
        var parent = await CreateProjectBlockAsync(host.Client, project.Id, lease.LeaseToken, "SEAMARK folder", "Folder with xray device PDFs and price lists.");

        WorkflowDefinition definition;
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var catalogService = scope.ServiceProvider.GetRequiredService<IWorkflowCatalogService>();
            definition = await catalogService.SaveDefinitionAsync(CreateProjectStructureAssetWorkflowDefinitionSaveRequest());
        }

        var workflowNode = await PostAndReadAsync<ProjectStructureWorkflowNodeCreateResult>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes/{parent.Id}/workflow-definition",
            new ProjectStructureWorkflowNodeCreateInput(
                definition.Id,
                definition.VersionId,
                InputSettings: ProjectStructureWorkflowInputSettings.Default(),
                LeaseToken: lease.LeaseToken));

        var started = await PostAndReadAsync<ProjectStructureWorkflowNodeStartResult>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes/{workflowNode.Node.Id}/workflow/start",
            new ProjectStructureWorkflowNodeStartInput(WorkflowRuntimeBackendKind.InProcess, LeaseToken: lease.LeaseToken));

        var readback = await PostAndReadAsync<ProjectStructureReadResponse>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/structure/read",
            new ProjectStructureReadRequest(
                IncludeLinks: true,
                IncludeMetadata: true,
                IncludeAssets: true,
                IncludeNotes: true));
        var generatedAsset = Assert.Single(readback.Nodes, node => node.Title == "Workflow generated summary");
        var assetId = generatedAsset.ArtifactId?.ToString("D") ?? generatedAsset.Id;
        var updatedWorkflowNode = Assert.Single(readback.Nodes, node => node.Id == workflowNode.Node.Id);
        var metadata = ProjectObjectMetadataSerializer.Parse(updatedWorkflowNode.MetadataJson).Workflow;

        Assert.Equal(WorkflowRunState.Completed, started.Status.State);
        Assert.Equal(workflowNode.Node.Id, generatedAsset.ParentId);
        Assert.Contains(readback.Links, link => link.SourceId == workflowNode.Node.Id && link.TargetId == generatedAsset.Id);
        Assert.Contains(generatedAsset.Id, started.Status.Summary.CreatedNodeIds);
        Assert.Contains(assetId, started.Status.Summary.CreatedAssetIds);
        Assert.Contains(generatedAsset.Id, metadata?.LastCreatedNodeIds ?? []);
        Assert.Contains(assetId, metadata?.LastCreatedAssetIds ?? []);
    }

    [Fact]
    public async Task ProjectStructureAgentApi_llm_workflow_uses_project_scope_and_creates_markdown_asset_under_workflow_node()
    {
        var runtime = new ProjectScopedWorkflowAgentRuntime();
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync(
            "workflow-llm-project-scope",
            testEnvironment => testEnvironment.CreatePostgreSqlProfile("workflow-llm-project-scope"),
            services =>
            {
                services.AddSingleton<IAgentRuntime>(runtime);
                services.AddScoped<IProviderProfileRegistry>(_ => new SingleProviderProfileRegistry(
                    CreateProviderProfile("deterministic-tetris-summary")));
            });
        var project = await CreateWorkflowProjectAsync(host.Client, "Client Tetris request project");
        var lease = await AcquireProjectLeaseAsync(host.Client, project.Id, "Run Tetris email summary workflow");
        var parent = await CreateProjectBlockAsync(
            host.Client,
            project.Id,
            lease.LeaseToken,
            "Office365 email source",
            "Category-triggered email summary.");
        var component = await PostAndReadAsync<LlmCallComponent>(
            host.Client,
            "/api/workflows/components",
            CreateTetrisSummaryComponentSaveRequest());
        var definition = await PostAndReadAsync<WorkflowDefinition>(
            host.Client,
            "/api/workflows/definitions",
            CreateTetrisSummaryWorkflowDefinitionSaveRequest(component.Id));
        var inputSettings = ProjectStructureWorkflowInputSettings.Default();
        inputSettings.ManualInputJson = """
            {
              "source": "office365",
              "clientEmail": {
                "from": "Jára Cimrman",
                "subject": "Tetris webová hra",
                "bodyText": "Dobrý den,\nPotřebujeme naprogramovat jednoduchou hru Tetris.\nPotřebujeme aby hra byla formou webové stránky. Chtěli bychom ji i jako mobilní aplikaci, ale to asi až později pokud by to bylo složité.\nHra se musí ovládat klávesnicí. Standardně šipkami nebo hráčské klávesy w,s,a,d.\nHra by si měla uložit poslední maximální dosažené skóre. Nicméně nechceme backend. Vše musí jet jen v aplikaci jako takové.Chceme ji hostovat na běžném statickém webhostingu.\nPotřebovali bychom aplikaci nejpozději do jednoho týdne.\nDěkuji,\n\nS pozdravem\n\nJára Cimrman"
              }
            }
            """;

        var workflowNode = await PostAndReadAsync<ProjectStructureWorkflowNodeCreateResult>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes/{parent.Id}/workflow-definition",
            new ProjectStructureWorkflowNodeCreateInput(
                definition.Id,
                definition.VersionId,
                "Tetris email summary workflow",
                InputSettings: inputSettings,
                LeaseToken: lease.LeaseToken));
        var started = await PostAndReadAsync<ProjectStructureWorkflowNodeStartResult>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes/{workflowNode.Node.Id}/workflow/start",
            new ProjectStructureWorkflowNodeStartInput(WorkflowRuntimeBackendKind.InProcess, LeaseToken: lease.LeaseToken));
        var readback = await PostAndReadAsync<ProjectStructureReadResponse>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/structure/read",
            new ProjectStructureReadRequest(
                IncludeLinks: true,
                IncludeAssets: true,
                IncludeNotes: true,
                IncludeMetadata: true));
        var generatedAsset = Assert.Single(readback.Nodes, node => node.Title == "Client email summary");
        var executionOptions = runtime.LastExecutionOptions;
        Assert.NotNull(executionOptions);
        var scope = executionOptions!.ContextWorkspaceScope;

        Assert.Equal(WorkflowRunState.Completed, started.Status.State);
        Assert.NotNull(scope);
        Assert.Equal(WorkspaceScopeKind.Project, scope!.Kind);
        Assert.Equal(project.Id.ToString("D"), scope.Key);
        Assert.Equal(workflowNode.Node.Id, generatedAsset.ParentId);
        Assert.Contains(readback.Links, link => link.SourceId == workflowNode.Node.Id && link.TargetId == generatedAsset.Id);
        Assert.Contains(generatedAsset.Id, started.Status.Summary.CreatedNodeIds);
        Assert.Contains("Tetris", generatedAsset.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("statický webhosting", generatedAsset.Notes, StringComparison.OrdinalIgnoreCase);

        using var payloadDocument = JsonDocument.Parse(runtime.LastPayloadJson);
        var emailBody = payloadDocument.RootElement
            .GetProperty("manualInput")
            .GetProperty("clientEmail")
            .GetProperty("bodyText")
            .GetString();
        Assert.Contains("klávesnicí", emailBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProjectStructureAgentApi_marks_workflow_node_waiting_cancelled_and_failed_states()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        var project = await CreateWorkflowProjectAsync(host.Client, "Workflow state project");
        var lease = await AcquireProjectLeaseAsync(host.Client, project.Id, "Start state workflows");
        var parent = await CreateProjectBlockAsync(host.Client, project.Id, lease.LeaseToken, "SEAMARK folder", "Folder with xray device PDFs and price lists.");

        WorkflowDefinition waitingDefinition;
        WorkflowDefinition completedDefinition;
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var catalogService = scope.ServiceProvider.GetRequiredService<IWorkflowCatalogService>();
            waitingDefinition = await catalogService.SaveDefinitionAsync(CreateHumanInputWorkflowDefinitionSaveRequest());
            completedDefinition = await catalogService.SaveDefinitionAsync(CreateWorkflowDefinitionSaveRequest("Backend failure probe workflow"));
        }

        var waitingWorkflowNode = await PostAndReadAsync<ProjectStructureWorkflowNodeCreateResult>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes/{parent.Id}/workflow-definition",
            new ProjectStructureWorkflowNodeCreateInput(
                waitingDefinition.Id,
                waitingDefinition.VersionId,
                "SEAMARK folder approval",
                InputSettings: ProjectStructureWorkflowInputSettings.Default(),
                LeaseToken: lease.LeaseToken));
        var waitingStart = await PostAndReadAsync<ProjectStructureWorkflowNodeStartResult>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes/{waitingWorkflowNode.Node.Id}/workflow/start",
            new ProjectStructureWorkflowNodeStartInput(WorkflowRuntimeBackendKind.InProcess, LeaseToken: lease.LeaseToken));

        Assert.Equal(WorkflowRunState.WaitingForInput, waitingStart.Status.State);
        Assert.Equal("pause", waitingStart.Status.MarkerIcon);
        Assert.Equal(2, waitingStart.Status.CurrentStepIndex);

        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var runtimeManager = scope.ServiceProvider.GetRequiredService<IWorkflowRuntimeManager>();
            await runtimeManager.CancelAsync(waitingStart.RunId);
        }

        var cancelledStatus = await GetAndReadAsync<ProjectStructureWorkflowRunStatus>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes/{waitingWorkflowNode.Node.Id}/workflow/status");

        Assert.Equal(WorkflowRunState.Cancelled, cancelledStatus.State);
        Assert.Equal("stop", cancelledStatus.MarkerIcon);
        Assert.Equal("Cancelled", cancelledStatus.MarkerLabel);

        var failedWorkflowNode = await PostAndReadAsync<ProjectStructureWorkflowNodeCreateResult>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes/{parent.Id}/workflow-definition",
            new ProjectStructureWorkflowNodeCreateInput(
                completedDefinition.Id,
                completedDefinition.VersionId,
                "Durable backend failure probe",
                InputSettings: ProjectStructureWorkflowInputSettings.Default(),
                LeaseToken: lease.LeaseToken));
        var failedStartResponse = await host.Client.PostAsJsonAsync(
            $"/api/project-structure/projects/{project.Id}/nodes/{failedWorkflowNode.Node.Id}/workflow/start",
            new ProjectStructureWorkflowNodeStartInput(WorkflowRuntimeBackendKind.DurableTask, LeaseToken: lease.LeaseToken));

        Assert.Equal(HttpStatusCode.BadRequest, failedStartResponse.StatusCode);

        var failedStatus = await GetAndReadAsync<ProjectStructureWorkflowRunStatus>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes/{failedWorkflowNode.Node.Id}/workflow/status");

        Assert.Equal(WorkflowRunState.Failed, failedStatus.State);
        Assert.Equal("alert", failedStatus.MarkerIcon);
        Assert.Contains("not registered", failedStatus.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProjectStructureAgentApi_rejects_workflow_start_from_non_workflow_node()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        var project = await CreateWorkflowProjectAsync(host.Client, "Invalid workflow start project");
        var lease = await AcquireProjectLeaseAsync(host.Client, project.Id, "Reject invalid workflow start");
        var parent = await CreateProjectBlockAsync(host.Client, project.Id, lease.LeaseToken, "Regular node", "This is not a workflow node.");

        var response = await host.Client.PostAsJsonAsync(
            $"/api/project-structure/projects/{project.Id}/nodes/{parent.Id}/workflow/start",
            new ProjectStructureWorkflowNodeStartInput(WorkflowRuntimeBackendKind.InProcess, LeaseToken: lease.LeaseToken));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("WorkflowNodeRequired", body);
    }

    [Fact]
    public async Task ProjectStructureAgentApi_returns_actionable_lease_conflicts()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        var scopeKey = $"repo-branch:{IntegrationTestPaths.RepositoryRoot}:main";
        var expectedScopeKey = scopeKey.Replace('\\', '/').ToLowerInvariant();

        await PostAndReadAsync<ProjectStructureLeaseSnapshot>(
            host.Client,
            "/api/project-structure/leases/acquire",
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.RepoBranch,
                scopeKey,
                "Primary branch mutation",
                15));

        using var competingClient = CreateClientForAgent(host.Client.BaseAddress!, "other-agent", "Other Agent", "other-machine");
        var response = await competingClient.PostAsJsonAsync(
            "/api/project-structure/leases/acquire",
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.RepoBranch,
                scopeKey,
                "Competing branch mutation",
                15));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var details = payload.RootElement
            .GetProperty("error")
            .GetProperty("details");
        Assert.Equal(expectedScopeKey, details.GetProperty("scopeKey").GetString());
        Assert.Equal("api-test-agent", details.GetProperty("agentId").GetString());
        Assert.Equal("API Test Agent", details.GetProperty("agentName").GetString());
        Assert.Equal("api-test-machine", details.GetProperty("machineName").GetString());
    }

    [Fact]
    public async Task ProjectStructureAgentApi_queries_dependency_readiness()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();

        var project = await PostAndReadAsync<ProjectSummary>(
            host.Client,
            "/api/project-structure/projects",
            new ProjectStructureProjectSaveRequest(
                "Dependency API project",
                "HTTP dependency validation",
                "Query dependency readiness over the central API.",
                "Execution",
                ProjectStatus.Active));

        var lease = await PostAndReadAsync<ProjectStructureLeaseSnapshot>(
            host.Client,
            "/api/project-structure/leases/acquire",
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.Project,
                project.Id.ToString(),
                "Create dependency graph",
                15));

        var note = await PostAndReadAsync<ProjectStructureNodeSummary>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes",
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.Note,
                "Architect note",
                string.Empty,
                "A top-level note dependency.",
                $"project:{project.Id}",
                360,
                220,
                null,
                null,
                null,
                null,
                null,
                lease.LeaseToken));

        var task = await PostAndReadAsync<ProjectStructureNodeSummary>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes",
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.WorkItem,
                "Implement feature",
                string.Empty,
                "Blocked until the note is completed.",
                $"project:{project.Id}",
                620,
                340,
                new DateTimeOffset(2026, 4, 3, 8, 0, 0, TimeSpan.Zero),
                null,
                "task",
                null,
                null,
                lease.LeaseToken,
                7200));

        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
            await workbench.LinkObjectsAsync(project.Id, task.Id, note.Id, ProjectObjectLinkKind.DependsOn);
        }

        var dependencies = await PostAndReadAsync<ProjectStructureDependencyResponse>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/dependencies/query",
            new ProjectStructureDependencyQueryRequest(DefaultDurationSeconds: 5400));

        var noteItem = Assert.Single(dependencies.Items, item => item.NodeId == note.Id);
        var taskItem = Assert.Single(dependencies.Items, item => item.NodeId == task.Id);

        Assert.Equal(5400, dependencies.DefaultDurationSeconds);
        Assert.True(noteItem.CanExecute);
        Assert.False(taskItem.CanExecute);
        Assert.Equal(7200, taskItem.DurationSeconds);
        Assert.Contains(taskItem.Prerequisites, prerequisite => prerequisite.NodeId == note.Id && prerequisite.Reason == "depends-on");
    }

    [Fact]
    public async Task ProjectStructureAgentApi_accepts_typed_block_aliases_and_node_move_requests()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();

        var project = await PostAndReadAsync<ProjectSummary>(
            host.Client,
            "/api/project-structure/projects",
            new ProjectStructureProjectSaveRequest(
                "Alias API project",
                "HTTP alias validation",
                "Accept typed block aliases and move requests over the central API.",
                "Execution",
                ProjectStatus.Active));

        var lease = await PostAndReadAsync<ProjectStructureLeaseSnapshot>(
            host.Client,
            "/api/project-structure/leases/acquire",
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.Project,
                project.Id.ToString(),
                "Alias and move validation",
                15));

        var placeholder = await PostAndReadAsync<ProjectStructureNodeSummary>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes",
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.Note,
                "Features",
                "Scratch",
                "Placeholder note for reclassification.",
                $"project:{project.Id}",
                420,
                220,
                null,
                null,
                null,
                null,
                null,
                lease.LeaseToken));

        var updateResponse = await PutAndReadJsonAsync<ProjectStructureNodeSummary>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes/{placeholder.Id}",
            """
            {
              "title": "Features",
              "subtitle": "Feature area",
              "notes": "Promoted into a typed feature block through an alias payload.",
              "objectType": "FeatureBlock",
              "leaseToken": "__LEASE__"
            }
            """.Replace("__LEASE__", lease.LeaseToken, StringComparison.Ordinal));

        Assert.Equal(ProjectObjectType.ProjectBlock, updateResponse.ObjectType);
        Assert.Equal("feature", updateResponse.ObjectSubtype);

        var moveAck = await PostAndReadAsync<OperationAck>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes/move",
            new ProjectStructureNodeMoveInput(placeholder.Id, 1040, 560, lease.LeaseToken));

        Assert.True(moveAck.Ok);

        var readback = await PostAndReadAsync<ProjectStructureReadResponse>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/structure/read",
            new ProjectStructureReadRequest(
                NodeIds: [placeholder.Id],
                IncludeLayout: true,
                IncludeNotes: true,
                IncludeMetadata: true));

        var movedNode = Assert.Single(readback.Nodes);
        Assert.Equal(ProjectObjectType.ProjectBlock, movedNode.ObjectType);
        Assert.Equal("feature", movedNode.ObjectSubtype);
        Assert.Equal(1040d, movedNode.X);
        Assert.Equal(560d, movedNode.Y);
    }

    [Fact]
    public async Task ProjectStructureAgentApi_supports_focused_node_dependency_and_asset_commands()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();

        var project = await PostAndReadAsync<ProjectSummary>(
            host.Client,
            "/api/project-structure/projects",
            new ProjectStructureProjectSaveRequest(
                "Command API project",
                "Focused node command validation",
                "Validate narrow project-structure API commands.",
                "Execution",
                ProjectStatus.Active));

        var lease = await PostAndReadAsync<ProjectStructureLeaseSnapshot>(
            host.Client,
            "/api/project-structure/leases/acquire",
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.Project,
                project.Id.ToString(),
                "Focused node command validation",
                15));

        var parent = await PostAndReadAsync<ProjectStructureNodeSummary>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes",
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.ProjectBlock,
                "Parent block",
                string.Empty,
                "Parent for focused command validation.",
                $"project:{project.Id}",
                360,
                220,
                null,
                null,
                "delivery",
                null,
                null,
                lease.LeaseToken));

        var child = await PostAndReadAsync<ProjectStructureNodeSummary>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes",
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.Note,
                "Command target",
                string.Empty,
                "Node that will be mutated through focused API calls.",
                parent.Id,
                620,
                340,
                null,
                null,
                null,
                null,
                null,
                lease.LeaseToken));

        var typedChild = await PostAndReadAsync<ProjectStructureNodeSummary>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes/{child.Id}/type",
            new ProjectStructureNodeTypeInput(ProjectObjectType.WorkItem, "task", lease.LeaseToken));
        Assert.Equal(ProjectObjectType.WorkItem, typedChild.ObjectType);
        Assert.Equal("task", typedChild.ObjectSubtype);

        Assert.Equal(1, await PostAndReadAsync<int>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes/{child.Id}/status",
            new ProjectStructureStatusInput("blocked", lease.LeaseToken)));
        Assert.Equal(1, await PostAndReadAsync<int>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes/{child.Id}/progress",
            new ProjectStructureProgressInput("manual", 35, lease.LeaseToken)));
        Assert.Equal(1, await PostAndReadAsync<int>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes/{child.Id}/markers",
            new ProjectStructureMarkerInput(
                ProjectStructureMarkerMutationMode.Replace,
                "flag",
                "warning",
                "Review",
                lease.LeaseToken)));
        Assert.Equal(1, await PostAndReadAsync<int>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes/{child.Id}/priority",
            new ProjectStructurePriorityInput(6, lease.LeaseToken)));

        var dependency = await PostAndReadAsync<ProjectStructureLinkChangeResult>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/dependencies/link",
            new ProjectStructureLinkInput(child.Id, parent.Id, ProjectObjectLinkKind.Uses, lease.LeaseToken));
        Assert.True(dependency.Changed);
        Assert.Equal(ProjectObjectLinkKind.DependsOn, dependency.Link.Kind);

        var asset = await PostAndReadAsync<ProjectStructureNodeSummary>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/assets",
            new ProjectStructureAssetCreateInput(
                ProjectObjectType.File,
                "Command evidence",
                "Attachment",
                "Attachment created through the focused asset API.",
                CreateMediaPayload("command-evidence.txt", "text/plain", "asset payload"),
                child.Id,
                "text",
                null,
                lease.LeaseToken));

        var content = await GetAndReadAsync<ProjectStructureAssetContentDescriptor>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/assets/{asset.Id}/content");

        var readback = await PostAndReadAsync<ProjectStructureReadResponse>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/structure/read",
            new ProjectStructureReadRequest(
                IncludeLinks: true,
                IncludeAssets: true,
                IncludeNotes: true,
                IncludeMetadata: true));

        var updatedChild = Assert.Single(readback.Nodes, node => node.Id == child.Id);
        Assert.Equal(ProjectObjectType.WorkItem, updatedChild.ObjectType);
        Assert.Equal("task", updatedChild.ObjectSubtype);
        Assert.Equal("blocked", updatedChild.Status);
        Assert.Equal("progress", updatedChild.ProgressMode);
        Assert.Equal(35, updatedChild.ProgressPercent);
        Assert.Equal("Review", updatedChild.MarkerLabel);
        Assert.Equal(6, updatedChild.Priority);
        Assert.Contains(readback.Links, link => link.SourceId == child.Id && link.TargetId == parent.Id && link.Kind == ProjectObjectLinkKind.DependsOn);
        Assert.Equal("command-evidence.txt", asset.MediaOriginalFileName);
        Assert.Equal("asset payload", Encoding.UTF8.GetString(Convert.FromBase64String(content.Base64Data)));
    }

    private static async Task<T> PostAndReadAsync<T>(HttpClient client, string path, object request)
    {
        var response = await client.PostAsJsonAsync(path, request);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode}). Body: {errorBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<T>();
        return payload ?? throw new InvalidOperationException($"No payload was returned for '{path}'.");
    }

    private static Task<ProjectSummary> CreateWorkflowProjectAsync(HttpClient client, string name)
    {
        return PostAndReadAsync<ProjectSummary>(
            client,
            "/api/project-structure/projects",
            new ProjectStructureProjectSaveRequest(
                name,
                "Workflow project-structure validation",
                "Validate workflow nodes from project structure.",
                "Execution",
                ProjectStatus.Active));
    }

    private static Task<ProjectStructureLeaseSnapshot> AcquireProjectLeaseAsync(
        HttpClient client,
        Guid projectId,
        string reason)
    {
        return PostAndReadAsync<ProjectStructureLeaseSnapshot>(
            client,
            "/api/project-structure/leases/acquire",
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString(),
                reason,
                15));
    }

    private static Task<ProjectStructureNodeSummary> CreateProjectBlockAsync(
        HttpClient client,
        Guid projectId,
        string leaseToken,
        string title,
        string notes)
    {
        return PostAndReadAsync<ProjectStructureNodeSummary>(
            client,
            $"/api/project-structure/projects/{projectId}/nodes",
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.ProjectBlock,
                title,
                "Workflow input",
                notes,
                $"project:{projectId}",
                ObjectSubtype: "workflow-input",
                LeaseToken: leaseToken));
    }

    private static WorkflowDefinitionSaveRequest CreateWorkflowDefinitionSaveRequest(
        string name = "Order reconciliation workflow",
        WorkflowLifecycleStatus status = WorkflowLifecycleStatus.Active,
        WorkflowRuntimePolicy? runtimePolicy = null)
    {
        return new WorkflowDefinitionSaveRequest(
            Id: null,
            ExpectedVersionId: null,
            Name: name,
            Description: "Checks order documents and produces a concise procurement summary.",
            Status: status,
            Graph: new WorkflowGraph(
                new WorkflowNodeId("start"),
                [
                    CreateWorkflowNode("start", WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                    CreateWorkflowNode("logic", WorkflowNodeKind.StrictLogic, inputShape: WorkflowValueShape.Text, resultShape: WorkflowValueShape.Text),
                    CreateWorkflowNode("end", WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
                ],
                [
                    CreateWorkflowEdge("start-to-logic", "start", "logic"),
                    CreateWorkflowEdge("logic-to-end", "logic", "end")
                ]),
            RuntimePolicy: runtimePolicy ?? new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false));
    }

    private static WorkflowDefinitionSaveRequest CreateHumanInputWorkflowDefinitionSaveRequest()
    {
        return new WorkflowDefinitionSaveRequest(
            Id: null,
            ExpectedVersionId: null,
            Name: "Human approval workflow",
            Description: "Waits for human input before producing a workflow result.",
            Status: WorkflowLifecycleStatus.Active,
            Graph: new WorkflowGraph(
                new WorkflowNodeId("start"),
                [
                    CreateWorkflowNode("start", WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                    CreateWorkflowNode("approval", WorkflowNodeKind.HumanInput, inputShape: WorkflowValueShape.Text, resultShape: WorkflowValueShape.Text),
                    CreateWorkflowNode("end", WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
                ],
                [
                    CreateWorkflowEdge("start-to-approval", "start", "approval"),
                    CreateWorkflowEdge("approval-to-end", "approval", "end")
                ]),
            RuntimePolicy: new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false));
    }

    private static WorkflowDefinitionSaveRequest CreateProjectStructureAssetWorkflowDefinitionSaveRequest()
    {
        var executorSettingsJson = JsonSerializer.Serialize(
            new WorkflowProjectStructureExecutorSettings
            {
                Operation = WorkflowProjectStructureOperation.CreateAsset,
                AssetKind = "md",
                Title = "Workflow generated summary",
                Content = "Project-structure result created by a workflow run.",
                ContentType = "text/markdown"
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return new WorkflowDefinitionSaveRequest(
            Id: null,
            ExpectedVersionId: null,
            Name: "Project-structure asset workflow",
            Description: "Creates a workflow result asset under the workflow node.",
            Status: WorkflowLifecycleStatus.Active,
            Graph: new WorkflowGraph(
                new WorkflowNodeId("start"),
                [
                    CreateWorkflowNode("start", WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                    CreateExecutorWorkflowNode("create-asset", WorkflowExecutorIds.ProjectStructure, executorSettingsJson),
                    CreateWorkflowNode("end", WorkflowNodeKind.End, inputShape: CreateJsonShape())
                ],
                [
                    CreateWorkflowEdge("start-to-asset", "start", "create-asset"),
                    CreateWorkflowEdge("asset-to-end", "create-asset", "end")
                ]),
            RuntimePolicy: new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false));
    }

    private static LlmCallComponentSaveRequest CreateTetrisSummaryComponentSaveRequest()
        => new(
            Id: null,
            Name: "Tetris email summary",
            ProviderProfileId: null,
            Model: "deterministic-tetris-summary",
            Modality: WorkflowModality.Text,
            ModelSettings: new WorkflowModelSettings(
                Temperature: 0,
                MaxOutputTokens: 1200,
                RequireJsonOutput: true,
                ResponseFormatJsonSchema: string.Empty),
            Instructions: "Summarize the client email as markdown, preserve projectId and nodeId, and return JSON only.",
            InputShape: CreateJsonShape(),
            ResultShape: CreateJsonShape(),
            Permissions: AgentPermissionsPolicy.Default);

    private static WorkflowDefinitionSaveRequest CreateTetrisSummaryWorkflowDefinitionSaveRequest(WorkflowComponentId componentId)
    {
        var assetSettingsJson = JsonSerializer.Serialize(
            new WorkflowProjectStructureExecutorSettings
            {
                Operation = WorkflowProjectStructureOperation.CreateAsset,
                ProjectIdJsonPath = "$.projectId",
                NodeIdJsonPath = "$.nodeId",
                AssetKind = "md",
                Title = "Client email summary",
                ContentFromInput = true,
                IncludeInputPayload = true,
                ContentType = "text/markdown"
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return new WorkflowDefinitionSaveRequest(
            Id: null,
            ExpectedVersionId: null,
            Name: "Client email summary workflow",
            Description: "Summarizes a client email and stores markdown under the workflow node.",
            Status: WorkflowLifecycleStatus.Active,
            Graph: new WorkflowGraph(
                new WorkflowNodeId("start"),
                [
                    CreateWorkflowNode("start", WorkflowNodeKind.Start, resultShape: CreateJsonShape()),
                    CreateLlmWorkflowNode("summarize-client-email", componentId),
                    CreateExecutorWorkflowNode("store-client-summary", WorkflowExecutorIds.ProjectStructure, assetSettingsJson, CreateJsonShape()),
                    CreateWorkflowNode("end", WorkflowNodeKind.End, inputShape: CreateJsonShape())
                ],
                [
                    CreateWorkflowEdge("start-to-summary", "start", "summarize-client-email"),
                    CreateWorkflowEdge("summary-to-store", "summarize-client-email", "store-client-summary"),
                    CreateWorkflowEdge("store-to-end", "store-client-summary", "end")
                ]),
            RuntimePolicy: new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false));
    }

    private static WorkflowNode CreateWorkflowNode(
        string id,
        WorkflowNodeKind kind,
        WorkflowValueShape? inputShape = null,
        WorkflowValueShape? resultShape = null)
    {
        return new WorkflowNode(
            new WorkflowNodeId(id),
            kind,
            id,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: inputShape ?? WorkflowValueShape.Text,
                ResultShape: resultShape ?? WorkflowValueShape.Text));
    }

    private static WorkflowNode CreateLlmWorkflowNode(string id, WorkflowComponentId componentId)
    {
        return new WorkflowNode(
            new WorkflowNodeId(id),
            WorkflowNodeKind.LlmCall,
            id,
            [],
            new WorkflowNodeSettings(
                componentId,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: CreateJsonShape(),
                ResultShape: CreateJsonShape()));
    }

    private static WorkflowNode CreateExecutorWorkflowNode(
        string id,
        WorkflowExecutorId executorId,
        string executorSettingsJson,
        WorkflowValueShape? inputShape = null)
    {
        return new WorkflowNode(
            new WorkflowNodeId(id),
            WorkflowNodeKind.Executor,
            id,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: inputShape ?? WorkflowValueShape.Text,
                ResultShape: CreateJsonShape()) with
            {
                ExecutorId = executorId,
                ExecutorSettingsJson = executorSettingsJson,
                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
            });
    }

    private static WorkflowValueShape CreateJsonShape()
        => new(WorkflowValueShapeKind.Json, "{}", "JSON payload");

    private static ProviderProfile CreateProviderProfile(string defaultModel)
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            "Workflow integration provider",
            ProviderKind.OpenAi,
            "https://api.openai.com/v1",
            "WORKFLOW_INTEGRATION_API_KEY",
            defaultModel,
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: [],
            Purpose: ProviderProfilePurpose.Chat);
    }

    private static WorkflowEdge CreateWorkflowEdge(string id, string source, string target)
    {
        return new WorkflowEdge(
            new WorkflowEdgeId(id),
            new WorkflowNodeId(source),
            SourcePortId: null,
            new WorkflowNodeId(target),
            TargetPortId: null,
            WorkflowEdgeKind.Direct,
            ConditionExpression: string.Empty);
    }

    private static async Task<T> GetAndReadAsync<T>(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode}). Body: {errorBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<T>();
        return payload ?? throw new InvalidOperationException($"No payload was returned for '{path}'.");
    }

    private static async Task<T> PutAndReadJsonAsync<T>(HttpClient client, string path, string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PutAsync(path, content);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode}). Body: {errorBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<T>();
        return payload ?? throw new InvalidOperationException($"No payload was returned for '{path}'.");
    }

    private static ProjectObjectMediaPayload CreateMediaPayload(string fileName, string contentType, string textContent)
    {
        return new ProjectObjectMediaPayload(
            fileName,
            contentType,
            Convert.ToBase64String(Encoding.UTF8.GetBytes(textContent)));
    }

    private static HttpClient CreateClientForAgent(Uri baseAddress, string agentId, string agentName, string machineName)
    {
        var client = new HttpClient
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.AgentId, agentId);
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.AgentName, agentName);
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.MachineName, machineName);
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.RepositoryRoot, IntegrationTestPaths.RepositoryRoot);
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.BranchName, "tests/project-structure");
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.SessionId, Guid.NewGuid().ToString("N"));
        return client;
    }

    private sealed class ProjectScopedWorkflowAgentRuntime : IAgentRuntime
    {
        public AgentRuntimeExecutionOptions? LastExecutionOptions { get; private set; }

        public string LastPrompt { get; private set; } = string.Empty;

        public string LastPayloadJson { get; private set; } = "{}";

        public Task<AgentRuntimeResponse> RunAsync(
            AgentDefinition agent,
            ProviderProfile provider,
            ChatSessionRecord session,
            IReadOnlyList<CapabilityCatalogItem> capabilities,
            IReadOnlyList<AgentMemoryRecord> memory,
            string prompt,
            string? runtimeSessionKey,
            Func<ExecutionState, string, string, Task> progressCallback,
            CancellationToken cancellationToken = default,
            bool suppressApprovalRequirements = false,
            AgentStructuredOutputContract? structuredOutput = null,
            AgentRuntimeExecutionOptions? executionOptions = null)
        {
            _ = agent;
            _ = provider;
            _ = session;
            _ = capabilities;
            _ = memory;
            _ = runtimeSessionKey;
            _ = progressCallback;
            _ = suppressApprovalRequirements;
            _ = structuredOutput;
            cancellationToken.ThrowIfCancellationRequested();
            LastPrompt = prompt;
            LastExecutionOptions = executionOptions;
            var payload = ExtractWorkflowPayload(prompt);
            LastPayloadJson = payload;
            using var document = JsonDocument.Parse(payload);
            var projectId = document.RootElement.GetProperty("projectId").GetString();
            var nodeId = document.RootElement.GetProperty("nodeId").GetString();
            JsonObject response = new()
            {
                ["markdown"] = JsonValue.Create("""
                    # Tetris request summary

                    - Client asks for a simple Tetris game as a web page.
                    - Controls must support arrow keys and W/S/A/D.
                    - The app must store the best score locally without a backend.
                    - Hosting target is statický webhosting.
                    - Deadline is one week.
                    """),
                ["projectId"] = JsonValue.Create(projectId),
                ["nodeId"] = JsonValue.Create(nodeId),
                ["source"] = JsonValue.Create("office365")
            };

            if (document.RootElement.TryGetProperty("runContext", out var runContext))
            {
                response["runContext"] = JsonNode.Parse(runContext.GetRawText());
            }

            var responseJson = response.ToJsonString();
            return Task.FromResult(new AgentRuntimeResponse(
                responseJson,
                InputTokens: 42,
                OutputTokens: 64,
                ToolCalls: 0,
                RuntimeSessionKey: string.Empty,
                SerializedSessionStateJson: null,
                PendingApprovals: []));
        }

        public Task<AgentRuntimeResponse> RespondToPendingApprovalsAsync(
            AgentDefinition agent,
            ProviderProfile provider,
            ChatSessionRecord session,
            IReadOnlyList<CapabilityCatalogItem> capabilities,
            IReadOnlyList<AgentMemoryRecord> memory,
            bool approved,
            string? runtimeSessionKey,
            Func<ExecutionState, string, string, Task> progressCallback,
            CancellationToken cancellationToken = default,
            bool suppressApprovalRequirements = false,
            AgentStructuredOutputContract? structuredOutput = null,
            AgentRuntimeExecutionOptions? executionOptions = null)
            => throw new NotSupportedException();

        public Task<OllamaModelfileResult> CreateOrUpdateOllamaModelAsync(
            ProviderProfile provider,
            OllamaModelfileRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProviderHealthResult> TestProviderAsync(
            ProviderProfile provider,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProviderTestChatResult> RunProviderTestChatAsync(
            ProviderProfile provider,
            ProviderTestChatRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        private static string ExtractWorkflowPayload(string prompt)
        {
            const string marker = "Workflow input payload:";
            var index = prompt.IndexOf(marker, StringComparison.Ordinal);
            return index < 0
                ? "{}"
                : prompt[(index + marker.Length)..].Trim();
        }
    }

    private sealed class SingleProviderProfileRegistry(ProviderProfile provider) : IProviderProfileRegistry
    {
        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<ProviderProfile>>([provider]);
        }

        public Task<ProviderProfile?> GetProviderAsync(
            Guid providerId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(provider.Id == providerId ? provider : null);
        }

        public Task<ProviderProfileEditorModel> GetProviderEditorAsync(
            Guid? providerId = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Guid> SaveProviderAsync(
            ProviderProfileEditorModel model,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteProviderAsync(
            Guid providerId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProviderProfile> UpdateProviderAsync(
            Guid providerId,
            Func<ProviderProfile, ProviderProfile> update,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}

