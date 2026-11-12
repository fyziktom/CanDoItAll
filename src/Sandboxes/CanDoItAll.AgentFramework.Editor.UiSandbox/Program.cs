using CanDoItAll.AgentFramework.Editor.UiSandbox.Components;
using CanDoItAll.Components.BaseLib;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCanDoItAllBaseLib();
builder.Services.AddScoped<CanDoItAll.Modules.Workspace.StorageSelection.Contracts.IStorageCatalogSelectionSource, CanDoItAll.AgentFramework.Editor.UiSandbox.EditorStorageSource>();
var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
