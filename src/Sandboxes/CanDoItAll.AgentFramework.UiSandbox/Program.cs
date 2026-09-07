using CanDoItAll.AgentFramework.UiSandbox;
using CanDoItAll.AgentFramework.UiSandbox.Components;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Charts;

var builder = WebApplication.CreateBuilder(args);
CatalogAssets.ValidateRequestedMode(builder.Configuration[nameof(CatalogAssetMode)]);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCanDoItAllBaseLib();
builder.Services.AddCanDoItAllCharts();
builder.Services.AddSingleton(CatalogFixture.Load());

var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
if (app.Environment.IsDevelopment()) {
    app.MapGet(CatalogWatchState.Endpoint, () => CatalogWatchState.Read(app.Configuration));
}
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
