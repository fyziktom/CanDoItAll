using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CanDoItAll.Tests.Integration;

internal sealed class ProjectStructureAgentApiTestHost : IAsyncDisposable
{
    private ProjectStructureAgentApiTestHost(
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

    public static async Task<ProjectStructureAgentApiTestHost> CreateAsync()
    {
        var testEnvironment = CanDoItAllTestEnvironment.Create("candoitall-api-tests");
        var activeProfile = testEnvironment.CreateManagedSqliteProfile("api-host");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = testEnvironment.RootPath,
            EnvironmentName = Environments.Development,
            ApplicationName = "CanDoItAll.Tests.Integration"
        });

        builder.Configuration.AddInMemoryCollection(activeProfile.CreateConfigurationValues(new Dictionary<string, string?>
        {
            ["DevelopmentManager:TuningModeEnabled"] = "false",
        }));

        TestApplicationBootstrap.ConfigureDefaultServices(
            builder.Services,
            builder.Configuration,
            builder.Environment);

        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.MapProjectStructureAgentApi();
        var clientToken = string.Empty;

        await TestApplicationBootstrap.InitializeSchemaAsync(app.Services, TestSchemaBootstrapModules.Full);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var administrationService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentAdministrationService>();
            await administrationService.SaveSettingsAsync(
                new ProjectStructureAgentWorkspaceSettingsModel
                {
                    CentralBaseUrl = "http://127.0.0.1",
                    InstallScriptPath = @"tools\Install-CanDoItAllProjectStructureMcp.ps1",
                    SetupReadmePath = @"docs\project-structure-mcp-setup.md"
                },
                CancellationToken.None);

            var profileResult = await administrationService.SaveProfileAsync(
                new ProjectStructureAgentProfileEditorModel
                {
                    Name = "API Test Agent",
                    Description = "Integration host agent profile",
                    CapabilityMask = ProjectStructureAgentCapability.All,
                    GenerateNewToken = true
                },
                CancellationToken.None);
            if (!profileResult.IsSuccess)
            {
                throw new InvalidOperationException(string.Join(" ", profileResult.Errors.Select(error => error.Message)));
            }

            var seededProfile = await administrationService.GetProfileAsync(profileResult.Value, CancellationToken.None);
            clientToken = seededProfile.TokenValue;
        }

        await app.StartAsync();
        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses
            ?? throw new InvalidOperationException("The API test host did not expose any server addresses.");
        var client = new HttpClient
        {
            BaseAddress = new Uri(addresses.Single())
        };
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.AgentId, "api-test-agent");
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.AgentName, "API Test Agent");
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.MachineName, "api-test-machine");
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.RepositoryRoot, "C:/repositories/CanDoItAll");
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.BranchName, "tests/project-structure");
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.SessionId, Guid.NewGuid().ToString("N"));
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.AgentToken, clientToken);

        return new ProjectStructureAgentApiTestHost(testEnvironment, activeProfile, app, client);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await App.StopAsync();
        await App.DisposeAsync();
        await TestEnvironment.DisposeAsync();
    }
}
