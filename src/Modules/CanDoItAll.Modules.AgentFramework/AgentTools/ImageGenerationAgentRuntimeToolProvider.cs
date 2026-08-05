using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class ImageGenerationAgentRuntimeToolProvider : IAgentRuntimeToolProvider
{
    private const int ProviderOrder = 950;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly ProviderProfileService ProviderFeatureService = new();

    private readonly IAgentImageGenerationService imageGenerationService;
    private readonly IWorkspacePathResolutionService workspacePaths;
    private readonly ImageGenerationToolBuilder toolBuilder;

    public ImageGenerationAgentRuntimeToolProvider(
        IProviderRuntimeProfileSource providerSource,
        IWorkspacePathResolutionService workspacePaths,
        IAgentImageGenerationService imageGenerationService,
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(providerSource);
        ArgumentNullException.ThrowIfNull(workspacePaths);
        ArgumentNullException.ThrowIfNull(imageGenerationService);
        ArgumentNullException.ThrowIfNull(services);

        this.imageGenerationService = imageGenerationService;
        this.workspacePaths = workspacePaths;

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
                    "Generates one image through the agent's allowed image-generation provider and writes the generated binary to a managed workspace path. To prepare a canonical project-asset attachment, supply projectAssetTarget with the exact projectId and parentNodeKey. The result then contains a strongly typed projectAssetCreateDraft for a separate project_structure_asset_create call. Image generation never mutates project structure itself, and the asset tool must be independently attached and authorized.")
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
            var projectAssetTarget = NormalizeProjectAssetTarget(normalizedAccess, request.ProjectAssetTarget);
            var provider = await ResolveImageProviderAsync(normalizedAccess, request.ProviderProfileId, runtimeProvider, cancellationToken);
            var model = ResolveImageModel(provider, normalizedAccess, request.Model);
            var providerConfiguration = ReadProviderConfiguration(provider);
            var size = NormalizeOption(request.Size, providerConfiguration.DefaultSize, "1024x1024", ValidImageSizes, "image size");
            var quality = NormalizeOption(request.Quality, providerConfiguration.DefaultQuality, "low", ValidImageQualities, "image quality");
            var outputFormat = NormalizeOption(request.OutputFormat, providerConfiguration.DefaultOutputFormat, "png", ValidImageOutputFormats, "image output format");
            var outputPath = owner.ResolveImageGenerationOutputPath(request.OutputWorkspacePath, outputFormat);
            var sourceImages = await ResolveSourceImagesAsync(agent, request, cancellationToken);
            var generated = await owner.imageGenerationService.GenerateAsync(
                new AgentImageGenerationRequest(
                    provider,
                    model,
                    request.Prompt.Trim(),
                    size,
                    quality,
                    ParseOutputFormat(outputFormat),
                    sourceImages),
                cancellationToken);
            var generatedImage = generated.Images.FirstOrDefault()
                ?? throw new InvalidOperationException("Image generation completed without image data.");
            var imageBytes = generatedImage.Bytes;
            var contentType = string.IsNullOrWhiteSpace(generatedImage.ContentType)
                ? ResolveOutputContentType(outputFormat)
                : generatedImage.ContentType.Trim();

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath.FullPath)!);
            await File.WriteAllBytesAsync(outputPath.FullPath, imageBytes, cancellationToken);
            var projectAssetCreateDraft = BuildProjectAssetCreateDraft(
                projectAssetTarget,
                outputPath,
                contentType);

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
                ProjectAssetCreateDraft: projectAssetCreateDraft,
                ProjectAssetStorageInstruction: ResolveProjectAssetStorageInstruction(normalizedAccess, projectAssetCreateDraft));
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
            ImageGenerationProjectAssetCreateDraft? projectAssetCreateDraft)
        {
            if (!access.CanStoreImagesAsProjectAssets)
            {
                return "Project asset storage is not enabled for this agent. Keep this generated image in the managed workspace until image project-asset storage is enabled.";
            }

            if (projectAssetCreateDraft is null)
            {
                return "To register this image as a managed project asset, identify the exact projectId and canonical parentNodeKey, then call project_structure_asset_create with the generated outputWorkspacePath as sourceWorkspacePath. Image generation itself does not mutate project structure.";
            }

            return "Submit projectAssetCreateDraft.projectId and projectAssetCreateDraft.request unchanged to project_structure_asset_create. That separate mutation tool must be attached and authorized; image-generation storage access does not grant project-structure write authority.";
        }

        private static ImageGenerationProjectAssetTarget? NormalizeProjectAssetTarget(
            AgentImageGenerationAccessSettings access,
            ImageGenerationProjectAssetTarget? target)
        {
            if (target is null)
            {
                return null;
            }

            if (!access.CanStoreImagesAsProjectAssets)
            {
                throw new ImageGenerationToolException(
                    "ProjectAssetStorageDenied",
                    "This agent is not allowed to prepare generated images for project-asset storage. Enable image project-asset storage or omit projectAssetTarget.",
                    canRetryWithCorrectedInput: false);
            }

            if (target.ProjectId == Guid.Empty)
            {
                throw new ImageGenerationToolException(
                    "ProjectAssetTargetInvalid",
                    "A generated-image project asset target requires a non-empty projectId.",
                    canRetryWithCorrectedInput: true);
            }

            if (string.IsNullOrWhiteSpace(target.ParentNodeKey))
            {
                throw new ImageGenerationToolException(
                    "ProjectAssetParentRequired",
                    "A generated-image project asset target requires an explicit canonical parentNodeKey. Use project:{projectId} for the project root or an existing canonical node id.",
                    canRetryWithCorrectedInput: true);
            }

            if (string.IsNullOrWhiteSpace(target.Title))
            {
                throw new ImageGenerationToolException(
                    "ProjectAssetTitleRequired",
                    "A generated-image project asset target requires a title.",
                    canRetryWithCorrectedInput: true);
            }

            return target with
            {
                ParentNodeKey = target.ParentNodeKey.Trim(),
                Title = target.Title.Trim(),
                Subtitle = target.Subtitle?.Trim(),
                Notes = target.Notes?.Trim(),
                ObjectSubtype = string.IsNullOrWhiteSpace(target.ObjectSubtype)
                    ? "generated"
                    : target.ObjectSubtype.Trim(),
                LeaseToken = string.IsNullOrWhiteSpace(target.LeaseToken)
                    ? null
                    : target.LeaseToken.Trim()
            };
        }

        private static ImageGenerationProjectAssetCreateDraft? BuildProjectAssetCreateDraft(
            ImageGenerationProjectAssetTarget? target,
            ImageGenerationOutputPath outputPath,
            string contentType)
        {
            if (target is null)
            {
                return null;
            }

            return new ImageGenerationProjectAssetCreateDraft(
                target.ProjectId,
                new ProjectStructureAgentAssetCreateInput(
                    ProjectObjectType.ImageAsset,
                    target.Title,
                    target.Subtitle ?? string.Empty,
                    target.Notes ?? string.Empty,
                    Media: null,
                    ParentNodeKey: target.ParentNodeKey,
                    ObjectSubtype: target.ObjectSubtype,
                    LeaseToken: target.LeaseToken,
                    SourceWorkspacePath: outputPath.RelativePath,
                    SourceFileName: Path.GetFileName(outputPath.FullPath),
                    SourceContentType: contentType));
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
        Exception? innerException = null) : InvalidOperationException(message, innerException), IAgentToolFailure
    {
        public string ErrorCode { get; } = errorCode;

        public string SafeMessage => Message;

        public bool IsSafeToExpose => true;

        public bool CanRetryWithCorrectedInput { get; } = canRetryWithCorrectedInput;
    }

    private WorkspaceImagePathResolution ResolveWorkspaceImagePath(string path)
    {
        WorkspaceResolvedPath resolution;
        try
        {
            resolution = workspacePaths.ResolveFilePath(path, allowMissing: false);
        }
        catch (WorkspacePathResolutionException exception)
        {
            throw CreateSourceImagePathFailure(exception);
        }

        if (!resolution.IsWorkspacePath)
        {
            throw new ImageGenerationToolException(
                "ImageSourcePathOutsideWorkspace",
                "Source images must be inside the active workspace scope. Choose an existing workspace image and retry.",
                canRetryWithCorrectedInput: true);
        }

        var extension = Path.GetExtension(resolution.FullPath).ToLowerInvariant();
        if (extension is not ".png" and not ".jpg" and not ".jpeg" and not ".webp")
        {
            throw new ImageGenerationToolException(
                "ImageSourceFormatUnsupported",
                "Source images must be PNG, JPEG, or WEBP files. Choose a supported workspace image and retry.",
                canRetryWithCorrectedInput: true);
        }

        return new WorkspaceImagePathResolution(
            resolution.FullPath,
            NormalizeWorkspaceRelativePath(resolution.RelativePath));
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

        WorkspaceResolvedPath resolution;
        try
        {
            resolution = workspacePaths.ResolveFilePath(normalizedPath, allowMissing: true);
        }
        catch (WorkspacePathResolutionException exception)
        {
            throw CreateImageOutputPathFailure(exception);
        }

        if (!resolution.IsWorkspacePath)
        {
            throw new ImageGenerationToolException(
                "ImageOutputPathOutsideWorkspace",
                "The image output path must be inside the active workspace scope. Choose a workspace file path and retry.",
                canRetryWithCorrectedInput: true);
        }

        if (Directory.Exists(resolution.FullPath))
        {
            throw new ImageGenerationToolException(
                "ImageOutputFileRequired",
                "The image output path identifies a directory, but a file path is required. Choose a workspace file path and retry.",
                canRetryWithCorrectedInput: true);
        }

        return new ImageGenerationOutputPath(
            resolution.FullPath,
            NormalizeWorkspaceRelativePath(resolution.RelativePath));
    }

    private static ImageGenerationToolException CreateSourceImagePathFailure(
        WorkspacePathResolutionException exception)
    {
        var (errorCode, safeMessage) = exception.Kind switch
        {
            WorkspacePathResolutionFailureKind.PathMissing => (
                "ImageSourcePathMissing",
                "The source image path does not identify an existing file. Choose an existing PNG, JPEG, or WEBP workspace image and retry."),
            WorkspacePathResolutionFailureKind.FileRequired => (
                "ImageSourceFileRequired",
                "The source image path identifies a directory, but an image file is required. Choose an existing PNG, JPEG, or WEBP workspace image and retry."),
            WorkspacePathResolutionFailureKind.OutsideWorkspace or
            WorkspacePathResolutionFailureKind.ForeignManagedScope => (
                "ImageSourcePathOutsideWorkspace",
                "Source images must be inside the active workspace scope. Choose an existing workspace image and retry."),
            WorkspacePathResolutionFailureKind.InvalidPath or
            WorkspacePathResolutionFailureKind.DirectoryRequired or
            WorkspacePathResolutionFailureKind.ManagedPathAliasMismatch or
            WorkspacePathResolutionFailureKind.ReparsePointTraversal => (
                "ImageSourcePathInvalid",
                "The source image path is not a valid accessible workspace file path. Choose an existing PNG, JPEG, or WEBP workspace image and retry."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(exception),
                exception.Kind,
                "Unknown workspace path resolution failure kind.")
        };

        return new ImageGenerationToolException(
            errorCode,
            safeMessage,
            canRetryWithCorrectedInput: true,
            exception);
    }

    private static ImageGenerationToolException CreateImageOutputPathFailure(
        WorkspacePathResolutionException exception)
    {
        var (errorCode, safeMessage) = exception.Kind switch
        {
            WorkspacePathResolutionFailureKind.FileRequired => (
                "ImageOutputFileRequired",
                "The image output path identifies a directory, but a file path is required. Choose a workspace file path and retry."),
            WorkspacePathResolutionFailureKind.OutsideWorkspace or
            WorkspacePathResolutionFailureKind.ForeignManagedScope => (
                "ImageOutputPathOutsideWorkspace",
                "The image output path must be inside the active workspace scope. Choose a workspace file path and retry."),
            WorkspacePathResolutionFailureKind.InvalidPath or
            WorkspacePathResolutionFailureKind.DirectoryRequired or
            WorkspacePathResolutionFailureKind.PathMissing or
            WorkspacePathResolutionFailureKind.ManagedPathAliasMismatch or
            WorkspacePathResolutionFailureKind.ReparsePointTraversal => (
                "ImageOutputPathInvalid",
                "The image output path is not a valid accessible workspace file path. Choose a workspace file path and retry."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(exception),
                exception.Kind,
                "Unknown workspace path resolution failure kind.")
        };

        return new ImageGenerationToolException(
            errorCode,
            safeMessage,
            canRetryWithCorrectedInput: true,
            exception);
    }

    private static string NormalizeWorkspaceRelativePath(string path)
    {
        return path.Replace('\\', '/').Trim();
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
    IReadOnlyList<ImageGenerationProjectAssetSource>? SourceProjectAssets = null,
    ImageGenerationProjectAssetTarget? ProjectAssetTarget = null);

public sealed record ImageGenerationProjectAssetSource(
    Guid ProjectId,
    string NodeId);

public sealed record ImageGenerationProjectAssetTarget(
    Guid ProjectId,
    string ParentNodeKey,
    string Title,
    string? Subtitle = null,
    string? Notes = null,
    string ObjectSubtype = "generated",
    string? LeaseToken = null);

public sealed record ImageGenerationProjectAssetCreateDraft(
    Guid ProjectId,
    ProjectStructureAgentAssetCreateInput Request);

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
    ImageGenerationProjectAssetCreateDraft? ProjectAssetCreateDraft,
    string ProjectAssetStorageInstruction);
