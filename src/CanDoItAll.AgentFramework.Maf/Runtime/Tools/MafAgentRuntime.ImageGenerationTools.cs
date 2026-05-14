using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.AgentFramework.Maf;

public sealed partial class MafAgentRuntime
{
    private ImageGenerationToolBuilder CreateImageGenerationToolBuilder()
    {
        return new ImageGenerationToolBuilder(
            this,
            services.GetService<IProviderProfileRegistry>(),
            services.GetService<ProjectStructureAgentService>());
    }

    private sealed class ImageGenerationToolBuilder(
        MafAgentRuntime owner,
        IProviderProfileRegistry? providerRegistry,
        ProjectStructureAgentService? projectStructureAgentService)
    {
        private readonly MafAgentRuntime owner = owner;
        private readonly IProviderProfileRegistry? providerRegistry = providerRegistry;
        private readonly ProjectStructureAgentService? projectStructureAgentService = projectStructureAgentService;

        public IReadOnlyList<AITool> CreateTools(AgentDefinition agent)
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

            if (providerRegistry is null)
            {
                throw new InvalidOperationException("Image generation requires a provider-profile registry.");
            }

            return
            [
                AIFunctionFactory.Create(
                    (ImageGenerationCreateInput request, CancellationToken cancellationToken = default) => ImageGenerationCreateAsync(agent, access, request, cancellationToken),
                    AgentToolInvocationPolicyMetadata.ImageGenerationCreate,
                    "Generates one image through the agent's allowed image-generation provider and writes the generated binary to a managed workspace path. Use project_structure_asset_create afterwards when the image must become a project asset.")
            ];
        }

        private async Task<ImageGenerationCreateResult> ImageGenerationCreateAsync(
            AgentDefinition agent,
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
            var provider = await ResolveImageProviderAsync(normalizedAccess, request.ProviderProfileId, cancellationToken);
            var model = ResolveImageModel(provider, normalizedAccess, request.Model);
            var providerConfiguration = ReadProviderConfiguration(provider);
            var size = NormalizeOption(request.Size, providerConfiguration.DefaultSize, "1024x1024", ValidImageSizes, "image size");
            var quality = NormalizeOption(request.Quality, providerConfiguration.DefaultQuality, "low", ValidImageQualities, "image quality");
            var outputFormat = NormalizeOption(request.OutputFormat, providerConfiguration.DefaultOutputFormat, "png", ValidImageOutputFormats, "image output format");
            var outputPath = owner.ResolveImageGenerationOutputPath(request.OutputWorkspacePath, outputFormat);
            var sourceImages = await ResolveSourceImagesAsync(agent, request, cancellationToken);
            var credential = owner.ResolveProviderCredential(provider);
            if (!credential.IsResolved)
            {
                throw new InvalidOperationException(credential.FailureMessage);
            }

            var imageBytes = sourceImages.Count == 0
                ? await GenerateOpenAiImageAsync(provider, credential, model, request.Prompt.Trim(), size, quality, outputFormat, cancellationToken)
                : await EditOpenAiImageAsync(provider, credential, model, request.Prompt.Trim(), size, quality, outputFormat, sourceImages, cancellationToken);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath.FullPath)!);
            await File.WriteAllBytesAsync(outputPath.FullPath, imageBytes, cancellationToken);

            return new ImageGenerationCreateResult(
                Success: true,
                ProviderProfileId: provider.Id,
                ProviderName: provider.Name,
                Model: model,
                Operation: sourceImages.Count == 0 ? "generation" : "edit",
                OutputWorkspacePath: outputPath.RelativePath,
                ContentType: ResolveOutputContentType(outputFormat),
                ContentLength: imageBytes.LongLength,
                Size: size,
                Quality: quality,
                OutputFormat: outputFormat,
                SourceCount: sourceImages.Count,
                SourceSummaries: sourceImages.Select(item => item.Summary).ToList(),
                ProjectAssetStorageInstruction: $"Call project_structure_asset_create with sourceWorkspacePath '{outputPath.RelativePath}', sourceContentType '{ResolveOutputContentType(outputFormat)}', and sourceFileName '{Path.GetFileName(outputPath.FullPath)}'.");
        }

        private async Task<ProviderProfile> ResolveImageProviderAsync(
            AgentImageGenerationAccessSettings access,
            Guid? requestedProviderId,
            CancellationToken cancellationToken)
        {
            var providers = await providerRegistry!.ListProvidersAsync(cancellationToken);
            var providerId = requestedProviderId ?? access.PreferredProviderProfileId;
            var provider = providerId.HasValue
                ? providers.FirstOrDefault(item => item.Id == providerId.Value)
                : providers.FirstOrDefault(item => item.IsEnabled && item.Purpose == ProviderProfilePurpose.ImageGeneration);

            if (provider is null)
            {
                var reason = providerId.HasValue
                    ? $"Image-generation provider '{providerId.Value:D}' was not found."
                    : "No enabled image-generation provider profile is configured.";
                throw new InvalidOperationException(reason);
            }

            provider = ProviderFeatureService.NormalizeImportedProfile(provider);
            if (!provider.IsEnabled)
            {
                throw new InvalidOperationException($"Image-generation provider '{provider.Name}' is disabled.");
            }

            if (provider.Purpose != ProviderProfilePurpose.ImageGeneration)
            {
                throw new InvalidOperationException($"Provider '{provider.Name}' is not an image-generation provider profile.");
            }

            if (provider.Kind != ProviderKind.OpenAi)
            {
                throw new InvalidOperationException($"Image generation currently supports OpenAI provider profiles only. Provider '{provider.Name}' uses '{provider.Kind}'.");
            }

            return provider;
        }

        private async Task<IReadOnlyList<ImageGenerationSourceImage>> ResolveSourceImagesAsync(
            AgentDefinition agent,
            ImageGenerationCreateInput request,
            CancellationToken cancellationToken)
        {
            var sourceImages = new List<ImageGenerationSourceImage>();
            foreach (var sourcePath in request.SourceWorkspacePaths ?? [])
            {
                if (string.IsNullOrWhiteSpace(sourcePath))
                {
                    continue;
                }

                var resolution = owner.ResolveWorkspaceImagePath(sourcePath);
                var bytes = await File.ReadAllBytesAsync(resolution.FullPath, cancellationToken);
                var fileName = Path.GetFileName(resolution.FullPath);
                sourceImages.Add(new ImageGenerationSourceImage(
                    bytes,
                    fileName,
                    ResolveInputContentType(fileName),
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

                sourceImages.Add(new ImageGenerationSourceImage(
                    bytes,
                    content.Asset.MediaOriginalFileName,
                    content.Asset.MediaContentType,
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

        private static async Task<byte[]> GenerateOpenAiImageAsync(
            ProviderProfile provider,
            ProviderCredentialResolution credential,
            string model,
            string prompt,
            string size,
            string quality,
            string outputFormat,
            CancellationToken cancellationToken)
        {
            using var httpClient = CreateImageGenerationHttpClient(provider, credential);
            using var response = await httpClient.PostAsJsonAsync(
                BuildOpenAiImagesEndpoint(provider, "generations"),
                new
                {
                    model,
                    prompt,
                    n = 1,
                    size,
                    quality,
                    output_format = outputFormat
                },
                SerializerOptions,
                cancellationToken);

            return await ReadOpenAiImageResponseAsync(response, cancellationToken);
        }

        private static async Task<byte[]> EditOpenAiImageAsync(
            ProviderProfile provider,
            ProviderCredentialResolution credential,
            string model,
            string prompt,
            string size,
            string quality,
            string outputFormat,
            IReadOnlyList<ImageGenerationSourceImage> sourceImages,
            CancellationToken cancellationToken)
        {
            using var httpClient = CreateImageGenerationHttpClient(provider, credential);
            using var form = new MultipartFormDataContent();
            AddFormString(form, "model", model);
            AddFormString(form, "prompt", prompt);
            AddFormString(form, "n", "1");
            AddFormString(form, "size", size);
            AddFormString(form, "quality", quality);
            AddFormString(form, "output_format", outputFormat);

            foreach (var sourceImage in sourceImages)
            {
                var content = new ByteArrayContent(sourceImage.Bytes);
                content.Headers.ContentType = MediaTypeHeaderValue.Parse(sourceImage.ContentType);
                form.Add(content, "image[]", sourceImage.FileName);
            }

            using var response = await httpClient.PostAsync(
                BuildOpenAiImagesEndpoint(provider, "edits"),
                form,
                cancellationToken);

            return await ReadOpenAiImageResponseAsync(response, cancellationToken);
        }

        private static HttpClient CreateImageGenerationHttpClient(
            ProviderProfile provider,
            ProviderCredentialResolution credential)
        {
            var httpClient = new HttpClient
            {
                Timeout = ResolveProviderNetworkTimeout(provider)
            };
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credential.ApiKey);
            return httpClient;
        }

        private static async Task<byte[]> ReadOpenAiImageResponseAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var message = TryReadOpenAiErrorMessage(errorContent);
                throw new InvalidOperationException($"OpenAI image generation failed with HTTP {(int)response.StatusCode}: {message}");
            }

            var payload = await response.Content.ReadFromJsonAsync<OpenAiImageResponse>(SerializerOptions, cancellationToken);
            var imageBase64 = payload?.Data.FirstOrDefault()?.B64Json;
            if (string.IsNullOrWhiteSpace(imageBase64))
            {
                throw new InvalidOperationException("OpenAI image generation completed without image data.");
            }

            try
            {
                return Convert.FromBase64String(imageBase64);
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException("OpenAI image generation returned invalid base64 image data.", exception);
            }
        }

        private static string TryReadOpenAiErrorMessage(string errorContent)
        {
            if (string.IsNullOrWhiteSpace(errorContent))
            {
                return "The response body was empty.";
            }

            try
            {
                var envelope = JsonSerializer.Deserialize<OpenAiErrorEnvelope>(errorContent, SerializerOptions);
                if (!string.IsNullOrWhiteSpace(envelope?.Error?.Message))
                {
                    return envelope.Error.Message.Trim();
                }
            }
            catch (JsonException)
            {
            }

            return errorContent.Length <= 800
                ? errorContent.Trim()
                : errorContent[..800].Trim() + "...";
        }

        private static string BuildOpenAiImagesEndpoint(ProviderProfile provider, string endpoint)
        {
            var baseUrl = provider.BaseUrl.Trim().TrimEnd('/');
            if (baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                return $"{baseUrl}/images/{endpoint}";
            }

            return $"{baseUrl}/v1/images/{endpoint}";
        }

        private static void AddFormString(
            MultipartFormDataContent form,
            string name,
            string value)
        {
            form.Add(new StringContent(value), name);
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

        private sealed record ImageGenerationSourceImage(
            byte[] Bytes,
            string FileName,
            string ContentType,
            string Summary);

        private sealed record OpenAiImageResponse
        {
            public List<OpenAiImageData> Data { get; init; } = [];
        }

        private sealed record OpenAiImageData
        {
            [JsonPropertyName("b64_json")]
            public string? B64Json { get; init; }
        }

        private sealed record OpenAiErrorEnvelope(OpenAiError? Error);

        private sealed record OpenAiError(string? Message);
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
