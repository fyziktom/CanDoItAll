using System.Net;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Integration;

public sealed class ApiResponseContractIntegrationTests
{
    private const string ApiErrorSchema = "ApiErrorResponse";

    private static readonly OperationContract[] OwnedOperations =
    [
        // The eight operations named by missing-API input 006.
        Contract("/api/agents", "get", Success("200", "AgentDefinition"), "401", "403"),
        Contract("/api/agents/providers", "get", Success("200", "ProviderProfile"), "401", "403"),
        Contract("/api/agents", "post", Success("200", expectedFormat: "uuid"), "400", "401", "403"),
        Contract(
            "/api/agents/{agentId}/execution-runs",
            "post",
            Success("200", "AgentExecutionRunResultApiResponse"),
            "400",
            "401",
            "403"),
        Contract(
            "/api/agents/{agentId}/execution-runs/{executionRunId}",
            "get",
            Success("200", "AgentExecutionRunDetailApiResponse"),
            "401",
            "403",
            "404"),
        Contract(
            "/api/workflows/definitions",
            "get",
            Success("200", "WorkflowCatalogItem"),
            "400",
            "401",
            "403"),
        Contract(
            "/api/workflows/definitions/{workflowId}/runs/start",
            "post",
            Success("200", "WorkflowRunStartApiResponse"),
            "400",
            "401",
            "403",
            "404",
            "409"),
        Contract(
            "/api/workflows/runs/{runId}/detail",
            "get",
            Success("200", "WorkflowRunDetailApiResponse"),
            "401",
            "403",
            "404"),

        // SB01: remote package import.
        Contract(
            "/api/agents/import-package",
            "post",
            [
                Success("200", "AgentPackageImportReceipt"),
                Success("201", "AgentPackageImportReceipt")
            ],
            "400",
            "401",
            "403",
            "409",
            "412"),

        // SB02: external-key provisioning.
        Contract(
            "/api/agents/by-external-key/{externalNamespace}/{key}",
            "get",
            Success("200", "AgentExternalProvisioningResource"),
            "400",
            "401",
            "403",
            "404"),
        Contract(
            "/api/agents/by-external-key/{externalNamespace}/{key}",
            "put",
            [
                Success("200", "AgentExternalProvisioningReceipt"),
                Success("201", "AgentExternalProvisioningReceipt")
            ],
            "400",
            "401",
            "403",
            "409",
            "412"),
        Contract(
            "/api/agents/by-external-key/{externalNamespace}/{key}",
            "delete",
            Success("200", "AgentExternalProvisioningReceipt"),
            "400",
            "401",
            "403",
            "404",
            "409",
            "412"),

        // SB03: both public execution-run request shapes.
        Contract(
            "/api/agents/execution-runs",
            "post",
            Success("200", "AgentExecutionRunResultApiResponse"),
            "400",
            "401",
            "403"),

        // SB04: stable identity lookup.
        Contract(
            "/api/workflows/definitions/by-template-key/{templateKey}",
            "get",
            Success("200", "WorkflowStableIdentityResolution"),
            "400",
            "401",
            "403"),
        Contract(
            "/api/workflows/definitions/by-external-key/{externalNamespace}/{externalKey}",
            "get",
            Success("200", "WorkflowStableIdentityResolution"),
            "400",
            "401",
            "403"),

        // SB05: both start variants and durable-key lookup.
        Contract(
            "/api/workflows/runs/start",
            "post",
            Success("200", "WorkflowRunStartApiResponse"),
            "400",
            "401",
            "403",
            "404",
            "409"),
        Contract(
            "/api/workflows/runs/by-idempotency-key/{key}",
            "get",
            Success("200", "WorkflowLaunchIdempotencyEvidence"),
            "400",
            "401",
            "403",
            "404"),

        // SB06: recruiting evidence.
        Contract(
            "/api/agent-recruiting/interviews",
            "post",
            Success("201", "AgentRecruitingInterview"),
            "400",
            "401",
            "403",
            "404",
            "409"),
        Contract(
            "/api/agent-recruiting/interviews/{interviewId}/attempts",
            "post",
            Success("201", "AgentRecruitingInterview"),
            "400",
            "401",
            "403",
            "404",
            "409"),
        Contract(
            "/api/agent-recruiting/interviews/{interviewId}/reviews",
            "post",
            Success("201", "AgentRecruitingInterview"),
            "400",
            "401",
            "403",
            "404",
            "409"),
        Contract(
            "/api/agent-recruiting/interviews/{interviewId}",
            "get",
            Success("200", "AgentRecruitingInterview"),
            "400",
            "401",
            "403",
            "404"),
        Contract(
            "/api/agent-recruiting/candidates/{agentId}/readiness",
            "get",
            Success("200", "AgentRecruitingCandidateReadiness"),
            "400",
            "401",
            "403",
            "404")
    ];

    [Fact]
    public async Task Named_and_numbered_operations_publish_typed_success_and_error_responses()
    {
        await using var host = await CreateHostAsync(jwtEnabled: false);
        using var openApi = await ReadOpenApiAsync(host);
        var paths = openApi.RootElement.GetProperty("paths");

        foreach (var contract in OwnedOperations)
        {
            var operation = GetOperation(paths, contract);
            foreach (var success in contract.Successes)
            {
                var schema = GetResponseSchema(operation, success.Status);
                if (success.ExpectedSchema is not null)
                {
                    AssertSchemaReference(
                        schema,
                        success.ExpectedSchema,
                        $"{contract.Method.ToUpperInvariant()} {contract.Path} {success.Status}");
                }
                else
                {
                    Assert.Equal(
                        success.ExpectedFormat,
                        FindSchemaProperty(schema, "format")?.GetString());
                }
            }

            foreach (var status in contract.ErrorStatuses)
            {
                AssertSchemaReference(
                    GetResponseSchema(operation, status),
                    ApiErrorSchema,
                    $"{contract.Method.ToUpperInvariant()} {contract.Path} {status}");
            }
        }
    }

    [Fact]
    public async Task Owned_schemas_expose_required_nullable_and_string_enum_contracts()
    {
        await using var host = await CreateHostAsync(jwtEnabled: false);
        using var openApi = await ReadOpenApiAsync(host);
        var schemas = openApi.RootElement.GetProperty("components").GetProperty("schemas");

        AssertRequired(schemas, "ApiErrorResponse", "errors");
        AssertRequired(schemas, "ApiErrorItem", "code", "message", "severity");

        AssertRequired(
            schemas,
            "AgentJsonSchemaOutputContract",
            "kind",
            "version",
            "name",
            "schema");
        AssertRequired(schemas, "AgentExecutionRunApiRequest", "agentId", "prompt");
        AssertRequired(schemas, "AgentExecutionRunStartApiRequest", "prompt");
        AssertNullableProperty(schemas, "AgentExecutionRunApiRequest", "structuredOutput");
        AssertNullableProperty(schemas, "AgentExecutionRunStartApiRequest", "structuredOutput");
        AssertNullableProperty(schemas, "AgentStructuredOutputApiResponse", "data");

        AssertRequired(
            schemas,
            "WorkflowStableIdentityResolution",
            "identityKind",
            "namespace",
            "key",
            "status",
            "workflowId",
            "runnableVersionId",
            "materializations",
            "message");
        AssertNullableProperty(schemas, "WorkflowStableIdentityResolution", "workflowId");
        AssertNullableProperty(schemas, "WorkflowStableIdentityResolution", "runnableVersionId");
        AssertNullableProperty(schemas, "WorkflowRunStartApiResponse", "idempotencyKeyHash");

        AssertStringEnum(
            schemas,
            "AgentRecruitingTargetKind",
            "agent-execution-run",
            "workflow-run",
            "process-run");
        AssertStringEnum(
            schemas,
            "AgentRecruitingReadinessStatus",
            "Ready",
            "NoInterviews",
            "IncompleteEvidence",
            "AwaitingHumanApproval",
            "Rejected");
        AssertStringEnum(
            schemas,
            "WorkflowStableIdentityResolutionStatus",
            "Resolved",
            "NotFound",
            "Ambiguous",
            "Stale");
        AssertStringEnum(
            schemas,
            "AgentJsonSchemaOutputValidationStatus",
            "Valid",
            "ProviderRefusal",
            "MalformedJson",
            "SchemaValidationFailed");
    }

    [Fact]
    public async Task Portable_structured_output_surfaces_never_expose_runtime_System_Type()
    {
        await using var host = await CreateHostAsync(jwtEnabled: false);
        using var openApi = await ReadOpenApiAsync(host);
        var root = openApi.RootElement;
        var paths = root.GetProperty("paths");
        var schemas = root.GetProperty("components").GetProperty("schemas");

        foreach (var (path, requestSchemaName) in new[]
                 {
                     ("/api/agents/execution-runs", "AgentExecutionRunApiRequest"),
                     ("/api/agents/{agentId}/execution-runs", "AgentExecutionRunStartApiRequest")
                 })
        {
            var operation = paths.GetProperty(path).GetProperty("post");
            var requestSchema = GetRequestSchema(operation);
            AssertSchemaReference(requestSchema, requestSchemaName, $"POST {path} request");
            var resolvedRequest = ResolveSchema(requestSchema, schemas);
            var structuredOutput = resolvedRequest
                .GetProperty("properties")
                .GetProperty("structuredOutput");
            AssertSchemaReference(
                structuredOutput,
                "AgentJsonSchemaOutputContract",
                $"POST {path} structuredOutput");
        }

        var portableSurface = string.Concat(
            schemas.GetProperty("AgentExecutionRunApiRequest").GetRawText(),
            schemas.GetProperty("AgentExecutionRunStartApiRequest").GetRawText(),
            schemas.GetProperty("AgentJsonSchemaOutputContract").GetRawText(),
            schemas.GetProperty("AgentExecutionRunResultApiResponse").GetRawText(),
            schemas.GetProperty("AgentStructuredOutputApiResponse").GetRawText(),
            schemas.GetProperty("AgentStructuredOutputValidationErrorApiResponse").GetRawText());
        Assert.DoesNotContain("System.Type", portableSurface, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "AgentStructuredOutputContract",
            portableSurface,
            StringComparison.Ordinal);
        Assert.DoesNotContain("outputType", portableSurface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rawOutput", portableSurface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("schemaHash", portableSurface, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Representative_runtime_payloads_match_OpenAPI_including_auth_error_envelope()
    {
        await using var host = await CreateHostAsync(jwtEnabled: false);
        using var openApi = await ReadOpenApiAsync(host);
        var root = openApi.RootElement;

        await AssertRuntimePayloadAsync(
            host.Client,
            root,
            HttpMethod.Get,
            "/api/agents?includeTemplates=false",
            "/api/agents",
            "get",
            HttpStatusCode.OK);
        await AssertRuntimePayloadAsync(
            host.Client,
            root,
            HttpMethod.Get,
            "/api/agents/providers",
            "/api/agents/providers",
            "get",
            HttpStatusCode.OK);
        await AssertRuntimePayloadAsync(
            host.Client,
            root,
            HttpMethod.Get,
            "/api/workflows/definitions",
            "/api/workflows/definitions",
            "get",
            HttpStatusCode.OK);

        using (var stableResponse = await AssertRuntimePayloadAsync(
                   host.Client,
                   root,
                   HttpMethod.Get,
                   "/api/workflows/definitions/by-template-key/contract-missing",
                   "/api/workflows/definitions/by-template-key/{templateKey}",
                   "get",
                   HttpStatusCode.OK))
        {
            using var stablePayload = JsonDocument.Parse(
                await stableResponse.Content.ReadAsStringAsync());
            Assert.Equal(
                "NotFound",
                stablePayload.RootElement.GetProperty("status").GetString());
            Assert.Equal(JsonValueKind.Null, stablePayload.RootElement.GetProperty("workflowId").ValueKind);
            Assert.Equal(
                JsonValueKind.Null,
                stablePayload.RootElement.GetProperty("runnableVersionId").ValueKind);
        }

        using (var errorResponse = await AssertRuntimePayloadAsync(
                   host.Client,
                   root,
                   HttpMethod.Get,
                   "/api/workflows/definitions?externalNamespace=partner-only",
                   "/api/workflows/definitions",
                   "get",
                   HttpStatusCode.BadRequest))
        {
            using var errorPayload = JsonDocument.Parse(
                await errorResponse.Content.ReadAsStringAsync());
            Assert.Equal(
                "workflows.external-identity-incomplete",
                errorPayload.RootElement
                    .GetProperty("errors")[0]
                    .GetProperty("code")
                    .GetString());
        }

        await using var protectedHost = await CreateHostAsync(jwtEnabled: true);
        using var authResponse = await AssertRuntimePayloadAsync(
            protectedHost.Client,
            root,
            HttpMethod.Get,
            "/api/agents",
            "/api/agents",
            "get",
            HttpStatusCode.Unauthorized);
        Assert.Equal("application/json", authResponse.Content.Headers.ContentType?.MediaType);
        using var authPayload = JsonDocument.Parse(await authResponse.Content.ReadAsStringAsync());
        var authError = Assert.Single(
            authPayload.RootElement.GetProperty("errors").EnumerateArray());
        Assert.Equal("api.authorization-required", authError.GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.String, authError.GetProperty("message").ValueKind);
        Assert.Equal(JsonValueKind.Number, authError.GetProperty("severity").ValueKind);
    }

    private static OperationContract Contract(
        string path,
        string method,
        ResponseContract success,
        params string[] errors)
        => new(path, method, [success], errors);

    private static OperationContract Contract(
        string path,
        string method,
        IReadOnlyList<ResponseContract> successes,
        params string[] errors)
        => new(path, method, successes, errors);

    private static ResponseContract Success(
        string status,
        string? expectedSchema = null,
        string? expectedFormat = null)
        => new(status, expectedSchema, expectedFormat);

    private static async Task<ApiTestHost> CreateHostAsync(bool jwtEnabled)
        => await ApiTestHost.CreateAsync(
            jwtEnabled,
            services =>
            {
                services.RemoveAll<ILoggerProvider>();
                services.RemoveAll<ISecretVault>();
                services.AddSingleton<ISecretVault, InMemorySecretVault>();
            });

    private static async Task<JsonDocument> ReadOpenApiAsync(ApiTestHost host)
        => JsonDocument.Parse(await host.Client.GetStringAsync("/openapi/v1.json"));

    private static JsonElement GetOperation(JsonElement paths, OperationContract contract)
        => paths.GetProperty(contract.Path).GetProperty(contract.Method);

    private static JsonElement GetResponseSchema(JsonElement operation, string status)
    {
        var response = operation.GetProperty("responses").GetProperty(status);
        var content = response.GetProperty("content");
        return content
            .EnumerateObject()
            .First(item => item.Name.Contains("json", StringComparison.OrdinalIgnoreCase))
            .Value
            .GetProperty("schema");
    }

    private static JsonElement GetRequestSchema(JsonElement operation)
    {
        var content = operation.GetProperty("requestBody").GetProperty("content");
        return content
            .EnumerateObject()
            .First(item => item.Name.Contains("json", StringComparison.OrdinalIgnoreCase))
            .Value
            .GetProperty("schema");
    }

    private static void AssertSchemaReference(
        JsonElement schema,
        string expectedSchema,
        string context)
    {
        var references = FindSchemaReferences(schema).ToList();
        Assert.Contains(
            $"#/components/schemas/{expectedSchema}",
            references,
            StringComparer.Ordinal);
        Assert.True(references.Count > 0, $"{context} did not contain a component schema reference.");
    }

    private static IEnumerable<string> FindSchemaReferences(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("$ref", out var reference))
            {
                yield return reference.GetString()!;
            }

            foreach (var property in element.EnumerateObject())
            {
                foreach (var nested in FindSchemaReferences(property.Value))
                {
                    yield return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var nested in FindSchemaReferences(item))
                {
                    yield return nested;
                }
            }
        }
    }

    private static JsonElement? FindSchemaProperty(JsonElement schema, string propertyName)
    {
        if (schema.ValueKind == JsonValueKind.Object)
        {
            if (schema.TryGetProperty(propertyName, out var value))
            {
                return value;
            }

            foreach (var property in schema.EnumerateObject())
            {
                var nested = FindSchemaProperty(property.Value, propertyName);
                if (nested.HasValue)
                {
                    return nested;
                }
            }
        }
        else if (schema.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in schema.EnumerateArray())
            {
                var nested = FindSchemaProperty(item, propertyName);
                if (nested.HasValue)
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static void AssertRequired(
        JsonElement schemas,
        string schemaName,
        params string[] propertyNames)
    {
        var schema = schemas.GetProperty(schemaName);
        var required = schema
            .GetProperty("required")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToHashSet(StringComparer.Ordinal);
        Assert.All(propertyNames, name => Assert.Contains(name, required));
    }

    private static void AssertNullableProperty(
        JsonElement schemas,
        string schemaName,
        string propertyName)
    {
        var property = schemas
            .GetProperty(schemaName)
            .GetProperty("properties")
            .GetProperty(propertyName);
        Assert.True(
            AllowsNull(property),
            $"{schemaName}.{propertyName} is nullable at runtime but OpenAPI does not allow null: {property.GetRawText()}");
    }

    private static bool AllowsNull(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (schema.TryGetProperty("nullable", out var nullable) && nullable.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (schema.TryGetProperty("type", out var type))
        {
            if (type.ValueKind == JsonValueKind.String && type.GetString() == "null")
            {
                return true;
            }

            if (type.ValueKind == JsonValueKind.Array &&
                type.EnumerateArray().Any(item => item.GetString() == "null"))
            {
                return true;
            }
        }

        foreach (var unionName in new[] { "anyOf", "oneOf" })
        {
            if (schema.TryGetProperty(unionName, out var union) &&
                union.EnumerateArray().Any(AllowsNull))
            {
                return true;
            }
        }

        return false;
    }

    private static void AssertStringEnum(
        JsonElement schemas,
        string schemaName,
        params string[] expectedValues)
    {
        var schema = schemas.GetProperty(schemaName);
        if (schema.TryGetProperty("type", out var type))
        {
            Assert.Equal("string", type.GetString());
        }

        var values = schema
            .GetProperty("enum")
            .EnumerateArray()
            .Select(item =>
            {
                Assert.Equal(JsonValueKind.String, item.ValueKind);
                return item.GetString();
            })
            .ToList();
        Assert.Equal(expectedValues, values);
    }

    private static JsonElement ResolveSchema(JsonElement schema, JsonElement schemas)
    {
        if (schema.ValueKind == JsonValueKind.Object &&
            schema.TryGetProperty("$ref", out var reference))
        {
            const string prefix = "#/components/schemas/";
            var value = reference.GetString()!;
            Assert.StartsWith(prefix, value, StringComparison.Ordinal);
            return schemas.GetProperty(value[prefix.Length..]);
        }

        if (schema.ValueKind == JsonValueKind.Object &&
            schema.TryGetProperty("anyOf", out var anyOf))
        {
            var nonNull = anyOf
                .EnumerateArray()
                .First(item => !AllowsNull(item));
            return ResolveSchema(nonNull, schemas);
        }

        return schema;
    }

    private static async Task<HttpResponseMessage> AssertRuntimePayloadAsync(
        HttpClient client,
        JsonElement openApi,
        HttpMethod method,
        string runtimePath,
        string contractPath,
        string operationName,
        HttpStatusCode expectedStatus)
    {
        using var request = new HttpRequestMessage(method, runtimePath);
        var response = await client.SendAsync(request);
        Assert.Equal(expectedStatus, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(body));
        using var payload = JsonDocument.Parse(body);
        var schema = openApi
            .GetProperty("paths")
            .GetProperty(contractPath)
            .GetProperty(operationName)
            .GetProperty("responses")
            .GetProperty(((int)expectedStatus).ToString())
            .GetProperty("content")
            .EnumerateObject()
            .First(item => item.Name.Contains("json", StringComparison.OrdinalIgnoreCase))
            .Value
            .GetProperty("schema");
        var schemas = openApi.GetProperty("components").GetProperty("schemas");
        AssertPayloadMatchesSchema(payload.RootElement, schema, schemas, "$");
        return response;
    }

    private static void AssertPayloadMatchesSchema(
        JsonElement payload,
        JsonElement sourceSchema,
        JsonElement schemas,
        string path)
    {
        if (payload.ValueKind == JsonValueKind.Null)
        {
            Assert.True(AllowsNull(sourceSchema), $"{path} was null but the OpenAPI schema is non-nullable.");
            return;
        }

        var schema = ResolveSchema(sourceSchema, schemas);
        if (schema.TryGetProperty("enum", out var enumValues))
        {
            Assert.Contains(
                enumValues.EnumerateArray(),
                item => JsonElement.DeepEquals(item, payload));
        }

        var type = GetNonNullType(schema);
        switch (type)
        {
            case "object":
                Assert.Equal(JsonValueKind.Object, payload.ValueKind);
                if (schema.TryGetProperty("required", out var required))
                {
                    foreach (var requiredName in required.EnumerateArray().Select(item => item.GetString()!))
                    {
                        Assert.True(
                            payload.TryGetProperty(requiredName, out _),
                            $"{path}.{requiredName} is required by OpenAPI but absent at runtime.");
                    }
                }

                if (schema.TryGetProperty("properties", out var properties))
                {
                    foreach (var property in properties.EnumerateObject())
                    {
                        if (payload.TryGetProperty(property.Name, out var value))
                        {
                            AssertPayloadMatchesSchema(
                                value,
                                property.Value,
                                schemas,
                                $"{path}.{property.Name}");
                        }
                    }
                }

                break;
            case "array":
                Assert.Equal(JsonValueKind.Array, payload.ValueKind);
                if (schema.TryGetProperty("items", out var items))
                {
                    foreach (var item in payload.EnumerateArray())
                    {
                        AssertPayloadMatchesSchema(item, items, schemas, $"{path}[]");
                    }
                }

                break;
            case "string":
                Assert.Equal(JsonValueKind.String, payload.ValueKind);
                break;
            case "integer":
                Assert.Equal(JsonValueKind.Number, payload.ValueKind);
                Assert.True(payload.TryGetInt64(out _), $"{path} was not an integer.");
                break;
            case "number":
                Assert.Equal(JsonValueKind.Number, payload.ValueKind);
                break;
            case "boolean":
                Assert.True(
                    payload.ValueKind is JsonValueKind.True or JsonValueKind.False,
                    $"{path} was not a boolean.");
                break;
        }
    }

    private static string? GetNonNullType(JsonElement schema)
    {
        if (!schema.TryGetProperty("type", out var type))
        {
            return null;
        }

        return type.ValueKind switch
        {
            JsonValueKind.String => type.GetString(),
            JsonValueKind.Array => type
                .EnumerateArray()
                .Select(item => item.GetString())
                .FirstOrDefault(value => value != "null"),
            _ => null
        };
    }

    private sealed record OperationContract(
        string Path,
        string Method,
        IReadOnlyList<ResponseContract> Successes,
        IReadOnlyList<string> ErrorStatuses);

    private sealed record ResponseContract(
        string Status,
        string? ExpectedSchema,
        string? ExpectedFormat);
}
