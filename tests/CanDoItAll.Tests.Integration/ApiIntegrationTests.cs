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

public sealed class ApiIntegrationTests
{
    [Fact]
    public async Task Api_allows_project_access_without_bearer_token_when_jwt_is_disabled()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);

        var projectResponse = await host.Client.GetAsync("/api/projects");
        var openApiResponse = await host.Client.GetAsync("/openapi/v1.json");
        var agentExecutionRunsResponse = await host.Client.GetAsync("/api/agents/execution-runs");

        Assert.Equal(HttpStatusCode.OK, projectResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, openApiResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, agentExecutionRunsResponse.StatusCode);
    }

    [Fact]
    public async Task Api_requires_bearer_token_when_jwt_is_enabled()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: true);

        var unauthorizedResponse = await host.Client.GetAsync("/api/projects");
        var statusResponse = await host.Client.GetAsync("/api/access/status");

        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

        var tokenService = host.App.Services.GetRequiredService<IApiTokenService>();
        var token = tokenService.IssueToken(new ApiTokenIssueRequest
        {
            Subject = "integration-test",
            DisplayName = "Integration test",
            LifetimeMinutes = 30,
            Scopes = ["api"]
        });

        using var authorizedClient = host.CreateClient();
        authorizedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        var authorizedResponse = await authorizedClient.GetAsync("/api/projects");
        var authorizedOpenApiResponse = await authorizedClient.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, authorizedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, authorizedOpenApiResponse.StatusCode);
    }

    [Fact]
    public async Task Api_filters_process_run_artifacts_by_artifact_id()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);

        Guid seededRunId;
        Guid seededStepRunId;
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
                RunName = "API filtering run",
                OperatingMode = ProcessOperatingMode.AssistedExecution,
                TriggerReason = "Integration test"
            });
            Assert.True(runResult.IsSuccess, string.Join(" | ", runResult.Errors.Select(error => error.Message)));
            seededRunId = runResult.Value;

            var stepRun = Assert.Single(await processesService.ListStepRunsAsync(seededRunId));
            seededStepRunId = stepRun.Id;
            var artifactResult = await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest
            {
                ProcessRunId = seededRunId,
                StepRunId = stepRun.Id,
                ArtifactKind = ProcessArtifactKind.Evidence,
                Title = "Focused API artifact",
                TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
                SensitivityLevel = ProcessSensitivityLevel.Internal,
                ProvenanceSummary = "Created by ApiIntegrationTests.",
                AllowedFutureUsageSummary = "Regression validation."
            });
            Assert.True(artifactResult.IsSuccess, string.Join(" | ", artifactResult.Errors.Select(error => error.Message)));

            expectedArtifact = Assert.Single(await processesService.ListArtifactsAsync(seededRunId));
        }

        var response = await host.Client.GetAsync(
            $"/api/processes/runs/{seededRunId:D}?artifactId={expectedArtifact.Id:D}&includeWorkBriefs=false&includeExecutionRuns=false&includeDirectMessages=false");

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, responseBody);
        using var payload = JsonDocument.Parse(responseBody);
        var artifacts = payload.RootElement.GetProperty("artifacts").EnumerateArray().ToList();
        var workBriefs = payload.RootElement.GetProperty("workBriefs").EnumerateArray().ToList();

        Assert.Single(artifacts);
        Assert.Equal(expectedArtifact.Id, artifacts[0].GetProperty("id").GetGuid());
        Assert.Empty(workBriefs);

        var stepArtifactsResponse = await host.Client.GetAsync(
            $"/api/processes/runs/{seededRunId:D}/steps/{seededStepRunId:D}/artifacts?artifactId={expectedArtifact.Id:D}");
        var stepArtifactsBody = await stepArtifactsResponse.Content.ReadAsStringAsync();
        Assert.True(stepArtifactsResponse.IsSuccessStatusCode, stepArtifactsBody);
        using var stepArtifactsPayload = JsonDocument.Parse(stepArtifactsBody);
        var stepArtifacts = stepArtifactsPayload.RootElement.EnumerateArray().ToList();
        Assert.Single(stepArtifacts);
        Assert.Equal(expectedArtifact.Id, stepArtifacts[0].GetProperty("id").GetGuid());

        var artifactResponse = await host.Client.GetAsync(
            $"/api/processes/runs/{seededRunId:D}/artifacts/{expectedArtifact.Id:D}");
        var artifactBody = await artifactResponse.Content.ReadAsStringAsync();
        Assert.True(artifactResponse.IsSuccessStatusCode, artifactBody);
        using var artifactPayload = JsonDocument.Parse(artifactBody);
        Assert.Equal(expectedArtifact.Id, artifactPayload.RootElement.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Api_openapi_exposes_focused_control_plane_routes()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);

        using var payload = JsonDocument.Parse(await host.Client.GetStringAsync("/openapi/v1.json"));
        var paths = payload.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/project-structure/projects/{projectId}/nodes/{nodeId}/type", out _));
        Assert.True(paths.TryGetProperty("/api/project-structure/projects/{projectId}/nodes/{nodeId}/process/start", out _));
        Assert.True(paths.TryGetProperty("/api/project-structure/projects/{projectId}/assets/{nodeId}/content", out _));
        Assert.True(paths.TryGetProperty("/api/processes/templates/{processKey}/detail", out _));
        Assert.True(paths.TryGetProperty("/api/processes/templates/baseline-scenarios", out _));
        Assert.True(paths.TryGetProperty("/api/processes/runs/{runId}/steps/{stepRunId}/artifacts", out _));
        Assert.True(paths.TryGetProperty("/api/processes/runs/{runId}/manager-directives", out _));
        Assert.True(paths.TryGetProperty("/api/agents/{agentId}/execution-runs/{executionRunId}/artifacts", out _));
        Assert.True(paths.TryGetProperty("/api/agents/{agentId}/execution-runs/{executionRunId}/log", out _));
    }

    private static ProcessDefinitionEditorModel BuildFilterTestDefinition()
    {
        var roleId = Guid.NewGuid();
        var stepId = Guid.NewGuid();
        return new ProcessDefinitionEditorModel
        {
            Name = "API filter definition",
            Summary = "Small definition for API filtering validation.",
            ValueStatement = "Expose focused process run evidence without loading unrelated slices.",
            CustomerName = "Engineering",
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
                    SnapshotSummary = "API integration test role."
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

    public static async Task<ApiTestHost> CreateAsync(bool jwtEnabled)
    {
        var testEnvironment = CanDoItAllTestEnvironment.Create("candoitall-api-tests");
        var activeProfile = testEnvironment.CreateManagedSqliteProfile("api-host");
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
