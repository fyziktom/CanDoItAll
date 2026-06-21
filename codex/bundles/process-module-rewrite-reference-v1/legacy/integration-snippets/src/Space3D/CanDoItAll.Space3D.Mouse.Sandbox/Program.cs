using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Space3D.Mouse.Sandbox;
using CanDoItAll.Space3D.Mouse.Sandbox.Components;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCanDoItAllBaseLib();
builder.Services.AddOptions<ProcessTemplatePackOptions>()
    .BindConfiguration(ProcessTemplatePackOptions.SectionName);
builder.Services.AddScoped(provider =>
{
    var options = provider.GetRequiredService<IOptions<ProcessTemplatePackOptions>>().Value;
    return new ProcessTemplatePackLoader(options.PackRoot);
});
builder.Services.AddScoped<ProcessTemplateCatalogService>();
builder.Services.AddScoped<ProcessTemplateProjectionService>();
builder.Services.AddScoped<ProcessCanvasChromeCatalogService>();
builder.Services.AddScoped<ProcessCanvasSurfaceFactory>();
builder.Services.AddScoped<ProcessWebGlSceneAdapter>();
builder.Services.AddScoped<Space3DProcessWorkbenchSession>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapGet("/_dev/runtime", () => Results.Json(new
{
    isReady = true,
    application = "space3d-mouse-sandbox"
}));
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program
{
}
