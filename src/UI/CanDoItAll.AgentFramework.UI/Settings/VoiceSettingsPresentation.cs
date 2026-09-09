using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.UI.Settings;

public sealed record VoiceSettingsValues {
    public bool SpeechEnabled { get; init; }
    public AgentVoiceDriverKind SpeechDriver { get; init; }
    public Guid? SpeechProviderId { get; init; }
    public string SpeechModel { get; init; } = AgentVoiceDefaults.OpenAiTranscriptionModel;
    public string Language { get; init; } = "";
    public string Prompt { get; init; } = "";
    public bool SynthesisEnabled { get; init; }
    public AgentVoiceDriverKind SynthesisDriver { get; init; }
    public Guid? SynthesisProviderId { get; init; }
    public string SynthesisModel { get; init; } = AgentVoiceDefaults.OpenAiSpeechModel;
    public string VoiceId { get; init; } = AgentVoiceDefaults.OpenAiVoiceId;
    public string ResponseFormat { get; init; } = AgentVoiceDefaults.ResponseFormat;
    public string Instructions { get; init; } = "";
    public string SampleText { get; init; } = AgentVoiceDefaults.SampleText;
    public string DisclosureText { get; init; } = AgentVoiceDefaults.DisclosureText;
}
public sealed record VoiceSettingsProvider(Guid Id, string Name, string DefaultModel);
public enum VoiceSettingsStatus { Loading, Ready, LoadFailed, Saving, Saved, GeneratingSample, PlayingSample, SamplePlayed, SaveFailed, SavedSynthesisFailed, SavedPlaybackFailed }
public enum VoiceSettingsAction { Save, PlaySample, Reload }
public sealed record VoiceSettingsIntent(long Generation, VoiceSettingsAction Action, VoiceSettingsValues? Submission = null);
public sealed record VoiceSettingsPresentation(long Generation = 0, VoiceSettingsValues? Source = null,
    ImmutableArray<VoiceSettingsProvider> Providers = default, VoiceSettingsStatus Status = VoiceSettingsStatus.Loading, bool ProvidersUnavailable = false) {
    public bool IsBusy => Status is VoiceSettingsStatus.Loading or VoiceSettingsStatus.Saving or VoiceSettingsStatus.GeneratingSample or VoiceSettingsStatus.PlayingSample;
    public bool IsPlaying => Status is VoiceSettingsStatus.GeneratingSample or VoiceSettingsStatus.PlayingSample;
    public string ProviderError => ProvidersUnavailable ? "Voice settings loaded, but the provider catalog is unavailable. Existing provider selections are retained." : "";
    public string StatusText => Status switch {
        VoiceSettingsStatus.Loading => "Loading",
        VoiceSettingsStatus.Ready => "Ready",
        VoiceSettingsStatus.LoadFailed => "Voice settings could not be loaded.",
        VoiceSettingsStatus.Saving => "Saving",
        VoiceSettingsStatus.Saved => "Saved",
        VoiceSettingsStatus.GeneratingSample => "Saved. Generating sample",
        VoiceSettingsStatus.PlayingSample => "Saved. Playing sample",
        VoiceSettingsStatus.SamplePlayed => "Saved. Sample played",
        VoiceSettingsStatus.SaveFailed => "Voice settings could not be saved. Review the values and try again.",
        VoiceSettingsStatus.SavedSynthesisFailed => "Saved. Sample generation failed.",
        VoiceSettingsStatus.SavedPlaybackFailed => "Saved. Sample playback failed.",
        _ => throw new ArgumentOutOfRangeException(nameof(Status))
    };
    public string StatusTone => Status switch {
        VoiceSettingsStatus.LoadFailed or VoiceSettingsStatus.SaveFailed => "danger",
        VoiceSettingsStatus.SavedSynthesisFailed or VoiceSettingsStatus.SavedPlaybackFailed => "warning",
        VoiceSettingsStatus.Ready or VoiceSettingsStatus.Saved or VoiceSettingsStatus.SamplePlayed => "success",
        _ => "neutral"
    };
}
