using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Media;

public sealed class ImageGenerationWorkflowExecutor(
    IProviderProfileRegistry providerRegistry,
    IAgentImageGenerationService imageGenerationService,
    IWorkspacePathResolutionService paths) : IWorkflowExecutor
{
    public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.ImageGeneration;

    public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        var settings = WorkflowExecutorJson.Deserialize<WorkflowImageGenerationExecutorSettings>(context.SettingsJson);
        if (string.IsNullOrWhiteSpace(settings.Prompt))
        {
            throw new InvalidOperationException("Image-generation executor setting 'Prompt' is required.");
        }

        if (settings.Operation != WorkflowImageGenerationOperation.Generate)
        {
            throw new InvalidOperationException("Workflow image edit requires source-image settings that are not part of the current workflow image-generation executor contract.");
        }

        var provider = await ResolveProviderAsync(settings.ProviderProfileId, cancellationToken).ConfigureAwait(false);
        var model = ResolveImageModel(provider, settings.Model);
        var providerConfiguration = ReadProviderConfiguration(provider);
        var size = NormalizeOption(settings.Size, providerConfiguration.DefaultSize, "1024x1024", ValidImageSizes, "image size");
        var quality = NormalizeOption(settings.Quality, providerConfiguration.DefaultQuality, "low", ValidImageQualities, "image quality");
        var outputFormat = NormalizeOption(settings.OutputFormat, providerConfiguration.DefaultOutputFormat, "png", ValidImageOutputFormats, "image output format");
        var output = ResolveOutputPath(settings.OutputWorkspacePath, outputFormat);
        var result = await imageGenerationService.GenerateAsync(
            new AgentImageGenerationRequest(
                provider,
                model,
                settings.Prompt.Trim(),
                size,
                quality,
                ParseOutputFormat(outputFormat),
                []),
            cancellationToken).ConfigureAwait(false);
        var image = result.Images.FirstOrDefault()
            ?? throw new InvalidOperationException("Workflow image generation completed without image data.");

        Directory.CreateDirectory(Path.GetDirectoryName(output.FullPath)!);
        await File.WriteAllBytesAsync(output.FullPath, image.Bytes, cancellationToken).ConfigureAwait(false);

        return WorkflowExecutorJson.Result(context, new
        {
            success = true,
            providerProfileId = provider.Id,
            providerName = provider.Name,
            model = result.Model,
            operation = "generation",
            outputWorkspacePath = output.RelativePath,
            contentType = string.IsNullOrWhiteSpace(image.ContentType)
                ? ResolveOutputContentType(outputFormat)
                : image.ContentType.Trim(),
            contentLength = image.Bytes.LongLength,
            size,
            quality,
            outputFormat,
            revisedPrompt = image.RevisedPrompt ?? string.Empty
        });
    }

    private async Task<ProviderProfile> ResolveProviderAsync(
        Guid? providerProfileId,
        CancellationToken cancellationToken)
    {
        var providers = await providerRegistry.ListProvidersAsync(cancellationToken).ConfigureAwait(false);
        var provider = providerProfileId.HasValue
            ? providers.FirstOrDefault(item => item.Id == providerProfileId.Value)
            : providers.FirstOrDefault(item => item.IsEnabled && item.Purpose == ProviderProfilePurpose.ImageGeneration);
        if (provider is null)
        {
            var message = providerProfileId.HasValue
                ? $"Image-generation provider '{providerProfileId.Value:D}' was not found."
                : "No enabled image-generation provider profile is configured.";
            throw new InvalidOperationException(message);
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

        return provider;
    }

    private WorkflowImageOutputPath ResolveOutputPath(
        string outputWorkspacePath,
        string outputFormat)
    {
        if (string.IsNullOrWhiteSpace(outputWorkspacePath))
        {
            throw new InvalidOperationException("Image-generation executor setting 'OutputWorkspacePath' is required.");
        }

        var normalizedPath = outputWorkspacePath.Trim().Replace('\\', '/');
        var extension = "." + outputFormat;
        if (!normalizedPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            normalizedPath += extension;
        }

        var resolved = paths.ResolveFilePath(normalizedPath, allowMissing: true);
        return new WorkflowImageOutputPath(resolved.FullPath, resolved.RelativePath);
    }

    private static string ResolveImageModel(
        ProviderProfile provider,
        string requestedModel)
    {
        var model = string.IsNullOrWhiteSpace(requestedModel)
            ? provider.DefaultModel
            : requestedModel.Trim();
        if (string.IsNullOrWhiteSpace(model))
        {
            model = provider.SuggestedModels.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item))?.Trim()
                ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException($"Image-generation provider '{provider.Name}' does not define a default model.");
        }

        return model;
    }

    private static WorkflowImageGenerationProviderConfiguration ReadProviderConfiguration(ProviderProfile provider)
    {
        if (string.IsNullOrWhiteSpace(provider.ConfigurationJson))
        {
            return new WorkflowImageGenerationProviderConfiguration();
        }

        try
        {
            return WorkflowExecutorJson.Deserialize<WorkflowImageGenerationProviderConfiguration>(provider.ConfigurationJson)
                   ?? new WorkflowImageGenerationProviderConfiguration();
        }
        catch (JsonException)
        {
            return new WorkflowImageGenerationProviderConfiguration();
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

    private static AgentGeneratedImageFormat ParseOutputFormat(string outputFormat)
    {
        return outputFormat.Trim().ToLowerInvariant() switch
        {
            "jpeg" => AgentGeneratedImageFormat.Jpeg,
            "webp" => AgentGeneratedImageFormat.Webp,
            _ => AgentGeneratedImageFormat.Png
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

    private static readonly ProviderProfileService ProviderFeatureService = new();

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

    private sealed record WorkflowImageGenerationProviderConfiguration
    {
        public string DefaultSize { get; init; } = string.Empty;

        public string DefaultQuality { get; init; } = string.Empty;

        public string DefaultOutputFormat { get; init; } = string.Empty;
    }

    private sealed record WorkflowImageOutputPath(
        string FullPath,
        string RelativePath);
}

