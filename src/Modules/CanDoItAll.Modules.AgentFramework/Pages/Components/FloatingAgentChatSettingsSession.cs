using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Settings;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public sealed class FloatingAgentChatSettingsSession(IFloatingAgentChatSettingsService settings,
    Action<FloatingAgentChatSettings> apply, ILogger<FloatingAgentChatSettingsSession> logger,
    IAgentSettingsAccess opening) : IDisposable {
    private CancellationTokenSource? operation;
    private bool disposed;
    private static readonly FloatingChatSettingsLimits Limits = new(FloatingAgentChatSettingsValidator.MaximumRetentionMinutes,
        FloatingAgentChatSettingsValidator.MaximumActiveChatLimit, FloatingAgentChatSettingsValidator.MaximumPreparedAgentLimit);
    public FloatingChatSettingsPresentation Presentation { get; private set; } = new(Limits: Limits);
    public event Action? Changed;
    public bool IsCurrent(long generation) => !disposed && Presentation.Generation == generation;
    public bool MustRetain => Presentation.IsBusy || Presentation.Editor.IsDirty || Presentation.Editor.OutcomeUnknown;

    public void Hint() {
        Presentation.Editor.Hint();
        Changed?.Invoke();
    }
    public void Retire() => SetStatus(FloatingChatSettingsStatus.AccessChanged);

    public async Task LoadAsync() {
        if (disposed) {
            return;
        }
        if (operation is not null) {
            if (Presentation.Source is not null || Presentation.Status == FloatingChatSettingsStatus.Saving) {
                return;
            }
            operation.Cancel();
            operation = null;
        }
        if (Presentation.Source is not null) {
            await CompareAsync();
            return;
        }
        using var request = new CancellationTokenSource();
        operation = request;
        Presentation = new(Presentation.Generation + 1, Limits: Limits);
        Changed?.Invoke();
        try {
            var loaded = await ReadAsync(request.Token);
            if (Owns(request)) {
                var values = ToValues(loaded);
                Presentation.Editor.Open(FloatingChatSettingsInput.From(values));
                Presentation = Presentation with { Source = values, Status = FloatingChatSettingsStatus.Ready };
                Changed?.Invoke();
            }
        } catch (Exception exception) when (exception is not OutOfMemoryException) {
            Fail(request, exception, FloatingChatSettingsStatus.LoadFailed);
        } finally {
            End(request);
        }
    }

    private async Task CompareAsync() {
        var editor = Presentation.Editor;
        var revision = editor.InputRevision;
        using var request = new CancellationTokenSource();
        operation = request;
        SetStatus(FloatingChatSettingsStatus.Loading);
        try {
            var current = await ReadAsync(request.Token);
            if (Owns(request)) {
                editor.Compare(FloatingChatSettingsInput.From(ToValues(current)), revision);
                SetStatus(FloatingChatSettingsStatus.Ready);
            }
        } catch (Exception exception) when (exception is not OutOfMemoryException) {
            Fail(request, exception, FloatingChatSettingsStatus.LoadFailed);
        } finally {
            End(request);
        }
    }

    private Task<FloatingAgentChatSettings> ReadAsync(CancellationToken token) => opening.ReadAsync(settings.GetSettingsAsync, token);

    public async Task ExecuteAsync(FloatingChatSettingsIntent intent) {
        if (!IsCurrent(intent.Generation) || operation is not null || Presentation.Status == FloatingChatSettingsStatus.AccessChanged) {
            return;
        }
        if (intent.Action is FloatingChatSettingsAction.Reload or FloatingChatSettingsAction.Compare) {
            await LoadAsync();
            return;
        }
        var editor = Presentation.Editor;
        if (Presentation.Source is null || intent.Submission is not { } values || intent.Action != FloatingChatSettingsAction.Save || editor.NeedsReview || editor.OutcomeUnknown) {
            return;
        }
        var submission = new FloatingAgentChatSettings(values.HiddenActiveChatRetentionMinutes, values.MaximumActiveChats,
            values.MaximumPreparedAgents, values.AdaptivePreparationEnabled, values.PreparedResourceIdleRetentionMinutes) { UpdatedAtUtc = editor.Original!.UpdatedAtUtc };
        try {
            FloatingAgentChatSettingsValidator.Validate(submission);
        } catch (ArgumentOutOfRangeException) {
            SetStatus(FloatingChatSettingsStatus.ValidationFailed);
            return;
        }
        var submitted = editor.IsDirty ? editor.Draft : FloatingChatSettingsInput.From(values);
        if (!editor.IsDirty) {
            editor.Input(submitted);
        }
        var submittedRevision = editor.InputRevision;
        using var request = new CancellationTokenSource();
        operation = request;
        SetStatus(FloatingChatSettingsStatus.Saving);
        try {
            FloatingAgentChatSettings accepted;
            var warning = false;
            try {
                accepted = await opening.WriteAsync(token => settings.SaveSettingsAsync(submission, token), request.Token);
            } catch (SettingsCommittedException<FloatingAgentChatSettings> committed) {
                accepted = committed.Accepted;
                warning = true;
            }
            if (!Owns(request)) {
                return;
            }
            var saved = ToValues(accepted);
            editor.Accept(submitted, FloatingChatSettingsInput.From(saved), submittedRevision);
            Presentation = Presentation with { Source = saved };
            try {
                apply(accepted);
            } catch (Exception exception) when (exception is not OutOfMemoryException) {
                Log(exception, FloatingChatSettingsStatus.SavedWithRuntimeWarning);
                warning = true;
            }
            SetStatus(warning ? FloatingChatSettingsStatus.SavedWithRuntimeWarning : FloatingChatSettingsStatus.Saved);
        } catch (WorkflowSettingsConcurrencyException) {
            editor.Refused();
            SetStatus(FloatingChatSettingsStatus.Conflict);
        } catch (UnauthorizedAccessException exception) {
            Fail(request, exception, FloatingChatSettingsStatus.AccessChanged);
        } catch (ArgumentException exception) {
            Fail(request, exception, FloatingChatSettingsStatus.ValidationFailed);
        } catch (Exception exception) when (exception is not OutOfMemoryException) {
            editor.Unknown();
            Fail(request, exception, FloatingChatSettingsStatus.Unknown);
        } finally {
            End(request);
        }
    }
    private void Fail(CancellationTokenSource request, Exception exception, FloatingChatSettingsStatus status) {
        if (Owns(request)) {
            Log(exception, status);
            SetStatus(exception is UnauthorizedAccessException ? FloatingChatSettingsStatus.AccessChanged : status);
        }
    }
    private void Log(Exception exception, FloatingChatSettingsStatus status) => logger.LogWarning(
        "Floating chat settings {Status} for generation {Generation} with {ExceptionType}", status, Presentation.Generation, exception.GetType().Name);
    private void SetStatus(FloatingChatSettingsStatus status) {
        if (!disposed) {
            Presentation = Presentation with { Status = status };
            Changed?.Invoke();
        }
    }
    private bool Owns(CancellationTokenSource request) => !disposed && ReferenceEquals(operation, request) && !request.IsCancellationRequested;
    private void End(CancellationTokenSource request) {
        if (ReferenceEquals(operation, request)) {
            operation = null;
        }
    }
    public void Dispose() {
        disposed = true;
        operation?.Cancel();
    }
    private static FloatingChatSettingsValues ToValues(FloatingAgentChatSettings value) => new(value.HiddenActiveChatRetentionMinutes,
        value.MaximumActiveChats, value.MaximumPreparedAgents, value.AdaptivePreparationEnabled, value.PreparedResourceIdleRetentionMinutes, value.UpdatedAtUtc);
}
