using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Settings;
using CanDoItAll.AgentFramework.Voice;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public sealed class AgentVoiceSettingsSession(IAgentVoiceService voice,
    Func<CancellationToken, Task<ImmutableArray<VoiceSettingsProvider>>> readProviders,
    Func<AgentVoiceSynthesisResult, CancellationToken, Task> play,
    ILogger<AgentVoiceSettingsSession> logger, IAgentSettingsAccess opening) : IDisposable {
    private CancellationTokenSource? read;
    private CancellationTokenSource? write;
    private bool disposed;
    public VoiceSettingsPresentation Presentation { get; private set; } = new(Providers: []);
    public event Action? Changed;
    public bool MustRetain => Presentation.IsBusy || Presentation.Editor.IsDirty || Presentation.Editor.OutcomeUnknown;
    public void Hint() {
        Presentation.Editor.Hint();
        Changed?.Invoke();
    }
    public void Retire() => SetStatus(VoiceSettingsStatus.AccessChanged);
    public bool IsCurrent(long generation) => !disposed && Presentation.Generation == generation;

    public async Task LoadAsync() {
        if (disposed) {
            return;
        }
        if (write is not null) {
            return;
        }
        if (Presentation.Source is not null) {
            await CompareAsync();
            return;
        }
        Stop(ref read);
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
        if (source is not null) {
            Presentation.Editor.Open(source);
        }
        read = null;
        Presentation = Presentation with { Source = source, Providers = providers ?? [], ProvidersUnavailable = providers is null,
            Status = source is null ? VoiceSettingsStatus.LoadFailed : VoiceSettingsStatus.Ready };
        Changed?.Invoke();
    }
    private async Task<VoiceSettingsValues?> ReadSettingsAsync(CancellationTokenSource request) {
        try {
            return ToValues(await opening.ReadAsync(voice.GetSettingsAsync, request.Token));
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
            return await opening.ReadAsync(readProviders, request.Token);
        } catch (OperationCanceledException) when (request.IsCancellationRequested) {
            return null;
        } catch (Exception exception) {
            if (Owns(read, request)) {
                LogFailure(exception, "provider read");
            }
            return null;
        }
    }
    private async Task CompareAsync() {
        if (read is not null) {
            return;
        }
        var revision = Presentation.Editor.InputRevision;
        using var request = new CancellationTokenSource();
        read = request;
        SetStatus(VoiceSettingsStatus.Loading);
        try {
            var current = await opening.ReadAsync(voice.GetSettingsAsync, request.Token);
            if (Owns(read, request)) {
                Presentation.Editor.Compare(ToValues(current), revision);
                SetStatus(VoiceSettingsStatus.Ready);
            }
        } catch (Exception exception) when (exception is not OutOfMemoryException) {
            if (Owns(read, request)) {
                LogFailure(exception, "current settings read");
                SetStatus(exception is UnauthorizedAccessException ? VoiceSettingsStatus.AccessChanged : VoiceSettingsStatus.LoadFailed);
            }
        } finally {
            if (ReferenceEquals(read, request)) {
                read = null;
            }
        }
    }
    public async Task ExecuteAsync(VoiceSettingsIntent intent) {
        if (!IsCurrent(intent.Generation) || Presentation.IsBusy || write is not null || Presentation.Status == VoiceSettingsStatus.AccessChanged) {
            return;
        }
        if (intent.Action is VoiceSettingsAction.Reload or VoiceSettingsAction.Compare) {
            await LoadAsync();
            return;
        }
        if (Presentation.Editor.NeedsReview || Presentation.Editor.OutcomeUnknown) {
            return;
        }
        if (Presentation.Source is null || intent.Submission is not { } submission || intent.Action is not (VoiceSettingsAction.Save or VoiceSettingsAction.PlaySample)) {
            return;
        }
        var editor = Presentation.Editor;
        submission = submission with { UpdatedAtUtc = editor.Original!.UpdatedAtUtc };
        if (!editor.IsDirty) {
            editor.Input(submission);
        }
        var submitted = editor.Draft;
        var submittedRevision = editor.InputRevision;
        using var request = new CancellationTokenSource();
        write = request;
        var failure = VoiceSettingsStatus.SaveFailed;
        try {
            SetStatus(VoiceSettingsStatus.Saving);
            if (intent.Action == VoiceSettingsAction.PlaySample) {
                await opening.AuthorizePreviewAsync(request.Token);
            }
            AgentVoiceSettings saved;
            var warning = false;
            try {
                saved = await opening.WriteAsync(token => voice.SaveSettingsAsync(ToSettings(submission), token), request.Token);
            } catch (SettingsCommittedException<AgentVoiceSettings> committed) {
                saved = committed.Accepted;
                warning = true;
            }
            if (!Owns(write, request)) {
                return;
            }
            var accepted = ToValues(saved);
            editor.Accept(submitted, accepted, submittedRevision);
            Presentation = Presentation with { Source = accepted };
            if (intent.Action == VoiceSettingsAction.Save || warning) {
                SetStatus(warning ? VoiceSettingsStatus.SavedWithWarning : VoiceSettingsStatus.Saved);
                return;
            }
            failure = VoiceSettingsStatus.SavedSynthesisFailed;
            SetStatus(VoiceSettingsStatus.GeneratingSample);
            await opening.AuthorizePreviewAsync(request.Token);
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
        } catch (WorkflowSettingsConcurrencyException) {
            if (Owns(write, request)) {
                editor.Refused();
                SetStatus(VoiceSettingsStatus.Conflict);
            }
        } catch (Exception exception) {
            if (Owns(write, request)) {
                LogFailure(exception, failure.ToString());
                if (failure == VoiceSettingsStatus.SaveFailed) {
                    if (exception is UnauthorizedAccessException) {
                        SetStatus(VoiceSettingsStatus.AccessChanged);
                    } else if (exception is ArgumentException or AgentVoiceException) {
                        SetStatus(VoiceSettingsStatus.SaveFailed);
                    } else {
                        editor.Unknown();
                        SetStatus(VoiceSettingsStatus.Unknown);
                    }
                } else {
                    SetStatus(failure);
                }
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
        UpdatedAtUtc = settings.UpdatedAtUtc,
        SpeechEnabled = settings.SpeechToText.IsEnabled, SpeechDriver = settings.SpeechToText.DriverKind,
        SpeechProviderId = settings.SpeechToText.ProviderProfileId, SpeechModel = settings.SpeechToText.Model,
        Language = settings.SpeechToText.Language, Prompt = settings.SpeechToText.Prompt,
        SynthesisEnabled = settings.TextToSpeech.IsEnabled, SynthesisDriver = settings.TextToSpeech.DriverKind,
        SynthesisProviderId = settings.TextToSpeech.ProviderProfileId, SynthesisModel = settings.TextToSpeech.Model,
        VoiceId = settings.TextToSpeech.VoiceId, ResponseFormat = settings.TextToSpeech.ResponseFormat,
        Instructions = settings.TextToSpeech.Instructions, SampleText = settings.SampleText, DisclosureText = settings.DisclosureText
    };
    private static AgentVoiceSettings ToSettings(VoiceSettingsValues values) => new() {
        UpdatedAtUtc = values.UpdatedAtUtc,
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
