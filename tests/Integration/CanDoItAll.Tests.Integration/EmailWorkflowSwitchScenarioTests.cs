using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class EmailWorkflowSwitchScenarioTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    [Fact]
    public async Task Email_workflow_uses_switch_and_creates_project_structure_task_nodes()
        => await RunEmailWorkflowSwitchValidationAsync(
            "email-workflow-switch",
            testEnvironment => testEnvironment.CreatePostgreSqlProfile("email-workflow-switch"),
            "email-switch-task-results.json");

    [Fact]
    public async Task Email_workflow_uses_switch_and_creates_project_structure_task_nodes_on_postgresql()
    {
        var availability = await PostgresTestAvailability.EnsureAvailableAsync(IntegrationTestPaths.RepositoryRoot);
        Assert.True(availability.IsAvailable, availability.Message);
        Assert.False(string.IsNullOrWhiteSpace(availability.ConnectionString));

        var databaseName = $"cditall_email_wf_{Guid.NewGuid():N}"[..30];
        await CreateDatabaseAsync(availability.ConnectionString!, databaseName);

        try
        {
            await RunEmailWorkflowSwitchValidationAsync(
                "email-workflow-switch-postgres",
                testEnvironment => testEnvironment.CreatePostgreSqlProfile(
                    "email-workflow-switch-postgres",
                    BuildDatabaseConnectionString(availability.ConnectionString!, databaseName)),
                "email-switch-task-postgresql-results.json");
        }
        finally
        {
            await DropDatabaseAsync(availability.ConnectionString!, databaseName);
        }
    }

    private static async Task RunEmailWorkflowSwitchValidationAsync(
        string testEnvironmentKey,
        Func<CanDoItAllTestEnvironment, TestDatabaseProfile> profileFactory,
        string resultFileName)
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync(
            testEnvironmentKey,
            profileFactory,
            ConfigureEmailWorkflowServices);
        var proofRoot = Path.Combine(
            host.RootPath,
            "proof",
            "email-workflows");
        var syntheticRoot = Path.Combine(proofRoot, "synthetic-inputs");
        var emailCases = await PrepareEmailCasesAsync(syntheticRoot);

        var component = await PostAndReadAsync<LlmCallComponent>(
            host.Client,
            "/api/workflows/components",
            CreateEmailComponentRequest());
        var definition = await PostAndReadAsync<WorkflowDefinition>(
            host.Client,
            "/api/workflows/definitions",
            CreateEmailWorkflowDefinitionSaveRequest(component.Id));
        var validation = await ValidateDefinitionAsync(host.Client, definition);

        Assert.True(validation.Succeeded, JsonSerializer.Serialize(validation, JsonOptions));
        Assert.Contains(
            definition.Graph.Edges,
            edge => edge.Routing.Kind == WorkflowRouteKind.SwitchCase &&
                    edge.Routing.JsonPath == "$.route" &&
                    edge.Routing.ExpectedValueJson == "\"tasks\"");
        Assert.Contains(
            definition.Graph.Edges,
            edge => edge.Routing.Kind == WorkflowRouteKind.SwitchCase &&
                    edge.Routing.ExpectedValueJson == "\"asap_response\"");
        Assert.Contains(definition.Graph.Edges, edge => edge.Routing.Kind == WorkflowRouteKind.SwitchDefault);

        var project = await PostAndReadAsync<ProjectSummary>(
            host.Client,
            "/api/project-structure/projects",
            new ProjectStructureProjectSaveRequest(
                "Email workflow switch validation",
                "Validates email classification, ASAP routing, and task-node creation.",
                "Project used by the synthesized email workflow API tests.",
                "Validation",
                ProjectStatus.Active));
        var lease = await PostAndReadAsync<ProjectStructureLeaseSnapshot>(
            host.Client,
            "/api/project-structure/leases/acquire",
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.Project,
                project.Id.ToString("D"),
                "Run synthesized email workflow cases",
                30));

        var runResults = new List<EmailCaseRunResult>();
        foreach (var emailCase in emailCases)
        {
            runResults.Add(await RunEmailCaseAsync(host, project, lease.LeaseToken, definition, emailCase));
        }

        Assert.Contains(runResults, result => result.Id == "E02" && result.CreatedTaskTitles.Contains("Prepare Q3 renewal checklist"));
        Assert.Contains(runResults, result => result.Id == "E03" && result.Route == "asap_response" && result.CreatedTaskTitles.Contains("Respond to Contoso outage escalation"));
        Assert.Contains(runResults, result => result.Id == "E05" && result.Route == "no_action" && result.CreatedTaskTitles.Count == 0);
        Assert.Contains(runResults, result => result.Id == "E08" && result.Route == "tasks" && result.CreatedTaskTitles.Contains("Reply with contract status"));

        Directory.CreateDirectory(proofRoot);
        await File.WriteAllTextAsync(
            Path.Combine(proofRoot, resultFileName),
            JsonSerializer.Serialize(
                new EmailWorkflowProof(
                    DateTimeOffset.UtcNow,
                    definition.Id.Value,
                    definition.VersionId.Value,
                    runResults),
                JsonOptions));
    }

    private static void ConfigureEmailWorkflowServices(IServiceCollection services)
    {
        services.RemoveAll<IWorkflowLlmComponentInvoker>();
        services.AddSingleton<IWorkflowLlmComponentInvoker, EmailWorkflowLlmInvoker>();
    }

    private static async Task<EmailCaseRunResult> RunEmailCaseAsync(
        ProjectStructureAgentApiTestHost host,
        ProjectSummary project,
        string leaseToken,
        WorkflowDefinition definition,
        EmailCase emailCase)
    {
        var sourceBytes = await File.ReadAllBytesAsync(emailCase.FilePath);
        var parent = await PostAndReadAsync<ProjectStructureNodeSummary>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/assets",
            new ProjectStructureAssetCreateInput(
                ProjectObjectType.File,
                emailCase.Title,
                emailCase.Subject,
                emailCase.FilePath,
                new ProjectObjectMediaPayload(
                    Path.GetFileName(emailCase.FilePath),
                    "message/rfc822",
                    Convert.ToBase64String(sourceBytes)),
                ParentNodeKey: $"project:{project.Id}",
                ObjectSubtype: "eml",
                LeaseToken: leaseToken));
        var inputSettings = ProjectStructureWorkflowInputSettings.Default();
        inputSettings.IncludeAssets = false;
        inputSettings.ManualInputJson = JsonSerializer.Serialize(
            new
            {
                emailCase.Id,
                emailCase.ExpectedRoute,
                emailCase.ExpectedTaskTitles
            },
            JsonOptions);

        var options = await PostAndReadAsync<ProjectStructureWorkflowAddOptionsResult>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes/{parent.Id}/workflow-add-options",
            new ProjectStructureWorkflowAddOptionsInput(
                definition.Id,
                definition.VersionId,
                inputSettings,
                inputSettings.SelectedNodeIds));

        Assert.Equal(project.Id, options.ProjectId);
        Assert.Equal(parent.Id, options.ParentNode.Id);
        using (var previewDocument = JsonDocument.Parse(options.Preview.InputJson))
        {
            Assert.Equal(emailCase.FilePath, previewDocument.RootElement.GetProperty("parentNode").GetProperty("notes").GetString());
        }

        var workflowNode = await PostAndReadAsync<ProjectStructureWorkflowNodeCreateResult>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes/{parent.Id}/workflow-definition",
            new ProjectStructureWorkflowNodeCreateInput(
                definition.Id,
                definition.VersionId,
                $"{emailCase.Id} Email intake",
                InputSettings: inputSettings,
                LeaseToken: leaseToken));
        var started = await PostAndReadAsync<ProjectStructureWorkflowNodeStartResult>(
            host.Client,
            $"/api/project-structure/projects/{project.Id}/nodes/{workflowNode.Node.Id}/workflow/start",
            new ProjectStructureWorkflowNodeStartInput(WorkflowRuntimeBackendKind.InProcess, LeaseToken: leaseToken));

        Assert.True(
            started.Status.State == WorkflowRunState.Completed,
            JsonSerializer.Serialize(started.Status, JsonOptions));
        Assert.Equal(100, started.Status.ProgressPercent);
        Assert.Equal("complete", started.Status.ProgressMode);

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
        var taskNodes = createdNodes
            .Where(node => node.ObjectType == ProjectObjectType.WorkItem &&
                           string.Equals(node.ObjectSubtype, "task", StringComparison.OrdinalIgnoreCase))
            .OrderBy(node => node.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var assetNodes = createdNodes
            .Where(node => node.ObjectType == ProjectObjectType.File)
            .ToArray();

        Assert.All(createdNodes, node => Assert.Equal(workflowNode.Node.Id, node.ParentId));
        Assert.Contains(readback.Links, link => link.SourceId == workflowNode.Node.Id && createdNodes.Any(node => node.Id == link.TargetId));
        Assert.Equal(emailCase.ExpectedTaskTitles.Count, taskNodes.Length);
        foreach (var expectedTitle in emailCase.ExpectedTaskTitles)
        {
            Assert.Contains(taskNodes, node => string.Equals(node.Title, expectedTitle, StringComparison.OrdinalIgnoreCase));
        }

        if (emailCase.ExpectedTaskTitles.Count == 0)
        {
            var assetNode = Assert.Single(assetNodes);
            Assert.Contains(emailCase.ExpectedEvidencePhrase, assetNode.Notes ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.NotEmpty(status.Summary.CreatedAssetIds);
        }
        else
        {
            Assert.Empty(assetNodes);
            Assert.Contains(taskNodes, node => (node.Notes ?? string.Empty).Contains(emailCase.ExpectedEvidencePhrase, StringComparison.OrdinalIgnoreCase));
            Assert.All(taskNodes, node => Assert.Contains("Source email:", node.Notes ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        }

        return new EmailCaseRunResult(
            emailCase.Id,
            emailCase.Subject,
            emailCase.ExpectedRoute,
            status.State.ToString(),
            status.ProgressPercent,
            taskNodes.Select(node => node.Title).ToArray(),
            assetNodes.Select(node => node.Title).ToArray(),
            status.Summary.CreatedNodeIds,
            status.Summary.CreatedAssetIds);
    }

    private static LlmCallComponentSaveRequest CreateEmailComponentRequest()
        => new(
            Id: null,
            Name: "Email switch task extraction test LLM",
            ProviderProfileId: null,
            Model: "deterministic-email-workflow-test",
            Modality: WorkflowModality.Text,
            ModelSettings: new WorkflowModelSettings(
                Temperature: 0.1,
                MaxOutputTokens: 1800,
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
                    "emailCategory": { "type": "string" },
                    "isInformational": { "type": "boolean" },
                    "asapResponseRequired": { "type": "boolean" },
                    "tasks": { "type": "array" },
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
                  "required": ["route", "summary", "markdown", "tasks", "emailCategory", "isInformational", "asapResponseRequired", "actions", "targets", "risk", "relevant", "needsReview", "requiresResponse", "ready", "projectId", "nodeId", "sourceUrl"]
                }
                """),
            Instructions:
            """
            Classify the loaded email source as tasks, asap_response, informative, or no_action.
            Use tasks only for concrete work assigned to me or the current project team.
            Use asap_response when the email needs an immediate or same-day response.
            Return JSON only and preserve projectId and nodeId for project-structure task creation.
            """,
            InputShape: CreateJsonShape(),
            ResultShape: CreateJsonShape(),
            Permissions: AgentPermissionsPolicy.Default);

    private static WorkflowDefinitionSaveRequest CreateEmailWorkflowDefinitionSaveRequest(WorkflowComponentId componentId)
    {
        var sourceSettingsJson = JsonSerializer.Serialize(
            new WorkflowSourceIngestionExecutorSettings
            {
                IncludeAdditionalSources = true,
                IncludeParentNodePath = true,
                IncludeSelectedNodePaths = false,
                IncludeParentSubtreePaths = false,
                RecursiveFolders = false,
                AllowAbsoluteInputPaths = true,
                MaxFiles = 1,
                MaxCharactersPerFile = 16000,
                MaxTotalCharacters = 16000
            },
            JsonOptions);
        var taskSettingsJson = JsonSerializer.Serialize(
            new WorkflowProjectStructureExecutorSettings
            {
                Operation = WorkflowProjectStructureOperation.CreateTaskNodes,
                ProjectIdJsonPath = "$.projectId",
                NodeIdJsonPath = "$.nodeId",
                TaskItemsJsonPath = "$.tasks",
                TaskObjectSubtype = "task",
                MaxTaskNodes = 12
            },
            JsonOptions);
        var assetSettingsJson = JsonSerializer.Serialize(
            new WorkflowProjectStructureExecutorSettings
            {
                Operation = WorkflowProjectStructureOperation.CreateAsset,
                ProjectIdJsonPath = "$.projectId",
                NodeIdJsonPath = "$.nodeId",
                AssetKind = "md",
                Title = "Email intake summary",
                ContentFromInput = true,
                ContentType = "text/markdown"
            },
            JsonOptions);
        var nodes = new List<WorkflowNode>
        {
            CreateWorkflowNode("start", WorkflowNodeKind.Start),
            CreateExecutorWorkflowNode("ingest-email-sources", WorkflowExecutorIds.SourceIngestion, sourceSettingsJson),
            CreateLlmWorkflowNode("classify-email", componentId),
            CreateWorkflowNode("email-switch", WorkflowNodeKind.Triage),
            CreateExecutorWorkflowNode("create-email-task-nodes", WorkflowExecutorIds.ProjectStructure, taskSettingsJson, CreateJsonShape()),
            CreateExecutorWorkflowNode("create-asap-response-task", WorkflowExecutorIds.ProjectStructure, taskSettingsJson, CreateJsonShape()),
            CreateExecutorWorkflowNode("store-informative-summary", WorkflowExecutorIds.ProjectStructure, assetSettingsJson, CreateJsonShape()),
            CreateExecutorWorkflowNode("store-default-summary", WorkflowExecutorIds.ProjectStructure, assetSettingsJson, CreateJsonShape()),
            CreateWorkflowNode("end", WorkflowNodeKind.End, inputShape: CreateJsonShape())
        };
        var edges = new List<WorkflowEdge>
        {
            CreateWorkflowEdge("start-to-ingest", "start", "ingest-email-sources"),
            CreateWorkflowEdge("ingest-to-classify", "ingest-email-sources", "classify-email"),
            CreateWorkflowEdge("classify-to-switch", "classify-email", "email-switch"),
            CreateWorkflowEdge("switch-to-tasks", "email-switch", "create-email-task-nodes", WorkflowEdgeKind.Conditional, WorkflowEdgeRouting.SwitchCase("$.route", "\"tasks\"", WorkflowRouteValueKind.String, "Tasks")),
            CreateWorkflowEdge("switch-to-asap", "email-switch", "create-asap-response-task", WorkflowEdgeKind.Conditional, WorkflowEdgeRouting.SwitchCase("$.route", "\"asap_response\"", WorkflowRouteValueKind.String, "ASAP response")),
            CreateWorkflowEdge("switch-to-info", "email-switch", "store-informative-summary", WorkflowEdgeKind.Conditional, WorkflowEdgeRouting.SwitchCase("$.route", "\"informative\"", WorkflowRouteValueKind.String, "Informative")),
            CreateWorkflowEdge("switch-to-default", "email-switch", "store-default-summary", WorkflowEdgeKind.Conditional, WorkflowEdgeRouting.SwitchDefault("DEFAULT")),
            CreateWorkflowEdge("tasks-to-end", "create-email-task-nodes", "end"),
            CreateWorkflowEdge("asap-to-end", "create-asap-response-task", "end"),
            CreateWorkflowEdge("info-to-end", "store-informative-summary", "end"),
            CreateWorkflowEdge("default-to-end", "store-default-summary", "end")
        };

        return new WorkflowDefinitionSaveRequest(
            Id: null,
            ExpectedVersionId: null,
            Name: "Email switch task extraction API validation",
            Description: "Uses source ingestion, LLM classification, SWITCH routing, and project-structure task-node creation.",
            Status: WorkflowLifecycleStatus.Active,
            Graph: new WorkflowGraph(new WorkflowNodeId("start"), nodes, edges),
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
        => new(
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
                InputShape: inputShape ?? CreateJsonShape(),
                ResultShape: resultShape ?? CreateJsonShape()));

    private static WorkflowNode CreateExecutorWorkflowNode(
        string id,
        WorkflowExecutorId executorId,
        string executorSettingsJson,
        WorkflowValueShape? inputShape = null)
        => new(
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
                InputShape: inputShape ?? CreateJsonShape(),
                ResultShape: CreateJsonShape())
            {
                ExecutorId = executorId,
                ExecutorSettingsJson = executorSettingsJson,
                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default with
                {
                    CaptureOutputArtifact = true,
                    TimeoutSeconds = 45
                }
            });

    private static WorkflowNode CreateLlmWorkflowNode(string id, WorkflowComponentId componentId)
        => new(
            new WorkflowNodeId(id),
            WorkflowNodeKind.LlmCall,
            id,
            [],
            new WorkflowNodeSettings(
                ComponentId: componentId,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: "Classify the loaded email and return the strict JSON contract.",
                InputShape: CreateJsonShape(),
                ResultShape: CreateJsonShape()));

    private static WorkflowEdge CreateWorkflowEdge(
        string id,
        string source,
        string target,
        WorkflowEdgeKind kind = WorkflowEdgeKind.Direct,
        WorkflowEdgeRouting? routing = null)
        => new(
            new WorkflowEdgeId(id),
            new WorkflowNodeId(source),
            SourcePortId: null,
            new WorkflowNodeId(target),
            TargetPortId: null,
            kind,
            ConditionExpression: string.Empty)
        {
            Routing = routing ?? WorkflowEdgeRouting.Always
        };

    private static WorkflowValueShape CreateJsonShape()
        => new(WorkflowValueShapeKind.Json, "{}", "JSON payload");

    private static async Task<WorkflowValidationResult> ValidateDefinitionAsync(HttpClient client, WorkflowDefinition definition)
    {
        var response = await client.PostAsync($"/api/workflows/definitions/{definition.Id.Value:D}/validate", content: null);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<WorkflowValidationResult>(body, JsonOptions)
               ?? throw new InvalidOperationException("Validation response was empty.");
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

    private static async Task<IReadOnlyList<EmailCase>> PrepareEmailCasesAsync(string syntheticRoot)
    {
        Directory.CreateDirectory(syntheticRoot);
        var cases = BuildEmailCases(syntheticRoot);
        foreach (var emailCase in cases)
        {
            await File.WriteAllTextAsync(emailCase.FilePath, emailCase.Content, Encoding.UTF8);
        }

        return cases;
    }

    private static IReadOnlyList<EmailCase> BuildEmailCases(string syntheticRoot)
        =>
        [
            CreateEmailCase(
                syntheticRoot,
                "E01",
                "FYI maintenance window confirmed",
                "FYI: Maintenance window confirmed",
                "informative",
                "maintenance window",
                [],
                """
                Message-ID: E01-maintenance-window
                From: ops@example.test
                To: me@example.test
                Subject: FYI: Maintenance window confirmed

                The maintenance window for the EU cluster is confirmed for May 20 from 01:00 to 03:00 UTC.
                This is informational only. No customer response or internal action is needed from you.
                """),
            CreateEmailCase(
                syntheticRoot,
                "E02",
                "Q3 renewal checklist request",
                "Action required: Q3 renewal checklist",
                "tasks",
                "Q3 renewal checklist",
                ["Prepare Q3 renewal checklist", "Upload updated pricing appendix", "Confirm legal review owner"],
                """
                Message-ID: E02-renewal-checklist
                From: manager@example.test
                To: me@example.test
                Subject: Action required: Q3 renewal checklist

                Please prepare the Q3 renewal checklist by Friday 17:00 UTC.
                Upload the updated pricing appendix to the project folder and confirm who owns legal review.
                This is for the current customer renewal project.
                """),
            CreateEmailCase(
                syntheticRoot,
                "E03",
                "Contoso outage escalation",
                "ASAP: customer outage executive response",
                "asap_response",
                "Contoso outage",
                ["Respond to Contoso outage escalation", "Send mitigation timeline"],
                """
                Message-ID: E03-contoso-outage
                From: account-director@example.test
                To: me@example.test
                Subject: ASAP: customer outage executive response

                Contoso is blocked by the billing outage and needs an ASAP response today by 15:00 UTC.
                Please confirm the mitigation owner and send the customer a short timeline before the executive call.
                """),
            CreateEmailCase(
                syntheticRoot,
                "E04",
                "Analytics release notes",
                "Newsletter: product analytics release notes",
                "informative",
                "analytics release",
                [],
                """
                Message-ID: E04-analytics-release
                From: product-news@example.test
                To: me@example.test
                Subject: Newsletter: product analytics release notes

                The analytics release adds cohort exports and dashboard filters next month.
                This newsletter has no assigned actions and does not ask for a response.
                """),
            CreateEmailCase(
                syntheticRoot,
                "E05",
                "Invoice cleanup delegated",
                "Delegated: invoice cleanup assigned to Petra",
                "no_action",
                "assigned Petra",
                [],
                """
                Message-ID: E05-delegated-invoice
                From: finance@example.test
                To: me@example.test
                Cc: petra@example.test
                Subject: Delegated: invoice cleanup assigned to Petra

                I assigned Petra to clean up invoice INV-2044 and reconcile the payment note.
                You are copied only for awareness. Please do not act unless Petra asks for help later.
                """),
            CreateEmailCase(
                syntheticRoot,
                "E06",
                "Board packet same-day reply",
                "ASAP: board packet and same-day reply",
                "asap_response",
                "board packet",
                ["Reply with board packet ETA", "Prepare board packet risk slide", "Export churn-risk customer list"],
                """
                Message-ID: E06-board-packet
                From: ceo@example.test
                To: me@example.test
                Subject: ASAP: board packet and same-day reply

                I need a same-day reply with the board packet ETA by 18:00 UTC.
                Please prepare the risk slide and export the churn-risk customer list before you respond.
                """),
            CreateEmailCase(
                syntheticRoot,
                "E07",
                "Partner portal cleanup",
                "Action required: partner portal cleanup",
                "tasks",
                "partner portal cleanup",
                ["Clean partner portal duplicates"],
                """
                Message-ID: E07-partner-portal
                From: partner-lead@example.test
                To: me@example.test
                Subject: Action required: partner portal cleanup

                Please clean partner portal duplicates this week.
                No customer reply is needed, but the portal list should be ready before the next partner sync.
                """),
            CreateEmailCase(
                syntheticRoot,
                "E08",
                "Contract status question",
                "Question: contract status for Delta",
                "tasks",
                "contract status",
                ["Reply with contract status"],
                """
                Message-ID: E08-contract-status
                From: sales@example.test
                To: me@example.test
                Subject: Question: contract status for Delta

                Can you reply with the current contract status for Delta and whether procurement has approved the latest terms?
                This is not urgent, but I need your answer for account planning.
                """)
        ];

    private static EmailCase CreateEmailCase(
        string syntheticRoot,
        string id,
        string title,
        string subject,
        string expectedRoute,
        string expectedEvidencePhrase,
        IReadOnlyList<string> expectedTaskTitles,
        string content)
        => new(
            id,
            title,
            subject,
            expectedRoute,
            expectedEvidencePhrase,
            expectedTaskTitles,
            Path.Combine(syntheticRoot, $"{id.ToLowerInvariant()}.eml"),
            content);

    private sealed class EmailWorkflowLlmInvoker : IWorkflowLlmComponentInvoker
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
            var sourceDocuments = ReadSourceDocuments(root);
            if (sourceDocuments.Count == 0)
            {
                throw new InvalidOperationException("Email workflow did not load an email source before classification.");
            }

            var emailText = string.Join(Environment.NewLine, sourceDocuments.Select(item => item.Text));
            var classification = Classify(emailText, ReadEmailId(emailText));
            var payload = new
            {
                route = classification.Route,
                summary = classification.Summary,
                markdown = BuildMarkdown(classification, sourceDocuments),
                emailCategory = classification.EmailCategory,
                isInformational = classification.IsInformational,
                asapResponseRequired = classification.AsapResponseRequired,
                tasks = classification.Tasks,
                actions = classification.Tasks.Select(task => task.Title).ToArray(),
                targets = classification.Tasks.Select(task => task.Owner).Where(owner => !string.IsNullOrWhiteSpace(owner)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                risk = classification.AsapResponseRequired ? "high" : classification.Tasks.Count > 0 ? "medium" : "low",
                relevant = classification.Route != "no_action",
                needsReview = classification.AsapResponseRequired,
                requiresResponse = classification.RequiresResponse,
                ready = true,
                projectId = ReadNestedString(root, "project", "id"),
                nodeId = ReadNestedString(root, "runContext", "workflowNodeId"),
                sourceUrl = string.Empty,
                project = CloneProperty(root, "project"),
                runContext = CloneProperty(root, "runContext")
            };

            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
                node.Id,
                JsonSerializer.Serialize(payload, JsonOptions),
                component.ResultShape));
        }

        private static EmailClassification Classify(string emailText, string sourceEmailId)
        {
            if (emailText.Contains("ASAP response today", StringComparison.OrdinalIgnoreCase) &&
                emailText.Contains("Contoso", StringComparison.OrdinalIgnoreCase))
            {
                return new EmailClassification(
                    "asap_response",
                    "Customer escalation requires same-day response and mitigation timeline.",
                    "asap_response",
                    IsInformational: false,
                    AsapResponseRequired: true,
                    RequiresResponse: true,
                    [
                        new EmailTask("Respond to Contoso outage escalation", "Send Contoso a same-day response confirming ownership and mitigation timing.", "me", "2026-05-12T15:00:00Z", "critical", true, true, sourceEmailId, ["Contoso outage", "ASAP response today by 15:00 UTC"]),
                        new EmailTask("Send mitigation timeline", "Prepare the short mitigation timeline before the executive call.", "me", "2026-05-12T15:00:00Z", "critical", true, true, sourceEmailId, ["send the customer a short timeline"])
                    ]);
            }

            if (emailText.Contains("board packet ETA", StringComparison.OrdinalIgnoreCase))
            {
                return new EmailClassification(
                    "asap_response",
                    "Executive request needs same-day reply plus board-packet preparation.",
                    "asap_response",
                    IsInformational: false,
                    AsapResponseRequired: true,
                    RequiresResponse: true,
                    [
                        new EmailTask("Reply with board packet ETA", "Send the CEO the same-day board packet ETA.", "me", "2026-05-12T18:00:00Z", "critical", true, true, sourceEmailId, ["board packet ETA by 18:00 UTC"]),
                        new EmailTask("Prepare board packet risk slide", "Prepare the risk slide before replying.", "me", "2026-05-12T18:00:00Z", "high", false, true, sourceEmailId, ["prepare the risk slide"]),
                        new EmailTask("Export churn-risk customer list", "Export the churn-risk customer list for the board packet.", "me", "2026-05-12T18:00:00Z", "high", false, true, sourceEmailId, ["export the churn-risk customer list"])
                    ]);
            }

            if (emailText.Contains("Q3 renewal checklist", StringComparison.OrdinalIgnoreCase))
            {
                return new EmailClassification(
                    "tasks",
                    "Renewal email assigns checklist, pricing, and legal-owner follow-up.",
                    "tasks",
                    IsInformational: false,
                    AsapResponseRequired: false,
                    RequiresResponse: false,
                    [
                        new EmailTask("Prepare Q3 renewal checklist", "Prepare the checklist for the current customer renewal project.", "me", "2026-05-15T17:00:00Z", "normal", false, false, sourceEmailId, ["Q3 renewal checklist", "by Friday 17:00 UTC"]),
                        new EmailTask("Upload updated pricing appendix", "Upload the pricing appendix to the project folder.", "me", "2026-05-15T17:00:00Z", "normal", false, false, sourceEmailId, ["Upload the updated pricing appendix"]),
                        new EmailTask("Confirm legal review owner", "Confirm the legal review owner for the renewal.", "me", "2026-05-15T17:00:00Z", "normal", false, false, sourceEmailId, ["confirm who owns legal review"])
                    ]);
            }

            if (emailText.Contains("partner portal duplicates", StringComparison.OrdinalIgnoreCase))
            {
                return new EmailClassification(
                    "tasks",
                    "Partner email assigns portal duplicate cleanup.",
                    "tasks",
                    IsInformational: false,
                    AsapResponseRequired: false,
                    RequiresResponse: false,
                    [
                        new EmailTask("Clean partner portal duplicates", "Clean the partner portal duplicates before the next partner sync.", "me", "", "normal", false, false, sourceEmailId, ["partner portal cleanup", "partner portal duplicates"])
                    ]);
            }

            if (emailText.Contains("contract status for Delta", StringComparison.OrdinalIgnoreCase))
            {
                return new EmailClassification(
                    "tasks",
                    "Sales email asks for a non-urgent contract-status reply.",
                    "response_task",
                    IsInformational: false,
                    AsapResponseRequired: false,
                    RequiresResponse: true,
                    [
                        new EmailTask("Reply with contract status", "Reply with Delta contract status and procurement approval state.", "me", "", "normal", true, false, sourceEmailId, ["contract status for Delta", "procurement has approved"])
                    ]);
            }

            if (emailText.Contains("assigned Petra", StringComparison.OrdinalIgnoreCase))
            {
                return new EmailClassification(
                    "no_action",
                    "Invoice cleanup is delegated to Petra and the current user is copied for awareness.",
                    "delegated_elsewhere",
                    IsInformational: false,
                    AsapResponseRequired: false,
                    RequiresResponse: false,
                    []);
            }

            if (emailText.Contains("analytics release", StringComparison.OrdinalIgnoreCase))
            {
                return new EmailClassification(
                    "informative",
                    "Product analytics newsletter has no assigned action.",
                    "informative",
                    IsInformational: true,
                    AsapResponseRequired: false,
                    RequiresResponse: false,
                    []);
            }

            return new EmailClassification(
                "informative",
                "Maintenance window notice is informational only.",
                "informative",
                IsInformational: true,
                AsapResponseRequired: false,
                RequiresResponse: false,
                []);
        }

        private static string BuildMarkdown(
            EmailClassification classification,
            IReadOnlyList<EmailSourceDocument> sourceDocuments)
        {
            var builder = new StringBuilder();
            builder.AppendLine("# Email intake result");
            builder.AppendLine();
            builder.AppendLine($"Route: {classification.Route}");
            builder.AppendLine($"Category: {classification.EmailCategory}");
            builder.AppendLine($"Summary: {classification.Summary}");
            builder.AppendLine();
            builder.AppendLine("Sources:");
            foreach (var source in sourceDocuments)
            {
                builder.AppendLine($"- {source.FileName}: {source.Path}");
            }

            builder.AppendLine();
            builder.AppendLine("Source excerpts:");
            foreach (var source in sourceDocuments)
            {
                builder.AppendLine($"- {BuildSourceExcerpt(source.Text)}");
            }

            if (classification.Tasks.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Tasks:");
                foreach (var task in classification.Tasks)
                {
                    builder.AppendLine($"- {task.Title}: {task.Summary}");
                }
            }

            return builder.ToString();
        }

        private static string BuildSourceExcerpt(string value)
        {
            var normalized = value
                .Replace("\r", " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal);
            return normalized.Length <= 500
                ? normalized
                : normalized[..500];
        }

        private static IReadOnlyList<EmailSourceDocument> ReadSourceDocuments(JsonElement root)
        {
            if (!root.TryGetProperty("sourceDocuments", out var documents) ||
                documents.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return documents.EnumerateArray()
                .Select(item => new EmailSourceDocument(
                    ReadString(item, "fileName"),
                    ReadString(item, "path"),
                    ReadString(item, "text"),
                    ReadString(item, "extractionStatus")))
                .ToArray();
        }

        private static string ReadEmailId(string emailText)
        {
            foreach (var line in emailText.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (line.StartsWith("Message-ID:", StringComparison.OrdinalIgnoreCase))
                {
                    return line["Message-ID:".Length..].Trim();
                }
            }

            return "synthetic-email";
        }

        private static JsonElement? CloneProperty(JsonElement root, string propertyName)
            => root.TryGetProperty(propertyName, out var property)
                ? property.Clone()
                : null;

        private static string ReadNestedString(JsonElement root, string objectName, string propertyName)
        {
            if (!root.TryGetProperty(objectName, out var item))
            {
                return string.Empty;
            }

            return ReadString(item, propertyName);
        }

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
    }

    private sealed record EmailSourceDocument(
        string FileName,
        string Path,
        string Text,
        string ExtractionStatus);

    private sealed record EmailClassification(
        string Route,
        string Summary,
        string EmailCategory,
        bool IsInformational,
        bool AsapResponseRequired,
        bool RequiresResponse,
        IReadOnlyList<EmailTask> Tasks);

    private sealed record EmailTask(
        string Title,
        string Summary,
        string Owner,
        string DueUtc,
        string Urgency,
        bool RequiresResponse,
        bool Asap,
        string SourceEmailId,
        IReadOnlyList<string> Evidence);

    private sealed record EmailCase(
        string Id,
        string Title,
        string Subject,
        string ExpectedRoute,
        string ExpectedEvidencePhrase,
        IReadOnlyList<string> ExpectedTaskTitles,
        string FilePath,
        string Content);

    private sealed record EmailCaseRunResult(
        string Id,
        string Subject,
        string Route,
        string State,
        int ProgressPercent,
        IReadOnlyList<string> CreatedTaskTitles,
        IReadOnlyList<string> CreatedAssetTitles,
        IReadOnlyList<string> CreatedNodeIds,
        IReadOnlyList<string> CreatedAssetIds);

    private sealed record EmailWorkflowProof(
        DateTimeOffset CompletedAtUtc,
        Guid WorkflowId,
        Guid WorkflowVersionId,
        IReadOnlyList<EmailCaseRunResult> Results);
}
