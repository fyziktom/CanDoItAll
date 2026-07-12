using System.Runtime.CompilerServices;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Voice;

public sealed class AgentVoiceService(
    IWorkflowSettingsService workflowSettingsService,
    IProviderProfileRegistry providerRegistry,
    IAgentVoiceDriverFactory driverFactory,
    IAgentVoiceSpeechTextPreprocessor speechTextPreprocessor) : IAgentVoiceService
{
    public async Task<AgentVoiceSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var workflowSettings = await workflowSettingsService.GetSettingsAsync(cancellationToken);
        return workflowSettings.NormalizedVoiceSettings;
    }

    public async Task<AgentVoiceSettings> SaveSettingsAsync(
        AgentVoiceSettings settings,
        CancellationToken cancellationToken = default)
    {
        var normalized = AgentVoiceSettingsNormalizer.Normalize(settings);
        var workflowSettings = await workflowSettingsService.GetSettingsAsync(cancellationToken);
        await workflowSettingsService.SaveSettingsAsync(
            workflowSettings with
            {
                VoiceSettings = normalized
            },
            cancellationToken);

        return normalized;
    }

    public async Task<AgentVoiceTranscriptionResult> TranscribeAsync(
        AgentVoiceTranscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var audioChunks = ResolveTranscriptionChunks(request);
        if (audioChunks.Count == 0)
        {
            throw new InvalidOperationException("Speech-to-text input audio is empty.");
        }

        for (var index = 0; index < audioChunks.Count; index++)
        {
            if (audioChunks[index].AudioBytes.Length == 0)
            {
                throw new InvalidOperationException($"Speech-to-text input audio chunk {index + 1} is empty.");
            }
        }

        var settings = (await GetSettingsAsync(cancellationToken)).SpeechToText;
        if (!settings.IsEnabled)
        {
            throw new InvalidOperationException("Speech-to-text is disabled in AgentFramework voice settings.");
        }

        var provider = await ResolveProviderAsync(
            settings.ProviderProfileId,
            "speech-to-text",
            cancellationToken);
        var driver = driverFactory.CreateSpeechToTextDriver(settings.DriverKind);
        if (audioChunks.Count == 1)
        {
            var chunk = audioChunks[0];
            return await driver.TranscribeAsync(
                new SpeechToTextDriverRequest(
                    provider,
                    settings,
                    chunk.AudioBytes,
                    NormalizeFileName(chunk.FileName),
                    NormalizeContentType(chunk.ContentType)),
                cancellationToken);
        }

        var transcriptSegments = new List<string>();
        var models = new List<string>();
        for (var index = 0; index < audioChunks.Count; index++)
        {
            var chunk = audioChunks[index];
            var result = await driver.TranscribeAsync(
                new SpeechToTextDriverRequest(
                    provider,
                    settings,
                    chunk.AudioBytes,
                    NormalizeFileName(chunk.FileName),
                    NormalizeContentType(chunk.ContentType)),
                cancellationToken);
            if (string.IsNullOrWhiteSpace(result.Text))
            {
                throw new InvalidOperationException($"Speech-to-text returned empty text for audio chunk {index + 1}.");
            }

            transcriptSegments.Add(result.Text.Trim());
            if (!models.Contains(result.Model, StringComparer.OrdinalIgnoreCase))
            {
                models.Add(result.Model);
            }
        }

        return new AgentVoiceTranscriptionResult(
            string.Join(Environment.NewLine, transcriptSegments),
            string.Join(", ", models));
    }

    public async Task<AgentVoiceSynthesisResult> SynthesizeAsync(
        AgentVoiceSynthesisRequest request,
        CancellationToken cancellationToken = default)
    {
        var context = await CreateSynthesisContextAsync(request, cancellationToken);

        var result = await context.Driver.SynthesizeAsync(
            new TextToSpeechDriverRequest(
                context.Provider,
                context.Settings,
                context.PreparedText.SpokenText,
                context.VoiceId),
            cancellationToken);

        return result with
        {
            SpokenText = context.PreparedText.SpokenText,
            IdentifiersOmitted = context.PreparedText.IdentifiersOmitted,
            IdentifierOmissionNoticeIncluded = context.PreparedText.IdentifierOmissionNoticeIncluded,
            ChunkIndex = 0,
            ChunkCount = 1
        };
    }

    public async IAsyncEnumerable<AgentVoiceSynthesisResult> SynthesizeChunksAsync(
        AgentVoiceSynthesisRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var context = await CreateSynthesisContextAsync(request, cancellationToken);
        var chunks = AgentVoiceSpeechTextChunker.Split(context.PreparedText.SpokenText);
        if (chunks.Count == 0)
        {
            throw new InvalidOperationException("Text-to-speech input text is required.");
        }

        for (var index = 0; index < chunks.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chunk = chunks[index];
            var result = await context.Driver.SynthesizeAsync(
                new TextToSpeechDriverRequest(
                    context.Provider,
                    context.Settings,
                    chunk,
                    context.VoiceId),
                cancellationToken);

            yield return result with
            {
                SpokenText = chunk,
                IdentifiersOmitted = context.PreparedText.IdentifiersOmitted,
                IdentifierOmissionNoticeIncluded = context.PreparedText.IdentifierOmissionNoticeIncluded && index == 0,
                ChunkIndex = index,
                ChunkCount = chunks.Count
            };
        }
    }

    public async Task<AgentVoiceSynthesisResult> SynthesizeSampleAsync(
        string? sampleText = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        var text = string.IsNullOrWhiteSpace(sampleText)
            ? settings.SampleText
            : sampleText.Trim();
        return await SynthesizeAsync(
            new AgentVoiceSynthesisRequest(text),
            cancellationToken);
    }

    private async Task<ProviderProfile> ResolveProviderAsync(
        Guid? providerProfileId,
        string capabilityName,
        CancellationToken cancellationToken)
    {
        if (!providerProfileId.HasValue)
        {
            throw new InvalidOperationException($"A provider profile must be selected for {capabilityName}.");
        }

        var provider = await providerRegistry.GetProviderAsync(providerProfileId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Provider profile '{providerProfileId.Value:D}' configured for {capabilityName} was not found.");
        if (!provider.IsEnabled)
        {
            throw new InvalidOperationException($"Provider profile '{provider.Name}' configured for {capabilityName} is disabled.");
        }

        return provider;
    }

    private async Task<AgentVoiceSynthesisContext> CreateSynthesisContextAsync(
        AgentVoiceSynthesisRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            throw new InvalidOperationException("Text-to-speech input text is required.");
        }

        var settings = (await GetSettingsAsync(cancellationToken)).TextToSpeech;
        if (!settings.IsEnabled)
        {
            throw new InvalidOperationException("Text-to-speech is disabled in AgentFramework voice settings.");
        }

        if (request.AgentVoiceAccess is not null &&
            !AgentVoiceAccessMetadata.Normalize(request.AgentVoiceAccess).CanUseVoiceMode)
        {
            throw new InvalidOperationException("This agent does not allow voice mode.");
        }

        var provider = await ResolveProviderAsync(
            settings.ProviderProfileId,
            "text-to-speech",
            cancellationToken);
        var driver = driverFactory.CreateTextToSpeechDriver(settings.DriverKind);
        var voiceId = string.IsNullOrWhiteSpace(request.VoiceIdOverride)
            ? AgentVoiceSettingsNormalizer.ResolveEffectiveVoiceId(settings, request.AgentVoiceAccess)
            : request.VoiceIdOverride.Trim();
        var preparedText = speechTextPreprocessor.Prepare(
            request.Text,
            request.SuppressIdentifierOmissionNotice);

        if (string.IsNullOrWhiteSpace(preparedText.SpokenText))
        {
            throw new InvalidOperationException("Text-to-speech prepared speech text is empty.");
        }

        return new AgentVoiceSynthesisContext(
            provider,
            settings,
            driver,
            voiceId,
            preparedText);
    }

    private static IReadOnlyList<AgentVoiceAudioChunk> ResolveTranscriptionChunks(
        AgentVoiceTranscriptionRequest request)
    {
        if (request.AudioChunks.Count > 0)
        {
            return request.AudioChunks
                .Select((chunk, index) => new AgentVoiceAudioChunk(
                    chunk.AudioBytes,
                    NormalizeFileName(string.IsNullOrWhiteSpace(chunk.FileName)
                        ? BuildChunkFileName(request.FileName, index)
                        : chunk.FileName),
                    NormalizeContentType(string.IsNullOrWhiteSpace(chunk.ContentType)
                        ? request.ContentType
                        : chunk.ContentType)))
                .ToList();
        }

        return request.AudioBytes.Length == 0
            ? []
            : [new AgentVoiceAudioChunk(
                request.AudioBytes,
                NormalizeFileName(request.FileName),
                NormalizeContentType(request.ContentType))];
    }

    private static string NormalizeFileName(string fileName)
    {
        var normalized = string.IsNullOrWhiteSpace(fileName)
            ? "voice-input.webm"
            : Path.GetFileName(fileName.Trim());

        return string.IsNullOrWhiteSpace(normalized) ? "voice-input.webm" : normalized;
    }

    private static string NormalizeContentType(string contentType)
    {
        return string.IsNullOrWhiteSpace(contentType)
            ? "audio/webm"
            : contentType.Trim();
    }

    private static string BuildChunkFileName(string fileName, int index)
    {
        var normalized = NormalizeFileName(fileName);
        var extension = Path.GetExtension(normalized);
        var name = Path.GetFileNameWithoutExtension(normalized);
        return string.IsNullOrWhiteSpace(extension)
            ? $"{name}-{index + 1}"
            : $"{name}-{index + 1}{extension}";
    }

    private sealed record AgentVoiceSynthesisContext(
        ProviderProfile Provider,
        AgentTextToSpeechSettings Settings,
        ITextToSpeechVoiceDriver Driver,
        string VoiceId,
        AgentVoiceSpeechTextPreparationResult PreparedText);
}
