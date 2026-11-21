using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workspace.Pages.Components;
using CanDoItAll.RemoteInstances;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class RemoteTemplateImportDialog : IDisposable {
    [Inject] private IEnumerable<IRemoteTemplateImportFeature> Features { get; set; } = default!;
    [Inject] private RemoteTemplateProviderCatalog Providers { get; set; } = default!;
    [Inject] private RemoteTemplateImportCoordinator Coordinator { get; set; } = default!;
    [Inject] private IAgentFrameworkWorkspaceService Workspace { get; set; } = default!;
    [Inject] private ILogger<RemoteTemplateImportDialog> Logger { get; set; } = default!;
    [Parameter, EditorRequired] public RemoteTemplateKind Kind { get; set; }
    [Parameter, EditorRequired] public Func<bool, CancellationToken, ValueTask> Authorize { get; set; } = default!;
    [Parameter, EditorRequired] public Func<Func<CancellationToken, Task<Guid>>, CancellationToken, Task<Guid>> Write { get; set; } = default!;
    [Parameter] public EventCallback Closed { get; set; }
    [Parameter] public EventCallback AcceptedAndClosed { get; set; }
    [Parameter] public EventCallback<Guid> Imported { get; set; }

    private readonly CancellationTokenSource lifetime = new();
    private readonly Dictionary<Guid, RemoteTemplateSelection> selected = [];
    private readonly Dictionary<ImportOrigin, Guid> accepted = [];
    private CancellationTokenSource? catalogLifetime;
    private IReadOnlyList<RemoteTemplateRow> rows = [];
    private IReadOnlyList<RemoteTemplateModelOption> options = [];
    private ModelReview? review;
    private RemoteInstanceSelection? source;
    private AgentExecutionActivityWorkspaceIdentity? workspace;
    private string? error;
    private string? reviewError;
    private bool loading;
    private bool writing;
    private bool refreshingOptions;
    private bool disposed;

    private IRemoteTemplateImportFeature Feature => Features.Single(feature => feature.Kind == Kind);
    private string Title => Kind switch {
        RemoteTemplateKind.Agent => "Import agent templates",
        RemoteTemplateKind.SimpleChat => "Import Simple Chats",
        RemoteTemplateKind.Workflow => "Import workflows",
        _ => throw new ArgumentOutOfRangeException(nameof(Kind))
    };
    private IReadOnlyList<RemoteTemplateSelection> Pending => selected.Values.Where(item =>
        item.Preview is not null && item.AcceptedId is null).ToArray();
    private bool CanImport => !disposed && !writing && !loading && review is null && Pending.Count > 0 &&
        selected.Values.All(item => item.Preview is not null || item.Error is not null);

    private async Task ChangeSourceAsync(RemoteInstanceSelection? instance) {
        if (disposed || writing) {
            return;
        }
        catalogLifetime?.Cancel();
        catalogLifetime?.Dispose();
        catalogLifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        var token = catalogLifetime.Token;
        source = instance;
        workspace = null;
        rows = [];
        options = [];
        selected.Clear();
        review = null;
        error = null;
        refreshingOptions = false;
        loading = instance is not null;
        if (instance is null) {
            return;
        }
        try {
            await Authorize(false, token);
            var opening = await Workspace.ReadAgentDecisionDataPolicyContextAsync(token);
            var catalog = await Feature.ListAsync(instance, token);
            if (catalog.Any(row => row.Id == Guid.Empty || row.PublisherId == Guid.Empty || row.PublisherId != catalog[0].PublisherId) ||
                catalog.DistinctBy(row => row.Id).Count() != catalog.Count) {
                throw new RemoteInstanceReadException("The publisher returned an invalid template catalog.");
            }
            var available = catalog.Count == 0 ? [] : await Providers.ListAsync(instance.Id, catalog[0].PublisherId, token, Kind);
            await Authorize(false, token);
            token.ThrowIfCancellationRequested();
            workspace = opening;
            rows = catalog.Select(row => row with { AvatarImageUrl = RemoteTemplateAvatar.ForDisplay(row.AvatarImageUrl) }).ToArray();
            options = available;
        } catch (Exception) when (token.IsCancellationRequested) {
        } catch (Exception exception) {
            error = Failure(exception, "Could not read the remote catalog.");
        } finally {
            if (!token.IsCancellationRequested) {
                loading = false;
            }
        }
    }

    private async Task SelectAsync(RemoteTemplateRow row, bool choose) {
        if (disposed || writing || review is not null || source is not { } instance || catalogLifetime is null ||
            !rows.Any(current => ReferenceEquals(current, row)) || Accepted(row).HasValue) {
            return;
        }
        if (!choose) {
            selected.Remove(row.Id);
            return;
        }
        var item = new RemoteTemplateSelection(row);
        var token = catalogLifetime.Token;
        selected[row.Id] = item;
        try {
            await Authorize(false, token);
            var preview = await Feature.PreviewAsync(instance, row, token);
            await Authorize(false, token);
            if (!token.IsCancellationRequested && ReferenceEquals(selected.GetValueOrDefault(row.Id), item)) {
                item.Load(preview, options);
            }
        } catch (Exception) when (token.IsCancellationRequested) {
        } catch (Exception exception) {
            item.Error = Failure(exception, "Could not read the selected definition.");
        }
    }

    private string DescribeSlot(RemoteTemplateModelSlot slot) {
        var option = options.FirstOrDefault(candidate => candidate.Choice == slot.Choice);
        return option is null ? $"{slot.Requirement.Label} · Model required" :
            $"{slot.Requirement.Label} · {option.ProviderName} · {option.DisplayName}";
    }

    private Guid? Accepted(RemoteTemplateRow row) => source is not null && workspace is not null &&
        accepted.TryGetValue(new(workspace, source.Id, row.PublisherId, row.Id, row.VersionId), out var id) ? id : null;

    private void OpenReview(RemoteTemplateSelection item, RemoteTemplateModelSlot slot, bool continueImport = false) {
        if (writing || disposed || item.AcceptedId.HasValue || !ReferenceEquals(selected.GetValueOrDefault(item.Row.Id), item)) {
            return;
        }
        review = new(item, slot, continueImport);
        reviewError = null;
    }

    private void CancelReview(ModelReview current) {
        if (ReferenceEquals(current, review)) {
            review = null;
            reviewError = null;
            refreshingOptions = false;
        }
    }

    private async Task RefreshOptionsAsync(ModelReview current) {
        if (!ReferenceEquals(current, review) || refreshingOptions || source is not { } instance || catalogLifetime is null) {
            return;
        }
        refreshingOptions = true;
        var token = catalogLifetime.Token;
        try {
            await Authorize(false, token);
            var available = await Providers.ListAsync(instance.Id, current.Item.Row.PublisherId, token, Kind);
            await Authorize(false, token);
            if (token.IsCancellationRequested || !ReferenceEquals(current, review)) {
                return;
            }
            options = available;
            foreach (var slot in Pending.SelectMany(item => item.Slots)) {
                slot.Reconcile(options);
            }
            reviewError = null;
        } catch (Exception) when (token.IsCancellationRequested || !ReferenceEquals(current, review)) {
        } catch (Exception exception) {
            reviewError = Failure(exception, "Could not refresh providers.");
        } finally {
            if (ReferenceEquals(current, review)) {
                refreshingOptions = false;
            }
        }
    }

    private async Task ConfirmAsync(ModelReview current, RemoteTemplateModelOption option, bool applyAll) {
        if (!ReferenceEquals(current, review) || refreshingOptions || !options.Contains(option) || !option.Supports(current.Slot.Requirement)) {
            return;
        }
        current.Slot.Confirm(option);
        if (applyAll) {
            foreach (var slot in Pending.SelectMany(item => item.Slots).Where(slot => option.Supports(slot.Requirement))) {
                slot.Confirm(option);
            }
        }
        CancelReview(current);
        if (current.ContinueImport) {
            await ImportAsync();
        }
    }

    private async Task ImportAsync() {
        if (!CanImport || source is not { } instance || workspace is not { } opening || catalogLifetime is null) {
            return;
        }
        var batch = Pending;
        foreach (var item in batch) {
            var slot = item.Slots.FirstOrDefault(slot => slot.Choice is null || batch.Count > 1 && !slot.Confirmed);
            if (slot is not null) {
                OpenReview(item, slot, true);
                return;
            }
        }
        writing = true;
        var token = catalogLifetime.Token;
        try {
            foreach (var item in batch) {
                if (token.IsCancellationRequested || !await ImportItemAsync(item, instance, opening, token)) {
                    break;
                }
                if (!disposed) {
                    StateHasChanged();
                }
            }
        } finally {
            writing = false;
        }
    }

    private async Task<bool> ImportItemAsync(RemoteTemplateSelection item, RemoteInstanceSelection instance,
        AgentExecutionActivityWorkspaceIdentity opening, CancellationToken token) {
        var key = new ImportOrigin(opening, instance.Id, item.Row.PublisherId, item.Row.Id, item.Row.VersionId);
        item.Error = null;
        try {
            var choices = item.Slots.Select(slot => slot.Choice!).ToArray();
            var local = await Write(t => Coordinator.ImportAsync(Feature, instance, item.Preview!, choices, opening, t), token);
            accepted[key] = local;
            item.AcceptedId = local;
        } catch (RemoteTemplateImportCommittedException committed) {
            accepted[key] = committed.AcceptedId;
            item.AcceptedId = committed.AcceptedId;
            item.Error = committed.Message;
        } catch (Exception exception) when (exception is RemoteInstanceReadException or RemoteAgentTemplateImportException or KeyNotFoundException) {
            item.Error = Failure(exception, "The definition could not be imported.");
        } catch (UnauthorizedAccessException exception) {
            item.Error = Failure(exception, "Workspace access changed.");
            return false;
        } catch (Exception exception) {
            item.Error = Failure(exception, "The import could not be confirmed. Retry to recover the local copy or finish the import.");
        }
        return true;
    }

    private string Failure(Exception exception, string message) {
        Logger.LogWarning("Remote {TemplateKind} import for source {SourceId} encountered {FailureType}", Kind, source?.Id, exception.GetType().Name);
        return exception switch {
            RemoteInstanceReadException or RemoteAgentTemplateImportException => exception.Message,
            UnauthorizedAccessException => "Workspace access changed. Reopen the catalog.",
            KeyNotFoundException => "The selected source or definition is no longer available.",
            _ => message
        };
    }

    private Task CloseAsync() => disposed || writing || review is not null ? Task.CompletedTask :
        accepted.Count > 0 && AcceptedAndClosed.HasDelegate ? AcceptedAndClosed.InvokeAsync() : Closed.InvokeAsync();

    public void Dispose() {
        disposed = true;
        lifetime.Cancel();
        catalogLifetime?.Dispose();
        lifetime.Dispose();
    }

    private sealed record ModelReview(RemoteTemplateSelection Item, RemoteTemplateModelSlot Slot, bool ContinueImport);
    private readonly record struct ImportOrigin(AgentExecutionActivityWorkspaceIdentity Workspace, Guid Source, Guid Publisher, Guid Template, Guid? Version);
}
