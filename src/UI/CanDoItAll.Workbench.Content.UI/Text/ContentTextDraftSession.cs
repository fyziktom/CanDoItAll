using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Workbench.Content.UI;

internal sealed class ContentTextDraftSession(Guid openingId, ContentTextDefinition definition,
    ContentTextInitialDraft initial, CancellationToken cancellationToken) {
    private bool disposed;
    public Guid OpeningId { get; } = openingId;
    public ContentTextDefinition Definition { get; } = definition;
    public CancellationTokenSource Lifetime { get; } = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    public ContentTextSource Source { get; set; }
    public string Title { get; set; } = initial.Title;
    public string Subtitle { get; set; } = initial.Subtitle;
    public string Notes { get; set; } = initial.Notes;
    public string FileName { get; set; } = definition.FileNamePlaceholder;
    public string Content { get; set; } = string.Empty;
    public IBrowserFile? Upload { get; set; }
    public string Error { get; set; } = string.Empty;
    public ContentTextOutcome? Outcome { get; set; }
    public bool IsBusy { get; set; }
    public bool IsDispatching { get; set; }
    public bool Retired { get; private set; }
    public bool Locked => IsBusy || Outcome?.RequiresObservation == true || Retired;

    public async Task RetireAsync() {
        if (Retired) {
            return;
        }
        Retired = true;
        await Lifetime.CancelAsync();
        ReleaseIfRetired();
    }

    public void ReleaseIfRetired() {
        if (Retired && !IsBusy && !disposed) {
            disposed = true;
            Lifetime.Dispose();
        }
    }
}
