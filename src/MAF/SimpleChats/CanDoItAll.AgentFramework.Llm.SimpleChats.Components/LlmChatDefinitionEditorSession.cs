using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.UI;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Components;

public sealed class LlmChatDefinitionEditorSession(ILlmChatDefinitionUiGateway definitions,
    ILlmChatProviderUiGateway providers, ILlmChatUiAuthorizationFacade authorization,
    ILogger<LlmChatDefinitionEditorSession> logger, ILlmChatDefinitionEditorAccess editorAccess, Func<Action, Task> dispatch) : IDisposable, IAsyncDisposable {
    private CancellationTokenSource? read;
    private CancellationTokenSource? mutation;
    private LlmChatDefinitionEditor? accepted;
    private LlmChatDefinitionEditor? reviewed;
    private DefinitionEditorValues? draft;
    private long inputRevision;
    private long reviewInputRevision;
    private long remoteRevision;
    private long reviewRemoteRevision;
    private LlmChatEditorOpening? opening;
    private Task? pendingDisposal;
    private readonly Guid editorId = Guid.NewGuid();
    private MergeUndo? mergeUndo;
    private sealed record MergeUndo(DefinitionEditorValues Draft, long InputRevision);
    private Guid? target;
    private long generation;
    private bool observedTarget;
    private bool completed;
    private bool disposed;

    public DefinitionEditorPresentation Presentation { get; private set; } = DefinitionEditorPresentation.Initial;
    public event Action? Changed;
    public bool IsCurrent(long expected) => !disposed && expected == generation;
    public bool HasUnfinishedDraft => !completed && (mutation is not null || Presentation.Unconfirmed || draft is not null &&
        !DefinitionEditorComparison.Same(draft, accepted is null ? new() : LlmChatDefinitionEditorForm.From(accepted)));

    public Task SetTargetAsync(Guid? definitionId) {
        if (disposed || observedTarget && target == definitionId) {
            return Task.CompletedTask;
        }
        observedTarget = true;
        target = definitionId;
        return LoadAsync();
    }

    public Task ReloadAsync(long expected) => IsCurrent(expected) && !completed && mutation is null
        ? CompareAsync(expected) : Task.CompletedTask;

    public void UpdateDraft(DefinitionEditorIntent intent) {
        if (!IsCurrent(intent.Generation) || completed || intent.Submission is null) {
            return;
        }
        draft = intent.Submission with { Tags = intent.Submission.Tags.ToImmutableArray() };
        inputRevision++;
        reviewed = null;
        Presentation = Presentation with { Source = draft, Comparison = null, ConfirmDiscard = false, Saved = false, CanUndoComparison = false };
        Publish();
    }

    public void ApplyComparison(long expected, bool acknowledgeUnconfirmed) {
        if (!IsCurrent(expected) || mutation is not null || read is not null || reviewed is null ||
            Presentation.Unconfirmed && !acknowledgeUnconfirmed || reviewInputRevision != inputRevision || reviewRemoteRevision != remoteRevision ||
            Presentation.Comparison is not { IsResolved: true } comparison) {
            return;
        }
        mergeUndo = new(draft!, inputRevision + 1);
        draft = comparison.Apply();
        accepted = reviewed;
        reviewed = null;
        inputRevision++;
        Presentation = Presentation with { Source = draft, Comparison = null, Unconfirmed = false, Failure = null, NeedsReview = false, Saved = false, CanUndoComparison = true,
            Definition = LlmChatDefinitionPresentationMapper.ToCatalogCard(accepted.Definition),
            Notice = Presentation.Unconfirmed ? "The earlier save remains unconfirmed. This draft uses the reviewed current version for a separate new save." : "Comparison applied to your draft. Save separately." };
        Publish();
    }

    public void UndoComparison(long expected) {
        if (!IsCurrent(expected) || Presentation.Phase != DefinitionEditorPhase.Ready || !Presentation.CanUndoComparison ||
            mutation is not null || read is not null || accepted is not { } current ||
            mergeUndo is not { } undo || undo.InputRevision != inputRevision) {
            return;
        }
        draft = undo.Draft;
        mergeUndo = null;
        reviewed = null;
        inputRevision++;
        Presentation = Presentation with { Source = draft, Comparison = null, CanUndoComparison = false, NeedsReview = true, Saved = false,
            Definition = LlmChatDefinitionPresentationMapper.ToCatalogCard(current.Definition), Failure = DefinitionEditorFailure.Conflict,
            Notice = "The pre-comparison draft is restored locally. Compare current state again before saving." };
        Publish();
    }

    private async Task CompareAsync(long expected) {
        if (accepted is null || draft is null || read is not null) {
            return;
        }
        var original = accepted;
        var local = draft;
        var revision = inputRevision;
        var remote = remoteRevision;
        using var request = new CancellationTokenSource();
        read = request;
        reviewed = null;
        Presentation = Presentation with { IsComparing = true, Comparison = null };
        Publish();
        try {
            if (!await authorization.IsAllowedAsync(LlmChatUiPermission.Manage, request.Token)) {
                Presentation = Presentation with { Phase = DefinitionEditorPhase.Denied, Failure = DefinitionEditorFailure.Authorization };
                return;
            }
            await opening!.AuthorizeAsync(false, request.Token);
            var result = await definitions.GetEditorAsync(original.Definition.DefinitionId, request.Token);
            if (!OwnsRead(expected, request)) {
                return;
            }
            await opening.AuthorizeAsync(false, request.Token);
            if (!await authorization.IsAllowedAsync(LlmChatUiPermission.Manage, request.Token)) {
                Presentation = Presentation with { Phase = DefinitionEditorPhase.Denied, Failure = DefinitionEditorFailure.Authorization };
                return;
            }
            if (!OwnsRead(expected, request)) {
                return;
            }
            if (result.IsFailure || result.Value?.Definition.DefinitionId != original.Definition.DefinitionId) {
                Presentation = Presentation with { Failure = DefinitionEditorFailure.Unavailable };
                return;
            }
            if (revision != inputRevision || remote != remoteRevision) {
                Presentation = Presentation with { Notice = "Your draft or the saved definition changed during the read. Compare again." };
                return;
            }
            reviewed = result.Value with { Definition = result.Value.Definition with { Tags = result.Value.Definition.Tags.ToImmutableArray() } };
            reviewInputRevision = revision;
            reviewRemoteRevision = remote;
            Presentation = Presentation with { Comparison = new(LlmChatDefinitionEditorForm.From(original), local, LlmChatDefinitionEditorForm.From(reviewed)) };
        } catch (UnauthorizedAccessException) {
            Retire();
        } catch (OperationCanceledException) when (request.IsCancellationRequested) {
        } catch (Exception exception) {
            if (OwnsRead(expected, request)) {
                LogFailure(exception, expected, "comparison");
                Presentation = Presentation with { Failure = DefinitionEditorFailure.Unavailable };
            }
        } finally {
            if (OwnsRead(expected, request)) {
                read = null;
                Presentation = Presentation with { IsComparing = false };
                Publish();
            }
        }
    }

    private async Task LoadAsync() {
        Stop(ref read);
        Stop(ref mutation);
        var owner = ++generation;
        var previous = opening;
        opening = null;
        if (previous is not null) {
            await previous.DisposeAsync();
        }
        if (!IsCurrent(owner)) {
            return;
        }
        accepted = null;
        mergeUndo = null;
        reviewed = null;
        draft = null;
        inputRevision++;
        completed = false;
        Presentation = new(owner, target, DefinitionEditorPhase.Loading);
        Publish();
        if (target == Guid.Empty) {
            Presentation = Presentation with { Phase = DefinitionEditorPhase.Failed, Failure = DefinitionEditorFailure.InvalidTarget };
            Publish();
            return;
        }
        using var request = new CancellationTokenSource();
        read = request;
        var requestedTarget = target;
        try {
            var access = await authorization.GetAsync(request.Token);
            if (!OwnsRead(owner, request)) {
                return;
            }
            if (!access.CanManage) {
                Presentation = Presentation with { Phase = DefinitionEditorPhase.Denied, Failure = DefinitionEditorFailure.Authorization };
                return;
            }
            var acquired = await editorAccess.OpenAsync(requestedTarget, editorId,
                () => dispatch(() => ReceiveHint(owner)), () => dispatch(() => { if (IsCurrent(owner)) { Retire(); } }), request.Token);
            if (!OwnsRead(owner, request)) {
                await acquired.DisposeAsync();
                return;
            }
            opening = acquired;
            await opening.AuthorizeAsync(false, request.Token);
            if (requestedTarget is { } id) {
                var result = await definitions.GetEditorAsync(id, request.Token);
                if (!OwnsRead(owner, request)) {
                    return;
                }
                await acquired.AuthorizeAsync(false, request.Token);
                if (!OwnsRead(owner, request)) {
                    return;
                }
                if (result.IsFailure || result.Value?.Definition.DefinitionId != id) {
                    Presentation = Presentation with { Phase = DefinitionEditorPhase.Failed, Failure = DefinitionEditorFailure.Unavailable };
                    return;
                }
                accepted = result.Value with { Definition = result.Value.Definition with { Tags = result.Value.Definition.Tags.ToImmutableArray() } };
            }
            draft = accepted is null ? new() : LlmChatDefinitionEditorForm.From(accepted);
            Presentation = Presentation with {
                Phase = DefinitionEditorPhase.Ready,
                Source = draft,
                Definition = accepted is null ? null : LlmChatDefinitionPresentationMapper.ToCatalogCard(accepted.Definition),
                ProvidersLoading = true
            };
            Publish();
            try {
                var result = await providers.ListAsync(request.Token);
                if (!OwnsRead(owner, request)) {
                    return;
                }
                await acquired.AuthorizeAsync(false, request.Token);
                if (!OwnsRead(owner, request)) {
                    return;
                }
                Presentation = Presentation with {
                    Providers = result.IsSuccess ? result.Value!.Select(ToProvider).ToImmutableArray() : [],
                    ProvidersUnavailable = result.IsFailure
                };
            } catch (UnauthorizedAccessException) {
                if (IsCurrent(owner) && !completed) {
                    Retire();
                }
            } catch (OperationCanceledException) when (request.IsCancellationRequested) {
            } catch (Exception exception) {
                if (OwnsRead(owner, request)) {
                    LogFailure(exception, owner, "providers");
                    Presentation = Presentation with { ProvidersUnavailable = true };
                }
            }
        } catch (UnauthorizedAccessException) {
            if (IsCurrent(owner) && !completed) {
                Retire();
            }
        } catch (OperationCanceledException) when (request.IsCancellationRequested) {
        } catch (Exception exception) {
            if (OwnsRead(owner, request)) {
                LogFailure(exception, owner, "editor");
                Presentation = Presentation with { Phase = DefinitionEditorPhase.Failed, Failure = DefinitionEditorFailure.Unavailable };
            }
        } finally {
            if (OwnsRead(owner, request)) {
                read = null;
                Presentation = Presentation with { ProvidersLoading = false };
                Publish();
            }
        }
    }

    public async Task<LlmChatDefinitionListItem?> SubmitAsync(DefinitionEditorIntent intent) {
        if (!IsCurrent(intent.Generation) || completed || mutation is not null || read is not null || Presentation.Unconfirmed || Presentation.NeedsReview || Presentation.Saved ||
            Presentation.Comparison is not null || Presentation.Phase != DefinitionEditorPhase.Ready) {
            return null;
        }
        LlmChatDefinitionMutation? submission = null;
        if (intent.Action == DefinitionEditorAction.Save) {
            if (intent.Submission is null) {
                return null;
            }
            if (!LlmChatDefinitionEditorForm.TryCreateMutation(intent.Submission, out submission, out var message)) {
                Presentation = Presentation with { Failure = DefinitionEditorFailure.Validation, ValidationMessage = message };
                Publish();
                return null;
            }
        } else if (intent.Action != DefinitionEditorAction.ChangeStatus || accepted is null || intent.Status is not { } status
            || !DefinitionEditorTransitions.From(Presentation.Definition!.Status).Contains(status)) {
            return null;
        }
        var current = accepted;
        var submitted = intent.Submission ?? draft;
        var submittedRevision = inputRevision;
        var submittedRemoteRevision = remoteRevision;
        var owner = generation;
        using var request = new CancellationTokenSource();
        mutation = request;
        mergeUndo = null;
        Presentation = Presentation with { IsSaving = true, Failure = null, ValidationMessage = "", CanUndoComparison = false };
        Publish();
        try {
            if (!await authorization.IsAllowedAsync(LlmChatUiPermission.Manage, request.Token)) {
                Presentation = Presentation with { Phase = DefinitionEditorPhase.Denied, Failure = DefinitionEditorFailure.Authorization };
                return null;
            }
            await opening!.AuthorizeAsync(true, request.Token);
            using var admission = opening.EnterWrite();
            LlmChatUiResult<LlmChatDefinitionListItem> result;
            LlmChatDefinitionEditor? acceptedEditor = null;
            if (intent.Action == DefinitionEditorAction.ChangeStatus) {
                result = await definitions.ChangeStatusAsync(current!.Definition.DefinitionId,
                    LlmChatDefinitionPresentationMapper.ToStatus(intent.Status!.Value)!.Value,
                    current.Definition.ConcurrencyToken, request.Token);
            } else {
                var saved = current is null
                    ? await definitions.CreateAsync(submission!, request.Token)
                    : await definitions.UpdateAsync(current.Definition.DefinitionId, submission!, current.Definition.ConcurrencyToken, request.Token);
                acceptedEditor = saved.IsSuccess ? saved.Value : null;
                result = saved.IsSuccess ? LlmChatUiResult<LlmChatDefinitionListItem>.Success(saved.Value!.Definition, saved.Warning)
                    : LlmChatUiResult<LlmChatDefinitionListItem>.Failure(saved.Failures.ToArray());
            }
            if (!OwnsMutation(owner, request)) {
                return null;
            }
            if (result.IsFailure) {
                var failure = Classify(result.Failures);
                Presentation = Presentation with { Failure = failure, NeedsReview = failure == DefinitionEditorFailure.Conflict,
                    ValidationMessage = "Review the definition values and try again." };
                return null;
            }
            if (result.Value!.DefinitionId == Guid.Empty || current is not null && result.Value.DefinitionId != current.Definition.DefinitionId) {
                Presentation = Presentation with { Failure = DefinitionEditorFailure.Unconfirmed, Unconfirmed = true };
                return null;
            }
            if (acceptedEditor is not null && submitted is not null) {
                opening.BindAccepted(acceptedEditor.Definition.DefinitionId);
                await opening.AuthorizeAsync(false, request.Token);
                accepted = acceptedEditor with { Definition = acceptedEditor.Definition with { Tags = acceptedEditor.Definition.Tags.ToImmutableArray() } };
                draft = DefinitionEditorComparison.Acknowledge(submitted, draft ?? submitted, LlmChatDefinitionEditorForm.From(accepted));
                Presentation = Presentation with { Source = draft, Definition = LlmChatDefinitionPresentationMapper.ToCatalogCard(accepted.Definition), Failure = null };
            } else if (current is not null) {
                await opening.AuthorizeAsync(false, request.Token);
                accepted = current with { Definition = result.Value! };
                Presentation = Presentation with { Definition = LlmChatDefinitionPresentationMapper.ToCatalogCard(accepted.Definition), Failure = null };
            }
            if (result.Warning is not null) {
                Presentation = Presentation with { Notice = result.Warning, Saved = submittedRevision == inputRevision };
                return null;
            }
            if (submittedRevision != inputRevision || submittedRemoteRevision != remoteRevision || intent.Action == DefinitionEditorAction.ChangeStatus &&
                draft is not null && current is not null && !DefinitionEditorComparison.Same(draft, LlmChatDefinitionEditorForm.From(current))) {
                Presentation = Presentation with { Notice = "Saved. Your additional draft changes are retained for a separate save." };
                return null;
            }
            completed = true;
            return result.Value! with { Tags = result.Value!.Tags.ToImmutableArray() };
        } catch (UnauthorizedAccessException) {
            Retire();
            return null;
        } catch (OperationCanceledException) when (request.IsCancellationRequested) {
            return null;
        } catch (Exception exception) {
            if (OwnsMutation(owner, request)) {
                LogFailure(exception, owner, "mutation");
                Presentation = Presentation with { Failure = DefinitionEditorFailure.Unconfirmed, Unconfirmed = true };
            }
            return null;
        } finally {
            if (OwnsMutation(owner, request)) {
                mutation = null;
                Presentation = Presentation with { IsSaving = false };
                Publish();
            }
        }
    }

    public bool Cancel(long expected, bool discardConfirmed = false) {
        if (!IsCurrent(expected) || completed) {
            return false;
        }
        if (mutation is not null) {
            Presentation = Presentation with { ConfirmDiscard = true, Notice = "Wait for the save outcome before closing this draft." };
            Publish();
            return false;
        }
        if (!discardConfirmed && HasUnfinishedDraft) {
            Presentation = Presentation with { ConfirmDiscard = true };
            Publish();
            return false;
        }
        completed = true;
        generation++;
        Stop(ref read);
        Stop(ref mutation);
        opening?.Stop();
        return true;
    }

    public void Dispose() {
        disposed = true;
        generation++;
        Changed = null;
        Stop(ref read);
        Stop(ref mutation);
        if (opening is { } current) {
            opening = null;
            current.Stop();
            pendingDisposal = current.DisposeAsync().AsTask();
        }
    }

    public async ValueTask DisposeAsync() {
        Dispose();
        if (pendingDisposal is { } pending) {
            await pending;
        }
    }

    private void ReceiveHint(long owner) {
        if (!IsCurrent(owner) || accepted is null) {
            return;
        }
        remoteRevision++;
        reviewed = null;
        Presentation = Presentation with { Comparison = null, NeedsReview = true,
            CanUndoComparison = false,
            Failure = Presentation.Unconfirmed ? DefinitionEditorFailure.Unconfirmed : DefinitionEditorFailure.Conflict,
            Notice = "This definition changed. Your draft is retained; compare current state before saving." };
        Publish();
    }

    private void Retire() {
        opening?.Stop();
        Stop(ref read);
        Stop(ref mutation);
        Presentation = Presentation with { Phase = DefinitionEditorPhase.Denied, Failure = DefinitionEditorFailure.Authorization,
            Comparison = null, IsSaving = false, IsComparing = false, CanUndoComparison = false };
        Publish();
    }

    private static DefinitionEditorProvider ToProvider(LlmChatProviderOptionPresentation provider) => new(
        LlmChatDefinitionPresentationMapper.ToProvider(provider), provider.Models.Select(model => new DefinitionEditorModel(model.Model,
            model.ThinkingEffort.Support switch {
                LlmChatThinkingEffortSupport.Supported => DefinitionEditorEffortSupport.Supported,
                LlmChatThinkingEffortSupport.Unsupported => DefinitionEditorEffortSupport.Unsupported,
                _ => DefinitionEditorEffortSupport.Unknown
            }, model.ThinkingEffort.AllowedEfforts.Select(effort => LlmChatDefinitionEditorForm.ToPresentation(effort)!.Value).ToImmutableArray())).ToImmutableArray());

    private static DefinitionEditorFailure Classify(IReadOnlyList<LlmChatUiFailure> failures) {
        if (failures.Any(failure => failure.Code == LlmChatUiFailureCodes.Forbidden)) {
            return DefinitionEditorFailure.Authorization;
        }
        if (failures.Any(failure => failure.Code == LlmChatErrorCodes.DefinitionConcurrencyConflict)) {
            return DefinitionEditorFailure.Conflict;
        }
        return failures.Any(failure => failure.Code is LlmChatUiFailureCodes.InvalidInput or LlmChatErrorCodes.InvalidRequest)
            ? DefinitionEditorFailure.Validation : DefinitionEditorFailure.Unavailable;
    }
    private bool OwnsRead(long owner, CancellationTokenSource request) => IsCurrent(owner) && ReferenceEquals(read, request) && !request.IsCancellationRequested;
    private bool OwnsMutation(long owner, CancellationTokenSource request) => IsCurrent(owner) && ReferenceEquals(mutation, request) && !request.IsCancellationRequested;
    private void Publish() => Changed?.Invoke();
    private void LogFailure(Exception exception, long owner, string operation)
        => logger.LogWarning("Definition editor {Operation} failed for generation {Generation} with {ExceptionType}", operation, owner, exception.GetType().Name);
    private static void Stop(ref CancellationTokenSource? request) {
        var previous = request;
        request = null;
        previous?.Cancel();
    }
}
