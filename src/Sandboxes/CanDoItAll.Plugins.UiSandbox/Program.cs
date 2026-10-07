using CanDoItAll.Plugins.UiSandbox.Components;
using CanDoItAll.Components.BaseLib;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCanDoItAllBaseLib();
var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapGet("/api/plugins/packages/sandbox.package/icon", () => Results.Redirect("/icons/plugin.svg"));
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
