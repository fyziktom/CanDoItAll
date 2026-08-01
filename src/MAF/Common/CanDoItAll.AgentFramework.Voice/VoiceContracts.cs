using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Voice;

public sealed record AgentVoiceTranscriptionRequest(
    byte[] AudioBytes,
    string FileName,
    string ContentType)
{
    public IReadOnlyList<AgentVoiceAudioChunk> AudioChunks { get; init; } = [];
}

public sealed record AgentVoiceAudioChunk(
    byte[] AudioBytes,
    string FileName,
    string ContentType);

public sealed class BrowserVoiceRecording
{
    public string Base64 { get; set; } = string.Empty;

    public string ContentType { get; set; } = "audio/webm";

    public string FileName { get; set; } = "voice-input.webm";

    public List<BrowserVoiceRecordingChunk> Chunks { get; set; } = [];

    public AgentVoiceTranscriptionRequest ToTranscriptionRequest()
    {
        var audioBytes = Chunks.Count == 0
            ? DecodeBase64(Base64)
            : [];

        return new AgentVoiceTranscriptionRequest(audioBytes, FileName, ContentType)
        {
            AudioChunks = Chunks
                .Select(chunk => new AgentVoiceAudioChunk(
                    DecodeBase64(chunk.Base64),
                    chunk.FileName,
                    chunk.ContentType))
                .ToList()
        };
    }

    private static byte[] DecodeBase64(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : Convert.FromBase64String(value);
    }
}

public sealed class BrowserVoiceRecordingChunk
{
    public string Base64 { get; set; } = string.Empty;

    public string ContentType { get; set; } = "audio/webm";

    public string FileName { get; set; } = "voice-input.webm";
}

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

    public int ChunkIndex { get; init; }

    public int ChunkCount { get; init; } = 1;
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

    IAsyncEnumerable<AgentVoiceSynthesisResult> SynthesizeChunksAsync(
        AgentVoiceSynthesisRequest request,
        CancellationToken cancellationToken = default);

    Task<AgentVoiceSynthesisResult> SynthesizeSampleAsync(
        string? sampleText = null,
        CancellationToken cancellationToken = default);
}
