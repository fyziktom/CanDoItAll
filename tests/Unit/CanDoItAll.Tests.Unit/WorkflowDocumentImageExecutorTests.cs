using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Documents;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Media;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowDocumentImageExecutorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Document_executor_prefers_explicit_source_and_delegates_all_settings()
    {
        var operations = new RecordingArtifactToolService
        {
            ConversionResult = CreateDocumentResult(succeeded: true)
        };
        var executor = new DocumentToMarkdownWorkflowExecutor(operations);

        var result = await ExecuteAsync(
            executor,
            new WorkflowDocumentToMarkdownExecutorSettings
            {
                SourcePath = "documents/brief.docx",
                SourcePathJsonPath = "$.invalid",
                OutputPath = "artifacts/brief.md",
                PreviewCharacters = 321
            },
            payloadJson: "not-json");

        Assert.Equal("documents/brief.docx", operations.SourcePath);
        Assert.Equal("artifacts/brief.md", operations.OutputPath);
        Assert.Equal(321, operations.PreviewCharacters);
        Assert.Equal("documents/brief.docx", JsonDocument.Parse(result.PayloadJson).RootElement.GetProperty("sourcePath").GetString());
    }

    [Fact]
    public async Task Document_executor_resolves_source_from_nested_array_input()
    {
        var operations = new RecordingArtifactToolService
        {
            ConversionResult = CreateDocumentResult(succeeded: true)
        };
        var executor = new DocumentToMarkdownWorkflowExecutor(operations);

        await ExecuteAsync(
            executor,
            new WorkflowDocumentToMarkdownExecutorSettings
            {
                SourcePathJsonPath = "$.documents[1].path"
            },
            """
            {
              "documents": [
                { "path": "documents/first.docx" },
                { "path": "documents/second.docx" }
              ]
            }
            """);

        Assert.Equal("documents/second.docx", operations.SourcePath);
    }

    [Fact]
    public async Task Shared_input_resolver_rejects_missing_or_non_string_values_explicitly()
    {
        var executor = new ImageInspectWorkflowExecutor(new RecordingImageOperationService());

        var missing = await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteAsync(
            executor,
            new WorkflowImageInspectExecutorSettings(),
            "{}"));
        Assert.Contains("Path", missing.Message, StringComparison.Ordinal);

        var wrongType = await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteAsync(
            executor,
            new WorkflowImageInspectExecutorSettings
            {
                PathJsonPath = "$.image.path"
            },
            """{"image":{"path":42}}"""));
        Assert.Contains("non-empty JSON string", wrongType.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Document_executor_surfaces_shared_operation_path_or_write_failure()
    {
        var operations = new RecordingArtifactToolService
        {
            ConversionResult = CreateDocumentResult(
                succeeded: false,
                message: "Output path is denied.",
                diagnostics: "Markdown write failed for artifacts/brief.md.")
        };
        var executor = new DocumentToMarkdownWorkflowExecutor(operations);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteAsync(
            executor,
            new WorkflowDocumentToMarkdownExecutorSettings
            {
                SourcePath = "documents/brief.docx",
                OutputPath = "artifacts/brief.md"
            }));

        Assert.Contains("Markdown write failed", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Document_executor_propagates_cancellation_into_shared_conversion_operation()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operations = new RecordingArtifactToolService
        {
            ConversionHandler = async cancellationToken =>
            {
                entered.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return CreateDocumentResult(succeeded: true);
            }
        };
        var executor = new DocumentToMarkdownWorkflowExecutor(operations);
        using var cancellationSource = new CancellationTokenSource();

        var execution = ExecuteAsync(
            executor,
            new WorkflowDocumentToMarkdownExecutorSettings
            {
                SourcePath = "documents/brief.docx"
            },
            cancellationToken: cancellationSource.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
        Assert.Equal(cancellationSource.Token, operations.CancellationToken);
    }

    [Fact]
    public async Task Image_inspect_executor_delegates_to_shared_image_operation_and_surfaces_failures()
    {
        var operations = new RecordingImageOperationService
        {
            InspectionResult = CreateImageInspectionResult(succeeded: true)
        };
        var executor = new ImageInspectWorkflowExecutor(operations);

        var success = await ExecuteAsync(
            executor,
            new WorkflowImageInspectExecutorSettings
            {
                PathJsonPath = "$.asset.path"
            },
            """{"asset":{"path":"images/evidence.png"}}""");
        Assert.Equal("images/evidence.png", operations.InspectedPath);
        Assert.Equal("PNG", JsonDocument.Parse(success.PayloadJson).RootElement.GetProperty("format").GetString());

        operations.InspectionResult = CreateImageInspectionResult(
            succeeded: false,
            message: "Image path is outside the workspace.",
            diagnostics: "Denied path.");
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteAsync(
            executor,
            new WorkflowImageInspectExecutorSettings
            {
                Path = "../outside.png"
            }));
        Assert.Contains("Denied path", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Image_analyze_executor_returns_typed_payload_and_known_usage()
    {
        var provider = CreateProvider(
            model: "gpt-5.4",
            prices: [new ProviderModelTokenPrice("gpt-5.4", 1m, 0m, 2m)]);
        var images = new RecordingImageOperationService
        {
            ContentResult = CreateImageContentResult(succeeded: true)
        };
        var analysis = new RecordingImageAnalysisService
        {
            Result = new AgentImageAnalysisResult("gpt-5.4", "A red square on a white background.", 100, 50)
        };
        var executor = new ImageAnalyzeWorkflowExecutor(
            new RecordingProviderRegistry([provider]),
            images,
            analysis);

        var result = await ExecuteAsync(
            executor,
            new WorkflowImageAnalyzeExecutorSettings
            {
                Path = "images/evidence.png",
                Prompt = "Describe only visible evidence.",
                ProviderProfileId = provider.Id,
                Model = "gpt-5.4",
                MaxBytes = 2048,
                ModelParameterConfigurationJson = """{"modelParameters":{"numPredict":128}}"""
            });
        var payload = JsonSerializer.Deserialize<WorkflowImageAnalyzeExecutorResult>(
            result.PayloadJson,
            WorkflowExecutorJson.Options);

        Assert.NotNull(payload);
        Assert.Equal(provider.Id, payload.ProviderProfileId);
        Assert.Equal("images/evidence.png", payload.Path);
        Assert.Equal("A red square on a white background.", payload.Analysis);
        Assert.Equal(100, payload.InputTokens);
        Assert.Equal(50, payload.OutputTokens);
        Assert.Equal(2048, images.MaxBytes);
        Assert.Equal(WorkflowExecutorIds.ImageAnalyze.Value, images.OperationName);
        var request = Assert.Single(analysis.Requests);
        Assert.Equal("Describe only visible evidence.", request.Prompt);
        Assert.Equal("image/png", Assert.Single(request.Sources).ContentType);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, request.Sources[0].Bytes);
        Assert.Equal("""{"modelParameters":{"numPredict":128}}""", request.ModelParameterConfigurationJson);
        Assert.NotNull(result.Usage);
        Assert.Equal(100, result.Usage.InputTokens);
        Assert.Equal(50, result.Usage.OutputTokens);
        Assert.Equal(0.0002m, result.Usage.CostUsd);
        Assert.Equal(1, result.Usage.KnownObservationCount);
        Assert.Equal(0, result.Usage.UnknownObservationCount);
    }

    [Fact]
    public async Task Image_analyze_executor_preserves_tokens_when_returned_model_price_is_unknown()
    {
        var provider = CreateProvider(
            model: "gpt-5.4",
            prices: [new ProviderModelTokenPrice("gpt-5.4", 1m, 0m, 2m)]);
        var analysis = new RecordingImageAnalysisService
        {
            Result = new AgentImageAnalysisResult("provider-model-alias-without-price", "Visible evidence.", 17, 9)
        };
        var executor = new ImageAnalyzeWorkflowExecutor(
            new RecordingProviderRegistry([provider]),
            new RecordingImageOperationService
            {
                ContentResult = CreateImageContentResult(succeeded: true)
            },
            analysis);

        var result = await ExecuteAsync(
            executor,
            new WorkflowImageAnalyzeExecutorSettings
            {
                Path = "images/evidence.png",
                ProviderProfileId = provider.Id
            });

        Assert.NotNull(result.Usage);
        Assert.Equal("provider-model-alias-without-price", result.Usage.Model);
        Assert.Equal(17, result.Usage.InputTokens);
        Assert.Equal(9, result.Usage.OutputTokens);
        Assert.Equal(0m, result.Usage.CostUsd);
        Assert.Equal(0, result.Usage.KnownObservationCount);
        Assert.Equal(1, result.Usage.UnknownObservationCount);
    }

    [Fact]
    public async Task Image_analyze_executor_selects_first_enabled_vision_chat_provider()
    {
        var disabledVision = CreateProvider(isEnabled: false);
        var textOnly = CreateProvider(
            kind: ProviderKind.Ollama,
            model: "llama3.1",
            name: "Text-only Ollama");
        var selected = CreateProvider(name: "Selected vision provider");
        var analysis = new RecordingImageAnalysisService();
        var executor = new ImageAnalyzeWorkflowExecutor(
            new RecordingProviderRegistry([disabledVision, textOnly, selected]),
            new RecordingImageOperationService
            {
                ContentResult = CreateImageContentResult(succeeded: true)
            },
            analysis);

        await ExecuteAsync(
            executor,
            new WorkflowImageAnalyzeExecutorSettings
            {
                Path = "images/evidence.png"
            });

        Assert.Equal(selected.Id, Assert.Single(analysis.Requests).Provider.Id);
    }

    [Fact]
    public async Task Image_analyze_executor_rejects_missing_disabled_non_chat_and_non_vision_profiles()
    {
        var missingId = Guid.NewGuid();
        var missingExecutor = CreateAnalyzeExecutor(new RecordingProviderRegistry([]));
        var missing = await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteAsync(
            missingExecutor,
            CreateAnalyzeSettings(missingId)));
        Assert.Contains("was not found", missing.Message, StringComparison.Ordinal);

        var disabled = CreateProvider(isEnabled: false);
        var disabledFailure = await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteAsync(
            CreateAnalyzeExecutor(new RecordingProviderRegistry([disabled])),
            CreateAnalyzeSettings(disabled.Id)));
        Assert.Contains("disabled", disabledFailure.Message, StringComparison.OrdinalIgnoreCase);

        var nonChat = CreateProvider(purpose: ProviderProfilePurpose.ImageGeneration);
        var nonChatFailure = await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteAsync(
            CreateAnalyzeExecutor(new RecordingProviderRegistry([nonChat])),
            CreateAnalyzeSettings(nonChat.Id)));
        Assert.Contains("not a Chat", nonChatFailure.Message, StringComparison.Ordinal);

        var textOnly = CreateProvider(
            kind: ProviderKind.Ollama,
            model: "llama3.1",
            name: "Text-only Ollama");
        var nonVisionFailure = await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteAsync(
            CreateAnalyzeExecutor(new RecordingProviderRegistry([textOnly])),
            CreateAnalyzeSettings(textOnly.Id)));
        Assert.Contains("vision-capable", nonVisionFailure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Image_analyze_executor_stops_before_provider_call_when_bounded_image_load_fails()
    {
        var provider = CreateProvider();
        var images = new RecordingImageOperationService
        {
            ContentResult = CreateImageContentResult(
                succeeded: false,
                message: "Image exceeds the analysis limit.",
                diagnostics: "4,096 bytes exceeds 1,024 bytes.")
        };
        var analysis = new RecordingImageAnalysisService();
        var executor = new ImageAnalyzeWorkflowExecutor(
            new RecordingProviderRegistry([provider]),
            images,
            analysis);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteAsync(
            executor,
            new WorkflowImageAnalyzeExecutorSettings
            {
                Path = "images/large.png",
                ProviderProfileId = provider.Id,
                MaxBytes = 1024
            }));

        Assert.Contains("exceeds 1,024 bytes", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1024, images.MaxBytes);
        Assert.Empty(analysis.Requests);
    }

    [Fact]
    public async Task Image_analyze_executor_does_not_hide_provider_failures()
    {
        var provider = CreateProvider();
        var analysis = new RecordingImageAnalysisService
        {
            Handler = (_, _) => throw new InvalidOperationException("Provider rejected the image request.")
        };
        var executor = new ImageAnalyzeWorkflowExecutor(
            new RecordingProviderRegistry([provider]),
            new RecordingImageOperationService
            {
                ContentResult = CreateImageContentResult(succeeded: true)
            },
            analysis);

        var exception = await Assert.ThrowsAsync<WorkflowUsageObservationException>(() => ExecuteAsync(
            executor,
            CreateAnalyzeSettings(provider.Id)));

        Assert.Equal("Provider rejected the image request.", exception.Message);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public async Task Image_analyze_executor_propagates_cancellation_to_provider_analysis()
    {
        var provider = CreateProvider();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var analysis = new RecordingImageAnalysisService
        {
            Handler = async (_, cancellationToken) =>
            {
                entered.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new AgentImageAnalysisResult("gpt-5.4", string.Empty, 0, 0);
            }
        };
        var executor = new ImageAnalyzeWorkflowExecutor(
            new RecordingProviderRegistry([provider]),
            new RecordingImageOperationService
            {
                ContentResult = CreateImageContentResult(succeeded: true)
            },
            analysis);
        using var cancellationSource = new CancellationTokenSource();

        var execution = ExecuteAsync(
            executor,
            CreateAnalyzeSettings(provider.Id),
            cancellationToken: cancellationSource.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
        Assert.Equal(cancellationSource.Token, analysis.CancellationToken);
    }

    [Fact]
    public void Standard_category_extensions_register_document_and_image_contributions()
    {
        var services = new ServiceCollection();

        services.AddStandardDocumentWorkflowExecutors(ServiceLifetime.Scoped);
        services.AddStandardMediaWorkflowExecutors(ServiceLifetime.Scoped);

        var executorTypes = services
            .Where(descriptor => descriptor.ServiceType == typeof(IWorkflowExecutor))
            .Select(descriptor => descriptor.ImplementationType)
            .Where(type => type is { IsGenericType: true })
            .Select(type => type!.GetGenericArguments().Single())
            .ToHashSet();
        Assert.Contains(typeof(DocumentToMarkdownWorkflowExecutor), executorTypes);
        Assert.Contains(typeof(ImageInspectWorkflowExecutor), executorTypes);
        Assert.Contains(typeof(ImageAnalyzeWorkflowExecutor), executorTypes);
    }

    [Fact]
    public async Task Contribution_core_keeps_catalog_implementation_and_invoker_in_parity()
    {
        var artifactOperations = new RecordingArtifactToolService
        {
            ConversionResult = CreateDocumentResult(succeeded: true)
        };
        var services = new ServiceCollection();
        services.AddSingleton<IWorkspaceArtifactToolService>(artifactOperations);
        services.AddSingleton<IWorkspaceImageOperationService>(new RecordingImageOperationService
        {
            InspectionResult = CreateImageInspectionResult(succeeded: true),
            ContentResult = CreateImageContentResult(succeeded: true)
        });
        services.AddSingleton<IProviderProfileRegistry>(new RecordingProviderRegistry([CreateProvider()]));
        services.AddSingleton<IAgentImageAnalysisService>(new RecordingImageAnalysisService());
        services.AddWorkflowExecutorContribution<DocumentToMarkdownWorkflowExecutor>(
            BuiltInWorkflowExecutorDescriptors.DocumentToMarkdown,
            ServiceLifetime.Scoped);
        services.AddWorkflowExecutorContribution<ImageInspectWorkflowExecutor>(
            BuiltInWorkflowExecutorDescriptors.ImageInspect,
            ServiceLifetime.Scoped);
        services.AddWorkflowExecutorContribution<ImageAnalyzeWorkflowExecutor>(
            BuiltInWorkflowExecutorDescriptors.ImageAnalyze,
            ServiceLifetime.Scoped);
        services.AddWorkflowExecutorCoreServices();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var catalog = scope.ServiceProvider.GetRequiredService<IWorkflowExecutorCatalog>();
        var implementations = scope.ServiceProvider.GetServices<IWorkflowExecutor>().ToArray();
        var expectedIds = new[]
        {
            WorkflowExecutorIds.DocumentToMarkdown,
            WorkflowExecutorIds.ImageInspect,
            WorkflowExecutorIds.ImageAnalyze
        };
        Assert.Equal(
            expectedIds.OrderBy(id => id.Value),
            catalog.ListExecutors().Select(descriptor => descriptor.Id).OrderBy(id => id.Value));
        Assert.Equal(
            expectedIds.OrderBy(id => id.Value),
            implementations.Select(executor => executor.Descriptor.Id).OrderBy(id => id.Value));

        var (definition, node) = CreateDefinitionAndNode(
            BuiltInWorkflowExecutorDescriptors.DocumentToMarkdown,
            new WorkflowDocumentToMarkdownExecutorSettings
            {
                SourcePath = "documents/brief.docx"
            });
        var result = await scope.ServiceProvider
            .GetRequiredService<IWorkflowExecutorInvoker>()
            .ExecuteAsync(definition, node, new WorkflowNodeInput("{}"));

        Assert.Equal(node.Id, result.NodeId);
        Assert.Equal("documents/brief.docx", artifactOperations.SourcePath);
    }

    [Fact]
    public void Media_registration_preserves_real_image_analysis_service()
    {
        var realService = new RecordingImageAnalysisService();
        var services = new ServiceCollection();
        services.AddSingleton<IAgentImageAnalysisService>(realService);

        services.AddStandardMediaWorkflowExecutors(ServiceLifetime.Scoped);
        using var provider = services.BuildServiceProvider();

        Assert.Same(realService, provider.GetRequiredService<IAgentImageAnalysisService>());
    }

    private static ImageAnalyzeWorkflowExecutor CreateAnalyzeExecutor(IProviderProfileRegistry registry)
        => new(
            registry,
            new RecordingImageOperationService
            {
                ContentResult = CreateImageContentResult(succeeded: true)
            },
            new RecordingImageAnalysisService());

    private static WorkflowImageAnalyzeExecutorSettings CreateAnalyzeSettings(Guid providerId)
        => new()
        {
            Path = "images/evidence.png",
            ProviderProfileId = providerId
        };

    private static async Task<WorkflowNodeExecutionResult> ExecuteAsync<TSettings>(
        IWorkflowExecutor executor,
        TSettings settings,
        string payloadJson = "{}",
        CancellationToken cancellationToken = default)
    {
        var (definition, node) = CreateDefinitionAndNode(executor.Descriptor, settings);
        var context = new WorkflowExecutorExecutionContext(
            definition,
            node,
            executor.Descriptor,
            node.Settings.ExecutorSettingsJson,
            executor.Descriptor.DefaultPolicy);
        return await executor.ExecuteAsync(context, new WorkflowNodeInput(payloadJson), cancellationToken);
    }

    private static (WorkflowDefinition Definition, WorkflowNode Node) CreateDefinitionAndNode<TSettings>(
        WorkflowExecutorDescriptor descriptor,
        TSettings settings)
    {
        var settingsJson = JsonSerializer.Serialize(settings, JsonOptions);
        var node = new WorkflowNode(
            new WorkflowNodeId("executor"),
            WorkflowNodeKind.Executor,
            descriptor.Name,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: descriptor.InputShape,
                ResultShape: descriptor.ResultShape)
            {
                ExecutorId = descriptor.Id,
                ExecutorSettingsJson = settingsJson,
                ExecutionPolicy = descriptor.DefaultPolicy
            });
        var now = DateTimeOffset.UtcNow;
        var definition = new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Executor test",
            "Document and image executor test.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(node.Id, [node], []),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            now,
            now);
        return (definition, node);
    }

    private static ProviderProfile CreateProvider(
        ProviderKind kind = ProviderKind.OpenAi,
        string model = "gpt-5.4",
        string name = "Vision provider",
        bool isEnabled = true,
        ProviderProfilePurpose purpose = ProviderProfilePurpose.Chat,
        IReadOnlyList<ProviderModelTokenPrice>? prices = null)
        => new(
            Guid.NewGuid(),
            name,
            kind,
            kind == ProviderKind.Ollama ? "http://localhost:11434" : "https://api.openai.com/v1",
            kind == ProviderKind.Ollama ? string.Empty : "TEST_PROVIDER_API_KEY",
            model,
            kind == ProviderKind.Ollama ? ProviderTransportKind.ChatCompletions : ProviderTransportKind.Responses,
            isEnabled,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: [model],
            Purpose: purpose)
        {
            ModelPrices = prices ?? []
        };

    private static WorkspaceDocumentConversionResult CreateDocumentResult(
        bool succeeded,
        string message = "Converted.",
        string diagnostics = "")
        => new(
            succeeded,
            message,
            CreateReceipt("document.to-markdown", succeeded),
            "documents/brief.docx",
            "artifacts/brief.md",
            "# Brief",
            PreviewTruncated: false,
            Diagnostics: diagnostics);

    private static WorkspaceImageInspectionResult CreateImageInspectionResult(
        bool succeeded,
        string message = "Inspected.",
        string diagnostics = "")
        => new(
            succeeded,
            message,
            CreateReceipt("image.inspect", succeeded),
            "images/evidence.png",
            succeeded ? "PNG" : string.Empty,
            succeeded ? "image/png" : string.Empty,
            succeeded ? 4 : 0,
            succeeded ? 1 : null,
            succeeded ? 1 : null,
            diagnostics);

    private static WorkspaceImageContentResult CreateImageContentResult(
        bool succeeded,
        string message = "Loaded.",
        string diagnostics = "")
        => new(
            succeeded,
            message,
            CreateReceipt("image.analyze", succeeded),
            "images/evidence.png",
            succeeded ? "PNG" : string.Empty,
            succeeded ? "image/png" : string.Empty,
            succeeded ? 4 : 4096,
            succeeded ? 1 : null,
            succeeded ? 1 : null,
            succeeded ? [1, 2, 3, 4] : [],
            diagnostics);

    private static WorkspaceToolReceipt CreateReceipt(string operation, bool succeeded)
    {
        var now = DateTimeOffset.UtcNow;
        return new WorkspaceToolReceipt(
            operation,
            MutatesWorkspace: operation == "document.to-markdown",
            Boundary: "sandbox",
            Outcome: succeeded ? "Succeeded" : "Failed",
            Message: succeeded ? "Succeeded." : "Failed.",
            ReceiptRelativePath: string.Empty,
            TargetPaths: [],
            ArtifactReferences: [],
            StartedAtUtc: now,
            CompletedAtUtc: now);
    }

    private sealed class RecordingArtifactToolService : IWorkspaceArtifactToolService
    {
        public WorkspaceDocumentConversionResult ConversionResult { get; set; } = CreateDocumentResult(succeeded: true);

        public Func<CancellationToken, Task<WorkspaceDocumentConversionResult>>? ConversionHandler { get; init; }

        public string SourcePath { get; private set; } = string.Empty;

        public string? OutputPath { get; private set; }

        public int PreviewCharacters { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<WorkspaceDocumentConversionResult> ConvertDocumentToMarkdown(
            string path,
            string? outputPath = null,
            int previewCharacters = 4000,
            CancellationToken cancellationToken = default)
        {
            SourcePath = path;
            OutputPath = outputPath;
            PreviewCharacters = previewCharacters;
            CancellationToken = cancellationToken;
            return ConversionHandler?.Invoke(cancellationToken) ?? Task.FromResult(ConversionResult);
        }

        public Task<WorkspaceSpreadsheetInspectionResult> InspectSpreadsheetFile(
            string path,
            int maxRows = 8,
            int maxColumns = 8,
            int previewCharacters = 4000)
            => throw new NotSupportedException();

        public Task<WorkspaceImageInspectionResult> InspectImageFile(string path)
            => throw new NotSupportedException();

        public Task<WorkspaceImageContentResult> ReadImageFile(
            string path,
            long maxBytes = 10 * 1024 * 1024,
            string operationName = "workspace_analyze_image")
            => throw new NotSupportedException();
    }

    private sealed class RecordingImageOperationService : IWorkspaceImageOperationService
    {
        public WorkspaceImageInspectionResult InspectionResult { get; set; } = CreateImageInspectionResult(succeeded: true);

        public WorkspaceImageContentResult ContentResult { get; set; } = CreateImageContentResult(succeeded: true);

        public string InspectedPath { get; private set; } = string.Empty;

        public string ReadPath { get; private set; } = string.Empty;

        public long MaxBytes { get; private set; }

        public string OperationName { get; private set; } = string.Empty;

        public Task<WorkspaceImageInspectionResult> InspectImageFile(string path)
        {
            InspectedPath = path;
            return Task.FromResult(InspectionResult);
        }

        public Task<WorkspaceImageContentResult> ReadImageFile(
            string path,
            long maxBytes = 10 * 1024 * 1024,
            string operationName = "workspace_analyze_image")
        {
            ReadPath = path;
            MaxBytes = maxBytes;
            OperationName = operationName;
            return Task.FromResult(ContentResult);
        }
    }

    private sealed class RecordingImageAnalysisService : IAgentImageAnalysisService
    {
        public AgentImageAnalysisResult Result { get; set; } = new("gpt-5.4", "Visible evidence.", 11, 7);

        public Func<AgentImageAnalysisRequest, CancellationToken, Task<AgentImageAnalysisResult>>? Handler { get; init; }

        public List<AgentImageAnalysisRequest> Requests { get; } = [];

        public CancellationToken CancellationToken { get; private set; }

        public Task<AgentImageAnalysisResult> AnalyzeAsync(
            AgentImageAnalysisRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            CancellationToken = cancellationToken;
            return Handler?.Invoke(request, cancellationToken) ?? Task.FromResult(Result);
        }
    }

    private sealed class RecordingProviderRegistry(IReadOnlyList<ProviderProfile> providers) : IProviderProfileRegistry
    {
        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(providers);

        public Task<ProviderProfile?> GetProviderAsync(
            Guid providerId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(providers.FirstOrDefault(provider => provider.Id == providerId));

        public Task<ProviderProfileEditorModel> GetProviderEditorAsync(
            Guid? providerId = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Guid> SaveProviderAsync(
            ProviderProfileEditorModel model,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProviderProfile> UpdateProviderAsync(
            Guid providerId,
            Func<ProviderProfile, ProviderProfile> update,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
