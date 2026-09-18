using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Settings;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public sealed class FloatingAgentChatSettingsSession(IFloatingAgentChatSettingsService settings,
    Action<FloatingAgentChatSettings> apply, ILogger<FloatingAgentChatSettingsSession> logger) : IDisposable {
    private CancellationTokenSource? operation;
    private bool disposed;
    private static readonly FloatingChatSettingsLimits Limits = new(FloatingAgentChatSettingsValidator.MaximumRetentionMinutes,
        FloatingAgentChatSettingsValidator.MaximumActiveChatLimit, FloatingAgentChatSettingsValidator.MaximumPreparedAgentLimit);
    public FloatingChatSettingsPresentation Presentation { get; private set; } = new(Limits: Limits);
    public event Action? Changed;
    public bool IsCurrent(long generation) => !disposed && Presentation.Generation == generation;

    public async Task LoadAsync() {
        if (disposed) {
            return;
        }
        Stop();
        using var request = new CancellationTokenSource();
        operation = request;
        Presentation = new(Presentation.Generation + 1, Limits: Limits);
        Changed?.Invoke();
        try {
            var loaded = await settings.GetSettingsAsync(request.Token);
            if (Owns(request)) {
                Presentation = Presentation with { Source = ToValues(loaded), Status = FloatingChatSettingsStatus.Ready };
                Changed?.Invoke();
            }
        } catch (OperationCanceledException) when (request.IsCancellationRequested) {
        } catch (Exception exception) {
            Fail(request, exception, FloatingChatSettingsStatus.LoadFailed);
        } finally {
            End(request);
        }
    }
    public async Task ExecuteAsync(FloatingChatSettingsIntent intent) {
        if (!IsCurrent(intent.Generation) || Presentation.IsBusy || operation is not null) {
            return;
        }
        if (intent.Action == FloatingChatSettingsAction.Reload) {
            await LoadAsync();
            return;
        }
        if (Presentation.Source is null || intent.Submission is not { } values || intent.Action != FloatingChatSettingsAction.Save) {
            return;
        }
        var submission = new FloatingAgentChatSettings(values.HiddenActiveChatRetentionMinutes, values.MaximumActiveChats,
            values.MaximumPreparedAgents, values.AdaptivePreparationEnabled, values.PreparedResourceIdleRetentionMinutes);
        try {
            FloatingAgentChatSettingsValidator.Validate(submission);
        } catch (ArgumentOutOfRangeException) {
            SetStatus(FloatingChatSettingsStatus.ValidationFailed);
            return;
        }
        using var request = new CancellationTokenSource();
        operation = request;
        SetStatus(FloatingChatSettingsStatus.Saving);
        var failure = FloatingChatSettingsStatus.SaveFailed;
        try {
            var saved = await settings.SaveSettingsAsync(submission, request.Token);
            if (!Owns(request)) {
                return;
            }
            Presentation = Presentation with { Source = ToValues(saved) };
            failure = FloatingChatSettingsStatus.SavedWithRuntimeWarning;
            apply(saved);
            if (Owns(request)) {
                SetStatus(FloatingChatSettingsStatus.Saved);
            }
        } catch (OperationCanceledException) when (request.IsCancellationRequested) {
        } catch (Exception exception) {
            Fail(request, exception, failure);
        } finally {
            End(request);
        }
    }
    private void Fail(CancellationTokenSource request, Exception exception, FloatingChatSettingsStatus status) {
        if (Owns(request)) {
            logger.LogWarning("Floating chat settings {Status} for generation {Generation} with {ExceptionType}", status, Presentation.Generation, exception.GetType().Name);
            SetStatus(status);
        }
    }
    private void SetStatus(FloatingChatSettingsStatus status) {
        Presentation = Presentation with { Status = status };
        Changed?.Invoke();
    }
    private bool Owns(CancellationTokenSource request) => !disposed && ReferenceEquals(operation, request) && !request.IsCancellationRequested;
    private void End(CancellationTokenSource request) {
        if (ReferenceEquals(operation, request)) {
            operation = null;
        }
    }
    private void Stop() {
        var previous = operation;
        operation = null;
        previous?.Cancel();
    }
    public void Dispose() {
        disposed = true;
        Stop();
    }
    private static FloatingChatSettingsValues ToValues(FloatingAgentChatSettings value) => new(value.HiddenActiveChatRetentionMinutes,
        value.MaximumActiveChats, value.MaximumPreparedAgents, value.AdaptivePreparationEnabled, value.PreparedResourceIdleRetentionMinutes);
}
