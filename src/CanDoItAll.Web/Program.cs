using CanDoItAll.Components;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.DependencyInjection;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Readiness;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Activity;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Automation;
using CanDoItAll.Modules.Collaboration;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Processes;
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
using CanDoItAll.Web;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var detailedErrorsEnabled = builder.Configuration.GetValue<bool?>("DetailedErrors") ?? builder.Environment.IsDevelopment();
var promptAttachmentMessageLimitBytes = 8 * 1024 * 1024;

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = detailedErrorsEnabled)
    // Prompt-session attachments are posted through JS interop, so the default 32 KB SignalR limit
    // is too small for screenshots and other evidence files added from the canvas wizard.
    .AddHubOptions(options => options.MaximumReceiveMessageSize = promptAttachmentMessageLimitBytes);

builder.Services.AddCanDoItAllBaseLib();
builder.Services.AddCanDoItAllInfrastructure(builder.Configuration, builder.Environment, ModuleAssemblies.All);
builder.Services.AddCanDoItAllRuntimeDatabaseSwitching();
builder.Services.AddMermaidJS();
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
builder.Services.AddProcessesModule();
builder.Services.AddValidationModule();
builder.Services.AddTestLabModule();
builder.Services.AddActivityModule();
builder.Services.AddAgentFrameworkModule();
builder.Services.AddAutomationModule();
builder.Services.AddCollaborationModule();
builder.Services.AddCrmHrModule();

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
app.MapCanDoItAllManagedFiles();

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

    app.MapGet("/_dev/database/selection", (IDatabaseProfileRuntimeAccessor profileAccessor) =>
    {
        var profile = profileAccessor.ResolveCurrentProfile();
        return Results.Ok(new
        {
            profile.Profile.Id,
            profile.Profile.DisplayName,
            profile.Profile.ProviderKind,
            profile.Profile.SourceKind,
            profile.Profile.Runtime.Fingerprint,
            profile.Profile.Storage.WorkspaceRoot,
            profile.ConnectionString
        });
    });

    app.MapPost("/_dev/database/profiles/managed-sqlite", async (
        IDatabaseProfileService profileService,
        IDatabaseProfileRuntimeAccessor profileAccessor,
        IAppDatabaseBootstrapper bootstrapper) =>
    {
        var saveResult = await profileService.SaveAsync(new CanDoItAll.Infrastructure.ControlPlane.DatabaseProfileEditorModel
        {
            DisplayName = $"Managed sqlite {Guid.NewGuid():N}"[..22],
            ProviderKind = DatabaseProviderKind.Sqlite,
            SourceKind = DatabaseProfileSourceKind.ManagedSqlite
        });
        if (saveResult.IsFailure)
        {
            return Results.BadRequest(saveResult.Errors.Select(error => error.Message).ToArray());
        }

        var profile = profileAccessor.ResolveProfile(saveResult.Value);
        await bootstrapper.EnsureProfileReadyAsync(profile);

        return Results.Ok(new
        {
            profile.Profile.Id,
            profile.Profile.DisplayName,
            profile.Profile.ProviderKind,
            profile.Profile.SourceKind,
            profile.Profile.Runtime.Fingerprint,
            profile.Profile.Storage.WorkspaceRoot,
            profile.ConnectionString
        });
    });

    app.MapPost("/_dev/database/switch/{profileId:guid}", async (
        Guid profileId,
        IDatabaseSwitchCoordinator switchCoordinator,
        IDatabaseProfileRuntimeAccessor profileAccessor) =>
    {
        var switchResult = await switchCoordinator.SwitchAsync(profileId);
        if (switchResult.IsFailure)
        {
            return Results.BadRequest(switchResult.Errors.Select(error => error.Message).ToArray());
        }

        var profile = profileAccessor.ResolveCurrentProfile();
        return Results.Ok(new
        {
            switchResult.Value!.Generation,
            switchResult.Value.CurrentProfileId,
            profile.Profile.DisplayName,
            profile.Profile.Runtime.Fingerprint,
            profile.Profile.Storage.WorkspaceRoot,
            profile.ConnectionString
        });
    });

    app.MapPost("/_dev/database/seed-profile", async (
        string? label,
        ProjectsService projectsService,
        IManagedArtifactStore managedArtifactStore) =>
    {
        var seedLabel = string.IsNullOrWhiteSpace(label)
            ? $"Seed {Guid.NewGuid():N}"[..12]
            : label.Trim();
        var saveResult = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = $"{seedLabel} Project",
            Description = $"{seedLabel} description",
            Objective = $"{seedLabel} objective",
            CurrentPhase = "Execution"
        });
        if (saveResult.IsFailure)
        {
            return Results.BadRequest(saveResult.Errors.Select(error => error.Message).ToArray());
        }

        var fileName = string.Concat(seedLabel.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '-' : character));
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "seed";
        }

        var relativePath = managedArtifactStore.GetRelativePath("profile-seeds", $"{fileName}.txt");
        var content = $"seed:{seedLabel}";
        var fullPath = await managedArtifactStore.SaveTextAsync("profile-seeds", $"{fileName}.txt", content);

        return Results.Ok(new
        {
            saveResult.Value,
            ProjectName = $"{seedLabel} Project",
            ManagedFileRelativePath = relativePath,
            ManagedFileFullPath = fullPath,
            ManagedFileContent = content
        });
    });

    app.MapPost("/_dev/projects", async (
        string? name,
        string? phase,
        ProjectsService projectsService) =>
    {
        var saveResult = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"Runtime Switch {Guid.NewGuid():N}"[..24] : name.Trim(),
            Description = "Development-only runtime switch proof project.",
            Objective = "Drive stale-route recovery proof.",
            CurrentPhase = string.IsNullOrWhiteSpace(phase) ? "Execution" : phase.Trim()
        });
        if (saveResult.IsFailure)
        {
            return Results.BadRequest(saveResult.Errors.Select(error => error.Message).ToArray());
        }

        return Results.Ok(new
        {
            ProjectId = saveResult.Value,
            Route = $"/projects/{saveResult.Value:D}/structure"
        });
    });
}

app.MapProjectStructureAgentApi();
app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(ModuleAssemblies.All)
    .AddInteractiveServerRenderMode();
app.MapHealthChecks("/health");

await using (var scope = app.Services.CreateAsyncScope())
{
    var readiness = scope.ServiceProvider.GetRequiredService<IRuntimeReadinessService>();
    readiness.MarkStarting(app.Environment.EnvironmentName, app.Urls.Count > 0 ? app.Urls : ["https://localhost"]);

    var bootstrapper = scope.ServiceProvider.GetRequiredService<IAppDatabaseBootstrapper>();
    await bootstrapper.EnsureCurrentProfileReadyAsync();

    readiness.MarkReady(app.Environment.EnvironmentName, urls: app.Urls.Count > 0 ? app.Urls : ["https://localhost"]);
}

app.Run();

public partial class Program;


