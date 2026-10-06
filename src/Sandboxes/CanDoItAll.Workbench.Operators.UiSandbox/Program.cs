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
    app.MapGet(OperatorsWatchState.Endpoint, () => OperatorsWatchState.Read(app.Configuration));
}
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
