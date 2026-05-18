using System.Buffers.Binary;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Voice;

public sealed class OpenAiVoiceDriver : ISpeechToTextVoiceDriver, ITextToSpeechVoiceDriver
{
    private const string DefaultBaseUrl = "https://api.openai.com/v1";
    private const long MaxAudioBytes = 25L * 1024L * 1024L;
    private const int DefaultRequestTimeoutSeconds = 60;
    private const int MinimumRequestTimeoutSeconds = 5;
    private const int MaximumRequestTimeoutSeconds = 300;
    private const string TimeoutSecondsPropertyName = "timeoutSeconds";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient httpClient;
    private readonly IAgentProviderCredentialResolver credentialResolver;

    public OpenAiVoiceDriver(
        HttpClient httpClient,
        IAgentProviderCredentialResolver credentialResolver)
    {
        this.httpClient = httpClient;
        this.credentialResolver = credentialResolver;
    }

    public AgentVoiceDriverKind DriverKind => AgentVoiceDriverKind.OpenAi;

    public async Task<AgentVoiceTranscriptionResult> TranscribeAsync(
        SpeechToTextDriverRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.AudioBytes.Length == 0)
        {
            throw new InvalidOperationException("OpenAI transcription audio is empty.");
        }

        if (request.AudioBytes.Length > MaxAudioBytes)
        {
            throw new InvalidOperationException("OpenAI transcription audio exceeds the 25 MB API limit.");
        }

        var settings = AgentVoiceSettingsNormalizer.NormalizeSpeechToText(request.Settings);
        var credential = ResolveCredential(request.Provider, "speech-to-text");
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            BuildEndpoint(request.Provider.BaseUrl, "audio/transcriptions"));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.ApiKey);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(settings.Model), "model");
        form.Add(new StringContent("json"), "response_format");
        if (!string.IsNullOrWhiteSpace(settings.Language))
        {
            form.Add(new StringContent(settings.Language), "language");
        }

        if (!string.IsNullOrWhiteSpace(settings.Prompt))
        {
            form.Add(new StringContent(settings.Prompt), "prompt");
        }

        var fileContent = new ByteArrayContent(request.AudioBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(request.ContentType);
        form.Add(fileContent, "file", request.FileName);
        httpRequest.Content = form;

        using var timeoutSource = CreateRequestTimeoutSource(request.Provider, cancellationToken);
        try
        {
            using var response = await httpClient.SendAsync(httpRequest, timeoutSource.Token);
            var payload = await response.Content.ReadAsStringAsync(timeoutSource.Token);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"OpenAI speech-to-text failed with HTTP {(int)response.StatusCode}: {TrimForError(payload)}");
            }

            var transcription = JsonSerializer.Deserialize<OpenAiTranscriptionResponse>(payload, JsonOptions);
            if (string.IsNullOrWhiteSpace(transcription?.Text))
            {
                throw new InvalidOperationException("OpenAI speech-to-text response did not include transcription text.");
            }

            return new AgentVoiceTranscriptionResult(transcription.Text.Trim(), settings.Model);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested && timeoutSource.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"OpenAI speech-to-text timed out after {ResolveRequestTimeoutSeconds(request.Provider)} second(s) for provider '{request.Provider.Name}'.",
                exception);
        }
    }

    public async Task<AgentVoiceSynthesisResult> SynthesizeAsync(
        TextToSpeechDriverRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            throw new InvalidOperationException("OpenAI text-to-speech input text is required.");
        }

        var settings = AgentVoiceSettingsNormalizer.NormalizeTextToSpeech(request.Settings);
        var credential = ResolveCredential(request.Provider, "text-to-speech");
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            BuildEndpoint(request.Provider.BaseUrl, "audio/speech"));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.ApiKey);
        httpRequest.Content = JsonContent.Create(new OpenAiSpeechRequest(
            settings.Model,
            request.VoiceId,
            request.Text,
            settings.ResponseFormat,
            string.IsNullOrWhiteSpace(settings.Instructions) ? null : settings.Instructions),
            options: JsonOptions);

        using var timeoutSource = CreateRequestTimeoutSource(request.Provider, cancellationToken);
        try
        {
            using var response = await httpClient.SendAsync(httpRequest, timeoutSource.Token);
            var audioBytes = await response.Content.ReadAsByteArrayAsync(timeoutSource.Token);
            if (!response.IsSuccessStatusCode)
            {
                var errorPayload = audioBytes.Length == 0
                    ? string.Empty
                    : Encoding.UTF8.GetString(audioBytes);
                throw new InvalidOperationException($"OpenAI text-to-speech failed with HTTP {(int)response.StatusCode}: {TrimForError(errorPayload)}");
            }

            if (audioBytes.Length == 0)
            {
                throw new InvalidOperationException("OpenAI text-to-speech returned an empty audio payload.");
            }

            var playbackPayload = PreparePlaybackPayload(settings.ResponseFormat, audioBytes);
            return new AgentVoiceSynthesisResult(
                playbackPayload.AudioBytes,
                playbackPayload.ContentType,
                settings.Model,
                request.VoiceId,
                settings.ResponseFormat);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested && timeoutSource.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"OpenAI text-to-speech timed out after {ResolveRequestTimeoutSeconds(request.Provider)} second(s) for provider '{request.Provider.Name}'.",
                exception);
        }
    }

    private ProviderCredentialResolution ResolveCredential(ProviderProfile provider, string capabilityName)
    {
        var credential = credentialResolver.Resolve(provider);
        if (!credential.IsResolved)
        {
            throw new InvalidOperationException($"OpenAI {capabilityName} provider '{provider.Name}' has no usable credential. {credential.FailureMessage}");
        }

        return credential;
    }

    private static Uri BuildEndpoint(string? baseUrl, string relativePath)
    {
        var normalizedBase = string.IsNullOrWhiteSpace(baseUrl)
            ? DefaultBaseUrl
            : baseUrl.Trim().TrimEnd('/');
        const string ModelsSuffix = "/models";
        if (normalizedBase.EndsWith(ModelsSuffix, StringComparison.OrdinalIgnoreCase))
        {
            normalizedBase = normalizedBase[..^ModelsSuffix.Length];
        }

        return new Uri($"{normalizedBase}/{relativePath.TrimStart('/')}", UriKind.Absolute);
    }

    private static string ResolveContentType(string responseFormat)
    {
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

    private static AudioPlaybackPayload PreparePlaybackPayload(
        string responseFormat,
        byte[] audioBytes)
    {
        var contentType = ResolveContentType(responseFormat);
        if (!string.Equals(responseFormat, "pcm", StringComparison.OrdinalIgnoreCase))
        {
            return new AudioPlaybackPayload(audioBytes, contentType);
        }

        return new AudioPlaybackPayload(WrapPcmAsWav(audioBytes), contentType);
    }

    private static CancellationTokenSource CreateRequestTimeoutSource(
        ProviderProfile provider,
        CancellationToken cancellationToken)
    {
        var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(ResolveRequestTimeoutSeconds(provider)));
        return timeoutSource;
    }

    private static int ResolveRequestTimeoutSeconds(ProviderProfile provider)
    {
        if (string.IsNullOrWhiteSpace(provider.ConfigurationJson))
        {
            return DefaultRequestTimeoutSeconds;
        }

        try
        {
            using var configuration = JsonDocument.Parse(provider.ConfigurationJson);
            if (configuration.RootElement.ValueKind == JsonValueKind.Object &&
                configuration.RootElement.TryGetProperty(TimeoutSecondsPropertyName, out var timeoutProperty) &&
                timeoutProperty.TryGetInt32(out var timeoutSeconds))
            {
                return Math.Clamp(timeoutSeconds, MinimumRequestTimeoutSeconds, MaximumRequestTimeoutSeconds);
            }
        }
        catch (JsonException)
        {
        }

        return DefaultRequestTimeoutSeconds;
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

    private static string TrimForError(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "No response body.";
        }

        var normalized = string.Join(
            ' ',
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        const int maxLength = 500;
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private sealed record OpenAiTranscriptionResponse(
        [property: JsonPropertyName("text")] string Text);

    private sealed record OpenAiSpeechRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("voice")] string Voice,
        [property: JsonPropertyName("input")] string Input,
        [property: JsonPropertyName("response_format")] string ResponseFormat,
        [property: JsonPropertyName("instructions")] string? Instructions);

    private sealed record AudioPlaybackPayload(
        byte[] AudioBytes,
        string ContentType);
}
