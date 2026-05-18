using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

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
        if (request.AudioBytes.Length == 0)
        {
            throw new InvalidOperationException("Speech-to-text input audio is empty.");
        }

        var settings = (await GetSettingsAsync(cancellationToken)).SpeechToText;
        if (!settings.IsEnabled)
        {
            throw new InvalidOperationException("Speech-to-text is disabled in AgentFramework voice settings.");
        }

        var provider = await ResolveProviderAsync(
            settings.ProviderProfileId,
            settings.DriverKind,
            "speech-to-text",
            cancellationToken);
        var driver = driverFactory.CreateSpeechToTextDriver(settings.DriverKind);
        return await driver.TranscribeAsync(
            new SpeechToTextDriverRequest(
                provider,
                settings,
                request.AudioBytes,
                NormalizeFileName(request.FileName),
                NormalizeContentType(request.ContentType)),
            cancellationToken);
    }

    public async Task<AgentVoiceSynthesisResult> SynthesizeAsync(
        AgentVoiceSynthesisRequest request,
        CancellationToken cancellationToken = default)
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
            settings.DriverKind,
            "text-to-speech",
            cancellationToken);
        var driver = driverFactory.CreateTextToSpeechDriver(settings.DriverKind);
        var voiceId = string.IsNullOrWhiteSpace(request.VoiceIdOverride)
            ? AgentVoiceSettingsNormalizer.ResolveEffectiveVoiceId(settings, request.AgentVoiceAccess)
            : request.VoiceIdOverride.Trim();
        var preparedText = speechTextPreprocessor.Prepare(
            request.Text,
            request.SuppressIdentifierOmissionNotice);

        var result = await driver.SynthesizeAsync(
            new TextToSpeechDriverRequest(
                provider,
                settings,
                preparedText.SpokenText,
                voiceId),
            cancellationToken);

        return result with
        {
            SpokenText = preparedText.SpokenText,
            IdentifiersOmitted = preparedText.IdentifiersOmitted,
            IdentifierOmissionNoticeIncluded = preparedText.IdentifierOmissionNoticeIncluded
        };
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
        AgentVoiceDriverKind driverKind,
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

        ValidateProviderForDriver(provider, driverKind, capabilityName);
        return provider;
    }

    private static void ValidateProviderForDriver(
        ProviderProfile provider,
        AgentVoiceDriverKind driverKind,
        string capabilityName)
    {
        if (driverKind == AgentVoiceDriverKind.OpenAi &&
            (provider.Kind != ProviderKind.OpenAi || provider.Purpose != ProviderProfilePurpose.Chat))
        {
            throw new InvalidOperationException(
                $"OpenAI {capabilityName} requires an enabled OpenAI chat provider profile. Provider '{provider.Name}' is '{provider.Kind}' with purpose '{provider.Purpose}'.");
        }
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
}
