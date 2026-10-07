using CanDoItAll.Components.BaseLib;
using CanDoItAll.Workbench.Planning.UiSandbox;
using CanDoItAll.Workbench.Planning.UiSandbox.Components;

var builder = WebApplication.CreateBuilder(args);
PlanningAssets.ValidateRequestedMode(builder.Configuration[nameof(PlanningAssetMode)]);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCanDoItAllBaseLib();
var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
if (app.Environment.IsDevelopment()) {
    app.MapGet(PlanningWatchState.Endpoint, () => PlanningWatchState.Read(app.Configuration));
}
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
