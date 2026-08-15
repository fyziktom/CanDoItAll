using System.Reflection;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Modules.AgentFramework;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ProviderProfileSaveValidationTests
{
    [Theory]
    [InlineData("empty-id")]
    [InlineData("blank-name")]
    [InlineData("blank-base-url")]
    [InlineData("relative-base-url")]
    [InlineData("base-url-user-info")]
    [InlineData("base-url-query")]
    [InlineData("base-url-fragment")]
    [InlineData("malformed-configuration")]
    [InlineData("non-object-configuration")]
    [InlineData("undefined-kind")]
    [InlineData("undefined-transport")]
    [InlineData("undefined-purpose")]
    [InlineData("incompatible-kind-transport")]
    public async Task Core_registry_rejects_invalid_editor_before_catalog_mutation(
        string scenario)
    {
        var store = CreateCatalogStore();
        var registry = new WorkspaceBackedProviderProfileRegistry(
            store.Service,
            new ProviderProfileService());
        var editor = CreateValidEditor();
        ApplyInvalidScenario(editor, scenario);

        await Assert.ThrowsAsync<ProviderProfileValidationException>(
            () => registry.SaveProviderAsync(editor));

        Assert.Equal(0, store.Proxy.UpdateCatalogCallCount);
        Assert.Empty(store.Proxy.Catalog.Providers);
    }

    [Theory]
    [InlineData("scenario://harness")]
    [InlineData("process-mock://agents")]
    public async Task Core_registry_accepts_absolute_first_party_custom_scheme(
        string baseUrl)
    {
        var store = CreateCatalogStore();
        var registry = new WorkspaceBackedProviderProfileRegistry(
            store.Service,
            new ProviderProfileService());
        var editor = CreateValidEditor();
        editor.BaseUrl = baseUrl;

        var providerId = await registry.SaveProviderAsync(editor);
        var provider = Assert.Single(store.Proxy.Catalog.Providers);

        Assert.Equal(providerId, provider.Id);
        Assert.Equal(baseUrl, provider.BaseUrl);
        Assert.Equal(1, store.Proxy.UpdateCatalogCallCount);
    }

    [Theory]
    [InlineData(
        "{\"connectorPluginKey\":\"provider.openai\",\"ConnectorPluginKey\":\"provider.ollama\"}",
        "connectorPluginKey")]
    [InlineData(
        "{\"configSchemaVersion\":\"1\",\"ConfigSchemaVersion\":\"2\"}",
        "configSchemaVersion")]
    [InlineData(
        "{\"secretRecordId\":\"72d10cca-63f7-4f6f-8055-938c2df2c170\",\"SecretRecordId\":\"68e90ef6-a111-4dcc-9ff5-33bd2a40bd80\"}",
        "secretRecordId")]
    [InlineData(
        "{\"timeoutSeconds\":45,\"TimeoutSeconds\":90}",
        "timeoutSeconds")]
    [InlineData(
        "{\"agentFrameworkProviderKind\":\"OpenAi\",\"AgentFrameworkProviderKind\":\"Ollama\"}",
        "agentFrameworkProviderKind")]
    [InlineData(
        "{\"providerTransport\":\"Responses\",\"ProviderTransport\":\"ChatCompletions\"}",
        "providerTransport")]
    [InlineData(
        "{\"providerPurpose\":\"Chat\",\"ProviderPurpose\":\"ImageGeneration\"}",
        "providerPurpose")]
    [InlineData(
        "{\"tags\":[\"planning\"],\"Tags\":[\"brainstorming\"]}",
        "tags")]
    public void Strict_metadata_reader_rejects_case_insensitive_duplicate_aliases(
        string configurationJson,
        string canonicalPropertyName)
    {
        var editor = CreateValidEditor();
        editor.ConfigurationJson = configurationJson;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AgentFrameworkProviderMetadata.ResolveConnectorPluginKey(
                editor,
                current: null));

        Assert.Contains(
            canonicalPropertyName,
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Metadata_writer_canonicalizes_single_case_insensitive_aliases()
    {
        var secretRecordId = Guid.NewGuid();
        var configurationJson = $$"""
            {
              "ConnectorPluginKey": "legacy.connector",
              "ConfigSchemaVersion": "legacy",
              "SecretRecordId": "{{Guid.NewGuid():D}}",
              "TimeoutSeconds": 90,
              "AgentFrameworkProviderKind": "Ollama",
              "ProviderTransport": "ChatCompletions",
              "ProviderPurpose": "ImageGeneration",
              "Tags": ["Legacy"],
              "customSetting": true
            }
            """;

        var result = AgentFrameworkProviderMetadata.BuildExtraSettingsJson(
            configurationJson,
            "provider.openai",
            "1",
            secretRecordId,
            45,
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            ProviderProfilePurpose.Chat,
            [],
            ["Planning"]);
        using var document = JsonDocument.Parse(result);
        var properties = document.RootElement
            .EnumerateObject()
            .ToArray();
        string[] canonicalPropertyNames =
        [
            "connectorPluginKey",
            "configSchemaVersion",
            "secretRecordId",
            "timeoutSeconds",
            "agentFrameworkProviderKind",
            "providerTransport",
            "providerPurpose",
            "tags"
        ];

        foreach (var canonicalPropertyName in canonicalPropertyNames)
        {
            Assert.Single(
                properties,
                property => string.Equals(
                    property.Name,
                    canonicalPropertyName,
                    StringComparison.OrdinalIgnoreCase));
            Assert.Contains(
                properties,
                property => string.Equals(
                    property.Name,
                    canonicalPropertyName,
                    StringComparison.Ordinal));
        }

        Assert.Equal(
            "provider.openai",
            document.RootElement.GetProperty("connectorPluginKey").GetString());
        Assert.Equal(
            secretRecordId.ToString("D"),
            document.RootElement.GetProperty("secretRecordId").GetString());
        Assert.Equal(
            "OpenAi",
            document.RootElement.GetProperty(
                "agentFrameworkProviderKind").GetString());
        Assert.Equal(
            "Responses",
            document.RootElement.GetProperty("providerTransport").GetString());
        Assert.Equal(
            "Chat",
            document.RootElement.GetProperty("providerPurpose").GetString());
        Assert.Equal(
            "planning",
            document.RootElement.GetProperty("tags")[0].GetString());
        Assert.True(document.RootElement.GetProperty("customSetting").GetBoolean());
    }

    [Fact]
    public void Metadata_secret_resolver_rejects_conflicting_explicit_sources()
    {
        var configuredSecretRecordId = Guid.NewGuid();
        var inlineSecretRecordId = Guid.NewGuid();
        var editor = CreateValidEditor();
        editor.ConfigurationJson =
            $$"""{"secretRecordId":"{{configuredSecretRecordId:D}}"}""";
        editor.ApiKeyEnvironmentVariable =
            $"secret:{inlineSecretRecordId:D}";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AgentFrameworkProviderMetadata.ResolveSecretRecordId(editor));

        Assert.Contains(
            "conflicting explicit secret record references",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Metadata_read_path_does_not_hide_corrupt_persisted_json()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            AgentFrameworkProviderMetadata.ReadTags("[]"));

        Assert.Contains(
            "JSON object",
            exception.Message,
            StringComparison.Ordinal);
    }

    private static ProviderProfileEditorModel CreateValidEditor()
    {
        return new ProviderProfileEditorModel
        {
            Name = "Provider validation probe",
            Kind = ProviderKind.OpenAi,
            BaseUrl = "https://api.example.test/v1",
            ApiKeyEnvironmentVariable = "PROVIDER_TEST_API_KEY",
            DefaultModel = "test-model",
            Transport = ProviderTransportKind.Responses,
            Purpose = ProviderProfilePurpose.Chat,
            ConfigurationJson = "{}"
        };
    }

    private static void ApplyInvalidScenario(
        ProviderProfileEditorModel editor,
        string scenario)
    {
        switch (scenario)
        {
            case "empty-id":
                editor.Id = Guid.Empty;
                break;
            case "blank-name":
                editor.Name = " ";
                break;
            case "blank-base-url":
                editor.BaseUrl = " ";
                break;
            case "relative-base-url":
                editor.BaseUrl = "api.example.test/v1";
                break;
            case "base-url-user-info":
                editor.BaseUrl = "https://user@example.test/v1";
                break;
            case "base-url-query":
                editor.BaseUrl = "https://api.example.test/v1?tenant=unsafe";
                break;
            case "base-url-fragment":
                editor.BaseUrl = "https://api.example.test/v1#unsafe";
                break;
            case "malformed-configuration":
                editor.ConfigurationJson = "{";
                break;
            case "non-object-configuration":
                editor.ConfigurationJson = "[]";
                break;
            case "undefined-kind":
                editor.Kind = (ProviderKind)int.MaxValue;
                break;
            case "undefined-transport":
                editor.Transport = (ProviderTransportKind)int.MaxValue;
                break;
            case "undefined-purpose":
                editor.Purpose = (ProviderProfilePurpose)int.MaxValue;
                break;
            case "incompatible-kind-transport":
                editor.Kind = ProviderKind.Ollama;
                editor.Transport = ProviderTransportKind.Responses;
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(scenario),
                    scenario,
                    "Unknown provider validation scenario.");
        }
    }

    private static CatalogStoreDependency CreateCatalogStore()
    {
        var service = DispatchProxy.Create<
            ISandboxWorkspaceStore,
            CatalogWorkspaceStoreProxy>();
        return new CatalogStoreDependency(
            service,
            (CatalogWorkspaceStoreProxy)(object)service);
    }

    private sealed record CatalogStoreDependency(
        ISandboxWorkspaceStore Service,
        CatalogWorkspaceStoreProxy Proxy);

    private class CatalogWorkspaceStoreProxy : DispatchProxy
    {
        private int updateCatalogCallCount;

        public int UpdateCatalogCallCount =>
            Volatile.Read(ref updateCatalogCallCount);

        public SandboxWorkspaceCatalog Catalog { get; private set; } =
            SandboxWorkspaceDocument.Empty.ToCatalog();

        protected override object? Invoke(
            MethodInfo? targetMethod,
            object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            ArgumentNullException.ThrowIfNull(args);

            if (targetMethod.Name ==
                    nameof(ISandboxWorkspaceStore.UpdateCatalogAsync) &&
                args[0] is Func<
                    SandboxWorkspaceCatalog,
                    SandboxWorkspaceCatalog> update)
            {
                Interlocked.Increment(ref updateCatalogCallCount);
                Catalog = update(Catalog);
                return Task.FromResult(Catalog);
            }

            if (targetMethod.Name ==
                nameof(ISandboxWorkspaceStore.LoadCatalogAsync))
            {
                return Task.FromResult(Catalog);
            }

            throw new InvalidOperationException(
                $"Workspace store member '{targetMethod.Name}' was not expected.");
        }
    }
}
