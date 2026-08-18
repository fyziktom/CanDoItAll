using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Processes.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessApiIntegrationTests
{
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
