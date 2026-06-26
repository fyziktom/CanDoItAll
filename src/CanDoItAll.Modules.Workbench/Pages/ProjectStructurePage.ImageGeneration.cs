using System.Text;
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
    private IProjectStructureDeferredNodeCompletionQueue DeferredNodeCompletionQueue { get; set; } = default!;

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
            var fileName = BuildGeneratedImageFileName(request.Title, settings.Format);
            var completionRequest = new ProjectStructureGeneratedImageCompletionRequest(
                settings.Provider.Id,
                settings.Model,
                settings.Prompt,
                settings.Size,
                settings.Quality,
                settings.Format,
                fileName);
            var operationId = Guid.NewGuid();
            var created = await CreateObjectAsync(definition, request with
            {
                UploadedFile = BuildGeneratedImageWaitingPlaceholderUpload(),
                ObjectSubtype = definition.ObjectSubtype,
                Title = string.IsNullOrWhiteSpace(request.Title) ? definition.DefaultTitle : request.Title,
                Notes = settings.Prompt
            }, createRequest => createRequest with
            {
                MetadataJson = ProjectStructureDeferredCompletionMetadataFactory.BuildGeneratedImageMetadataJson(
                    operationId,
                    ProjectStructureDeferredNodeCompletionState.Queued,
                    completionRequest,
                    settings.Provider),
                Status = "Image generation queued"
            });
            if (created is null)
            {
                throw new InvalidOperationException("Generated image placeholder node could not be created.");
            }

            var handle = await DeferredNodeCompletionQueue.EnqueueAsync(
                new ProjectStructureDeferredNodeCompletionRequest(
                    operationId,
                    ProjectId,
                    created.Id,
                    ProjectStructureDeferredNodeCompletionKind.GeneratedImageAsset,
                    completionRequest));
            _ = ObserveDeferredNodeCompletionAsync(handle.Completion, created.Id, deferredCompletionCts.Token);

            workflowFeedback = $"{created.Title} was added. Image generation is running through {settings.Provider.Name}.";
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

    private async Task ObserveDeferredNodeCompletionAsync(
        Task<ProjectStructureDeferredNodeCompletionResult> completionTask,
        string nodeId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await completionTask.WaitAsync(cancellationToken);
            if (!string.Equals(result.NodeId, nodeId, StringComparison.Ordinal))
            {
                return;
            }

            await InvokeAsync(async () =>
            {
                if (surface is not null &&
                    result.UpdatedNode is not null &&
                    surface.Nodes.Any(node => string.Equals(node.Id, result.UpdatedNode.Id, StringComparison.Ordinal)))
                {
                    await ApplySurfaceNodeUpdatesAsync([result.UpdatedNode]);
                }

                workflowFeedback = result.Message;
                workflowFeedbackTone = result.IsSuccess ? "mint" : "warn";
                StateHasChanged();
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Logger.LogWarning(
                exception,
                "Project structure deferred completion observation failed. ProjectId={ProjectId} NodeId={NodeId}",
                ProjectId,
                nodeId);
        }
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

    private static CanvasWorkbenchUploadedFile BuildGeneratedImageWaitingPlaceholderUpload()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="1024" height="1024" viewBox="0 0 1024 1024">
              <rect width="1024" height="1024" rx="64" fill="#f8fafc"/>
              <rect x="96" y="96" width="832" height="832" rx="48" fill="#e0f2fe" stroke="#0f766e" stroke-width="8"/>
              <circle cx="512" cy="390" r="92" fill="#ffffff" opacity="0.92"/>
              <path d="M512 314v76l52 30" fill="none" stroke="#0f172a" stroke-width="22" stroke-linecap="round" stroke-linejoin="round"/>
              <text x="512" y="560" text-anchor="middle" font-family="Segoe UI, Arial, sans-serif" font-size="42" font-weight="700" fill="#0f172a">Waiting for Image creation by AI...</text>
              <text x="512" y="622" text-anchor="middle" font-family="Segoe UI, Arial, sans-serif" font-size="28" font-weight="500" fill="#475569">The generated image will replace this placeholder.</text>
            </svg>
            """;

        return new CanvasWorkbenchUploadedFile
        {
            FileName = "waiting-for-image-creation-by-ai.svg",
            ContentType = "image/svg+xml",
            Base64Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(svg))
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
