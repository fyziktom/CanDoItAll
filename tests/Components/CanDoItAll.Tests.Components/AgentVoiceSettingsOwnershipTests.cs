using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Voice;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using Microsoft.Extensions.DependencyInjection;
using ProviderProfile = CanDoItAll.AgentFramework.Models.ProviderProfile;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentVoiceSettingsOwnershipTests {
    [Fact]
    public void Prerender_does_not_publish_editable_fields_or_start_voice_reads() {
        using var context = Context(out var voice);
        context.SetRendererInfo(new("Static", isInteractive: false));
        var cut = context.Render<AgentVoiceSettingsPanel>();
        Assert.Equal(0, voice.Reads);
        Assert.Empty(cut.FindAll("[data-testid='agents-voice-sample-text']"));
        Assert.Contains("Loading", cut.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_settings_conflict_retains_the_draft_and_original_without_starting_sample_audio(bool play) {
        using var context = Context(out var voice);
        voice.RefuseSettings = true;
        var cut = context.Render<AgentVoiceSettingsPanel>();
        var original = voice.Revision;
        cut.Find("[data-testid='agents-voice-sample-text']").Change("Retained unsaved voice sample");
        await cut.Find(play ? "[data-testid='agents-voice-play-sample']" : "[data-testid='agents-voice-save']").ClickAsync();
        Assert.Equal(1, voice.Saves);
        Assert.Equal(0, voice.Samples);
        Assert.Equal(original, voice.Submission!.UpdatedAtUtc);
        Assert.Equal("Retained unsaved voice sample", voice.Submission.SampleText);
        Assert.Equal("Retained unsaved voice sample", cut.Find("[data-testid='agents-voice-sample-text']").GetAttribute("value"));
        Assert.Contains("your local draft is retained", cut.Find("[data-testid='agents-voice-settings-conflict']").TextContent, StringComparison.Ordinal);
        Assert.True(cut.Find("[data-testid='agents-voice-save']").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid='agents-voice-play-sample']").HasAttribute("disabled"));
        voice.Revision = original.AddTicks(10);
        await cut.Find("[data-testid='agents-voice-compare']").ClickAsync();
        Assert.Equal("Retained unsaved voice sample", cut.Find("[data-testid='agents-voice-sample-text']").GetAttribute("value"));
        foreach (var button in cut.FindAll("button").Where(button => button.TextContent.Trim() == "Use current").ToArray()) {
            await button.ClickAsync();
        }
        await cut.Find("[data-testid='settings-review-apply']").ClickAsync();
        Assert.Equal(AgentVoiceDefaults.SampleText, cut.Find("[data-testid='agents-voice-sample-text']").GetAttribute("value"));
        Assert.Empty(cut.FindAll("[data-testid='agents-voice-settings-conflict']"));
        Assert.Equal(1, voice.Saves);
    }

    [Fact]
    public async Task Two_settings_views_use_distinct_playback_owners_and_dispose_only_their_own_media() {
        using var context = Context(out var voice);
        var first = context.Render<AgentVoiceSettingsPanel>();
        var second = context.Render<AgentVoiceSettingsPanel>();
        await first.Find("[data-testid='agents-voice-play-sample']").ClickAsync();
        await second.Find("[data-testid='agents-voice-play-sample']").ClickAsync();
        var calls = context.JSInterop.Invocations.Where(call => call.Identifier.Contains(".playAudio", StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, calls.Length);
        Assert.All(calls, call => Assert.Equal("CanDoItAll.agentFramework.voice.playAudioForOwner", call.Identifier));
        Assert.NotEqual(calls[0].Arguments[0], calls[1].Arguments[0]);
        await DisposeAsync(first);
        var dispose = Assert.Single(context.JSInterop.Invocations, call => call.Identifier == "CanDoItAll.agentFramework.voice.disposeOwner");
        Assert.Equal(calls[0].Arguments[0], Assert.Single(dispose.Arguments));
        Assert.Equal(2, voice.Saves);
        Assert.Equal(2, voice.Samples);
        Assert.Contains("Sample played", second.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Synthesis_completing_after_retirement_cannot_activate_audio() {
        using var context = Context(out var voice);
        voice.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var cut = context.Render<AgentVoiceSettingsPanel>();
        var sample = cut.Find("[data-testid='agents-voice-play-sample']").ClickAsync();
        cut.WaitForAssertion(() => Assert.Equal(1, voice.Samples));
        await DisposeAsync(cut);
        Assert.True(voice.SampleToken.IsCancellationRequested);
        Assert.True(voice.SampleToken.WaitHandle.WaitOne(0));
        voice.Pending.SetResult(Voice.Audio);
        await sample;
        Assert.DoesNotContain(context.JSInterop.Invocations, call => call.Identifier.Contains(".playAudio", StringComparison.Ordinal));
        Assert.Empty(context.Services.GetRequiredService<NotificationService>().Messages);
        Assert.Equal(1, voice.Saves);
    }

    private static Task DisposeAsync(IRenderedComponent<AgentVoiceSettingsPanel> cut) => cut.InvokeAsync(async () => {
        if (cut.Instance is IAsyncDisposable asynchronous) {
            await asynchronous.DisposeAsync();
        } else if (cut.Instance is IDisposable synchronous) {
            synchronous.Dispose();
        }
    });

    private static BunitContext Context(out Voice voice) {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSettingsOpeningFixture();
        voice = new();
        context.Services.AddSingleton<IAgentVoiceService>(voice);
        context.Services.AddSingleton(DispatchProxy.Create<IProviderRuntimeAdministrationService, Providers>());
        context.SetRendererInfo(new("Server", isInteractive: true));
        return context;
    }

    public class Providers : DispatchProxy {
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => method?.Name == nameof(IProviderRuntimeAdministrationService.ListProvidersAsync)
                ? Task.FromResult<IReadOnlyList<ProviderProfile>>([])
                : throw new NotSupportedException(method?.Name);
    }

    private sealed class Voice : IAgentVoiceService {
        public static AgentVoiceSynthesisResult Audio { get; } = new([1, 2, 3], "audio/wav", "owned", "sample", "wav");
        public TaskCompletionSource<AgentVoiceSynthesisResult>? Pending { get; set; }
        public CancellationToken SampleToken { get; private set; }
        public int Saves { get; private set; }
        public int Samples { get; private set; }
        public int Reads { get; private set; }
        public bool RefuseSettings { get; set; }
        public DateTimeOffset Revision { get; set; } = DateTimeOffset.UtcNow;
        public AgentVoiceSettings? Submission { get; private set; }
        public Task<AgentVoiceSettings> GetSettingsAsync(CancellationToken cancellationToken = default) {
            Reads++;
            var settings = AgentVoiceSettings.Default;
            settings.UpdatedAtUtc = Revision;
            return Task.FromResult(settings);
        }
        public Task<AgentVoiceSettings> SaveSettingsAsync(AgentVoiceSettings settings, CancellationToken cancellationToken = default) {
            Saves++;
            Submission = settings;
            if (RefuseSettings) {
                throw new WorkflowSettingsConcurrencyException();
            }
            return Task.FromResult(settings);
        }
        public Task<AgentVoiceSynthesisResult> SynthesizeSampleAsync(string? sampleText = null, CancellationToken cancellationToken = default) {
            Samples++;
            SampleToken = cancellationToken;
            return Pending?.Task ?? Task.FromResult(Audio);
        }
        public Task<AgentVoiceTranscriptionResult> TranscribeAsync(AgentVoiceTranscriptionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AgentVoiceSynthesisResult> SynthesizeAsync(AgentVoiceSynthesisRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<AgentVoiceSynthesisResult> SynthesizeChunksAsync(AgentVoiceSynthesisRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
