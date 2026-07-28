using System.Net;
using System.Net.Sockets;
using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Control;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Documents;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Media;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Network;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Transforms;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard;
using CanDoItAll.Modules.Security;
using CanDoItAll.Tools.Documents;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowExecutorTests
{
    private static readonly WorkflowValueShape JsonObjectShape = new(
        WorkflowValueShapeKind.Object,
        "{}",
        "JSON object");

    private static readonly WorkflowValueShape JsonPayloadShape = new(
        WorkflowValueShapeKind.Json,
        "{}",
        "JSON payload");

    [Fact]
    public void CatalogListsBuiltInAndPlannedExecutors()
    {
        var catalog = new WorkflowExecutorCatalog(
        [
            new RecordingWorkflowExecutor(),
            new JsonTransformWorkflowExecutor(),
            new PlannedWorkflowExecutor(BuiltInWorkflowExecutorDescriptors.Planned[0])
        ]);

        var descriptors = catalog.ListExecutors();

        Assert.Contains(descriptors, descriptor => descriptor.Id == WorkflowExecutorIds.StorageFile);
        Assert.Contains(descriptors, descriptor => descriptor.Id == WorkflowExecutorIds.JsonTransform && descriptor.CanExecute);
        Assert.Contains(descriptors, descriptor => descriptor.Id == WorkflowExecutorIds.CommandProcess && !descriptor.IsImplemented);
    }

    [Fact]
    public void BuiltInRegistrationAddsImplementedAndPlannedContributions()
    {
        var services = new ServiceCollection();

        services.AddStandardWorkflowExecutors();

        var contributionDescriptors = services
            .Where(descriptor => descriptor.ServiceType == typeof(IWorkflowExecutorContribution))
            .ToArray();
        var implementationTypes = services
            .Where(descriptor => descriptor.ServiceType == typeof(IWorkflowExecutor))
            .Select(descriptor => descriptor.ImplementationType)
            .OfType<Type>()
            .Select(type => type.GetGenericArguments().Single())
            .ToArray();
        var plannedDescriptors = contributionDescriptors
            .Select(descriptor => descriptor.ImplementationInstance)
            .OfType<IWorkflowExecutorContribution>()
            .Where(contribution => !contribution.Descriptor.CanExecute)
            .Select(contribution => contribution.Descriptor)
            .ToArray();

        Assert.Equal(13 + BuiltInWorkflowExecutorDescriptors.Planned.Count, contributionDescriptors.Length);
        Assert.Contains(typeof(WorkspaceFileWorkflowExecutor), implementationTypes);
        Assert.Contains(typeof(JsonTransformWorkflowExecutor), implementationTypes);
        Assert.Contains(typeof(MarkdownRenderWorkflowExecutor), implementationTypes);
        Assert.Contains(typeof(SourceIngestionWorkflowExecutor), implementationTypes);
        Assert.Contains(typeof(HttpFetchWorkflowExecutor), implementationTypes);
        Assert.Contains(typeof(DelayWorkflowExecutor), implementationTypes);
        Assert.Contains(typeof(HumanApprovalWorkflowExecutor), implementationTypes);
        Assert.Contains(typeof(SpreadsheetWorkflowExecutor), implementationTypes);
        Assert.Contains(typeof(ProjectStructureWorkflowExecutor), implementationTypes);
        Assert.Contains(typeof(ImageGenerationWorkflowExecutor), implementationTypes);
        Assert.Contains(typeof(DocumentToMarkdownWorkflowExecutor), implementationTypes);
        Assert.Contains(typeof(ImageInspectWorkflowExecutor), implementationTypes);
        Assert.Contains(typeof(ImageAnalyzeWorkflowExecutor), implementationTypes);
        Assert.Equal(BuiltInWorkflowExecutorDescriptors.Planned, plannedDescriptors);
        Assert.Equal(13, services.Count(descriptor => descriptor.ServiceType == typeof(IWorkflowExecutor)));
    }

    [Fact]
    public async Task ImageGenerationWorkflowExecutor_WritesProviderRuntimeOutput()
    {
        using var temp = new TempDirectory();
        var provider = CreateProviderProfile("gpt-image-1-mini") with
        {
            Purpose = ProviderProfilePurpose.ImageGeneration,
            DefaultModel = "gpt-image-1-mini"
        };
        var imageService = new RecordingImageGenerationService(
            [4, 5, 6],
            "workflow revised prompt");
        var executor = new ImageGenerationWorkflowExecutor(
            new TestProviderProfileRegistry([provider]),
            imageService,
            new WorkspacePathResolutionService(temp.Path));

        var result = await ExecuteDirectAsync(executor, new WorkflowImageGenerationExecutorSettings
        {
            ProviderProfileId = provider.Id,
            Prompt = "A clean workflow diagram",
            Model = "gpt-image-1-mini",
            Size = "1024x1024",
            Quality = "low",
            OutputFormat = "png",
            OutputWorkspacePath = "output/workflow-image"
        });

        var outputPath = System.IO.Path.Combine(temp.Path, "output", "workflow-image.png");
        Assert.True(File.Exists(outputPath));
        Assert.Equal(new byte[] { 4, 5, 6 }, await File.ReadAllBytesAsync(outputPath));
        Assert.Contains("workflow-image.png", result.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("workflow revised prompt", result.PayloadJson, StringComparison.Ordinal);
        var request = Assert.Single(imageService.Requests);
        Assert.Equal(provider.Id, request.Provider.Id);
        Assert.Equal("gpt-image-1-mini", request.Model);
        Assert.Equal("A clean workflow diagram", request.Prompt);
        Assert.Empty(request.Sources);
    }

    [Fact]
    public async Task ImageGenerationWorkflowExecutor_RejectsEditWithoutSourceContract()
    {
        using var temp = new TempDirectory();
        var provider = CreateProviderProfile("gpt-image-1-mini") with
        {
            Purpose = ProviderProfilePurpose.ImageGeneration,
            DefaultModel = "gpt-image-1-mini"
        };
        var executor = new ImageGenerationWorkflowExecutor(
            new TestProviderProfileRegistry([provider]),
            new RecordingImageGenerationService([1, 2, 3]),
            new WorkspacePathResolutionService(temp.Path));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteDirectAsync(executor, new WorkflowImageGenerationExecutorSettings
        {
            Operation = WorkflowImageGenerationOperation.Edit,
            Prompt = "Edit a clean workflow diagram",
            OutputWorkspacePath = "output/workflow-image.png"
        }));
        Assert.Contains("source-image settings", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuiltInDescriptorsExposeSourceAvailabilityAndSchemaMetadata()
    {
        var descriptor = BuiltInWorkflowExecutorDescriptors.StorageFile;
        var planned = BuiltInWorkflowExecutorDescriptors.Planned[0];

        Assert.Equal(WorkflowExecutorSourceKind.BuiltIn, descriptor.Source.Kind);
        Assert.Equal(WorkflowExecutorSourceIds.BuiltIn, descriptor.Source.SourceId);
        Assert.Equal(WorkflowExecutorTrustLevel.Application, descriptor.Source.TrustLevel);
        Assert.Equal(WorkflowExecutorAvailabilityKind.Available, descriptor.Availability.Kind);
        Assert.True(descriptor.CanExecute);
        Assert.Equal(WorkflowExecutorSettingsSchemaKind.JsonSchema, descriptor.SettingsSchema.Kind);
        Assert.Equal(descriptor.SettingsSchemaJson, descriptor.SettingsSchema.SchemaJson);
        Assert.True(descriptor.PermissionPolicy.RequiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.ReadsWorkspace));
        Assert.True(descriptor.DeterministicTestMode.IsSupported);

        Assert.False(planned.CanExecute);
        Assert.False(planned.IsImplemented);
        Assert.Equal(WorkflowExecutorAvailabilityKind.Planned, planned.Availability.Kind);
        Assert.False(planned.Availability.IsRunnable);
    }

    [Fact]
    public void WorkflowExecutorDescriptorDeserializesLegacyJsonWithDefaultMetadata()
    {
        const string legacyJson = """
            {
              "id": "storage.file",
              "name": "Workspace files",
              "description": "Legacy descriptor",
              "category": "Storage",
              "iconName": "folder_open",
              "setupRendererKey": "builtin.storage-file",
              "inputShape": {
                "kind": "Text",
                "schemaJson": "",
                "description": "Plain text"
              },
              "resultShape": {
                "kind": "Json",
                "schemaJson": "{}",
                "description": "JSON payload"
              },
              "settingsSchemaJson": "{\"type\":\"object\"}",
              "defaultSettingsJson": "{}",
              "defaultPolicy": {
                "timeoutSeconds": 30,
                "maxRetryAttempts": 0,
                "retryDelayMilliseconds": 250,
                "captureOutputArtifact": false
              },
              "isImplemented": true
            }
            """;

        var descriptor = System.Text.Json.JsonSerializer.Deserialize<WorkflowExecutorDescriptor>(
            legacyJson,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)
            {
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            });

        Assert.NotNull(descriptor);
        Assert.True(descriptor.CanExecute);
        Assert.Equal(WorkflowExecutorSourceKind.BuiltIn, descriptor.Source.Kind);
        Assert.Equal(WorkflowExecutorAvailabilityKind.Available, descriptor.Availability.Kind);
        Assert.Equal(WorkflowExecutorSettingsSchemaKind.JsonSchema, descriptor.SettingsSchema.Kind);
        Assert.Equal("{\"type\":\"object\"}", descriptor.SettingsSchema.SchemaJson);
        Assert.Equal(WorkflowExecutorPermissionPolicy.None, descriptor.PermissionPolicy);
        Assert.Equal(WorkflowExecutorSideEffectDescriptor.None, descriptor.SideEffects);
        Assert.False(descriptor.DeterministicTestMode.IsSupported);
    }

    [Fact]
    public void ValidatorRejectsPlannedExecutorNode()
    {
        var plannedExecutor = new PlannedWorkflowExecutor(BuiltInWorkflowExecutorDescriptors.Planned[0]);
        var catalog = new WorkflowExecutorCatalog([plannedExecutor]);
        var validator = new WorkflowDefinitionValidator(catalog);
        var definition = CreateDefinition(
        [
            CreateNode("start", WorkflowNodeKind.Start),
            CreateExecutorNode("tool", plannedExecutor.Descriptor.Id),
            CreateNode("end", WorkflowNodeKind.End)
        ], [
            CreateEdge("start-tool", "start", "tool"),
            CreateEdge("tool-end", "tool", "end")
        ]);

        var result = validator.Validate(definition, []);

        Assert.Contains(result.Issues, issue =>
            issue.Code == WorkflowValidationIssueCode.InvalidExecutorReference &&
            issue.Message.Contains("not runnable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task InvokerRejectsPlannedExecutorBeforeCallingImplementation()
    {
        var plannedExecutor = new PlannedWorkflowExecutor(BuiltInWorkflowExecutorDescriptors.Planned[0]);
        var catalog = new WorkflowExecutorCatalog([plannedExecutor]);
        var invoker = new WorkflowExecutorInvoker(catalog, [plannedExecutor]);
        var node = CreateExecutorNode("tool", plannedExecutor.Descriptor.Id);

        var exception = await Assert.ThrowsAsync<WorkflowExecutorUnavailableException>(() => invoker.ExecuteAsync(
            CreateDefinition([node], [], "tool"),
            node,
            new WorkflowNodeInput("{}")).AsTask());

        Assert.Equal(plannedExecutor.Descriptor.Id, exception.ExecutorId);
        Assert.Equal(WorkflowExecutorAvailabilityKind.Planned, exception.Availability.Kind);
    }

    [Fact]
    public void InvokerRejectsDuplicateExecutorImplementations()
    {
        var first = new RecordingWorkflowExecutor();
        var second = new RecordingWorkflowExecutor();
        var catalog = new WorkflowExecutorCatalog([first]);

        var exception = Assert.Throws<InvalidOperationException>(() => new WorkflowExecutorInvoker(catalog, [first, second]));

        Assert.Contains(WorkflowExecutorIds.StorageFile.Value, exception.Message, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains(result.Events, workflowEvent =>
            workflowEvent.Kind == WorkflowEventKind.ExecutorInvoked &&
            workflowEvent.NodeId == new WorkflowNodeId("tool"));
        Assert.Contains(result.Events, workflowEvent =>
            workflowEvent.Kind == WorkflowEventKind.ExecutorCompleted &&
            workflowEvent.NodeId == new WorkflowNodeId("tool"));
    }

    [Fact]
    public async Task MafBackendRecordsFailedExecutorEventWithoutAmbiguousDataReflection()
    {
        var executor = new RecordingWorkflowExecutor { FailuresBeforeSuccess = 1 };
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

        Assert.Equal(WorkflowRunState.Failed, result.Run.State);
        Assert.Contains(result.Events, workflowEvent => workflowEvent.Kind is WorkflowEventKind.ExecutorFailed or WorkflowEventKind.Error);
        Assert.DoesNotContain("Ambiguous match", result.Run.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MafBackendRecordsConfiguredFileArtifactsForCompletedFileWrites()
    {
        var executor = new RecordingWorkflowExecutor();
        var catalog = new WorkflowExecutorCatalog([executor]);
        var invoker = new WorkflowExecutorInvoker(catalog, [executor]);
        var compiler = new MafWorkflowCompiler(new WorkflowDefinitionValidator(catalog), invoker);
        var backend = new MafInProcessWorkflowExecutionBackend(compiler, []);
        var filePath = "reports/generated-summary.md";
        var definition = CreateDefinition(
        [
            CreateExecutorNode("write-summary", WorkflowExecutorIds.StorageFile) with
            {
                Settings = CreateSettings(WorkflowExecutorIds.StorageFile) with
                {
                    ExecutorSettingsJson = System.Text.Json.JsonSerializer.Serialize(
                        new WorkflowStorageFileExecutorSettings
                        {
                            Operation = WorkflowStorageFileOperation.WriteText,
                            Path = filePath,
                            Content = "Generated procurement summary."
                        },
                        new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))
                }
            },
            CreateNode("end", WorkflowNodeKind.End)
        ], [
            CreateEdge("write-end", "write-summary", "end")
        ],
        startNodeId: "write-summary") with
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

        var artifact = Assert.Single(result.Artifacts, artifact => artifact.Kind == WorkflowArtifactKind.File);
        Assert.Equal(WorkflowRunState.Completed, result.Run.State);
        Assert.Equal(WorkflowArtifactKind.File, artifact.Kind);
        Assert.Equal(new WorkflowNodeId("write-summary"), artifact.NodeId);
        Assert.Equal(filePath, artifact.StoragePath);
        Assert.Equal("generated-summary.md", artifact.Name);
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
    public async Task MafCompilerSkipsPredicateFalseBranch()
    {
        var executor = new BranchRecordingWorkflowExecutor();
        var catalog = new WorkflowExecutorCatalog([executor]);
        var invoker = new WorkflowExecutorInvoker(catalog, [executor]);
        var compiler = new MafWorkflowCompiler(new WorkflowDefinitionValidator(catalog), invoker);
        var backend = new MafInProcessWorkflowExecutionBackend(compiler, []);
        var definition = CreateDefinition(
        [
            CreateNode("start", WorkflowNodeKind.Start),
            CreateExecutorNode("spam", WorkflowExecutorIds.StorageFile),
            CreateExecutorNode("normal", WorkflowExecutorIds.StorageFile),
            CreateNode("end", WorkflowNodeKind.End)
        ], [
            CreateEdge(
                "start-spam",
                "start",
                "spam",
                WorkflowEdgeKind.Conditional,
                WorkflowEdgeRouting.Predicate(
                    "$.classification",
                    WorkflowRouteOperator.Equals,
                    "\"spam\"",
                    WorkflowRouteValueKind.String,
                    label: "spam")),
            CreateEdge(
                "start-normal",
                "start",
                "normal",
                WorkflowEdgeKind.Conditional,
                WorkflowEdgeRouting.Predicate(
                    "$.classification",
                    WorkflowRouteOperator.NotEquals,
                    "\"spam\"",
                    WorkflowRouteValueKind.String,
                    label: "not spam")),
            CreateEdge("spam-end", "spam", "end"),
            CreateEdge("normal-end", "normal", "end")
        ]);

        var result = await backend.StartAsync(
            definition,
            new WorkflowRunStartRequest(
                definition.Id,
                definition.VersionId,
                "{\"classification\":\"spam\"}",
                WorkflowRuntimeBackendKind.InProcess,
                SourceProcessRunId: null,
                SourceProcessAssignmentId: null),
            WorkflowRunId.New());

        Assert.Equal(WorkflowRunState.Completed, result.Run.State);
        Assert.Equal(1, executor.InvocationCountFor("spam"));
        Assert.Equal(0, executor.InvocationCountFor("normal"));
    }

    [Fact]
    public async Task MafCompilerUsesSwitchDefaultWhenNoCaseMatches()
    {
        var executor = new BranchRecordingWorkflowExecutor
        {
            OutputsByNode =
            {
                ["classify"] = "{\"decision\":\"needsHuman\"}"
            }
        };
        var catalog = new WorkflowExecutorCatalog([executor]);
        var invoker = new WorkflowExecutorInvoker(catalog, [executor]);
        var compiler = new MafWorkflowCompiler(new WorkflowDefinitionValidator(catalog), invoker);
        var backend = new MafInProcessWorkflowExecutionBackend(compiler, []);
        var definition = CreateDefinition(
        [
            CreateNode("start", WorkflowNodeKind.Start),
            CreateExecutorNode("classify", WorkflowExecutorIds.StorageFile),
            CreateExecutorNode("approved", WorkflowExecutorIds.StorageFile, JsonObjectShape),
            CreateExecutorNode("rework", WorkflowExecutorIds.StorageFile, JsonObjectShape),
            CreateExecutorNode("manual", WorkflowExecutorIds.StorageFile, JsonObjectShape),
            CreateNode("end", WorkflowNodeKind.End)
        ], [
            CreateEdge("start-classify", "start", "classify"),
            CreateEdge(
                "classify-approved",
                "classify",
                "approved",
                WorkflowEdgeKind.Conditional,
                WorkflowEdgeRouting.SwitchCase("$.decision", "\"approved\"", WorkflowRouteValueKind.String, "approved")),
            CreateEdge(
                "classify-rework",
                "classify",
                "rework",
                WorkflowEdgeKind.Conditional,
                WorkflowEdgeRouting.SwitchCase("$.decision", "\"rework\"", WorkflowRouteValueKind.String, "rework")),
            CreateEdge(
                "classify-manual",
                "classify",
                "manual",
                WorkflowEdgeKind.Conditional,
                WorkflowEdgeRouting.SwitchDefault("default manual review")),
            CreateEdge("approved-end", "approved", "end"),
            CreateEdge("rework-end", "rework", "end"),
            CreateEdge("manual-end", "manual", "end")
        ]);

        var result = await backend.StartAsync(
            definition,
            new WorkflowRunStartRequest(
                definition.Id,
                definition.VersionId,
                "{\"ticket\":\"A-100\"}",
                WorkflowRuntimeBackendKind.InProcess,
                SourceProcessRunId: null,
                SourceProcessAssignmentId: null),
            WorkflowRunId.New());

        Assert.Equal(WorkflowRunState.Completed, result.Run.State);
        Assert.Equal(1, executor.InvocationCountFor("classify"));
        Assert.Equal(0, executor.InvocationCountFor("approved"));
        Assert.Equal(0, executor.InvocationCountFor("rework"));
        Assert.Equal(1, executor.InvocationCountFor("manual"));
    }

    [Fact]
    public async Task MafCompilerFanOutRoutesOnlySelectedTargets()
    {
        var executor = new BranchRecordingWorkflowExecutor
        {
            OutputsByNode =
            {
                ["select-channels"] = "{\"channels\":[\"email\",\"slack\"]}"
            }
        };
        var catalog = new WorkflowExecutorCatalog([executor]);
        var invoker = new WorkflowExecutorInvoker(catalog, [executor]);
        var compiler = new MafWorkflowCompiler(new WorkflowDefinitionValidator(catalog), invoker);
        var backend = new MafInProcessWorkflowExecutionBackend(compiler, []);
        var definition = CreateDefinition(
        [
            CreateNode("start", WorkflowNodeKind.Start),
            CreateExecutorNode("select-channels", WorkflowExecutorIds.StorageFile),
            CreateExecutorNode("email", WorkflowExecutorIds.StorageFile, JsonObjectShape),
            CreateExecutorNode("slack", WorkflowExecutorIds.StorageFile, JsonObjectShape),
            CreateExecutorNode("ticket", WorkflowExecutorIds.StorageFile, JsonObjectShape),
            CreateNode("end", WorkflowNodeKind.End)
        ], [
            CreateEdge("start-select", "start", "select-channels"),
            CreateEdge(
                "select-email",
                "select-channels",
                "email",
                WorkflowEdgeKind.FanOut,
                WorkflowEdgeRouting.FanOutSelector(
                    "$.channels",
                    WorkflowRouteOperator.Contains,
                    "\"email\"",
                    WorkflowRouteValueKind.String,
                    targetIndex: 0,
                    label: "email")),
            CreateEdge(
                "select-slack",
                "select-channels",
                "slack",
                WorkflowEdgeKind.FanOut,
                WorkflowEdgeRouting.FanOutSelector(
                    "$.channels",
                    WorkflowRouteOperator.Contains,
                    "\"slack\"",
                    WorkflowRouteValueKind.String,
                    targetIndex: 1,
                    label: "slack")),
            CreateEdge(
                "select-ticket",
                "select-channels",
                "ticket",
                WorkflowEdgeKind.FanOut,
                WorkflowEdgeRouting.FanOutSelector(
                    "$.channels",
                    WorkflowRouteOperator.Contains,
                    "\"ticket\"",
                    WorkflowRouteValueKind.String,
                    targetIndex: 2,
                    label: "ticket")),
            CreateEdge("email-end", "email", "end"),
            CreateEdge("slack-end", "slack", "end"),
            CreateEdge("ticket-end", "ticket", "end")
        ]);

        var result = await backend.StartAsync(
            definition,
            new WorkflowRunStartRequest(
                definition.Id,
                definition.VersionId,
                "{\"case\":\"route updates\"}",
                WorkflowRuntimeBackendKind.InProcess,
                SourceProcessRunId: null,
                SourceProcessAssignmentId: null),
            WorkflowRunId.New());

        Assert.Equal(WorkflowRunState.Completed, result.Run.State);
        Assert.Equal(1, executor.InvocationCountFor("email"));
        Assert.Equal(1, executor.InvocationCountFor("slack"));
        Assert.Equal(0, executor.InvocationCountFor("ticket"));
    }

    [Fact]
    public void BuiltInRoutingScenarioMatrixCoversRealWorldExamples()
    {
        var compiler = new BuiltInJsonWorkflowRoutingCompiler();
        var scenarios = new[]
        {
            CreateRouteScenario("invoice over approval threshold", "{\"invoice\":{\"amount\":1250}}", "$.invoice.amount", WorkflowRouteOperator.GreaterThan, "1000", WorkflowRouteValueKind.Number, true),
            CreateRouteScenario("small invoice auto approval", "{\"invoice\":{\"amount\":250}}", "$.invoice.amount", WorkflowRouteOperator.LessThanOrEqual, "500", WorkflowRouteValueKind.Number, true),
            CreateRouteScenario("enterprise customer switch case", "{\"customer\":{\"tier\":\"enterprise\"}}", "$.customer.tier", WorkflowRouteOperator.Equals, "\"enterprise\"", WorkflowRouteValueKind.String, true),
            CreateRouteScenario("support ticket urgent priority", "{\"ticket\":{\"priority\":\"Urgent\"}}", "$.ticket.priority", WorkflowRouteOperator.Equals, "\"urgent\"", WorkflowRouteValueKind.String, true),
            CreateRouteScenario("fraud risk above review score", "{\"risk\":{\"score\":0.92}}", "$.risk.score", WorkflowRouteOperator.GreaterThanOrEqual, "0.85", WorkflowRouteValueKind.Number, true),
            CreateRouteScenario("inventory does not need restock", "{\"stock\":{\"onHand\":42}}", "$.stock.onHand", WorkflowRouteOperator.LessThan, "10", WorkflowRouteValueKind.Number, false),
            CreateRouteScenario("email notification selected", "{\"channels\":[\"email\",\"slack\"]}", "$.channels", WorkflowRouteOperator.Contains, "\"email\"", WorkflowRouteValueKind.String, true),
            CreateRouteScenario("sms notification not selected", "{\"channels\":[\"email\",\"slack\"]}", "$.channels", WorkflowRouteOperator.Contains, "\"sms\"", WorkflowRouteValueKind.String, false),
            CreateRouteScenario("incident starts with sev prefix", "{\"incident\":{\"severity\":\"sev-1\"}}", "$.incident.severity", WorkflowRouteOperator.StartsWith, "\"sev-\"", WorkflowRouteValueKind.String, true),
            CreateRouteScenario("document ends with pdf extension", "{\"file\":{\"name\":\"contract.pdf\"}}", "$.file.name", WorkflowRouteOperator.EndsWith, "\".pdf\"", WorkflowRouteValueKind.String, true),
            CreateRouteScenario("customer note contains renewal", "{\"note\":\"Renewal requested by account owner\"}", "$.note", WorkflowRouteOperator.Contains, "\"renewal\"", WorkflowRouteValueKind.String, true),
            CreateRouteScenario("missing approval reason", "{\"approval\":{\"status\":\"approved\"}}", "$.approval.reason", WorkflowRouteOperator.DoesNotExist, "", WorkflowRouteValueKind.Json, true),
            CreateRouteScenario("approval flag truthy", "{\"approval\":{\"approved\":true}}", "$.approval.approved", WorkflowRouteOperator.IsTruthy, "", WorkflowRouteValueKind.Json, true),
            CreateRouteScenario("archive flag falsy", "{\"archive\":false}", "$.archive", WorkflowRouteOperator.IsFalsy, "", WorkflowRouteValueKind.Json, true),
            CreateRouteScenario("region is not blocked", "{\"region\":\"emea\"}", "$.region", WorkflowRouteOperator.NotEquals, "\"blocked\"", WorkflowRouteValueKind.String, true),
            CreateRouteScenario("first line item sku match", "{\"items\":[{\"sku\":\"A1\"}]}", "$.items[0].sku", WorkflowRouteOperator.Equals, "\"A1\"", WorkflowRouteValueKind.String, true),
            CreateRouteScenario("contract expiration is present", "{\"contract\":{\"expiresOn\":\"2026-12-31\"}}", "$.contract.expiresOn", WorkflowRouteOperator.Exists, "", WorkflowRouteValueKind.Json, true),
            CreateRouteScenario("nullable manager assignment", "{\"manager\":null}", "$.manager", WorkflowRouteOperator.Equals, "null", WorkflowRouteValueKind.Null, true),
            CreateRouteScenario("lead score is below sales handoff", "{\"lead\":{\"score\":61}}", "$.lead.score", WorkflowRouteOperator.LessThan, "75", WorkflowRouteValueKind.Number, true),
            CreateRouteScenario("sentiment avoids negative path", "{\"sentiment\":\"neutral\"}", "$.sentiment", WorkflowRouteOperator.NotEquals, "\"negative\"", WorkflowRouteValueKind.String, true)
        };
        var definition = CreateDefinition([CreateNode("start", WorkflowNodeKind.Start), CreateNode("end", WorkflowNodeKind.End)], [
            CreateEdge("start-end", "start", "end")
        ]);
        var passed = new List<string>();

        foreach (var scenario in scenarios)
        {
            var edge = CreateEdge(
                scenario.Name,
                "start",
                "end",
                WorkflowEdgeKind.Conditional,
                WorkflowEdgeRouting.Predicate(
                    scenario.JsonPath,
                    scenario.Operator,
                    scenario.ExpectedValueJson,
                    scenario.ExpectedValueKind,
                    scenario.Name));
            var route = compiler.CompilePredicate(definition, edge);

            Assert.Equal(scenario.Expected, route.Predicate(new WorkflowNodeInput(scenario.PayloadJson)));
            passed.Add(scenario.Name);
        }

        Assert.True(passed.Count >= 20);
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
    public async Task MafWorkflowLlmComponentInvokerPassesProjectScopeFromWorkflowPayload()
    {
        var projectId = Guid.Parse("ad8e7db7-4041-4fd7-a5f7-b5c6756f9a1f");
        var runtime = new CapturingAgentRuntime(
            $$"""
            {
              "markdown": "# Tetris request summary\n\nKeyboard-controlled static-web Tetris within one week.",
              "projectId": "{{projectId:D}}",
              "nodeId": "workflow-node-1"
            }
            """);
        var provider = CreateProviderProfile("gpt-5-mini");
        var invoker = new MafWorkflowLlmComponentInvoker(
            runtime,
            new TestProviderProfileRegistry([provider]),
            new ProviderProfileService());
        var component = CreateLlmComponent(JsonObjectShape, JsonObjectShape);
        var node = CreateLlmNode("summarize-office365", component.Id);
        var definition = CreateDefinition([node], [], node.Id.Value);

        var result = await invoker.ExecuteAsync(
            definition,
            node,
            component,
            new WorkflowNodeInput($$"""
            {
              "projectId": "{{projectId:D}}",
              "nodeId": "workflow-node-1",
              "messages": [
                {
                  "subject": "Tetris",
                  "bodyText": "Potřebujeme naprogramovat jednoduchou hru Tetris."
                }
              ]
            }
            """));

        Assert.Contains("Tetris request summary", result.PayloadJson, StringComparison.Ordinal);
        var executionOptions = runtime.LastExecutionOptions;
        Assert.NotNull(executionOptions);
        var scope = executionOptions!.ContextWorkspaceScope;
        Assert.NotNull(scope);
        Assert.Equal(WorkspaceScopeKind.Project, scope!.Kind);
        Assert.Equal(projectId.ToString("D"), scope.Key);
        Assert.Contains("Potřebujeme naprogramovat jednoduchou hru Tetris", runtime.LastPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MafWorkflowLlmComponentInvokerUsesNodeInstructionSnapshot()
    {
        var runtime = new CapturingAgentRuntime("{\"ok\":true}");
        var provider = CreateProviderProfile("gpt-5-mini");
        var invoker = new MafWorkflowLlmComponentInvoker(
            runtime,
            new TestProviderProfileRegistry([provider]),
            new ProviderProfileService());
        var component = CreateLlmComponent(JsonObjectShape, JsonPayloadShape) with
        {
            Instructions = "A later Gallery edit must not affect this workflow version."
        };
        var node = CreateLlmNode("pinned-prompt-version", component.Id) with
        {
            Settings = CreateLlmNode("pinned-prompt-version", component.Id).Settings with
            {
                Instructions = "Pinned workflow prompt snapshot."
            }
        };
        var definition = CreateDefinition([node], [], node.Id.Value);

        await invoker.ExecuteAsync(
            definition,
            node,
            component,
            new WorkflowNodeInput("{}"));

        Assert.NotNull(runtime.LastAgent);
        Assert.Equal("Pinned workflow prompt snapshot.", runtime.LastAgent!.Instructions);
    }

    [Fact]
    public async Task MafWorkflowLlmComponentInvokerUsesNodeProviderModelAndInstructionOverrides()
    {
        var runtime = new CapturingAgentRuntime("{\"ok\":true}");
        var componentProvider = CreateProviderProfile("component-model");
        var nodeProvider = CreateProviderProfile("node-model");
        var invoker = new MafWorkflowLlmComponentInvoker(
            runtime,
            new TestProviderProfileRegistry([componentProvider, nodeProvider]),
            new ProviderProfileService());
        var component = CreateLlmComponent(JsonObjectShape, JsonPayloadShape) with
        {
            ProviderProfileId = componentProvider.Id,
            Model = componentProvider.DefaultModel,
            Instructions = "Component instructions must remain unchanged."
        };
        var node = CreateLlmNode("node-execution-overrides", component.Id) with
        {
            Settings = CreateLlmNode("node-execution-overrides", component.Id).Settings with
            {
                ProviderProfileId = nodeProvider.Id,
                Model = nodeProvider.DefaultModel,
                Instructions = "Pinned node instructions."
            }
        };
        var definition = CreateDefinition([node], [], node.Id.Value);

        await invoker.ExecuteAsync(
            definition,
            node,
            component,
            new WorkflowNodeInput("{}"));

        Assert.Equal(nodeProvider.Id, runtime.LastProvider!.Id);
        Assert.Equal(nodeProvider.DefaultModel, runtime.LastAgent!.Model);
        Assert.Equal("Pinned node instructions.", runtime.LastAgent.Instructions);
        Assert.Equal(componentProvider.Id, component.ProviderProfileId);
        Assert.Equal(componentProvider.DefaultModel, component.Model);
        Assert.Equal("Component instructions must remain unchanged.", component.Instructions);
    }

    [Fact]
    public async Task MafWorkflowLlmComponentInvokerRejectsBlankNodeInstructionSnapshot()
    {
        var runtime = new CapturingAgentRuntime("{\"ok\":true}");
        var provider = CreateProviderProfile("gpt-5-mini");
        var invoker = new MafWorkflowLlmComponentInvoker(
            runtime,
            new TestProviderProfileRegistry([provider]),
            new ProviderProfileService());
        var component = CreateLlmComponent(JsonObjectShape, JsonPayloadShape) with
        {
            Instructions = "Mutable component instructions must never be used as an execution fallback."
        };
        var node = CreateLlmNode("missing-prompt-snapshot", component.Id) with
        {
            Settings = CreateLlmNode("missing-prompt-snapshot", component.Id).Settings with
            {
                Instructions = "   "
            }
        };
        var definition = CreateDefinition([node], [], node.Id.Value);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await invoker.ExecuteAsync(
                definition,
                node,
                component,
                new WorkflowNodeInput("{}")));

        Assert.Contains("immutable instruction snapshot", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(runtime.LastAgent);
    }

    [Fact]
    public async Task MafWorkflowLlmComponentInvokerUsesProviderUsageObservationsForWorkflowUsage()
    {
        var runtime = new CapturingAgentRuntime("{\"ok\":true}")
        {
            InputTokens = 0,
            OutputTokens = 0,
            UsageObservations =
            [
                CreateWorkflowUsageObservation(
                    ProviderUsageObservationStatus.Observed,
                    inputTokens: 1_000_000,
                    cachedInputTokens: 250_000,
                    outputTokens: 500_000)
            ]
        };
        var provider = CreateProviderProfile("gpt-5-mini") with
        {
            ModelPrices = [new ProviderModelTokenPrice("gpt-5-mini", 1.00m, 0.10m, 4.00m)]
        };
        var invoker = new MafWorkflowLlmComponentInvoker(
            runtime,
            new TestProviderProfileRegistry([provider]),
            new ProviderProfileService());
        var component = CreateLlmComponent(JsonObjectShape, JsonPayloadShape);
        var node = CreateLlmNode("summarize-office365", component.Id);
        var definition = CreateDefinition([node], [], node.Id.Value);

        var result = await invoker.ExecuteAsync(
            definition,
            node,
            component,
            new WorkflowNodeInput("{}"));

        Assert.NotNull(result.Usage);
        Assert.Equal(1_000_000, result.Usage!.InputTokens);
        Assert.Equal(250_000, result.Usage.CachedInputTokens);
        Assert.Equal(500_000, result.Usage.OutputTokens);
        Assert.Equal(2.775m, result.Usage.CostUsd);
        Assert.Equal(1, result.Usage.KnownObservationCount);
        Assert.Equal(0, result.Usage.UnknownObservationCount);
    }

    [Fact]
    public async Task MafWorkflowLlmComponentInvokerMarksUnavailableWorkflowUsageAsUnknown()
    {
        var runtime = new CapturingAgentRuntime("{\"ok\":true}")
        {
            InputTokens = 0,
            OutputTokens = 0,
            UsageObservations =
            [
                CreateWorkflowUsageObservation(
                    ProviderUsageObservationStatus.UsageUnavailable,
                    inputTokens: 0,
                    cachedInputTokens: 0,
                    outputTokens: 0)
            ]
        };
        var provider = CreateProviderProfile("gpt-5-mini") with
        {
            ModelPrices = [new ProviderModelTokenPrice("gpt-5-mini", 1.00m, 0.10m, 4.00m)]
        };
        var invoker = new MafWorkflowLlmComponentInvoker(
            runtime,
            new TestProviderProfileRegistry([provider]),
            new ProviderProfileService());
        var component = CreateLlmComponent(JsonObjectShape, JsonPayloadShape);
        var node = CreateLlmNode("summarize-office365", component.Id);
        var definition = CreateDefinition([node], [], node.Id.Value);

        var result = await invoker.ExecuteAsync(
            definition,
            node,
            component,
            new WorkflowNodeInput("{}"));

        Assert.NotNull(result.Usage);
        Assert.Equal(0m, result.Usage!.CostUsd);
        Assert.Equal(0, result.Usage.KnownObservationCount);
        Assert.Equal(1, result.Usage.UnknownObservationCount);
        Assert.True(result.Usage.HasUnknownUsage);
    }

    [Fact]
    public async Task MafWorkflowLlmComponentInvokerRequestsJsonResponseFormatSchemaForJsonComponents()
    {
        const string schemaJson = """
            {
              "type": "object",
              "additionalProperties": true,
              "properties": {
                "markdown": { "type": "string" },
                "projectId": { "type": "string" },
                "nodeId": { "type": "string" }
              },
              "required": ["markdown", "projectId", "nodeId"]
            }
            """;
        var runtime = new CapturingAgentRuntime(
            """
            {
              "markdown": "# Summary",
              "projectId": "ad8e7db7-4041-4fd7-a5f7-b5c6756f9a1f",
              "nodeId": "workflow-node-1"
            }
            """);
        var provider = CreateProviderProfile("gpt-5-mini");
        var invoker = new MafWorkflowLlmComponentInvoker(
            runtime,
            new TestProviderProfileRegistry([provider]),
            new ProviderProfileService());
        var component = CreateLlmComponent(
            JsonObjectShape,
            JsonPayloadShape,
            responseFormatJsonSchema: schemaJson);
        var node = CreateLlmNode("summarize-office365", component.Id);
        var definition = CreateDefinition([node], [], node.Id.Value);

        await invoker.ExecuteAsync(
            definition,
            node,
            component,
            new WorkflowNodeInput("{}"));

        var executionOptions = runtime.LastExecutionOptions;
        Assert.NotNull(executionOptions);
        Assert.True(executionOptions!.RequireJsonResponseFormat);
        Assert.Equal(schemaJson.Trim(), executionOptions.ResponseFormatJsonSchema);
        Assert.Equal("workflow_llm_component_result", executionOptions.ResponseFormatSchemaName);
    }

    [Fact]
    public async Task MafWorkflowLlmComponentInvokerRequestsGenericJsonResponseFormatWhenSchemaIsMissing()
    {
        var runtime = new CapturingAgentRuntime("{\"ok\":true}");
        var provider = CreateProviderProfile("gpt-5-mini");
        var invoker = new MafWorkflowLlmComponentInvoker(
            runtime,
            new TestProviderProfileRegistry([provider]),
            new ProviderProfileService());
        var component = CreateLlmComponent(JsonObjectShape, JsonPayloadShape);
        var node = CreateLlmNode("summarize-office365", component.Id);
        var definition = CreateDefinition([node], [], node.Id.Value);

        await invoker.ExecuteAsync(
            definition,
            node,
            component,
            new WorkflowNodeInput("{}"));

        var executionOptions = runtime.LastExecutionOptions;
        Assert.NotNull(executionOptions);
        Assert.True(executionOptions!.RequireJsonResponseFormat);
        Assert.Equal(string.Empty, executionOptions.ResponseFormatJsonSchema);
    }

    [Fact]
    public async Task MafWorkflowLlmComponentInvokerRejectsInvalidJsonWithoutRepairingPayload()
    {
        var runtime = new CapturingAgentRuntime("{\"markdown\":\"ok\"} + invalid");
        var provider = CreateProviderProfile("gpt-5-mini");
        var invoker = new MafWorkflowLlmComponentInvoker(
            runtime,
            new TestProviderProfileRegistry([provider]),
            new ProviderProfileService());
        var component = CreateLlmComponent(JsonObjectShape, JsonPayloadShape);
        var node = CreateLlmNode("summarize-office365", component.Id);
        var definition = CreateDefinition([node], [], node.Id.Value);

        var exception = await Assert.ThrowsAsync<WorkflowUsageObservationException>(() => invoker.ExecuteAsync(
            definition,
            node,
            component,
            new WorkflowNodeInput("{}")).AsTask());

        Assert.Contains("summarize-office365", exception.Message, StringComparison.Ordinal);
        Assert.Contains("invalid JSON", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        var executionOptions = runtime.LastExecutionOptions;
        Assert.NotNull(executionOptions);
        Assert.True(executionOptions!.RequireJsonResponseFormat);
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
    public async Task WorkspaceFileExecutorSupportsDirectoryHashZipAndDryRunDelete()
    {
        using var temp = new TempDirectory();
        var service = new WorkspaceFileService(temp.Path);
        var executor = new WorkspaceFileWorkflowExecutor(service);

        await ExecuteDirectAsync(executor, new WorkflowStorageFileExecutorSettings
        {
            Operation = WorkflowStorageFileOperation.CreateDirectory,
            Path = "reports"
        });
        await ExecuteDirectAsync(executor, new WorkflowStorageFileExecutorSettings
        {
            Operation = WorkflowStorageFileOperation.WriteText,
            Path = "reports/a.md",
            Content = "alpha"
        });
        var hash = await ExecuteDirectAsync(executor, new WorkflowStorageFileExecutorSettings
        {
            Operation = WorkflowStorageFileOperation.Hash,
            Path = "reports"
        });
        var zip = await ExecuteDirectAsync(executor, new WorkflowStorageFileExecutorSettings
        {
            Operation = WorkflowStorageFileOperation.Zip,
            Path = "reports",
            DestinationPath = "archive/reports.zip"
        });
        var dryRun = await ExecuteDirectAsync(executor, new WorkflowStorageFileExecutorSettings
        {
            Operation = WorkflowStorageFileOperation.Delete,
            Path = "reports",
            Recursive = true,
            DryRun = true
        });

        Assert.Contains("sha-256", hash.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reports.zip", zip.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"dryRun\":true", dryRun.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.True(Directory.Exists(Path.Combine(temp.Path, "reports")));
    }

    [Fact]
    public async Task JsonTransformExecutorShapesArraysAndRejectsInvalidPaths()
    {
        var executor = new JsonTransformWorkflowExecutor();
        var result = await ExecuteDirectAsync(
            executor,
            new WorkflowJsonTransformExecutorSettings
            {
                Operations =
                [
                    new WorkflowJsonTransformStep
                    {
                        Operation = WorkflowJsonTransformOperation.ArrayFilter,
                        Path = "$.items",
                        DestinationPath = "$.openItems",
                        PredicatePath = "$.status",
                        ExpectedValueJson = "\"open\""
                    },
                    new WorkflowJsonTransformStep
                    {
                        Operation = WorkflowJsonTransformOperation.Count,
                        Path = "$.openItems",
                        DestinationPath = "$.openCount"
                    }
                ]
            },
            """{"items":[{"title":"A","status":"open"},{"title":"B","status":"done"}]}""");

        Assert.Contains("\"openCount\":1", result.PayloadJson, StringComparison.Ordinal);
        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteDirectAsync(
            executor,
            new WorkflowJsonTransformExecutorSettings
            {
                Operations =
                [
                    new WorkflowJsonTransformStep
                    {
                        Operation = WorkflowJsonTransformOperation.Select,
                        Path = "$.missing"
                    }
                ]
            },
            "{}"));
    }

    [Fact]
    public async Task MarkdownRenderExecutorRendersTablesAndWritesOutputFile()
    {
        using var temp = new TempDirectory();
        var files = new WorkspaceFileService(temp.Path);
        var executor = new MarkdownRenderWorkflowExecutor(files);

        var result = await ExecuteDirectAsync(
            executor,
            new WorkflowMarkdownRenderExecutorSettings
            {
                Template = "# {{title}}\n\n{{itemsTable}}",
                Bindings = new Dictionary<string, string>
                {
                    ["title"] = "$.title"
                },
                Tables =
                [
                    new WorkflowMarkdownTableBinding
                    {
                        Placeholder = "itemsTable",
                        JsonPath = "$.items",
                        Columns = ["name", "status"]
                    }
                ],
                OutputPath = "reports/report.md"
            },
            """{"title":"Run report","items":[{"name":"A","status":"open"}]}""");

        Assert.Contains("| name | status |", result.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("Run report", File.ReadAllText(Path.Combine(temp.Path, "reports", "report.md")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DelayAndApprovalExecutorsUseBoundedRuntimeSemantics()
    {
        var delay = new DelayWorkflowExecutor();
        var delayResult = await ExecuteDirectAsync(delay, new WorkflowDelayExecutorSettings
        {
            DelayMilliseconds = 1,
            MaxDelayMilliseconds = 10
        });
        Assert.Contains("\"durableScheduling\":false", delayResult.PayloadJson, StringComparison.OrdinalIgnoreCase);
        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteDirectAsync(delay, new WorkflowDelayExecutorSettings
        {
            DelayMilliseconds = 50,
            MaxDelayMilliseconds = 10
        }));

        var approval = new HumanApprovalWorkflowExecutor();
        var approvalContext = CreateExecutionContext(approval.Descriptor, new WorkflowApprovalExecutorSettings
        {
            Prompt = "Approve release?"
        }) with
        {
            RunId = WorkflowRunId.New()
        };
        var exception = await Assert.ThrowsAsync<WorkflowExternalRequestPendingException>(() => approval.ExecuteAsync(
            approvalContext,
            new WorkflowNodeInput("{\"release\":\"v1\"}")).AsTask());
        Assert.Equal(WorkflowExternalRequestKind.Approval, exception.Request.Kind);
        Assert.Equal(approvalContext.Node.Id, exception.Request.NodeId);
    }

    [Fact]
    public async Task HttpFetchBlocksPrivateNetworkTargetsByDefault()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteDirectAsync(new HttpFetchWorkflowExecutor(), new WorkflowHttpExecutorSettings
        {
            Url = "http://127.0.0.1:12345"
        }));
    }

    [Fact]
    public async Task HttpFetchDownloadsToWorkspaceAndSourceIngestionReadsOutputPath()
    {
        using var temp = new TempDirectory();
        var files = new WorkspaceFileService(temp.Path);
        await using var server = SingleResponseHttpServer.Html(200, "<html><body><h1>Download evidence</h1></body></html>");
        var httpResult = await ExecuteDirectAsync(new HttpFetchWorkflowExecutor(files: files), new WorkflowHttpExecutorSettings
        {
            Url = server.Url,
            AllowPrivateNetworkTargets = true,
            DownloadToWorkspace = true,
            OutputPath = "downloads/source.html"
        });

        Assert.True(File.Exists(Path.Combine(temp.Path, "downloads", "source.html")));
        Assert.Contains("\"outputPath\":\"downloads/source.html\"", httpResult.PayloadJson, StringComparison.Ordinal);

        var ingestion = await ExecuteDirectAsync(
            new SourceIngestionWorkflowExecutor(
                new WorkspacePathResolutionService(temp.Path),
                new ManagedCodeMarkItDownDocumentMarkdownConverter()),
            new WorkflowSourceIngestionExecutorSettings
            {
                IncludeAdditionalSources = true,
                IncludeParentNodePath = false,
                IncludeSelectedNodePaths = false,
                IncludeParentSubtreePaths = false,
                AllowedExtensions = [".html"],
                MaxFiles = 1
            },
            httpResult.PayloadJson);

        Assert.Contains("Download evidence", ingestion.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("markitdown-html", ingestion.PayloadJson, StringComparison.Ordinal);
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
            [new SpreadsheetCellWrite("A1", "Name"), new SpreadsheetCellWrite("B1", "Value"), new SpreadsheetCellWrite("C1", "Formula")],
            [new SpreadsheetRangeWrite("A2:B3", [["Alpha", "10"], ["Beta", "20"]])],
            CreateWorkbookIfMissing: true,
            Overwrite: true));
        service.Write(new SpreadsheetWriteRequest(
            workbookPath,
            workbookPath,
            "Data",
            [new SpreadsheetCellWrite("C2", "=SUM(B2:B3)")],
            [],
            CreateWorkbookIfMissing: false,
            Overwrite: true));

        var cell = service.ReadCell(workbookPath, "Data", "A2");
        var formula = service.ReadCell(workbookPath, "Data", "C2");
        var range = service.ReadRange(workbookPath, "Data", "A1:C3", maxRows: 10, maxColumns: 10);
        var functions = SpreadsheetFunctionCatalog.List(query: "sum", category: null, maxResults: 10);

        Assert.Equal("Alpha", cell.Value);
        Assert.Equal("=SUM(B2:B3)", formula.Value);
        Assert.Contains("| Name | Value | Formula |", range.MarkdownTable, StringComparison.Ordinal);
        Assert.Contains(functions, function => string.Equals(function.Name, "SUMIFS", StringComparison.Ordinal));
    }

    [Fact]
    public void WorkspaceSpreadsheetRuntimePluginPersistsToolReceipts()
    {
        using var temp = new TempDirectory();
        var run = CreateExecutionRunRecord();
        var plugin = new WorkspaceSpreadsheetRuntimePlugin(
            new ClosedXmlSpreadsheetDocumentService(),
            temp.Path,
            WorkspaceScopeDescriptor.Sandbox,
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true,
                CanWriteFiles = true,
                CanTransformArtifacts = true
            });

        using (WorkspaceExecutionAuditContext.BeginScope(run))
        {
            plugin.WriteSpreadsheetWorkbook(
                "margin.xlsx",
                "Summary",
                cellWrites:
                [
                    new SpreadsheetCellWrite("A1", "Metric"),
                    new SpreadsheetCellWrite("B1", "Value"),
                    new SpreadsheetCellWrite("A2", "Total"),
                    new SpreadsheetCellWrite("B2", "=SUM(1,2)")
                ],
                createWorkbookIfMissing: true,
                overwrite: true);
            plugin.InspectWorkbook("margin.xlsx");
            plugin.ReadSpreadsheetCell("margin.xlsx", "Summary", "B2");
            plugin.ReadSpreadsheetRange("margin.xlsx", "Summary", "A1:B2");
            plugin.ListSpreadsheetFunctions("sum", maxResults: 5);
        }

        var receiptRoot = Path.Combine(
            temp.Path,
            "data",
            "execution",
            "runs",
            run.Id.ToString("N"),
            "audit",
            "receipts");
        var receipts = Directory.EnumerateFiles(receiptRoot, "*.json")
            .Select(path => System.Text.Json.JsonSerializer.Deserialize<ToolExecutionReceiptRecord>(
                File.ReadAllText(path),
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)))
            .OfType<ToolExecutionReceiptRecord>()
            .ToArray();

        Assert.Contains(receipts, receipt => receipt.ToolName == "workspace_write_spreadsheet" &&
                                             receipt.RiskClass == "MutatingWorkspace");
        Assert.Contains(receipts, receipt => receipt.ToolName == "workspace_spreadsheet_summary" &&
                                             receipt.RiskClass == "ReadOnlyWorkspace");
        Assert.Contains(receipts, receipt => receipt.ToolName == "workspace_read_spreadsheet_cell");
        Assert.Contains(receipts, receipt => receipt.ToolName == "workspace_read_spreadsheet_range");
        Assert.Contains(receipts, receipt => receipt.ToolName == "workspace_spreadsheet_function_catalog");
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
                Url = server.Url,
                AllowPrivateNetworkTargets = true
            });

            Assert.Contains("ok", result.PayloadJson, StringComparison.Ordinal);
        });

        await RecordAsync("http gets url from workflow input and carries payload", async () =>
        {
            await using var server = SingleResponseHttpServer.Json(200, "{\"ok\":true}");
            var result = await ExecuteDirectAsync(
                new HttpFetchWorkflowExecutor(),
                new WorkflowHttpExecutorSettings
                {
                    Method = WorkflowHttpMethodKind.Get,
                    UrlJsonPath = "$.source.url",
                    IncludeInputPayload = true,
                    AllowPrivateNetworkTargets = true
                },
                $$"""{"source":{"url":"{{server.Url}}"},"projectId":"11111111-1111-1111-1111-111111111111"}""");

            Assert.Contains("ok", result.PayloadJson, StringComparison.Ordinal);
            Assert.Contains("11111111-1111-1111-1111-111111111111", result.PayloadJson, StringComparison.Ordinal);
        });

        await RecordAsync("http posts bounded payload", async () =>
        {
            await using var server = SingleResponseHttpServer.Json(201, "{\"created\":true}");
            var result = await ExecuteDirectAsync(new HttpFetchWorkflowExecutor(), new WorkflowHttpExecutorSettings
            {
                Method = WorkflowHttpMethodKind.Post,
                Url = server.Url,
                Body = "{\"name\":\"report\"}",
                AllowPrivateNetworkTargets = true
            });

            Assert.Contains("201", result.PayloadJson, StringComparison.Ordinal);
        });

        await RecordAsync("http applies secret header only to request", async () =>
        {
            var secretId = Guid.NewGuid();
            await using var server = SingleResponseHttpServer.Json(200, "{\"ok\":true}");
            var result = await ExecuteDirectAsync(
                new HttpFetchWorkflowExecutor(new StaticSecretRuntimeResolver(secretId, "secret-token")),
                new WorkflowHttpExecutorSettings
                {
                    Method = WorkflowHttpMethodKind.Get,
                    Url = server.Url,
                    AllowPrivateNetworkTargets = true,
                    SecretHeader = new WorkflowHttpSecretHeaderBinding
                    {
                        SecretId = secretId,
                        SecretNameSnapshot = "API",
                        HeaderName = "Authorization",
                        ValueFormat = WorkflowHttpSecretValueFormat.Bearer
                    }
                });

            Assert.Contains("Authorization: Bearer secret-token", server.RequestHeaders, StringComparison.Ordinal);
            Assert.DoesNotContain("secret-token", result.PayloadJson, StringComparison.Ordinal);
        });

        await RecordAsync("http secret header requires runtime resolver", async () =>
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteDirectAsync(new HttpFetchWorkflowExecutor(), new WorkflowHttpExecutorSettings
            {
                Method = WorkflowHttpMethodKind.Get,
                Url = "https://example.test",
                SecretHeader = new WorkflowHttpSecretHeaderBinding
                {
                    SecretId = Guid.NewGuid()
                }
            }));
        });

        await RecordAsync("http fails on server error", async () =>
        {
            await using var server = SingleResponseHttpServer.Json(500, "{\"error\":\"boom\"}");
            await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteDirectAsync(new HttpFetchWorkflowExecutor(), new WorkflowHttpExecutorSettings
            {
                Method = WorkflowHttpMethodKind.Get,
                Url = server.Url,
                AllowPrivateNetworkTargets = true
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
            await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteDirectAsync(new ProjectStructureWorkflowExecutor(new UnavailableProjectStructureRuntimeGateway()), new WorkflowProjectStructureExecutorSettings
            {
                Operation = WorkflowProjectStructureOperation.ListProjects,
                ProjectId = Guid.NewGuid()
            }));
        });

        await RecordAsync("image generation writes provider-runtime output", async () =>
        {
            using var temp = new TempDirectory();
            var provider = CreateProviderProfile("gpt-image-1-mini") with
            {
                Purpose = ProviderProfilePurpose.ImageGeneration,
                DefaultModel = "gpt-image-1-mini"
            };
            var imageService = new RecordingImageGenerationService(
                [4, 5, 6],
                "workflow revised prompt");
            var executor = new ImageGenerationWorkflowExecutor(
                new TestProviderProfileRegistry([provider]),
                imageService,
                new WorkspacePathResolutionService(temp.Path));

            var result = await ExecuteDirectAsync(executor, new WorkflowImageGenerationExecutorSettings
            {
                ProviderProfileId = provider.Id,
                Prompt = "A clean workflow diagram",
                Model = "gpt-image-1-mini",
                Size = "1024x1024",
                Quality = "low",
                OutputFormat = "png",
                OutputWorkspacePath = "output/workflow-image"
            });

            var outputPath = System.IO.Path.Combine(temp.Path, "output", "workflow-image.png");
            Assert.True(File.Exists(outputPath));
            Assert.Equal(new byte[] { 4, 5, 6 }, await File.ReadAllBytesAsync(outputPath));
            Assert.Contains("workflow-image.png", result.PayloadJson, StringComparison.Ordinal);
            Assert.Contains("workflow revised prompt", result.PayloadJson, StringComparison.Ordinal);
            var request = Assert.Single(imageService.Requests);
            Assert.Equal(provider.Id, request.Provider.Id);
            Assert.Equal("gpt-image-1-mini", request.Model);
            Assert.Equal("A clean workflow diagram", request.Prompt);
            Assert.Empty(request.Sources);
        });

        await RecordAsync("image generation edit reports unsupported workflow source contract", async () =>
        {
            using var temp = new TempDirectory();
            var provider = CreateProviderProfile("gpt-image-1-mini") with
            {
                Purpose = ProviderProfilePurpose.ImageGeneration,
                DefaultModel = "gpt-image-1-mini"
            };
            var executor = new ImageGenerationWorkflowExecutor(
                new TestProviderProfileRegistry([provider]),
                new RecordingImageGenerationService([1, 2, 3]),
                new WorkspacePathResolutionService(temp.Path));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteDirectAsync(executor, new WorkflowImageGenerationExecutorSettings
            {
                Operation = WorkflowImageGenerationOperation.Edit,
                Prompt = "Edit a clean workflow diagram",
                OutputWorkspacePath = "output/workflow-image.png"
            }));
            Assert.Contains("source-image settings", exception.Message, StringComparison.OrdinalIgnoreCase);
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
        => await ExecuteDirectAsync(executor, settings, "{}");

    private static async Task<WorkflowNodeExecutionResult> ExecuteDirectAsync<TSettings>(
        IWorkflowExecutor executor,
        TSettings settings,
        string inputJson)
    {
        return await executor.ExecuteAsync(
            CreateExecutionContext(executor.Descriptor, settings),
            new WorkflowNodeInput(inputJson));
    }

    private static WorkflowNode CreateExecutorNode(
        string id,
        WorkflowExecutorId executorId,
        WorkflowValueShape? inputShape = null)
        => new(
            new WorkflowNodeId(id),
            WorkflowNodeKind.Executor,
            id,
            [],
            CreateSettings(executorId, inputShape));

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
                Instructions: "Pinned workflow instruction snapshot.",
                InputShape: WorkflowValueShape.Text,
                ResultShape: WorkflowValueShape.Text));

    private static WorkflowNodeSettings CreateSettings(
        WorkflowExecutorId executorId,
        WorkflowValueShape? inputShape = null)
        => new WorkflowNodeSettings(
            ComponentId: null,
            AgentId: null,
            SubworkflowId: null,
            ExternalRequestKind: null,
            Instructions: string.Empty,
            InputShape: inputShape ?? WorkflowValueShape.Text,
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

    private static WorkflowEdge CreateEdge(
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

    private static RouteScenario CreateRouteScenario(
        string name,
        string payloadJson,
        string jsonPath,
        WorkflowRouteOperator @operator,
        string expectedValueJson,
        WorkflowRouteValueKind expectedValueKind,
        bool expected)
        => new(
            name,
            payloadJson,
            jsonPath,
            @operator,
            expectedValueJson,
            expectedValueKind,
            expected);

    private static LlmCallComponent CreateLlmComponent(
        WorkflowValueShape inputShape,
        WorkflowValueShape resultShape,
        string responseFormatJsonSchema = "")
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
                ResponseFormatJsonSchema: responseFormatJsonSchema),
            "Summarize the workflow payload.",
            inputShape,
            resultShape,
            AgentPermissionsPolicy.Default,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    private static ProviderProfile CreateProviderProfile(string defaultModel)
        => new(
            Guid.NewGuid(),
            "Workflow unit provider",
            ProviderKind.OpenAi,
            "https://api.openai.com/v1",
            "UNIT_TEST_OPENAI_API_KEY",
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

    private static ProviderUsageObservation CreateWorkflowUsageObservation(
        ProviderUsageObservationStatus status,
        int inputTokens,
        int cachedInputTokens,
        int outputTokens)
    {
        return new ProviderUsageObservation(
            Id: Guid.NewGuid(),
            CreatedAtUtc: DateTimeOffset.UtcNow,
            ProviderName: "Workflow unit provider",
            ProviderKind: ProviderKind.OpenAi,
            Model: "gpt-5-mini",
            TransportKind: ProviderTransportKind.ChatCompletions,
            SourcePhase: ProviderUsageSourcePhases.AgentRuntime,
            UsageStatus: status,
            InputTokens: inputTokens,
            CachedInputTokens: cachedInputTokens,
            OutputTokens: outputTokens,
            ReasoningTokens: 0,
            TotalTokens: inputTokens + outputTokens,
            ToolCallCount: 0);
    }

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

    private sealed class BranchRecordingWorkflowExecutor : IWorkflowExecutor
    {
        private readonly Dictionary<string, int> invocationCounts = new(StringComparer.Ordinal);

        public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.StorageFile;

        public Dictionary<string, string> OutputsByNode { get; init; } = new(StringComparer.Ordinal);

        public int InvocationCountFor(string nodeId)
            => invocationCounts.GetValueOrDefault(nodeId);

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
        {
            var nodeId = context.Node.Id.Value;
            invocationCounts[nodeId] = invocationCounts.GetValueOrDefault(nodeId) + 1;
            var payload = OutputsByNode.TryGetValue(nodeId, out var output)
                ? output
                : input.PayloadJson;

            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
                context.Node.Id,
                payload,
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

    private sealed class CapturingAgentRuntime(string responseText) : IAgentRuntime
    {
        public AgentDefinition? LastAgent { get; private set; }

        public ProviderProfile? LastProvider { get; private set; }

        public AgentRuntimeExecutionOptions? LastExecutionOptions { get; private set; }

        public string LastPrompt { get; private set; } = string.Empty;

        public int InputTokens { get; init; } = 12;

        public int OutputTokens { get; init; } = 8;

        public IReadOnlyList<ProviderUsageObservation> UsageObservations { get; init; } = [];

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
            LastAgent = agent;
            LastProvider = provider;
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
            return Task.FromResult(new AgentRuntimeResponse(
                responseText,
                InputTokens,
                OutputTokens,
                ToolCalls: 0,
                RuntimeSessionKey: string.Empty,
                SerializedSessionStateJson: null,
                PendingApprovals: [])
            {
                UsageObservations = UsageObservations
            });
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

        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
            ProviderProfile provider,
            ProviderModelMaintenanceEditorRequest request,
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
    }

    private sealed class TestProviderProfileRegistry(
        IReadOnlyList<ProviderProfile> providers) :
        IProviderProfileRegistry,
        IProviderRuntimeProfileSource
    {
        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(providers);
        }

        public Task<ProviderProfile?> GetProviderAsync(
            Guid providerId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(providers.FirstOrDefault(provider => provider.Id == providerId));
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

    private sealed class RecordingImageGenerationService(
        byte[] imageBytes,
        string revisedPrompt = "") : IAgentImageGenerationService
    {
        public List<AgentImageGenerationRequest> Requests { get; } = [];

        public Task<AgentImageGenerationResult> GenerateAsync(
            AgentImageGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(new AgentImageGenerationResult(
                request.Model,
                request.Format,
                [new AgentGeneratedImage("image/png", imageBytes, revisedPrompt)]));
        }
    }

    private static ExecutionRunRecord CreateExecutionRunRecord()
    {
        var now = DateTimeOffset.UtcNow;
        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            ChatSessionId: null,
            Title: "Spreadsheet receipt test",
            SourceKind: "unit-test",
            SourceId: "spreadsheet-receipt",
            CorrelationId: "spreadsheet-receipt",
            CausationId: string.Empty,
            RequestedBy: "unit-test",
            RequestedByKind: "system",
            MetadataJson: "{}",
            InputSummary: "Input",
            ResultSummary: string.Empty,
            ProviderName: "Provider",
            Model: "model",
            State: ExecutionState.Running,
            Outcome: null,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            StartedAtUtc: now,
            CompletedAtUtc: null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: []);
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

        public string RequestHeaders { get; private set; } = string.Empty;

        public static SingleResponseHttpServer Json(int statusCode, string body)
            => new(statusCode, body, "application/json");

        public static SingleResponseHttpServer Html(int statusCode, string body)
            => new(statusCode, body, "text/html");

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
            RequestHeaders = await ReadRequestHeadersAsync(stream);
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

        private static async Task<string> ReadRequestHeadersAsync(NetworkStream stream)
        {
            var buffer = new byte[1024];
            var received = new List<byte>();
            while (received.Count < 8192)
            {
                var read = await stream.ReadAsync(buffer);
                if (read == 0)
                {
                    return Encoding.ASCII.GetString(received.ToArray());
                }

                received.AddRange(buffer.Take(read));
                if (received.Count >= 4 &&
                    Encoding.ASCII.GetString(received.ToArray()).Contains("\r\n\r\n", StringComparison.Ordinal))
                {
                    return Encoding.ASCII.GetString(received.ToArray());
                }
            }

            return Encoding.ASCII.GetString(received.ToArray());
        }
    }

    private sealed class StaticSecretRuntimeResolver(Guid expectedSecretId, string value) : ISecretRuntimeResolver
    {
        public Task<string?> ResolveValueAsync(
            SecretRuntimeRequest request,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(expectedSecretId, request.SecretId);
            Assert.Contains(expectedSecretId, request.AllowedSecretIds ?? []);
            return Task.FromResult<string?>(value);
        }
    }

    private sealed record RouteScenario(
        string Name,
        string PayloadJson,
        string JsonPath,
        WorkflowRouteOperator Operator,
        string ExpectedValueJson,
        WorkflowRouteValueKind ExpectedValueKind,
        bool Expected);
}
