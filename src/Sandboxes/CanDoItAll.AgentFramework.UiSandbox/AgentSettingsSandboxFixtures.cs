using CanDoItAll.AgentFramework.UI.Settings;

namespace CanDoItAll.AgentFramework.UiSandbox;

public enum VoiceSettingsScenario { Loading, Ready, ProviderPartial, SavedWarning }
public enum FloatingChatSettingsScenario { Loading, Ready, Error, SavedWarning }

public sealed class VoiceSettingsSandboxFixture {
    public VoiceSettingsScenario Scenario { get; private set; } = VoiceSettingsScenario.Ready;
    public VoiceSettingsPresentation Presentation { get; private set; } = Create(VoiceSettingsScenario.Ready);
    public string IntentLog { get; private set; } = "No intent";
    public void SetScenario(VoiceSettingsScenario scenario) {
        Scenario = scenario;
        Presentation = Create(scenario);
    }
    public void Apply(VoiceSettingsIntent intent) {
        IntentLog = intent.Action.ToString();
        if (intent.Action == VoiceSettingsAction.Reload) {
            SetScenario(VoiceSettingsScenario.Ready);
        } else {
            Presentation = Presentation with { Source = intent.Submission, Status = intent.Action == VoiceSettingsAction.Save ? VoiceSettingsStatus.Saved : VoiceSettingsStatus.SamplePlayed };
        }
    }
    public static VoiceSettingsPresentation Create(VoiceSettingsScenario scenario) {
        var id = Guid.Parse("34000000-0000-0000-0000-000000000001");
        var presentation = new VoiceSettingsPresentation(1, new() { SpeechProviderId = id, SynthesisProviderId = id }, [new(id, "Synthetic voice provider", "sample-model")], VoiceSettingsStatus.Ready);
        return scenario switch {
            VoiceSettingsScenario.Loading => presentation with { Source = null, Status = VoiceSettingsStatus.Loading },
            VoiceSettingsScenario.Ready => presentation,
            VoiceSettingsScenario.ProviderPartial => presentation with { Providers = [], ProvidersUnavailable = true },
            VoiceSettingsScenario.SavedWarning => presentation with { Status = VoiceSettingsStatus.SavedPlaybackFailed },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
    }
}

public sealed class FloatingChatSettingsSandboxFixture {
    public FloatingChatSettingsScenario Scenario { get; private set; } = FloatingChatSettingsScenario.Ready;
    public FloatingChatSettingsPresentation Presentation { get; private set; } = Create(FloatingChatSettingsScenario.Ready);
    public string IntentLog { get; private set; } = "No intent";
    public void SetScenario(FloatingChatSettingsScenario scenario) {
        Scenario = scenario;
        Presentation = Create(scenario);
    }
    public void Apply(FloatingChatSettingsIntent intent) {
        IntentLog = intent.Action.ToString();
        if (intent.Action == FloatingChatSettingsAction.Reload) {
            SetScenario(FloatingChatSettingsScenario.Ready);
        } else {
            Presentation = Presentation with { Source = intent.Submission, Status = FloatingChatSettingsStatus.Saved };
        }
    }
    public static FloatingChatSettingsPresentation Create(FloatingChatSettingsScenario scenario) {
        var presentation = new FloatingChatSettingsPresentation(1, new(10, 12, 3, true, 15), new(1440, 50, 20), FloatingChatSettingsStatus.Ready);
        return scenario switch {
            FloatingChatSettingsScenario.Loading => presentation with { Source = null, Status = FloatingChatSettingsStatus.Loading },
            FloatingChatSettingsScenario.Ready => presentation,
            FloatingChatSettingsScenario.Error => presentation with { Source = null, Status = FloatingChatSettingsStatus.LoadFailed },
            FloatingChatSettingsScenario.SavedWarning => presentation with { Status = FloatingChatSettingsStatus.SavedWithRuntimeWarning },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
    }
}
