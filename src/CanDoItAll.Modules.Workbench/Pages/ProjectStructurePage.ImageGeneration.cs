using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.CanvasLib;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private static readonly IReadOnlySet<string> ValidGeneratedImageSizes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "auto",
        "1024x1024",
        "1024x1536",
        "1536x1024"
    };

    private static readonly IReadOnlySet<string> ValidGeneratedImageQualities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "auto",
        "low",
        "medium",
        "high"
    };

    private static readonly IReadOnlySet<string> ValidGeneratedImageOutputFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "png",
        "jpeg",
        "webp"
    };

    [Inject]
    private IAgentImageGenerationService ImageGenerationService { get; set; } = default!;

    private IReadOnlyList<ProviderProfile> imageGenerationProviders = [];
    private bool areImageGenerationProvidersLoaded;
    private string imageGenerationProvidersErrorMessage = string.Empty;

    private async Task LoadImageGenerationProvidersAsync()
    {
        areImageGenerationProvidersLoaded = false;

        try
        {
            imageGenerationProviders = (await AgentWorkspaceService.ListProvidersAsync())
                .Where(provider => provider.IsEnabled && provider.Purpose == ProviderProfilePurpose.ImageGeneration)
                .OrderBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            imageGenerationProvidersErrorMessage = string.Empty;
        }
        catch (Exception exception)
        {
            imageGenerationProviders = [];
            imageGenerationProvidersErrorMessage = $"Image-generation providers could not be loaded. {exception.Message}";
            Logger.LogWarning(exception, "Image-generation provider catalog load failed. ProjectId={ProjectId}", ProjectId);
        }
        finally
        {
            areImageGenerationProvidersLoaded = true;
        }
    }

    private async Task EnsureImageGenerationProvidersLoadedAsync(bool refreshIfEmpty = false)
    {
        if (areImageGenerationProvidersLoaded &&
            (!refreshIfEmpty || imageGenerationProviders.Count > 0 || !string.IsNullOrWhiteSpace(imageGenerationProvidersErrorMessage)))
        {
            return;
        }

        await LoadImageGenerationProvidersAsync();
        if (surface is not null)
        {
            RefreshCanvasSurface();
        }
    }

    private async Task<CanvasWorkbenchAction> RefreshGeneratedImageCreateActionAsync(CanvasWorkbenchAction action)
    {
        await EnsureImageGenerationProvidersLoadedAsync(refreshIfEmpty: true);
        return ProjectStructureCanvasCatalog.TryResolveCreateDefinition(action.ActionId, out var definition)
            ? HydrateCreateAction(ProjectStructureCanvasCatalog.BuildComposerAction(definition))
            : HydrateCreateAction(action);
    }

    private IReadOnlyList<CanvasWorkbenchInputOption> BuildImageGenerationProviderOptions()
    {
        return imageGenerationProviders
            .Select(provider => new CanvasWorkbenchInputOption
            {
                Value = provider.Id.ToString("D"),
                Label = $"{provider.Name} ({provider.DefaultModel})"
            })
            .ToList();
    }

    private async Task<bool> TryCreateGeneratedImageAssetAsync(
        ProjectStructureCreateLeafDefinition definition,
        CanvasWorkbenchCreateActionRequest request)
    {
        if (!IsGeneratedImageAssetCreateAction(request.ActionId))
        {
            return false;
        }

        try
        {
            await EnsureImageGenerationProvidersLoadedAsync(refreshIfEmpty: true);
            var settings = ResolveGeneratedImageCreateSettings(request);
            var generated = await ImageGenerationService.GenerateAsync(
                new AgentImageGenerationRequest(
                    settings.Provider,
                    settings.Model,
                    settings.Prompt,
                    settings.Size,
                    settings.Quality,
                    settings.Format,
                    []));
            var image = generated.Images.FirstOrDefault()
                ?? throw new InvalidOperationException("Image generation completed without image data.");
            if (image.Bytes.Length == 0)
            {
                throw new InvalidOperationException("Image generation completed with empty image data.");
            }

            var contentType = string.IsNullOrWhiteSpace(image.ContentType)
                ? ResolveGeneratedImageContentType(settings.Format)
                : image.ContentType.Trim();
            var fileName = BuildGeneratedImageFileName(request.Title, settings.Format);
            var upload = new CanvasWorkbenchUploadedFile
            {
                FileName = fileName,
                ContentType = contentType,
                Base64Data = Convert.ToBase64String(image.Bytes)
            };

            await CreateObjectAsync(definition, request with
            {
                UploadedFile = upload,
                ObjectSubtype = definition.ObjectSubtype,
                Title = string.IsNullOrWhiteSpace(request.Title) ? definition.DefaultTitle : request.Title,
                Notes = settings.Prompt
            });

            workflowFeedback = $"{fileName} was generated through {settings.Provider.Name}.";
            workflowFeedbackTone = "mint";
        }
        catch (Exception exception)
        {
            var message = exception.GetBaseException().Message;
            workflowFeedback = $"Image generation failed. {message}";
            workflowFeedbackTone = "warn";
            Logger.LogWarning(
                exception,
                "Generated image asset creation failed. ProjectId={ProjectId} ActionId={ActionId}",
                ProjectId,
                request.ActionId);
            await InvokeAsync(StateHasChanged);
        }

        return true;
    }

    private GeneratedImageCreateSettings ResolveGeneratedImageCreateSettings(CanvasWorkbenchCreateActionRequest request)
    {
        var inputValues = BuildGeneratedImageInputDictionary(request);
        var providerId = ParseRequiredGeneratedImageProviderId(inputValues);
        var provider = imageGenerationProviders.FirstOrDefault(item => item.Id == providerId)
            ?? throw new InvalidOperationException($"Selected image-generation provider '{providerId:D}' is unavailable or disabled.");
        var prompt = request.Notes?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new InvalidOperationException("Enter an image prompt before generating an image.");
        }

        var model = GetGeneratedImageInputValue(inputValues, ProjectStructureCanvasCatalog.ImageModelFieldKey);
        if (string.IsNullOrWhiteSpace(model))
        {
            model = provider.DefaultModel.Trim();
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException($"Image-generation provider '{provider.Name}' does not define a default model.");
        }

        var size = NormalizeGeneratedImageOption(
            inputValues,
            ProjectStructureCanvasCatalog.ImageSizeFieldKey,
            "1024x1024",
            ValidGeneratedImageSizes,
            "image size");
        var quality = NormalizeGeneratedImageOption(
            inputValues,
            ProjectStructureCanvasCatalog.ImageQualityFieldKey,
            "low",
            ValidGeneratedImageQualities,
            "image quality");
        var outputFormat = NormalizeGeneratedImageOption(
            inputValues,
            ProjectStructureCanvasCatalog.ImageOutputFormatFieldKey,
            "png",
            ValidGeneratedImageOutputFormats,
            "image output format");

        return new GeneratedImageCreateSettings(
            provider,
            model.Trim(),
            prompt,
            size,
            quality,
            ParseGeneratedImageFormat(outputFormat));
    }

    private Guid ParseRequiredGeneratedImageProviderId(IReadOnlyDictionary<string, string> inputValues)
    {
        var providerValue = GetGeneratedImageInputValue(inputValues, ProjectStructureCanvasCatalog.ImageProviderProfileFieldKey);
        if (Guid.TryParse(providerValue, out var providerId) && providerId != Guid.Empty)
        {
            return providerId;
        }

        if (!string.IsNullOrWhiteSpace(imageGenerationProvidersErrorMessage))
        {
            throw new InvalidOperationException(imageGenerationProvidersErrorMessage);
        }

        throw new InvalidOperationException("Select an enabled image-generation provider before generating an image.");
    }

    private static IReadOnlyDictionary<string, string> BuildGeneratedImageInputDictionary(CanvasWorkbenchCreateActionRequest request)
    {
        return (request.InputValues ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .GroupBy(item => item.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Last().Value?.Trim() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
    }

    private static string GetGeneratedImageInputValue(
        IReadOnlyDictionary<string, string> inputValues,
        string key)
    {
        return inputValues.TryGetValue(key, out var value)
            ? value.Trim()
            : string.Empty;
    }

    private static string NormalizeGeneratedImageOption(
        IReadOnlyDictionary<string, string> inputValues,
        string key,
        string fallbackValue,
        IReadOnlySet<string> allowedValues,
        string label)
    {
        var value = GetGeneratedImageInputValue(inputValues, key);
        value = string.IsNullOrWhiteSpace(value) ? fallbackValue : value;
        if (allowedValues.Contains(value))
        {
            return value;
        }

        throw new InvalidOperationException($"Unsupported {label} '{value}'. Allowed values: {string.Join(", ", allowedValues.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))}.");
    }

    private static AgentGeneratedImageFormat ParseGeneratedImageFormat(string outputFormat)
    {
        return outputFormat.Trim().ToLowerInvariant() switch
        {
            "jpeg" => AgentGeneratedImageFormat.Jpeg,
            "webp" => AgentGeneratedImageFormat.Webp,
            _ => AgentGeneratedImageFormat.Png
        };
    }

    private static string ResolveGeneratedImageContentType(AgentGeneratedImageFormat format)
    {
        return format switch
        {
            AgentGeneratedImageFormat.Jpeg => "image/jpeg",
            AgentGeneratedImageFormat.Webp => "image/webp",
            _ => "image/png"
        };
    }

    private static string BuildGeneratedImageFileName(string? title, AgentGeneratedImageFormat format)
    {
        var extension = format switch
        {
            AgentGeneratedImageFormat.Jpeg => ".jpg",
            AgentGeneratedImageFormat.Webp => ".webp",
            _ => ".png"
        };
        var stem = new string((string.IsNullOrWhiteSpace(title) ? "generated-image" : title.Trim())
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray());
        stem = string.Join('-', stem.Split('-', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(stem)
            ? $"generated-image{extension}"
            : $"{stem[..Math.Min(stem.Length, 64)]}{extension}";
    }

    private static bool IsGeneratedImageAssetCreateAction(string? actionId)
        => string.Equals(actionId, ProjectStructureCanvasCatalog.GenerateImageAssetActionId, StringComparison.Ordinal);

    private sealed record GeneratedImageCreateSettings(
        ProviderProfile Provider,
        string Model,
        string Prompt,
        string Size,
        string Quality,
        AgentGeneratedImageFormat Format);
}
