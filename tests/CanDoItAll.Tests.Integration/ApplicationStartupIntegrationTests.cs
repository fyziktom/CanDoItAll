using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AppComponents;
using CanDoItAll.Components.Charts;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Readiness;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web;
using CanDoItAll.Web.Api;
using CanDoItAll.Web.Composition;
using CanDoItAll.Web.Components;
using CanDoItAll.Web.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Integration;

public sealed class ApplicationStartupIntegrationTests {
    private static readonly ProcessTemplateCatalogInventoryItem[] RequiredProcessTemplateCatalogEntries =
        ProcessTemplateCatalogInventory.RequiredRepresentativeTemplates.ToArray();

    [Fact]
    public async Task Web_app_startup_SB009_INV_001_starts_current_composition_with_process_module_registered() {
        await using var host = await StartupSmokeHost.CreateAsync();

        var healthResponse = await host.Client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);

        var templates = await host.Client.GetFromJsonAsync<IReadOnlyList<ProcessTemplateCatalogItem>>("/api/processes/templates");
        Assert.NotNull(templates);
        Assert.NotEmpty(templates);
        Assert.Contains(templates, template => string.Equals(template.Key, "blazor-app-delivery", StringComparison.Ordinal));

        await using var scope = host.App.Services.CreateAsyncScope();
        var serviceProvider = scope.ServiceProvider;
        Assert.Contains(
            CanDoItAll.Web.Composition.ModuleAssemblies.All,
            assembly => string.Equals(assembly.GetName().Name, "CanDoItAll.Modules.Processes", StringComparison.Ordinal));
        Assert.NotNull(serviceProvider.GetRequiredService<ProcessesService>());
        Assert.NotNull(serviceProvider.GetRequiredService<ProcessTemplateCatalogService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IProcessRunAutomationDispatchService>());
    }

    [Fact]
    public async Task Process_template_catalog_SB012_INV_001_exposes_required_templates_to_api_and_ui_launch_surfaces() {
        await using var host = await StartupSmokeHost.CreateAsync();

        var templates = await host.Client.GetFromJsonAsync<IReadOnlyList<ProcessTemplateCatalogItem>>("/api/processes/templates");
        Assert.NotNull(templates);

        foreach (var requiredTemplate in RequiredProcessTemplateCatalogEntries) {
            var catalogItem = Assert.Single(
                templates,
                template => string.Equals(template.Key, requiredTemplate.TemplateKey, StringComparison.Ordinal));
            Assert.Equal(requiredTemplate.RelativePath, catalogItem.RelativePath.Replace("\\", "/", StringComparison.Ordinal));
            if (requiredTemplate.Family == ProcessTemplateInventoryFamily.MultiTeamDevelopment) {
                Assert.Equal(ProcessTemplateInventoryResolutionKind.MappedTemplate, requiredTemplate.ResolutionKind);
                Assert.Contains("multi-team", catalogItem.DisplayName, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("multi-team", catalogItem.Summary, StringComparison.OrdinalIgnoreCase);
            }

            using var detail = await ReadSuccessfulJsonAsync(host.Client, $"/api/processes/templates/{requiredTemplate.TemplateKey}/detail");
            Assert.Equal(requiredTemplate.TemplateKey, detail.RootElement.GetProperty("summary").GetProperty("key").GetString());
            Assert.True(detail.RootElement.GetProperty("template").GetProperty("steps").GetArrayLength() > 0);

            var envelope = await host.Client.GetFromJsonAsync<ProcessImportExportEnvelope>($"/api/processes/templates/{requiredTemplate.TemplateKey}/envelope");
            Assert.NotNull(envelope);
            Assert.Equal("CanDoItAll.ProcessTemplatePack/current-module-projection", envelope.SourceFormat);
            Assert.NotEmpty(envelope.Definition.Steps);
            Assert.Contains(
                envelope.Warnings,
                warning => warning.Contains($"'{requiredTemplate.TemplateKey}'", StringComparison.Ordinal));

            using var mermaid = await ReadSuccessfulJsonAsync(host.Client, $"/api/processes/templates/{requiredTemplate.TemplateKey}/mermaid");
            Assert.Equal(requiredTemplate.TemplateKey, mermaid.RootElement.GetProperty("processKey").GetString());
            Assert.False(string.IsNullOrWhiteSpace(mermaid.RootElement.GetProperty("flowchart").GetString()));
        }

        var baselineScenarios = await host.Client.GetFromJsonAsync<IReadOnlyList<ProcessTemplateBaselineScenarioSummary>>("/api/processes/templates/baseline-scenarios");
        Assert.NotNull(baselineScenarios);
        Assert.Contains(baselineScenarios, scenario => string.Equals(scenario.Key, "baseline-software-delivery", StringComparison.Ordinal));
        Assert.Contains(baselineScenarios, scenario => string.Equals(scenario.Key, "baseline-business-plan-development", StringComparison.Ordinal));
        Assert.Contains(baselineScenarios, scenario => string.Equals(scenario.Key, "baseline-blazor-wasm-pwa-app", StringComparison.Ordinal));

        var liveRunProfiles = await host.Client.GetFromJsonAsync<IReadOnlyList<ProcessTemplateLiveRunProfileSummary>>("/api/processes/templates/live-run-profiles");
        Assert.NotNull(liveRunProfiles);
        Assert.Contains(liveRunProfiles, profile =>
            string.Equals(profile.Key, "generic-blazor-wasm-pwa-app", StringComparison.Ordinal) &&
            string.Equals(profile.ProcessTemplateKey, "blazor-app-delivery", StringComparison.Ordinal) &&
            profile.FreshRunPolicy.RequiresFreshRun);

        Assert.Contains(ShellNavigation.Items, item => string.Equals(item.Route, "/processes", StringComparison.Ordinal));
        Assert.Contains(ShellNavigation.Items, item => string.Equals(item.Route, "/processes/live", StringComparison.Ordinal));

        await using var scope = host.App.Services.CreateAsyncScope();
        var libraryService = scope.ServiceProvider.GetRequiredService<ProcessTemplateLibraryService>();
        var libraryItems = libraryService.ListItems(ProcessTemplateLibraryCategory.Processes);
        foreach (var requiredTemplate in RequiredProcessTemplateCatalogEntries) {
            Assert.Contains(libraryItems, item => string.Equals(item.Key, requiredTemplate.TemplateKey, StringComparison.Ordinal));
        }
    }

    private static async Task<JsonDocument> ReadSuccessfulJsonAsync(HttpClient client, string requestUri) {
        var response = await client.GetAsync(requestUri);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonDocument.Parse(body);
    }

    private sealed class StartupSmokeHost : IAsyncDisposable {
        private StartupSmokeHost(
            CanDoItAllTestEnvironment testEnvironment,
            WebApplication app,
            HttpClient client) {
            TestEnvironment = testEnvironment;
            App = app;
            Client = client;
        }

        public CanDoItAllTestEnvironment TestEnvironment { get; }

        public WebApplication App { get; }

        public HttpClient Client { get; }

        public static async Task<StartupSmokeHost> CreateAsync() {
            var testEnvironment = CanDoItAllTestEnvironment.Create("candoitall-startup-smoke");
            var activeProfile = testEnvironment.CreateInMemoryProfile("startup-smoke", $"startup-smoke-{Guid.NewGuid():N}");
            var configurationOverrides = new Dictionary<string, string?> {
                ["ControlPlane:RootPath"] = testEnvironment.ControlPlaneRootPath,
                ["DevelopmentManager:TuningModeEnabled"] = "false",
                [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind,
                ["Api:Enabled"] = "true",
                ["Api:OpenApiEnabled"] = "true",
                ["Api:Authorization:Enabled"] = "false"
            };

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
                ContentRootPath = testEnvironment.RootPath,
                EnvironmentName = Environments.Development,
                ApplicationName = typeof(ProjectStructureAgentApi).Assembly.GetName().Name
            });
            builder.Host.UseDefaultServiceProvider(options => {
                options.ValidateOnBuild = true;
                options.ValidateScopes = true;
            });
            builder.Configuration.AddInMemoryCollection(activeProfile.CreateConfigurationValues(configurationOverrides));

            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();
            builder.Services.AddCanDoItAllCharts();
            TestApplicationBootstrap.ConfigureDefaultServices(
                builder.Services,
                builder.Configuration,
                builder.Environment,
                registerTestHostApplicationLifetime: false);
            builder.Services.AddCanDoItAllApi(builder.Configuration);
            builder.Services.AddHttpClient<DevelopmentManagerClient>();
            builder.Services.AddScoped<TuningCoordinator>();

            var app = builder.Build();
            app.Urls.Add("http://127.0.0.1:0");

            var readiness = app.Services.GetRequiredService<IRuntimeReadinessService>();
            readiness.MarkStarting(app.Environment.EnvironmentName, app.Urls);

            app.UseAntiforgery();
            app.MapStaticAssets();
            app.MapCanDoItAllManagedFiles();

            var apiOptions = app.Services.GetRequiredService<IOptions<ApiAccessOptions>>().Value;
            if (apiOptions.OpenApiEnabled) {
                app.MapOpenApi();
                app.MapOpenApi("/swagger/{documentName}/swagger.json");
            }

            app.MapProjectStructureAgentApi();
            app.MapCanDoItAllApi();
            app.MapRazorComponents<App>()
                .AddAdditionalAssemblies(CanDoItAll.Web.Composition.ModuleAssemblies.All)
                .AddInteractiveServerRenderMode();
            app.MapHealthChecks("/health");

            await TestApplicationBootstrap.InitializeSchemaAsync(app.Services, TestSchemaBootstrapModules.Full);
            var bootstrapper = app.Services.GetRequiredService<IAppDatabaseBootstrapper>();
            await bootstrapper.EnsureCurrentProfileReadyAsync();
            readiness.MarkReady(app.Environment.EnvironmentName, urls: app.Urls);

            await app.StartAsync();
            return new StartupSmokeHost(testEnvironment, app, CreateClient(app));
        }

        public async ValueTask DisposeAsync() {
            Client.Dispose();
            await App.StopAsync();
            await App.DisposeAsync();
            await TestEnvironment.DisposeAsync();
        }

        private static HttpClient CreateClient(WebApplication app) {
            var server = app.Services.GetRequiredService<IServer>();
            var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses
                ?? throw new InvalidOperationException("The startup smoke host did not expose any server addresses.");
            return new HttpClient {
                BaseAddress = new Uri(addresses.Single()),
                Timeout = TimeSpan.FromSeconds(30)
            };
        }
    }
}
