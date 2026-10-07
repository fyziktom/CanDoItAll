using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Workspace.StorageSelection.Contracts;
using CanDoItAll.Workspace.StorageSelection.UiSandbox;
using CanDoItAll.Workspace.StorageSelection.UiSandbox.Components;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCanDoItAllBaseLib();
builder.Services.AddScoped<SelectionScenarioSource>();
builder.Services.AddScoped<IStorageCatalogSelectionSource>(services => services.GetRequiredService<SelectionScenarioSource>());
var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
