using CanDoItAll.Resources.UiSandbox.Components;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.FileTools.FileInteraction.Components;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCanDoItAllBaseLib();
builder.Services.AddSingleton(new FileInteractionComponentBuilder().AddBuiltIns().Build());
var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
