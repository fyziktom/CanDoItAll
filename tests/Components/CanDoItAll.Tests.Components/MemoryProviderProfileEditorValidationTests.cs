using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Http;
using CanDoItAll.Memory.Mcp;
using CanDoItAll.Modules.Memory.Services;
using static CanDoItAll.Tests.Components.MemoryProviderProfileEditorTestData;

namespace CanDoItAll.Tests.Components;

public sealed class MemoryProviderProfileEditorValidationTests
{
    private readonly MemoryProviderProfileEditorMapper mapper = new();

    [Fact]
    public void Legacy_raw_credential_requires_explicit_environment_reference_before_save()
    {
        var profile = CreateProfile(
            MemoryProviderDriverKind.Http,
            [
                (HttpMemoryProviderConfigurationKeys.BaseUrl, String("https://memory.example.test")),
                (HttpMemoryProviderConfigurationKeys.LegacyRawApiKey, String("do-not-persist-this-secret"))
            ],
            [MemoryCapabilityIds.ContextQuerySync]);
        var editor = mapper.FromProfile(MemoryProviderManagementProfile.FromProfile(profile));

        var exception = Assert.Throws<InvalidOperationException>(() => mapper.ToProfile(editor));

        Assert.Contains("legacy raw credential", exception.Message, StringComparison.OrdinalIgnoreCase);
        editor.Http.ApiKeyEnvironmentVariable = "CANDOITALL_MIGRATED_MEMORY_KEY";
        var migrated = mapper.ToProfile(editor);
        Assert.False(migrated.Manifest.Extensions.Values.ContainsKey(HttpMemoryProviderConfigurationKeys.LegacyRawApiKey));
        Assert.DoesNotContain("do-not-persist-this-secret", SerializeExtensions(migrated), StringComparison.Ordinal);
    }

    [Fact]
    public void Secret_value_is_rejected_when_environment_variable_name_is_expected()
    {
        var editor = CreateHttpEditor();
        editor.Http.ApiKeyEnvironmentVariable = "Bearer actual-secret-value";

        var exception = Assert.Throws<ArgumentException>(() => mapper.ToProfile(editor));

        Assert.Contains("environment variable name", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Insecure_remote_endpoint_and_unsupported_capability_claim_are_rejected()
    {
        var editor = CreateHttpEditor();
        editor.Http.BaseUrl = "http://memory.example.test";
        Assert.Throws<InvalidOperationException>(() => mapper.ToProfile(editor));

        editor.Http.BaseUrl = "https://memory.example.test";
        editor.SupportsContextQueryAsync = true;
        var exception = Assert.Throws<InvalidOperationException>(() => mapper.ToProfile(editor));
        Assert.Contains(MemoryCapabilityIds.ContextQueryAsync.Value, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Secret_bearing_transport_and_provider_ui_urls_are_rejected_before_save()
    {
        var httpEditor = CreateHttpEditor();
        httpEditor.Http.BaseUrl = "https://memory.example.test?token=secret";
        Assert.Throws<InvalidOperationException>(() => mapper.ToProfile(httpEditor));

        var mcpEditor = CreateMcpEditor();
        mcpEditor.Mcp.RemoteEndpoint = "https://memory.example.test/mcp#secret";
        Assert.Throws<InvalidOperationException>(() => mapper.ToProfile(mcpEditor));

        var uiEditor = CreateHttpEditor();
        uiEditor.SupportsIframeUi = true;
        uiEditor.ProviderUiUrl = "https://memory.example.test/console?token=secret";
        var exception = Assert.Throws<InvalidOperationException>(() => mapper.ToProfile(uiEditor));

        Assert.Contains("query strings", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Mcp_internal_hosted_and_async_without_status_tool_are_rejected()
    {
        var editor = CreateMcpEditor();
        editor.Mcp.DescriptorKind = McpMemoryProviderDescriptorKinds.InternalHosted;
        Assert.Throws<InvalidOperationException>(() => mapper.ToProfile(editor));

        editor.Mcp.DescriptorKind = McpMemoryProviderDescriptorKinds.RemoteHttp;
        editor.SupportsContextQueryAsync = true;
        editor.Mcp.OperationStatusTool = string.Empty;
        var exception = Assert.Throws<InvalidOperationException>(() => mapper.ToProfile(editor));
        Assert.Contains("operation-status tool", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Non_executable_driver_and_mock_overclaims_are_rejected()
    {
        var editor = CreateHttpEditor();
        editor.DriverKind = MemoryProviderDriverKind.InProcessMigration;
        Assert.Throws<InvalidOperationException>(() => mapper.ToProfile(editor));

        editor = new MemoryProviderProfileEditorModel
        {
            DriverKind = MemoryProviderDriverKind.Mock,
            SupportsContextQuerySync = true,
            SupportsImmediateFeedback = true
        };
        var exception = Assert.Throws<InvalidOperationException>(() => mapper.ToProfile(editor));
        Assert.Contains(MemoryCapabilityIds.FeedbackImmediate.Value, exception.Message, StringComparison.Ordinal);

        editor.SupportsImmediateFeedback = false;
        Assert.Equal(MemoryProviderDriverKind.Mock, mapper.ToProfile(editor).DriverKind);
    }

    [Fact]
    public void Native_remote_rejects_non_token_authorization_scheme_before_save()
    {
        var editor = CreateHttpEditor();
        editor.DriverKind = MemoryProviderDriverKind.NativeRemote;
        editor.Http.AuthScheme = "Bearer injected\r\nX-Leak: value";

        var exception = Assert.Throws<ArgumentException>(() => mapper.ToProfile(editor));

        Assert.Contains("RFC HTTP token", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Mcp_legacy_event_poll_configuration_is_not_persisted_or_advertised()
    {
        var profile = CreateProfile(
            MemoryProviderDriverKind.Mcp,
            [
                (McpMemoryProviderConfigurationKeys.DescriptorKind, String(McpMemoryProviderDescriptorKinds.RemoteHttp)),
                (McpMemoryProviderConfigurationKeys.ServerKey, String("memory-test")),
                (McpMemoryProviderConfigurationKeys.RemoteEndpoint, String("https://mcp-memory.example.test")),
                (McpMemoryProviderConfigurationKeys.ContextQueryTool, String("memory_query")),
                (McpMemoryProviderConfigurationKeys.EventPollTool, String("legacy_events"))
            ],
            [MemoryCapabilityIds.ContextQuerySync]);

        var saved = mapper.ToProfile(mapper.FromProfile(MemoryProviderManagementProfile.FromProfile(profile)));

        Assert.False(saved.Manifest.Extensions.Values.ContainsKey(McpMemoryProviderConfigurationKeys.EventPollTool));
        Assert.DoesNotContain(
            saved.Manifest.Capabilities,
            capability => capability.Id == MemoryCapabilityIds.EventsHostPoll && capability.Supported);
    }

    [Fact]
    public void Single_workspace_scope_is_rejected_until_scoped_routing_exists()
    {
        var editor = CreateHttpEditor();
        editor.WorkspaceScope = MemoryProviderWorkspaceScope.SingleWorkspace;

        var exception = Assert.Throws<InvalidOperationException>(() => mapper.ToProfile(editor));

        Assert.Contains("workspace-bound provider routing is not implemented", exception.Message, StringComparison.Ordinal);
    }
}
