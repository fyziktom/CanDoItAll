namespace CanDoItAll.AgentFramework.Models;

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

public sealed class AgentSpeechToTextSettings
{
    public bool IsEnabled { get; set; }

    public AgentVoiceDriverKind DriverKind { get; set; } = AgentVoiceDriverKind.OpenAi;

    public Guid? ProviderProfileId { get; set; }

    public string Model { get; set; } = AgentVoiceDefaults.OpenAiTranscriptionModel;

    public string Language { get; set; } = string.Empty;
}

public sealed class AgentTextToSpeechSettings
{
    public bool IsEnabled { get; set; }

    public AgentVoiceDriverKind DriverKind { get; set; } = AgentVoiceDriverKind.OpenAi;

    public Guid? ProviderProfileId { get; set; }

    public string Model { get; set; } = AgentVoiceDefaults.OpenAiSpeechModel;

    public string VoiceId { get; set; } = AgentVoiceDefaults.OpenAiVoiceId;

    public string ResponseFormat { get; set; } = AgentVoiceDefaults.ResponseFormat;

    public string Instructions { get; set; } = string.Empty;
}

public sealed class AgentVoiceSettings
{
    public AgentSpeechToTextSettings SpeechToText { get; set; } = AgentVoiceDefaults.CreateSpeechToTextSettings();

    public AgentTextToSpeechSettings TextToSpeech { get; set; } = AgentVoiceDefaults.CreateTextToSpeechSettings();

    public string SampleText { get; set; } = AgentVoiceDefaults.SampleText;

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
            Language = string.Empty
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
            Language = NormalizeOptionalText(settings.Language)
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
