using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Settings;
using CanDoItAll.AgentFramework.Voice;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public sealed class AgentVoiceSettingsSession(IAgentVoiceService voice,
    Func<CancellationToken, Task<ImmutableArray<VoiceSettingsProvider>>> readProviders,
    Func<AgentVoiceSynthesisResult, CancellationToken, Task> play,
    ILogger<AgentVoiceSettingsSession> logger) : IDisposable {
    private CancellationTokenSource? read;
    private CancellationTokenSource? write;
    private bool disposed;
    public VoiceSettingsPresentation Presentation { get; private set; } = new(Providers: []);
    public event Action? Changed;
    public bool IsCurrent(long generation) => !disposed && Presentation.Generation == generation;

    public async Task LoadAsync() {
        if (disposed) {
            return;
        }
        Stop(ref read);
        Stop(ref write);
        using var request = new CancellationTokenSource();
        read = request;
        Presentation = new(Presentation.Generation + 1, Providers: []);
        Changed?.Invoke();
        var settingsTask = ReadSettingsAsync(request);
        var providersTask = ReadProvidersAsync(request);
        await Task.WhenAll(settingsTask, providersTask);
        if (!Owns(read, request)) {
            return;
        }
        var settings = await settingsTask;
        var providers = await providersTask;
        var source = settings;
        if (source is not null && providers is { } candidates) {
            source = source with {
                SpeechProviderId = ResolveProvider(source.SpeechProviderId, candidates),
                SynthesisProviderId = ResolveProvider(source.SynthesisProviderId, candidates)
            };
        }
        read = null;
        Presentation = Presentation with { Source = source, Providers = providers ?? [], ProvidersUnavailable = providers is null,
            Status = source is null ? VoiceSettingsStatus.LoadFailed : VoiceSettingsStatus.Ready };
        Changed?.Invoke();
    }
    private async Task<VoiceSettingsValues?> ReadSettingsAsync(CancellationTokenSource request) {
        try {
            return ToValues(await voice.GetSettingsAsync(request.Token));
        } catch (OperationCanceledException) when (request.IsCancellationRequested) {
            return null;
        } catch (Exception exception) {
            if (Owns(read, request)) {
                LogFailure(exception, "settings read");
            }
            return null;
        }
    }
    private async Task<ImmutableArray<VoiceSettingsProvider>?> ReadProvidersAsync(CancellationTokenSource request) {
        try {
            return await readProviders(request.Token);
        } catch (OperationCanceledException) when (request.IsCancellationRequested) {
            return null;
        } catch (Exception exception) {
            if (Owns(read, request)) {
                LogFailure(exception, "provider read");
            }
            return null;
        }
    }
    public async Task ExecuteAsync(VoiceSettingsIntent intent) {
        if (!IsCurrent(intent.Generation) || Presentation.IsBusy || write is not null) {
            return;
        }
        if (intent.Action == VoiceSettingsAction.Reload) {
            await LoadAsync();
            return;
        }
        if (Presentation.Source is null || intent.Submission is not { } submission || intent.Action is not (VoiceSettingsAction.Save or VoiceSettingsAction.PlaySample)) {
            return;
        }
        using var request = new CancellationTokenSource();
        write = request;
        var failure = VoiceSettingsStatus.SaveFailed;
        try {
            SetStatus(VoiceSettingsStatus.Saving);
            var saved = await voice.SaveSettingsAsync(ToSettings(submission), request.Token);
            if (!Owns(write, request)) {
                return;
            }
            Presentation = Presentation with { Source = ToValues(saved) };
            if (intent.Action == VoiceSettingsAction.Save) {
                SetStatus(VoiceSettingsStatus.Saved);
                return;
            }
            failure = VoiceSettingsStatus.SavedSynthesisFailed;
            SetStatus(VoiceSettingsStatus.GeneratingSample);
            var synthesis = await voice.SynthesizeSampleAsync(saved.SampleText, request.Token);
            if (!Owns(write, request)) {
                return;
            }
            failure = VoiceSettingsStatus.SavedPlaybackFailed;
            SetStatus(VoiceSettingsStatus.PlayingSample);
            await play(synthesis, request.Token);
            if (Owns(write, request)) {
                SetStatus(VoiceSettingsStatus.SamplePlayed);
            }
        } catch (OperationCanceledException) when (request.IsCancellationRequested) {
        } catch (Exception exception) {
            if (Owns(write, request)) {
                LogFailure(exception, failure.ToString());
                SetStatus(failure);
            }
        } finally {
            if (ReferenceEquals(write, request)) {
                write = null;
            }
        }
    }
    private void SetStatus(VoiceSettingsStatus status) {
        Presentation = Presentation with { Status = status };
        Changed?.Invoke();
    }
    private bool Owns(CancellationTokenSource? owner, CancellationTokenSource request) => !disposed && ReferenceEquals(owner, request) && !request.IsCancellationRequested;
    private void LogFailure(Exception exception, string operation) => logger.LogWarning("Voice {Operation} failed for settings generation {Generation} with {ExceptionType}", operation, Presentation.Generation, exception.GetType().Name);
    private static Guid? ResolveProvider(Guid? current, ImmutableArray<VoiceSettingsProvider> candidates) => current is { } id
        ? candidates.Any(item => item.Id == id) ? id : null : candidates.FirstOrDefault()?.Id;
    private static void Stop(ref CancellationTokenSource? owner) {
        var previous = owner;
        owner = null;
        previous?.Cancel();
    }
    public void Dispose() {
        disposed = true;
        Stop(ref read);
        Stop(ref write);
    }
    internal static VoiceSettingsValues ToValues(AgentVoiceSettings settings) => new() {
        SpeechEnabled = settings.SpeechToText.IsEnabled, SpeechDriver = settings.SpeechToText.DriverKind,
        SpeechProviderId = settings.SpeechToText.ProviderProfileId, SpeechModel = settings.SpeechToText.Model,
        Language = settings.SpeechToText.Language, Prompt = settings.SpeechToText.Prompt,
        SynthesisEnabled = settings.TextToSpeech.IsEnabled, SynthesisDriver = settings.TextToSpeech.DriverKind,
        SynthesisProviderId = settings.TextToSpeech.ProviderProfileId, SynthesisModel = settings.TextToSpeech.Model,
        VoiceId = settings.TextToSpeech.VoiceId, ResponseFormat = settings.TextToSpeech.ResponseFormat,
        Instructions = settings.TextToSpeech.Instructions, SampleText = settings.SampleText, DisclosureText = settings.DisclosureText
    };
    private static AgentVoiceSettings ToSettings(VoiceSettingsValues values) => new() {
        SpeechToText = new() {
            IsEnabled = values.SpeechEnabled, DriverKind = values.SpeechDriver, ProviderProfileId = values.SpeechProviderId,
            Model = values.SpeechModel, Language = values.Language, Prompt = values.Prompt
        },
        TextToSpeech = new() {
            IsEnabled = values.SynthesisEnabled, DriverKind = values.SynthesisDriver, ProviderProfileId = values.SynthesisProviderId,
            Model = values.SynthesisModel, VoiceId = values.VoiceId, ResponseFormat = values.ResponseFormat, Instructions = values.Instructions
        },
        SampleText = values.SampleText, DisclosureText = values.DisclosureText
    };
}
