using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectStructureWorkflowScenarioHarnessTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    [Fact]
    public async Task WorkflowScenarioHarness_runs_twenty_one_project_structure_workflow_cases()
    {
        var proofRoot = Path.Combine(
            IntegrationTestPaths.RepositoryRoot,
            "artifacts",
            "codex-bundles",
            "project-structure-workflow-runs",
            "proof",
            "scenarios");
        var syntheticRoot = Path.Combine(proofRoot, "synthetic-inputs");

        await PrepareSyntheticInputsAsync(syntheticRoot);
        var scenarios = BuildScenarios(syntheticRoot);
        Assert.Equal(21, scenarios.Count);
        Assert.All(scenarios, ValidateScenarioSources);

        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync(
            "workflow-scenario-harness",
            testEnvironment => testEnvironment.CreatePostgreSqlProfile("scenario-harness"),
            ConfigureGroundedWorkflowServices);
        var execution = await RunHarnessAsync(
            host,
            proofRoot,
            scenarios,
            "scenario-harness-results.json",
            "InProcess deterministic workflow definitions; provider-specific runs are owned by workstream 07.");

        AssertScenarioHarnessResults(execution);
    }

    [Fact]
    public async Task WorkflowScenarioHarness_runs_twenty_one_project_structure_workflow_cases_on_postgresql()
    {
        var availability = await PostgresTestAvailability.EnsureAvailableAsync(IntegrationTestPaths.RepositoryRoot);
        Assert.True(availability.IsAvailable, availability.Message);
        Assert.False(string.IsNullOrWhiteSpace(availability.ConnectionString));

        var proofRoot = Path.Combine(
            IntegrationTestPaths.RepositoryRoot,
            "artifacts",
            "codex-bundles",
            "project-structure-workflow-runs",
            "proof",
            "scenarios");
        var syntheticRoot = Path.Combine(proofRoot, "synthetic-inputs");

        await PrepareSyntheticInputsAsync(syntheticRoot);
        var scenarios = BuildScenarios(syntheticRoot);
        Assert.Equal(21, scenarios.Count);
        Assert.All(scenarios, ValidateScenarioSources);

        var databaseName = $"cditall_wf_{Guid.NewGuid():N}"[..30];
        await CreateDatabaseAsync(availability.ConnectionString!, databaseName);

        try
        {
            await using var host = await ProjectStructureAgentApiTestHost.CreateAsync(
                "workflow-scenario-harness-postgres",
                testEnvironment => testEnvironment.CreatePostgreSqlProfile(
                    "scenario-harness-postgres",
                    BuildDatabaseConnectionString(availability.ConnectionString!, databaseName)),
                ConfigureGroundedWorkflowServices);
            var execution = await RunHarnessAsync(
                host,
                proofRoot,
                scenarios,
                "scenario-harness-postgresql-results.json",
                "InProcess deterministic workflow definitions on PostgreSQL; provider-specific runs are owned by workstream 07.");

            AssertScenarioHarnessResults(execution);
            Assert.Equal(TestDatabaseProviderKind.PostgreSql.ToString(), execution.DatabaseProvider);
        }
        finally
        {
            await DropDatabaseAsync(availability.ConnectionString!, databaseName);
        }
    }

    private static async Task<HarnessExecutionResult> RunHarnessAsync(
        ProjectStructureAgentApiTestHost host,
        string proofRoot,
        IReadOnlyList<WorkflowScenario> scenarios,
        string resultFileName,
        string providerPlan)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(proofRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(resultFileName);

        var project = await PostAndReadAsync<ProjectSummary>(
            host.Client,
            "/api/project-structure/projects",
            new ProjectStructureProjectSaveRequest(
                "Workflow scenario harness",
                "Twenty-one real-world project-structure workflow cases",
                "Validates workflow nodes, input preview, result-node projection, and file-summary artifacts.",
                "Validation",
                ProjectStatus.Active));
        var lease = await PostAndReadAsync<ProjectStructureLeaseSnapshot>(
            host.Client,
            "/api/project-structure/leases/acquire",
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.Project,
                project.Id.ToString(),
                "Run workflow scenario harness",
                30));

        var results = new List<ScenarioRunResult>();
        foreach (var scenario in scenarios)
        {
            results.Add(await RunScenarioAsync(host, project, lease.LeaseToken, scenario));
        }

        var resultPath = Path.Combine(proofRoot, resultFileName);
        Directory.CreateDirectory(proofRoot);
        await File.WriteAllTextAsync(
            resultPath,
            JsonSerializer.Serialize(
                new ScenarioHarnessResult(
                    DateTimeOffset.UtcNow,
                    host.ActiveProfile.Provider.ToString(),
                    host.ActiveProfile.ProfileKey,
                    providerPlan,
                    results),
                JsonOptions));

        return new HarnessExecutionResult(
            host.ActiveProfile.Provider.ToString(),
            host.ActiveProfile.ProfileKey,
            resultPath,
            results);
    }

    private static void AssertScenarioHarnessResults(HarnessExecutionResult execution)
    {
        Assert.All(execution.Scenarios, result =>
        {
            Assert.Equal(WorkflowRunState.Completed.ToString(), result.State);
            Assert.Equal(100, result.ProgressPercent);
            Assert.NotEmpty(result.CreatedNodeIds);
        });
        Assert.Contains(execution.Scenarios, result => result.Id == "S01" && result.Validations.Any(item => item.Contains("89566550", StringComparison.Ordinal)));
        Assert.Contains(execution.Scenarios, result => result.Id == "S03" && result.Validations.Any(item => item.Contains("ZM-x5600", StringComparison.Ordinal)));
        Assert.Contains(execution.Scenarios, result => result.Id == "S06" && result.Validations.Any(item => item.Contains("Summary Rozpočet", StringComparison.Ordinal)));
        Assert.Contains(execution.Scenarios, result => result.Id == "S17" && result.CreatedFilePaths.Contains("samples/workflows/scenario-harness/S17-file-save-result.md"));
        Assert.True(File.Exists(execution.ResultPath), $"Expected scenario harness result artifact at '{execution.ResultPath}'.");
    }

    private static void ConfigureGroundedWorkflowServices(IServiceCollection services)
    {
        services.RemoveAll<IWorkflowLlmComponentInvoker>();
        services.AddSingleton<IWorkflowLlmComponentInvoker, GroundedScenarioWorkflowLlmInvoker>();
    }

    private static async Task<ScenarioRunResult> RunScenarioAsync(
        ProjectStructureAgentApiTestHost host,
        ProjectSummary project,
        string leaseToken,
        WorkflowScenario scenario)
    {
        var parent = await PostAndReadAsync<ProjectStructureNodeSummary>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes",
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.ProjectBlock,
                scenario.ParentTitle,
                "Workflow input",
                scenario.ParentNotes,
                $"project:{project.Id}",
                ObjectSubtype: "workflow-scenario",
                LeaseToken: leaseToken));
        var childNodes = new List<ProjectStructureNodeSummary>();
        foreach (var child in scenario.ChildNodes)
        {
            childNodes.Add(await CreateScenarioChildAsync(
                host.Client,
                project.Id,
                parent.Id,
                leaseToken,
                child));
        }

        var component = await PostAndReadAsync<LlmCallComponent>(
            host.Client,
            "/api/workflows/components",
            CreateScenarioComponentRequest(scenario));
        var definition = await PostAndReadAsync<WorkflowDefinition>(
            host.Client,
            "/api/workflows/definitions",
            CreateScenarioWorkflowDefinitionSaveRequest(scenario, component.Id));

        var inputSettings = ProjectStructureWorkflowInputSettings.Default();
        inputSettings.IncludeParentSubtree = scenario.IncludeParentSubtree;
        inputSettings.SelectedNodeIds = scenario.IncludeSelectedNodes
            ? childNodes.Select(node => node.Id).ToArray()
            : [];
        inputSettings.AdditionalSources = scenario.Sources
            .Select(source => new ProjectStructureWorkflowInputSource(
                source.Kind,
                source.Key,
                source.Label,
                source.Value))
            .ToArray();
        inputSettings.ManualInputJson = JsonSerializer.Serialize(
            new ScenarioManualInput(
                scenario.Id,
                scenario.Title,
                scenario.Instructions,
                scenario.ExpectedPhrases),
            JsonOptions);

        var options = await PostAndReadAsync<ProjectStructureWorkflowAddOptionsResult>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes/{parent.Id}/workflow-add-options",
            new ProjectStructureWorkflowAddOptionsInput(
                definition.Id,
                definition.VersionId,
                inputSettings,
                inputSettings.SelectedNodeIds));
        ValidateInputPreview(options, scenario, project, parent, childNodes);

        var workflowNode = await PostAndReadAsync<ProjectStructureWorkflowNodeCreateResult>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes/{parent.Id}/workflow-definition",
            new ProjectStructureWorkflowNodeCreateInput(
                definition.Id,
                definition.VersionId,
                $"{scenario.Id} {scenario.Title}",
                InputSettings: inputSettings,
                LeaseToken: leaseToken));
        var started = await PostAndReadAsync<ProjectStructureWorkflowNodeStartResult>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes/{workflowNode.Node.Id}/workflow/start",
            new ProjectStructureWorkflowNodeStartInput(WorkflowRuntimeBackendKind.InProcess, LeaseToken: leaseToken));

        Assert.True(
            started.Status.State == WorkflowRunState.Completed,
            JsonSerializer.Serialize(started.Status, JsonOptions));
        Assert.Equal("complete", started.Status.ProgressMode);
        Assert.Equal(100, started.Status.ProgressPercent);

        var status = await GetAndReadAsync<ProjectStructureWorkflowRunStatus>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes/{workflowNode.Node.Id}/workflow/status");
        var readback = await PostAndReadAsync<ProjectStructureReadResponse>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/structure/read",
            new ProjectStructureReadRequest(
                IncludeLinks: true,
                IncludeAssets: true,
                IncludeNotes: true,
                IncludeMetadata: true));
        var createdNodes = readback.Nodes
            .Where(node => status.Summary.CreatedNodeIds.Contains(node.Id, StringComparer.Ordinal))
            .ToArray();

        Assert.NotEmpty(createdNodes);
        Assert.All(createdNodes, node => Assert.Equal(workflowNode.Node.Id, node.ParentId));
        Assert.Contains(readback.Links, link => link.SourceId == workflowNode.Node.Id && createdNodes.Any(node => node.Id == link.TargetId));
        Assert.NotEmpty(status.Summary.CreatedAssetIds);
        foreach (var expectedPhrase in scenario.ExpectedPhrases)
        {
            Assert.Contains(
                createdNodes,
                node => (node.Notes ?? string.Empty).Contains(expectedPhrase, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(scenario.FileOutputPath))
        {
            Assert.Contains(scenario.FileOutputPath, status.Summary.CreatedFilePaths);
        }

        return new ScenarioRunResult(
            scenario.Id,
            scenario.Title,
            definition.Name,
            started.RunId.Value.ToString("D"),
            status.State.ToString(),
            status.ProgressPercent,
            status.CurrentStepIndex,
            status.StepCount,
            status.Summary.CreatedNodeIds,
            status.Summary.CreatedAssetIds,
            status.Summary.CreatedFilePaths,
            scenario.ExpectedPhrases
                .Select(phrase => $"Validated result contains '{phrase}'.")
                .ToArray());
    }

    private static async Task<ProjectStructureNodeSummary> CreateScenarioChildAsync(
        HttpClient client,
        Guid projectId,
        string parentNodeKey,
        string leaseToken,
        ScenarioChildNode child)
    {
        if (!RequiresManagedAssetCreation(child))
        {
            if (child.ManagedAssetSource is not null)
            {
                throw new InvalidOperationException(
                    $"Scenario child '{child.Title}' provides a managed asset source but is not a File or Mermaid asset.");
            }

            return await PostAndReadAsync<ProjectStructureNodeSummary>(
                client,
                $"/api/project-structure/projects/{projectId}/nodes",
                new ProjectStructureNodeCreateInput(
                    child.ObjectType,
                    child.Title,
                    child.Subtitle,
                    child.Notes,
                    parentNodeKey,
                    ObjectSubtype: child.ObjectSubtype,
                    LeaseToken: leaseToken));
        }

        var source = child.ManagedAssetSource ?? throw new InvalidOperationException(
            $"Scenario asset child '{child.Title}' requires an explicit managed asset source.");
        var fileName = Path.GetFileName(source.Path);
        var content = await File.ReadAllBytesAsync(source.Path);
        var media = new ProjectObjectMediaPayload(
            fileName,
            ProjectStructureAssetMediaTypePolicy.Resolve(null, fileName),
            Convert.ToBase64String(content));

        var created = await PostAndReadAsync<ProjectStructureNodeSummary>(
            client,
            $"/api/project-structure/projects/{projectId}/assets",
            new ProjectStructureAssetCreateInput(
                child.ObjectType,
                child.Title,
                child.Subtitle,
                child.Notes,
                media,
                ParentNodeKey: parentNodeKey,
                ObjectSubtype: child.ObjectSubtype,
                LeaseToken: leaseToken));

        if (source.VerifyPersistedContent)
        {
            await AssertManagedAssetRoundTripAsync(
                client,
                projectId,
                created,
                fileName,
                media.ContentType,
                content);
        }

        return created;
    }

    private static async Task AssertManagedAssetRoundTripAsync(
        HttpClient client,
        Guid projectId,
        ProjectStructureNodeSummary created,
        string expectedFileName,
        string expectedContentType,
        byte[] expectedContent)
    {
        var asset = await GetAndReadAsync<ProjectStructureAssetDescriptor>(
            client,
            $"/api/project-structure/projects/{projectId}/assets/{created.Id}");
        Assert.Equal(projectId, asset.ProjectId);
        Assert.Equal(created.Id, asset.NodeId);
        Assert.Equal(created.ObjectType, asset.ObjectType);
        Assert.Equal(created.ObjectSubtype, asset.ObjectSubtype);
        Assert.Equal(expectedFileName, asset.MediaOriginalFileName);
        Assert.Equal(expectedContentType, asset.MediaContentType);
        Assert.Equal(created.MediaRelativePath, asset.MediaRelativePath);

        var persistedContent = await GetAndReadAsync<ProjectStructureAssetContentDescriptor>(
            client,
            $"/api/project-structure/projects/{projectId}/assets/{created.Id}/content");
        Assert.False(persistedContent.Base64DataOmitted);
        Assert.Equal(expectedContent.LongLength, persistedContent.ContentLength);
        Assert.Equal(asset, persistedContent.Asset);
        Assert.Equal(Convert.ToBase64String(expectedContent), persistedContent.Base64Data);
    }

    private static bool RequiresManagedAssetCreation(ScenarioChildNode child)
    {
        var normalizedSubtype = ProjectStructureRequestedNodeKindParser.NormalizeSubtypeForType(
            child.ObjectType,
            child.ObjectSubtype);
        return child.ObjectType == ProjectObjectType.File ||
               string.Equals(
                   normalizedSubtype,
                   ProjectFileSubtype.Mermaid.ToString(),
                   StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateInputPreview(
        ProjectStructureWorkflowAddOptionsResult options,
        WorkflowScenario scenario,
        ProjectSummary project,
        ProjectStructureNodeSummary parent,
        IReadOnlyList<ProjectStructureNodeSummary> childNodes)
    {
        Assert.Equal(project.Id, options.ProjectId);
        Assert.Equal(parent.Id, options.ParentNode.Id);
        Assert.Contains("Project", options.Preview.Summary);
        Assert.Contains("Parent node", options.Preview.Summary);

        using var payload = JsonDocument.Parse(options.Preview.InputJson);
        var root = payload.RootElement;
        Assert.Equal(project.Id, root.GetProperty("project").GetProperty("id").GetGuid());
        Assert.Equal(parent.Id, root.GetProperty("parentNode").GetProperty("id").GetString());
        Assert.Equal(scenario.Id, root.GetProperty("manualInput").GetProperty("scenarioId").GetString());

        foreach (var source in scenario.Sources)
        {
            Assert.Contains(
                root.GetProperty("sources").EnumerateArray(),
                item => item.GetProperty("key").GetString() == source.Key &&
                        item.GetProperty("value").GetString() == source.Value);
        }

        if (scenario.IncludeParentSubtree && childNodes.Count > 0)
        {
            Assert.Contains(
                root.GetProperty("parentSubtree").EnumerateArray(),
                item => item.GetProperty("title").GetString() == childNodes[0].Title);
        }

        if (scenario.IncludeSelectedNodes && childNodes.Count > 0)
        {
            Assert.Contains(
                root.GetProperty("selectedNodes").EnumerateArray(),
                item => item.GetProperty("title").GetString() == childNodes[0].Title);
        }
    }

    private static LlmCallComponentSaveRequest CreateScenarioComponentRequest(WorkflowScenario scenario)
    {
        return new LlmCallComponentSaveRequest(
            Id: null,
            Name: $"Scenario Harness LLM: {scenario.Id} {scenario.Title}",
            ProviderProfileId: null,
            Model: "scenario-grounded-test",
            Modality: WorkflowModality.Text,
            ModelSettings: new WorkflowModelSettings(
                Temperature: 0.1,
                MaxOutputTokens: 1600,
                RequireJsonOutput: true,
                ResponseFormatJsonSchema:
                """
                {
                  "type": "object",
                  "additionalProperties": true,
                  "properties": {
                    "route": { "type": "string" },
                    "summary": { "type": "string" },
                    "markdown": { "type": "string" },
                    "actions": { "type": "array", "items": { "type": "string" } },
                    "targets": { "type": "array", "items": { "type": "string" } },
                    "risk": { "type": "string" },
                    "relevant": { "type": "boolean" },
                    "needsReview": { "type": "boolean" },
                    "requiresResponse": { "type": "boolean" },
                    "ready": { "type": "boolean" },
                    "projectId": { "type": "string" },
                    "nodeId": { "type": "string" },
                    "sourceUrl": { "type": "string" },
                    "project": { "type": "object", "additionalProperties": true },
                    "runContext": { "type": "object", "additionalProperties": true }
                  },
                  "required": ["route", "summary", "markdown", "actions", "targets", "risk", "relevant", "needsReview", "requiresResponse", "ready", "projectId", "nodeId", "sourceUrl"]
                }
                """),
            Instructions:
            $"""
            Produce a grounded project-structure workflow result for scenario {scenario.Id}: {scenario.Title}.
            Use only the loaded sourceDocuments/documents supplied by the previous source-ingestion node.
            The markdown must include source paths, verified facts, gaps, and next actions.
            Scenario instructions: {scenario.Instructions}
            Expected grounded phrases: {string.Join(", ", scenario.ExpectedPhrases)}
            """,
            InputShape: CreateJsonShape(),
            ResultShape: CreateJsonShape(),
            Permissions: AgentPermissionsPolicy.Default);
    }

    private static WorkflowDefinitionSaveRequest CreateScenarioWorkflowDefinitionSaveRequest(
        WorkflowScenario scenario,
        WorkflowComponentId componentId)
    {
        var sourceSettingsJson = JsonSerializer.Serialize(
            new WorkflowSourceIngestionExecutorSettings
            {
                IncludeAdditionalSources = true,
                IncludeParentNodePath = true,
                IncludeSelectedNodePaths = true,
                IncludeParentSubtreePaths = true,
                RecursiveFolders = true,
                AllowAbsoluteInputPaths = true,
                MaxFiles = 18,
                MaxCharactersPerFile = 16000,
                MaxTotalCharacters = 120000
            },
            JsonOptions);
        var assetSettingsJson = JsonSerializer.Serialize(
            new WorkflowProjectStructureExecutorSettings
            {
                Operation = WorkflowProjectStructureOperation.CreateAsset,
                ProjectIdJsonPath = "$.projectId",
                NodeIdJsonPath = "$.nodeId",
                AssetKind = "md",
                Title = $"{scenario.Id} result summary",
                ContentFromInput = true,
                ContentType = "text/markdown"
            },
            JsonOptions);
        var nodes = new List<WorkflowNode>
        {
            CreateWorkflowNode("start", WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
            CreateExecutorWorkflowNode("ingest-sources", WorkflowExecutorIds.SourceIngestion, sourceSettingsJson),
            CreateLlmWorkflowNode("summarize-sources", componentId),
            CreateExecutorWorkflowNode("create-summary-asset", WorkflowExecutorIds.ProjectStructure, assetSettingsJson, CreateJsonShape())
        };
        var edges = new List<WorkflowEdge>
        {
            CreateWorkflowEdge("start-to-ingest", "start", "ingest-sources"),
            CreateWorkflowEdge("ingest-to-llm", "ingest-sources", "summarize-sources"),
            CreateWorkflowEdge("llm-to-summary", "summarize-sources", "create-summary-asset")
        };

        if (!string.IsNullOrWhiteSpace(scenario.FileOutputPath))
        {
            var fileSettingsJson = JsonSerializer.Serialize(
                new WorkflowStorageFileExecutorSettings
                {
                    Operation = WorkflowStorageFileOperation.WriteText,
                    Path = scenario.FileOutputPath,
                    ContentFromInput = true,
                    Overwrite = true
                },
                JsonOptions);
            nodes.Add(CreateExecutorWorkflowNode("write-result-file", WorkflowExecutorIds.StorageFile, fileSettingsJson, CreateJsonShape()));
            edges.Add(CreateWorkflowEdge("llm-to-file", "summarize-sources", "write-result-file"));
            edges.Add(CreateWorkflowEdge("summary-to-end", "create-summary-asset", "end"));
            edges.Add(CreateWorkflowEdge("file-to-end", "write-result-file", "end"));
        }
        else
        {
            edges.Add(CreateWorkflowEdge("summary-to-end", "create-summary-asset", "end"));
        }

        nodes.Add(CreateWorkflowNode("end", WorkflowNodeKind.End, inputShape: CreateJsonShape()));
        return new WorkflowDefinitionSaveRequest(
            Id: null,
            ExpectedVersionId: null,
            Name: $"Scenario Harness: {scenario.Id} {scenario.Title}",
            Description: scenario.Instructions,
            Status: WorkflowLifecycleStatus.Active,
            Graph: new WorkflowGraph(
                new WorkflowNodeId("start"),
                nodes,
                edges),
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
                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default with
                {
                    CaptureOutputArtifact = true,
                    TimeoutSeconds = 45
                }
            });
    }

    private static WorkflowNode CreateLlmWorkflowNode(
        string id,
        WorkflowComponentId componentId)
    {
        return new WorkflowNode(
            new WorkflowNodeId(id),
            WorkflowNodeKind.LlmCall,
            id,
            [],
            new WorkflowNodeSettings(
                ComponentId: componentId,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: "Produce a grounded markdown result from loaded workflow sources.",
                InputShape: CreateJsonShape(),
                ResultShape: CreateJsonShape()));
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

    private static WorkflowValueShape CreateJsonShape()
        => new(WorkflowValueShapeKind.Json, "{}", "JSON payload");

    private static IReadOnlyList<WorkflowScenario> BuildScenarios(string syntheticRoot)
    {
        var mouserFolder = Path.Combine(syntheticRoot, "mouser-order");
        var mouserCart = Path.Combine(mouserFolder, "cart-extract.md");
        var mouserReceipt = Path.Combine(mouserFolder, "receipt-extract.md");
        var seamarkFolder = Path.Combine(syntheticRoot, "seamark");
        var seamarkCatalogue = Path.Combine(seamarkFolder, "catalogue-extract.md");
        var seamarkQuotation = Path.Combine(seamarkFolder, "quotation-extract.md");
        var seamarkX5600 = Path.Combine(seamarkFolder, "x-5600-specification.md");
        var seamarkX6600 = Path.Combine(seamarkFolder, "x-6600-specification.md");
        var seamarkX6600A = Path.Combine(seamarkFolder, "x-6600a-specification.md");
        var iotFactoryWorkbook = Path.Combine(syntheticRoot, "iotfactory-financial-workbook-extract.md");
        var businessPlan = Path.Combine(syntheticRoot, "business-plan.md");
        var customerEmail = Path.Combine(syntheticRoot, "customer-email.eml");
        var vendorRenewal = Path.Combine(syntheticRoot, "vendor-renewal.md");
        var supportTicket = Path.Combine(syntheticRoot, "support-ticket.md");
        var meetingNotes = Path.Combine(syntheticRoot, "meeting-notes.md");
        var releaseReadiness = Path.Combine(syntheticRoot, "release-readiness.md");
        var vendorRisk = Path.Combine(syntheticRoot, "vendor-risk.md");
        var salesLeads = Path.Combine(syntheticRoot, "sales-leads.md");
        var incident = Path.Combine(syntheticRoot, "incident-response.md");
        var folderIntake = Path.Combine(syntheticRoot, "folder-intake");
        var fileSaveInput = Path.Combine(syntheticRoot, "file-save-input.md");
        var projectSubtree = Path.Combine(syntheticRoot, "project-subtree.md");
        var promptSession = Path.Combine(syntheticRoot, "prompt-session-cleanup.md");
        var complianceMemo = Path.Combine(syntheticRoot, "compliance-checklist.md");
        var managedAssetOnly = Path.Combine(syntheticRoot, "managed-asset-only.md");

        return
        [
            CreateScenario(
                "S01",
                "Mouser order XLS/PDF reconciliation",
                "Mouser order package",
                "Compare the uploaded Mouser cart workbook and receipt PDF for purchasing validation.",
                "Reconcile line items, quantities, and totals across the Mouser XLS and receipt PDF.",
                [FolderSource("mouser-folder", "Mouser order folder", mouserFolder), FileSource("mouser-cart", "Mouser cart workbook", mouserCart), FileSource("mouser-receipt", "Mouser receipt PDF", mouserReceipt)],
                [FileChild("Mouser cart workbook", mouserCart), FileChild("Mouser receipt PDF", mouserReceipt)],
                true,
                true,
                ["89566550", "485-4754", "378.16", "FEDEX"]),
            CreateScenario(
                "S02",
                "Mouser order purchasing summary",
                "Mouser purchasing packet",
                "Summarize order purpose, notable components, totals, and follow-up questions.",
                "Create a purchasing brief from both Mouser order files.",
                [FolderSource("mouser-folder", "Mouser order folder", mouserFolder), FileSource("mouser-cart", "Mouser cart workbook", mouserCart), FileSource("mouser-receipt", "Mouser receipt PDF", mouserReceipt)],
                [FileChild("Mouser cart workbook", mouserCart)],
                true,
                false,
                ["89566550", "Adafruit", "MEAN WELL"]),
            CreateScenario(
                "S03",
                "SEAMARK folder x-ray device summary",
                "SEAMARK source folder",
                "Summarize the SEAMARK x-ray inspection PDFs and identify device families.",
                "Read the SEAMARK folder as workflow input and create a device/use-case summary.",
                [FolderSource("seamark-folder", "SEAMARK source folder", seamarkFolder), FileSource("seamark-catalogue", "SEAMARK catalogue", seamarkCatalogue), FileSource("seamark-quote", "SEAMARK price list", seamarkQuotation)],
                [FileChild("SEAMARK catalogue", seamarkCatalogue), FileChild("SEAMARK quotation list", seamarkQuotation)],
                true,
                true,
                ["ZM-x5600", "$35,000", "X-6600A", "90kV"]),
            CreateScenario(
                "S04",
                "SEAMARK price list extraction",
                "SEAMARK quotation review",
                "Extract pricing evidence and mark uncertainty from the quotation PDF.",
                "Focus on price list highlights and uncertainty.",
                [FileSource("seamark-quote", "SEAMARK price list", seamarkQuotation)],
                [FileChild("SEAMARK quotation list", seamarkQuotation)],
                true,
                true,
                ["Quotation Date: 2018.10.15", "USD73000-78000", "$41,500.00"]),
            CreateScenario(
                "S05",
                "SEAMARK model comparison",
                "SEAMARK model comparison",
                "Compare X-5600, X-6600, and X-6600A device specifications.",
                "Create a model comparison and recommendation criteria.",
                [FileSource("x5600", "X-5600 specification", seamarkX5600), FileSource("x6600", "X-6600 specification", seamarkX6600), FileSource("x6600a", "X-6600A specification", seamarkX6600A)],
                [FileChild("X-5600 specification", seamarkX5600), FileChild("X-6600 specification", seamarkX6600), FileChild("X-6600A specification", seamarkX6600A)],
                true,
                true,
                ["X-5600", "X-6600", "640mm*540mm", "1176*1104"]),
            CreateScenario(
                "S06",
                "IoTFactory financial plan review",
                "IoTFactory financial plan",
                "Review workbook assumptions, cost risks, and validation questions.",
                "Summarize the financial workbook and produce actionable finance follow-up.",
                [FileSource("iotfactory-workbook", "IoTFactory financial workbook", iotFactoryWorkbook)],
                [FileChild("IoTFactory financial workbook", iotFactoryWorkbook)],
                true,
                true,
                ["Summary Rozpočet", "Příjmy z modulů", "Výdaje"]),
            CreateScenario(
                "S07",
                "Business plan markdown review",
                "Subscription analytics business plan",
                "Review a lightweight business plan for strengths, risks, and investor questions.",
                "Produce investor-style strengths, risks, and actions.",
                [FileSource("business-plan", "Business plan markdown", businessPlan)],
                [NoteChild("Market assumptions", "Needs pricing validation against three competitors.")],
                true,
                false,
                ["regional distributors", "gross margin", "competitors"]),
            CreateScenario(
                "S08",
                "Customer email task extraction",
                "Customer renewal email",
                "Extract operational tasks from a realistic customer email.",
                "Create task/action result output from the customer message.",
                [FileSource("customer-email", "Customer email", customerEmail)],
                [FileChild("Customer email", customerEmail)],
                true,
                true,
                ["INV-1042", "finance", "account management"]),
            CreateScenario(
                "S09",
                "Vendor renewal risk",
                "Vendor renewal note",
                "Decide whether the vendor renewal needs review and why.",
                "Summarize renewal risk and next steps.",
                [FileSource("vendor-renewal", "Vendor renewal note", vendorRenewal)],
                [],
                false,
                false,
                ["two-year renewal", "14 percent price increase", "Finance approval"]),
            CreateScenario(
                "S10",
                "Support SLA escalation",
                "Support escalation ticket",
                "Route a support ticket with a breached SLA and identify follow-up.",
                "Produce escalation summary and owner actions.",
                [FileSource("support-ticket", "Support ticket", supportTicket)],
                [NoteChild("SLA note", "The customer has a P1 billing-impact ticket with a missed response window.")],
                true,
                true,
                ["P1 support ticket", "Billing outage", "missed by 42 minutes"]),
            CreateScenario(
                "S11",
                "Meeting notes action extraction",
                "Launch meeting notes",
                "Extract blocked, owner-needed, and informational actions from meeting notes.",
                "Create a compact action list from the notes.",
                [FileSource("meeting-notes", "Meeting notes", meetingNotes)],
                [NoteChild("Inventory action", "Supplier ETA is blocking shipment reservation.")],
                true,
                false,
                ["inventory check", "supplier ETA", "owner confirmation"]),
            CreateScenario(
                "S12",
                "Release readiness gate",
                "Release readiness packet",
                "Decide whether the release should proceed or be held.",
                "Produce ready/hold decision evidence.",
                [FileSource("release-readiness", "Release readiness memo", releaseReadiness)],
                [NoteChild("QA status", "Regression passed, but security sign-off is still pending.")],
                true,
                true,
                ["Release candidate 2026.05", "regression", "Security sign-off"]),
            CreateScenario(
                "S13",
                "Vendor risk routing",
                "Vendor onboarding risk memo",
                "Route vendor risks to security, legal, or finance follow-up.",
                "Produce selected risk lane and evidence.",
                [FileSource("vendor-risk", "Vendor risk memo", vendorRisk)],
                [],
                false,
                false,
                ["analytics vendor", "SSO", "Security questionnaire"]),
            CreateScenario(
                "S14",
                "Sales lead qualification",
                "Sales lead packet",
                "Classify sales leads into enterprise handoff, nurture, or disqualify.",
                "Summarize lead qualification evidence.",
                [FileSource("sales-leads", "Sales lead notes", salesLeads)],
                [NoteChild("ACME lead", "Score 91, enterprise segment, security review requested.")],
                true,
                true,
                ["ACME score 91", "Globex score 74", "Initech score 32"]),
            CreateScenario(
                "S15",
                "Incident response fan-out",
                "Incident response packet",
                "Plan communications, engineering, security, and leadership follow-up.",
                "Create a response summary for a customer-impacting incident.",
                [FileSource("incident", "Incident response brief", incident)],
                [NoteChild("Security note", "No credential exposure confirmed, but logs need retention.")],
                true,
                false,
                ["incident", "engineering", "leadership"]),
            CreateScenario(
                "S16",
                "Folder intake summary",
                "Synthetic folder intake",
                "Summarize a folder and list relevant file paths.",
                "Produce a folder intake summary grounded in file names.",
                [FolderSource("folder-intake", "Synthetic intake folder", folderIntake)],
                [FileChild("Folder intake scope", Path.Combine(folderIntake, "scope.md"))],
                true,
                true,
                ["folder intake", "scope.md", "risks.csv"]),
            CreateScenario(
                "S17",
                "File-save workflow",
                "File save request",
                "Write a workflow summary file and record the saved path in execution summary.",
                "Persist a non-asset file operation and also keep a project-structure summary.",
                [FileSource("file-save-input", "File save input", fileSaveInput)],
                [],
                false,
                false,
                ["workflow result", "output path", "execution summary"],
                "samples/workflows/scenario-harness/S17-file-save-result.md"),
            CreateScenario(
                "S18",
                "Project subtree summary",
                "Project subtree root",
                "Include parent and child nodes in the workflow input.",
                "Summarize a project subtree with child-node evidence.",
                [FileSource("subtree-source", "Project subtree source", projectSubtree)],
                [NoteChild("Design task", "Design depends on API payload shape."), NoteChild("Validation task", "Validation needs browser and API proof.")],
                true,
                true,
                ["project subtree", "Design task", "Validation task"]),
            CreateScenario(
                "S19",
                "Prompt session cleanup plan",
                "Prompt cleanup session",
                "Generate cleanup actions from prompt/session notes.",
                "Create a cleanup plan with concrete prompt and session actions.",
                [FileSource("prompt-session", "Prompt session notes", promptSession)],
                [NoteChild("Prompt issue", "Two duplicate cleanup prompts conflict with each other.")],
                true,
                false,
                ["prompt", "session", "cleanup"]),
            CreateScenario(
                "S20",
                "Compliance checklist extraction",
                "Compliance checklist memo",
                "Extract compliance checklist items and risks.",
                "Create a compliance checklist summary and risk list.",
                [FileSource("compliance-memo", "Compliance memo", complianceMemo)],
                [NoteChild("Evidence gap", "Retention evidence for exports is incomplete.")],
                true,
                true,
                ["compliance", "checklist", "retention"]),
            CreateScenario(
                "S21",
                "Managed asset child ingestion",
                "Managed asset source",
                "Use only the attached managed asset as grounded workflow input.",
                "Prove that a selected managed asset child is persisted and loaded into workflow source documents.",
                [],
                [FileChild("Managed asset evidence", managedAssetOnly, verifyPersistedContent: true)],
                true,
                true,
                ["cedar-orbit-421", "asset-review-board"])
        ];
    }

    private static WorkflowScenario CreateScenario(
        string id,
        string title,
        string parentTitle,
        string parentNotes,
        string instructions,
        IReadOnlyList<WorkflowInputSourceSpec> sources,
        IReadOnlyList<ScenarioChildNode> childNodes,
        bool includeParentSubtree,
        bool includeSelectedNodes,
        IReadOnlyList<string> expectedPhrases,
        string? fileOutputPath = null)
    {
        var sourceLines = sources.Count == 0
            ? "- Manual project-structure context only."
            : string.Join(Environment.NewLine, sources.Select(source => $"- {source.Label}: {source.Value}"));
        var phraseLines = string.Join(Environment.NewLine, expectedPhrases.Select(phrase => $"- {phrase}"));
        return new WorkflowScenario(
            id,
            title,
            parentTitle,
            parentNotes,
            instructions,
            sources,
            childNodes,
            includeParentSubtree,
            includeSelectedNodes,
            $"""
             # {id} {title}

             Workflow result generated by the project-structure scenario harness.

             ## Instructions
             {instructions}

             ## Sources
             {sourceLines}

             ## Expected grounded checks
             {phraseLines}
             """,
            expectedPhrases,
            fileOutputPath);
    }

    private static WorkflowInputSourceSpec FileSource(string key, string label, string value)
        => new(ProjectStructureWorkflowInputSourceKind.FilePath, key, label, value);

    private static WorkflowInputSourceSpec FolderSource(string key, string label, string value)
        => new(ProjectStructureWorkflowInputSourceKind.FolderPath, key, label, value);

    private static ScenarioChildNode FileChild(
        string title,
        string sourcePath,
        bool verifyPersistedContent = false)
        => new(
            ProjectObjectType.File,
            title,
            "Source file",
            sourcePath,
            "source-file",
            new ScenarioManagedAssetSource(sourcePath, verifyPersistedContent));

    private static ScenarioChildNode NoteChild(string title, string notes)
        => new(ProjectObjectType.Note, title, "Scenario note", notes, "scenario-note");

    private static void ValidateScenarioSources(WorkflowScenario scenario)
    {
        foreach (var source in scenario.Sources)
        {
            if (source.Kind == ProjectStructureWorkflowInputSourceKind.FilePath)
            {
                Assert.True(File.Exists(source.Value), $"Scenario {scenario.Id} expects file '{source.Value}'.");
            }

            if (source.Kind == ProjectStructureWorkflowInputSourceKind.FolderPath)
            {
                Assert.True(Directory.Exists(source.Value), $"Scenario {scenario.Id} expects folder '{source.Value}'.");
            }
        }
    }

    private static async Task PrepareSyntheticInputsAsync(string syntheticRoot)
    {
        Directory.CreateDirectory(syntheticRoot);
        await WriteTextAsync(
            Path.Combine(syntheticRoot, "mouser-order", "cart-extract.md"),
            "Mouser cart 89566550 includes Adafruit 485-4754 and MEAN WELL components. Order total: 378.16.");
        await WriteTextAsync(
            Path.Combine(syntheticRoot, "mouser-order", "receipt-extract.md"),
            "Receipt 89566550 confirms FEDEX delivery for the Mouser order.");
        await WriteTextAsync(
            Path.Combine(syntheticRoot, "seamark", "catalogue-extract.md"),
            "SEAMARK catalogue lists ZM-x5600, X-6600A, 90kV, and a $35,000 reference price.");
        await WriteTextAsync(
            Path.Combine(syntheticRoot, "seamark", "quotation-extract.md"),
            "Quotation Date: 2018.10.15. Listed ranges include USD73000-78000 and $41,500.00.");
        await WriteTextAsync(
            Path.Combine(syntheticRoot, "seamark", "x-5600-specification.md"),
            "X-5600 inspection area: 640mm*540mm.");
        await WriteTextAsync(
            Path.Combine(syntheticRoot, "seamark", "x-6600-specification.md"),
            "X-6600 detector resolution: 1176*1104.");
        await WriteTextAsync(
            Path.Combine(syntheticRoot, "seamark", "x-6600a-specification.md"),
            "X-6600A is the enhanced model in the comparison set.");
        await WriteTextAsync(
            Path.Combine(syntheticRoot, "iotfactory-financial-workbook-extract.md"),
            "Summary Rozpočet includes Příjmy z modulů and Výdaje assumptions.");
        await WriteTextAsync(
            Path.Combine(syntheticRoot, "business-plan.md"),
            """
            # Subscription Analytics Business Plan

            Target customers are regional distributors. The first year plan assumes 18 enterprise contracts, 78 percent gross margin, and direct sales hiring in Q3.
            Key risks are slow onboarding, high support load, and unvalidated pricing against three competitors.
            """);
        await WriteTextAsync(
            Path.Combine(syntheticRoot, "customer-email.eml"),
            """
            From: operations@example.test
            Subject: Renewal blocked by invoice mismatch

            Please create tasks for finance and account management. The customer cannot approve the renewal until invoice INV-1042 matches the contract.
            """);
        await WriteTextAsync(
            Path.Combine(syntheticRoot, "vendor-renewal.md"),
            """
            Vendor asks for a two-year renewal, 14 percent price increase, and a support addendum. Finance approval is missing.
            """);
        await WriteTextAsync(
            Path.Combine(syntheticRoot, "support-ticket.md"),
            """
            P1 support ticket. Billing outage affects three enterprise customers. First response SLA was missed by 42 minutes.
            """);
        await WriteTextAsync(
            Path.Combine(syntheticRoot, "meeting-notes.md"),
            """
            Launch meeting: inventory check is blocked by supplier ETA. Shipment reservation needs owner confirmation. Payment validation passed.
            """);
        await WriteTextAsync(
            Path.Combine(syntheticRoot, "release-readiness.md"),
            """
            Release candidate 2026.05 passed regression and smoke tests. Security sign-off remains pending and rollback owner is assigned.
            """);
        await WriteTextAsync(
            Path.Combine(syntheticRoot, "vendor-risk.md"),
            """
            New analytics vendor requests SSO, exports customer data, and requires prepaid annual billing. Security questionnaire is incomplete.
            """);
        await WriteTextAsync(
            Path.Combine(syntheticRoot, "sales-leads.md"),
            """
            ACME score 91 enterprise with security review. Globex score 74 mid-market with pricing concern. Initech score 32 no active budget.
            """);
        await WriteTextAsync(
            Path.Combine(syntheticRoot, "incident-response.md"),
            """
            Incident affected checkout for 22 minutes. Engineering identified a cache invalidation bug. Customer communications and leadership update are required.
            """);
        await WriteTextAsync(
            Path.Combine(syntheticRoot, "file-save-input.md"),
            """
            Save a markdown decision record for the workflow result. The output path must be captured in the workflow execution summary.
            """);
        await WriteTextAsync(
            Path.Combine(syntheticRoot, "project-subtree.md"),
            """
            Parent node includes two child tasks: design payload shape and validate browser/API proof. Summarize the subtree dependencies.
            """);
        await WriteTextAsync(
            Path.Combine(syntheticRoot, "prompt-session-cleanup.md"),
            """
            Two prompt drafts duplicate cleanup instructions. Session notes should keep only the project-specific workflow proof plan.
            """);
        await WriteTextAsync(
            Path.Combine(syntheticRoot, "compliance-checklist.md"),
            """
            Compliance memo requires export retention evidence, reviewer sign-off, and deletion policy confirmation before launch.
            """);
        await WriteTextAsync(
            Path.Combine(syntheticRoot, "managed-asset-only.md"),
            """
            Managed asset verification record.
            Evidence token: cedar-orbit-421.
            Approval owner: asset-review-board.
            """);

        var folderIntakeRoot = Path.Combine(syntheticRoot, "folder-intake");
        Directory.CreateDirectory(folderIntakeRoot);
        await WriteTextAsync(Path.Combine(folderIntakeRoot, "scope.md"), "Folder intake scope for onboarding documents.");
        await WriteTextAsync(Path.Combine(folderIntakeRoot, "risks.csv"), "risk,owner\nmissing owner,operations\nretention gap,compliance\n");
    }

    private static Task WriteTextAsync(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return File.WriteAllTextAsync(path, content);
    }

    private static async Task<T> PostAndReadAsync<T>(HttpClient client, string path, object request)
    {
        var response = await client.PostAsJsonAsync(path, request);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode}). Body: {errorBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<T>(
            ProjectStructureHttpContractTestJson.SerializerOptions);
        return payload ?? throw new InvalidOperationException($"No payload was returned for '{path}'.");
    }

    private static async Task<T> GetAndReadAsync<T>(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode}). Body: {errorBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<T>(
            ProjectStructureHttpContractTestJson.SerializerOptions);
        return payload ?? throw new InvalidOperationException($"No payload was returned for '{path}'.");
    }

    private static string BuildDatabaseConnectionString(string connectionString, string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = databaseName,
            IncludeErrorDetail = true,
            Timeout = 5,
            CommandTimeout = 15
        };

        return builder.ConnectionString;
    }

    private static async Task CreateDatabaseAsync(string connectionString, string databaseName)
    {
        await using var connection = new NpgsqlConnection(BuildAdminConnectionString(connectionString));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"create database \"{databaseName}\";";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(string connectionString, string databaseName)
    {
        await using var connection = new NpgsqlConnection(BuildAdminConnectionString(connectionString));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"drop database if exists \"{databaseName}\" with (force);";
        await command.ExecuteNonQueryAsync();
    }

    private static string BuildAdminConnectionString(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Database))
        {
            builder.Database = "postgres";
        }

        builder.IncludeErrorDetail = true;
        builder.Timeout = 5;
        builder.CommandTimeout = 15;
        return builder.ConnectionString;
    }

    private sealed class GroundedScenarioWorkflowLlmInvoker : IWorkflowLlmComponentInvoker
    {
        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowDefinition definition,
            WorkflowNode node,
            LlmCallComponent component,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var document = JsonDocument.Parse(input.PayloadJson);
            var root = document.RootElement;
            var manualInput = root.TryGetProperty("manualInput", out var manualInputElement)
                ? manualInputElement
                : default;
            var scenarioId = ReadString(manualInput, "scenarioId");
            var title = ReadString(manualInput, "title");
            var instructions = ReadString(manualInput, "instructions");
            var expectedPhrases = ReadStringArray(manualInput, "expectedPhrases");
            var sourceDocuments = ReadSourceDocuments(root);
            if (sourceDocuments.Count == 0)
            {
                throw new InvalidOperationException($"Scenario '{scenarioId}' did not load any source documents before the LLM node.");
            }

            var projectContext = BuildProjectContext(root);
            var evidence = new List<GroundedScenarioEvidence>();
            foreach (var phrase in expectedPhrases)
            {
                var item = FindEvidence(phrase, sourceDocuments, projectContext);
                if (item is null)
                {
                    throw new InvalidOperationException(
                        $"Scenario '{scenarioId}' expected grounded phrase '{phrase}', but it was not found in loaded source documents or project-structure context.");
                }

                evidence.Add(item);
            }

            var markdown = BuildMarkdown(
                scenarioId,
                title,
                instructions,
                root,
                sourceDocuments,
                evidence);
            var payload = new
            {
                route = "summary",
                summary = $"Grounded result for {scenarioId}: {title}. Loaded {sourceDocuments.Count} source document(s).",
                markdown,
                actions = new[]
                {
                    "Review cited source paths.",
                    "Confirm stale prices, dates, and operational decisions with the source owner before committing changes."
                },
                targets = sourceDocuments.Select(item => item.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                risk = evidence.Count == expectedPhrases.Count ? "medium" : "high",
                relevant = true,
                needsReview = sourceDocuments.Any(item => string.Equals(item.ExtractionStatus, "pdf-no-extractable-text", StringComparison.OrdinalIgnoreCase)),
                requiresResponse = false,
                ready = true,
                projectId = ReadNestedString(root, "project", "id"),
                nodeId = ReadNestedString(root, "runContext", "workflowNodeId"),
                sourceUrl = string.Empty,
                project = CloneProperty(root, "project"),
                runContext = CloneProperty(root, "runContext"),
                loadedSourceCount = sourceDocuments.Count,
                evidence = evidence.Select(item => new
                {
                    item.Phrase,
                    item.SourceName,
                    item.SourcePath,
                    item.Snippet
                }).ToArray()
            };

            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
                node.Id,
                JsonSerializer.Serialize(payload, JsonOptions),
                component.ResultShape));
        }

        private static string BuildMarkdown(
            string scenarioId,
            string title,
            string instructions,
            JsonElement root,
            IReadOnlyList<GroundedScenarioDocument> sourceDocuments,
            IReadOnlyList<GroundedScenarioEvidence> evidence)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"# {scenarioId} {title}");
            builder.AppendLine();
            builder.AppendLine("## Result");
            builder.AppendLine($"Loaded {sourceDocuments.Count} source document(s) through the workflow source-ingestion node.");
            if (!string.IsNullOrWhiteSpace(instructions))
            {
                builder.AppendLine($"Instruction focus: {instructions}");
            }

            builder.AppendLine();
            builder.AppendLine("## Sources");
            builder.AppendLine("| Source | Path | Status | Characters |");
            builder.AppendLine("| --- | --- | --- | ---: |");
            foreach (var source in sourceDocuments)
            {
                builder.AppendLine($"| {EscapeMarkdown(source.FileName)} | {EscapeMarkdown(source.Path)} | {EscapeMarkdown(source.ExtractionStatus)} | {source.TotalCharacters} |");
            }

            builder.AppendLine();
            builder.AppendLine("## Verified Facts");
            foreach (var item in evidence)
            {
                builder.AppendLine($"- **{EscapeMarkdown(item.Phrase)}** - {EscapeMarkdown(item.SourceName)}: {EscapeMarkdown(item.Snippet)}");
            }

            var noTextSources = sourceDocuments
                .Where(item => string.Equals(item.ExtractionStatus, "pdf-no-extractable-text", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (noTextSources.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine("## Gaps");
                foreach (var source in noTextSources)
                {
                    builder.AppendLine($"- {EscapeMarkdown(source.FileName)} had no extractable PDF text and needs OCR/manual review.");
                }
            }

            builder.AppendLine();
            builder.AppendLine("## Project Context");
            builder.AppendLine($"- Project: {EscapeMarkdown(ReadNestedString(root, "project", "name"))}");
            builder.AppendLine($"- Workflow node: {EscapeMarkdown(ReadNestedString(root, "runContext", "workflowNodeId"))}");
            builder.AppendLine($"- Parent node: {EscapeMarkdown(ReadNestedString(root, "parentNode", "title"))}");
            builder.AppendLine();
            builder.AppendLine("## Next Actions");
            builder.AppendLine("- Keep this summary attached under the workflow node for traceability.");
            builder.AppendLine("- Re-run the workflow if source files or pricing data change.");
            return builder.ToString().Trim();
        }

        private static IReadOnlyList<GroundedScenarioDocument> ReadSourceDocuments(JsonElement root)
        {
            var documents = new List<GroundedScenarioDocument>();
            var sourceArray = root.TryGetProperty("sourceDocuments", out var sourceDocuments)
                ? sourceDocuments
                : root.TryGetProperty("documents", out var documentsElement)
                    ? documentsElement
                    : default;
            if (sourceArray.ValueKind != JsonValueKind.Array)
            {
                return documents;
            }

            foreach (var item in sourceArray.EnumerateArray())
            {
                var text = ReadString(item, "text");
                var path = ReadString(item, "path");
                var fileName = ReadString(item, "fileName");
                if (string.IsNullOrWhiteSpace(text) &&
                    string.IsNullOrWhiteSpace(path) &&
                    string.IsNullOrWhiteSpace(fileName))
                {
                    continue;
                }

                documents.Add(new GroundedScenarioDocument(
                    fileName,
                    path,
                    ReadString(item, "label"),
                    text,
                    ReadString(item, "extractionStatus"),
                    ReadInt32(item, "totalCharacters")));
            }

            return documents;
        }

        private static string BuildProjectContext(JsonElement root)
        {
            var builder = new StringBuilder();
            AppendRawProperty(builder, root, "project");
            AppendRawProperty(builder, root, "runContext");
            AppendRawProperty(builder, root, "parentNode");
            AppendRawProperty(builder, root, "selectedNodes");
            AppendRawProperty(builder, root, "parentSubtree");
            AppendRawProperty(builder, root, "sources");
            return builder.ToString();
        }

        private static GroundedScenarioEvidence? FindEvidence(
            string phrase,
            IReadOnlyList<GroundedScenarioDocument> sourceDocuments,
            string projectContext)
        {
            foreach (var source in sourceDocuments)
            {
                var sourceText = string.Join(
                    Environment.NewLine,
                    source.FileName,
                    source.Path,
                    source.Label,
                    source.ExtractionStatus,
                    source.Text);
                var snippet = FindSnippet(sourceText, phrase);
                if (!string.IsNullOrWhiteSpace(snippet))
                {
                    return new GroundedScenarioEvidence(
                        phrase,
                        string.IsNullOrWhiteSpace(source.FileName) ? source.Label : source.FileName,
                        source.Path,
                        snippet);
                }
            }

            var contextSnippet = FindSnippet(projectContext, phrase);
            return string.IsNullOrWhiteSpace(contextSnippet)
                ? null
                : new GroundedScenarioEvidence(phrase, "project-structure context", "project structure input", contextSnippet);
        }

        private static string? FindSnippet(string value, string phrase)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(phrase))
            {
                return null;
            }

            var index = value.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return null;
            }

            var start = Math.Max(0, index - 80);
            var length = Math.Min(value.Length - start, phrase.Length + 160);
            return value.Substring(start, length)
                .Replace("\r", " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal)
                .Trim();
        }

        private static IReadOnlyList<string> ReadStringArray(JsonElement element, string propertyName)
        {
            if (element.ValueKind == JsonValueKind.Undefined ||
                !element.TryGetProperty(propertyName, out var property) ||
                property.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return property.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();
        }

        private static string ReadNestedString(JsonElement root, string objectName, string propertyName)
        {
            if (!root.TryGetProperty(objectName, out var item))
            {
                return string.Empty;
            }

            return ReadString(item, propertyName);
        }

        private static JsonElement? CloneProperty(JsonElement root, string propertyName)
            => root.TryGetProperty(propertyName, out var property)
                ? property.Clone()
                : null;

        private static string ReadString(JsonElement element, string propertyName)
        {
            if (element.ValueKind == JsonValueKind.Undefined ||
                !element.TryGetProperty(propertyName, out var property))
            {
                return string.Empty;
            }

            return property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : property.GetRawText();
        }

        private static int ReadInt32(JsonElement element, string propertyName)
        {
            if (element.ValueKind == JsonValueKind.Undefined ||
                !element.TryGetProperty(propertyName, out var property) ||
                property.ValueKind != JsonValueKind.Number)
            {
                return 0;
            }

            return property.TryGetInt32(out var value) ? value : 0;
        }

        private static void AppendRawProperty(StringBuilder builder, JsonElement root, string propertyName)
        {
            if (root.TryGetProperty(propertyName, out var property))
            {
                builder.AppendLine(property.GetRawText());
            }
        }

        private static string EscapeMarkdown(string value)
            => value
                .Replace("|", "\\|", StringComparison.Ordinal)
                .Replace("\r", " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal);

        private sealed record GroundedScenarioDocument(
            string FileName,
            string Path,
            string Label,
            string Text,
            string ExtractionStatus,
            int TotalCharacters);

        private sealed record GroundedScenarioEvidence(
            string Phrase,
            string SourceName,
            string SourcePath,
            string Snippet);
    }

    private sealed record WorkflowScenario(
        string Id,
        string Title,
        string ParentTitle,
        string ParentNotes,
        string Instructions,
        IReadOnlyList<WorkflowInputSourceSpec> Sources,
        IReadOnlyList<ScenarioChildNode> ChildNodes,
        bool IncludeParentSubtree,
        bool IncludeSelectedNodes,
        string ExpectedSummaryMarkdown,
        IReadOnlyList<string> ExpectedPhrases,
        string? FileOutputPath);

    private sealed record WorkflowInputSourceSpec(
        ProjectStructureWorkflowInputSourceKind Kind,
        string Key,
        string Label,
        string Value);

    private sealed record ScenarioChildNode(
        ProjectObjectType ObjectType,
        string Title,
        string Subtitle,
        string Notes,
        string ObjectSubtype,
        ScenarioManagedAssetSource? ManagedAssetSource = null);

    private sealed record ScenarioManagedAssetSource(
        string Path,
        bool VerifyPersistedContent);

    private sealed record ScenarioManualInput(
        string ScenarioId,
        string Title,
        string Instructions,
        IReadOnlyList<string> ExpectedPhrases);

    private sealed record ScenarioHarnessResult(
        DateTimeOffset CompletedAtUtc,
        string DatabaseProvider,
        string DatabaseProfileKey,
        string ProviderPlan,
        IReadOnlyList<ScenarioRunResult> Scenarios);

    private sealed record HarnessExecutionResult(
        string DatabaseProvider,
        string DatabaseProfileKey,
        string ResultPath,
        IReadOnlyList<ScenarioRunResult> Scenarios);

    private sealed record ScenarioRunResult(
        string Id,
        string Title,
        string WorkflowName,
        string RunId,
        string State,
        int ProgressPercent,
        int CurrentStepIndex,
        int StepCount,
        IReadOnlyList<string> CreatedNodeIds,
        IReadOnlyList<string> CreatedAssetIds,
        IReadOnlyList<string> CreatedFilePaths,
        IReadOnlyList<string> Validations);
}
