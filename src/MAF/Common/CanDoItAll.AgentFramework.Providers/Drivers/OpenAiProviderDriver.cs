using System.Buffers.Binary;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Providers;

public sealed class OpenAiProviderDriver(HttpClient httpClient, IProviderDriverCredentialResolver credentialResolver) :
    IProviderHealthDriver,
    IProviderModelCatalogDriver,
    IProviderChatCompletionDriver,
    IProviderImageGenerationDriver,
    IProviderSpeechToTextDriver,
    IProviderTextToSpeechDriver
{
    private const long MaxAudioBytes = 25L * 1024L * 1024L;
    private static readonly IReadOnlySet<AgentProviderCapabilityKind> SupportedCapabilities = new HashSet<AgentProviderCapabilityKind>
    {
        AgentProviderCapabilityKind.Health,
        AgentProviderCapabilityKind.ModelCatalog,
        AgentProviderCapabilityKind.ChatCompletion,
        AgentProviderCapabilityKind.ImageGeneration,
        AgentProviderCapabilityKind.SpeechToText,
        AgentProviderCapabilityKind.TextToSpeech
    };

    public ProviderKind ProviderKind => ProviderKind.OpenAi;

    public IReadOnlySet<AgentProviderCapabilityKind> Capabilities => SupportedCapabilities;

    public ProviderDispatchLimits GetDispatchLimits(ProviderDispatchQuery query)
    {
        return ProviderDispatchLimits.Unbatched(ResolveTimeout(query.Provider));
    }

    public async Task<ProviderHealthResult> TestHealthAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var models = await ListModelsAsync(
                new ProviderModelCatalogRequest(provider, AgentProviderCapabilityKind.ChatCompletion),
                cancellationToken).ConfigureAwait(false);
            var suggestedModels = models.Select(model => model.Model).ToList();
            var model = ResolveHealthModel(provider, suggestedModels, "gpt-4o-mini");
            var result = await CompleteChatAsync(
                CreateHealthChatRequest(provider, model),
                cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(result.ResponseText))
            {
                return new ProviderHealthResult(false, "OpenAI health check returned an empty chat response.", suggestedModels);
            }

            return new ProviderHealthResult(
                true,
                $"OpenAI model catalog returned {models.Count} model(s) and completed a chat probe with model '{model}'.",
                suggestedModels);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new ProviderHealthResult(false, $"OpenAI health check failed: {exception.Message}", provider.SuggestedModels);
        }
    }

    public async Task<IReadOnlyList<ProviderModelDescriptor>> ListModelsAsync(
        ProviderModelCatalogRequest request,
        CancellationToken cancellationToken = default)
    {
        var credential = ResolveCredential(request.Provider);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, BuildEndpoint(request.Provider, "models"));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.ApiKey);
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        await ProviderDriverProtocol.EnsureSuccessAsync(response, "OpenAI model catalog", cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var models = document.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array
            ? data.EnumerateArray()
                .Select(item => ProviderDriverJson.ReadString(item, "id"))
                .Where(model => !string.IsNullOrWhiteSpace(model))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(model => new ProviderModelDescriptor(
                    model,
                    model,
                    request.Capability,
                    ProviderDispatchLimits.Unbatched(ResolveTimeout(request.Provider))))
                .ToList()
            : [];
        return models;
    }

    public async Task<ProviderChatCompletionResult> CompleteChatAsync(
        ProviderChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Provider.Transport == ProviderTransportKind.Responses)
        {
            return await CompleteResponseAsync(request, cancellationToken).ConfigureAwait(false);
        }

        var credential = ResolveCredential(request.Provider);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint(request.Provider, "chat/completions"));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.ApiKey);
        var payload = new Dictionary<string, object?>
        {
            ["model"] = request.Model,
            ["messages"] = ProviderDriverProtocol.BuildOpenAiChatMessages(request),
            ["stream"] = false
        };
        AddChatCompletionModelParameters(payload, request);
        httpRequest.Content = JsonContent.Create(
            payload,
            options: ProviderDriverJson.Options);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        await ProviderDriverProtocol.EnsureSuccessAsync(response, "OpenAI chat completion", cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var choice = document.RootElement.GetProperty("choices")[0].GetProperty("message");
        var usage = document.RootElement.TryGetProperty("usage", out var usageElement)
            ? usageElement
            : default;
        return new ProviderChatCompletionResult(
            request.Model,
            ProviderDriverJson.ReadString(choice, "content"),
            usage.ValueKind == JsonValueKind.Object ? ProviderDriverJson.ReadInt(usage, "prompt_tokens") : 0,
            usage.ValueKind == JsonValueKind.Object ? ProviderDriverJson.ReadInt(usage, "completion_tokens") : 0);
    }

    private async Task<ProviderChatCompletionResult> CompleteResponseAsync(
        ProviderChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var credential = ResolveCredential(request.Provider);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint(request.Provider, "responses"));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.ApiKey);
        var payload = new Dictionary<string, object?>
        {
            ["model"] = request.Model,
            ["input"] = ProviderDriverProtocol.BuildOpenAiResponsesInput(request),
            ["stream"] = false,
            ["store"] = false
        };
        AddResponsesModelParameters(payload, request);
        httpRequest.Content = JsonContent.Create(payload, options: ProviderDriverJson.Options);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        await ProviderDriverProtocol.EnsureSuccessAsync(response, "OpenAI response", cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        EnsureResponseCompleted(root);
        var usage = root.TryGetProperty("usage", out var usageElement)
            ? usageElement
            : default;
        return new ProviderChatCompletionResult(
            request.Model,
            ReadResponseText(root),
            usage.ValueKind == JsonValueKind.Object ? ProviderDriverJson.ReadInt(usage, "input_tokens") : 0,
            usage.ValueKind == JsonValueKind.Object ? ProviderDriverJson.ReadInt(usage, "output_tokens") : 0);
    }

    private static void AddChatCompletionModelParameters(
        IDictionary<string, object?> payload,
        ProviderChatCompletionRequest request)
    {
        var reasoningEffort = AgentProviderModelParameterPolicy.ResolveReasoningEffort(
            request.Provider.Kind,
            request.Provider.Transport,
            request.Model,
            request.Provider.ConfigurationJson,
            request.ModelParameterConfigurationJson);
        if (reasoningEffort is not null)
        {
            payload["reasoning_effort"] = AgentProviderModelParameterPolicy.FormatReasoningEffort(reasoningEffort.Value);
        }

        var maxOutputTokens = AgentProviderModelParameterPolicy.ResolveMaxOutputTokens(
            request.Provider.Kind,
            request.Model,
            request.Provider.ConfigurationJson,
            request.ModelParameterConfigurationJson);
        if (maxOutputTokens is not null)
        {
            payload["max_completion_tokens"] = maxOutputTokens.Value;
        }
    }

    private static void AddResponsesModelParameters(
        IDictionary<string, object?> payload,
        ProviderChatCompletionRequest request)
    {
        var reasoningEffort = AgentProviderModelParameterPolicy.ResolveReasoningEffort(
            request.Provider.Kind,
            request.Provider.Transport,
            request.Model,
            request.Provider.ConfigurationJson,
            request.ModelParameterConfigurationJson);
        if (reasoningEffort is not null)
        {
            payload["reasoning"] = new Dictionary<string, object>
            {
                ["effort"] = AgentProviderModelParameterPolicy.FormatReasoningEffort(reasoningEffort.Value)
            };
        }

        var maxOutputTokens = AgentProviderModelParameterPolicy.ResolveMaxOutputTokens(
            request.Provider.Kind,
            request.Model,
            request.Provider.ConfigurationJson,
            request.ModelParameterConfigurationJson);
        if (maxOutputTokens is not null)
        {
            payload["max_output_tokens"] = maxOutputTokens.Value;
        }
    }

    private static string ReadResponseText(JsonElement root)
    {
        var directText = ProviderDriverJson.ReadString(root, "output_text");
        if (!string.IsNullOrWhiteSpace(directText))
        {
            return directText;
        }

        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        foreach (var outputItem in output.EnumerateArray())
        {
            if (!string.Equals(
                    ProviderDriverJson.ReadString(outputItem, "type"),
                    "message",
                    StringComparison.Ordinal) ||
                !outputItem.TryGetProperty("content", out var content) ||
                content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var contentItem in content.EnumerateArray())
            {
                var value = ProviderDriverJson.ReadString(contentItem, "type") switch
                {
                    "output_text" => ProviderDriverJson.ReadString(contentItem, "text"),
                    "refusal" => ProviderDriverJson.ReadString(contentItem, "refusal"),
                    _ => string.Empty
                };
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (text.Length > 0)
                {
                    text.AppendLine();
                }

                text.Append(value);
            }
        }

        return text.ToString();
    }

    private static void EnsureResponseCompleted(JsonElement root)
    {
        var status = ProviderDriverJson.ReadString(root, "status");
        if (string.Equals(status, "completed", StringComparison.Ordinal))
        {
            return;
        }

        var detail = status switch
        {
            "failed" => ReadNestedResponseDetail(root, "error", "message"),
            "incomplete" => ReadNestedResponseDetail(root, "incomplete_details", "reason"),
            _ => string.Empty
        };
        var detailSuffix = string.IsNullOrWhiteSpace(detail)
            ? string.Empty
            : $": {detail}";
        var statusLabel = string.IsNullOrWhiteSpace(status)
            ? "missing"
            : status;
        throw new InvalidOperationException($"OpenAI response ended with status '{statusLabel}'{detailSuffix}");
    }

    private static string ReadNestedResponseDetail(
        JsonElement root,
        string objectPropertyName,
        string detailPropertyName)
    {
        if (!root.TryGetProperty(objectPropertyName, out var container) ||
            container.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        var detail = ProviderDriverJson.ReadString(container, detailPropertyName).Trim();
        return detail.Length <= 800
            ? detail
            : detail[..800];
    }

    public async Task<ProviderImageGenerationResult> GenerateImageAsync(
        ProviderImageGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureImageProviderPurpose(request.Provider);
        var credential = ResolveCredential(request.Provider);
        using var httpRequest = request.Sources.Count == 0
            ? CreateImageGenerationRequest(request, credential)
            : CreateImageEditRequest(request, credential);
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        await ProviderDriverProtocol.EnsureSuccessAsync(response, "OpenAI image generation", cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var images = document.RootElement.GetProperty("data")
            .EnumerateArray()
            .Select(item => ReadImage(item, request.Format))
            .ToList();
        if (images.Count == 0)
        {
            throw new InvalidOperationException("OpenAI image generation completed without image data.");
        }

        return new ProviderImageGenerationResult(request.Model, request.Format, images);
    }

    public async Task<ProviderSpeechToTextResult> TranscribeSpeechAsync(
        ProviderSpeechToTextRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureVoiceProviderPurpose(request.Provider, "speech-to-text");
        if (request.Audio.Count == 0)
        {
            throw new InvalidOperationException("At least one audio item is required for speech transcription.");
        }

        var firstAudio = request.Audio[0];
        if (firstAudio.Bytes.Length == 0)
        {
            throw new InvalidOperationException("OpenAI transcription audio is empty.");
        }

        if (firstAudio.Bytes.Length > MaxAudioBytes)
        {
            throw new InvalidOperationException("OpenAI transcription audio exceeds the 25 MB API limit.");
        }

        var credential = ResolveCredential(request.Provider);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint(request.Provider, "audio/transcriptions"));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.ApiKey);
        using var form = new MultipartFormDataContent();
        AddFormString(form, "model", request.Model);
        AddFormString(form, "response_format", "json");
        if (!string.IsNullOrWhiteSpace(request.Language))
        {
            AddFormString(form, "language", request.Language);
        }

        if (!string.IsNullOrWhiteSpace(request.Prompt))
        {
            AddFormString(form, "prompt", request.Prompt);
        }

        var content = new ByteArrayContent(firstAudio.Bytes);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(firstAudio.ContentType);
        form.Add(content, "file", firstAudio.FileName);
        httpRequest.Content = form;

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        await ProviderDriverProtocol.EnsureSuccessAsync(response, "OpenAI speech transcription", cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new ProviderSpeechToTextResult(request.Model, ProviderDriverJson.ReadString(document.RootElement, "text"));
    }

    public async Task<ProviderTextToSpeechResult> SynthesizeSpeechAsync(
        ProviderTextToSpeechRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureVoiceProviderPurpose(request.Provider, "text-to-speech");
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            throw new InvalidOperationException("OpenAI text-to-speech input text is required.");
        }

        var credential = ResolveCredential(request.Provider);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint(request.Provider, "audio/speech"));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.ApiKey);
        var body = new Dictionary<string, object>
        {
            ["model"] = request.Model,
            ["input"] = request.Text,
            ["voice"] = request.VoiceId,
            ["response_format"] = request.ResponseFormat
        };
        if (!string.IsNullOrWhiteSpace(request.Instructions))
        {
            body["instructions"] = request.Instructions.Trim();
        }

        httpRequest.Content = JsonContent.Create(body, options: ProviderDriverJson.Options);
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        await ProviderDriverProtocol.EnsureSuccessAsync(response, "OpenAI speech synthesis", cancellationToken).ConfigureAwait(false);
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (bytes.Length == 0)
        {
            throw new InvalidOperationException("OpenAI text-to-speech returned an empty audio payload.");
        }

        var responseFormat = NormalizeResponseFormat(request.ResponseFormat);
        var contentType = ResolveSpeechContentType(responseFormat, response.Content.Headers.ContentType?.MediaType);
        var playbackBytes = string.Equals(responseFormat, "pcm", StringComparison.OrdinalIgnoreCase)
            ? WrapPcmAsWav(bytes)
            : bytes;
        return new ProviderTextToSpeechResult(request.Model, request.VoiceId, responseFormat, contentType, playbackBytes);
    }

    private static HttpRequestMessage CreateImageGenerationRequest(
        ProviderImageGenerationRequest request,
        ProviderDriverCredential credential)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint(request.Provider, "images/generations"));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.ApiKey);
        var payload = new Dictionary<string, object>
        {
            ["model"] = request.Model,
            ["prompt"] = request.Prompt,
            ["n"] = 1,
            ["size"] = request.Size,
            ["quality"] = request.Quality,
            ["output_format"] = FormatToApiValue(request.Format)
        };
        if (request.OutputCompression is int outputCompression)
        {
            payload["output_compression"] = outputCompression;
        }

        httpRequest.Content = JsonContent.Create(
            payload,
            options: ProviderDriverJson.Options);
        return httpRequest;
    }

    private static HttpRequestMessage CreateImageEditRequest(
        ProviderImageGenerationRequest request,
        ProviderDriverCredential credential)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint(request.Provider, "images/edits"));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.ApiKey);
        var form = new MultipartFormDataContent();
        AddFormString(form, "model", request.Model);
        AddFormString(form, "prompt", request.Prompt);
        AddFormString(form, "n", "1");
        AddFormString(form, "size", request.Size);
        AddFormString(form, "quality", request.Quality);
        AddFormString(form, "output_format", FormatToApiValue(request.Format));
        if (request.OutputCompression is int outputCompression)
        {
            AddFormString(form, "output_compression", outputCompression.ToString(CultureInfo.InvariantCulture));
        }
        foreach (var source in request.Sources)
        {
            var content = new ByteArrayContent(source.Bytes);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(source.ContentType);
            form.Add(content, "image[]", source.Name);
        }

        httpRequest.Content = form;
        return httpRequest;
    }

    private static ProviderGeneratedImage ReadImage(JsonElement item, ProviderGeneratedImageFormat format)
    {
        var base64 = ProviderDriverJson.ReadString(item, "b64_json");
        if (string.IsNullOrWhiteSpace(base64))
        {
            throw new InvalidOperationException("OpenAI image response did not contain base64 image data.");
        }

        try
        {
            return new ProviderGeneratedImage(
                FormatToContentType(format),
                Convert.FromBase64String(base64),
                ProviderDriverJson.ReadString(item, "revised_prompt"));
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("OpenAI image generation returned invalid base64 image data.", exception);
        }
    }

    private ProviderDriverCredential ResolveCredential(ProviderProfile provider)
    {
        var credential = credentialResolver.Resolve(provider);
        if (!credential.IsResolved)
        {
            throw new InvalidOperationException(credential.FailureMessage);
        }

        return credential;
    }

    private static ProviderChatCompletionRequest CreateHealthChatRequest(
        ProviderProfile provider,
        string model)
    {
        return new ProviderChatCompletionRequest(
            provider,
            model,
            "Reply with a short confirmation.",
            [],
            "Reply with the single word OK.");
    }

    private static string ResolveHealthModel(
        ProviderProfile provider,
        IReadOnlyList<string> suggestedModels,
        string fallbackModel)
    {
        if (!string.IsNullOrWhiteSpace(provider.DefaultModel))
        {
            return provider.DefaultModel.Trim();
        }

        return suggestedModels.FirstOrDefault(model => !string.IsNullOrWhiteSpace(model))?.Trim()
            ?? fallbackModel;
    }

    private static string BuildEndpoint(ProviderProfile provider, string relativePath)
    {
        var baseUrl = string.IsNullOrWhiteSpace(provider.BaseUrl)
            ? "https://api.openai.com/v1"
            : provider.BaseUrl.Trim().TrimEnd('/');
        const string ModelsSuffix = "/models";
        if (baseUrl.EndsWith(ModelsSuffix, StringComparison.OrdinalIgnoreCase))
        {
            baseUrl = baseUrl[..^ModelsSuffix.Length];
        }

        return baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? $"{baseUrl}/{relativePath.TrimStart('/')}"
            : $"{baseUrl}/v1/{relativePath.TrimStart('/')}";
    }

    private static TimeSpan ResolveTimeout(ProviderProfile provider)
    {
        if (string.IsNullOrWhiteSpace(provider.ConfigurationJson))
        {
            return TimeSpan.FromMinutes(2);
        }

        try
        {
            using var document = JsonDocument.Parse(provider.ConfigurationJson);
            return document.RootElement.TryGetProperty("timeoutSeconds", out var timeout) &&
                   timeout.TryGetInt32(out var seconds)
                ? TimeSpan.FromSeconds(Math.Clamp(seconds, 5, 3600))
                : TimeSpan.FromMinutes(2);
        }
        catch (JsonException)
        {
            return TimeSpan.FromMinutes(2);
        }
    }

    private static string FormatToApiValue(ProviderGeneratedImageFormat format)
    {
        return format switch
        {
            ProviderGeneratedImageFormat.Jpeg => "jpeg",
            ProviderGeneratedImageFormat.Webp => "webp",
            _ => "png"
        };
    }

    private static string FormatToContentType(ProviderGeneratedImageFormat format)
    {
        return format switch
        {
            ProviderGeneratedImageFormat.Jpeg => "image/jpeg",
            ProviderGeneratedImageFormat.Webp => "image/webp",
            _ => "image/png"
        };
    }

    private static void AddFormString(
        MultipartFormDataContent form,
        string name,
        string value)
    {
        form.Add(new StringContent(value, Encoding.UTF8), name);
    }

    private static void EnsureVoiceProviderPurpose(ProviderProfile provider, string capabilityName)
    {
        if (provider.Purpose != ProviderProfilePurpose.Chat)
        {
            throw new InvalidOperationException(
                $"OpenAI {capabilityName} requires an enabled OpenAI chat provider profile. Provider '{provider.Name}' is '{provider.Kind}' with purpose '{provider.Purpose}'.");
        }
    }

    private static void EnsureImageProviderPurpose(ProviderProfile provider)
    {
        if (provider.Purpose != ProviderProfilePurpose.ImageGeneration)
        {
            throw new InvalidOperationException(
                $"OpenAI image generation requires an enabled OpenAI image-generation provider profile. Provider '{provider.Name}' is '{provider.Kind}' with purpose '{provider.Purpose}'.");
        }
    }

    private static string NormalizeResponseFormat(string responseFormat)
    {
        return string.IsNullOrWhiteSpace(responseFormat)
            ? "mp3"
            : responseFormat.Trim();
    }

    private static string ResolveSpeechContentType(
        string responseFormat,
        string? responseContentType)
    {
        if (!string.IsNullOrWhiteSpace(responseContentType) &&
            !string.Equals(responseContentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return responseContentType.Trim();
        }

        return responseFormat.ToLowerInvariant() switch
        {
            "wav" => "audio/wav",
            "opus" => "audio/ogg; codecs=opus",
            "aac" => "audio/aac",
            "flac" => "audio/flac",
            "pcm" => "audio/wav",
            _ => "audio/mpeg"
        };
    }

    private static byte[] WrapPcmAsWav(byte[] pcmBytes)
    {
        const short channelCount = 1;
        const int sampleRate = 24000;
        const short bitsPerSample = 16;
        const short audioFormatPcm = 1;
        const int headerLength = 44;

        var wavBytes = new byte[headerLength + pcmBytes.Length];
        WriteAscii(wavBytes, 0, "RIFF");
        BinaryPrimitives.WriteInt32LittleEndian(wavBytes.AsSpan(4, 4), 36 + pcmBytes.Length);
        WriteAscii(wavBytes, 8, "WAVE");
        WriteAscii(wavBytes, 12, "fmt ");
        BinaryPrimitives.WriteInt32LittleEndian(wavBytes.AsSpan(16, 4), 16);
        BinaryPrimitives.WriteInt16LittleEndian(wavBytes.AsSpan(20, 2), audioFormatPcm);
        BinaryPrimitives.WriteInt16LittleEndian(wavBytes.AsSpan(22, 2), channelCount);
        BinaryPrimitives.WriteInt32LittleEndian(wavBytes.AsSpan(24, 4), sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(wavBytes.AsSpan(28, 4), sampleRate * channelCount * bitsPerSample / 8);
        BinaryPrimitives.WriteInt16LittleEndian(wavBytes.AsSpan(32, 2), (short)(channelCount * bitsPerSample / 8));
        BinaryPrimitives.WriteInt16LittleEndian(wavBytes.AsSpan(34, 2), bitsPerSample);
        WriteAscii(wavBytes, 36, "data");
        BinaryPrimitives.WriteInt32LittleEndian(wavBytes.AsSpan(40, 4), pcmBytes.Length);
        Buffer.BlockCopy(pcmBytes, 0, wavBytes, headerLength, pcmBytes.Length);
        return wavBytes;
    }

    private static void WriteAscii(byte[] target, int offset, string value)
    {
        Encoding.ASCII.GetBytes(value, target.AsSpan(offset, value.Length));
    }
}
