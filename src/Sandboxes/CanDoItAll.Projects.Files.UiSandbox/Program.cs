using CanDoItAll.Components.BaseLib;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.Projects.Files.UiSandbox;
using CanDoItAll.Projects.Files.UiSandbox.Components;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCanDoItAllBaseLib();
builder.Services.AddSingleton(FilesFixtureComposition.Create());
var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
