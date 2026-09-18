using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Immutable;
using CanDoItAll.AgentFramework.UI.Settings;
using CanDoItAll.AgentFramework.Voice;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentSettingsSessionTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Voice_stale_load_and_disposal_keep_tokens_alive_until_both_reads_finish(bool dispose) {
        var settings = new TaskCompletionSource<AgentVoiceSettings>();
        var providers = new TaskCompletionSource<ImmutableArray<VoiceSettingsProvider>>();
        var service = new VoiceService { Read = _ => settings.Task };
        var providerCalls = 0;
        CancellationToken providerToken = default;
        using var session = new AgentVoiceSettingsSession(service, token => {
            providerToken = token;
            return ++providerCalls == 1 ? providers.Task : Task.FromResult(ImmutableArray<VoiceSettingsProvider>.Empty);
        }, (_, _) => Task.CompletedTask, NullLogger<AgentVoiceSettingsSession>.Instance);
        var publications = 0;
        session.Changed += () => publications++;
        var old = session.LoadAsync();
        var oldToken = providerToken;
        if (dispose) {
            session.Dispose();
        } else {
            service.Read = _ => Task.FromResult(new AgentVoiceSettings { SampleText = "Current settings" });
            await session.LoadAsync();
        }
        Assert.True(oldToken.IsCancellationRequested);
        using var registration = oldToken.Register(() => { });
        var count = publications;
        settings.SetResult(new() { SampleText = "Obsolete settings" });
        providers.SetResult([new(Guid.NewGuid(), "Obsolete provider", "sample")]);
        await old;
        Assert.Equal(count, publications);
        Assert.Equal(dispose ? null : "Current settings", session.Presentation.Source?.SampleText);
    }

    [Fact]
    public async Task Voice_provider_failure_retains_existing_ids_and_settings_failure_is_closed() {
        var source = new AgentVoiceSettings();
        source.SpeechToText.ProviderProfileId = Guid.NewGuid();
        source.TextToSpeech.ProviderProfileId = Guid.NewGuid();
        var service = new VoiceService { Read = _ => Task.FromResult(source) };
        using var session = new AgentVoiceSettingsSession(service, _ => throw new InvalidOperationException("private provider error"), (_, _) => Task.CompletedTask, NullLogger<AgentVoiceSettingsSession>.Instance);
        await session.LoadAsync();
        Assert.True(session.Presentation.ProvidersUnavailable);
        Assert.Empty(session.Presentation.Providers);
        Assert.Equal(source.SpeechToText.ProviderProfileId, session.Presentation.Source!.SpeechProviderId);
        Assert.Equal(source.TextToSpeech.ProviderProfileId, session.Presentation.Source.SynthesisProviderId);
        Assert.Equal(VoiceSettingsStatus.Ready, session.Presentation.Status);
        source.SpeechToText.Model = "externally mutated";
        Assert.NotEqual(source.SpeechToText.Model, session.Presentation.Source.SpeechModel);
        service.Read = _ => throw new InvalidOperationException("private settings error");
        await session.LoadAsync();
        Assert.Null(session.Presentation.Source);
        Assert.Equal(VoiceSettingsStatus.LoadFailed, session.Presentation.Status);
        Assert.DoesNotContain("private", session.Presentation.StatusText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(VoiceSettingsStatus.SaveFailed)]
    [InlineData(VoiceSettingsStatus.SavedSynthesisFailed)]
    [InlineData(VoiceSettingsStatus.SavedPlaybackFailed)]
    [InlineData(VoiceSettingsStatus.SamplePlayed)]
    public async Task Voice_sample_saves_once_before_synthesis_and_distinguishes_later_failures(VoiceSettingsStatus expected) {
        var effects = new List<string>();
        var service = new VoiceService {
            Save = (settings, _) => {
                effects.Add("save");
                return expected == VoiceSettingsStatus.SaveFailed ? throw new InvalidOperationException("private save error") : Task.FromResult(settings);
            },
            Sample = (_, _) => {
                effects.Add("synthesize");
                return expected == VoiceSettingsStatus.SavedSynthesisFailed ? throw new InvalidOperationException("private synthesis error") : Task.FromResult(Audio);
            }
        };
        using var session = Voice(service, (_, _) => {
            effects.Add("play");
            return expected == VoiceSettingsStatus.SavedPlaybackFailed ? throw new InvalidOperationException("private playback error") : Task.CompletedTask;
        });
        await session.LoadAsync();
        var original = session.Presentation.Source!;
        var submission = original with { SampleText = "Immutable sample", SpeechModel = "model-edited", Instructions = "Preserved hidden instructions" };
        await session.ExecuteAsync(new(session.Presentation.Generation, VoiceSettingsAction.PlaySample, submission));
        Assert.Equal(expected, session.Presentation.Status);
        Assert.Equal(1, service.Saves);
        Assert.Equal(expected == VoiceSettingsStatus.SaveFailed ? ["save"] : expected == VoiceSettingsStatus.SavedSynthesisFailed ? ["save", "synthesize"] : new[] { "save", "synthesize", "play" }, effects);
        Assert.Equal(expected == VoiceSettingsStatus.SaveFailed ? original : submission, session.Presentation.Source);
        Assert.DoesNotContain("private", session.Presentation.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Voice_single_flight_captures_independent_backend_settings_and_disposal_suppresses_sample() {
        var pending = new TaskCompletionSource<AgentVoiceSettings>();
        var service = new VoiceService { Save = (_, _) => pending.Task };
        using var session = Voice(service);
        await session.LoadAsync();
        var submission = session.Presentation.Source! with { SpeechModel = "Captured", SampleText = "Captured sample" };
        var intent = new VoiceSettingsIntent(session.Presentation.Generation, VoiceSettingsAction.PlaySample, submission);
        var save = session.ExecuteAsync(intent);
        await session.ExecuteAsync(intent);
        await session.ExecuteAsync(intent with { Action = VoiceSettingsAction.Save });
        Assert.Equal(1, service.Saves);
        Assert.Equal("Captured", service.Saved!.SpeechToText.Model);
        service.Saved.SpeechToText.Model = "Backend changed its input";
        Assert.Equal("Captured", submission.SpeechModel);
        session.Dispose();
        Assert.True(service.SaveToken.IsCancellationRequested);
        using var registration = service.SaveToken.Register(() => { });
        pending.SetResult(service.Saved);
        await save;
        Assert.Equal(0, service.Samples);
        Assert.NotEqual("Backend changed its input", session.Presentation.Source!.SpeechModel);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Floating_stale_load_and_disposal_suppress_late_publication(bool dispose) {
        var pending = new TaskCompletionSource<FloatingAgentChatSettings>();
        var service = new FloatingSettingsService { Read = _ => pending.Task };
        using var session = Floating(service);
        var first = session.LoadAsync();
        var token = service.ReadToken;
        if (dispose) {
            session.Dispose();
        } else {
            service.Read = _ => Task.FromResult(new FloatingAgentChatSettings(MaximumActiveChats: 7));
            await session.LoadAsync();
        }
        Assert.True(token.IsCancellationRequested);
        using var registration = token.Register(() => { });
        pending.SetResult(new(MaximumActiveChats: 15));
        await first;
        Assert.Equal(dispose ? null : 7, session.Presentation.Source?.MaximumActiveChats);
    }

    [Fact]
    public async Task Floating_validation_does_not_change_canonical_state_and_save_is_single_flight() {
        var pending = new TaskCompletionSource<FloatingAgentChatSettings>();
        var service = new FloatingSettingsService { Save = (_, _) => pending.Task };
        var applied = new List<FloatingAgentChatSettings>();
        using var session = Floating(service, applied.Add);
        await session.LoadAsync();
        var original = session.Presentation.Source!;
        await session.ExecuteAsync(new(session.Presentation.Generation, FloatingChatSettingsAction.Save, original with { MaximumActiveChats = 0 }));
        Assert.Equal(FloatingChatSettingsStatus.ValidationFailed, session.Presentation.Status);
        Assert.Same(original, session.Presentation.Source);
        Assert.Equal(0, service.Saves);
        var captured = original with { MaximumActiveChats = 5 };
        var intent = new FloatingChatSettingsIntent(session.Presentation.Generation, FloatingChatSettingsAction.Save, captured);
        var save = session.ExecuteAsync(intent);
        await session.ExecuteAsync(intent with { Submission = captured with { MaximumActiveChats = 9 } });
        Assert.Equal(1, service.Saves);
        Assert.Equal(5, service.Saved!.MaximumActiveChats);
        pending.SetResult(service.Saved);
        await save;
        Assert.Equal(captured, session.Presentation.Source);
        Assert.Single(applied);
    }

    [Fact]
    public async Task Floating_disposal_after_save_started_suppresses_apply_and_notifications() {
        var pending = new TaskCompletionSource<FloatingAgentChatSettings>();
        var service = new FloatingSettingsService { Save = (_, _) => pending.Task };
        var applied = false;
        using var session = Floating(service, _ => applied = true);
        await session.LoadAsync();
        var task = session.ExecuteAsync(new(session.Presentation.Generation, FloatingChatSettingsAction.Save, session.Presentation.Source));
        session.Dispose();
        Assert.True(service.SaveToken.IsCancellationRequested);
        pending.SetResult(service.Saved!);
        await task;
        Assert.False(applied);
    }

    private static readonly AgentVoiceSynthesisResult Audio = new([1, 2, 3], "audio/wav", "synthetic", "sample", "wav");
    private static AgentVoiceSettingsSession Voice(VoiceService service, Func<AgentVoiceSynthesisResult, CancellationToken, Task>? play = null) => new(
        service, _ => Task.FromResult(ImmutableArray<VoiceSettingsProvider>.Empty), play ?? ((_, _) => Task.CompletedTask), NullLogger<AgentVoiceSettingsSession>.Instance);
    private static FloatingAgentChatSettingsSession Floating(FloatingSettingsService service, Action<FloatingAgentChatSettings>? apply = null) => new(service, apply ?? (_ => { }), NullLogger<FloatingAgentChatSettingsSession>.Instance);
    [Fact]
    public void Floating_persistence_success_followed_by_coordinator_failure_is_a_saved_warning() {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddLogging();
        var settings = new FloatingSettingsService();
        var coordinator = DispatchProxy.Create<IFloatingAgentChatCoordinator, CoordinatorProxy>();
        ((CoordinatorProxy)(object)coordinator).Apply = _ => throw new InvalidOperationException("private coordinator failure");
        context.Services.AddSingleton<IFloatingAgentChatSettingsService>(settings);
        context.Services.AddSingleton(coordinator);
        var cut = context.Render<FloatingAgentChatSettingsPanel>();
        cut.Find("[data-testid='floating-agent-chat-settings-save']").Click();
        Assert.Equal(1, settings.Saves);
        Assert.Contains("Saved with runtime application warning", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("private coordinator failure", cut.Markup, StringComparison.Ordinal);
    }
    public class CoordinatorProxy : DispatchProxy {
        public Action<FloatingAgentChatSettings> Apply { get; set; } = _ => { };
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            if (targetMethod?.Name != nameof(IFloatingAgentChatCoordinator.ApplySettings)) {
                throw new NotSupportedException();
            }
            Apply((FloatingAgentChatSettings)args![0]!);
            return null;
        }
    }
    private sealed class FloatingSettingsService : IFloatingAgentChatSettingsService {
        public int Saves { get; private set; }
        public FloatingAgentChatSettings? Saved { get; private set; }
        public CancellationToken ReadToken { get; private set; }
        public CancellationToken SaveToken { get; private set; }
        public Func<CancellationToken, Task<FloatingAgentChatSettings>> Read { get; set; } = _ => Task.FromResult(FloatingAgentChatSettings.Default);
        public Func<FloatingAgentChatSettings, CancellationToken, Task<FloatingAgentChatSettings>> Save { get; set; } = (value, _) => Task.FromResult(value);
        public Task<FloatingAgentChatSettings> GetSettingsAsync(CancellationToken cancellationToken = default) {
            ReadToken = cancellationToken;
            return Read(cancellationToken);
        }
        public Task<FloatingAgentChatSettings> SaveSettingsAsync(FloatingAgentChatSettings settings, CancellationToken cancellationToken = default) {
            Saves++;
            Saved = settings;
            SaveToken = cancellationToken;
            return Save(settings, cancellationToken);
        }
    }
    private sealed class VoiceService : IAgentVoiceService {
        public int Saves { get; private set; }
        public int Samples { get; private set; }
        public AgentVoiceSettings? Saved { get; private set; }
        public CancellationToken SaveToken { get; private set; }
        public Func<CancellationToken, Task<AgentVoiceSettings>> Read { get; set; } = _ => Task.FromResult(AgentVoiceSettings.Default);
        public Func<AgentVoiceSettings, CancellationToken, Task<AgentVoiceSettings>> Save { get; set; } = (value, _) => Task.FromResult(value);
        public Func<string?, CancellationToken, Task<AgentVoiceSynthesisResult>> Sample { get; set; } = (_, _) => Task.FromResult(Audio);
        public Task<AgentVoiceSettings> GetSettingsAsync(CancellationToken cancellationToken = default) => Read(cancellationToken);
        public Task<AgentVoiceSettings> SaveSettingsAsync(AgentVoiceSettings settings, CancellationToken cancellationToken = default) {
            Saves++;
            Saved = settings;
            SaveToken = cancellationToken;
            return Save(settings, cancellationToken);
        }
        public Task<AgentVoiceSynthesisResult> SynthesizeSampleAsync(string? sampleText = null, CancellationToken cancellationToken = default) {
            Samples++;
            return Sample(sampleText, cancellationToken);
        }
        public Task<AgentVoiceTranscriptionResult> TranscribeAsync(AgentVoiceTranscriptionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AgentVoiceSynthesisResult> SynthesizeAsync(AgentVoiceSynthesisRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<AgentVoiceSynthesisResult> SynthesizeChunksAsync(AgentVoiceSynthesisRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
