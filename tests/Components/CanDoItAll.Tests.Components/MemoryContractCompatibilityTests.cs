using System.Text.Json;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Http;
using CanDoItAll.Memory.Mcp;
using CanDoItAll.Modules.Memory.Services;
using static CanDoItAll.Tests.Components.MemoryProviderProfileEditorTestData;

namespace CanDoItAll.Tests.Components.Memory;

public sealed class MemoryContractCompatibilityTests {
    [Fact]
    public void Data_only_defaults_match_the_real_transport_constants() {
        Assert.Equal(HttpMemoryProviderEndpoints.Query, new MemoryProviderHttpTransportEditorModel().QueryPath);
        Assert.Equal(HttpMemoryProviderEndpoints.Health, new MemoryProviderHttpTransportEditorModel().HealthPath);
        Assert.Equal(McpMemoryProviderDescriptorKinds.RemoteHttp, new MemoryProviderMcpTransportEditorModel().DescriptorKind);
        Assert.Equal(HttpMemoryProviderConfigurationKeys.LegacyRawApiKey, MemoryEditorDefaults.LegacyHttpCredential);
        Assert.Equal(NativeRemoteMemoryProviderConfigurationKeys.LegacyRawApiKey, MemoryEditorDefaults.LegacyNativeCredential);
    }

    [Fact]
    public void Profile_snapshot_never_contains_legacy_credential_values_and_keeps_migration_keys() {
        var profile = CreateProfile(MemoryProviderDriverKind.Mock,
            [(MemoryEditorDefaults.LegacyHttpCredential, String("fixture-private-http-value")),
             (MemoryEditorDefaults.LegacyNativeCredential, String("fixture-private-native-value")),
             ("provider.vendor.customSettings", Json(new { enabled = true }))],
            [MemoryCapabilityIds.ContextQuerySync]);
        var view = MemoryProviderManagementProfile.FromProfile(profile);
        var editor = new MemoryProviderProfileEditorMapper().FromProfile(view);
        var serialized = JsonSerializer.Serialize(new { view, editor });
        Assert.DoesNotContain("fixture-private-http-value", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("fixture-private-native-value", serialized, StringComparison.Ordinal);
        Assert.Equal(2, editor.LegacyRawCredentialKeys.Count);
        Assert.True(editor.PreservedExtensions.Values["provider.vendor.customSettings"].GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public void Detached_capture_preserves_nested_objects_collections_and_JSON_after_document_disposal() {
        var editor = CreateHttpEditor();
        using (var document = JsonDocument.Parse("{\"nested\":[1,2,3]}")) {
            editor.PreservedExtensions = MemoryExtensionData.From([("provider.vendor.customSettings", document.RootElement)]);
        }
        editor.SelectionTags = ["original"];
        editor.Mcp.ContextQueryTool = "original_tool";
        MemoryCapabilityDescriptor[] capabilities = [new(MemoryCapabilityIds.ContextQuerySync, "4", true)];
        editor.PreservedCapabilities = capabilities;
        var captured = editor.Capture();
        editor.Http.BaseUrl = "https://changed.example/";
        editor.Mcp.ContextQueryTool = "changed_tool";
        editor.SelectionTags.Clear();
        capabilities[0] = new(MemoryCapabilityIds.UiIframe, "1", true);
        Assert.Equal("https://memory.example.test", captured.Http.BaseUrl);
        Assert.Equal("original_tool", captured.Mcp.ContextQueryTool);
        Assert.Equal(["original"], captured.SelectionTags);
        Assert.Equal(MemoryCapabilityIds.ContextQuerySync, captured.PreservedCapabilities[0].Id);
        Assert.Equal(3, captured.PreservedExtensions.Values["provider.vendor.customSettings"].GetProperty("nested").GetArrayLength());
    }

    [Fact]
    public void Unchanged_capabilities_preserve_distinct_interaction_metadata_instead_of_recomputing_it() {
        var profile = CreateProfile(MemoryProviderDriverKind.Mock, [("provider.vendor.customSettings", Json(new { retained = true }))], [MemoryCapabilityIds.ContextQuerySync]);
        profile = profile with { Manifest = profile.Manifest with { InteractionSupport = new(false, false, false, false, false) } };
        var mapper = new MemoryProviderProfileEditorMapper();
        var saved = mapper.ToProfile(mapper.FromProfile(MemoryProviderManagementProfile.FromProfile(profile)));
        AssertLosslessManifest(profile, saved);
        Assert.Equal(profile.Manifest.InteractionSupport, saved.Manifest.InteractionSupport);
    }
}
