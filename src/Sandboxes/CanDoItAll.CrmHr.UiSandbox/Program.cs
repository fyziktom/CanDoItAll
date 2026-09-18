using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Charts;
using CanDoItAll.CrmHr.UiSandbox;
using CanDoItAll.CrmHr.UiSandbox.Components;

var builder = WebApplication.CreateBuilder(args);
CrmHrSandboxAssets.ValidateRequestedMode(builder.Configuration[nameof(CrmHrAssetMode)]);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCanDoItAllBaseLib();
// The Financials specimen plots the real chart; the same registration the Web host uses.
builder.Services.AddCanDoItAllCharts();
// The workspace surfaces' catalog browsers and pickers inject these read ports; Gantt and the AgentFramework
// agent cards are plain presentational libraries with no DI registration of their own.
builder.Services.AddCrmHrSandboxReadPorts();

var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
