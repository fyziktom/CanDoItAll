using System.Net;
using System.Net.Sockets;
using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Tools.Documents;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowExecutorTests
{
    [Fact]
    public void CatalogListsBuiltInAndPlannedExecutors()
    {
        var catalog = new WorkflowExecutorCatalog(
        [
            new RecordingWorkflowExecutor(),
            new PlannedWorkflowExecutor(BuiltInWorkflowExecutorDescriptors.Planned[0])
        ]);

        var descriptors = catalog.ListExecutors();

        Assert.Contains(descriptors, descriptor => descriptor.Id == WorkflowExecutorIds.StorageFile);
        Assert.Contains(descriptors, descriptor => descriptor.Id == WorkflowExecutorIds.JsonTransform && !descriptor.IsImplemented);
    }

    [Fact]
    public void ValidatorRejectsUnknownExecutorId()
    {
        var catalog = new WorkflowExecutorCatalog([new RecordingWorkflowExecutor()]);
        var validator = new WorkflowDefinitionValidator(catalog);
        var definition = CreateDefinition(
        [
            CreateNode("start", WorkflowNodeKind.Start),
            CreateExecutorNode("tool", new WorkflowExecutorId("missing.executor")),
            CreateNode("end", WorkflowNodeKind.End)
        ], [
            CreateEdge("start-tool", "start", "tool"),
            CreateEdge("tool-end", "tool", "end")
        ]);

        var result = validator.Validate(definition, []);

        Assert.Contains(result.Issues, issue => issue.Code == WorkflowValidationIssueCode.InvalidExecutorReference);
    }

    [Fact]
    public void ValidatorRejectsInvalidExecutorPolicy()
    {
        var catalog = new WorkflowExecutorCatalog([new RecordingWorkflowExecutor()]);
        var validator = new WorkflowDefinitionValidator(catalog);
        var definition = CreateDefinition(
        [
            CreateNode("start", WorkflowNodeKind.Start),
            CreateExecutorNode("tool", WorkflowExecutorIds.StorageFile) with
            {
                Settings = CreateSettings(WorkflowExecutorIds.StorageFile) with
                {
                    ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 0 }
                }
            },
            CreateNode("end", WorkflowNodeKind.End)
        ], [
            CreateEdge("start-tool", "start", "tool"),
            CreateEdge("tool-end", "tool", "end")
        ]);

        var result = validator.Validate(definition, []);

        Assert.Contains(result.Issues, issue => issue.Code == WorkflowValidationIssueCode.InvalidExecutionPolicy);
    }

    [Fact]
    public async Task MafCompilerInvokesExecutorNodeThroughInvoker()
    {
        var executor = new RecordingWorkflowExecutor();
        var catalog = new WorkflowExecutorCatalog([executor]);
        var invoker = new WorkflowExecutorInvoker(catalog, [executor]);
        var compiler = new MafWorkflowCompiler(new WorkflowDefinitionValidator(catalog), invoker);
        var backend = new MafInProcessWorkflowExecutionBackend(compiler, []);
        var definition = CreateDefinition(
        [
            CreateExecutorNode("tool", WorkflowExecutorIds.StorageFile),
            CreateNode("end", WorkflowNodeKind.End)
        ], [
            CreateEdge("tool-end", "tool", "end")
        ],
        startNodeId: "tool") with
        {
            RuntimePolicy = new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false)
        };

        var result = await backend.StartAsync(
            definition,
            new WorkflowRunStartRequest(
                definition.Id,
                definition.VersionId,
                "{\"input\":\"hello\"}",
                WorkflowRuntimeBackendKind.InProcess,
                SourceProcessRunId: null,
                SourceProcessAssignmentId: null),
            WorkflowRunId.New());

        Assert.Equal(WorkflowRunState.Completed, result.Run.State);
        Assert.Equal(1, executor.InvocationCount);
    }

    [Fact]
    public async Task MafCompilerRoutesStartOutputIntoExecutorNode()
    {
        var executor = new RecordingWorkflowExecutor();
        var catalog = new WorkflowExecutorCatalog([executor]);
        var invoker = new WorkflowExecutorInvoker(catalog, [executor]);
        var compiler = new MafWorkflowCompiler(new WorkflowDefinitionValidator(catalog), invoker);
        var backend = new MafInProcessWorkflowExecutionBackend(compiler, []);
        var definition = CreateDefinition(
        [
            CreateNode("start", WorkflowNodeKind.Start),
            CreateExecutorNode("tool", WorkflowExecutorIds.StorageFile),
            CreateNode("end", WorkflowNodeKind.End)
        ], [
            CreateEdge("start-tool", "start", "tool"),
            CreateEdge("tool-end", "tool", "end")
        ]);

        var result = await backend.StartAsync(
            definition,
            new WorkflowRunStartRequest(
                definition.Id,
                definition.VersionId,
                "{\"input\":\"hello\"}",
                WorkflowRuntimeBackendKind.InProcess,
                SourceProcessRunId: null,
                SourceProcessAssignmentId: null),
            WorkflowRunId.New());

        Assert.Equal(WorkflowRunState.Completed, result.Run.State);
        Assert.Equal(1, executor.InvocationCount);
    }

    [Fact]
    public async Task MafCompilerRoutesExecutorOutputThroughLlmIntoNextExecutor()
    {
        var component = CreateLlmComponent(
            inputShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "Project tree JSON"),
            resultShape: WorkflowValueShape.Text);
        var executor = new RoutingWorkflowExecutor();
        var catalog = new WorkflowExecutorCatalog([executor]);
        var invoker = new WorkflowExecutorInvoker(catalog, [executor]);
        var llmInvoker = new RecordingLlmComponentInvoker(input =>
            $"WORKFLOW_LLM_TRANSFORMED\n\nInput contained approval: {input.Contains("Approval decision", StringComparison.OrdinalIgnoreCase)}");
        var compiler = new MafWorkflowCompiler(new WorkflowDefinitionValidator(catalog), invoker, llmInvoker);
        var backend = new MafInProcessWorkflowExecutionBackend(compiler, [component]);
        var definition = CreateDefinition(
        [
            CreateNode("start", WorkflowNodeKind.Start),
            CreateExecutorNode("read-tree", WorkflowExecutorIds.StorageFile),
            CreateLlmNode("summarize", component.Id),
            CreateExecutorNode("save-asset", WorkflowExecutorIds.StorageFile),
            CreateNode("end", WorkflowNodeKind.End)
        ], [
            CreateEdge("start-read", "start", "read-tree"),
            CreateEdge("read-llm", "read-tree", "summarize"),
            CreateEdge("llm-save", "summarize", "save-asset"),
            CreateEdge("save-end", "save-asset", "end")
        ]);

        var result = await backend.StartAsync(
            definition,
            new WorkflowRunStartRequest(
                definition.Id,
                definition.VersionId,
                "{\"input\":\"project\"}",
                WorkflowRuntimeBackendKind.InProcess,
                SourceProcessRunId: null,
                SourceProcessAssignmentId: null),
            WorkflowRunId.New());

        Assert.Equal(WorkflowRunState.Completed, result.Run.State);
        Assert.Contains("Approval decision", llmInvoker.InputPayloads.Single(), StringComparison.Ordinal);
        Assert.Contains("WORKFLOW_LLM_TRANSFORMED", executor.InputsByNode["save-asset"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokerRetriesTransientExecutorFailure()
    {
        var executor = new RecordingWorkflowExecutor { FailuresBeforeSuccess = 1 };
        var catalog = new WorkflowExecutorCatalog([executor]);
        var invoker = new WorkflowExecutorInvoker(catalog, [executor]);
        var node = CreateExecutorNode("tool", WorkflowExecutorIds.StorageFile) with
        {
            Settings = CreateSettings(WorkflowExecutorIds.StorageFile) with
            {
                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default with
                {
                    MaxRetryAttempts = 1,
                    RetryDelayMilliseconds = 1
                }
            }
        };

        var result = await invoker.ExecuteAsync(
            CreateDefinition([CreateNode("start", WorkflowNodeKind.Start), node, CreateNode("end", WorkflowNodeKind.End)], [
                CreateEdge("start-tool", "start", "tool"),
                CreateEdge("tool-end", "tool", "end")
            ]),
            node,
            new WorkflowNodeInput("{}"));

        Assert.Equal("{\"recorded\":true}", result.PayloadJson);
        Assert.Equal(2, executor.InvocationCount);
    }

    [Fact]
    public async Task WorkspaceFileExecutorWritesAndReadsThroughStorageService()
    {
        using var temp = new TempDirectory();
        var service = new WorkspaceFileService(temp.Path);
        var executor = new WorkspaceFileWorkflowExecutor(service);
        var writeContext = CreateExecutionContext(
            executor.Descriptor,
            new WorkflowStorageFileExecutorSettings
            {
                Operation = WorkflowStorageFileOperation.WriteText,
                Path = "reports/summary.md",
                Content = "# Report"
            });

        await executor.ExecuteAsync(writeContext, new WorkflowNodeInput("{}"));

        var readContext = CreateExecutionContext(
            executor.Descriptor,
            new WorkflowStorageFileExecutorSettings
            {
                Operation = WorkflowStorageFileOperation.ReadText,
                Path = "reports/summary.md"
            });
        var result = await executor.ExecuteAsync(readContext, new WorkflowNodeInput("{}"));

        Assert.Contains("# Report", result.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public void SpreadsheetDocumentServiceCreatesReadsAndRendersWorkbook()
    {
        using var temp = new TempDirectory();
        var workbookPath = Path.Combine(temp.Path, "input.xlsx");
        var service = new ClosedXmlSpreadsheetDocumentService();

        service.Write(new SpreadsheetWriteRequest(
            workbookPath,
            workbookPath,
            "Data",
            [new SpreadsheetCellWrite("A1", "Name"), new SpreadsheetCellWrite("B1", "Value")],
            [new SpreadsheetRangeWrite("A2:B3", [["Alpha", "10"], ["Beta", "20"]])],
            CreateWorkbookIfMissing: true,
            Overwrite: true));

        var cell = service.ReadCell(workbookPath, "Data", "A2");
        var range = service.ReadRange(workbookPath, "Data", "A1:B3", maxRows: 10, maxColumns: 10);

        Assert.Equal("Alpha", cell.Value);
        Assert.Contains("| Name | Value |", range.MarkdownTable, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WorkflowExecutorScenarioMatrixCoversRealWorldExamples()
    {
        using var temp = new TempDirectory();
        var workspaceFiles = new WorkspaceFileService(temp.Path);
        var storageExecutor = new WorkspaceFileWorkflowExecutor(workspaceFiles);
        var spreadsheetExecutor = new SpreadsheetWorkflowExecutor(
            new ClosedXmlSpreadsheetDocumentService(),
            new WorkspacePathResolutionService(temp.Path));
        var completedScenarios = new List<string>();

        async Task RecordAsync(string name, Func<Task> scenario)
        {
            await scenario();
            completedScenarios.Add(name);
        }

        await RecordAsync("storage writes markdown report", async () =>
        {
            await ExecuteDirectAsync(storageExecutor, new WorkflowStorageFileExecutorSettings
            {
                Operation = WorkflowStorageFileOperation.WriteText,
                Path = "reports/summary.md",
                Content = "# Invoice summary\ninvoice total: 120"
            });

            Assert.True(File.Exists(Path.Combine(temp.Path, "reports", "summary.md")));
        });

        await RecordAsync("storage appends audit line", async () =>
        {
            await ExecuteDirectAsync(storageExecutor, new WorkflowStorageFileExecutorSettings
            {
                Operation = WorkflowStorageFileOperation.AppendText,
                Path = "reports/summary.md",
                Content = "\nstatus: reviewed"
            });

            Assert.Contains("reviewed", File.ReadAllText(Path.Combine(temp.Path, "reports", "summary.md")), StringComparison.Ordinal);
        });

        await RecordAsync("storage reads report text", async () =>
        {
            var result = await ExecuteDirectAsync(storageExecutor, new WorkflowStorageFileExecutorSettings
            {
                Operation = WorkflowStorageFileOperation.ReadText,
                Path = "reports/summary.md"
            });

            Assert.Contains("invoice total", result.PayloadJson, StringComparison.Ordinal);
        });

        await RecordAsync("storage lists markdown files", async () =>
        {
            var result = await ExecuteDirectAsync(storageExecutor, new WorkflowStorageFileExecutorSettings
            {
                Operation = WorkflowStorageFileOperation.List,
                Path = "reports",
                SearchPattern = "*.md"
            });

            Assert.Contains("summary.md", result.PayloadJson, StringComparison.OrdinalIgnoreCase);
        });

        await RecordAsync("storage stats report file", async () =>
        {
            var result = await ExecuteDirectAsync(storageExecutor, new WorkflowStorageFileExecutorSettings
            {
                Operation = WorkflowStorageFileOperation.Stat,
                Path = "reports/summary.md"
            });

            Assert.Contains("summary.md", result.PayloadJson, StringComparison.OrdinalIgnoreCase);
        });

        await RecordAsync("storage searches report text", async () =>
        {
            var result = await ExecuteDirectAsync(storageExecutor, new WorkflowStorageFileExecutorSettings
            {
                Operation = WorkflowStorageFileOperation.SearchText,
                Path = "reports",
                Query = "invoice"
            });

            Assert.Contains("invoice", result.PayloadJson, StringComparison.OrdinalIgnoreCase);
        });

        await RecordAsync("storage diffs text files", async () =>
        {
            await File.WriteAllTextAsync(Path.Combine(temp.Path, "left.txt"), "alpha\nbeta\n");
            await File.WriteAllTextAsync(Path.Combine(temp.Path, "right.txt"), "alpha\ngamma\n");
            var result = await ExecuteDirectAsync(storageExecutor, new WorkflowStorageFileExecutorSettings
            {
                Operation = WorkflowStorageFileOperation.DiffText,
                Path = "left.txt",
                DestinationPath = "right.txt"
            });

            Assert.Contains("gamma", result.PayloadJson, StringComparison.OrdinalIgnoreCase);
        });

        await RecordAsync("storage fails predictably for missing file", async () =>
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteDirectAsync(storageExecutor, new WorkflowStorageFileExecutorSettings
            {
                Operation = WorkflowStorageFileOperation.ReadText,
                Path = "missing.md"
            }));
        });

        await RecordAsync("http gets local json", async () =>
        {
            await using var server = SingleResponseHttpServer.Json(200, "{\"ok\":true}");
            var result = await ExecuteDirectAsync(new HttpFetchWorkflowExecutor(), new WorkflowHttpExecutorSettings
            {
                Method = WorkflowHttpMethodKind.Get,
                Url = server.Url
            });

            Assert.Contains("ok", result.PayloadJson, StringComparison.Ordinal);
        });

        await RecordAsync("http posts bounded payload", async () =>
        {
            await using var server = SingleResponseHttpServer.Json(201, "{\"created\":true}");
            var result = await ExecuteDirectAsync(new HttpFetchWorkflowExecutor(), new WorkflowHttpExecutorSettings
            {
                Method = WorkflowHttpMethodKind.Post,
                Url = server.Url,
                Body = "{\"name\":\"report\"}"
            });

            Assert.Contains("201", result.PayloadJson, StringComparison.Ordinal);
        });

        await RecordAsync("http fails on server error", async () =>
        {
            await using var server = SingleResponseHttpServer.Json(500, "{\"error\":\"boom\"}");
            await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteDirectAsync(new HttpFetchWorkflowExecutor(), new WorkflowHttpExecutorSettings
            {
                Method = WorkflowHttpMethodKind.Get,
                Url = server.Url
            }));
        });

        await RecordAsync("http rejects unsupported scheme", async () =>
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteDirectAsync(new HttpFetchWorkflowExecutor(), new WorkflowHttpExecutorSettings
            {
                Url = "ftp://example.test/file.txt"
            }));
        });

        await RecordAsync("spreadsheet writes single invoice cell", async () =>
        {
            await ExecuteDirectAsync(spreadsheetExecutor, new WorkflowSpreadsheetExecutorSettings
            {
                Operation = WorkflowSpreadsheetOperation.WriteCell,
                WorkbookPath = "invoices.xlsx",
                WorksheetName = "Invoices",
                CellAddress = "A1",
                Value = "Customer",
                CreateWorkbookIfMissing = true
            });

            Assert.True(File.Exists(Path.Combine(temp.Path, "invoices.xlsx")));
        });

        await RecordAsync("spreadsheet reads single invoice cell", async () =>
        {
            var result = await ExecuteDirectAsync(spreadsheetExecutor, new WorkflowSpreadsheetExecutorSettings
            {
                Operation = WorkflowSpreadsheetOperation.ReadCell,
                WorkbookPath = "invoices.xlsx",
                WorksheetName = "Invoices",
                CellAddress = "A1"
            });

            Assert.Contains("Customer", result.PayloadJson, StringComparison.Ordinal);
        });

        await RecordAsync("spreadsheet writes tabular invoice range", async () =>
        {
            await ExecuteDirectAsync(spreadsheetExecutor, new WorkflowSpreadsheetExecutorSettings
            {
                Operation = WorkflowSpreadsheetOperation.ApplyBatch,
                WorkbookPath = "invoices.xlsx",
                WorksheetName = "Invoices",
                RangeWrites =
                [
                    new WorkflowSpreadsheetRangeWrite("A2:C4", [["Customer", "Amount", "Status"], ["Aqua", "120", "Paid"], ["Contoso", "80", "Open"]])
                ]
            });

            Assert.True(File.Exists(Path.Combine(temp.Path, "invoices.xlsx")));
        });

        await RecordAsync("spreadsheet renders range to markdown", async () =>
        {
            var result = await ExecuteDirectAsync(spreadsheetExecutor, new WorkflowSpreadsheetExecutorSettings
            {
                Operation = WorkflowSpreadsheetOperation.RangeToMarkdown,
                WorkbookPath = "invoices.xlsx",
                WorksheetName = "Invoices",
                RangeAddress = "A2:C4"
            });

            Assert.Contains("| Customer | Amount | Status |", result.PayloadJson, StringComparison.Ordinal);
        });

        await RecordAsync("spreadsheet inspects workbook summary", async () =>
        {
            var result = await ExecuteDirectAsync(spreadsheetExecutor, new WorkflowSpreadsheetExecutorSettings
            {
                Operation = WorkflowSpreadsheetOperation.WorkbookSummary,
                WorkbookPath = "invoices.xlsx"
            });

            Assert.Contains("Invoices", result.PayloadJson, StringComparison.Ordinal);
        });

        await RecordAsync("spreadsheet fails predictably for missing workbook", async () =>
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteDirectAsync(spreadsheetExecutor, new WorkflowSpreadsheetExecutorSettings
            {
                Operation = WorkflowSpreadsheetOperation.WorkbookSummary,
                WorkbookPath = "missing.xlsx"
            }));
        });

        await RecordAsync("invoker retries transient executor failure", async () =>
        {
            var executor = new RecordingWorkflowExecutor { FailuresBeforeSuccess = 1 };
            var catalog = new WorkflowExecutorCatalog([executor]);
            var invoker = new WorkflowExecutorInvoker(catalog, [executor]);
            var node = CreateExecutorNode("tool", WorkflowExecutorIds.StorageFile) with
            {
                Settings = CreateSettings(WorkflowExecutorIds.StorageFile) with
                {
                    ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default with
                    {
                        MaxRetryAttempts = 1,
                        RetryDelayMilliseconds = 1
                    }
                }
            };

            await invoker.ExecuteAsync(CreateDefinition([node], [], "tool"), node, new WorkflowNodeInput("{}"));

            Assert.Equal(2, executor.InvocationCount);
        });

        await RecordAsync("invoker rejects invalid timeout policy", async () =>
        {
            var executor = new RecordingWorkflowExecutor();
            var catalog = new WorkflowExecutorCatalog([executor]);
            var invoker = new WorkflowExecutorInvoker(catalog, [executor]);
            var node = CreateExecutorNode("tool", WorkflowExecutorIds.StorageFile) with
            {
                Settings = CreateSettings(WorkflowExecutorIds.StorageFile) with
                {
                    ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 0 }
                }
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() => invoker.ExecuteAsync(CreateDefinition([node], [], "tool"), node, new WorkflowNodeInput("{}")).AsTask());
        });

        await RecordAsync("project structure reports missing host service", async () =>
        {
            var provider = new ServiceCollection().BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
            await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteDirectAsync(new ProjectStructureWorkflowExecutor(scopeFactory), new WorkflowProjectStructureExecutorSettings
            {
                Operation = WorkflowProjectStructureOperation.ListProjects,
                ProjectId = Guid.NewGuid()
            }));
        });

        await RecordAsync("image generation reports missing provider bridge", async () =>
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteDirectAsync(new ImageGenerationWorkflowExecutor(), new WorkflowImageGenerationExecutorSettings
            {
                Prompt = "A clean workflow diagram"
            }));
        });

        await RecordAsync("planned executor reports not implemented", async () =>
        {
            await Assert.ThrowsAsync<NotSupportedException>(() => ExecuteDirectAsync(new PlannedWorkflowExecutor(BuiltInWorkflowExecutorDescriptors.Planned[0]), new { }));
        });

        Assert.True(completedScenarios.Count >= 20);
    }

    private static WorkflowExecutorExecutionContext CreateExecutionContext<TSettings>(
        WorkflowExecutorDescriptor descriptor,
        TSettings settings)
    {
        var node = CreateExecutorNode("tool", descriptor.Id) with
        {
            Settings = CreateSettings(descriptor.Id) with
            {
                ExecutorSettingsJson = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)
                {
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                })
            }
        };

        return new WorkflowExecutorExecutionContext(
            CreateDefinition([CreateNode("start", WorkflowNodeKind.Start), node, CreateNode("end", WorkflowNodeKind.End)], [
                CreateEdge("start-tool", "start", "tool"),
                CreateEdge("tool-end", "tool", "end")
            ]),
            node,
            descriptor,
            node.Settings.ExecutorSettingsJson,
            WorkflowExecutorExecutionPolicy.Default);
    }

    private static WorkflowDefinition CreateDefinition(
        IReadOnlyList<WorkflowNode> nodes,
        IReadOnlyList<WorkflowEdge> edges,
        string startNodeId = "start")
    {
        return new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Executor workflow",
            "Executor workflow for tests.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(new WorkflowNodeId(startNodeId), nodes, edges),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private static async Task<WorkflowNodeExecutionResult> ExecuteDirectAsync<TSettings>(
        IWorkflowExecutor executor,
        TSettings settings)
    {
        return await executor.ExecuteAsync(
            CreateExecutionContext(executor.Descriptor, settings),
            new WorkflowNodeInput("{}"));
    }

    private static WorkflowNode CreateExecutorNode(string id, WorkflowExecutorId executorId)
        => new(
            new WorkflowNodeId(id),
            WorkflowNodeKind.Executor,
            id,
            [],
            CreateSettings(executorId));

    private static WorkflowNode CreateLlmNode(string id, WorkflowComponentId componentId)
        => new(
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
                InputShape: WorkflowValueShape.Text,
                ResultShape: WorkflowValueShape.Text));

    private static WorkflowNodeSettings CreateSettings(WorkflowExecutorId executorId)
        => new WorkflowNodeSettings(
            ComponentId: null,
            AgentId: null,
            SubworkflowId: null,
            ExternalRequestKind: null,
            Instructions: string.Empty,
            InputShape: WorkflowValueShape.Text,
            ResultShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON")) with
        {
            ExecutorId = executorId,
            ExecutorSettingsJson = "{}",
            ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
        };

    private static WorkflowNode CreateNode(string id, WorkflowNodeKind kind)
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
                InputShape: kind == WorkflowNodeKind.End
                    ? new WorkflowValueShape(WorkflowValueShapeKind.Object, "{}", "Any result")
                    : WorkflowValueShape.Text,
                ResultShape: WorkflowValueShape.Text));

    private static WorkflowEdge CreateEdge(string id, string source, string target)
        => new(
            new WorkflowEdgeId(id),
            new WorkflowNodeId(source),
            SourcePortId: null,
            new WorkflowNodeId(target),
            TargetPortId: null,
            WorkflowEdgeKind.Direct,
            ConditionExpression: string.Empty);

    private static LlmCallComponent CreateLlmComponent(
        WorkflowValueShape inputShape,
        WorkflowValueShape resultShape)
        => new(
            WorkflowComponentId.New(),
            "Project summarizer",
            ProviderProfileId: null,
            "gpt-5-mini",
            WorkflowModality.Text,
            new WorkflowModelSettings(
                Temperature: 0,
                MaxOutputTokens: 400,
                RequireJsonOutput: resultShape.Kind == WorkflowValueShapeKind.Json,
                ResponseFormatJsonSchema: string.Empty),
            "Summarize the workflow payload.",
            inputShape,
            resultShape,
            AgentPermissionsPolicy.Default,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    private sealed class RecordingWorkflowExecutor : IWorkflowExecutor
    {
        public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.StorageFile;

        public int InvocationCount { get; private set; }

        public int FailuresBeforeSuccess { get; init; }

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            if (InvocationCount <= FailuresBeforeSuccess)
            {
                throw new InvalidOperationException("Transient test failure.");
            }

            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
                context.Node.Id,
                "{\"recorded\":true}",
                context.Descriptor.ResultShape));
        }
    }

    private sealed class RoutingWorkflowExecutor : IWorkflowExecutor
    {
        public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.StorageFile;

        public Dictionary<string, string> InputsByNode { get; } = new(StringComparer.Ordinal);

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
        {
            InputsByNode[context.Node.Id.Value] = input.PayloadJson;
            var payload = context.Node.Id.Value switch
            {
                "read-tree" => "{\"projectName\":\"Solar Asset Invoice Intake\",\"nodes\":[{\"title\":\"Approval decision\"}]}",
                "save-asset" => "{\"saved\":true}",
                _ => "{}"
            };

            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
                context.Node.Id,
                payload,
                context.Descriptor.ResultShape));
        }
    }

    private sealed class RecordingLlmComponentInvoker(Func<string, string> transform) : IWorkflowLlmComponentInvoker
    {
        public List<string> InputPayloads { get; } = [];

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowDefinition definition,
            WorkflowNode node,
            LlmCallComponent component,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
        {
            InputPayloads.Add(input.PayloadJson);
            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
                node.Id,
                transform(input.PayloadJson),
                component.ResultShape));
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"candoitall-workflow-executor-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class SingleResponseHttpServer : IAsyncDisposable
    {
        private readonly TcpListener listener;
        private readonly Task serverTask;

        private SingleResponseHttpServer(int statusCode, string body, string contentType)
        {
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Url = $"http://127.0.0.1:{port}/scenario";
            serverTask = Task.Run(() => ServeOnceAsync(statusCode, body, contentType));
        }

        public string Url { get; }

        public static SingleResponseHttpServer Json(int statusCode, string body)
            => new(statusCode, body, "application/json");

        public async ValueTask DisposeAsync()
        {
            listener.Stop();
            try
            {
                await serverTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (Exception exception) when (exception is SocketException or ObjectDisposedException or TimeoutException)
            {
            }
        }

        private async Task ServeOnceAsync(int statusCode, string body, string contentType)
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            await ReadRequestHeadersAsync(stream);
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var reason = statusCode switch
            {
                >= 200 and < 300 => "OK",
                >= 500 => "Internal Server Error",
                _ => "Status"
            };
            var header =
                $"HTTP/1.1 {statusCode} {reason}\r\n" +
                $"Content-Type: {contentType}\r\n" +
                $"Content-Length: {bodyBytes.Length}\r\n" +
                "Connection: close\r\n\r\n";
            var headerBytes = Encoding.ASCII.GetBytes(header);
            await stream.WriteAsync(headerBytes);
            await stream.WriteAsync(bodyBytes);
        }

        private static async Task ReadRequestHeadersAsync(NetworkStream stream)
        {
            var buffer = new byte[1024];
            var received = new List<byte>();
            while (received.Count < 8192)
            {
                var read = await stream.ReadAsync(buffer);
                if (read == 0)
                {
                    return;
                }

                received.AddRange(buffer.Take(read));
                if (received.Count >= 4 &&
                    Encoding.ASCII.GetString(received.ToArray()).Contains("\r\n\r\n", StringComparison.Ordinal))
                {
                    return;
                }
            }
        }
    }
}
