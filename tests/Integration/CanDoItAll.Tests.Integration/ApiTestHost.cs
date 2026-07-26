using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web;
using CanDoItAll.Web.Api;
using CanDoItAll.Web.Composition;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Integration;

internal sealed class ApiTestHost : IAsyncDisposable
{
    private ApiTestHost(
        CanDoItAllTestEnvironment testEnvironment,
        TestDatabaseProfile activeProfile,
        WebApplication app,
        HttpClient client)
    {
        TestEnvironment = testEnvironment;
        ActiveProfile = activeProfile;
        RootPath = testEnvironment.RootPath;
        App = app;
        Client = client;
    }

    public string RootPath { get; }

    public CanDoItAllTestEnvironment TestEnvironment { get; }

    public TestDatabaseProfile ActiveProfile { get; }

    public WebApplication App { get; }

    public HttpClient Client { get; }

    public static async Task<ApiTestHost> CreateAsync(
        bool jwtEnabled,
        Action<IServiceCollection>? configureServices = null,
        bool useInMemoryDatabase = false)
    {
        var testEnvironment = CanDoItAllTestEnvironment.Create("candoitall-api-tests");
        var activeProfile = useInMemoryDatabase
            ? testEnvironment.CreateInMemoryProfile("api-host")
            : testEnvironment.CreatePostgreSqlProfile("api-host");
        var configurationOverrides = new Dictionary<string, string?>
        {
            ["DevelopmentManager:TuningModeEnabled"] = "false",
            [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind,
            ["Api:Enabled"] = "true",
            ["Api:OpenApiEnabled"] = "true",
            ["Api:Authorization:Enabled"] = jwtEnabled.ToString(),
            ["Api:Authorization:Issuer"] = "CanDoItAll.Api.Tests",
            ["Api:Authorization:Audience"] = "CanDoItAll.Api.Tests",
            ["Api:Authorization:SigningKey"] = "api-test-signing-key-32-bytes-minimum",
            ["Api:Authorization:DefaultTokenLifetimeMinutes"] = "30",
            ["Api:Authorization:MaxTokenLifetimeMinutes"] = "120"
        };

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = testEnvironment.RootPath,
            EnvironmentName = Environments.Development,
            ApplicationName = "CanDoItAll.Tests.Integration"
        });
        builder.Configuration.AddInMemoryCollection(activeProfile.CreateConfigurationValues(configurationOverrides));

        TestApplicationBootstrap.ConfigureDefaultServices(
            builder.Services,
            builder.Configuration,
            builder.Environment,
            registerTestHostApplicationLifetime: false);
        builder.Services.AddCanDoItAllApi(builder.Configuration);
        configureServices?.Invoke(builder.Services);

        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");

        var options = app.Services.GetRequiredService<IOptions<ApiAccessOptions>>().Value;
        if (options.Authorization.Enabled)
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }

        if (options.OpenApiEnabled)
        {
            var openApiEndpoint = app.MapOpenApi();
            var swaggerEndpoint = app.MapOpenApi("/swagger/{documentName}/swagger.json");
            if (options.Authorization.Enabled)
            {
                openApiEndpoint.RequireAuthorization();
                swaggerEndpoint.RequireAuthorization();
            }
        }

        app.MapProjectStructureAgentApi();
        app.MapCanDoItAllApi();

        await TestApplicationBootstrap.InitializeSchemaAsync(app.Services, TestSchemaBootstrapModules.Full);
        await app.StartAsync();

        var client = CreateClient(app);
        return new ApiTestHost(testEnvironment, activeProfile, app, client);
    }

    public HttpClient CreateClient()
    {
        return CreateClient(App);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await App.StopAsync();
        await App.DisposeAsync();
        await TestEnvironment.DisposeAsync();
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses
            ?? throw new InvalidOperationException("The API test host did not expose any server addresses.");
        return new HttpClient
        {
            BaseAddress = new Uri(addresses.Single()),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }
}
