using System.Net;
using System.Net.Http.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.AgentFramework;

// Governed process runs are started by process automation inside the host, and the server trusts their process labels
// and reserved metadata. Every HTTP run start must refuse a context that claims that origin or binds host folders.
public sealed class AgentExecutionRunContextBoundaryIntegrationTests
{
    [Theory]
    [InlineData("process-step", "", "", "{}")]
    [InlineData(" Process-Step ", "", "", "{}")]
    [InlineData("manual", "8a0f3c52-4a4e-4d53-9f3b-0c9d2f6a1e27", "", "{}")]
    [InlineData("manual", "", "review-step", "{}")]
    [InlineData("manual", "", "", """{"agentExternalTargetRootBindings":[]}""")]
    [InlineData("manual", "", "", """{"AgentExternalTargetRootBindings":[{"rootId":"r1"}]}""")]
    public async Task Http_run_starts_reject_process_claims_and_host_folder_bindings_before_any_run(
        string sourceKind,
        string processRunId,
        string processStepId,
        string metadataJson)
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, useInMemoryDatabase: true);
        var agentId = Guid.NewGuid();
        var context = new ExecutionInvocationContext(
            SourceKind: sourceKind,
            SourceId: "boundary-test",
            CorrelationId: "boundary-correlation",
            CausationId: string.Empty,
            RequestedBy: "integration-test",
            RequestedByKind: "system",
            MetadataJson: metadataJson,
            ProcessRunId: processRunId,
            ProcessStepId: processStepId);

        foreach (var (route, body) in new (string Route, object Body)[]
                 {
                     ("/api/agents/execution-runs", new { agentId, prompt = "Summarize the notes.", context }),
                     ($"/api/agents/{agentId:D}/execution-runs", new { prompt = "Summarize the notes.", context }),
                     ("/api/agents/execution-runs/stream", new { agentId, prompt = "Summarize the notes.", context }),
                     ($"/api/agents/{agentId:D}/execution-runs/stream", new { prompt = "Summarize the notes.", context })
                 })
        {
            using var response = await host.Client.PostAsJsonAsync(route, body);
            var responseBody = await response.Content.ReadAsStringAsync();

            Assert.True(HttpStatusCode.BadRequest == response.StatusCode, $"{route}: {(int)response.StatusCode} {responseBody}");
            Assert.Contains("agents.request-invalid", responseBody, StringComparison.Ordinal);
        }

        await using var scope = host.App.Services.CreateAsyncScope();
        var runs = await scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>()
            .ListExecutionRunsAsync(new ExecutionRunQuery(AgentId: agentId));
        Assert.Empty(runs);
    }
}
