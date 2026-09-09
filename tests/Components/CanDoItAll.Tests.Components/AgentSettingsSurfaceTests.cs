using Bunit;
using CanDoItAll.AgentFramework.UI.Settings;
using CanDoItAll.AgentFramework.UiSandbox;
using CanDoItAll.Components.BaseLib;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentSettingsSurfaceTests {
    [Theory]
    [InlineData(VoiceSettingsScenario.Loading)]
    [InlineData(VoiceSettingsScenario.Ready)]
    [InlineData(VoiceSettingsScenario.ProviderPartial)]
    [InlineData(VoiceSettingsScenario.SavedWarning)]
    public void Voice_scenarios_are_service_free(VoiceSettingsScenario scenario) {
        using var context = Context();
        var presentation = VoiceSettingsSandboxFixture.Create(scenario);
        var cut = context.Render<VoiceSettingsSurface>(p => p.Add(x => x.Presentation, presentation));
        Assert.Contains(presentation.StatusText, cut.Markup, StringComparison.Ordinal);
        if (presentation.Source is not null) {
            Assert.Equal(presentation.Source.SampleText, cut.Find("[data-testid='agents-voice-sample-text']").GetAttribute("value"));
        }
        if (scenario == VoiceSettingsScenario.ProviderPartial) {
            Assert.Equal(presentation.Source!.SynthesisProviderId!.Value.ToString("D"), cut.Find("[data-testid='agents-voice-tts-provider']").GetAttribute("value"));
            Assert.Contains("Existing provider (catalog unavailable)", cut.Markup, StringComparison.Ordinal);
        }
    }
    [Theory]
    [InlineData(FloatingChatSettingsScenario.Loading)]
    [InlineData(FloatingChatSettingsScenario.Ready)]
    [InlineData(FloatingChatSettingsScenario.Error)]
    [InlineData(FloatingChatSettingsScenario.SavedWarning)]
    public void Floating_scenarios_are_service_free(FloatingChatSettingsScenario scenario) {
        using var context = Context();
        var presentation = FloatingChatSettingsSandboxFixture.Create(scenario);
        var cut = context.Render<FloatingChatSettingsSurface>(p => p.Add(x => x.Presentation, presentation));
        Assert.Contains(presentation.StatusText, cut.Markup, StringComparison.Ordinal);
        if (presentation.Source is not null) {
            Assert.Equal("12", cut.Find("[data-testid='floating-agent-chat-maximum-active']").GetAttribute("value"));
            Assert.Contains("Live runtime agents, credentials, tool clients, and execution sessions remain per-run", cut.Markup, StringComparison.Ordinal);
        }
    }
    [Fact]
    public void Voice_edits_are_independent_immutable_submissions_and_status_updates_preserve_draft() {
        using var context = Context();
        var presentation = VoiceSettingsSandboxFixture.Create(VoiceSettingsScenario.Ready);
        var intents = new List<VoiceSettingsIntent>();
        var cut = context.Render<VoiceSettingsSurface>(p => p.Add(x => x.Presentation, presentation).Add(x => x.Intent, value => intents.Add(value)));
        cut.Find("[data-testid='agents-voice-stt-enabled']").Change(true);
        cut.Find("[data-testid='agents-voice-tts-enabled']").Change(true);
        cut.Find("[data-testid='agents-voice-stt-provider']").Change("");
        cut.Find("[data-testid='agents-voice-stt-model']").Change("edited transcription");
        cut.Find("[data-testid='agents-voice-stt-language']").Change("en");
        cut.Find("[data-testid='agents-voice-stt-prompt']").Change("edited prompt");
        cut.Find("[data-testid='agents-voice-tts-model']").Change("edited speech");
        cut.Find("[data-testid='agents-voice-default-voice']").Change("alloy");
        cut.Find("[data-testid='agents-voice-format']").Change("wav");
        cut.Find("[data-testid='agents-voice-sample-text']").Change("edited sample");
        cut.Render(p => p.Add(x => x.Presentation, presentation with { Status = VoiceSettingsStatus.SaveFailed }));
        cut.Find("[data-testid='agents-voice-save']").Click();
        var saved = Assert.Single(intents);
        Assert.Equal(presentation.Source! with { SpeechEnabled = true, SynthesisEnabled = true, SpeechProviderId = null, SpeechModel = "edited transcription", Language = "en", Prompt = "edited prompt", SynthesisModel = "edited speech", VoiceId = "alloy", ResponseFormat = "wav", SampleText = "edited sample" }, saved.Submission);
        cut.Find("[data-testid='agents-voice-sample-text']").Change("later sample");
        cut.Find("[data-testid='agents-voice-play-sample']").Click();
        Assert.Equal("edited sample", saved.Submission!.SampleText);
        Assert.Equal(VoiceSettingsAction.PlaySample, intents[1].Action);
        Assert.Equal("later sample", intents[1].Submission!.SampleText);
        Assert.All(intents, intent => Assert.Equal(presentation.Generation, intent.Generation));
    }
    [Fact]
    public void Floating_all_fields_submit_one_snapshot_and_do_not_change_the_accepted_source() {
        using var context = Context();
        var presentation = FloatingChatSettingsSandboxFixture.Create(FloatingChatSettingsScenario.Ready);
        var intents = new List<FloatingChatSettingsIntent>();
        var cut = context.Render<FloatingChatSettingsSurface>(p => p.Add(x => x.Presentation, presentation).Add(x => x.Intent, value => intents.Add(value)));
        cut.Find("[data-testid='floating-agent-chat-retention']").Change("25");
        cut.Find("[data-testid='floating-agent-chat-maximum-active']").Change("9");
        cut.Find("[data-testid='floating-agent-chat-maximum-prepared']").Change("7");
        cut.Find("[data-testid='floating-agent-chat-prepared-retention']").Change("30");
        cut.Find("[data-testid='floating-agent-chat-adaptive']").Change(false);
        cut.Find("[data-testid='floating-agent-chat-settings-save']").Click();
        Assert.Equal(new FloatingChatSettingsValues(25, 9, 7, false, 30), Assert.Single(intents).Submission);
        Assert.Equal(12, presentation.Source!.MaximumActiveChats);
        cut.Find("[data-testid='floating-agent-chat-maximum-active']").Change("5");
        Assert.Equal(9, intents[0].Submission!.MaximumActiveChats);
    }
    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
