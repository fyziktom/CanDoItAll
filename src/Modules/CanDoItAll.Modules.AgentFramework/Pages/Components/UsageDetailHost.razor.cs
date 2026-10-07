using System.Collections.Immutable;
using CanDoItAll.AgentFramework.UI.Overview;
using CanDoItAll.AgentFramework.UI.Usage;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class UsageDetailHost : IDisposable {
    [Parameter, EditorRequired] public ProviderUsageQuery Query { get; set; } = default!;
    [Parameter] public AgentsOverviewDetail Detail { get; set; }
    [Parameter] public CancellationToken OwnerLifetime { get; set; }
    [CascadingParameter] public DialogReference? DialogReference { get; set; }
    [CascadingParameter] public Task<AuthenticationState>? AuthenticationState { get; set; }
    [Inject] public IAgentsWorkspaceQuery WorkspaceQuery { get; set; } = default!;
    [Inject] public IDatabaseSwitchNotificationService ProfileChanges { get; set; } = default!;
    [Inject] public ILogger<UsageDetailHost> Logger { get; set; } = default!;

    private UsageDetailState? state;
    private CancellationTokenSource? readCancellation;
    private CancellationTokenRegistration parentRegistration;
    private CancellationToken parentLifetime;
    private Task<AuthenticationState>? openingAuthentication;
    private bool initialized;
    private bool disposed;

    protected override void OnInitialized() => ProfileChanges.Changed += ProfileChanged;

    protected override Task OnParametersSetAsync() {
        ArgumentNullException.ThrowIfNull(Query);
        if (!Enum.IsDefined(Detail)) {
            throw new ArgumentOutOfRangeException(nameof(Detail));
        }
        if (!initialized) {
            initialized = true;
            openingAuthentication = AuthenticationState;
            parentLifetime = OwnerLifetime;
            parentRegistration = parentLifetime.Register(Retire);
        } else if (!ReferenceEquals(openingAuthentication, AuthenticationState) || parentLifetime != OwnerLifetime) {
            Retire();
        }
        return disposed || state?.Retired == true || parentLifetime.IsCancellationRequested || state?.Query == Query
            ? Task.CompletedTask : ReadAsync();
    }

    private async Task ReadAsync() {
        CancelRead();
        var request = new UsageDetailState(Guid.NewGuid(), Query);
        state = request;
        var owner = new CancellationTokenSource();
        readCancellation = owner;
        var token = owner.Token;
        try {
            var result = await WorkspaceQuery.ReadUsageAsync(request.Query, token);
            if (!IsCurrent()) {
                return;
            }
            if (result.Query != request.Query) {
                throw new InvalidDataException("Usage evidence returned a different requested scope.");
            }
            state = request with {
                Loading = false,
                Snapshot = result with {
                    Consumers = result.Consumers.ToImmutableArray(),
                    Providers = result.Providers.ToImmutableArray(),
                    Models = result.Models.ToImmutableArray(),
                    Sources = result.Sources.ToImmutableArray()
                }
            };
        } catch (OperationCanceledException) when (token.IsCancellationRequested) {
        } catch (Exception exception) {
            if (IsCurrent()) {
                state = request with { Loading = false, Error = "Usage evidence could not be refreshed. Retry the selected workload and period." };
                Logger.LogWarning("Usage detail read failed ({FailureType}); scope {Scope}.", exception.GetType().Name, request.Query);
            }
        } finally {
            if (ReferenceEquals(readCancellation, owner)) {
                readCancellation = null;
            }
            owner.Dispose();
        }

        bool IsCurrent() => !disposed && state?.Origin == request.Origin && state.Retired == false
            && Query == request.Query && !token.IsCancellationRequested && !parentLifetime.IsCancellationRequested;
    }

    private async Task HandleIntentAsync(UsageDetailIntent intent) {
        if (disposed || state?.Origin != intent.Origin || state.Query != intent.Query) {
            return;
        }
        switch (intent.Action) {
            case UsageDetailAction.Close:
                Retire();
                if (DialogReference is not null) {
                    await DialogReference.CloseAsync();
                }
                break;
            case UsageDetailAction.Retry when !state.Retired && !state.Loading && state.Error is not null:
                await ReadAsync();
                break;
            case UsageDetailAction.Retry:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(intent));
        }
    }

    private void ProfileChanged(object? sender, DatabaseProfileChangedNotification notification) => Retire();

    private void Retire() {
        CancelRead();
        state = (state ?? new(Guid.NewGuid(), Query)) with { Retired = true, Loading = false, Snapshot = null, Error = null };
        if (!disposed) {
            _ = InvokeAsync(StateHasChanged);
        }
    }

    private void CancelRead() {
        var owner = readCancellation;
        readCancellation = null;
        owner?.Cancel();
    }

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        ProfileChanges.Changed -= ProfileChanged;
        parentRegistration.Dispose();
        Retire();
    }
}
