using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.UI;
using CanDoItAll.Components.BaseLib;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Components;

public sealed class LlmChatDefinitionCatalogSession(ILlmChatDefinitionUiGateway definitions,
    Func<LlmChatDefinitionListItem, DefinitionCatalogCard> project, ILogger<LlmChatDefinitionCatalogSession> logger) : IDisposable {
    private static readonly TimeSpan SearchDebounce = TimeSpan.FromMilliseconds(250);
    public static DefinitionCatalogFilterLimits Limits { get; } = new(LlmChatDefinitionQuery.MaximumSearchLength,
        LlmChatDefinitionQuery.MaximumTagFilters, LlmChatDefinitionValidation.MaximumTagLength);
    private CancellationTokenSource? active;
    private CancellationTokenSource? debounce;
    private LlmChatDefinitionCursor? nextCursor;
    private long generation;
    private bool disposed;
    public LlmChatUiAuthorizationSnapshot? Authorization { get; private set; }
    public LlmChatDefinitionQuery? AppliedQuery { get; private set; }
    public DefinitionCatalogPresentation Presentation { get; private set; } = DefinitionCatalogPresentation.Initial;
    public event Action? Changed;

    public void AcceptAuthorization(LlmChatUiAuthorizationSnapshot authorization) {
        if (disposed) {
            return;
        }
        Authorization = authorization;
        Presentation = Presentation with { CanRead = authorization.CanRead, CanManage = authorization.CanRead && authorization.CanManage,
            Phase = DefinitionCatalogPhase.Ready, Failure = null };
        Changed?.Invoke();
    }

    public void FailAuthorization() {
        if (!disposed) {
            Presentation = Presentation with { Phase = DefinitionCatalogPhase.Failed, Failure = DefinitionCatalogFailure.AuthorizationUnavailable };
            Changed?.Invoke();
        }
    }

    public Task ApplyAsync(DefinitionCatalogIntent intent) {
        if (disposed || !Presentation.CanRead) {
            return Task.CompletedTask;
        }
        var filters = Presentation.Filters;
        switch (intent) {
            case DefinitionCatalogIntent.SearchChanged search:
                return ReplaceFiltersAsync(filters with { Search = search.Value }, delay: true);
            case DefinitionCatalogIntent.TagsChanged tags:
                return ReplaceFiltersAsync(filters with { Tags = TagTextValueNormalizer.NormalizeTags(tags.Value, Limits.TagCount).ToImmutableArray() });
            case DefinitionCatalogIntent.StatusChanged status:
                return ReplaceFiltersAsync(filters with { Status = status.Value });
            case DefinitionCatalogIntent.ResetFilters:
                return ReplaceFiltersAsync(DefinitionCatalogFilters.Empty);
            case DefinitionCatalogIntent.LoadMore when !Presentation.IsBusy && nextCursor is not null:
                return LoadAsync(append: true);
            default:
                return Task.CompletedTask;
        }
    }

    public Task ReloadAsync() {
        if (disposed || !Presentation.CanRead) {
            return Task.CompletedTask;
        }
        CancelDebounce();
        return LoadAsync(append: false);
    }

    private async Task ReplaceFiltersAsync(DefinitionCatalogFilters filters, bool delay = false) {
        CancelDebounce();
        CancelList();
        nextCursor = null;
        Presentation = Presentation with { Filters = filters, HasMore = false, Failure = null,
            Phase = Presentation.Cards.IsEmpty ? DefinitionCatalogPhase.Loading : DefinitionCatalogPhase.Refreshing };
        if (!delay) {
            await LoadAsync(append: false);
            return;
        }
        using var owner = new CancellationTokenSource();
        debounce = owner;
        Changed?.Invoke();
        try {
            await Task.Delay(SearchDebounce, owner.Token);
            if (!disposed && ReferenceEquals(debounce, owner) && !owner.IsCancellationRequested) {
                await LoadAsync(append: false);
            }
        } catch (OperationCanceledException) when (owner.IsCancellationRequested) {
        } finally {
            if (ReferenceEquals(debounce, owner)) {
                debounce = null;
            }
        }
    }

    private async Task LoadAsync(bool append) {
        CancelList();
        var requested = ++generation;
        using var owner = new CancellationTokenSource();
        active = owner;
        Presentation = Presentation with { Failure = null,
            Phase = Presentation.Cards.IsEmpty ? DefinitionCatalogPhase.Loading : DefinitionCatalogPhase.Refreshing };
        Changed?.Invoke();
        try {
            if (!IsCurrent(owner)) {
                return;
            }
            var filters = Presentation.Filters;
            try {
                AppliedQuery = new(take: 24, status: LlmChatDefinitionPresentationMapper.ToStatus(filters.Status),
                    cursor: append ? nextCursor : null, searchText: filters.Search, tags: filters.Tags);
            } catch (ArgumentException) {
                Presentation = Presentation with { Failure = DefinitionCatalogFailure.InvalidFilters, Phase = DefinitionCatalogPhase.Failed };
                return;
            }
            var result = await definitions.ListPageAsync(AppliedQuery, owner.Token);
            if (!IsCurrent(owner)) {
                return;
            }
            if (result.IsFailure) {
                Presentation = Presentation with { Failure = DefinitionCatalogFailure.Unavailable, Phase = DefinitionCatalogPhase.Failed };
                logger.LogWarning("Definition catalog request {Generation} was rejected with {FailureCount} public failures.", requested, result.Failures.Count);
                return;
            }
            var cards = result.Value!.Items.Select(project).Select(card => card with { Tags = card.Tags.ToImmutableArray() }).ToImmutableArray();
            nextCursor = result.Value.NextCursor;
            Presentation = Presentation with { Cards = append ? Presentation.Cards.AddRange(cards) : cards,
                HasMore = nextCursor is not null, Phase = DefinitionCatalogPhase.Ready };
        } catch (OperationCanceledException) when (owner.IsCancellationRequested) {
        } catch (Exception exception) {
            if (IsCurrent(owner)) {
                logger.LogWarning("Definition catalog request {Generation} failed ({ExceptionType}).", requested, exception.GetType().Name);
                Presentation = Presentation with { Failure = DefinitionCatalogFailure.Unavailable, Phase = DefinitionCatalogPhase.Failed };
            }
        } finally {
            if (IsCurrent(owner)) {
                active = null;
                Changed?.Invoke();
            }
        }
    }

    private bool IsCurrent(CancellationTokenSource owner) => !disposed && ReferenceEquals(active, owner) && !owner.IsCancellationRequested;
    private void CancelList() {
        var previous = active;
        active = null;
        previous?.Cancel();
    }
    private void CancelDebounce() {
        var previous = debounce;
        debounce = null;
        previous?.Cancel();
    }
    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        Changed = null;
        CancelDebounce();
        CancelList();
    }
}
