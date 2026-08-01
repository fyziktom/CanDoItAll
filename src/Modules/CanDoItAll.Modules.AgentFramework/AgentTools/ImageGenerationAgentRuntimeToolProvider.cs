using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class ImageGenerationAgentRuntimeToolProvider : IAgentRuntimeToolProvider
{
    private const int ProviderOrder = 950;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly ProviderProfileService ProviderFeatureService = new();

    private readonly IAgentImageGenerationService imageGenerationService;
    private readonly string workspaceRoot;
    private readonly ImageGenerationToolBuilder toolBuilder;

    public ImageGenerationAgentRuntimeToolProvider(
        IProviderRuntimeProfileSource providerSource,
        IWorkspacePathResolver workspacePathResolver,
        IAgentImageGenerationService imageGenerationService,
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(providerSource);
        ArgumentNullException.ThrowIfNull(workspacePathResolver);
        ArgumentNullException.ThrowIfNull(imageGenerationService);
        ArgumentNullException.ThrowIfNull(services);

        this.imageGenerationService = imageGenerationService;
        workspaceRoot = Path.GetFullPath(workspacePathResolver.ResolveWorkspaceRoot());

        toolBuilder = new ImageGenerationToolBuilder(
            this,
            providerSource,
            services.GetService<ProjectStructureAgentService>());
    }

    public int Order => ProviderOrder;

    public AgentRuntimeToolProviderDescriptor Descriptor { get; } = new(
        "image-generation.runtime-tools",
        "Image generation runtime tools",
        "Provides image generation and image editing tools backed by agent image-generation access settings.",
        ["image-generation", "media", "agent-framework"],
        [
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            AgentRuntimeToolProviderPurpose.GovernedProcessAutomation,
            AgentRuntimeToolProviderPurpose.AutoApprovedNonInteractive
        ]);

    public ValueTask<IReadOnlyList<AITool>> CreateToolsAsync(
        AgentRuntimeToolProviderContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(toolBuilder.CreateTools(context.Agent, context.Provider));
    }

    private sealed class ImageGenerationToolBuilder(
        ImageGenerationAgentRuntimeToolProvider owner,
        IProviderRuntimeProfileSource providerSource,
        ProjectStructureAgentService? projectStructureAgentService)
    {
        private readonly ImageGenerationAgentRuntimeToolProvider owner = owner;
        private readonly IProviderRuntimeProfileSource providerSource =
            providerSource;
        private readonly ProjectStructureAgentService? projectStructureAgentService = projectStructureAgentService;

        public IReadOnlyList<AITool> CreateTools(AgentDefinition agent, ProviderProfile runtimeProvider)
        {
            var access = AgentImageGenerationAccessMetadata.Read(agent.ConfigurationJson);
            if (!access.CanGenerateImages)
            {
                return [];
            }

            if (!agent.Permissions.CanUseTools)
            {
                return [];
            }

            return
            [
                AIFunctionFactory.Create(
                    (ImageGenerationCreateInput request, CancellationToken cancellationToken = default) => ImageGenerationCreateAsync(agent, runtimeProvider, access, request, cancellationToken),
                    AgentToolInvocationPolicyMetadata.ImageGenerationCreate,
                    "Generates one image through the agent's allowed image-generation provider and writes the generated binary to a managed workspace path. When project asset storage is enabled, use project_structure_asset_create afterwards to store the image as a project asset.")
            ];
        }

        private async Task<ImageGenerationCreateResult> ImageGenerationCreateAsync(
            AgentDefinition agent,
            ProviderProfile runtimeProvider,
            AgentImageGenerationAccessSettings access,
            ImageGenerationCreateInput request,
            CancellationToken cancellationToken)
        {
            var normalizedAccess = AgentImageGenerationAccessMetadata.Normalize(access);
            if (!normalizedAccess.CanGenerateImages)
            {
                throw new InvalidOperationException("This agent is not allowed to generate images.");
            }

            ValidateRequest(request);
            var provider = await ResolveImageProviderAsync(normalizedAccess, request.ProviderProfileId, runtimeProvider, cancellationToken);
            var model = ResolveImageModel(provider, normalizedAccess, request.Model);
            var providerConfiguration = ReadProviderConfiguration(provider);
            var size = NormalizeOption(request.Size, providerConfiguration.DefaultSize, "1024x1024", ValidImageSizes, "image size");
            var quality = NormalizeOption(request.Quality, providerConfiguration.DefaultQuality, "low", ValidImageQualities, "image quality");
            var outputFormat = NormalizeOption(request.OutputFormat, providerConfiguration.DefaultOutputFormat, "png", ValidImageOutputFormats, "image output format");
            var outputPath = owner.ResolveImageGenerationOutputPath(request.OutputWorkspacePath, outputFormat);
            var sourceImages = await ResolveSourceImagesAsync(agent, request, cancellationToken);
            AgentImageGenerationResult generated;
            try
            {
                generated = await owner.imageGenerationService.GenerateAsync(
                    new AgentImageGenerationRequest(
                        provider,
                        model,
                        request.Prompt.Trim(),
                        size,
                        quality,
                        ParseOutputFormat(outputFormat),
                        sourceImages),
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new ImageGenerationToolException(
                    "ImageGenerationProviderFailed",
                    $"Image-generation provider '{provider.Name}' could not complete the request. Verify that the provider is reachable or select a different enabled image provider, then retry.",
                    canRetryWithCorrectedInput: true,
                    exception);
            }
            var generatedImage = generated.Images.FirstOrDefault()
                ?? throw new InvalidOperationException("Image generation completed without image data.");
            var imageBytes = generatedImage.Bytes;
            var contentType = string.IsNullOrWhiteSpace(generatedImage.ContentType)
                ? ResolveOutputContentType(outputFormat)
                : generatedImage.ContentType.Trim();

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath.FullPath)!);
            await File.WriteAllBytesAsync(outputPath.FullPath, imageBytes, cancellationToken);

            return new ImageGenerationCreateResult(
                Success: true,
                ProviderProfileId: provider.Id,
                ProviderName: provider.Name,
                Model: generated.Model,
                Operation: sourceImages.Count == 0 ? "generation" : "edit",
                OutputWorkspacePath: outputPath.RelativePath,
                ContentType: contentType,
                ContentLength: imageBytes.LongLength,
                Size: size,
                Quality: quality,
                OutputFormat: outputFormat,
                SourceCount: sourceImages.Count,
                SourceSummaries: sourceImages.Select(item => item.Summary).ToList(),
                ProjectAssetStorageInstruction: ResolveProjectAssetStorageInstruction(normalizedAccess, outputPath, outputFormat));
        }

        private async Task<ProviderProfile> ResolveImageProviderAsync(
            AgentImageGenerationAccessSettings access,
            Guid? requestedProviderId,
            ProviderProfile runtimeProvider,
            CancellationToken cancellationToken)
        {
            var providers = (await providerSource.ListProvidersAsync(cancellationToken))
                .Select(ProviderFeatureService.NormalizeImportedProfile)
                .ToList();
            var providerId = requestedProviderId ?? access.PreferredProviderProfileId;
            var provider = providerId.HasValue
                ? providers.FirstOrDefault(item => item.Id == providerId.Value)
                : ImageGenerationProviderSelectionPolicy.ResolveDefault(providers, runtimeProvider);

            if (provider is null)
            {
                var reason = providerId.HasValue
                    ? $"Image-generation provider '{providerId.Value:D}' was not found."
                    : "No enabled image-generation provider profile is configured.";
                throw new InvalidOperationException(reason);
            }

            if (!provider.IsEnabled)
            {
                throw new InvalidOperationException($"Image-generation provider '{provider.Name}' is disabled.");
            }

            if (provider.Purpose != ProviderProfilePurpose.ImageGeneration)
            {
                throw new InvalidOperationException($"Provider '{provider.Name}' is not an image-generation provider profile.");
            }

            return provider;
        }

        private async Task<IReadOnlyList<AgentImageGenerationSource>> ResolveSourceImagesAsync(
            AgentDefinition agent,
            ImageGenerationCreateInput request,
            CancellationToken cancellationToken)
        {
            var sourceImages = new List<AgentImageGenerationSource>();
            foreach (var sourcePath in request.SourceWorkspacePaths ?? [])
            {
                if (string.IsNullOrWhiteSpace(sourcePath))
                {
                    continue;
                }

                var resolution = owner.ResolveWorkspaceImagePath(sourcePath);
                var bytes = await File.ReadAllBytesAsync(resolution.FullPath, cancellationToken);
                var fileName = Path.GetFileName(resolution.FullPath);
                sourceImages.Add(new AgentImageGenerationSource(
                    fileName,
                    ResolveInputContentType(fileName),
                    bytes,
                    $"workspace:{resolution.RelativePath}"));
            }

            foreach (var sourceAsset in request.SourceProjectAssets ?? [])
            {
                EnsureProjectAssetReadAllowed(agent, sourceAsset.ProjectId);
                if (projectStructureAgentService is null)
                {
                    throw new InvalidOperationException("Project asset sources require project-structure services.");
                }

                var content = await projectStructureAgentService.GetAssetContentAsync(
                    sourceAsset.ProjectId,
                    sourceAsset.NodeId,
                    cancellationToken);
                if (!content.Asset.MediaContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Project asset '{sourceAsset.NodeId}' is not an image asset.");
                }

                byte[] bytes;
                try
                {
                    bytes = Convert.FromBase64String(content.Base64Data);
                }
                catch (FormatException exception)
                {
                    throw new InvalidOperationException($"Project asset '{sourceAsset.NodeId}' did not contain valid base64 image content.", exception);
                }

                sourceImages.Add(new AgentImageGenerationSource(
                    content.Asset.MediaOriginalFileName,
                    content.Asset.MediaContentType,
                    bytes,
                    $"project:{sourceAsset.ProjectId:D}/{sourceAsset.NodeId}"));
            }

            return sourceImages;
        }

        private static void EnsureProjectAssetReadAllowed(AgentDefinition agent, Guid projectId)
        {
            var access = AgentProjectStructureAccessMetadata.Normalize(
                AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson));
            if (!access.CanRead)
            {
                throw new InvalidOperationException("This agent is not allowed to read project-structure assets.");
            }

            if (access.AllowAllProjects ||
                access.AllowedProjectIds.Contains(projectId))
            {
                return;
            }

            throw new InvalidOperationException($"Project '{projectId:D}' is outside the agent's allowed project-structure scope.");
        }

        private static string ResolveImageModel(
            ProviderProfile provider,
            AgentImageGenerationAccessSettings access,
            string? requestedModel)
        {
            var model = string.IsNullOrWhiteSpace(requestedModel)
                ? string.IsNullOrWhiteSpace(access.DefaultModel) ? provider.DefaultModel : access.DefaultModel
                : requestedModel.Trim();
            if (string.IsNullOrWhiteSpace(model))
            {
                throw new InvalidOperationException($"Image-generation provider '{provider.Name}' does not define a default model.");
            }

            return model;
        }

        private static ImageGenerationProviderConfiguration ReadProviderConfiguration(ProviderProfile provider)
        {
            if (string.IsNullOrWhiteSpace(provider.ConfigurationJson))
            {
                return new ImageGenerationProviderConfiguration();
            }

            try
            {
                return JsonSerializer.Deserialize<ImageGenerationProviderConfiguration>(provider.ConfigurationJson, SerializerOptions)
                       ?? new ImageGenerationProviderConfiguration();
            }
            catch (JsonException)
            {
                return new ImageGenerationProviderConfiguration();
            }
        }

        private static string NormalizeOption(
            string? requestedValue,
            string? providerDefaultValue,
            string fallbackValue,
            IReadOnlySet<string> allowedValues,
            string label)
        {
            var value = string.IsNullOrWhiteSpace(requestedValue)
                ? string.IsNullOrWhiteSpace(providerDefaultValue) ? fallbackValue : providerDefaultValue.Trim()
                : requestedValue.Trim();
            if (allowedValues.Contains(value))
            {
                return value;
            }

            throw new InvalidOperationException($"Unsupported {label} '{value}'. Allowed values: {string.Join(", ", allowedValues.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))}.");
        }

        private static void ValidateRequest(ImageGenerationCreateInput request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                throw new InvalidOperationException("Image generation requires a prompt.");
            }

            if (string.IsNullOrWhiteSpace(request.OutputWorkspacePath))
            {
                throw new InvalidOperationException("Image generation requires an output workspace path.");
            }
        }

        private static string ResolveProjectAssetStorageInstruction(
            AgentImageGenerationAccessSettings access,
            ImageGenerationOutputPath outputPath,
            string outputFormat)
        {
            if (!access.CanStoreImagesAsProjectAssets)
            {
                return "Project asset storage is not enabled for this agent. Keep this generated image in the managed workspace until image project-asset storage is enabled.";
            }

            return $"Call project_structure_asset_create with sourceWorkspacePath '{outputPath.RelativePath}', sourceContentType '{ResolveOutputContentType(outputFormat)}', and sourceFileName '{Path.GetFileName(outputPath.FullPath)}'.";
        }

        private static string ResolveInputContentType(string fileName)
        {
            return Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
        }

        private static string ResolveOutputContentType(string outputFormat)
        {
            return outputFormat switch
            {
                "jpeg" => "image/jpeg",
                "webp" => "image/webp",
                _ => "image/png"
            };
        }

        private static readonly IReadOnlySet<string> ValidImageSizes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "auto",
            "1024x1024",
            "1024x1536",
            "1536x1024"
        };

        private static readonly IReadOnlySet<string> ValidImageQualities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "auto",
            "low",
            "medium",
            "high"
        };

        private static readonly IReadOnlySet<string> ValidImageOutputFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "png",
            "jpeg",
            "webp"
        };

        private sealed record ImageGenerationProviderConfiguration
        {
            public string DefaultSize { get; init; } = string.Empty;

            public string DefaultQuality { get; init; } = string.Empty;

            public string DefaultOutputFormat { get; init; } = string.Empty;
        }

        private static AgentGeneratedImageFormat ParseOutputFormat(string outputFormat)
        {
            return outputFormat.Trim().ToLowerInvariant() switch
            {
                "jpeg" => AgentGeneratedImageFormat.Jpeg,
                "webp" => AgentGeneratedImageFormat.Webp,
                _ => AgentGeneratedImageFormat.Png
            };
        }
    }

    private sealed class ImageGenerationToolException(
        string errorCode,
        string message,
        bool canRetryWithCorrectedInput,
        Exception innerException) : InvalidOperationException(message, innerException), IAgentToolFailure
    {
        public string ErrorCode { get; } = errorCode;

        public string SafeMessage => Message;

        public bool IsSafeToExpose => true;

        public bool CanRetryWithCorrectedInput { get; } = canRetryWithCorrectedInput;
    }

    private WorkspaceImagePathResolution ResolveWorkspaceImagePath(string path)
    {
        var fullPath = ResolvePathFromWorkspace(path, false);
        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException($"Source image '{path}' was not found.");
        }

        var extension = Path.GetExtension(fullPath).ToLowerInvariant();
        if (extension is not ".png" and not ".jpg" and not ".jpeg" and not ".webp")
        {
            throw new InvalidOperationException($"Source image '{path}' must be a PNG, JPEG, or WEBP file.");
        }

        var relativePath = NormalizeWorkspaceRelativePath(Path.GetRelativePath(workspaceRoot, fullPath));
        return new WorkspaceImagePathResolution(fullPath, relativePath);
    }

    private ImageGenerationOutputPath ResolveImageGenerationOutputPath(
        string outputWorkspacePath,
        string outputFormat)
    {
        var normalizedPath = outputWorkspacePath.Trim().Replace('\\', '/');
        var extension = "." + outputFormat;
        if (!normalizedPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            normalizedPath += extension;
        }

        var fullPath = ResolvePathFromWorkspace(normalizedPath, false);
        if (Directory.Exists(fullPath))
        {
            throw new InvalidOperationException($"Output path '{normalizedPath}' resolves to a directory.");
        }

        var relativePath = NormalizeWorkspaceRelativePath(Path.GetRelativePath(workspaceRoot, fullPath));
        return new ImageGenerationOutputPath(fullPath, relativePath);
    }

    private static string NormalizeWorkspaceRelativePath(string path)
    {
        return path.Replace('\\', '/').Trim();
    }

    private string ResolvePathFromWorkspace(string path, bool allowExternal, IReadOnlyList<string>? allowedExternalRoots = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return workspaceRoot;
        }

        var expandedPath = ExpandPortablePath(path);
        var fullPath = Path.GetFullPath(Path.IsPathRooted(expandedPath) ? expandedPath : Path.Combine(workspaceRoot, expandedPath));
        if (IsPathWithinRoot(fullPath, workspaceRoot))
        {
            return fullPath;
        }

        if (!allowExternal)
        {
            throw new InvalidOperationException($"Path '{path}' resolves outside the workspace root. Use a workspace-relative path or import the external file into chat attachments first.");
        }

        var allowedRoots = allowedExternalRoots?
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(ExpandPortablePath)
            .Select(item => Path.GetFullPath(Path.IsPathRooted(item) ? item : Path.Combine(workspaceRoot, item)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? [];

        if (allowedRoots.Any(allowedRoot => IsPathWithinRoot(fullPath, allowedRoot)))
        {
            return fullPath;
        }

        throw new InvalidOperationException($"Path '{path}' resolves outside the workspace root and is not covered by an explicit external-root allowlist.");
    }

    private static string ExpandPortablePath(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
        if (string.Equals(expanded, "~", StringComparison.Ordinal))
        {
            var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return string.IsNullOrWhiteSpace(homeDirectory)
                ? expanded
                : homeDirectory;
        }

        if (!expanded.StartsWith("~" + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
            !expanded.StartsWith("~" + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
        {
            return expanded;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
        {
            return expanded;
        }

        var relativePath = expanded[2..]
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return Path.Combine(home, relativePath);
    }

    private static bool IsPathWithinRoot(string fullPath, string rootPath)
    {
        var normalizedRoot = Path.GetFullPath(rootPath);
        if (string.Equals(fullPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var rootWithSeparator = normalizedRoot.EndsWith(Path.DirectorySeparatorChar) || normalizedRoot.EndsWith(Path.AltDirectorySeparatorChar)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record WorkspaceImagePathResolution(
        string FullPath,
        string RelativePath);

    private sealed record ImageGenerationOutputPath(
        string FullPath,
        string RelativePath);
}

public sealed record ImageGenerationCreateInput(
    string Prompt,
    string OutputWorkspacePath,
    Guid? ProviderProfileId = null,
    string? Model = null,
    string? Size = null,
    string? Quality = null,
    string? OutputFormat = null,
    IReadOnlyList<string>? SourceWorkspacePaths = null,
    IReadOnlyList<ImageGenerationProjectAssetSource>? SourceProjectAssets = null);

public sealed record ImageGenerationProjectAssetSource(
    Guid ProjectId,
    string NodeId);

public sealed record ImageGenerationCreateResult(
    bool Success,
    Guid ProviderProfileId,
    string ProviderName,
    string Model,
    string Operation,
    string OutputWorkspacePath,
    string ContentType,
    long ContentLength,
    string Size,
    string Quality,
    string OutputFormat,
    int SourceCount,
    IReadOnlyList<string> SourceSummaries,
    string ProjectAssetStorageInstruction);
