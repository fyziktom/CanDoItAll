using CanDoItAll.AgentFramework.UI.Diagnostics;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class AgentDiagnosticsPanel : IDisposable {
    [Inject] public IAgentDiagnosticsReads Reads { get; set; } = default!;
    [Inject] public ILogger<AgentDiagnosticsSession> Logger { get; set; } = default!;
    [Inject] public NotificationService NotificationService { get; set; } = default!;
    private AgentDiagnosticsSession session = default!;
    private long intentRevision;
    private bool disposed;

    protected override async Task OnInitializedAsync() {
        session = new(Reads, Logger);
        session.Changed += Render;
        await DispatchAsync(new DiagnosticsIntent.Refresh());
    }

    private void Render() => _ = InvokeAsync(() => {
        if (!disposed) {
            StateHasChanged();
        }
    });

    private async Task DispatchAsync(DiagnosticsIntent intent) {
        var revision = ++intentRevision;
        await (intent switch {
            DiagnosticsIntent.Refresh => session.RefreshAsync(),
            DiagnosticsIntent.Retry retry => session.RetryAsync(retry.Lane),
            _ => Task.CompletedTask
        });
        if (disposed || revision != intentRevision) {
            return;
        }
        if (Enum.GetValues<DiagnosticsLane>().Any(lane => session.Presentation.ReadState(lane).Error is not null)) {
            NotificationService.Error("Attention", "Some diagnostics could not be refreshed. Review the affected section and retry.");
        } else {
            NotificationService.Success("Ready", "Diagnostics refreshed from the integrated runtime.");
        }
    }

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        session?.Dispose();
    }
}
