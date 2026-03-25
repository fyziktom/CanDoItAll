using CanDoItAll.ComponentKit.Components;
using CanDoItAll.Components;
using CanDoItAll.Infrastructure.DependencyInjection;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Readiness;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Activity;
using CanDoItAll.Modules.Automation;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.Validation;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web.Components;
using CanDoItAll.Web.Composition;
using CanDoItAll.Web.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
var detailedErrorsEnabled = builder.Configuration.GetValue<bool?>("DetailedErrors") ?? builder.Environment.IsDevelopment();
var promptAttachmentMessageLimitBytes = 8 * 1024 * 1024;

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = detailedErrorsEnabled)
    // Prompt-session attachments are posted through JS interop, so the default 32 KB SignalR limit
    // is too small for screenshots and other evidence files added from the canvas wizard.
    .AddHubOptions(options => options.MaximumReceiveMessageSize = promptAttachmentMessageLimitBytes);

builder.Services.AddCanDoItAllComponents();
builder.Services.AddCanDoItAllInfrastructure(builder.Configuration, builder.Environment, ModuleAssemblies.All);
builder.Services.AddHttpClient<DevelopmentManagerClient>();
builder.Services.AddScoped<IWorkbenchStateStore, BrowserWorkspaceStateStore>();
builder.Services.AddScoped<TuningCoordinator>();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddSecurityModule();
builder.Services.AddWorkspaceModule();
builder.Services.AddProjectsModule();
builder.Services.AddWorkbenchModule();
builder.Services.AddResourcesModule();
builder.Services.AddPromptsModule();
builder.Services.AddFactoryModule();
builder.Services.AddValidationModule();
builder.Services.AddTestLabModule();
builder.Services.AddActivityModule();
builder.Services.AddAutomationModule();

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
var workspaceResolver = app.Services.GetRequiredService<IWorkspacePathResolver>();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(workspaceResolver.ResolveManagedFilesRoot()),
    RequestPath = "/managed-files"
});

if (app.Environment.IsDevelopment())
{
    app.MapGet("/_dev/runtime", (IRuntimeReadinessService readiness) =>
    {
        var iteration = int.TryParse(Environment.GetEnvironmentVariable("DOTNET_WATCH_ITERATION"), out var parsed)
            ? parsed
            : (int?)null;

        var snapshot = readiness.GetSnapshot();

        return Results.Ok(new
        {
            snapshot.IsReady,
            snapshot.EnvironmentName,
            snapshot.Summary,
            WatchIteration = iteration,
            HotReloadGeneration = RuntimeHotReloadTracker.CurrentGeneration,
            RuntimePid = Environment.ProcessId,
            OwnerKind = app.Configuration["CanDoItAllMcpOwnerKind"],
            OwnerId = app.Configuration["CanDoItAllMcpOwnerId"],
            ServerInstanceId = app.Configuration["CanDoItAllMcpServerInstanceId"],
            snapshot.StartedAtUtc,
            snapshot.LastChangedAtUtc,
            snapshot.ActiveUrls
        });
    });
}

app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(ModuleAssemblies.All)
    .AddInteractiveServerRenderMode();
app.MapHealthChecks("/health");

await using (var scope = app.Services.CreateAsyncScope())
{
    var readiness = scope.ServiceProvider.GetRequiredService<IRuntimeReadinessService>();
    readiness.MarkStarting(app.Environment.EnvironmentName, app.Urls.Count > 0 ? app.Urls : ["https://localhost"]);

    var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var dbContext = await dbContextFactory.CreateDbContextAsync();
    await dbContext.Database.EnsureCreatedAsync();
    await PromptFactorySchemaInitializer.EnsureAsync(dbContext);
    await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext);

    readiness.MarkReady(app.Environment.EnvironmentName, urls: app.Urls.Count > 0 ? app.Urls : ["https://localhost"]);
}

app.Run();

public partial class Program;
