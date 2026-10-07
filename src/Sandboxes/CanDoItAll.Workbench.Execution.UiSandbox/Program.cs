using CanDoItAll.Components.BaseLib;
using CanDoItAll.Workbench.Execution.UiSandbox;
using CanDoItAll.Workbench.Execution.UiSandbox.Components;

var builder = WebApplication.CreateBuilder(args);
ExecutionAssets.ValidateRequestedMode(builder.Configuration[nameof(ExecutionAssetMode)]);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCanDoItAllBaseLib();
var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
if (app.Environment.IsDevelopment()) {
    app.MapGet(ExecutionWatchState.Endpoint, () => ExecutionWatchState.Read(app.Configuration));
}
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
