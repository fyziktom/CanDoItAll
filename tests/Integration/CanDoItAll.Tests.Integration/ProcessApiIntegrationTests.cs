using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessApiIntegrationTests
{
    private const string SelectedNodeTitle = "Saved process variable source";
    private const string NodeTitleVariable = "ProjectNodeTitle";
    private const string CallerVariable = "CallerInput";
    private const string ContributedVariable = "ContributedInput";

    [Fact]
    public async Task Project_scoped_variables_use_owner_reads_without_resolving_launch_execution() {
        var contributor = new RecordingLaunchContributor();
        await using var host = await CreateVariablePreparationHostAsync(contributor);
        await using var scope = host.App.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var source = await CreateVariableSourceAsync(services);
        var input = new Dictionary<string, string> {
            [NodeTitleVariable] = "Caller-supplied title",
            [CallerVariable] = "Retained input"
        };
        var before = input.ToArray();

        var result = await services.GetRequiredService<ProjectStructureProcessNodeService>()
            .BuildProjectScopedLaunchVariablesAsync(new(source.ProjectId, source.NodeId, null, null, "Process API test", input),
                services.GetRequiredService<IProcessLaunchVariablePreparer>());

        Assert.Equal(SelectedNodeTitle, result[NodeTitleVariable]);
        Assert.Equal(input[CallerVariable], result[CallerVariable]);
        Assert.Equal("Owner contribution", result[ContributedVariable]);
        Assert.Equal(before, input.ToArray());
        Assert.Equal(1, contributor.Calls);
        Assert.Equal(source.ProjectId, contributor.Context!.Source.ProjectId);
        Assert.Equal(source.NodeId, contributor.Context.Source.SelectedItem.Id);
    }

    [Fact]
    public async Task Project_scoped_variables_reject_missing_nodes_before_contributors() {
        var contributor = new RecordingLaunchContributor();
        await using var host = await CreateVariablePreparationHostAsync(contributor);
        await using var scope = host.App.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var source = await CreateVariableSourceAsync(services);

        var failure = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            services.GetRequiredService<ProjectStructureProcessNodeService>()
                .BuildProjectScopedLaunchVariablesAsync(new(source.ProjectId, "missing-node", null, null,
                    "Process API test", new Dictionary<string, string>()), services.GetRequiredService<IProcessLaunchVariablePreparer>()));

        Assert.Equal(404, failure.StatusCode);
        Assert.Equal("ProjectStructureNodeNotFound", failure.ErrorCode);
        Assert.Equal(0, contributor.Calls);
    }

    [Fact]
    public async Task Project_scoped_variables_preserve_contributor_failures() {
        var expected = new InvalidOperationException("The selected contributor failed.");
        var contributor = new RecordingLaunchContributor(expected);
        await using var host = await CreateVariablePreparationHostAsync(contributor);
        await using var scope = host.App.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var source = await CreateVariableSourceAsync(services);
        var input = new Dictionary<string, string> { [CallerVariable] = "Retained input" };
        var before = input.ToArray();

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            services.GetRequiredService<ProjectStructureProcessNodeService>()
                .BuildProjectScopedLaunchVariablesAsync(new(source.ProjectId, source.NodeId, null, null, "Process API test", input),
                    services.GetRequiredService<IProcessLaunchVariablePreparer>()));

        Assert.Same(expected, failure);
        Assert.Equal(1, contributor.Calls);
        Assert.Equal(before, input.ToArray());
    }

    private static Task<ApiTestHost> CreateVariablePreparationHostAsync(IProcessLaunchVariableContributor contributor) =>
        ApiTestHost.CreateAsync(jwtEnabled: false, services => {
            services.RemoveAll<ProcessLaunchApplicationService>();
            services.AddScoped<ProcessLaunchApplicationService>(_ =>
                throw new InvalidOperationException("Variable preparation must not resolve launch execution."));
            services.AddSingleton(contributor);
        });

    private static async Task<(Guid ProjectId, string NodeId)> CreateVariableSourceAsync(IServiceProvider services) {
        var created = await services.GetRequiredService<ProjectsService>().SaveAsync(new ProjectEditorModel {
            Name = "Scoped variable owner reads",
            Description = "A persisted source for process variable preparation.",
            Objective = "Prepare variables without resolving execution.",
            CurrentPhase = "Validation"
        });
        Assert.True(created.IsSuccess);
        var node = await services.GetRequiredService<ProjectWorkbenchService>().CreateObjectAsync(created.Value,
            new ProjectObjectCreateRequest(ProjectObjectType.ProjectBlock, SelectedNodeTitle, string.Empty,
                string.Empty, $"project:{created.Value:D}"));
        return (created.Value, node.Id);
    }

    private sealed class RecordingLaunchContributor(Exception? failure = null) : IProcessLaunchVariableContributor {
        public int Calls { get; private set; }
        public ProcessLaunchPreparationContext? Context { get; private set; }

        public void Enrich(ProcessLaunchPreparationContext context, IDictionary<string, string> variables) {
            Calls++;
            Context = context;
            if (failure is not null) {
                throw failure;
            }
            variables[ContributedVariable] = "Owner contribution";
        }
    }

    [Fact]
    public async Task Projection_reads_do_not_require_foreground_catchup_service()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            services =>
            {
                services.RemoveAll<ProcessRuntimeProjectionCatchupService>();
                services.AddScoped<ProcessRuntimeProjectionCatchupService>(_ =>
                    throw new InvalidOperationException(
                        "Projection reads must not resolve foreground catch-up."));
            });
        var missingRunId = Guid.NewGuid();

        using var liveResponse = await host.Client.GetAsync("/api/processes/live");
        using var detailResponse = await host.Client.GetAsync($"/api/processes/runs/{missingRunId:D}");
        using var historyResponse = await host.Client.GetAsync($"/api/processes/runs/{missingRunId:D}/history");

        Assert.True(liveResponse.IsSuccessStatusCode, await liveResponse.Content.ReadAsStringAsync());
        Assert.Equal(System.Net.HttpStatusCode.NotFound, detailResponse.StatusCode);
        Assert.True(historyResponse.IsSuccessStatusCode, await historyResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Contract_lists_launch_check_and_launch_check_does_not_create_run()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);

        using var contract = JsonDocument.Parse(await host.Client.GetStringAsync("/api/processes/contract"));
        var endpoints = contract.RootElement
            .GetProperty("endpoints")
            .EnumerateArray()
            .Select(endpoint => endpoint.GetString())
            .ToArray();

        Assert.Contains("GET /api/processes/contract", endpoints);
        Assert.Contains("POST /api/processes/launch/check", endpoints);
        Assert.Contains("POST /api/processes/launch", endpoints);
        Assert.Contains("GET /api/processes/runs", endpoints);
        Assert.Contains("GET /api/processes/runs/analytics", endpoints);
        Assert.Contains("GET /api/processes/runs/{runId}/summary", endpoints);
        Assert.Contains("GET /api/processes/runs/{runId}/graph", endpoints);

        var checkResponse = await host.Client.PostAsJsonAsync(
            "/api/processes/launch/check",
            new
            {
                definitionKey = "business-plan-development",
                requestedBy = "process-api-test",
                runReadiness = true,
                execute = true
            });
        var checkBody = await checkResponse.Content.ReadAsStringAsync();
        Assert.True(checkResponse.IsSuccessStatusCode, checkBody);

        using var checkResult = JsonDocument.Parse(checkBody);
        Assert.Equal(JsonValueKind.Null, checkResult.RootElement.GetProperty("runId").ValueKind);
        Assert.Equal(
            "business-plan-development",
            checkResult.RootElement.GetProperty("launchPlan").GetProperty("definitionKey").GetString());
        var stage = checkResult.RootElement.GetProperty("stage").GetString() ?? string.Empty;
        Assert.Contains(stage, new[] { "Planned", "Blocked" });

        using var liveProcesses = JsonDocument.Parse(await host.Client.GetStringAsync("/api/processes/live"));
        Assert.Empty(liveProcesses.RootElement.GetProperty("runs").EnumerateArray());
    }
}
