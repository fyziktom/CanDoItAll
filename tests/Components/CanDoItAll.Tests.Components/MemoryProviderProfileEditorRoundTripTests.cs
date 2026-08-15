using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Http;
using CanDoItAll.Memory.Mcp;
using CanDoItAll.Modules.Memory.Services;
using static CanDoItAll.Tests.Components.MemoryProviderProfileEditorTestData;

namespace CanDoItAll.Tests.Components.Memory;

public sealed class MemoryProviderProfileEditorRoundTripTests
{
    private readonly MemoryProviderProfileEditorMapper mapper = new();

    [Fact]
    public void Http_profile_round_trip_is_lossless_and_uses_environment_credential_reference()
    {
        var profile = CreateProfile(
            MemoryProviderDriverKind.Http,
            [
                (HttpMemoryProviderConfigurationKeys.BaseUrl, String("https://memory.example.test")),
                (HttpMemoryProviderConfigurationKeys.QueryPath, String("/v1/query")),
                (HttpMemoryProviderConfigurationKeys.HealthPath, String("/healthz")),
                (HttpMemoryProviderConfigurationKeys.ApiKeyEnvironmentVariable, String("CANDOITALL_HTTP_MEMORY_KEY")),
                (HttpMemoryProviderConfigurationKeys.AuthHeaderName, String("Authorization")),
                (HttpMemoryProviderConfigurationKeys.AuthScheme, String("Bearer")),
                (HttpMemoryProviderConfigurationKeys.TimeoutMilliseconds, Number(12_000)),
                (HttpMemoryProviderConfigurationKeys.MaxRetryAttempts, Number(2)),
                (MemoryProviderUiSurfaceKeys.ProviderVendorUiUrlExtension, String("https://memory.example.test/console")),
                ("provider.vendor.customSettings", Json(new { region = "west", weight = 3 }))
            ],
            [MemoryCapabilityIds.ContextQuerySync, MemoryCapabilityIds.UiIframe]);

        var roundTripped = mapper.ToProfile(mapper.FromProfile(MemoryProviderManagementProfile.FromProfile(profile)));

        AssertLosslessManifest(profile, roundTripped);
        AssertExtension(roundTripped, HttpMemoryProviderConfigurationKeys.ApiKeyEnvironmentVariable, "CANDOITALL_HTTP_MEMORY_KEY");
        Assert.False(roundTripped.Manifest.Extensions.Values.ContainsKey(HttpMemoryProviderConfigurationKeys.LegacyRawApiKey));
    }

    [Fact]
    public void Native_remote_profile_round_trip_preserves_native_transport_fields()
    {
        var profile = CreateProfile(
            MemoryProviderDriverKind.NativeRemote,
            [
                (NativeRemoteMemoryProviderConfigurationKeys.ServiceBaseUrl, String("https://native-memory.example.test")),
                (NativeRemoteMemoryProviderConfigurationKeys.QueryPath, String("/memory/query")),
                (NativeRemoteMemoryProviderConfigurationKeys.HealthPath, String("/memory/health")),
                (NativeRemoteMemoryProviderConfigurationKeys.ApiKeyEnvironmentVariable, String("CANDOITALL_NATIVE_MEMORY_KEY")),
                (NativeRemoteMemoryProviderConfigurationKeys.AuthHeaderName, String("X-Memory-Key")),
                (NativeRemoteMemoryProviderConfigurationKeys.AuthScheme, String("Key")),
                (NativeRemoteMemoryProviderConfigurationKeys.TimeoutMilliseconds, Number(21_000)),
                (NativeRemoteMemoryProviderConfigurationKeys.MaxRetryAttempts, Number(1)),
                ("provider.vendor.customSettings", Json(new[] { "one", "two" }))
            ],
            [MemoryCapabilityIds.ContextQuerySync]);

        var roundTripped = mapper.ToProfile(mapper.FromProfile(MemoryProviderManagementProfile.FromProfile(profile)));

        AssertLosslessManifest(profile, roundTripped);
        AssertExtension(roundTripped, NativeRemoteMemoryProviderConfigurationKeys.ApiKeyEnvironmentVariable, "CANDOITALL_NATIVE_MEMORY_KEY");
        Assert.False(roundTripped.Manifest.Extensions.Values.ContainsKey(NativeRemoteMemoryProviderConfigurationKeys.LegacyRawApiKey));
    }

    [Fact]
    public void Mcp_remote_http_profile_round_trip_preserves_executable_tools_and_complete_header_reference()
    {
        var profile = CreateProfile(
            MemoryProviderDriverKind.Mcp,
            [
                (McpMemoryProviderConfigurationKeys.DescriptorKind, String(McpMemoryProviderDescriptorKinds.RemoteHttp)),
                (McpMemoryProviderConfigurationKeys.ServerKey, String("memory-remote")),
                (McpMemoryProviderConfigurationKeys.DisplayName, String("Remote MCP memory")),
                (McpMemoryProviderConfigurationKeys.Description, String("External memory MCP")),
                (McpMemoryProviderConfigurationKeys.RemoteEndpoint, String("https://mcp-memory.example.test")),
                (McpMemoryProviderConfigurationKeys.AuthHeaderName, String("Authorization")),
                (McpMemoryProviderConfigurationKeys.AuthHeaderEnvironmentVariable, String("CANDOITALL_MCP_AUTH_HEADER")),
                (McpMemoryProviderConfigurationKeys.ContextQueryTool, String("memory_query")),
                (McpMemoryProviderConfigurationKeys.OperationStatusTool, String("memory_status")),
                ("provider.vendor.customSettings", Json(new { tenant = "alpha" }))
            ],
            [
                MemoryCapabilityIds.ContextQuerySync,
                MemoryCapabilityIds.ContextQueryAsync,
                MemoryCapabilityIds.OperationStatus
            ]);

        var roundTripped = mapper.ToProfile(mapper.FromProfile(MemoryProviderManagementProfile.FromProfile(profile)));

        AssertLosslessManifest(profile, roundTripped);
        AssertExtension(roundTripped, McpMemoryProviderConfigurationKeys.AuthHeaderEnvironmentVariable, "CANDOITALL_MCP_AUTH_HEADER");
        AssertExtension(roundTripped, McpMemoryProviderConfigurationKeys.DescriptorKind, McpMemoryProviderDescriptorKinds.RemoteHttp);
    }
}
