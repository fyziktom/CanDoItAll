using CanDoItAll.Components.BaseLib;
using CanDoItAll.Workbench.Structure.UiSandbox;
using CanDoItAll.Workbench.Structure.UiSandbox.Components;

var builder = WebApplication.CreateBuilder(args);
StructureAssets.ValidateRequestedMode(builder.Configuration[nameof(StructureAssetMode)]);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCanDoItAllBaseLib();
var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
if (app.Environment.IsDevelopment()) {
    app.MapGet(StructureWatchState.Endpoint, () => StructureWatchState.Read(app.Configuration));
}
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
