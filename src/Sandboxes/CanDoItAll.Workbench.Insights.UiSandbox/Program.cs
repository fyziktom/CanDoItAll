using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Charts;
using CanDoItAll.Workbench.Insights.UiSandbox;
using CanDoItAll.Workbench.Insights.UiSandbox.Components;

var builder = WebApplication.CreateBuilder(args);
InsightsAssets.ValidateRequestedMode(builder.Configuration[nameof(InsightsAssetMode)]);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCanDoItAllBaseLib();
builder.Services.AddCanDoItAllCharts();
var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
if (app.Environment.IsDevelopment()) {
    app.MapGet(InsightsWatchState.Endpoint, () => InsightsWatchState.Read(app.Configuration));
}
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
