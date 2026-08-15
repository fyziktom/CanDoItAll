using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Capabilities.Templates;
using CanDoItAll.AgentFramework.Mcp;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using AccessCapabilityKind = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityKind;
using ModelCapabilityKind = CanDoItAll.AgentFramework.Models.CapabilityKind;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class AgentCapabilitySetupApiIntegrationTests
{
    [Fact]
    public async Task Capability_setup_api_host_registers_live_mcp_setup_runtime()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        await using var scope = host.App.Services.CreateAsyncScope();

        var clientFactory = scope.ServiceProvider.GetRequiredService<IMcpClientFactory>();
        var setupService = scope.ServiceProvider.GetRequiredService<IMcpSetupTestService>();

        Assert.IsType<LocalStdioMcpClientFactory>(clientFactory);
        Assert.IsType<McpSetupTestService>(setupService);
    }

    [Fact]
    public async Task Capability_setup_api_returns_typed_negative_setup_and_access_diagnostics()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            configureServices: services =>
            {
                services.RemoveAll<IMcpClientFactory>();
                services.RemoveAll<IMcpSetupTestService>();
                services.AddSingleton<IMcpClientFactory>(_ => new FakeMcpClientFactory(new FakeMcpServerScript(
                    Tools: [],
                    ListToolsException: new McpSetupException(
                        CapabilityDiagnosticCategory.McpListTools,
                        "$.listTools",
                        "tools/list failed with token=raw-secret-value",
                        "Fix the MCP server list-tools handler."))));
                services.AddSingleton<IMcpSetupTestService, McpSetupTestService>();
            });

        var toolResponse = await host.Client.PostAsJsonAsync(
            "/api/agents/capabilities/setup-tests/tool",
            new CapabilityToolSetupTestRequest
            {
                Capability = CreateExternalToolCapability("api-external-audit"),
                JsonInput = "{not-json",
                CorrelationId = "API_TOOL"
            });
        var toolResult = await ReadJsonAsync<CapabilitySetupTestResult>(toolResponse);

        var mcpResponse = await host.Client.PostAsJsonAsync(
            "/api/agents/capabilities/setup-tests/mcp",
            new CapabilityMcpSetupTestRequest
            {
                Capability = CreateRemoteMcpCapability("api-browser-mcp"),
                CorrelationId = "API_MCP"
            });
        var mcpResult = await ReadJsonAsync<McpSetupTestResult>(mcpResponse);

        var invalidPolicyResponse = await host.Client.PostAsJsonAsync(
            "/api/agents/capabilities/access-preview",
            new CapabilityAccessPreviewRequest
            {
                DraftCapabilities = [CreateExternalToolCapability("api-external-audit")],
                Policy = new CapabilityAccessPolicyTemplateDto
                {
                    Rules =
                    [
                        new CapabilityAccessRuleTemplateDto
                        {
                            Id = "deny-invalid-key",
                            Effect = "deny",
                            Scope = "processStep",
                            Selector = new CapabilitySelectorTemplateDto
                            {
                                Kind = "capabilityKey",
                                Value = "bad key"
                            },
                            Reason = "Invalid selector proof."
                        }
                    ]
                },
                CorrelationId = "API_INVALID_POLICY"
            });
        using var invalidPolicyJson = await ReadJsonDocumentAsync(invalidPolicyResponse);

        var deniedRequiredResponse = await host.Client.PostAsJsonAsync(
            "/api/agents/capabilities/access-preview",
            new CapabilityAccessPreviewRequest
            {
                DraftCapabilities = [CreateExternalToolCapability("api-required-audit")],
                RequiredCapabilities =
                [
                    new CapabilityIdentityEditorModel
                    {
                        Kind = AccessCapabilityKind.Tool,
                        Key = "api-required-audit"
                    }
                ],
                Policy = new CapabilityAccessPolicyTemplateDto
                {
                    Rules =
                    [
                        new CapabilityAccessRuleTemplateDto
                        {
                            Id = "deny-required-audit",
                            Effect = "deny",
                            Scope = "processStep",
                            Selector = new CapabilitySelectorTemplateDto
                            {
                                Kind = "capabilityKey",
                                Value = "api-required-audit"
                            },
                            Reason = "Required tool is denied by process policy."
                        }
                    ]
                },
                CorrelationId = "API_REQUIRED_DENIED"
            });
        using var deniedRequiredJson = await ReadJsonDocumentAsync(deniedRequiredResponse);

        Assert.False(toolResult.IsSuccess);
        Assert.Contains(toolResult.Diagnostics, diagnostic =>
            diagnostic.Category == CapabilityDiagnosticCategory.JsonParse &&
            diagnostic.FieldPath == "$.jsonInput" &&
            diagnostic.CorrelationId == "API_TOOL");

        Assert.False(mcpResult.IsSuccess);
        Assert.True(mcpResult.CleanupCompleted);
        Assert.Contains(mcpResult.Diagnostics, diagnostic =>
            diagnostic.Category == CapabilityDiagnosticCategory.McpListTools &&
            diagnostic.FieldPath == "$.listTools" &&
            !diagnostic.MaskedDetail.Contains("raw-secret-value", StringComparison.Ordinal));

        Assert.False(invalidPolicyJson.RootElement
            .GetProperty("validationResult")
            .GetProperty("isValid")
            .GetBoolean());
        Assert.Contains(
            invalidPolicyJson.RootElement
                .GetProperty("validationResult")
                .GetProperty("issues")
                .EnumerateArray(),
            issue => EnumPropertyEquals(issue, "category", CapabilityDiagnosticCategory.AccessPolicy) &&
                     StringPropertyEquals(issue, "fieldPath", "$.rules[0].selector.value"));

        Assert.True(deniedRequiredJson.RootElement
            .GetProperty("validationResult")
            .GetProperty("isValid")
            .GetBoolean());
        Assert.DoesNotContain(
            deniedRequiredJson.RootElement
                .GetProperty("effectiveSet")
                .GetProperty("allowedCapabilities")
                .EnumerateArray(),
            capability => capability.TryGetProperty("identity", out var identity) &&
                          IdentityEquals(identity, AccessCapabilityKind.Tool, "api-required-audit"));
        Assert.Contains(
            deniedRequiredJson.RootElement
                .GetProperty("effectiveSet")
                .GetProperty("diagnostics")
                .EnumerateArray(),
            diagnostic => IdentityPropertyEquals(diagnostic, AccessCapabilityKind.Tool, "api-required-audit") &&
                          EnumPropertyEquals(diagnostic, "category", CapabilityDiagnosticCategory.RequiredCapabilityDenied) &&
                          ValueObjectPropertyEquals(diagnostic, "ruleId", "deny-required-audit") &&
                          StringPropertyEquals(diagnostic, "correlationId", "API_REQUIRED_DENIED"));
    }

    private static CapabilityEditorModel CreateExternalToolCapability(string key)
    {
        return new CapabilityEditorModel
        {
            Kind = ModelCapabilityKind.Tool,
            Key = key,
            Name = key,
            Description = "API regression external tool.",
            EndpointOrPath = "dotnet",
            Tags = ["external"],
            ConfigurationJson = """
            {
              "toolKind": "externalProcess",
              "runtimeToolName": "external_audit",
              "implementationKey": "external.audit",
              "operationClassifications": [ "externalAction" ],
              "externalProcess": {
                "command": "dotnet",
                "workingDirectory": ".",
                "allowedExecutableNames": [ "dotnet" ],
                "requiredOutputProperties": [ "ok" ]
              }
            }
            """
        };
    }

    private static CapabilityEditorModel CreateRemoteMcpCapability(string key)
    {
        return new CapabilityEditorModel
        {
            Kind = ModelCapabilityKind.McpServer,
            Key = key,
            Name = key,
            Description = "API regression remote MCP.",
            ConfigurationJson = """
            {
              "transport": "remote-http",
              "serverName": "api-browser-mcp",
              "endpoint": "https://example.test/mcp",
              "allowedTools": [ "browser_snapshot" ]
            }
            """
        };
    }

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<T>(body, JsonOptions())
            ?? throw new InvalidOperationException($"Response body could not be deserialized as {typeof(T).Name}.");
    }

    private static async Task<JsonDocument> ReadJsonDocumentAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonDocument.Parse(body);
    }

    private static bool IdentityPropertyEquals(JsonElement diagnostic, AccessCapabilityKind kind, string key)
    {
        if (!diagnostic.TryGetProperty("identity", out var identity))
        {
            return false;
        }

        return IdentityEquals(identity, kind, key);
    }

    private static bool IdentityEquals(JsonElement identity, AccessCapabilityKind kind, string key)
    {
        return EnumPropertyEquals(identity, "kind", kind) &&
               ValueObjectPropertyEquals(identity, "key", key);
    }

    private static bool ValueObjectPropertyEquals(JsonElement element, string propertyName, string expectedValue)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            return string.Equals(property.GetString(), expectedValue, StringComparison.Ordinal);
        }

        return property.ValueKind == JsonValueKind.Object &&
               property.TryGetProperty("value", out var value) &&
               string.Equals(value.GetString(), expectedValue, StringComparison.Ordinal);
    }

    private static bool StringPropertyEquals(JsonElement element, string propertyName, string expectedValue)
    {
        return element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String &&
               string.Equals(property.GetString(), expectedValue, StringComparison.Ordinal);
    }

    private static bool EnumPropertyEquals<TEnum>(JsonElement element, string propertyName, TEnum expected)
        where TEnum : struct, Enum
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            return string.Equals(property.GetString(), expected.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        return property.ValueKind == JsonValueKind.Number &&
               property.TryGetInt32(out var numericValue) &&
               numericValue == Convert.ToInt32(expected);
    }

    private static JsonSerializerOptions JsonOptions()
    {
        return new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }
}
