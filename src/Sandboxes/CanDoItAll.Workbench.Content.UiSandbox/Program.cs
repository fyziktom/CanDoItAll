using CanDoItAll.Components.BaseLib;
using CanDoItAll.Workbench.Content.UiSandbox;
using CanDoItAll.Workbench.Content.UiSandbox.Components;
using CanDoItAll.Workbench.Content.UI;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.FileTools.FileInteraction.Markdown;
using CanDoItAll.FileTools.FileInteraction.Spreadsheet;

var builder = WebApplication.CreateBuilder(args);
ContentAssets.ValidateRequestedMode(builder.Configuration[nameof(ContentAssetMode)]);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCanDoItAllBaseLib();
builder.Services.AddFileInteractionComponents(builder => builder.AddBuiltIns().AddMarkdown().AddContentMermaid().AddSpreadsheet());
builder.Services.AddSingleton<IMarkdownFencedCodeComponentRegistration, ContentMarkdownMermaidRegistration>();
var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
if (app.Environment.IsDevelopment()) {
    app.MapGet(ContentWatchState.Endpoint, () => ContentWatchState.Read(app.Configuration));
}
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
