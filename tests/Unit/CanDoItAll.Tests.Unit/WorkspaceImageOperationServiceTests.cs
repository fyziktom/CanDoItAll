using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Hosting;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceImageOperationServiceTests
{
    public static TheoryData<string, byte[], string, string, int, int> SupportedImages { get; } = new()
    {
        { "sample.png", PngBytes(), "PNG", "image/png", 2, 3 },
        { "sample.jpg", JpegBytes(), "JPEG", "image/jpeg", 2, 3 },
        { "sample.gif", GifBytes(), "GIF", "image/gif", 2, 3 }
    };

    [Theory]
    [MemberData(nameof(SupportedImages))]
    public async Task InspectImageFile_RecognizesSupportedMetadata(
        string fileName,
        byte[] bytes,
        string expectedFormat,
        string expectedContentType,
        int expectedWidth,
        int expectedHeight)
    {
        var workspaceRoot = CreateWorkspaceRoot();
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(workspaceRoot, fileName), bytes);
            var service = new WorkspaceImageOperationService(workspaceRoot);

            var result = await service.InspectImageFile(fileName);

            Assert.True(result.Succeeded, result.Diagnostics);
            Assert.Equal(expectedFormat, result.Format);
            Assert.Equal(expectedContentType, result.ContentType);
            Assert.Equal(expectedWidth, result.Width);
            Assert.Equal(expectedHeight, result.Height);
            Assert.Equal(bytes.LongLength, result.SizeBytes);
            Assert.Equal("workspace_inspect_image", result.Receipt.Operation);
            Assert.Equal("Succeeded", result.Receipt.Outcome);
            Assert.False(result.Receipt.MutatesWorkspace);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ReadImageFile_ReturnsBytesAndUsesNormalizedOperationName()
    {
        var workspaceRoot = CreateWorkspaceRoot();
        try
        {
            var bytes = PngBytes();
            await File.WriteAllBytesAsync(Path.Combine(workspaceRoot, "frame.png"), bytes);
            var service = new WorkspaceImageOperationService(workspaceRoot);

            var result = await service.ReadImageFile(
                "frame.png",
                maxBytes: bytes.Length,
                operationName: " workspace_analyze_images ");

            Assert.True(result.Succeeded, result.Diagnostics);
            Assert.Equal(bytes, result.Bytes);
            Assert.Equal("PNG", result.Format);
            Assert.Equal(2, result.Width);
            Assert.Equal(3, result.Height);
            Assert.Equal("workspace_analyze_images", result.Receipt.Operation);
            Assert.Equal("Succeeded", result.Receipt.Outcome);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ReadImageFile_OverByteLimitFailsWithoutReadingContent()
    {
        var workspaceRoot = CreateWorkspaceRoot();
        try
        {
            var bytes = PngBytes();
            await File.WriteAllBytesAsync(Path.Combine(workspaceRoot, "large.png"), bytes);
            var service = new WorkspaceImageOperationService(workspaceRoot);

            var result = await service.ReadImageFile("large.png", maxBytes: bytes.Length - 1);

            Assert.False(result.Succeeded);
            Assert.Empty(result.Bytes);
            Assert.Equal(bytes.LongLength, result.SizeBytes);
            Assert.Contains("exceeds", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("workspace_analyze_image", result.Receipt.Operation);
            Assert.Equal("Failed", result.Receipt.Outcome);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task InspectImageFile_PathOutsideWorkspaceIsDenied()
    {
        var workspaceRoot = CreateWorkspaceRoot();
        try
        {
            var service = new WorkspaceImageOperationService(workspaceRoot);

            var result = await service.InspectImageFile("../outside.png");

            Assert.False(result.Succeeded);
            Assert.Equal("Denied", result.Receipt.Outcome);
            Assert.Equal("workspace_inspect_image", result.Receipt.Operation);
            Assert.Contains("outside the workspace root", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Theory]
    [InlineData(WorkspaceImageReadOperation.Inspect)]
    [InlineData(WorkspaceImageReadOperation.Read)]
    public async Task Unexpected_image_io_is_thrown_opaquely_instead_of_returning_native_path_diagnostics(
        WorkspaceImageReadOperation operation)
    {
        var workspaceRoot = CreateWorkspaceRoot();
        try
        {
            var sentinel = $"private-native-{Guid.NewGuid():N}";
            var relativePath = $"images/{sentinel}.png";
            var fullPath = Path.Combine(
                workspaceRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllBytesAsync(fullPath, PngBytes());
            var expected = new IOException($"Unexpected I/O failure at '{fullPath}'.");
            string? observedReadPath = null;
            var service = new WorkspaceImageOperationService(
                workspaceRoot,
                workspaceScope: null,
                readAllBytes: path =>
                {
                    observedReadPath = path;
                    throw expected;
                });

            var exception = await Record.ExceptionAsync(InvokeAsync);

            Assert.Same(expected, exception);
            Assert.Equal(fullPath, observedReadPath);
            Assert.Contains(sentinel, exception!.Message, StringComparison.Ordinal);
            Assert.False(MafAgentToolFailureMapper.TryMap(exception, out _));

            async Task InvokeAsync()
            {
                if (operation == WorkspaceImageReadOperation.Inspect)
                {
                    _ = await service.InspectImageFile(relativePath);
                    return;
                }

                _ = await service.ReadImageFile(relativePath);
            }
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ArtifactService_ImageMethodsDelegateToInjectedOperationService()
    {
        var workspaceRoot = CreateWorkspaceRoot();
        try
        {
            var imageOperations = new RecordingImageOperationService();
            var service = new WorkspaceArtifactToolService(
                workspaceRoot,
                new WorkspaceCommandExecutionService(workspaceRoot, new LocalWorkspaceProcessHost()),
                new UnusedDocumentMarkdownConverter(),
                WorkspaceScopeDescriptor.Sandbox,
                imageOperations);

            var inspection = await service.InspectImageFile("images/frame.png");
            var content = await service.ReadImageFile(
                "images/frame.png",
                maxBytes: 1234,
                operationName: "workspace_analyze_images");

            Assert.Same(imageOperations.InspectionResult, inspection);
            Assert.Same(imageOperations.ContentResult, content);
            Assert.Equal("images/frame.png", imageOperations.InspectedPath);
            Assert.Equal("images/frame.png", imageOperations.ReadPath);
            Assert.Equal(1234, imageOperations.MaxBytes);
            Assert.Equal("workspace_analyze_images", imageOperations.OperationName);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task RuntimeFallbackComposition_UsesRegisteredImageOperationService()
    {
        var workspaceRoot = CreateWorkspaceRoot();
        try
        {
            var imageOperations = new RecordingImageOperationService();
            var services = new ServiceCollection();
            services.AddSingleton<IWorkspaceImageOperationService>(imageOperations);
            await using var provider = services.BuildServiceProvider();
            var resolver = new MafRuntimeDependencyResolver();

            var workspaceServices = resolver.ResolveWorkspaceServices(
                provider,
                workspaceRoot,
                WorkspaceScopeDescriptor.Sandbox);
            var result = await workspaceServices.ArtifactToolService.InspectImageFile("fallback.png");

            Assert.Same(imageOperations.InspectionResult, result);
            Assert.Equal("fallback.png", imageOperations.InspectedPath);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task Composition_UsesWorkspaceAppropriateImageOperationLifetimes()
    {
        var workspaceRoot = CreateWorkspaceRoot();
        try
        {
            var hostingServices = new ServiceCollection();
            hostingServices.AddAgentFrameworkCore(workspaceRoot);
            var hostingDescriptor = Assert.Single(
                hostingServices,
                descriptor => descriptor.ServiceType == typeof(IWorkspaceImageOperationService));
            Assert.Equal(ServiceLifetime.Singleton, hostingDescriptor.Lifetime);

            await using var provider = hostingServices.BuildServiceProvider();
            Assert.Same(
                provider.GetRequiredService<IWorkspaceImageOperationService>(),
                provider.GetRequiredService<IWorkspaceImageOperationService>());

            var moduleServices = new ServiceCollection();
            moduleServices.AddAgentFrameworkModule(new ConfigurationBuilder().Build());
            var moduleDescriptor = Assert.Single(
                moduleServices,
                descriptor => descriptor.ServiceType == typeof(IWorkspaceImageOperationService));
            Assert.Equal(ServiceLifetime.Scoped, moduleDescriptor.Lifetime);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void ArtifactServiceSource_DoesNotOwnImageParsing()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "MAF",
            "Common",
            "CanDoItAll.AgentFramework.Core",
            "Workspace",
            "Tools",
            "WorkspaceArtifactToolService.cs"));

        Assert.DoesNotContain("BinaryPrimitives", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TryReadImageMetadata", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TryReadPngMetadata", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TryReadJpegMetadata", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TryReadGifMetadata", source, StringComparison.Ordinal);
        Assert.DoesNotContain("File.ReadAllBytes", source, StringComparison.Ordinal);
        Assert.Contains("imageOperationService.InspectImageFile(path)", source, StringComparison.Ordinal);
        Assert.Contains("imageOperationService.ReadImageFile(path, maxBytes, operationName)", source, StringComparison.Ordinal);
    }

    private static WorkspaceToolReceipt CreateReceipt(string operation)
    {
        var now = DateTimeOffset.UtcNow;
        return new WorkspaceToolReceipt(
            operation,
            MutatesWorkspace: false,
            Boundary: "workspace",
            Outcome: "Succeeded",
            Message: "recorded",
            ReceiptRelativePath: string.Empty,
            TargetPaths: [],
            ArtifactReferences: [],
            StartedAtUtc: now,
            CompletedAtUtc: now);
    }

    private static byte[] PngBytes()
        =>
        [
            0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x02,
            0x00, 0x00, 0x00, 0x03
        ];

    private static byte[] JpegBytes()
        =>
        [
            0xff, 0xd8,
            0xff, 0xc0,
            0x00, 0x07,
            0x08,
            0x00, 0x03,
            0x00, 0x02
        ];

    private static byte[] GifBytes()
        =>
        [
            0x47, 0x49, 0x46, 0x38, 0x39, 0x61,
            0x02, 0x00,
            0x03, 0x00
        ];

    private static string CreateWorkspaceRoot()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "candoitall-workspace-image-operation-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }

    private sealed class RecordingImageOperationService : IWorkspaceImageOperationService
    {
        public WorkspaceImageInspectionResult InspectionResult { get; } = new(
            Succeeded: true,
            Message: "inspected",
            Receipt: CreateReceipt("workspace_inspect_image"),
            Path: "recorded.png",
            Format: "PNG",
            ContentType: "image/png",
            SizeBytes: 24,
            Width: 2,
            Height: 3,
            Diagnostics: string.Empty);

        public WorkspaceImageContentResult ContentResult { get; } = new(
            Succeeded: true,
            Message: "read",
            Receipt: CreateReceipt("workspace_analyze_image"),
            Path: "recorded.png",
            Format: "PNG",
            ContentType: "image/png",
            SizeBytes: 24,
            Width: 2,
            Height: 3,
            Bytes: PngBytes(),
            Diagnostics: string.Empty);

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

    private sealed class UnusedDocumentMarkdownConverter : IWorkspaceDocumentMarkdownConverter
    {
        public Task<WorkspaceDocumentMarkdownConversionResult> ConvertToMarkdownAsync(
            WorkspaceDocumentMarkdownConversionRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Document conversion was not expected in this test.");
    }

    public enum WorkspaceImageReadOperation
    {
        Inspect,
        Read
    }
}
