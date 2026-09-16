using CanDoItAll.Components.BaseLib;
using CanDoItAll.CrmHr.UiSandbox;
using CanDoItAll.CrmHr.UiSandbox.Components;

var builder = WebApplication.CreateBuilder(args);
CrmHrSandboxAssets.ValidateRequestedMode(builder.Configuration[nameof(CrmHrAssetMode)]);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCanDoItAllBaseLib();

var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
