using CanDoItAll.Prompts.UI.Gallery;

namespace CanDoItAll.Modules.Prompts.Components;

public sealed record PromptGallerySearchContext(
    PromptGalleryConsumer? Consumer,
    string? Provider,
    string? Model,
    bool ShowActualChatModelFilter,
    bool Compact,
    int PageSize)
{
    // Only the pinned context (consumer/provider/model/optional filter) invalidates the current page.
    public bool SharesPinnedContextWith(PromptGallerySearchContext other)
        => Consumer == other.Consumer &&
           string.Equals(Provider, other.Provider, StringComparison.Ordinal) &&
           string.Equals(Model, other.Model, StringComparison.Ordinal) &&
           ShowActualChatModelFilter == other.ShowActualChatModelFilter;
}

public enum PromptGallerySearchCommitKind
{
    // A debounced text filter took effect.
    Live,
    // An explicit filter, clear or paging action.
    Explicit,
    // The session moved off a page that no longer exists.
    Correction
}

// Filters and page a routed owner asks the list to show, for example from its URL.
public sealed record PromptGallerySearchRequest(PromptGalleryFilterValues Filters, int PageIndex)
{
    public bool Matches(PromptGalleryFilterValues filters, int pageIndex)
        => PageIndex == pageIndex &&
           string.Equals(Filters.Text, filters.Text, StringComparison.Ordinal) &&
           Filters.Tags.SequenceEqual(filters.Tags, StringComparer.Ordinal) &&
           Filters.Kind == filters.Kind &&
           Filters.Status == filters.Status &&
           string.Equals(Filters.ProviderFilter, filters.ProviderFilter, StringComparison.Ordinal) &&
           string.Equals(Filters.ModelFilter, filters.ModelFilter, StringComparison.Ordinal) &&
           Filters.FavoritesOnly == filters.FavoritesOnly &&
           Filters.IncludeArchived == filters.IncludeArchived &&
           Filters.ActualChatModelOnly == filters.ActualChatModelOnly;

    public bool SameAs(PromptGallerySearchRequest other)
        => Matches(other.Filters, other.PageIndex);
}

public sealed record PromptGallerySearchCommit(PromptGalleryFilterValues Filters, int PageIndex, PromptGallerySearchCommitKind Kind);

// Owns one search list instance: committed filters, the current page, request generations, debounce and favorite writes.
public sealed class PromptGallerySearchSession : IAsyncDisposable
{
    public const int DebounceMilliseconds = 250;
    private const string SearchFailureSummary = "Prompt gallery search failed";
    private const string SearchFailureDescription = "The prompt gallery search failed. Retry after checking the active data source.";

    private readonly IPromptGalleryService gallery;
    private readonly Func<CancellationToken, Task> debounce;
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? operation;
    private long operationGeneration;
    private PromptGallerySearchContext context = new(null, null, null, false, false, 25);
    private bool configured;
    private PromptGalleryFilterValues filters = PromptGalleryFilterValues.Initial;
    private PromptGalleryPage<PromptGallerySearchItem> page = new([], 0, 25, 0);
    private int requestedPageIndex;
    private IReadOnlyList<string> availableTags = [];
    private string? loadError;
    private Guid? favoriteBusyItemId;
    private bool isLoading = true;
    private bool disposed;

    public PromptGallerySearchSession(IPromptGalleryService gallery, Func<CancellationToken, Task>? debounce = null)
    {
        this.gallery = gallery ?? throw new ArgumentNullException(nameof(gallery));
        this.debounce = debounce ?? (token => Task.Delay(DebounceMilliseconds, token));
        Presentation = BuildPresentation();
    }

    public PromptGallerySearchPresentation Presentation { get; private set; }

    public Func<Task>? Changed { get; set; }

    public Func<PromptGalleryNotice, Task>? NoticeRaised { get; set; }

    // Raised when a user intent or a page correction changes the committed filters or page.
    public Func<PromptGallerySearchCommit, Task>? Committed { get; set; }

    public Task ConfigureAsync(PromptGallerySearchContext next, PromptGallerySearchRequest? request = null)
    {
        ArgumentNullException.ThrowIfNull(next);
        if (disposed)
        {
            return Task.CompletedTask;
        }

        var previous = context;
        var pinnedContextChanged = !configured || !previous.SharesPinnedContextWith(next);
        context = next;
        if (pinnedContextChanged)
        {
            configured = true;
            filters = request?.Filters ?? filters with { ActualChatModelOnly = true };
            return LoadRequestedAsync(request?.PageIndex ?? 0);
        }

        if (request is null || request.Matches(filters, requestedPageIndex))
        {
            // Compact/page-size echoes and unrelated rerenders never issue a new request.
            return Task.CompletedTask;
        }

        filters = request.Filters;
        return LoadRequestedAsync(request.PageIndex);
    }

    public Task ApplyAsync(PromptGallerySearchIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (disposed)
        {
            return Task.CompletedTask;
        }

        return intent switch
        {
            PromptGallerySearchIntent.ChangeFilters change => ChangeFiltersAsync(change.Filters, change.Debounce),
            PromptGallerySearchIntent.ClearFilters => ChangeFiltersAsync(PromptGalleryFilterValues.Cleared, debounce: false),
            PromptGallerySearchIntent.LoadPage load => LoadAsync(Math.Max(0, load.PageIndex), commit: PromptGallerySearchCommitKind.Explicit),
            PromptGallerySearchIntent.Retry => LoadAsync(requestedPageIndex),
            PromptGallerySearchIntent.ToggleFavorite favorite => ToggleFavoriteAsync(favorite.Item),
            _ => throw new NotSupportedException($"The search session does not own the {intent.GetType().Name} intent.")
        };
    }

    public async Task RefreshAsync()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var generation = await LoadAsync(requestedPageIndex);
        await CorrectPageAsync(generation);
    }

    public ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return ValueTask.CompletedTask;
        }

        disposed = true;
        operation?.Cancel();
        lifetime.Cancel();
        lifetime.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task ChangeFiltersAsync(PromptGalleryFilterValues next, bool debounce)
    {
        filters = next;
        await LoadAsync(0, debounce, commit: debounce ? PromptGallerySearchCommitKind.Live : PromptGallerySearchCommitKind.Explicit);
    }

    private async Task LoadRequestedAsync(int pageIndex)
    {
        var generation = await LoadAsync(pageIndex);
        await CorrectPageAsync(generation);
    }

    // A page beyond the last one (a stale link or a list that shrank) moves to the last existing page.
    private async Task CorrectPageAsync(long generation)
    {
        if (!IsCurrent(generation) || loadError is not null)
        {
            return;
        }

        var lastPageIndex = Math.Max(0, page.TotalPages - 1);
        if (requestedPageIndex <= lastPageIndex)
        {
            return;
        }

        await LoadAsync(lastPageIndex, commit: PromptGallerySearchCommitKind.Correction);
    }

    private async Task<long> LoadAsync(
        int pageIndex,
        bool debounce = false,
        string failureSummary = SearchFailureSummary,
        PromptGallerySearchCommitKind? commit = null)
    {
        if (disposed)
        {
            return -1;
        }

        var (current, generation) = ReplaceOperation();
        requestedPageIndex = pageIndex;
        if (debounce)
        {
            // The typed text is already committed; only the request waits, and a newer request replaces this one.
            await PublishAsync();
            try
            {
                await this.debounce(current.Token);
            }
            catch (OperationCanceledException) when (current.IsCancellationRequested)
            {
                return generation;
            }

            if (!IsCurrent(generation))
            {
                return generation;
            }
        }

        if (commit is { } kind)
        {
            await CommitAsync(kind);
            if (!IsCurrent(generation))
            {
                return generation;
            }
        }

        isLoading = true;
        loadError = null;
        await PublishAsync();
        try
        {
            var result = await gallery.SearchAsync(BuildQuery(pageIndex), current.Token);
            if (!IsCurrent(generation))
            {
                return generation;
            }

            page = result;
            availableTags = availableTags
                .Concat(result.Items.SelectMany(item => item.Tags))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (OperationCanceledException) when (current.IsCancellationRequested)
        {
            return generation;
        }
        catch (Exception exception)
        {
            if (!IsCurrent(generation))
            {
                return generation;
            }

            loadError = SearchFailureDescription;
            await RaiseAsync(PromptGalleryNoticeSeverity.Error, failureSummary, exception.Message);
        }
        finally
        {
            // A superseded or disposed request must not clear the busy indicator of the current one.
            if (IsCurrent(generation))
            {
                isLoading = false;
                await PublishAsync();
            }
        }

        return generation;
    }

    private async Task ToggleFavoriteAsync(PromptGallerySearchItem item)
    {
        if (favoriteBusyItemId.HasValue)
        {
            return;
        }

        favoriteBusyItemId = item.Id;
        await PublishAsync();
        var lifetimeToken = lifetime.Token;
        try
        {
            var result = await gallery.SetFavoriteAsync(item.Id, !item.IsFavorite, lifetimeToken);
            if (disposed)
            {
                return;
            }

            if (!result.IsSuccess)
            {
                await RaiseAsync(
                    PromptGalleryNoticeSeverity.Warning,
                    "Favorite was not updated",
                    PromptGalleryNotices.DescribeErrors(result.Errors, "The prompt item could not be updated."));
                return;
            }

            // The write is committed; a failed reload is reported as a refresh problem, never as a favorite failure.
            await LoadAsync(requestedPageIndex, failureSummary: "Favorite saved, but the gallery could not be refreshed");
        }
        catch (OperationCanceledException) when (lifetimeToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (!disposed)
            {
                await RaiseAsync(PromptGalleryNoticeSeverity.Error, "Favorite update failed", exception.Message);
            }
        }
        finally
        {
            if (!disposed)
            {
                favoriteBusyItemId = null;
                await PublishAsync();
            }
        }
    }

    private PromptGalleryQuery BuildQuery(int pageIndex)
    {
        var usesContext = BuildPresentation().UsesContextProviderModel;
        return new PromptGalleryQuery(
            Text: filters.Text,
            Tags: filters.Tags,
            Kind: filters.Kind,
            Status: filters.Status,
            IncludeArchived: filters.IncludeArchived,
            Provider: usesContext
                ? string.IsNullOrWhiteSpace(context.Provider) ? filters.ProviderFilter : context.Provider
                : context.Compact ? null : filters.ProviderFilter,
            Model: usesContext
                ? string.IsNullOrWhiteSpace(context.Model) ? filters.ModelFilter : context.Model
                : context.Compact ? null : filters.ModelFilter,
            PageIndex: pageIndex,
            PageSize: Math.Clamp(context.PageSize, 1, PromptGalleryQuery.MaximumPageSize),
            Consumer: context.Consumer,
            FavoritesOnly: filters.FavoritesOnly);
    }

    private (CancellationTokenSource Operation, long Generation) ReplaceOperation()
    {
        operation?.Cancel();
        operation = new CancellationTokenSource();
        return (operation, ++operationGeneration);
    }

    private bool IsCurrent(long generation) => !disposed && generation == operationGeneration;

    private PromptGallerySearchPresentation BuildPresentation()
        => new(
            filters,
            page.Items,
            page.PageIndex,
            page.TotalPages,
            page.TotalCount,
            isLoading,
            loadError,
            availableTags,
            context.Provider,
            context.Model,
            context.ShowActualChatModelFilter,
            favoriteBusyItemId);

    // A filter the list does not offer is reported at its default, so it never reaches the owner's location.
    private Task CommitAsync(PromptGallerySearchCommitKind kind)
    {
        var reported = BuildPresentation().OffersActualChatModelFilter ? filters : filters with { ActualChatModelOnly = true };
        return Committed?.Invoke(new PromptGallerySearchCommit(reported, requestedPageIndex, kind)) ?? Task.CompletedTask;
    }

    private Task PublishAsync()
    {
        Presentation = BuildPresentation();
        return Changed?.Invoke() ?? Task.CompletedTask;
    }

    private Task RaiseAsync(PromptGalleryNoticeSeverity severity, string summary, string detail)
        => NoticeRaised?.Invoke(new PromptGalleryNotice(severity, summary, detail)) ?? Task.CompletedTask;
}
