using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web;
using CanDoItAll.Web.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Integration;

public sealed class DevelopmentApiIntegrationTests
{
    [Fact]
    public async Task DevelopmentApi_allows_project_access_without_bearer_token_when_jwt_is_disabled()
    {
        await using var host = await DevelopmentApiTestHost.CreateAsync(jwtEnabled: false);

        var projectResponse = await host.Client.GetAsync("/api/dev/projects");
        var openApiResponse = await host.Client.GetAsync("/openapi/v1.json");
        var agentExecutionRunsResponse = await host.Client.GetAsync("/api/dev/agents/execution-runs");

        Assert.Equal(HttpStatusCode.OK, projectResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, openApiResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, agentExecutionRunsResponse.StatusCode);
    }

    [Fact]
    public async Task DevelopmentApi_requires_bearer_token_when_jwt_is_enabled()
    {
        await using var host = await DevelopmentApiTestHost.CreateAsync(jwtEnabled: true);

        var unauthorizedResponse = await host.Client.GetAsync("/api/dev/projects");
        var statusResponse = await host.Client.GetAsync("/api/dev/access/status");

        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

        var tokenService = host.App.Services.GetRequiredService<IDevelopmentApiTokenService>();
        var token = tokenService.IssueToken(new DevelopmentApiTokenIssueRequest
        {
            Subject = "integration-test",
            DisplayName = "Integration test",
            LifetimeMinutes = 30,
            Scopes = ["development-api"]
        });

        using var authorizedClient = host.CreateClient();
        authorizedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        var authorizedResponse = await authorizedClient.GetAsync("/api/dev/projects");
        var authorizedOpenApiResponse = await authorizedClient.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, authorizedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, authorizedOpenApiResponse.StatusCode);
    }

    [Fact]
    public async Task DevelopmentApi_filters_process_run_artifacts_by_artifact_id()
    {
        await using var host = await DevelopmentApiTestHost.CreateAsync(jwtEnabled: false);

        Guid seededRunId;
        ProcessArtifactViewModel expectedArtifact;
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
            var definitionResult = await processesService.SaveAsync(BuildFilterTestDefinition());
            Assert.True(definitionResult.IsSuccess, string.Join(" | ", definitionResult.Errors.Select(error => error.Message)));

            var publishResult = await processesService.PublishAsync(definitionResult.Value);
            Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

            var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
            {
                ProcessDefinitionId = definitionResult.Value,
                RunName = "Development API filtering run",
                OperatingMode = ProcessOperatingMode.AssistedExecution,
                TriggerReason = "Integration test"
            });
            Assert.True(runResult.IsSuccess, string.Join(" | ", runResult.Errors.Select(error => error.Message)));
            seededRunId = runResult.Value;

            var stepRun = Assert.Single(await processesService.ListStepRunsAsync(seededRunId));
            var artifactResult = await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest
            {
                ProcessRunId = seededRunId,
                StepRunId = stepRun.Id,
                ArtifactKind = ProcessArtifactKind.Evidence,
                Title = "Focused API artifact",
                TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
                SensitivityLevel = ProcessSensitivityLevel.Internal,
                ProvenanceSummary = "Created by DevelopmentApiIntegrationTests.",
                AllowedFutureUsageSummary = "Regression validation."
            });
            Assert.True(artifactResult.IsSuccess, string.Join(" | ", artifactResult.Errors.Select(error => error.Message)));

            expectedArtifact = Assert.Single(await processesService.ListArtifactsAsync(seededRunId));
        }

        var response = await host.Client.GetAsync(
            $"/api/dev/processes/runs/{seededRunId:D}?artifactId={expectedArtifact.Id:D}&includeWorkBriefs=false&includeExecutionRuns=false&includeDirectMessages=false");

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, responseBody);
        using var payload = JsonDocument.Parse(responseBody);
        var artifacts = payload.RootElement.GetProperty("artifacts").EnumerateArray().ToList();
        var workBriefs = payload.RootElement.GetProperty("workBriefs").EnumerateArray().ToList();

        Assert.Single(artifacts);
        Assert.Equal(expectedArtifact.Id, artifacts[0].GetProperty("id").GetGuid());
        Assert.Empty(workBriefs);
    }

    private static ProcessDefinitionEditorModel BuildFilterTestDefinition()
    {
        var roleId = Guid.NewGuid();
        var stepId = Guid.NewGuid();
        return new ProcessDefinitionEditorModel
        {
            Name = "Development API filter definition",
            Summary = "Small definition for API filtering validation.",
            ValueStatement = "Expose focused process run evidence without loading unrelated slices.",
            CustomerName = "Development",
            OwnerName = "Integration tests",
            GovernancePolicySummary = "Generated only for local test coverage.",
            ChangeSummary = "Initial test definition.",
            ConstitutionRuleSummary = "Keep API filtering deterministic.",
            OperatingModeSummary = "Assisted local validation.",
            SimulationReadinessSummary = "Safe for integration tests.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = roleId,
                    Key = "api-operator",
                    DisplayName = "API operator",
                    Purpose = "Own the focused API filtering test.",
                    StaffingIntent = "A deterministic local role for process runtime validation.",
                    PreferredExecutorKind = "person",
                    SnapshotSummary = "Development API integration test role."
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = stepId,
                    Key = "evidence",
                    Title = "Capture focused artifact",
                    StepKind = ProcessStepKind.Work,
                    InputContractSummary = "None.",
                    OutputContractSummary = "One focused artifact.",
                    EvidenceContractSummary = "The API filter should return exactly this artifact.",
                    DecisionRightsSummary = "Integration test controls the step.",
                    ExceptionPolicySummary = "Fail the test on unexpected runtime errors.",
                    TargetLeadHours = 1,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = roleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible,
                            RebindPolicySummary = "Keep the local API operator on the step."
                        }
                    ],
                    ArtifactExpectations =
                    [
                        new ProcessArtifactExpectationEditorModel
                        {
                            Id = Guid.NewGuid(),
                            ArtifactKind = ProcessArtifactKind.Evidence,
                            Title = "Focused API artifact",
                            ValidationRequirementSummary = "Must be addressable by artifact id."
                        }
                    ]
                }
            ]
        };
    }
}

internal sealed class DevelopmentApiTestHost : IAsyncDisposable
{
    private DevelopmentApiTestHost(
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

    public static async Task<DevelopmentApiTestHost> CreateAsync(bool jwtEnabled)
    {
        var testEnvironment = CanDoItAllTestEnvironment.Create("candoitall-development-api-tests");
        var activeProfile = testEnvironment.CreateManagedSqliteProfile("development-api-host");
        var configurationOverrides = new Dictionary<string, string?>
        {
            ["DevelopmentManager:TuningModeEnabled"] = "false",
            [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind,
            ["DevelopmentApi:Enabled"] = "true",
            ["DevelopmentApi:OpenApiEnabled"] = "true",
            ["DevelopmentApi:Authorization:Enabled"] = jwtEnabled.ToString(),
            ["DevelopmentApi:Authorization:Issuer"] = "CanDoItAll.DevelopmentApi.Tests",
            ["DevelopmentApi:Authorization:Audience"] = "CanDoItAll.DevelopmentApi.Tests",
            ["DevelopmentApi:Authorization:SigningKey"] = "development-api-test-signing-key-32-bytes-minimum",
            ["DevelopmentApi:Authorization:DefaultTokenLifetimeMinutes"] = "30",
            ["DevelopmentApi:Authorization:MaxTokenLifetimeMinutes"] = "120"
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
        builder.Services.AddCanDoItAllDevelopmentApi(builder.Configuration);

        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");

        var options = app.Services.GetRequiredService<IOptions<DevelopmentApiAccessOptions>>().Value;
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
        app.MapCanDoItAllDevelopmentApi();

        await TestApplicationBootstrap.InitializeSchemaAsync(app.Services, TestSchemaBootstrapModules.Full);
        await app.StartAsync();

        var client = CreateClient(app);
        return new DevelopmentApiTestHost(testEnvironment, activeProfile, app, client);
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
            ?? throw new InvalidOperationException("The development API test host did not expose any server addresses.");
        return new HttpClient
        {
            BaseAddress = new Uri(addresses.Single()),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }
}
