using CanDoItAll.Components.BaseLib;
using CanDoItAll.Prompts.UiSandbox;
using CanDoItAll.Prompts.UiSandbox.Components;

var builder = WebApplication.CreateBuilder(args);
PromptsSandboxAssets.ValidateRequestedMode(builder.Configuration[nameof(PromptsAssetMode)]);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCanDoItAllBaseLib();

var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
