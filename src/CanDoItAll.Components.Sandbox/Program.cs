using CanDoItAll.Components.Sandbox.Components;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Charts;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCanDoItAllBaseLib();
builder.Services.AddCanDoItAllCharts();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
