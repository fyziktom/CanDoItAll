using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Providers;

public sealed class ComfyUiProviderDriver(HttpClient httpClient) :
    IProviderHealthDriver,
    IProviderImageGenerationDriver
{
    private static readonly IReadOnlySet<AgentProviderCapabilityKind> SupportedCapabilities = new HashSet<AgentProviderCapabilityKind>
    {
        AgentProviderCapabilityKind.Health,
        AgentProviderCapabilityKind.ImageGeneration
    };

    public ProviderKind ProviderKind => ProviderKind.ComfyUi;

    public IReadOnlySet<AgentProviderCapabilityKind> Capabilities => SupportedCapabilities;

    public ProviderDispatchLimits GetDispatchLimits(ProviderDispatchQuery query)
    {
        var options = ComfyUiProviderOptions.FromProvider(query.Provider);
        return ProviderDispatchLimits.Unbatched(TimeSpan.FromSeconds(options.TimeoutSeconds));
    }

    public async Task<ProviderHealthResult> TestHealthAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var baseUrl = NormalizeBaseUrl(provider);
            using var response = await httpClient.GetAsync($"{baseUrl}/system_stats", cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? new ProviderHealthResult(true, "ComfyUI system stats endpoint responded.", [provider.DefaultModel])
                : new ProviderHealthResult(false, $"ComfyUI health check failed with HTTP {(int)response.StatusCode}.", [provider.DefaultModel]);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new ProviderHealthResult(false, $"ComfyUI health check failed: {exception.Message}", [provider.DefaultModel]);
        }
    }

    public async Task<ProviderImageGenerationResult> GenerateImageAsync(
        ProviderImageGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureImageRequest(request);

        var options = ComfyUiProviderOptions.FromProvider(request.Provider);
        options.Validate();
        var workflow = await LoadWorkflowAsync(options, cancellationToken).ConfigureAwait(false);
        ApplyPrompt(workflow, options, request);

        var promptId = await EnqueuePromptAsync(request.Provider, workflow, cancellationToken).ConfigureAwait(false);
        var history = await PollHistoryAsync(request.Provider, options, promptId, cancellationToken).ConfigureAwait(false);
        var imageReferences = ReadImageReferences(history, options, promptId);
        var images = new List<ProviderGeneratedImage>();
        foreach (var imageReference in imageReferences.Take(options.MaxImages))
        {
            images.Add(await DownloadImageAsync(request.Provider, imageReference, request.Format, cancellationToken).ConfigureAwait(false));
        }

        if (images.Count == 0)
        {
            throw new InvalidOperationException($"ComfyUI prompt '{promptId}' completed without image outputs.");
        }

        return new ProviderImageGenerationResult(request.Model, request.Format, images);
    }

    private static void EnsureImageRequest(ProviderImageGenerationRequest request)
    {
        if (request.Provider.Purpose != ProviderProfilePurpose.ImageGeneration)
        {
            throw new InvalidOperationException(
                $"ComfyUI image generation requires an image-generation provider profile. Provider '{request.Provider.Name}' is '{request.Provider.Kind}' with purpose '{request.Provider.Purpose}'.");
        }

        if (request.Provider.Kind != ProviderKind.ComfyUi)
        {
            throw new InvalidOperationException($"ComfyUI image driver cannot handle provider kind '{request.Provider.Kind}'.");
        }

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            throw new InvalidOperationException("ComfyUI image generation requires a prompt.");
        }

        if (request.Sources.Count > 0)
        {
            throw new InvalidOperationException("ComfyUI image edit with source images is not supported by this driver.");
        }
    }

    private static async Task<JsonObject> LoadWorkflowAsync(
        ComfyUiProviderOptions options,
        CancellationToken cancellationToken)
    {
        var workflowJson = options.WorkflowTemplateJson;
        if (string.IsNullOrWhiteSpace(workflowJson) && !string.IsNullOrWhiteSpace(options.WorkflowTemplatePath))
        {
            workflowJson = await File.ReadAllTextAsync(options.WorkflowTemplatePath, cancellationToken).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(workflowJson))
        {
            throw new InvalidOperationException("ComfyUI provider configuration requires a workflow template JSON value or workflow template path.");
        }

        try
        {
            return JsonNode.Parse(workflowJson)?.AsObject()
                   ?? throw new InvalidOperationException("ComfyUI workflow template JSON must be a JSON object.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("ComfyUI workflow template JSON is invalid.", exception);
        }
    }

    private static void ApplyPrompt(
        JsonObject workflow,
        ComfyUiProviderOptions options,
        ProviderImageGenerationRequest request)
    {
        SetRequiredInput(workflow, options.PositivePromptNodeId, options.PositivePromptInputName, request.Prompt.Trim());
        if (!string.IsNullOrWhiteSpace(options.NegativePromptNodeId) &&
            !string.IsNullOrWhiteSpace(options.NegativePrompt))
        {
            SetRequiredInput(workflow, options.NegativePromptNodeId, options.NegativePromptInputName, options.NegativePrompt.Trim());
        }

        if (!string.IsNullOrWhiteSpace(options.SamplerNodeId))
        {
            if (options.Seed.HasValue)
            {
                SetRequiredInput(workflow, options.SamplerNodeId, options.SeedInputName, options.Seed.Value);
            }
            else if (options.RandomizeSeed)
            {
                SetRequiredInput(workflow, options.SamplerNodeId, options.SeedInputName, Random.Shared.NextInt64(1, long.MaxValue));
            }
        }

        if (TryParseImageSize(request.Size, out var width, out var height))
        {
            if (!string.IsNullOrWhiteSpace(options.WidthNodeId))
            {
                SetRequiredInput(workflow, options.WidthNodeId, options.WidthInputName, width);
            }

            if (!string.IsNullOrWhiteSpace(options.HeightNodeId))
            {
                SetRequiredInput(workflow, options.HeightNodeId, options.HeightInputName, height);
            }
        }
    }

    private async Task<string> EnqueuePromptAsync(
        ProviderProfile provider,
        JsonObject workflow,
        CancellationToken cancellationToken)
    {
        var baseUrl = NormalizeBaseUrl(provider);
        using var response = await httpClient.PostAsJsonAsync(
            $"{baseUrl}/prompt",
            new JsonObject { ["prompt"] = workflow },
            ProviderDriverJson.Options,
            cancellationToken).ConfigureAwait(false);
        await ProviderDriverProtocol.EnsureSuccessAsync(response, "ComfyUI prompt enqueue", cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var promptId = ProviderDriverJson.ReadString(document.RootElement, "prompt_id");
        if (string.IsNullOrWhiteSpace(promptId))
        {
            throw new InvalidOperationException("ComfyUI prompt enqueue response did not contain a prompt_id.");
        }

        return promptId;
    }

    private async Task<JsonObject> PollHistoryAsync(
        ProviderProfile provider,
        ComfyUiProviderOptions options,
        string promptId,
        CancellationToken cancellationToken)
    {
        var baseUrl = NormalizeBaseUrl(provider);
        var startedAt = TimeProvider.System.GetTimestamp();
        var timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        while (TimeProvider.System.GetElapsedTime(startedAt) < timeout)
        {
            using var response = await httpClient.GetAsync($"{baseUrl}/history/{Uri.EscapeDataString(promptId)}", cancellationToken).ConfigureAwait(false);
            await ProviderDriverProtocol.EnsureSuccessAsync(response, "ComfyUI history polling", cancellationToken).ConfigureAwait(false);
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var history = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (history is JsonObject historyObject && historyObject.ContainsKey(promptId))
            {
                return historyObject;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(options.PollIntervalMilliseconds), cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException($"ComfyUI prompt '{promptId}' did not complete within {options.TimeoutSeconds} second(s).");
    }

    private static IReadOnlyList<ComfyUiImageReference> ReadImageReferences(
        JsonObject historyRoot,
        ComfyUiProviderOptions options,
        string promptId)
    {
        var promptHistory = historyRoot[promptId]?.AsObject()
            ?? throw new InvalidOperationException($"ComfyUI history for prompt '{promptId}' is invalid.");
        var outputs = promptHistory["outputs"]?.AsObject()
            ?? throw new InvalidOperationException($"ComfyUI history for prompt '{promptId}' does not contain outputs.");
        var references = new List<ComfyUiImageReference>();
        foreach (var outputNode in outputs)
        {
            if (!string.IsNullOrWhiteSpace(options.OutputNodeId) &&
                !string.Equals(outputNode.Key, options.OutputNodeId, StringComparison.Ordinal))
            {
                continue;
            }

            var images = outputNode.Value?["images"]?.AsArray();
            if (images is null)
            {
                continue;
            }

            foreach (var imageNode in images)
            {
                var image = imageNode?.AsObject();
                var filename = image.ReadString("filename");
                if (string.IsNullOrWhiteSpace(filename))
                {
                    continue;
                }

                references.Add(new ComfyUiImageReference(
                    filename,
                    image.ReadString("subfolder"),
                    string.IsNullOrWhiteSpace(image.ReadString("type")) ? "output" : image.ReadString("type")));
            }
        }

        if (references.Count == 0)
        {
            throw new InvalidOperationException($"ComfyUI history for prompt '{promptId}' did not contain downloadable image outputs.");
        }

        return references;
    }

    private async Task<ProviderGeneratedImage> DownloadImageAsync(
        ProviderProfile provider,
        ComfyUiImageReference image,
        ProviderGeneratedImageFormat format,
        CancellationToken cancellationToken)
    {
        var baseUrl = NormalizeBaseUrl(provider);
        var viewUrl =
            $"{baseUrl}/view" +
            $"?filename={WebUtility.UrlEncode(image.FileName)}" +
            $"&subfolder={WebUtility.UrlEncode(image.Subfolder)}" +
            $"&type={WebUtility.UrlEncode(image.Type)}";
        using var response = await httpClient.GetAsync(viewUrl, cancellationToken).ConfigureAwait(false);
        await ProviderDriverProtocol.EnsureSuccessAsync(response, "ComfyUI image download", cancellationToken).ConfigureAwait(false);
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (bytes.Length == 0)
        {
            throw new InvalidOperationException($"ComfyUI image '{image.FileName}' downloaded an empty payload.");
        }

        return new ProviderGeneratedImage(
            ResolveContentType(response.Content.Headers.ContentType?.MediaType, format),
            bytes);
    }

    private static void SetRequiredInput(
        JsonObject workflow,
        string nodeId,
        string inputName,
        JsonNode? value)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new InvalidOperationException("ComfyUI workflow node id is required.");
        }

        if (string.IsNullOrWhiteSpace(inputName))
        {
            throw new InvalidOperationException($"ComfyUI workflow node '{nodeId}' input name is required.");
        }

        var node = workflow[nodeId]?.AsObject()
            ?? throw new InvalidOperationException($"ComfyUI workflow node '{nodeId}' was not found.");
        var inputs = node["inputs"]?.AsObject()
            ?? throw new InvalidOperationException($"ComfyUI workflow node '{nodeId}' does not contain an inputs object.");
        inputs[inputName] = value;
    }

    private static bool TryParseImageSize(
        string size,
        out int width,
        out int height)
    {
        width = 0;
        height = 0;
        if (string.IsNullOrWhiteSpace(size) ||
            string.Equals(size.Trim(), "auto", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parts = size.Split('x', 'X');
        return parts.Length == 2 &&
               int.TryParse(parts[0], out width) &&
               int.TryParse(parts[1], out height) &&
               width > 0 &&
               height > 0;
    }

    private static string NormalizeBaseUrl(ProviderProfile provider)
    {
        if (string.IsNullOrWhiteSpace(provider.BaseUrl))
        {
            throw new InvalidOperationException($"ComfyUI provider '{provider.Name}' requires a base URL.");
        }

        return provider.BaseUrl.Trim().TrimEnd('/');
    }

    private static string ResolveContentType(
        string? responseContentType,
        ProviderGeneratedImageFormat format)
    {
        if (!string.IsNullOrWhiteSpace(responseContentType) &&
            !string.Equals(responseContentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return responseContentType.Trim();
        }

        return format switch
        {
            ProviderGeneratedImageFormat.Jpeg => "image/jpeg",
            ProviderGeneratedImageFormat.Webp => "image/webp",
            _ => "image/png"
        };
    }

    private sealed record ComfyUiImageReference(
        string FileName,
        string Subfolder,
        string Type);
}

public sealed record ComfyUiProviderOptions
{
    public const string WorkflowTemplateJsonKey = "workflowTemplateJson";
    public const string WorkflowTemplatePathKey = "workflowTemplatePath";
    public const string PositivePromptNodeIdKey = "positivePromptNodeId";
    public const string PositivePromptInputNameKey = "positivePromptInputName";
    public const string NegativePromptNodeIdKey = "negativePromptNodeId";
    public const string NegativePromptInputNameKey = "negativePromptInputName";
    public const string NegativePromptKey = "negativePrompt";
    public const string SamplerNodeIdKey = "samplerNodeId";
    public const string SeedInputNameKey = "seedInputName";
    public const string SeedKey = "seed";
    public const string RandomizeSeedKey = "randomizeSeed";
    public const string WidthNodeIdKey = "widthNodeId";
    public const string WidthInputNameKey = "widthInputName";
    public const string HeightNodeIdKey = "heightNodeId";
    public const string HeightInputNameKey = "heightInputName";
    public const string OutputNodeIdKey = "outputNodeId";
    public const string PollIntervalMillisecondsKey = "pollIntervalMilliseconds";
    public const string TimeoutSecondsKey = "timeoutSeconds";
    public const string MaxImagesKey = "maxImages";

    public string WorkflowTemplateJson { get; init; } = string.Empty;

    public string WorkflowTemplatePath { get; init; } = string.Empty;

    public string PositivePromptNodeId { get; init; } = string.Empty;

    public string PositivePromptInputName { get; init; } = "text";

    public string NegativePromptNodeId { get; init; } = string.Empty;

    public string NegativePromptInputName { get; init; } = "text";

    public string NegativePrompt { get; init; } = string.Empty;

    public string SamplerNodeId { get; init; } = string.Empty;

    public string SeedInputName { get; init; } = "seed";

    public long? Seed { get; init; }

    public bool RandomizeSeed { get; init; }

    public string WidthNodeId { get; init; } = string.Empty;

    public string WidthInputName { get; init; } = "width";

    public string HeightNodeId { get; init; } = string.Empty;

    public string HeightInputName { get; init; } = "height";

    public string OutputNodeId { get; init; } = string.Empty;

    public int PollIntervalMilliseconds { get; init; } = 1000;

    public int TimeoutSeconds { get; init; } = 120;

    public int MaxImages { get; init; } = 1;

    public static ComfyUiProviderOptions FromProvider(ProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (string.IsNullOrWhiteSpace(provider.ConfigurationJson))
        {
            return new ComfyUiProviderOptions();
        }

        try
        {
            using var document = JsonDocument.Parse(provider.ConfigurationJson);
            var root = document.RootElement;
            return new ComfyUiProviderOptions
            {
                WorkflowTemplateJson = ReadString(root, WorkflowTemplateJsonKey),
                WorkflowTemplatePath = ReadString(root, WorkflowTemplatePathKey),
                PositivePromptNodeId = ReadString(root, PositivePromptNodeIdKey),
                PositivePromptInputName = ReadString(root, PositivePromptInputNameKey, "text"),
                NegativePromptNodeId = ReadString(root, NegativePromptNodeIdKey),
                NegativePromptInputName = ReadString(root, NegativePromptInputNameKey, "text"),
                NegativePrompt = ReadString(root, NegativePromptKey),
                SamplerNodeId = ReadString(root, SamplerNodeIdKey),
                SeedInputName = ReadString(root, SeedInputNameKey, "seed"),
                Seed = ReadLong(root, SeedKey),
                RandomizeSeed = ReadBool(root, RandomizeSeedKey),
                WidthNodeId = ReadString(root, WidthNodeIdKey),
                WidthInputName = ReadString(root, WidthInputNameKey, "width"),
                HeightNodeId = ReadString(root, HeightNodeIdKey),
                HeightInputName = ReadString(root, HeightInputNameKey, "height"),
                OutputNodeId = ReadString(root, OutputNodeIdKey),
                PollIntervalMilliseconds = ReadInt(root, PollIntervalMillisecondsKey, 1000),
                TimeoutSeconds = ReadInt(root, TimeoutSecondsKey, 120),
                MaxImages = ReadInt(root, MaxImagesKey, 1)
            };
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("ComfyUI provider configuration JSON is invalid.", exception);
        }
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(WorkflowTemplateJson) &&
            string.IsNullOrWhiteSpace(WorkflowTemplatePath))
        {
            throw new InvalidOperationException("ComfyUI provider configuration requires a workflow template JSON value or workflow template path.");
        }

        if (string.IsNullOrWhiteSpace(PositivePromptNodeId))
        {
            throw new InvalidOperationException("ComfyUI provider configuration requires a positive prompt node id.");
        }

        if (PollIntervalMilliseconds is < 100 or > 60000)
        {
            throw new InvalidOperationException("ComfyUI poll interval must be between 100 and 60000 milliseconds.");
        }

        if (TimeoutSeconds is < 1 or > 3600)
        {
            throw new InvalidOperationException("ComfyUI timeout must be between 1 and 3600 seconds.");
        }

        if (MaxImages is < 1 or > 16)
        {
            throw new InvalidOperationException("ComfyUI max images must be between 1 and 16.");
        }
    }

    private static string ReadString(
        JsonElement root,
        string propertyName,
        string fallback = "")
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return fallback;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? fallback
            : value.GetRawText().Trim();
    }

    private static int ReadInt(
        JsonElement root,
        string propertyName,
        int fallback)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return fallback;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var stringNumber)
            ? stringNumber
            : fallback;
    }

    private static long? ReadLong(
        JsonElement root,
        string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out var stringNumber)
            ? stringNumber
            : null;
    }

    private static bool ReadBool(
        JsonElement root,
        string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(value.GetString(), out var result) && result,
            _ => false
        };
    }
}

file static class ComfyUiJsonObjectExtensions
{
    public static string ReadString(
        this JsonObject? item,
        string propertyName)
    {
        if (item is null ||
            item[propertyName] is not JsonValue value ||
            !value.TryGetValue<string>(out var result))
        {
            return string.Empty;
        }

        return result.Trim();
    }
}
