using CanDoItAll.AppComponents;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Charts;
using CanDoItAll.Components.Mermaid.Infrastructure;
using CanDoItAll.Conversations.Shell;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.DependencyInjection;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Readiness;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using IProviderRuntimeAdministrationService = CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderRuntimeAdministrationService;
using CanDoItAll.Modules.Collaboration;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web.Components;
using CanDoItAll.Web.Composition;
using CanDoItAll.Web.Infrastructure;
using CanDoItAll.Web.Dashboard;
using CanDoItAll.Web;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Web.Api;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();
DatabasePasswordFileConfiguration.Apply(builder.Configuration, builder.Environment.ContentRootPath);
ApiAuthorizationSigningKeyFileConfiguration.Apply(
    builder.Configuration,
    builder.Environment.ContentRootPath);
var detailedErrorsEnabled = builder.Configuration.GetValue<bool?>("DetailedErrors") ?? builder.Environment.IsDevelopment();
var databaseOptions = builder.Configuration.GetSection("Database").Get<DatabaseOptions>() ?? new DatabaseOptions();
var webHostOptions = builder.Configuration.GetSection(WebHostRuntimeOptions.SectionName).Get<WebHostRuntimeOptions>() ?? new WebHostRuntimeOptions();

if (!databaseOptions.EnableEntityFrameworkConsoleLogging)
{
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Infrastructure", LogLevel.Warning);
}

builder.Services.AddCanDoItAllInteractiveServer(detailedErrorsEnabled);

builder.Services.AddCanDoItAllBaseLib();
builder.Services.AddConversationShell();
builder.Services.AddCanDoItAllCharts();
builder.Services.AddCanDoItAllInfrastructure(builder.Configuration, builder.Environment, CanDoItAll.Web.Composition.ModuleAssemblies.All);
builder.Services.AddCanDoItAllRuntimeDatabaseSwitching();
builder.Services.AddCanDoItAllRuntimeModules(
    builder.Configuration,
    builder.Environment,
    builder.Environment.ContentRootPath);
builder.Services.AddAgentFrameworkUi();
builder.Services.AddCanDoItAllDashboard();
builder.Services.AddCanDoItAllFileToolsStoragePlacementRevision();
builder.Services.AddCanDoItAllApi(builder.Configuration);
builder.Services.AddCanDoItAllLocalOperatorUiAuthentication();
builder.Services.AddCanDoItAllLlmChatsUi();
builder.Services.AddCanDoItAllMermaid();
builder.Services.AddHttpClient<DevelopmentManagerClient>();
builder.Services.AddScoped<IWorkbenchStateStore, BrowserWorkspaceStateStore>();
builder.Services.AddScoped<TuningCoordinator>();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = webHostOptions.TrustedProxies.Length == 0 ? ForwardedHeaders.None :
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    foreach (var proxy in webHostOptions.TrustedProxies) {
        options.KnownProxies.Add(System.Net.IPAddress.Parse(proxy));
    }
});
if (webHostOptions.AllowedOrigins.Length > 0) {
    if (webHostOptions.AllowedOrigins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
        uri.Scheme is not ("http" or "https") || uri.GetLeftPart(UriPartial.Authority) != origin || origin.Contains('*'))) {
        throw new InvalidOperationException("WebHost:AllowedOrigins must contain exact HTTP(S) origins.");
    }
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
        .WithOrigins(webHostOptions.AllowedOrigins).WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
        .WithHeaders("Authorization", "Content-Type", "If-Match", "Idempotency-Key", "Last-Event-ID")));
}

var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Items[DevelopmentEndpointAccess.OriginalRemoteIpItemKey] =
        context.Connection.RemoteIpAddress;
    await next();
});

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseWhen(context => !ApiTransportMiddleware.IsApiRequest(context), branch =>
        branch.UseExceptionHandler("/Error", createScopeForErrors: true));
    app.UseHsts();
}

app.UseWhen(context => !ApiTransportMiddleware.IsApiRequest(context), branch =>
    branch.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));
app.UseMiddleware<ApiTransportMiddleware>();
if (webHostOptions.AllowedOrigins.Length > 0) {
    app.UseCors();
}
if (webHostOptions.HttpsRedirectionEnabled)
{
    app.UseHttpsRedirection();
}
var apiOptions = app.Services.GetRequiredService<IOptions<ApiAccessOptions>>().Value;
if (apiOptions.Authorization.Enabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.UseMiddleware<AccessContextReferenceMiddleware>();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapCanDoItAllManagedFiles();

app.MapCanDoItAllApiDocumentation();

if (app.Environment.IsDevelopment())
{
    app.MapDevelopmentDiagnosticsEndpoints();
}

app.MapProjectStructureAgentApi();
app.MapCanDoItAllApi();
app.MapRuntimeEndpoints();
app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(CanDoItAll.Web.Composition.ModuleAssemblies.All)
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

/// <summary>
/// Request of the development-only operation <c>POST /_dev/database/profiles/postgresql</c>: connection settings of a
/// local PostgreSQL database for a new database profile, and whether to activate it. Every member may be null or
/// omitted except <c>databaseName</c> and <c>username</c>; text values are trimmed.
/// </summary>
/// <param name="DisplayName">
/// Display name of the new profile. Null or blank means <c>PostgreSQL {databaseName}</c>.
/// </param>
/// <param name="Host">PostgreSQL server host name or address. Null or blank means <c>127.0.0.1</c>.</param>
/// <param name="Port">PostgreSQL server TCP port. Null, zero or negative means 5432.</param>
/// <param name="DatabaseName">
/// Name of the application database; it is created when it does not exist and reused when it does. Required: null
/// or blank is rejected with HTTP 400.
/// </param>
/// <param name="Username">PostgreSQL user name. Required: null or blank is rejected with HTTP 400.</param>
/// <param name="Password">
/// PostgreSQL password, stored with the profile. Null means an empty password. Use only local development
/// credentials.
/// </param>
/// <param name="AdminDatabaseName">
/// Maintenance database used to create the application database. Null or blank means <c>postgres</c>.
/// </param>
/// <param name="TrustServerCertificate">
/// True accepts the server's TLS certificate without validation. Null means false.
/// </param>
/// <param name="WorkspaceRoot">
/// Workspace directory of the profile on this host; a relative path is resolved against the host's content root.
/// Null or blank means the host's default workspace root.
/// </param>
/// <param name="Activate">
/// False saves the profile without switching to it. Null or true switches the host to the new profile.
/// </param>
internal sealed record PostgreSqlDevDatabaseProfileRequest(
    string? DisplayName,
    string? Host,
    int? Port,
    string? DatabaseName,
    string? Username,
    string? Password,
    string? AdminDatabaseName,
    bool? TrustServerCertificate,
    string? WorkspaceRoot,
    bool? Activate);

public partial class Program;
