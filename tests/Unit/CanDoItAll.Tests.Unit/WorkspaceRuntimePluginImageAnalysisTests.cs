using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceRuntimePluginImageAnalysisTests
{
    [Fact]
    public async Task AnalyzeImageFile_delegates_to_analysis_service_and_preserves_result_shape()
    {
        var bytes = CreatePng(240, 20, 30);
        var image = CreateImage("images/frame.png", bytes, "workspace_analyze_image");
        var artifactService = new FakeWorkspaceArtifactToolService(image);
        var analysisService = new FakeAgentImageAnalysisService
        {
            Result = new AgentImageAnalysisResult("provider-model", "A red pixel is visible.", 17, 9)
        };
        var provider = CreateProvider();
        var plugin = CreatePlugin(artifactService, analysisService, provider);

        var result = await plugin.AnalyzeImageFile("images/frame.png", "What is visible?");

        Assert.True(result.Succeeded, result.Diagnostics);
        Assert.Equal("images/frame.png", result.Path);
        Assert.Equal(provider.Name, result.ProviderName);
        Assert.Equal("gpt-4o", result.Model);
        Assert.Equal("A red pixel is visible.", result.Analysis);
        Assert.Equal(17, result.InputTokens);
        Assert.Equal(9, result.OutputTokens);
        Assert.Same(image.Receipt, result.Receipt);
        Assert.Contains("Analyzed image 'images/frame.png'", result.Message, StringComparison.Ordinal);
        var request = Assert.Single(analysisService.Requests);
        Assert.Same(provider, request.Provider);
        Assert.Equal("gpt-4o", request.Model);
        Assert.Contains("User question: What is visible?", request.Prompt, StringComparison.Ordinal);
        Assert.Equal(request.Prompt, result.Prompt);
        Assert.Equal("""{"modelParameters":{"numPredict":512}}""", request.ModelParameterConfigurationJson);
        var source = Assert.Single(request.Sources);
        Assert.Equal("frame.png", source.Name);
        Assert.Equal("image/png", source.ContentType);
        Assert.Same(bytes, source.Bytes);
    }

    [Fact]
    public async Task AnalyzeImageFiles_preserves_ordered_sources_and_deterministic_pixel_evidence()
    {
        var before = CreateImage("images/before.png", CreatePng(240, 20, 30), "workspace_analyze_images");
        var after = CreateImage("images/after.png", CreatePng(20, 40, 230), "workspace_analyze_images");
        var artifactService = new FakeWorkspaceArtifactToolService(before, after);
        var analysisService = new FakeAgentImageAnalysisService
        {
            Result = new AgentImageAnalysisResult("provider-model", "The visible color changes.", 31, 12)
        };
        var provider = CreateProvider();
        var plugin = CreatePlugin(artifactService, analysisService, provider);

        var result = await plugin.AnalyzeImageFiles(
            ["images/before.png", "images/after.png"],
            "Compare the frames.");

        Assert.True(result.Succeeded, result.Diagnostics);
        Assert.Equal(["images/before.png", "images/after.png"], result.Paths);
        Assert.Equal(2, result.Images.Count);
        Assert.Equal("The visible color changes.", result.Analysis);
        Assert.Equal(31, result.InputTokens);
        Assert.Equal(12, result.OutputTokens);
        Assert.Contains("Analyzed 2 image(s)", result.Message, StringComparison.Ordinal);
        var request = Assert.Single(analysisService.Requests);
        Assert.Equal(["01-before.png", "02-after.png"], request.Sources.Select(source => source.Name));
        Assert.Contains("Tool-computed pixel evidence from the image files", request.Prompt, StringComparison.Ordinal);
        Assert.Contains("Frame 1 file: before.png", request.Prompt, StringComparison.Ordinal);
        Assert.Contains("Frame 2 file: after.png", request.Prompt, StringComparison.Ordinal);
        Assert.Contains("changed pixels", request.Prompt, StringComparison.Ordinal);
        Assert.Contains("User question: Compare the frames.", request.Prompt, StringComparison.Ordinal);
        Assert.Equal(request.Prompt, result.Prompt);
        Assert.Collection(
            artifactService.ReadRequests,
            request => Assert.Equal(("images/before.png", "workspace_analyze_images"), request),
            request => Assert.Equal(("images/after.png", "workspace_analyze_images"), request));
    }

    [Theory]
    [InlineData(WorkspaceImageAnalysisOperation.Single)]
    [InlineData(WorkspaceImageAnalysisOperation.Multiple)]
    public async Task Analyze_images_leaves_unexpected_provider_failure_opaque(
        WorkspaceImageAnalysisOperation operation)
    {
        var image = CreateImage("images/frame.png", CreatePng(240, 20, 30), "workspace_analyze_image");
        var artifactService = new FakeWorkspaceArtifactToolService(image);
        var sentinel = $@"provider-private-detail C:\private\{Guid.NewGuid():N}";
        var expected = new InvalidOperationException(sentinel);
        var analysisService = new FakeAgentImageAnalysisService
        {
            Handler = (_, _) => Task.FromException<AgentImageAnalysisResult>(
                expected)
        };
        var plugin = CreatePlugin(artifactService, analysisService, CreateProvider());

        var exception = await Record.ExceptionAsync(InvokeAsync);

        Assert.Same(expected, exception);
        Assert.Contains(sentinel, exception!.Message, StringComparison.Ordinal);
        Assert.False(MafAgentToolFailureMapper.TryMap(exception, out _));
        Assert.Single(analysisService.Requests);

        async Task InvokeAsync()
        {
            if (operation == WorkspaceImageAnalysisOperation.Single)
            {
                _ = await plugin.AnalyzeImageFile("images/frame.png", "Analyze.");
                return;
            }

            _ = await plugin.AnalyzeImageFiles(["images/frame.png"], "Analyze.");
        }
    }

    [Fact]
    public async Task AnalyzeImageFile_preserves_artifact_transformation_access_check()
    {
        var artifactService = new FakeWorkspaceArtifactToolService();
        var analysisService = new FakeAgentImageAnalysisService();
        var plugin = CreatePlugin(
            artifactService,
            analysisService,
            CreateProvider(),
            AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.ReadOnly));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            plugin.AnalyzeImageFile("images/frame.png", "Analyze."));

        Assert.Contains("not allowed to transform workspace artifacts", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(artifactService.ReadRequests);
        Assert.Empty(analysisService.Requests);
    }

    [Fact]
    public async Task AnalyzeImageFiles_empty_paths_returns_existing_failure_without_invoking_service()
    {
        var artifactService = new FakeWorkspaceArtifactToolService();
        var analysisService = new FakeAgentImageAnalysisService();
        var plugin = CreatePlugin(artifactService, analysisService, CreateProvider());

        var result = await plugin.AnalyzeImageFiles([], "Compare.");

        Assert.False(result.Succeeded);
        Assert.Equal("At least one image path is required.", result.Diagnostics);
        Assert.Empty(result.Images);
        Assert.Empty(result.Paths);
        Assert.Empty(artifactService.ReadRequests);
        Assert.Empty(analysisService.Requests);
    }

    [Fact]
    public void Workspace_plugin_and_composer_do_not_locate_or_fallback_image_analysis_services()
    {
        var root = FindRepositoryRoot();
        var pluginSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MAF",
            "Common",
            "CanDoItAll.AgentFramework.Maf",
            "Runtime",
            "Workspace",
            "WorkspaceRuntimePlugin.cs"));
        var composerSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MAF",
            "Common",
            "CanDoItAll.AgentFramework.Maf",
            "Runtime",
            "Capabilities",
            "RuntimeCapabilityComposer.cs"));

        Assert.DoesNotContain(nameof(IMafProviderRuntimeGateway), pluginSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RunProviderImageChatAsync", pluginSource, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(UnavailableAgentImageAnalysisService), pluginSource, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(UnavailableAgentImageAnalysisService), composerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("GetService(typeof(IAgentImageAnalysisService))", composerSource, StringComparison.Ordinal);
        Assert.Contains("IAgentImageAnalysisService imageAnalysisService", composerSource, StringComparison.Ordinal);
    }

    private static WorkspaceRuntimePlugin CreatePlugin(
        IWorkspaceArtifactToolService artifactService,
        IAgentImageAnalysisService analysisService,
        ProviderProfile provider,
        AgentWorkspaceToolAccessSettings? accessSettings = null)
    {
        var root = Path.GetTempPath();
        return new WorkspaceRuntimePlugin(
            TestWorkspaceServices.CreateCommandExecutionService(root, new LocalWorkspaceProcessHost()),
            artifactService,
            root,
            TestWorkspaceServices.PhysicalPathPolicyFactory,
            WorkspaceScopeDescriptor.Sandbox,
            accessSettings ?? AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.QualityValidation),
            provider,
            "gpt-4o",
            analysisService);
    }

    private static ProviderProfile CreateProvider()
        => new(
            Guid.NewGuid(),
            "Vision Provider",
            ProviderKind.OpenAi,
            "https://provider.example.test",
            "PROVIDER_API_KEY",
            "gpt-4o",
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: false,
            SupportsTools: false,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: ["gpt-4o"],
            Purpose: ProviderProfilePurpose.Chat);

    private static WorkspaceImageContentResult CreateImage(
        string path,
        byte[] bytes,
        string operation)
    {
        var now = DateTimeOffset.UtcNow;
        return new WorkspaceImageContentResult(
            Succeeded: true,
            Message: "PNG image loaded for analysis.",
            Receipt: new WorkspaceToolReceipt(
                operation,
                MutatesWorkspace: false,
                "workspace",
                "Succeeded",
                "loaded",
                string.Empty,
                [path],
                [],
                now,
                now),
            Path: path,
            Format: "PNG",
            ContentType: "image/png",
            SizeBytes: bytes.Length,
            Width: 1,
            Height: 1,
            Bytes: bytes,
            Diagnostics: string.Empty);
    }

    private static byte[] CreatePng(byte red, byte green, byte blue)
    {
        using var output = new MemoryStream();
        output.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), 1);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), 1);
        header[8] = 8;
        header[9] = 6;
        WritePngChunk(output, "IHDR", header);

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write([0, red, green, blue, 255]);
        }

        WritePngChunk(output, "IDAT", compressed.ToArray());
        WritePngChunk(output, "IEND", []);
        return output.ToArray();
    }

    private static void WritePngChunk(Stream output, string chunkType, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);
        output.Write(Encoding.ASCII.GetBytes(chunkType));
        output.Write(data);
        output.Write([0, 0, 0, 0]);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "CanDoItAll.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName
            ?? throw new InvalidOperationException("Repository root was not found.");
    }

    private sealed class FakeWorkspaceArtifactToolService(
        params WorkspaceImageContentResult[] images) : IWorkspaceArtifactToolService
    {
        private readonly IReadOnlyDictionary<string, WorkspaceImageContentResult> images = images
            .ToDictionary(image => image.Path, StringComparer.OrdinalIgnoreCase);

        public List<(string Path, string OperationName)> ReadRequests { get; } = [];

        public Task<WorkspaceDocumentConversionResult> ConvertDocumentToMarkdown(
            string path,
            string? outputPath = null,
            int previewCharacters = 4000,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

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
        {
            ReadRequests.Add((path, operationName));
            return Task.FromResult(images[path]);
        }
    }

    private sealed class FakeAgentImageAnalysisService : IAgentImageAnalysisService
    {
        public Func<
            AgentImageAnalysisRequest,
            CancellationToken,
            Task<AgentImageAnalysisResult>>? Handler { get; init; }

        public AgentImageAnalysisResult Result { get; init; } = new("vision-model", "analysis", 1, 2);

        public List<AgentImageAnalysisRequest> Requests { get; } = [];

        public Task<AgentImageAnalysisResult> AnalyzeAsync(
            AgentImageAnalysisRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Handler is null
                ? Task.FromResult(Result)
                : Handler(request, cancellationToken);
        }
    }

    public enum WorkspaceImageAnalysisOperation
    {
        Single,
        Multiple
    }
}
