using CanDoItAll.Components.BaseLib;
using CanDoItAll.Workbench.Operators.UiSandbox;
using CanDoItAll.Workbench.Operators.UiSandbox.Components;

var builder = WebApplication.CreateBuilder(args);
OperatorsAssets.ValidateRequestedMode(builder.Configuration[nameof(OperatorsAssetMode)]);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCanDoItAllBaseLib();
var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
if (app.Environment.IsDevelopment()) {
    app.MapGet("/_dev/preview", () => Results.Content("<!doctype html><html><head><title>Owned renderer fixture</title></head><body><h1>Owned renderer fixture</h1><p>Static preview bytes from the independent Operators sandbox.</p></body></html>", "text/html"));
    app.MapGet(OperatorsWatchState.Endpoint, () => OperatorsWatchState.Read(app.Configuration));
}
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
