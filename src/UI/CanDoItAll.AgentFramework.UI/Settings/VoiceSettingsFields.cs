using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.UI.Settings;

public static class VoiceSettingsFields {
    public static SettingsDraftState<VoiceSettingsValues> CreateEditor() => new(value => value with { UpdatedAtUtc = null }, Fields);

    private static IReadOnlyList<SettingsMergeField<VoiceSettingsValues>> Fields(VoiceSettingsValues b, VoiceSettingsValues l, VoiceSettingsValues r) => [
        new SettingsMergeField<VoiceSettingsValues, bool>("Speech input enabled", b, l, r, value => value.SpeechEnabled, (value, field) => value with { SpeechEnabled = field }),
        new SettingsMergeField<VoiceSettingsValues, AgentVoiceDriverKind>("Speech input driver", b, l, r, value => value.SpeechDriver, (value, field) => value with { SpeechDriver = field }),
        new SettingsMergeField<VoiceSettingsValues, Guid?>("Speech input provider", b, l, r, value => value.SpeechProviderId, (value, field) => value with { SpeechProviderId = field }),
        new SettingsMergeField<VoiceSettingsValues, string>("Speech input model", b, l, r, value => value.SpeechModel, (value, field) => value with { SpeechModel = field }),
        new SettingsMergeField<VoiceSettingsValues, string>("Language", b, l, r, value => value.Language, (value, field) => value with { Language = field }),
        new SettingsMergeField<VoiceSettingsValues, string>("Speech prompt", b, l, r, value => value.Prompt, (value, field) => value with { Prompt = field }),
        new SettingsMergeField<VoiceSettingsValues, bool>("Spoken responses enabled", b, l, r, value => value.SynthesisEnabled, (value, field) => value with { SynthesisEnabled = field }),
        new SettingsMergeField<VoiceSettingsValues, AgentVoiceDriverKind>("Speech output driver", b, l, r, value => value.SynthesisDriver, (value, field) => value with { SynthesisDriver = field }),
        new SettingsMergeField<VoiceSettingsValues, Guid?>("Speech output provider", b, l, r, value => value.SynthesisProviderId, (value, field) => value with { SynthesisProviderId = field }),
        new SettingsMergeField<VoiceSettingsValues, string>("Speech output model", b, l, r, value => value.SynthesisModel, (value, field) => value with { SynthesisModel = field }),
        new SettingsMergeField<VoiceSettingsValues, string>("Voice", b, l, r, value => value.VoiceId, (value, field) => value with { VoiceId = field }),
        new SettingsMergeField<VoiceSettingsValues, string>("Output format", b, l, r, value => value.ResponseFormat, (value, field) => value with { ResponseFormat = field }),
        new SettingsMergeField<VoiceSettingsValues, string>("Synthesis instructions", b, l, r, value => value.Instructions, (value, field) => value with { Instructions = field }),
        new SettingsMergeField<VoiceSettingsValues, string>("Sample text", b, l, r, value => value.SampleText, (value, field) => value with { SampleText = field }),
        new SettingsMergeField<VoiceSettingsValues, string>("Disclosure", b, l, r, value => value.DisclosureText, (value, field) => value with { DisclosureText = field })
    ];
}
