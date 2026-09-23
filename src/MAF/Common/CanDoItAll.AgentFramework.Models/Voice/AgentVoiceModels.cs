namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// Voice driver that performs speech-to-text or text-to-speech, as a JSON integer: 0 OpenAi, the only driver, which
/// sends the audio request through the selected provider profile.
/// </summary>
public enum AgentVoiceDriverKind
{
    OpenAi = 0
}

public enum AgentVoiceConfirmationIntent
{
    Unknown = 0,
    Affirm = 1,
    Reject = 2
}

/// <summary>
/// Speech-to-text (voice input) settings: whether spoken input is transcribed and which provider profile and model
/// transcribe it.
/// </summary>
public sealed class AgentSpeechToTextSettings
{
    /// <summary>
    /// Whether voice input is transcribed. False by default; while false, every transcription request is rejected.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Voice driver, as a JSON integer: 0 OpenAi (the only value and the default).
    /// </summary>
    public AgentVoiceDriverKind DriverKind { get; set; } = AgentVoiceDriverKind.OpenAi;

    /// <summary>
    /// Identifier (GUID) of the provider profile that transcribes the audio, as listed by
    /// <c>GET /api/agents/providers</c>. Null by default, and an empty GUID counts as null. Transcription fails until
    /// the setting names an existing, enabled profile.
    /// </summary>
    public Guid? ProviderProfileId { get; set; }

    /// <summary>
    /// Transcription model name sent to the provider. Defaults to <c>gpt-4o-mini-transcribe</c>; a blank value falls
    /// back to that default when the settings are applied.
    /// </summary>
    public string Model { get; set; } = AgentVoiceDefaults.OpenAiTranscriptionModel;

    /// <summary>
    /// Optional language hint sent with the audio, for example <c>en</c>. Empty by default, which sends no hint.
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Optional text sent with the audio to guide the transcription, such as names or terms to expect. Empty by
    /// default, which sends none.
    /// </summary>
    public string Prompt { get; set; } = string.Empty;
}

/// <summary>
/// Text-to-speech (voice output) settings: whether replies can be spoken and which provider profile, model, voice and
/// audio format produce the speech.
/// </summary>
public sealed class AgentTextToSpeechSettings
{
    /// <summary>
    /// Whether text can be turned into speech. False by default; while false, every speech request is rejected.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Voice driver, as a JSON integer: 0 OpenAi (the only value and the default).
    /// </summary>
    public AgentVoiceDriverKind DriverKind { get; set; } = AgentVoiceDriverKind.OpenAi;

    /// <summary>
    /// Identifier (GUID) of the provider profile that produces the speech, as listed by
    /// <c>GET /api/agents/providers</c>. Null by default, and an empty GUID counts as null. Speech output fails until
    /// the setting names an existing, enabled profile.
    /// </summary>
    public Guid? ProviderProfileId { get; set; }

    /// <summary>
    /// Speech model name sent to the provider. Defaults to <c>gpt-4o-mini-tts</c>; a blank value falls back to that
    /// default when the settings are applied.
    /// </summary>
    public string Model { get; set; } = AgentVoiceDefaults.OpenAiSpeechModel;

    /// <summary>
    /// Default voice name. Defaults to <c>marin</c>; the settings screen offers alloy, ash, ballad, coral, echo, fable,
    /// nova, onyx, sage, shimmer, verse, marin and cedar, but the value is not checked against that list. An agent
    /// that allows voice mode can prefer another voice.
    /// </summary>
    public string VoiceId { get; set; } = AgentVoiceDefaults.OpenAiVoiceId;

    /// <summary>
    /// Audio format requested from the provider: <c>mp3</c> (the default), <c>wav</c>, <c>opus</c>, <c>aac</c>,
    /// <c>flac</c> or <c>pcm</c>. When the settings are applied the value is lower-cased, and a blank or unknown value
    /// becomes <c>mp3</c>.
    /// </summary>
    public string ResponseFormat { get; set; } = AgentVoiceDefaults.ResponseFormat;

    /// <summary>
    /// Optional speaking-style instructions sent to the provider, such as tone or pace. Empty by default, which sends
    /// none.
    /// </summary>
    public string Instructions { get; set; } = string.Empty;
}

/// <summary>
/// Agent voice settings: speech-to-text for voice input, text-to-speech for spoken replies, and the texts the voice
/// settings screen uses. They are stored with the workflow settings. When they are applied, text is trimmed, a blank
/// model, voice or text falls back to its default, and an empty provider profile GUID counts as null.
/// </summary>
public sealed class AgentVoiceSettings
{
    /// <summary>
    /// Speech-to-text settings. Transcription is disabled by default.
    /// </summary>
    public AgentSpeechToTextSettings SpeechToText { get; set; } = AgentVoiceDefaults.CreateSpeechToTextSettings();

    /// <summary>
    /// Text-to-speech settings. Speech output is disabled by default.
    /// </summary>
    public AgentTextToSpeechSettings TextToSpeech { get; set; } = AgentVoiceDefaults.CreateTextToSpeechSettings();

    /// <summary>
    /// Text spoken when a user previews the selected voice. A blank value falls back to the built-in sample sentence
    /// when the settings are applied.
    /// </summary>
    public string SampleText { get; set; } = AgentVoiceDefaults.SampleText;

    /// <summary>
    /// Notice shown in the voice settings screen that tells users the audio is AI-generated. Defaults to
    /// <c>Voice output is AI-generated.</c>; a blank value falls back to that default when the settings are applied.
    /// </summary>
    public string DisclosureText { get; set; } = AgentVoiceDefaults.DisclosureText;

    public static AgentVoiceSettings Default => new()
    {
        SpeechToText = AgentVoiceDefaults.CreateSpeechToTextSettings(),
        TextToSpeech = AgentVoiceDefaults.CreateTextToSpeechSettings(),
        SampleText = AgentVoiceDefaults.SampleText,
        DisclosureText = AgentVoiceDefaults.DisclosureText
    };
}

public static class AgentVoiceDefaults
{
    public const string OpenAiTranscriptionModel = "gpt-4o-mini-transcribe";
    public const string OpenAiSpeechModel = "gpt-4o-mini-tts";
    public const string OpenAiVoiceId = "marin";
    public const string ResponseFormat = "mp3";
    public const string SampleText = "This is the selected voice for CanDoItAll agent audio mode.";
    public const string DisclosureText = "Voice output is AI-generated.";

    public static IReadOnlyList<string> OpenAiVoiceIds { get; } =
    [
        "alloy",
        "ash",
        "ballad",
        "coral",
        "echo",
        "fable",
        "nova",
        "onyx",
        "sage",
        "shimmer",
        "verse",
        "marin",
        "cedar"
    ];

    public static IReadOnlyList<string> ResponseFormats { get; } =
    [
        "mp3",
        "wav",
        "opus",
        "aac",
        "flac",
        "pcm"
    ];

    public static AgentSpeechToTextSettings CreateSpeechToTextSettings()
    {
        return new AgentSpeechToTextSettings
        {
            IsEnabled = false,
            DriverKind = AgentVoiceDriverKind.OpenAi,
            ProviderProfileId = null,
            Model = OpenAiTranscriptionModel,
            Language = string.Empty,
            Prompt = string.Empty
        };
    }

    public static AgentTextToSpeechSettings CreateTextToSpeechSettings()
    {
        return new AgentTextToSpeechSettings
        {
            IsEnabled = false,
            DriverKind = AgentVoiceDriverKind.OpenAi,
            ProviderProfileId = null,
            Model = OpenAiSpeechModel,
            VoiceId = OpenAiVoiceId,
            ResponseFormat = ResponseFormat,
            Instructions = string.Empty
        };
    }
}

public static class AgentVoiceSettingsNormalizer
{
    public static AgentVoiceSettings Normalize(AgentVoiceSettings? settings)
    {
        settings ??= AgentVoiceSettings.Default;

        return new AgentVoiceSettings
        {
            SpeechToText = NormalizeSpeechToText(settings.SpeechToText),
            TextToSpeech = NormalizeTextToSpeech(settings.TextToSpeech),
            SampleText = NormalizeText(settings.SampleText, AgentVoiceDefaults.SampleText),
            DisclosureText = NormalizeText(settings.DisclosureText, AgentVoiceDefaults.DisclosureText)
        };
    }

    public static AgentSpeechToTextSettings NormalizeSpeechToText(AgentSpeechToTextSettings? settings)
    {
        settings ??= AgentVoiceDefaults.CreateSpeechToTextSettings();

        return new AgentSpeechToTextSettings
        {
            IsEnabled = settings.IsEnabled,
            DriverKind = settings.DriverKind,
            ProviderProfileId = NormalizeProviderId(settings.ProviderProfileId),
            Model = NormalizeText(settings.Model, AgentVoiceDefaults.OpenAiTranscriptionModel),
            Language = NormalizeOptionalText(settings.Language),
            Prompt = NormalizeOptionalText(settings.Prompt)
        };
    }

    public static AgentTextToSpeechSettings NormalizeTextToSpeech(AgentTextToSpeechSettings? settings)
    {
        settings ??= AgentVoiceDefaults.CreateTextToSpeechSettings();
        var responseFormat = NormalizeOptionalText(settings.ResponseFormat);
        if (string.IsNullOrWhiteSpace(responseFormat) ||
            !AgentVoiceDefaults.ResponseFormats.Contains(responseFormat, StringComparer.OrdinalIgnoreCase))
        {
            responseFormat = AgentVoiceDefaults.ResponseFormat;
        }

        return new AgentTextToSpeechSettings
        {
            IsEnabled = settings.IsEnabled,
            DriverKind = settings.DriverKind,
            ProviderProfileId = NormalizeProviderId(settings.ProviderProfileId),
            Model = NormalizeText(settings.Model, AgentVoiceDefaults.OpenAiSpeechModel),
            VoiceId = NormalizeText(settings.VoiceId, AgentVoiceDefaults.OpenAiVoiceId),
            ResponseFormat = responseFormat.ToLowerInvariant(),
            Instructions = NormalizeOptionalText(settings.Instructions)
        };
    }

    public static string ResolveEffectiveVoiceId(
        AgentTextToSpeechSettings textToSpeechSettings,
        AgentVoiceAccessSettings? agentVoiceAccess)
    {
        ArgumentNullException.ThrowIfNull(textToSpeechSettings);

        var normalizedAccess = agentVoiceAccess is null
            ? null
            : AgentVoiceAccessMetadata.Normalize(agentVoiceAccess);
        if (normalizedAccess is { CanUseVoiceMode: true } &&
            !string.IsNullOrWhiteSpace(normalizedAccess.PreferredVoiceId))
        {
            return normalizedAccess.PreferredVoiceId;
        }

        return NormalizeText(textToSpeechSettings.VoiceId, AgentVoiceDefaults.OpenAiVoiceId);
    }

    private static Guid? NormalizeProviderId(Guid? providerProfileId)
    {
        return providerProfileId is { } value && value != Guid.Empty
            ? value
            : null;
    }

    private static string NormalizeText(string? value, string fallback)
    {
        var normalized = NormalizeOptionalText(value);
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static string NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }
}
