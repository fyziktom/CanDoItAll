using CanDoItAll.Processes.UiSandbox;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.FileTools.FileInteraction.Markdown;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Processes.UiSandbox.Components;

var builder = WebApplication.CreateBuilder(args);
ProcessesAssets.ValidateRequestedMode(builder.Configuration[nameof(ProcessesAssetMode)]);
builder.Services.AddSingleton(new FileInteractionComponentBuilder().AddBuiltIns().AddMarkdown().Build());
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCanDoItAllBaseLib();
var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
if (app.Environment.IsDevelopment()) {
    app.MapGet(ProcessesWatchState.Endpoint, () => ProcessesWatchState.Read(app.Configuration));
}
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
