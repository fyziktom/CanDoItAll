using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace CanDoItAll.Tests.Unit;

public sealed class ImageGenerationAgentRuntimeToolProviderTests
{
    private static readonly JsonSerializerOptions FunctionResultJsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task CreateToolsAsync_returns_image_generation_tool_when_agent_is_allowed()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var toolProvider = new ImageGenerationAgentRuntimeToolProvider(
            new ThrowingProviderProfileRegistry(),
            TestWorkspaceServices.CreatePathResolutionService(Path.GetTempPath()),
            new FakeAgentImageGenerationService(),
            services);
        var chatProvider = CreateProvider(ProviderProfilePurpose.Chat);
        var imageProvider = CreateProvider(ProviderProfilePurpose.ImageGeneration);
        var agent = CreateAgent(
            chatProvider.Id,
            AgentImageGenerationAccessMetadata.Write(
                "{}",
                new AgentImageGenerationAccessSettings
                {
                    CanGenerateImages = true,
                    PreferredProviderProfileId = imageProvider.Id,
                    DefaultModel = "gpt-image-1-mini"
                }));

        var tools = await toolProvider.CreateToolsAsync(
            CreateContext(agent, chatProvider),
            CancellationToken.None);

        var tool = Assert.Single(tools);
        Assert.Equal(950, toolProvider.Order);
        Assert.Equal("image-generation.runtime-tools", toolProvider.Descriptor?.ProviderKey);
        Assert.Equal(AgentToolInvocationPolicyMetadata.ImageGenerationCreate, tool.Name);
    }

    [Fact]
    public async Task CreateToolsAsync_returns_no_tools_when_agent_image_generation_is_disabled()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var toolProvider = new ImageGenerationAgentRuntimeToolProvider(
            new ThrowingProviderProfileRegistry(),
            TestWorkspaceServices.CreatePathResolutionService(Path.GetTempPath()),
            new FakeAgentImageGenerationService(),
            services);
        var chatProvider = CreateProvider(ProviderProfilePurpose.Chat);
        var agent = CreateAgent(
            chatProvider.Id,
            AgentImageGenerationAccessMetadata.Write(
                "{}",
                new AgentImageGenerationAccessSettings()));

        var tools = await toolProvider.CreateToolsAsync(
            CreateContext(agent, chatProvider),
            CancellationToken.None);

        Assert.Empty(tools);
    }

    [Fact]
    public async Task Image_generation_tool_uses_image_capable_runtime_provider_when_no_preferred_provider_is_set()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var imageService = new FakeAgentImageGenerationService();
        var runtimeProvider = CreateProvider(ProviderProfilePurpose.ImageGeneration);
        var secondaryProvider = CreateProvider(ProviderProfilePurpose.ImageGeneration);
        var workspaceRoot = CreateTempWorkspaceRoot();
        var toolProvider = new ImageGenerationAgentRuntimeToolProvider(
            new InMemoryProviderProfileRegistry([runtimeProvider, secondaryProvider]),
            TestWorkspaceServices.CreatePathResolutionService(workspaceRoot),
            imageService,
            services);
        var agent = CreateAgent(
            runtimeProvider.Id,
            AgentImageGenerationAccessMetadata.Write(
                "{}",
                new AgentImageGenerationAccessSettings
                {
                    CanGenerateImages = true,
                    CanStoreImagesAsProjectAssets = true
                }));

        var tools = await toolProvider.CreateToolsAsync(
            CreateContext(agent, runtimeProvider),
            CancellationToken.None);

        var result = await InvokeImageGenerationToolAsync(
            Assert.Single(tools),
            new ImageGenerationCreateInput(
                "A clean product concept render.",
                "generated/runtime-default",
                Size: "1024x1024",
                Quality: "low",
                OutputFormat: "png"));

        Assert.Equal(runtimeProvider.Id, result.ProviderProfileId);
        Assert.Equal(runtimeProvider.Id, Assert.Single(imageService.Requests).Provider.Id);
        Assert.Contains("project_structure_asset_create", result.ProjectAssetStorageInstruction, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(workspaceRoot, "generated", "runtime-default.png")));
    }

    [Fact]
    public async Task Image_generation_tool_blocks_project_asset_instruction_when_agent_storage_is_disabled()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var imageService = new FakeAgentImageGenerationService();
        var imageProvider = CreateProvider(ProviderProfilePurpose.ImageGeneration);
        var toolProvider = new ImageGenerationAgentRuntimeToolProvider(
            new InMemoryProviderProfileRegistry([imageProvider]),
            TestWorkspaceServices.CreatePathResolutionService(CreateTempWorkspaceRoot()),
            imageService,
            services);
        var agent = CreateAgent(
            imageProvider.Id,
            AgentImageGenerationAccessMetadata.Write(
                "{}",
                new AgentImageGenerationAccessSettings
                {
                    CanGenerateImages = true
                }));

        var tools = await toolProvider.CreateToolsAsync(
            CreateContext(agent, imageProvider),
            CancellationToken.None);

        var result = await InvokeImageGenerationToolAsync(
            Assert.Single(tools),
            new ImageGenerationCreateInput(
                "A clean product concept render.",
                "generated/storage-disabled",
                OutputFormat: "png"));

        Assert.DoesNotContain("project_structure_asset_create", result.ProjectAssetStorageInstruction, StringComparison.Ordinal);
        Assert.Contains("not enabled", result.ProjectAssetStorageInstruction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Image_generation_tool_prefers_enabled_non_private_provider_when_no_preference_is_set()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var imageService = new FakeAgentImageGenerationService();
        var chatProvider = CreateProvider(ProviderProfilePurpose.Chat);
        var localProvider = CreateProvider(ProviderProfilePurpose.ImageGeneration) with
        {
            Name = "Local ComfyUI",
            IsPrivateProvider = true
        };
        var cloudProvider = CreateProvider(ProviderProfilePurpose.ImageGeneration) with
        {
            Name = "OpenAI image generation",
            IsPrivateProvider = false
        };
        var toolProvider = new ImageGenerationAgentRuntimeToolProvider(
            new InMemoryProviderProfileRegistry([localProvider, cloudProvider, chatProvider]),
            TestWorkspaceServices.CreatePathResolutionService(CreateTempWorkspaceRoot()),
            imageService,
            services);
        var agent = CreateAgent(
            chatProvider.Id,
            AgentImageGenerationAccessMetadata.Write(
                "{}",
                new AgentImageGenerationAccessSettings { CanGenerateImages = true }));

        var tools = await toolProvider.CreateToolsAsync(
            CreateContext(agent, chatProvider),
            CancellationToken.None);

        var result = await InvokeImageGenerationToolAsync(
            Assert.Single(tools),
            new ImageGenerationCreateInput(
                "A clean garden layout.",
                "generated/recommended-provider",
                OutputFormat: "png"));

        Assert.Equal(cloudProvider.Id, result.ProviderProfileId);
        Assert.Equal(cloudProvider.Id, Assert.Single(imageService.Requests).Provider.Id);
    }

    [Fact]
    public async Task Image_generation_tool_leaves_unexpected_provider_failure_opaque()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var imageProvider = CreateProvider(ProviderProfilePurpose.ImageGeneration) with { Name = "Offline image provider" };
        var imageService = new FakeAgentImageGenerationService
        {
            Failure = new HttpRequestException("Connection refused at a private endpoint.")
        };
        var toolProvider = new ImageGenerationAgentRuntimeToolProvider(
            new InMemoryProviderProfileRegistry([imageProvider]),
            TestWorkspaceServices.CreatePathResolutionService(CreateTempWorkspaceRoot()),
            imageService,
            services);
        var agent = CreateAgent(
            imageProvider.Id,
            AgentImageGenerationAccessMetadata.Write(
                "{}",
                new AgentImageGenerationAccessSettings
                {
                    CanGenerateImages = true,
                    PreferredProviderProfileId = imageProvider.Id
                }));
        var tools = await toolProvider.CreateToolsAsync(
            CreateContext(agent, imageProvider),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => InvokeImageGenerationToolAsync(
                Assert.Single(tools),
                new ImageGenerationCreateInput(
                    "A clean garden layout.",
                    "generated/provider-failure",
                    OutputFormat: "png")));

        Assert.Same(imageService.Failure, exception);
        Assert.Contains("private endpoint", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(MafAgentToolFailureMapper.TryMap(exception, out _));
    }

    [Theory]
    [InlineData(ImagePathFailureScenario.SourceOutsideWorkspace, "ImageSourcePathOutsideWorkspace")]
    [InlineData(ImagePathFailureScenario.SourceMissing, "ImageSourcePathMissing")]
    [InlineData(ImagePathFailureScenario.SourceDirectory, "ImageSourceFileRequired")]
    [InlineData(ImagePathFailureScenario.SourceUnsupportedExtension, "ImageSourceFormatUnsupported")]
    [InlineData(ImagePathFailureScenario.OutputDirectory, "ImageOutputFileRequired")]
    public async Task Image_generation_tool_exposes_retryable_sanitized_expected_path_failures(
        ImagePathFailureScenario scenario,
        string expectedErrorCode)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        using var workspace = new ImageGenerationTempWorkspace();
        var sensitiveMarker = $"private-{Guid.NewGuid():N}";
        var imageService = new FakeAgentImageGenerationService();
        var imageProvider = CreateProvider(ProviderProfilePurpose.ImageGeneration);
        var toolProvider = new ImageGenerationAgentRuntimeToolProvider(
            new InMemoryProviderProfileRegistry([imageProvider]),
            TestWorkspaceServices.CreatePathResolutionService(workspace.Path),
            imageService,
            services);
        var agent = CreateAgent(
            imageProvider.Id,
            AgentImageGenerationAccessMetadata.Write(
                "{}",
                new AgentImageGenerationAccessSettings
                {
                    CanGenerateImages = true,
                    PreferredProviderProfileId = imageProvider.Id
                }));
        var tool = Assert.Single(await toolProvider.CreateToolsAsync(
            CreateContext(agent, imageProvider),
            CancellationToken.None));
        var (outputPath, sourcePaths) = PreparePathFailureScenario(
            workspace.Path,
            sensitiveMarker,
            scenario);

        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            InvokeImageGenerationToolAsync(
                tool,
                new ImageGenerationCreateInput(
                    "A generic concept render.",
                    outputPath,
                    OutputFormat: "png",
                    SourceWorkspacePaths: sourcePaths)));

        var failure = Assert.IsAssignableFrom<IAgentToolFailure>(exception);
        Assert.Equal(expectedErrorCode, failure.ErrorCode);
        Assert.True(failure.IsSafeToExpose);
        Assert.True(failure.CanRetryWithCorrectedInput);
        Assert.Contains("retry", failure.SafeMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(sensitiveMarker, failure.SafeMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(workspace.Path, failure.SafeMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(MafAgentToolFailureMapper.TryMap(exception, out var mappedFailure));
        Assert.Equal(failure.SafeMessage, mappedFailure.Message);
        Assert.True(mappedFailure.CanRetryWithCorrectedInput);
        Assert.Empty(imageService.Requests);
    }

    [Fact]
    public async Task Image_generation_tool_succeeds_after_retrying_with_a_corrected_source_path()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        using var workspace = new ImageGenerationTempWorkspace();
        var imageService = new FakeAgentImageGenerationService();
        var imageProvider = CreateProvider(ProviderProfilePurpose.ImageGeneration);
        var toolProvider = new ImageGenerationAgentRuntimeToolProvider(
            new InMemoryProviderProfileRegistry([imageProvider]),
            TestWorkspaceServices.CreatePathResolutionService(workspace.Path),
            imageService,
            services);
        var agent = CreateAgent(
            imageProvider.Id,
            AgentImageGenerationAccessMetadata.Write(
                "{}",
                new AgentImageGenerationAccessSettings
                {
                    CanGenerateImages = true,
                    PreferredProviderProfileId = imageProvider.Id
                }));
        var tool = Assert.Single(await toolProvider.CreateToolsAsync(
            CreateContext(agent, imageProvider),
            CancellationToken.None));
        var failedRequest = new ImageGenerationCreateInput(
            "A generic concept render.",
            "generated/retry-result",
            OutputFormat: "png",
            SourceWorkspacePaths: ["missing-source.png"]);

        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            InvokeImageGenerationToolAsync(tool, failedRequest));

        var failure = Assert.IsAssignableFrom<IAgentToolFailure>(exception);
        Assert.True(failure.CanRetryWithCorrectedInput);
        File.WriteAllBytes(Path.Combine(workspace.Path, "corrected-source.png"), [1, 2, 3]);

        var result = await InvokeImageGenerationToolAsync(
            tool,
            failedRequest with { SourceWorkspacePaths = ["corrected-source.png"] });

        Assert.True(result.Success);
        Assert.Equal(1, result.SourceCount);
        Assert.Single(imageService.Requests);
        Assert.True(File.Exists(Path.Combine(workspace.Path, "generated", "retry-result.png")));
    }

    [Theory]
    [InlineData(UnexpectedPathResolverFailureKind.InvalidOperation)]
    [InlineData(UnexpectedPathResolverFailureKind.Io)]
    public async Task Image_generation_tool_leaves_untyped_path_resolver_failures_opaque(
        UnexpectedPathResolverFailureKind failureKind)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var sensitiveDiagnostic = $@"Unexpected path failure at C:\private\{Guid.NewGuid():N}.png";
        Exception expected = failureKind switch
        {
            UnexpectedPathResolverFailureKind.InvalidOperation =>
                new InvalidOperationException(sensitiveDiagnostic),
            UnexpectedPathResolverFailureKind.Io =>
                new IOException(sensitiveDiagnostic),
            _ => throw new ArgumentOutOfRangeException(
                nameof(failureKind),
                failureKind,
                "Unknown unexpected path resolver failure kind.")
        };
        var imageProvider = CreateProvider(ProviderProfilePurpose.ImageGeneration);
        var toolProvider = new ImageGenerationAgentRuntimeToolProvider(
            new InMemoryProviderProfileRegistry([imageProvider]),
            new ThrowingWorkspacePathResolutionService(expected),
            new FakeAgentImageGenerationService(),
            services);
        var agent = CreateAgent(
            imageProvider.Id,
            AgentImageGenerationAccessMetadata.Write(
                "{}",
                new AgentImageGenerationAccessSettings
                {
                    CanGenerateImages = true,
                    PreferredProviderProfileId = imageProvider.Id
                }));
        var tool = Assert.Single(await toolProvider.CreateToolsAsync(
            CreateContext(agent, imageProvider),
            CancellationToken.None));

        var exception = await Record.ExceptionAsync(() =>
            InvokeImageGenerationToolAsync(
                tool,
                new ImageGenerationCreateInput(
                    "A generic concept render.",
                    "generated/unexpected-failure",
                    OutputFormat: "png")));

        Assert.Same(expected, exception);
        Assert.False(exception is IAgentToolFailure);
        Assert.False(MafAgentToolFailureMapper.TryMap(exception!, out _));
    }

    private static (string OutputPath, IReadOnlyList<string>? SourcePaths) PreparePathFailureScenario(
        string workspaceRoot,
        string sensitiveMarker,
        ImagePathFailureScenario scenario)
    {
        var sourcePath = scenario switch
        {
            ImagePathFailureScenario.SourceOutsideWorkspace =>
                Path.Combine(Path.GetDirectoryName(workspaceRoot)!, $"{sensitiveMarker}.png"),
            ImagePathFailureScenario.SourceMissing => $"{sensitiveMarker}.png",
            ImagePathFailureScenario.SourceDirectory => CreateDirectorySource(workspaceRoot, sensitiveMarker),
            ImagePathFailureScenario.SourceUnsupportedExtension => CreateUnsupportedSource(workspaceRoot, sensitiveMarker),
            ImagePathFailureScenario.OutputDirectory => null,
            _ => throw new ArgumentOutOfRangeException(
                nameof(scenario),
                scenario,
                "Unknown image path failure scenario.")
        };
        var outputPath = scenario == ImagePathFailureScenario.OutputDirectory
            ? CreateOutputDirectory(workspaceRoot, sensitiveMarker)
            : "generated/path-failure-result";

        return (outputPath, sourcePath is null ? null : [sourcePath]);
    }

    private static string CreateDirectorySource(string workspaceRoot, string sensitiveMarker)
    {
        var relativePath = $"{sensitiveMarker}.png";
        Directory.CreateDirectory(Path.Combine(workspaceRoot, relativePath));
        return relativePath;
    }

    private static string CreateUnsupportedSource(string workspaceRoot, string sensitiveMarker)
    {
        var relativePath = $"{sensitiveMarker}.txt";
        File.WriteAllText(Path.Combine(workspaceRoot, relativePath), "not an image");
        return relativePath;
    }

    private static string CreateOutputDirectory(string workspaceRoot, string sensitiveMarker)
    {
        var relativePath = $"{sensitiveMarker}.png";
        Directory.CreateDirectory(Path.Combine(workspaceRoot, relativePath));
        return relativePath;
    }

    private static AgentRuntimeToolProviderContext CreateContext(
        AgentDefinition agent,
        ProviderProfile provider)
    {
        return new AgentRuntimeToolProviderContext(
            agent,
            provider,
            [],
            SuppressApprovalRequirements: false,
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            RuntimeSessionKey: "unit-image-generation",
            AgentRuntimeContextIntent.Empty,
            Tags: new Dictionary<string, string>());
    }

    private static AgentDefinition CreateAgent(
        Guid providerProfileId,
        string configurationJson)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Guid.NewGuid(),
            "Image test agent",
            "Image tester",
            "Tests image generation tool provider.",
            "Test image generation.",
            AgentLifecycleStatus.Active,
            providerProfileId,
            "gpt-5-mini",
            AgentWorkloadKind.General,
            AgentChatHistoryMode.ProviderDefault,
            0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            configurationJson,
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default,
            [],
            [],
            now,
            now);
    }

    private static ProviderProfile CreateProvider(ProviderProfilePurpose purpose)
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            purpose == ProviderProfilePurpose.ImageGeneration ? "OpenAI image" : "OpenAI chat",
            ProviderKind.OpenAi,
            "https://api.openai.com",
            "OPENAI_API_KEY",
            purpose == ProviderProfilePurpose.ImageGeneration ? "gpt-image-1-mini" : "gpt-5-mini",
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: string.Empty,
            Notes: string.Empty,
            HealthStatus: string.Empty,
            LastCheckedAtUtc: null,
            SuggestedModels: [],
            purpose);
    }

    private static string CreateTempWorkspaceRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "candoitall-image-tool-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task<ImageGenerationCreateResult> InvokeImageGenerationToolAsync(
        AITool tool,
        ImageGenerationCreateInput input)
    {
        var function = Assert.IsAssignableFrom<AIFunction>(tool);
        var rawResult = await function.InvokeAsync(
            new AIFunctionArguments
            {
                ["request"] = input
            });

        return rawResult switch
        {
            ImageGenerationCreateResult result => result,
            JsonElement jsonElement => JsonSerializer.Deserialize<ImageGenerationCreateResult>(jsonElement.GetRawText(), FunctionResultJsonOptions)
                ?? throw new InvalidOperationException("Image generation function returned null JSON."),
            _ => throw new InvalidOperationException($"Unexpected image generation result type '{rawResult?.GetType().FullName ?? "<null>"}'.")
        };
    }

    private sealed class ThrowingProviderProfileRegistry :
        IProviderProfileRegistry,
        IProviderRuntimeProfileSource
    {
        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ProviderProfile?> GetProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ProviderProfileEditorModel> GetProviderEditorAsync(Guid? providerId = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Guid> SaveProviderAsync(ProviderProfileEditorModel model, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ProviderProfile> UpdateProviderAsync(
            Guid providerId,
            Func<ProviderProfile, ProviderProfile> update,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class InMemoryProviderProfileRegistry(
        IReadOnlyList<ProviderProfile> providers) :
        IProviderProfileRegistry,
        IProviderRuntimeProfileSource
    {
        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(providers);
        }

        public Task<ProviderProfile?> GetProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(providers.FirstOrDefault(provider => provider.Id == providerId));
        }

        public Task<ProviderProfileEditorModel> GetProviderEditorAsync(Guid? providerId = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Guid> SaveProviderAsync(ProviderProfileEditorModel model, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ProviderProfile> UpdateProviderAsync(
            Guid providerId,
            Func<ProviderProfile, ProviderProfile> update,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeAgentImageGenerationService : IAgentImageGenerationService
    {
        public List<AgentImageGenerationRequest> Requests { get; } = [];

        public Exception? Failure { get; init; }

        public Task<AgentImageGenerationResult> GenerateAsync(
            AgentImageGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            if (Failure is not null)
            {
                throw Failure;
            }

            return Task.FromResult(new AgentImageGenerationResult(
                request.Model,
                request.Format,
                [new AgentGeneratedImage("image/png", [1, 2, 3], "revised")]));
        }
    }

    private sealed class ThrowingWorkspacePathResolutionService(Exception exception)
        : IWorkspacePathResolutionService
    {
        public WorkspaceResolvedPath ResolveFilePath(string path, bool allowMissing)
            => throw exception;

        public WorkspaceResolvedPath ResolveDirectoryPath(string path, bool allowMissing)
            => throw exception;
    }

    private sealed class ImageGenerationTempWorkspace : IDisposable
    {
        public ImageGenerationTempWorkspace()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                nameof(ImageGenerationTempWorkspace),
                Guid.NewGuid().ToString("N"));
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

    public enum ImagePathFailureScenario
    {
        SourceOutsideWorkspace,
        SourceMissing,
        SourceDirectory,
        SourceUnsupportedExtension,
        OutputDirectory
    }

    public enum UnexpectedPathResolverFailureKind
    {
        InvalidOperation,
        Io
    }
}
