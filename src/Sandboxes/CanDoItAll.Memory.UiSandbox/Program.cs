using CanDoItAll.Memory.UiSandbox.Components;
using CanDoItAll.Components.BaseLib;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCanDoItAllBaseLib();
var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapGet("/provider-fixture", () => Results.Content("<!doctype html><html lang='en'><title>Owned provider fixture</title><body>Owned local provider console</body></html>", "text/html"));
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
