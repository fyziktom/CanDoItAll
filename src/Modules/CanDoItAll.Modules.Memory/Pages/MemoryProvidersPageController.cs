using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Modules.Memory.Services;

namespace CanDoItAll.Modules.Memory.Pages;

public sealed class MemoryProvidersPageController(IMemoryProviderManagementUiService providerUiService)
{
    public MemoryProviderManagementSnapshot? Snapshot { get; private set; }

    public MemoryProviderProfileEditorModel Editor { get; private set; } = new();

    public MemoryQueryEditorModel QueryEditor { get; } = new();

    public MemoryFeedbackEditorModel FeedbackEditor { get; } = new();

    public MemoryManualIngestionEditorModel IngestionEditor { get; } = new();

    public int ActiveTabIndex { get; set; }

    public bool IsLoading { get; private set; } = true;

    public bool IsBusy { get; private set; }

    public bool IsQueryBusy { get; private set; }

    public bool IsFeedbackBusy { get; private set; }

    public bool IsOperationBusy { get; private set; }

    public bool IsEventBusy { get; private set; }

    public bool IsIngestionBusy { get; private set; }

    public string? ErrorMessage { get; private set; }

    public string? SelectedProviderId { get; private set; }

    public MemoryProviderQueryUiResult? QueryResult { get; private set; }

    public MemoryProviderOperationUiResult? OperationActionResult { get; private set; }

    public MemoryProviderFeedbackUiResult? FeedbackActionResult { get; private set; }

    public MemoryProviderManualIngestionUiResult? IngestionResult { get; private set; }

    public MemoryProviderEventAcknowledgeUiResult? EventActionResult { get; private set; }

    public MemoryProviderManagementProfile? SelectedProvider => Snapshot?.SelectedProvider;

    public string TabsRenderKey => Snapshot is null
        ? "loading"
        : $"{Snapshot.ProviderCount}:{Snapshot.EnabledProviderCount}:{Snapshot.HealthyProviderCount}:{Snapshot.ProviderUiSurfaces.Count}:{Snapshot.UiSurfaceCount}";

    public async Task RefreshAsync()
    {
        IsLoading = Snapshot is null;
        await ExecuteAsync(value => IsBusy = value, RefreshCoreAsync);
        IsLoading = false;
    }

    public async Task SelectProviderAsync(string providerId)
    {
        SelectedProviderId = providerId;
        QueryResult = null;
        OperationActionResult = null;
        FeedbackActionResult = null;
        EventActionResult = null;
        IngestionResult = null;
        await RefreshAsync();
    }

    public Task SaveProviderAsync() => ExecuteAsync(value => IsBusy = value, async () =>
    {
        var saved = await providerUiService.SaveProviderAsync(Editor);
        SelectedProviderId = saved.InstanceId.Value;
        await RefreshCoreAsync();
    });

    public Task AddDemoProvidersAsync() => ExecuteAsync(value => IsBusy = value, async () =>
    {
        await providerUiService.CreateDemoProvidersAsync();
        await RefreshCoreAsync();
    });

    public Task RunQueryAsync() => ExecuteAsync(value => IsQueryBusy = value, async () =>
    {
        QueryResult = await providerUiService.RunQueryAsync(SelectedProviderId, QueryEditor);
        if (QueryResult.ContextPack is not null)
        {
            FeedbackEditor.ContextPackId = QueryResult.ContextPack.ContextPackId.Value.ToString("D");
        }

        await RefreshCoreAsync();
        ActiveTabIndex = 4;
    });

    public Task SubmitFeedbackAsync() => ExecuteAsync(value => IsFeedbackBusy = value, async () =>
    {
        FeedbackActionResult = await providerUiService.SubmitFeedbackAsync(SelectedProviderId, FeedbackEditor);
        await RefreshCoreAsync();
        ActiveTabIndex = 3;
    });

    public Task EnqueueManualIngestionAsync() => ExecuteAsync(value => IsIngestionBusy = value, async () =>
    {
        IngestionResult = await providerUiService.EnqueueManualIngestionAsync(SelectedProviderId, IngestionEditor);
        await RefreshCoreAsync();
        ActiveTabIndex = 5;
    });

    public Task RefreshOperationAsync(MemoryOperationId operationId) =>
        ExecuteOperationAsync(() => providerUiService.RefreshOperationAsync(operationId.Value.ToString("D")));

    public Task CancelOperationAsync(MemoryOperationId operationId) =>
        ExecuteOperationAsync(() => providerUiService.CancelOperationAsync(operationId.Value.ToString("D")));

    public Task AcknowledgeEventAsync(MemoryProviderEventId eventId) =>
        ExecuteAsync(value => IsEventBusy = value, async () =>
        {
            EventActionResult = await providerUiService.AcknowledgeEventAsync(
                SelectedProviderId,
                eventId.Value.ToString("D"),
                accepted: true);
            await RefreshCoreAsync();
            ActiveTabIndex = 2;
        });

    private Task ExecuteOperationAsync(Func<Task<MemoryProviderOperationUiResult>> action) =>
        ExecuteAsync(value => IsOperationBusy = value, async () =>
        {
            OperationActionResult = await action();
            await RefreshCoreAsync();
            ActiveTabIndex = 1;
        });

    private async Task RefreshCoreAsync()
    {
        Snapshot = await providerUiService.GetSnapshotAsync(SelectedProviderId);
        SelectedProviderId = Snapshot.SelectedProvider?.InstanceId.Value;
        Editor = MemoryProviderProfileEditorModel.FromProfile(Snapshot.SelectedProvider);
    }

    private async Task ExecuteAsync(Action<bool> setBusy, Func<Task> action)
    {
        setBusy(true);
        ErrorMessage = null;
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            setBusy(false);
        }
    }
}
