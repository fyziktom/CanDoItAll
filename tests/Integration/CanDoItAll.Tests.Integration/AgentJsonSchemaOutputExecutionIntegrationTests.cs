using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Security;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration;

public sealed class AgentJsonSchemaOutputExecutionIntegrationTests
{
    private const string SchemaJson =
        """
        {
          "type": "object",
          "properties": {
            "status": {
              "type": "string",
              "enum": [ "ready", "blocked" ]
            },
            "count": {
              "type": "integer",
              "minimum": 1
            }
          },
          "required": [ "status", "count" ],
          "additionalProperties": false
        }
        """;

    [Fact]
    public async Task Portable_schema_contract_flows_through_service_api_and_openapi_without_runtime_types()
    {
        var runtime = new DeterministicJsonSchemaAgentRuntime(
            """{"status":"ready","count":2}""",
            """{"status":"unknown","extra":true}""",
            """{"status":"blocked","count":4}""");
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            configureServices: services =>
            {
                services.RemoveAll<ISecretVault>();
                services.AddSingleton<ISecretVault, InMemorySecretVault>();
                services.RemoveAll<IFakeAgentRuntime>();
                services.RouteRuntimePortsThroughAgentRuntime();
                services.AddSingleton(runtime);
                services.AddSingleton<IFakeAgentRuntime>(serviceProvider =>
                    serviceProvider.GetRequiredService<DeterministicJsonSchemaAgentRuntime>());
                UseDirectWorkspaceService(services);
            });
        await using var scope = host.App.Services.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);
        var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);
        var contract = CreateContract();

        var validResult = await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                AgentId: agent.Id,
                Prompt: "Return the deterministic valid portable output.",
                InitialActivityOperationId: AgentExecutionOperationId.New(),
                ChatSessionId: session.Id,
                JsonSchemaOutput: contract));

        Assert.Equal(ExecutionState.Completed, validResult.State);
        var validStructuredOutput = Assert.IsType<AgentJsonSchemaOutputResult>(validResult.StructuredOutput);
        Assert.Equal(AgentJsonSchemaOutputValidationStatus.Valid, validStructuredOutput.ValidationStatus);
        Assert.Equal("ready", validStructuredOutput.Data!.Value.GetProperty("status").GetString());
        Assert.Empty(validStructuredOutput.ValidationErrors);

        var validDetail = await executionRunStore.GetExecutionRunDetailAsync(validResult.ExecutionRunId);
        Assert.NotNull(validDetail);
        Assert.Equal(ExecutionState.Completed, validDetail!.Run.State);
        Assert.Equal(RunOutcome.Succeeded, validDetail.Run.Outcome);
        Assert.Equal(AgentJsonSchemaOutputContractVersions.Current, validDetail.Run.StructuredOutputSchemaVersion);
        Assert.Equal("portable_result", validDetail.Run.StructuredOutputSchemaName);
        Assert.Equal(contract.Schema.GetRawText(), validDetail.Run.StructuredOutputJsonSchema);
        Assert.Equal(validStructuredOutput.SchemaHash, validDetail.Run.StructuredOutputSchemaHash);
        Assert.True(validDetail.Run.StructuredOutputSchemaStrict);
        Assert.Equal(validResult.ResponseText, validDetail.Run.StructuredOutputRawOutput);
        Assert.Equal(
            AgentJsonSchemaOutputValidationStatus.Valid.ToString(),
            validDetail.Run.StructuredOutputValidationStatus);
        Assert.Equal("[]", validDetail.Run.StructuredOutputValidationErrorsJson);
        Assert.Contains(
            validDetail.ExecutionLog,
            entry => entry.Phase == "Output validation" &&
                     entry.Message.Contains(validStructuredOutput.SchemaHash, StringComparison.Ordinal));

        var invalidResult = await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                AgentId: agent.Id,
                Prompt: "Return the deterministic schema-invalid portable output.",
                InitialActivityOperationId: AgentExecutionOperationId.New(),
                ChatSessionId: session.Id,
                JsonSchemaOutput: contract));

        Assert.Equal(ExecutionState.Failed, invalidResult.State);
        var invalidStructuredOutput = Assert.IsType<AgentJsonSchemaOutputResult>(invalidResult.StructuredOutput);
        Assert.Equal(
            AgentJsonSchemaOutputValidationStatus.SchemaValidationFailed,
            invalidStructuredOutput.ValidationStatus);
        Assert.NotNull(invalidStructuredOutput.Data);
        Assert.Contains(
            invalidStructuredOutput.ValidationErrors,
            error => error.Code == "required-property-missing");
        Assert.Contains(
            invalidStructuredOutput.ValidationErrors,
            error => error.Code == "enum-mismatch");
        Assert.Contains(
            invalidStructuredOutput.ValidationErrors,
            error => error.Code == "additional-property-not-allowed");

        var invalidDetail = await executionRunStore.GetExecutionRunDetailAsync(invalidResult.ExecutionRunId);
        Assert.NotNull(invalidDetail);
        Assert.Equal(ExecutionState.Failed, invalidDetail!.Run.State);
        Assert.Equal(RunOutcome.Failed, invalidDetail.Run.Outcome);
        Assert.Equal(invalidResult.ResponseText, invalidDetail.Run.StructuredOutputRawOutput);
        Assert.Equal(
            AgentJsonSchemaOutputValidationStatus.SchemaValidationFailed.ToString(),
            invalidDetail.Run.StructuredOutputValidationStatus);
        Assert.Contains("required-property-missing", invalidDetail.Run.StructuredOutputValidationErrorsJson, StringComparison.Ordinal);
        Assert.Contains("enum-mismatch", invalidDetail.Run.StructuredOutputValidationErrorsJson, StringComparison.Ordinal);
        Assert.Contains("additional-property-not-allowed", invalidDetail.Run.StructuredOutputValidationErrorsJson, StringComparison.Ordinal);

        Assert.Collection(
            runtime.ExecutionOptions,
            options => AssertResponseFormatOptions(options, contract),
            options => AssertResponseFormatOptions(options, contract));

        using var apiResponse = await host.Client.PostAsJsonAsync(
            $"/api/agents/{agent.Id:D}/execution-runs",
            new
            {
                prompt = "Return the deterministic API portable output.",
                structuredOutput = contract
            });
        var apiBody = await apiResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, apiResponse.StatusCode);
        using (var apiPayload = JsonDocument.Parse(apiBody))
        {
            var apiStructuredOutput = apiPayload.RootElement.GetProperty("structuredOutput");
            Assert.Equal("Valid", apiStructuredOutput.GetProperty("validationStatus").GetString());
            Assert.Equal("blocked", apiStructuredOutput.GetProperty("data").GetProperty("status").GetString());
            Assert.False(apiStructuredOutput.TryGetProperty("schema", out _));
            Assert.False(apiStructuredOutput.TryGetProperty("schemaHash", out _));
            Assert.False(apiStructuredOutput.TryGetProperty("rawOutput", out _));
            Assert.DoesNotContain(
                runtime.ExecutionOptions[2]!.ResponseFormatJsonSchema,
                apiBody,
                StringComparison.Ordinal);
        }

        Assert.Equal(3, runtime.ExecutionOptions.Count);
        AssertResponseFormatOptions(runtime.ExecutionOptions[2], contract);

        using var openApi = JsonDocument.Parse(await host.Client.GetStringAsync("/openapi/v1.json"));
        AssertPortableRequestSchema(openApi.RootElement);
    }

    private static void AssertResponseFormatOptions(
        AgentRuntimeExecutionOptions? options,
        AgentJsonSchemaOutputContract contract)
    {
        Assert.NotNull(options);
        Assert.True(options!.RequireJsonResponseFormat);
        Assert.Null(options.StructuredOutput);
        using var responseFormatSchema = JsonDocument.Parse(options.ResponseFormatJsonSchema);
        Assert.True(JsonElement.DeepEquals(contract.Schema, responseFormatSchema.RootElement));
        Assert.Equal(contract.Name, options.ResponseFormatSchemaName);
        Assert.Equal(
            $"Portable JSON Schema output contract {contract.Version}.",
            options.ResponseFormatSchemaDescription);
    }

    private static void AssertPortableRequestSchema(JsonElement openApi)
    {
        var path = openApi
            .GetProperty("paths")
            .GetProperty("/api/agents/{agentId}/execution-runs")
            .GetProperty("post");
        var requestBody = path.GetProperty("requestBody");
        var mediaType = requestBody
            .GetProperty("content")
            .EnumerateObject()
            .First(item => item.Name.StartsWith("application/json", StringComparison.OrdinalIgnoreCase));
        var components = openApi.GetProperty("components").GetProperty("schemas");
        var requestSchema = ResolveSchema(mediaType.Value.GetProperty("schema"), components);
        var requestProperties = requestSchema.GetProperty("properties");

        Assert.True(requestProperties.TryGetProperty("structuredOutput", out var structuredOutput));
        var portableReference = FindFirstSchemaReference(structuredOutput);
        Assert.NotNull(portableReference);
        Assert.Contains("AgentJsonSchemaOutputContract", portableReference, StringComparison.Ordinal);

        var portableSchema = ResolveSchemaReference(portableReference!, components);
        var portableProperties = portableSchema.GetProperty("properties");
        Assert.True(portableProperties.TryGetProperty("kind", out _));
        Assert.True(portableProperties.TryGetProperty("version", out _));
        Assert.True(portableProperties.TryGetProperty("name", out _));
        Assert.True(portableProperties.TryGetProperty("schema", out _));
        Assert.True(portableProperties.TryGetProperty("strict", out _));

        var requestBodySurface = string.Concat(
            requestSchema.GetRawText(),
            structuredOutput.GetRawText(),
            portableSchema.GetRawText());
        Assert.DoesNotContain("System.Type", requestBodySurface, StringComparison.Ordinal);
        Assert.DoesNotContain("AgentStructuredOutputContract", requestBodySurface, StringComparison.Ordinal);
        Assert.DoesNotContain("outputType", requestBodySurface, StringComparison.OrdinalIgnoreCase);
    }

    private static JsonElement ResolveSchema(JsonElement schema, JsonElement components)
    {
        if (schema.ValueKind == JsonValueKind.Object &&
            schema.TryGetProperty("$ref", out var reference))
        {
            return ResolveSchemaReference(reference.GetString()!, components);
        }

        return schema;
    }

    private static JsonElement ResolveSchemaReference(string reference, JsonElement components)
    {
        const string prefix = "#/components/schemas/";
        Assert.StartsWith(prefix, reference, StringComparison.Ordinal);
        return components.GetProperty(reference[prefix.Length..]);
    }

    private static string? FindFirstSchemaReference(JsonElement schema)
    {
        if (schema.ValueKind == JsonValueKind.Object)
        {
            if (schema.TryGetProperty("$ref", out var reference))
            {
                return reference.GetString();
            }

            foreach (var property in schema.EnumerateObject())
            {
                var nested = FindFirstSchemaReference(property.Value);
                if (nested is not null)
                {
                    return nested;
                }
            }
        }
        else if (schema.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in schema.EnumerateArray())
            {
                var nested = FindFirstSchemaReference(item);
                if (nested is not null)
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static AgentJsonSchemaOutputContract CreateContract()
    {
        using var document = JsonDocument.Parse(SchemaJson);
        return new AgentJsonSchemaOutputContract(
            Kind: AgentJsonSchemaOutputContractVersions.Kind,
            Version: AgentJsonSchemaOutputContractVersions.Current,
            Name: "portable_result",
            Schema: document.RootElement.Clone(),
            Strict: true);
    }

    private static void UseDirectWorkspaceService(IServiceCollection services)
    {
        services.RemoveAll<IAgentFrameworkWorkspaceService>();
        services.RemoveAll<IAgentPackageService>();
        services.RemoveAll<IProviderDiagnosticsService>();
        services.RemoveAll<IAgentExecutionCheckpointBridge>();
        services.RemoveAll<IAgentExecutionGovernanceBridge>();
        services.RemoveAll<IAgentExecutionEventSink>();
        services.AddScoped<IAgentPackageService>(serviceProvider => new ZipAgentPackageService(
            serviceProvider.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot(),
            ResolveWorkspaceScope(serviceProvider)));
        services.AddScoped<IProviderDiagnosticsService>(serviceProvider =>
        {
            var portFacade = new FakeAgentRuntimePortAdapter(
                serviceProvider.GetRequiredService<IFakeAgentRuntime>());
            return new ProviderDiagnosticsService(portFacade, portFacade);
        });
        services.AddScoped<IAgentExecutionCheckpointBridge>(serviceProvider => new WorkflowBackedAgentExecutionCheckpointBridge(
            serviceProvider.GetRequiredService<ISandboxWorkspaceStore>(),
            serviceProvider.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot(),
            ResolveWorkspaceScope(serviceProvider)));
        services.AddScoped<IAgentExecutionGovernanceBridge>(serviceProvider => new DurableAgentExecutionGovernanceBridge(
            serviceProvider.GetRequiredService<IAgentExecutionCheckpointBridge>()));
        services.AddScoped<IAgentExecutionEventSink, NullAgentExecutionEventSink>();
        services.AddScoped(serviceProvider =>
        {
            var profile = serviceProvider
                .GetRequiredService<IDatabaseProfileRuntimeAccessor>()
                .ResolveCurrentProfile()
                .Profile;
            return new AgentExecutionActivityWorkspaceIdentity(
                profile.Id,
                WorkspaceScopeDescriptor.Organization(
                    profile.Id.ToString("N")),
                serviceProvider
                    .GetRequiredService<IAgentExecutionProfileGenerationSource>()
                    .GetGeneration());
        });
        services.AddScoped<IAgentFrameworkWorkspaceService, AgentFrameworkWorkspaceService>();
    }

    private static WorkspaceScopeDescriptor ResolveWorkspaceScope(IServiceProvider serviceProvider)
    {
        var profile = serviceProvider.GetRequiredService<IDatabaseProfileRuntimeAccessor>().ResolveCurrentProfile();
        return WorkspaceScopeDescriptor.Organization(profile.Profile.Id.ToString("N"));
    }

    private sealed class DeterministicJsonSchemaAgentRuntime(params string[] responses) : IFakeAgentRuntime
    {
        private readonly Queue<string> responseQueue = new(responses);

        public List<AgentRuntimeExecutionOptions?> ExecutionOptions { get; } = [];

        public Task<ProviderHealthResult> TestProviderAsync(
            ProviderProfile provider,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ProviderHealthResult(true, "ok", []));

        public Task<ProviderTestChatResult> RunProviderTestChatAsync(
            ProviderProfile provider,
            ProviderTestChatRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ProviderTestChatResult(provider.DefaultModel, "ok", 1, 1));

        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
            ProviderProfile provider,
            ProviderModelMaintenanceEditorRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ProviderModelMaintenanceEditorResult(
                request.TargetModel,
                request.BaseModel,
                request.SystemPrompt,
                request.ContextLength,
                string.Empty,
                "ok"));

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
            ExecutionOptions.Add(executionOptions);
            if (!responseQueue.TryDequeue(out var response))
            {
                throw new InvalidOperationException("The deterministic runtime response queue is empty.");
            }

            return Task.FromResult(new AgentRuntimeResponse(
                ResponseText: response,
                InputTokens: 5,
                OutputTokens: 7,
                ToolCalls: 0,
                RuntimeSessionKey: runtimeSessionKey ?? string.Empty,
                SerializedSessionStateJson: null,
                PendingApprovals: []));
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
            => throw new NotSupportedException(
                "Pending approval continuation is not used by the portable schema integration test.");
    }
}
