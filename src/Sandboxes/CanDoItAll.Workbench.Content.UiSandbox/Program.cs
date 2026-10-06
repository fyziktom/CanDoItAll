using CanDoItAll.Components.BaseLib;
using CanDoItAll.Workbench.Content.UiSandbox;
using CanDoItAll.Workbench.Content.UiSandbox.Components;

var builder = WebApplication.CreateBuilder(args);
ContentAssets.ValidateRequestedMode(builder.Configuration[nameof(ContentAssetMode)]);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCanDoItAllBaseLib();
var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
if (app.Environment.IsDevelopment()) {
    app.MapGet(ContentWatchState.Endpoint, () => ContentWatchState.Read(app.Configuration));
}
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
