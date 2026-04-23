using CanDoItAll.Components.BaseLib;
using CanDoItAll.Space3D.Mouse.Sandbox.Components;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCanDoItAllBaseLib();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapGet("/_dev/runtime", () => Results.Json(new
{
    isReady = true,
    application = "space3d-mouse-sandbox"
}));
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program
{
}
