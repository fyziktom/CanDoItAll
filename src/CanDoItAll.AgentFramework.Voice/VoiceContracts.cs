using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Voice;

public sealed record AgentVoiceTranscriptionRequest(
    byte[] AudioBytes,
    string FileName,
    string ContentType);

public sealed record AgentVoiceTranscriptionResult(
    string Text,
    string Model);

public sealed record AgentVoiceSynthesisRequest(
    string Text,
    AgentVoiceAccessSettings? AgentVoiceAccess = null,
    string? VoiceIdOverride = null,
    bool SuppressIdentifierOmissionNotice = false);

public sealed record AgentVoiceSynthesisResult(
    byte[] AudioBytes,
    string ContentType,
    string Model,
    string VoiceId,
    string ResponseFormat)
{
    public string SpokenText { get; init; } = string.Empty;

    public bool IdentifiersOmitted { get; init; }

    public bool IdentifierOmissionNoticeIncluded { get; init; }
}

public sealed record AgentVoiceSpeechTextPreparationResult(
    string SpokenText,
    bool IdentifiersOmitted,
    bool IdentifierOmissionNoticeIncluded,
    int RemovedIdentifierCount);

public sealed record SpeechToTextDriverRequest(
    ProviderProfile Provider,
    AgentSpeechToTextSettings Settings,
    byte[] AudioBytes,
    string FileName,
    string ContentType);

public sealed record TextToSpeechDriverRequest(
    ProviderProfile Provider,
    AgentTextToSpeechSettings Settings,
    string Text,
    string VoiceId);

public interface ISpeechToTextVoiceDriver
{
    AgentVoiceDriverKind DriverKind { get; }

    Task<AgentVoiceTranscriptionResult> TranscribeAsync(
        SpeechToTextDriverRequest request,
        CancellationToken cancellationToken = default);
}

public interface ITextToSpeechVoiceDriver
{
    AgentVoiceDriverKind DriverKind { get; }

    Task<AgentVoiceSynthesisResult> SynthesizeAsync(
        TextToSpeechDriverRequest request,
        CancellationToken cancellationToken = default);
}

public interface IAgentVoiceDriverFactory
{
    ISpeechToTextVoiceDriver CreateSpeechToTextDriver(AgentVoiceDriverKind driverKind);

    ITextToSpeechVoiceDriver CreateTextToSpeechDriver(AgentVoiceDriverKind driverKind);
}

public interface IAgentVoiceSpeechTextPreprocessor
{
    AgentVoiceSpeechTextPreparationResult Prepare(
        string text,
        bool suppressIdentifierOmissionNotice);
}

public interface IAgentVoiceService
{
    Task<AgentVoiceSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<AgentVoiceSettings> SaveSettingsAsync(
        AgentVoiceSettings settings,
        CancellationToken cancellationToken = default);

    Task<AgentVoiceTranscriptionResult> TranscribeAsync(
        AgentVoiceTranscriptionRequest request,
        CancellationToken cancellationToken = default);

    Task<AgentVoiceSynthesisResult> SynthesizeAsync(
        AgentVoiceSynthesisRequest request,
        CancellationToken cancellationToken = default);

    Task<AgentVoiceSynthesisResult> SynthesizeSampleAsync(
        string? sampleText = null,
        CancellationToken cancellationToken = default);
}
