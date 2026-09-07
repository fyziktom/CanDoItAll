using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;

using var dialogs = new DialogService(new Navigation());
using var lease = dialogs.PreserveDialogsOnSamePageNavigation();
using var cancellation = new CancellationTokenSource();
cancellation.Cancel();
var result = dialogs.OpenAsync("Canceled", _ => builder => builder.AddContent(0, "Content"), cancellationToken: cancellation.Token);
if (!result.IsCanceled || dialogs.Dialogs.Count != 0) {
    throw new InvalidOperationException("Packed cancellation contract failed.");
}
Console.WriteLine("Packed external consumer passed.");

sealed class Navigation : NavigationManager {
    public Navigation() {
        Initialize("https://example.invalid/", "https://example.invalid/agents");
    }
    protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotSupportedException();
}
