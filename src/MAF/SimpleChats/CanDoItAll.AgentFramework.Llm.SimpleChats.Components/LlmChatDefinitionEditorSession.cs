using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.UI;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Components;

public sealed class LlmChatDefinitionEditorSession(ILlmChatDefinitionUiGateway definitions,
    ILlmChatProviderUiGateway providers, ILlmChatUiAuthorizationFacade authorization,
    ILogger<LlmChatDefinitionEditorSession> logger) : IDisposable {
    private CancellationTokenSource? read;
    private CancellationTokenSource? mutation;
    private LlmChatDefinitionEditor? accepted;
    private Guid? target;
    private long generation;
    private bool observedTarget;
    private bool completed;
    private bool disposed;

    public DefinitionEditorPresentation Presentation { get; private set; } = DefinitionEditorPresentation.Initial;
    public event Action? Changed;
    public bool IsCurrent(long expected) => !disposed && expected == generation;

    public Task SetTargetAsync(Guid? definitionId) {
        if (disposed || observedTarget && target == definitionId) {
            return Task.CompletedTask;
        }
        observedTarget = true;
        target = definitionId;
        return LoadAsync();
    }

    public Task ReloadAsync(long expected) => IsCurrent(expected) && !completed && mutation is null
        ? LoadAsync() : Task.CompletedTask;

    private async Task LoadAsync() {
        Stop(ref read);
        Stop(ref mutation);
        var owner = ++generation;
        accepted = null;
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
            if (requestedTarget is { } id) {
                var result = await definitions.GetEditorAsync(id, request.Token);
                if (!OwnsRead(owner, request)) {
                    return;
                }
                if (result.IsFailure || result.Value?.Definition.DefinitionId != id) {
                    Presentation = Presentation with { Phase = DefinitionEditorPhase.Failed, Failure = DefinitionEditorFailure.Unavailable };
                    return;
                }
                accepted = result.Value with { Definition = result.Value.Definition with { Tags = result.Value.Definition.Tags.ToImmutableArray() } };
            }
            Presentation = Presentation with {
                Phase = DefinitionEditorPhase.Ready,
                Source = accepted is null ? new() : LlmChatDefinitionEditorForm.From(accepted),
                Definition = accepted is null ? null : LlmChatDefinitionPresentationMapper.ToCatalogCard(accepted.Definition),
                ProvidersLoading = true
            };
            Publish();
            try {
                var result = await providers.ListAsync(request.Token);
                if (!OwnsRead(owner, request)) {
                    return;
                }
                Presentation = Presentation with {
                    Providers = result.IsSuccess ? result.Value!.Select(ToProvider).ToImmutableArray() : [],
                    ProvidersUnavailable = result.IsFailure
                };
            } catch (OperationCanceledException) when (request.IsCancellationRequested) {
            } catch (Exception exception) {
                if (OwnsRead(owner, request)) {
                    LogFailure(exception, owner, "providers");
                    Presentation = Presentation with { ProvidersUnavailable = true };
                }
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
        if (!IsCurrent(intent.Generation) || completed || mutation is not null || Presentation.Phase != DefinitionEditorPhase.Ready) {
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
        var owner = generation;
        using var request = new CancellationTokenSource();
        mutation = request;
        Presentation = Presentation with { IsSaving = true, Failure = null, ValidationMessage = "" };
        Publish();
        try {
            LlmChatUiResult<LlmChatDefinitionListItem> result;
            if (intent.Action == DefinitionEditorAction.ChangeStatus) {
                result = await definitions.ChangeStatusAsync(current!.Definition.DefinitionId,
                    LlmChatDefinitionPresentationMapper.ToStatus(intent.Status!.Value)!.Value,
                    current.Definition.ConcurrencyToken, request.Token);
            } else {
                var saved = current is null
                    ? await definitions.CreateAsync(submission!, request.Token)
                    : await definitions.UpdateAsync(current.Definition.DefinitionId, submission!, current.Definition.ConcurrencyToken, request.Token);
                result = saved.IsSuccess ? LlmChatUiResult<LlmChatDefinitionListItem>.Success(saved.Value!.Definition)
                    : LlmChatUiResult<LlmChatDefinitionListItem>.Failure(saved.Failures.ToArray());
            }
            if (!OwnsMutation(owner, request)) {
                return null;
            }
            if (result.IsFailure) {
                Presentation = Presentation with { Failure = Classify(result.Failures), ValidationMessage = "Review the definition values and try again." };
                return null;
            }
            if (current is not null && result.Value!.DefinitionId != current.Definition.DefinitionId) {
                Presentation = Presentation with { Failure = DefinitionEditorFailure.Unavailable };
                return null;
            }
            completed = true;
            return result.Value! with { Tags = result.Value!.Tags.ToImmutableArray() };
        } catch (OperationCanceledException) when (request.IsCancellationRequested) {
            return null;
        } catch (Exception exception) {
            if (OwnsMutation(owner, request)) {
                LogFailure(exception, owner, "mutation");
                Presentation = Presentation with { Failure = DefinitionEditorFailure.Unavailable };
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

    public bool Cancel(long expected) {
        if (!IsCurrent(expected) || completed) {
            return false;
        }
        completed = true;
        generation++;
        Stop(ref read);
        Stop(ref mutation);
        return true;
    }

    public void Dispose() {
        disposed = true;
        generation++;
        Changed = null;
        Stop(ref read);
        Stop(ref mutation);
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
