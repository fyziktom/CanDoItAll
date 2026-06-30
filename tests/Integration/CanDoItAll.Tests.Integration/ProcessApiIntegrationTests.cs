using System.Net.Http.Json;
using System.Text.Json;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessApiIntegrationTests
{
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
