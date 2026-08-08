using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Api;
using Microsoft.Extensions.DependencyInjection;

using CanDoItAll.AgentFramework.Runtime.Abstractions;
namespace CanDoItAll.Tests.Integration;

public sealed class AgentApiPublicProjectionIntegrationTests
{
    private static readonly string[] ForbiddenPropertyNames =
    [
        "runtimeSessionKey",
        "serializedSessionStateJson",
        "compatibility",
        "providerRequestId",
        "providerResponseId",
        "argumentsJson",
        "details",
        "callId",
        "rawUsageJson",
        "diagnosticsJson",
        "entryAgentRequestCompatibilityEvidence",
        "failureProviderProfileId",
        "workingDirectory",
        "runtimeToolProviderKey",
        "instructions",
        "configurationJson",
        "permissions",
        "capabilities",
        "tags",
        "providerProfileId",
        "tokenEstimate",
        "sourceKind",
        "sourceId",
        "correlationId",
        "causationId",
        "requestedBy",
        "requestedByKind",
        "metadataJson",
        "inputSummary",
        "resultSummary",
        "processRunId",
        "processStepId",
        "schedulerRunId",
        "messageId",
        "structuredOutputContractKey",
        "structuredOutputTypeName",
        "structuredOutputSchemaName",
        "structuredOutputSchemaDescription",
        "structuredOutputJsonSchema",
        "structuredOutputSchemaHash",
        "structuredOutputSchemaVersion",
        "structuredOutputSchemaStrict",
        "structuredOutputRawOutput",
        "structuredOutputValidationStatus",
        "structuredOutputValidationErrorsJson",
        "failureProviderName",
        "failureModel",
        "requestSummary",
        "exitSummary",
        "decisionSourceId",
        "decisionNotes",
        "usageObservations",
        "workflowSessionId",
        "workflowCheckpointId",
        "pendingApprovalIds",
        "traceId",
        "spanId",
        "context"
    ];

    [Fact]
    public async Task Public_agent_read_endpoints_project_runtime_state_to_safe_explicit_contracts()
    {
        var runtime = new DisclosureSentinelAgentRuntime();
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true,
            agentRuntimeOverride: runtime);

        Guid agentId;
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var workspaceService = scope.ServiceProvider
                .GetRequiredService<IAgentFrameworkWorkspaceService>();
            agentId = (await workspaceService.ListAgentsAsync(includeTemplates: false))
                .First(agent => agent.ProviderProfileId.HasValue)
                .Id;
        }

        var chatSessionId = await CreateChatSessionAsync(host.Client, agentId);
        var executionRunId = await StartPendingExecutionRunAsync(
            host.Client,
            agentId,
            chatSessionId);
        await SeedExcludedRunEvidenceAsync(host, executionRunId, chatSessionId);

        var endpointPayloads = new List<(string Family, JsonDocument Payload)>();
        try
        {
            endpointPayloads.Add((
                "bootstrap",
                await GetJsonAsync(host.Client, "/api/agents/bootstrap")));
            endpointPayloads.Add((
                "chat-workspace",
                await GetJsonAsync(
                    host.Client,
                    $"/api/agents/{agentId:D}/chat-workspace?preferredSessionId={chatSessionId:D}")));
            endpointPayloads.Add((
                "chat-sessions",
                await GetJsonAsync(
                    host.Client,
                    $"/api/agents/{agentId:D}/chat-sessions")));
            endpointPayloads.Add((
                "chat-session-create",
                await PostJsonAsync(
                    host.Client,
                    $"/api/agents/{agentId:D}/chat-sessions?chatSessionId={chatSessionId:D}",
                    content: null)));
            endpointPayloads.Add((
                "chat-session-rename",
                await PostJsonAsync(
                    host.Client,
                    $"/api/agents/{agentId:D}/chat-sessions/{chatSessionId:D}/rename",
                    JsonContent.Create(new ChatSessionRenameApiRequest("Public projection chat")))));
            endpointPayloads.Add((
                "execution-run-list",
                await GetJsonAsync(host.Client, "/api/agents/execution-runs")));
            endpointPayloads.Add((
                "scoped-execution-run-list",
                await GetJsonAsync(
                    host.Client,
                    $"/api/agents/{agentId:D}/execution-runs")));
            endpointPayloads.Add((
                "execution-run-detail",
                await GetJsonAsync(
                    host.Client,
                    $"/api/agents/execution-runs/{executionRunId:D}")));
            endpointPayloads.Add((
                "scoped-execution-run-detail",
                await GetJsonAsync(
                    host.Client,
                    $"/api/agents/{agentId:D}/execution-runs/{executionRunId:D}")));
            endpointPayloads.Add((
                "execution-approvals",
                await GetJsonAsync(
                    host.Client,
                    $"/api/agents/execution-runs/{executionRunId:D}/approvals")));
            endpointPayloads.Add((
                "scoped-execution-approvals",
                await GetJsonAsync(
                    host.Client,
                    $"/api/agents/{agentId:D}/execution-runs/{executionRunId:D}/approvals")));
            endpointPayloads.Add((
                "execution-tool-receipts",
                await GetJsonAsync(
                    host.Client,
                    $"/api/agents/execution-runs/{executionRunId:D}/tool-receipts")));
            endpointPayloads.Add((
                "scoped-execution-tool-receipts",
                await GetJsonAsync(
                    host.Client,
                    $"/api/agents/{agentId:D}/execution-runs/{executionRunId:D}/tool-receipts")));
            endpointPayloads.Add((
                "execution-artifacts",
                await GetJsonAsync(
                    host.Client,
                    $"/api/agents/execution-runs/{executionRunId:D}/artifacts")));
            endpointPayloads.Add((
                "scoped-execution-artifacts",
                await GetJsonAsync(
                    host.Client,
                    $"/api/agents/{agentId:D}/execution-runs/{executionRunId:D}/artifacts")));
            endpointPayloads.Add((
                "execution-checkpoints",
                await GetJsonAsync(
                    host.Client,
                    $"/api/agents/execution-runs/{executionRunId:D}/checkpoints")));
            endpointPayloads.Add((
                "scoped-execution-checkpoints",
                await GetJsonAsync(
                    host.Client,
                    $"/api/agents/{agentId:D}/execution-runs/{executionRunId:D}/checkpoints")));
            endpointPayloads.Add((
                "scoped-execution-log",
                await GetJsonAsync(
                    host.Client,
                    $"/api/agents/{agentId:D}/execution-runs/{executionRunId:D}/log")));
            endpointPayloads.Add((
                "scoped-execution-metrics",
                await GetJsonAsync(
                    host.Client,
                    $"/api/agents/{agentId:D}/execution-runs/{executionRunId:D}/metrics")));
            endpointPayloads.Add((
                "agent-execution-log",
                await GetJsonAsync(
                    host.Client,
                    $"/api/agents/{agentId:D}/execution-log?chatSessionId={chatSessionId:D}")));
            endpointPayloads.Add((
                "agent-runtime-snapshot",
                await GetJsonAsync(
                    host.Client,
                    $"/api/agents/{agentId:D}/runtime-snapshot?chatSessionId={chatSessionId:D}")));
            endpointPayloads.Add((
                "agent-metrics",
                await GetJsonAsync(
                    host.Client,
                    $"/api/agents/{agentId:D}/metrics")));

            foreach (var (family, payload) in endpointPayloads)
            {
                AssertNoPrivateRuntimeState(payload.RootElement, family);
            }

            AssertChatSessionProjection(
                endpointPayloads.Single(item => item.Family == "chat-session-rename").Payload.RootElement,
                chatSessionId);
            AssertChatWorkspaceProjection(
                endpointPayloads.Single(item => item.Family == "chat-workspace").Payload.RootElement,
                chatSessionId,
                executionRunId);
            AssertBootstrapProjection(
                endpointPayloads.Single(item => item.Family == "bootstrap").Payload.RootElement,
                agentId,
                executionRunId);
            AssertRunListProjection(
                endpointPayloads.Single(item => item.Family == "execution-run-list").Payload.RootElement,
                executionRunId);
            AssertRunDetailProjection(
                endpointPayloads.Single(item => item.Family == "execution-run-detail").Payload.RootElement,
                executionRunId,
                chatSessionId);
            AssertApprovalProjection(
                endpointPayloads.Single(item => item.Family == "execution-approvals").Payload.RootElement,
                executionRunId);
            AssertToolReceiptProjection(
                endpointPayloads.Single(item => item.Family == "execution-tool-receipts").Payload.RootElement,
                executionRunId);
        }
        finally
        {
            foreach (var (_, payload) in endpointPayloads)
            {
                payload.Dispose();
            }
        }
    }

    [Fact]
    public async Task Public_agent_mutation_and_stream_endpoints_use_the_same_safe_result_projection()
    {
        var runtime = new DisclosureSentinelAgentRuntime();
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true,
            agentRuntimeOverride: runtime);

        Guid agentId;
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var workspaceService = scope.ServiceProvider
                .GetRequiredService<IAgentFrameworkWorkspaceService>();
            agentId = (await workspaceService.ListAgentsAsync(includeTemplates: false))
                .First(agent => agent.ProviderProfileId.HasValue)
                .Id;
        }

        var chatSessionId = await CreateChatSessionAsync(host.Client, agentId);
        using var chatResponse = await host.Client.PostAsJsonAsync(
            $"/api/agents/{agentId:D}/chat",
            new AgentChatApiRequest(
                chatSessionId,
                "Create a pending public chat projection."));
        Assert.Equal(HttpStatusCode.OK, chatResponse.StatusCode);
        using var chatPayload = JsonDocument.Parse(await chatResponse.Content.ReadAsStringAsync());
        AssertNoPrivateRuntimeState(chatPayload.RootElement, "chat-mutation");
        Assert.True(chatPayload.RootElement.TryGetProperty("assistantMessage", out _));
        Assert.True(chatPayload.RootElement.TryGetProperty("metric", out _));
        var pendingChatRunId = chatPayload.RootElement.GetProperty("executionRunId").GetGuid();

        using var approvalResponse = await host.Client.PostAsJsonAsync(
            $"/api/agents/execution-runs/{pendingChatRunId:D}/pending-approvals",
            new PendingApprovalApiRequest(
                Approved: true,
                AutoApprovePendingToolCalls: false));
        Assert.Equal(HttpStatusCode.OK, approvalResponse.StatusCode);
        using var approvalPayload = JsonDocument.Parse(await approvalResponse.Content.ReadAsStringAsync());
        AssertNoPrivateRuntimeState(approvalPayload.RootElement, "approval-mutation");
        Assert.Equal(
            (int)ExecutionState.Completed,
            approvalPayload.RootElement.GetProperty("state").GetInt32());
        Assert.Equal(
            DisclosureSentinelAgentRuntime.PublicContinuationResponse,
            approvalPayload.RootElement.GetProperty("responseText").GetString());

        var executionStreamPayload = await PostServerSentEventsAsync(
            host.Client,
            "/api/agents/execution-runs/stream",
            new AgentExecutionRunApiRequest(
                agentId,
                "Create a streamed public run projection."));
        var streamedRunId = executionStreamPayload
            .GetProperty("result")
            .GetProperty("executionRunId")
            .GetGuid();

        _ = await PostServerSentEventsAsync(
            host.Client,
            $"/api/agents/execution-runs/{streamedRunId:D}/pending-approvals/stream",
            new PendingApprovalApiRequest(
                Approved: true,
                AutoApprovePendingToolCalls: false));

        var streamChatSessionId = await CreateChatSessionAsync(host.Client, agentId);
        _ = await PostServerSentEventsAsync(
            host.Client,
            $"/api/agents/{agentId:D}/chat/stream",
            new AgentChatApiRequest(
                streamChatSessionId,
                "Create a streamed public chat projection."));

        _ = await PostServerSentEventsAsync(
            host.Client,
            $"/api/agents/{agentId:D}/execution-runs/stream",
            new AgentExecutionRunStartApiRequest(
                "Create a streamed scoped public run projection."));
    }

    [Fact]
    public async Task Public_agent_endpoints_advertise_their_projected_response_contracts()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true);
        using var openApi = JsonDocument.Parse(
            await host.Client.GetStringAsync("/openapi/v1.json"));
        var paths = openApi.RootElement.GetProperty("paths");

        var contracts = new (string Path, string Method, string Schema, bool IsArray)[]
        {
            ("/api/agents/bootstrap", "get", "AgentChatPageBootstrapApiResponse", false),
            ("/api/agents/{agentId}/chat-sessions", "get", "AgentChatSessionApiResponse", true),
            ("/api/agents/{agentId}/chat-sessions", "post", "AgentChatSessionApiResponse", false),
            ("/api/agents/{agentId}/chat-sessions/{chatSessionId}/rename", "post", "AgentChatSessionApiResponse", false),
            ("/api/agents/{agentId}/chat-workspace", "get", "AgentChatWorkspaceApiResponse", false),
            ("/api/agents/{agentId}/chat", "post", "AgentChatRunApiResponse", false),
            ("/api/agents/execution-runs/{executionRunId}/pending-approvals", "post", "AgentExecutionRunResultApiResponse", false),
            ("/api/agents/execution-runs", "get", "AgentExecutionRunApiResponse", true),
            ("/api/agents/execution-runs", "post", "AgentExecutionRunResultApiResponse", false),
            ("/api/agents/{agentId}/execution-runs", "get", "AgentExecutionRunApiResponse", true),
            ("/api/agents/{agentId}/execution-runs", "post", "AgentExecutionRunResultApiResponse", false),
            ("/api/agents/execution-runs/{executionRunId}", "get", "AgentExecutionRunDetailApiResponse", false),
            ("/api/agents/{agentId}/execution-runs/{executionRunId}", "get", "AgentExecutionRunDetailApiResponse", false),
            ("/api/agents/execution-runs/{executionRunId}/artifacts", "get", "AgentExecutionArtifactApiResponse", true),
            ("/api/agents/{agentId}/execution-runs/{executionRunId}/artifacts", "get", "AgentExecutionArtifactApiResponse", true),
            ("/api/agents/execution-runs/{executionRunId}/checkpoints", "get", "AgentExecutionCheckpointApiResponse", true),
            ("/api/agents/{agentId}/execution-runs/{executionRunId}/checkpoints", "get", "AgentExecutionCheckpointApiResponse", true),
            ("/api/agents/execution-runs/{executionRunId}/tool-receipts", "get", "AgentExecutionToolReceiptApiResponse", true),
            ("/api/agents/{agentId}/execution-runs/{executionRunId}/tool-receipts", "get", "AgentExecutionToolReceiptApiResponse", true),
            ("/api/agents/{agentId}/execution-runs/{executionRunId}/log", "get", "AgentExecutionLogApiResponse", true),
            ("/api/agents/{agentId}/execution-runs/{executionRunId}/metrics", "get", "AgentRunMetricApiResponse", true),
            ("/api/agents/execution-runs/{executionRunId}/approvals", "get", "AgentExecutionApprovalApiResponse", true),
            ("/api/agents/{agentId}/execution-runs/{executionRunId}/approvals", "get", "AgentExecutionApprovalApiResponse", true),
            ("/api/agents/{agentId}/execution-log", "get", "AgentExecutionLogApiResponse", true),
            ("/api/agents/{agentId}/runtime-snapshot", "get", "AgentChatRuntimeApiResponse", false),
            ("/api/agents/{agentId}/metrics", "get", "AgentRunMetricApiResponse", true)
        };

        foreach (var contract in contracts)
        {
            AssertOpenApiResponseSchema(paths, contract);
        }
    }

    [Fact]
    public async Task Public_execution_result_preserves_validated_data_without_raw_output_or_schema_internals()
    {
        var runtime = new DisclosureSentinelAgentRuntime(completeInitialRun: true);
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true,
            agentRuntimeOverride: runtime);

        Guid agentId;
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var workspaceService = scope.ServiceProvider
                .GetRequiredService<IAgentFrameworkWorkspaceService>();
            agentId = (await workspaceService.ListAgentsAsync(includeTemplates: false))
                .First(agent => agent.ProviderProfileId.HasValue)
                .Id;
        }

        using var schema = JsonDocument.Parse("""
            {
              "type": "object",
              "properties": {
                "value": { "type": "string" }
              },
              "required": ["value"],
              "additionalProperties": false
            }
            """);
        using var response = await host.Client.PostAsJsonAsync(
            $"/api/agents/{agentId:D}/execution-runs",
            new AgentExecutionRunStartApiRequest(
                "Return validated public structured data.",
                StructuredOutput: new AgentJsonSchemaOutputContract(
                    AgentJsonSchemaOutputContractVersions.Kind,
                    AgentJsonSchemaOutputContractVersions.Current,
                    "public_projection",
                    schema.RootElement.Clone())));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        AssertNoPrivateRuntimeState(payload.RootElement, "structured-output-result");
        var structuredOutput = payload.RootElement.GetProperty("structuredOutput");
        Assert.Equal(
            "public-data",
            structuredOutput.GetProperty("data").GetProperty("value").GetString());
        Assert.Equal(
            nameof(AgentJsonSchemaOutputValidationStatus.Valid),
            structuredOutput.GetProperty("validationStatus").GetString());
        Assert.Empty(structuredOutput.GetProperty("validationErrors").EnumerateArray());
        Assert.False(structuredOutput.TryGetProperty("rawOutput", out _));
        Assert.False(structuredOutput.TryGetProperty("schema", out _));
        Assert.False(structuredOutput.TryGetProperty("schemaHash", out _));

        var validationErrors = Enumerable.Range(0, 25)
            .Select(index => new AgentJsonSchemaOutputValidationError(
                $"validation-{index}",
                $"Public validation message {index}.",
                $"$.items[{index}]"))
            .ToArray();
        using var data = JsonDocument.Parse("""{"value":"public-data-with-errors"}""");
        var persistedResult = new ExecutionRunResult(
            Guid.NewGuid(),
            ChatSessionId: null,
            ResponseText: "Structured validation failed.",
            AssistantMessage: null,
            Metric: new AgentRunMetric(
                Guid.NewGuid(),
                agentId,
                ChatSessionId: null,
                DateTimeOffset.UtcNow,
                RunOutcome.Failed,
                "Public provider",
                "public-model",
                DurationMs: 10,
                InputTokens: 1,
                OutputTokens: 1,
                ToolCalls: 0))
        {
            State = ExecutionState.Failed,
            StructuredOutput = new AgentJsonSchemaOutputResult(
                data.RootElement.Clone(),
                DisclosureSentinelAgentRuntime.PrivateStructuredRawSentinel,
                DisclosureSentinelAgentRuntime.PrivateStructuredSchemaSentinel,
                "private-structured-schema-hash-47fe",
                AgentJsonSchemaOutputValidationStatus.SchemaValidationFailed,
                validationErrors)
        };
        var projected = AgentApiResponseMapper.ToExecutionRunResult(persistedResult);
        using var projectedPayload = JsonDocument.Parse(JsonSerializer.Serialize(
            projected,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        AssertNoPrivateRuntimeState(projectedPayload.RootElement, "structured-output-validation-result");
        var projectedOutput = projectedPayload.RootElement.GetProperty("structuredOutput");
        Assert.Equal(
            "public-data-with-errors",
            projectedOutput.GetProperty("data").GetProperty("value").GetString());
        Assert.Equal(
            nameof(AgentJsonSchemaOutputValidationStatus.SchemaValidationFailed),
            projectedOutput.GetProperty("validationStatus").GetString());
        var projectedErrors = projectedOutput.GetProperty("validationErrors").EnumerateArray().ToArray();
        Assert.Equal(20, projectedErrors.Length);
        Assert.Equal("validation-0", projectedErrors[0].GetProperty("code").GetString());
        Assert.Equal("validation-19", projectedErrors[^1].GetProperty("code").GetString());
        Assert.DoesNotContain("validation-20", projectedOutput.GetRawText(), StringComparison.Ordinal);
    }

    private static async Task<Guid> CreateChatSessionAsync(HttpClient client, Guid agentId)
    {
        using var payload = await PostJsonAsync(
            client,
            $"/api/agents/{agentId:D}/chat-sessions",
            content: null);
        return payload.RootElement.GetProperty("id").GetGuid();
    }

    private static void AssertOpenApiResponseSchema(
        JsonElement paths,
        (string Path, string Method, string Schema, bool IsArray) contract)
    {
        var schema = paths
            .GetProperty(contract.Path)
            .GetProperty(contract.Method)
            .GetProperty("responses")
            .GetProperty("200")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");
        if (contract.IsArray)
        {
            Assert.Equal("array", schema.GetProperty("type").GetString());
            schema = schema.GetProperty("items");
        }

        Assert.Equal(
            $"#/components/schemas/{contract.Schema}",
            schema.GetProperty("$ref").GetString());
    }

    private static async Task<Guid> StartPendingExecutionRunAsync(
        HttpClient client,
        Guid agentId,
        Guid chatSessionId)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/agents/{agentId:D}/execution-runs",
            new AgentExecutionRunStartApiRequest(
                "Create a public API projection regression run.",
                ChatSessionId: chatSessionId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        AssertNoPrivateRuntimeState(payload.RootElement, "execution-run-start");
        Assert.Equal(
            (int)ExecutionState.WaitingOnTool,
            payload.RootElement.GetProperty("state").GetInt32());
        return payload.RootElement.GetProperty("executionRunId").GetGuid();
    }

    private static async Task SeedExcludedRunEvidenceAsync(
        ApiTestHost host,
        Guid executionRunId,
        Guid chatSessionId)
    {
        await using var scope = host.App.Services.CreateAsyncScope();
        var store = scope.ServiceProvider
            .GetRequiredService<ISandboxWorkspaceStore>();
        var chatQueryStore = Assert.IsAssignableFrom<ISandboxWorkspaceChatQueryStore>(store);
        var chatSessionStore = Assert.IsAssignableFrom<ISandboxWorkspaceChatSessionStore>(store);
        var session = await chatQueryStore.GetChatSessionAsync(chatSessionId);
        Assert.NotNull(session);
        await chatSessionStore.UpdateChatSessionAsync(session with
        {
            Compatibility = new ChatSessionRuntimeCompatibilityRecord(
                DisclosureSentinelAgentRuntime.RuntimeSessionKeySentinel,
                $$"""{"sentinel":"{{DisclosureSentinelAgentRuntime.SerializedSessionSentinel}}"}""",
                [DisclosureSentinelAgentRuntime.CreatePendingApproval()],
                autoApprovePendingToolCalls: false)
        });
        var mutationStore = Assert.IsAssignableFrom<ISandboxWorkspaceExecutionRunMutationStore>(store);
        await mutationStore.UpdateExecutionRunDetailAsync(
            executionRunId,
            detail => detail with
            {
                Run = detail.Run with
                {
                    SourceKind = DisclosureSentinelAgentRuntime.PrivateSourceKindSentinel,
                    SourceId = DisclosureSentinelAgentRuntime.PrivateSourceIdSentinel,
                    CorrelationId = DisclosureSentinelAgentRuntime.PrivateCorrelationSentinel,
                    CausationId = DisclosureSentinelAgentRuntime.PrivateCausationSentinel,
                    RequestedBy = DisclosureSentinelAgentRuntime.PrivateRequestedBySentinel,
                    RequestedByKind = DisclosureSentinelAgentRuntime.PrivateRequestedByKindSentinel,
                    MetadataJson = $$"""{"sentinel":"{{DisclosureSentinelAgentRuntime.PrivateMetadataSentinel}}"}""",
                    InputSummary = DisclosureSentinelAgentRuntime.PrivateInputSummarySentinel,
                    ResultSummary = DisclosureSentinelAgentRuntime.PrivateResultSummarySentinel,
                    ProcessRunId = DisclosureSentinelAgentRuntime.PrivateProcessRunSentinel,
                    ProcessStepId = DisclosureSentinelAgentRuntime.PrivateProcessStepSentinel,
                    SchedulerRunId = DisclosureSentinelAgentRuntime.PrivateSchedulerRunSentinel,
                    MessageId = DisclosureSentinelAgentRuntime.PrivateMessageSentinel,
                    StructuredOutputJsonSchema = DisclosureSentinelAgentRuntime.PrivateStructuredSchemaSentinel,
                    StructuredOutputRawOutput = DisclosureSentinelAgentRuntime.PrivateStructuredRawSentinel,
                    StructuredOutputValidationErrorsJson = DisclosureSentinelAgentRuntime.PrivateStructuredErrorsSentinel,
                    FailureProviderName = DisclosureSentinelAgentRuntime.PrivateFailureProviderSentinel,
                    FailureModel = DisclosureSentinelAgentRuntime.PrivateFailureModelSentinel
                },
                Approvals = detail.Approvals.Select(approval => approval with
                {
                    DecisionSourceId = DisclosureSentinelAgentRuntime.PrivateDecisionSourceSentinel,
                    DecisionNotes = DisclosureSentinelAgentRuntime.PrivateDecisionNotesSentinel
                }).ToArray(),
                Artifacts =
                [
                    new ExecutionArtifactRecord(
                        Guid.NewGuid(),
                        executionRunId,
                        "spreadsheet",
                        "Garden calculations",
                        "artifacts/garden-calculations.xlsx",
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        "Gardener",
                        "Validated workbook",
                        DateTimeOffset.UtcNow)
                ],
                Checkpoints =
                [
                    new ExecutionWorkflowCheckpointRecord(
                        Guid.NewGuid(),
                        executionRunId,
                        DisclosureSentinelAgentRuntime.PrivateWorkflowSessionSentinel,
                        DisclosureSentinelAgentRuntime.PrivateWorkflowCheckpointSentinel,
                        "approval",
                        ExecutionState.WaitingOnTool,
                        [DisclosureSentinelAgentRuntime.PrivateCheckpointApprovalSentinel],
                        DateTimeOffset.UtcNow,
                        null,
                        DisclosureSentinelAgentRuntime.PrivateCorrelationSentinel,
                        DisclosureSentinelAgentRuntime.PrivateSourceKindSentinel,
                        DisclosureSentinelAgentRuntime.PrivateSourceIdSentinel,
                        DisclosureSentinelAgentRuntime.PrivateProcessRunSentinel,
                        DisclosureSentinelAgentRuntime.PrivateProcessStepSentinel,
                        DisclosureSentinelAgentRuntime.PrivateSchedulerRunSentinel,
                        DisclosureSentinelAgentRuntime.PrivateMessageSentinel,
                        DisclosureSentinelAgentRuntime.PrivateTraceSentinel,
                        DisclosureSentinelAgentRuntime.PrivateSpanSentinel)
                ],
                ToolReceipts = detail.ToolReceipts.Select(receipt => receipt with
                {
                    WorkingDirectory = DisclosureSentinelAgentRuntime.PrivateWorkingDirectorySentinel,
                    RequestSummary = DisclosureSentinelAgentRuntime.PrivateRequestSummarySentinel,
                    ExitSummary = DisclosureSentinelAgentRuntime.PrivateExitSummarySentinel,
                    RuntimeToolProviderKey = DisclosureSentinelAgentRuntime.PrivateRuntimeToolProviderKeySentinel
                }).ToArray()
            });
    }

    private static async Task<JsonDocument> GetJsonAsync(HttpClient client, string requestUri)
    {
        using var response = await client.GetAsync(requestUri);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static async Task<JsonDocument> PostJsonAsync(
        HttpClient client,
        string requestUri,
        HttpContent? content)
    {
        using var response = await client.PostAsync(requestUri, content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static async Task<JsonElement> PostServerSentEventsAsync<TRequest>(
        HttpClient client,
        string requestUri,
        TRequest request)
    {
        using var response = await client.PostAsJsonAsync(requestUri, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        foreach (var sentinel in DisclosureSentinelAgentRuntime.PrivateSentinels)
        {
            Assert.DoesNotContain(sentinel, raw, StringComparison.Ordinal);
        }

        JsonElement? completed = null;
        var activityCount = 0;
        foreach (var frame in raw.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var lines = frame
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(static line => line.TrimEnd('\r'))
                .ToArray();
            var eventName = lines
                .FirstOrDefault(static line => line.StartsWith("event: ", StringComparison.Ordinal))
                ?["event: ".Length..];
            var data = lines
                .FirstOrDefault(static line => line.StartsWith("data: ", StringComparison.Ordinal))
                ?["data: ".Length..];
            if (string.IsNullOrWhiteSpace(data))
            {
                continue;
            }

            using var document = JsonDocument.Parse(data);
            AssertNoPrivateRuntimeState(document.RootElement, $"sse-{eventName}");
            if (eventName?.StartsWith("agent.activity", StringComparison.Ordinal) == true)
            {
                activityCount++;
                Assert.True(document.RootElement.TryGetProperty("phase", out _));
                Assert.False(document.RootElement.TryGetProperty("activity", out _));
            }

            if (string.Equals(
                    eventName,
                    AgentServerEventNames.CommandCompleted,
                    StringComparison.Ordinal))
            {
                completed = document.RootElement.Clone();
            }
        }

        Assert.True(activityCount > 0);
        Assert.True(completed.HasValue);
        return completed.Value;
    }

    private static void AssertNoPrivateRuntimeState(JsonElement payload, string family)
    {
        var raw = payload.GetRawText();
        foreach (var sentinel in DisclosureSentinelAgentRuntime.PrivateSentinels)
        {
            Assert.DoesNotContain(sentinel, raw, StringComparison.Ordinal);
        }

        AssertNoForbiddenProperties(payload, family);
    }

    private static void AssertNoForbiddenProperties(JsonElement element, string family)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                Assert.DoesNotContain(
                    property.Name,
                    ForbiddenPropertyNames,
                    StringComparer.OrdinalIgnoreCase);
                AssertNoForbiddenProperties(property.Value, family);
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in element.EnumerateArray())
        {
            AssertNoForbiddenProperties(item, family);
        }
    }

    private static void AssertChatSessionProjection(JsonElement session, Guid chatSessionId)
    {
        Assert.Equal(chatSessionId, session.GetProperty("id").GetGuid());
        Assert.Equal("Public projection chat", session.GetProperty("title").GetString());
        var approval = Assert.Single(session.GetProperty("pendingApprovals").EnumerateArray());
        Assert.Equal(
            DisclosureSentinelAgentRuntime.PublicApprovalId,
            approval.GetProperty("approvalId").GetString());
        Assert.Equal(
            DisclosureSentinelAgentRuntime.PublicToolName,
            approval.GetProperty("toolName").GetString());
    }

    private static void AssertChatWorkspaceProjection(
        JsonElement workspace,
        Guid chatSessionId,
        Guid executionRunId)
    {
        Assert.Equal(
            chatSessionId,
            workspace.GetProperty("selectedSession").GetProperty("id").GetGuid());
        Assert.Equal(
            executionRunId,
            workspace.GetProperty("selectedRun").GetProperty("id").GetGuid());
        Assert.Equal(
            (int)ExecutionState.WaitingOnTool,
            workspace.GetProperty("selectedRun").GetProperty("state").GetInt32());
    }

    private static void AssertBootstrapProjection(
        JsonElement bootstrap,
        Guid agentId,
        Guid executionRunId)
    {
        Assert.Equal(agentId, bootstrap.GetProperty("initialAgentId").GetGuid());
        Assert.Equal(
            executionRunId,
            bootstrap
                .GetProperty("selectedAgentWorkspace")
                .GetProperty("selectedRun")
                .GetProperty("id")
                .GetGuid());
    }

    private static void AssertRunListProjection(JsonElement runs, Guid executionRunId)
    {
        var run = Assert.Single(
            runs.EnumerateArray(),
            candidate => candidate.GetProperty("id").GetGuid() == executionRunId);
        Assert.Equal(
            (int)ExecutionState.WaitingOnTool,
            run.GetProperty("state").GetInt32());
        Assert.Equal(
            DisclosureSentinelAgentRuntime.PublicToolName,
            Assert.Single(run.GetProperty("pendingApprovals").EnumerateArray())
                .GetProperty("toolName")
                .GetString());
    }

    private static void AssertRunDetailProjection(
        JsonElement detail,
        Guid executionRunId,
        Guid chatSessionId)
    {
        Assert.Equal(executionRunId, detail.GetProperty("run").GetProperty("id").GetGuid());
        Assert.Equal(chatSessionId, detail.GetProperty("chatSession").GetProperty("id").GetGuid());

        var totals = detail.GetProperty("usageTotals");
        Assert.Equal(1, totals.GetProperty("observationCount").GetInt32());
        Assert.Equal(17, totals.GetProperty("inputTokens").GetInt32());
        Assert.Equal(6, totals.GetProperty("outputTokens").GetInt32());
        Assert.Equal(23, totals.GetProperty("totalTokens").GetInt32());
        Assert.Equal(0.125m, totals.GetProperty("knownCostUsd").GetDecimal());
    }

    private static void AssertApprovalProjection(JsonElement approvals, Guid executionRunId)
    {
        var approval = Assert.Single(approvals.EnumerateArray());
        Assert.Equal(executionRunId, approval.GetProperty("executionRunId").GetGuid());
        Assert.Equal(
            (int)ExecutionApprovalStatus.Pending,
            approval.GetProperty("status").GetInt32());
        Assert.Equal(
            DisclosureSentinelAgentRuntime.PublicToolName,
            approval.GetProperty("toolName").GetString());
    }

    private static void AssertToolReceiptProjection(JsonElement receipts, Guid executionRunId)
    {
        var receipt = Assert.Single(receipts.EnumerateArray());
        Assert.Equal(executionRunId, receipt.GetProperty("executionRunId").GetGuid());
        Assert.Equal(
            DisclosureSentinelAgentRuntime.PublicToolName,
            receipt.GetProperty("toolName").GetString());
        Assert.Equal(
            DisclosureSentinelAgentRuntime.PublicRuntimeToolProviderName,
            receipt.GetProperty("runtimeToolProviderName").GetString());
    }

    private sealed class DisclosureSentinelAgentRuntime : IFakeAgentRuntime
    {
        private readonly bool completeInitialRun;

        public DisclosureSentinelAgentRuntime(bool completeInitialRun = false)
        {
            this.completeInitialRun = completeInitialRun;
        }

        public const string PublicApprovalId = "approval-public-projection";
        public const string PublicToolName = "workspace_write_spreadsheet";
        public const string PublicRuntimeToolProviderName = "Workspace tools";
        public const string PublicContinuationResponse = "The approved operation completed.";

        public const string RuntimeSessionKeySentinel = "private-runtime-session-key-97ef";
        public const string SerializedSessionSentinel = "private-serialized-session-state-63c1";
        private const string ProviderConversationSentinel = "private-provider-conversation-id-281d";
        private const string ToolResultSentinel = "private-tool-result-id-4af9";
        private const string ProviderRequestSentinel = "private-provider-request-id-408e";
        private const string ProviderResponseSentinel = "private-provider-response-id-2d32";
        private const string UsageRuntimeSessionSentinel = "private-usage-runtime-session-key-ab10";
        private const string ApprovalCallSentinel = "private-approval-call-id-31c6";
        private const string ApprovalDetailsSentinel = "private-approval-details-276a";
        private const string ApprovalArgumentsSentinel = "private-approval-arguments-a21c";
        private const string RawUsageSentinel = "private-raw-usage-6b8a";
        private const string DiagnosticsSentinel = "private-usage-diagnostics-80db";
        private const string ProviderCompatibilitySentinel = "private-provider-compatibility-model-6d2f";
        public const string PrivateRuntimeToolProviderKeySentinel = "private-runtime-tool-provider-key-e913";
        public const string PrivateSourceKindSentinel = "private-source-kind-04bf";
        public const string PrivateSourceIdSentinel = "private-source-id-e7ad";
        public const string PrivateCorrelationSentinel = "private-correlation-id-32d1";
        public const string PrivateCausationSentinel = "private-causation-id-c8af";
        public const string PrivateRequestedBySentinel = "private-requested-by-d49a";
        public const string PrivateRequestedByKindSentinel = "private-requested-by-kind-c074";
        public const string PrivateMetadataSentinel = "private-metadata-json-847d";
        public const string PrivateInputSummarySentinel = "private-input-summary-5326";
        public const string PrivateResultSummarySentinel = "private-result-summary-8fae";
        public const string PrivateProcessRunSentinel = "private-process-run-id-488e";
        public const string PrivateProcessStepSentinel = "private-process-step-id-945f";
        public const string PrivateSchedulerRunSentinel = "private-scheduler-run-id-028d";
        public const string PrivateMessageSentinel = "private-message-id-f9cf";
        public const string PrivateStructuredSchemaSentinel = "private-structured-schema-ee5e";
        public const string PrivateStructuredRawSentinel = "private-structured-raw-66a5";
        public const string PrivateStructuredErrorsSentinel = "private-structured-errors-1e17";
        public const string PrivateFailureProviderSentinel = "private-failure-provider-fb3a";
        public const string PrivateFailureModelSentinel = "private-failure-model-d1df";
        public const string PrivateDecisionSourceSentinel = "private-decision-source-id-678a";
        public const string PrivateDecisionNotesSentinel = "private-decision-notes-0b3e";
        public const string PrivateWorkflowSessionSentinel = "private-workflow-session-id-93db";
        public const string PrivateWorkflowCheckpointSentinel = "private-workflow-checkpoint-id-54c8";
        public const string PrivateCheckpointApprovalSentinel = "private-checkpoint-approval-id-923d";
        public const string PrivateTraceSentinel = "private-trace-id-6f15";
        public const string PrivateSpanSentinel = "private-span-id-ce46";
        public const string PrivateWorkingDirectorySentinel = "private-working-directory-b967";
        public const string PrivateRequestSummarySentinel = "private-tool-request-summary-1be7";
        public const string PrivateExitSummarySentinel = "private-tool-exit-summary-66dd";

        public static IReadOnlyList<string> PrivateSentinels { get; } =
        [
            RuntimeSessionKeySentinel,
            SerializedSessionSentinel,
            ProviderConversationSentinel,
            ToolResultSentinel,
            ProviderRequestSentinel,
            ProviderResponseSentinel,
            UsageRuntimeSessionSentinel,
            ApprovalCallSentinel,
            ApprovalDetailsSentinel,
            ApprovalArgumentsSentinel,
            RawUsageSentinel,
            DiagnosticsSentinel,
            ProviderCompatibilitySentinel,
            PrivateRuntimeToolProviderKeySentinel,
            PrivateSourceKindSentinel,
            PrivateSourceIdSentinel,
            PrivateCorrelationSentinel,
            PrivateCausationSentinel,
            PrivateRequestedBySentinel,
            PrivateRequestedByKindSentinel,
            PrivateMetadataSentinel,
            PrivateInputSummarySentinel,
            PrivateResultSummarySentinel,
            PrivateProcessRunSentinel,
            PrivateProcessStepSentinel,
            PrivateSchedulerRunSentinel,
            PrivateMessageSentinel,
            PrivateStructuredSchemaSentinel,
            PrivateStructuredRawSentinel,
            PrivateStructuredErrorsSentinel,
            PrivateFailureProviderSentinel,
            PrivateFailureModelSentinel,
            PrivateDecisionSourceSentinel,
            PrivateDecisionNotesSentinel,
            PrivateWorkflowSessionSentinel,
            PrivateWorkflowCheckpointSentinel,
            PrivateCheckpointApprovalSentinel,
            PrivateTraceSentinel,
            PrivateSpanSentinel,
            PrivateWorkingDirectorySentinel,
            PrivateRequestSummarySentinel,
            PrivateExitSummarySentinel
        ];

        public static PendingToolApprovalRecord CreatePendingApproval()
        {
            return new PendingToolApprovalRecord(
                PublicApprovalId,
                ApprovalCallSentinel,
                PublicToolName,
                "function",
                ApprovalDetailsSentinel,
                $$"""{"sentinel":"{{ApprovalArgumentsSentinel}}"}""");
        }

        public Task<ProviderHealthResult> TestProviderAsync(
            ProviderProfile provider,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderHealthResult(true, "ok", []));
        }

        public Task<ProviderTestChatResult> RunProviderTestChatAsync(
            ProviderProfile provider,
            ProviderTestChatRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderTestChatResult(provider.DefaultModel, "ok", 1, 1));
        }

        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
            ProviderProfile provider,
            ProviderModelMaintenanceEditorRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<AgentRuntimeResponse> RunAsync(
            AgentDefinition agent,
            ProviderProfile provider,
            ChatSessionRecord session,
            IReadOnlyList<CapabilityCatalogItem> capabilities,
            IReadOnlyList<AgentMemoryRecord> memory,
            string prompt,
            string? runtimeSessionKey,
            Func<ExecutionState, string, string, Task> progressCallback,
            CancellationToken cancellationToken = default,
            bool suppressApprovalRequirements = false,
            AgentStructuredOutputContract? structuredOutput = null,
            AgentRuntimeExecutionOptions? executionOptions = null)
        {
            var now = DateTimeOffset.UtcNow;
            var response = new AgentRuntimeResponse(
                ResponseText: completeInitialRun
                    ? """{"value":"public-data"}"""
                    : "Approval is required for the public projection test.",
                InputTokens: 17,
                OutputTokens: 6,
                ToolCalls: completeInitialRun ? 0 : 1,
                RuntimeSessionKey: RuntimeSessionKeySentinel,
                SerializedSessionStateJson: $$"""
                    {
                      "state": "{{SerializedSessionSentinel}}",
                      "providerConversationId": "{{ProviderConversationSentinel}}",
                      "toolResults": [
                        { "id": "{{ToolResultSentinel}}" }
                      ]
                    }
                    """,
                PendingApprovals: completeInitialRun
                    ? []
                    : [CreatePendingApproval()])
            {
                UsageObservations =
                [
                    new ProviderUsageObservation(
                        Guid.NewGuid(),
                        now,
                        provider.Name,
                        provider.Kind,
                        provider.DefaultModel,
                        provider.Transport,
                        ProviderUsageSourcePhases.AgentRuntime,
                        ProviderUsageObservationStatus.Observed,
                        InputTokens: 17,
                        CachedInputTokens: 3,
                        OutputTokens: 6,
                        ReasoningTokens: 2,
                        TotalTokens: 23,
                        ToolCallCount: 1)
                    {
                        CacheWriteTokens = 1,
                        ProviderRequestId = ProviderRequestSentinel,
                        ProviderResponseId = ProviderResponseSentinel,
                        RuntimeSessionKey = UsageRuntimeSessionSentinel,
                        ProviderCostUsd = 0.125m,
                        RawUsageJson = $$"""{"sentinel":"{{RawUsageSentinel}}"}""",
                        DiagnosticsJson = $$"""{"sentinel":"{{DiagnosticsSentinel}}"}"""
                    }
                ],
                ToolInvocationTraces =
                [
                    new AgentToolInvocationTrace(
                        PublicToolName,
                        ToolInvocationClassification.Mutation,
                        Sequence: 1,
                        StartedAtUtc: now,
                        CompletedAtUtc: now.AddMilliseconds(10),
                        Succeeded: true,
                        FailureMessage: string.Empty)
                    {
                        RuntimeToolProviderKey = PrivateRuntimeToolProviderKeySentinel,
                        RuntimeToolProviderName = PublicRuntimeToolProviderName,
                        Signature = "workbookPath=artifacts/public-projection.xlsx"
                    }
                ],
                EntryAgentRequestCompatibilityEvidence = new ProviderRequestCompatibilityEvidence(
                    ProviderRequestCompatibilityEvidence.CurrentSchemaVersion,
                    provider.Kind,
                    provider.Id,
                    provider.Transport,
                    ProviderCompatibilitySentinel,
                    provider.DefaultModel,
                    ProviderInvocationFeatures.FunctionTools,
                    RequestedEffort: null,
                    EffectiveEffort: null,
                    ProviderRequestCompatibilityDisposition.Adjusted,
                    ProviderModelParameterAdjustment.None)
            };

            return Task.FromResult(response);
        }

        public Task<AgentRuntimeResponse> RespondToPendingApprovalsAsync(
            AgentDefinition agent,
            ProviderProfile provider,
            ChatSessionRecord session,
            IReadOnlyList<CapabilityCatalogItem> capabilities,
            IReadOnlyList<AgentMemoryRecord> memory,
            bool approved,
            string? runtimeSessionKey,
            Func<ExecutionState, string, string, Task> progressCallback,
            CancellationToken cancellationToken = default,
            bool suppressApprovalRequirements = false,
            AgentStructuredOutputContract? structuredOutput = null,
            AgentRuntimeExecutionOptions? executionOptions = null)
        {
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new AgentRuntimeResponse(
                PublicContinuationResponse,
                InputTokens: 5,
                OutputTokens: 7,
                ToolCalls: 0,
                RuntimeSessionKey: RuntimeSessionKeySentinel,
                SerializedSessionStateJson: $$"""{"sentinel":"{{SerializedSessionSentinel}}"}""",
                PendingApprovals: [])
            {
                UsageObservations =
                [
                    new ProviderUsageObservation(
                        Guid.NewGuid(),
                        now,
                        provider.Name,
                        provider.Kind,
                        provider.DefaultModel,
                        provider.Transport,
                        ProviderUsageSourcePhases.AgentRuntimeContinuation,
                        ProviderUsageObservationStatus.Observed,
                        InputTokens: 5,
                        CachedInputTokens: 1,
                        OutputTokens: 7,
                        ReasoningTokens: 0,
                        TotalTokens: 12,
                        ToolCallCount: 0)
                    {
                        ProviderRequestId = ProviderRequestSentinel,
                        ProviderResponseId = ProviderResponseSentinel,
                        RuntimeSessionKey = UsageRuntimeSessionSentinel,
                        RawUsageJson = $$"""{"sentinel":"{{RawUsageSentinel}}"}""",
                        DiagnosticsJson = $$"""{"sentinel":"{{DiagnosticsSentinel}}"}"""
                    }
                ]
            });
        }
    }
}
