using System.Text.Json;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Modules.Memory.Services;

namespace CanDoItAll.Tests.Components;

internal static class MemoryProviderProfileEditorTestData
{
    private static readonly MemoryCapabilityDescriptor VendorCapability = new(
        MemoryCapabilityId.Parse("vendor.context.enrichment"),
        Version: "7",
        Supported: true);
    private static readonly MemoryProviderLimits CustomLimits = new(
        maxContextSections: 17,
        maxSourceItems: 23,
        maxInFlightOperations: 5,
        operationTimeout: TimeSpan.FromSeconds(42));

    public static MemoryProviderProfileEditorModel CreateHttpEditor() => new()
    {
        InstanceId = "provider.http-test",
        DisplayName = "HTTP test",
        DriverKind = MemoryProviderDriverKind.Http,
        ProviderKind = "memory.http",
        SupportsContextQuerySync = true,
        Http = new MemoryProviderHttpTransportEditorModel
        {
            BaseUrl = "https://memory.example.test"
        }
    };

    public static MemoryProviderProfileEditorModel CreateMcpEditor() => new()
    {
        InstanceId = "provider.mcp-test",
        DisplayName = "MCP test",
        DriverKind = MemoryProviderDriverKind.Mcp,
        ProviderKind = "memory.mcp",
        SupportsContextQuerySync = true,
        Mcp = new MemoryProviderMcpTransportEditorModel
        {
            DescriptorKind = "remote-http",
            ServerKey = "memory-test",
            RemoteEndpoint = "https://mcp-memory.example.test",
            ContextQueryTool = "memory_query"
        }
    };

    public static MemoryProviderProfile CreateProfile(
        MemoryProviderDriverKind driverKind,
        IReadOnlyList<(string Key, JsonElement Value)> extensions,
        IReadOnlyList<MemoryCapabilityId> managedCapabilities)
    {
        var capabilities = managedCapabilities
            .Select(capability => new MemoryCapabilityDescriptor(capability, Version: "2", Supported: true))
            .Append(VendorCapability)
            .ToArray();
        var surfaces = new[]
        {
            new MemoryProviderUiSurface(
                MemoryProviderUiSurfaceKind.ExternalUrl,
                "Vendor diagnostics",
                ComponentKey: null,
                UrlSettingKey: "provider.vendor.diagnosticsUrl",
                VendorCapability.Id)
        };
        var values = extensions
            .Append(("provider.vendor.diagnosticsUrl", String("https://memory.example.test/diagnostics")))
            .ToArray();
        return new MemoryProviderProfile(
            MemoryProviderInstanceId.Parse($"provider.{driverKind.ToString().ToLowerInvariant()}"),
            $"{driverKind} memory",
            driverKind,
            IsEnabled: true,
            MemoryProviderHealthState.Healthy,
            MemoryProviderWorkspaceScope.AllWorkspaces,
            SelectionTags: ["primary", "finance", "Primary"],
            new MemoryProviderProfilePolicy(MemoryProviderFallbackBehavior.DenyImplicitFallback),
            new MemoryProviderManifest(
                MemoryProviderKind.Parse($"memory.{driverKind.ToString().ToLowerInvariant()}"),
                MemoryProtocolVersion.Current,
                capabilities,
                new MemoryProviderInteractionSupport(
                    managedCapabilities.Contains(MemoryCapabilityIds.ContextQuerySync),
                    managedCapabilities.Contains(MemoryCapabilityIds.ContextQueryAsync),
                    managedCapabilities.Contains(MemoryCapabilityIds.IngestionProviderRequestedSource),
                    managedCapabilities.Contains(MemoryCapabilityIds.FeedbackImmediate) ||
                    managedCapabilities.Contains(MemoryCapabilityIds.FeedbackDelayed),
                    managedCapabilities.Contains(MemoryCapabilityIds.EventsProviderPush) ||
                    managedCapabilities.Contains(MemoryCapabilityIds.EventsHostPoll)),
                surfaces,
                CustomLimits,
                MemoryExtensionData.From(values)));
    }

    public static void AssertLosslessManifest(MemoryProviderProfile expected, MemoryProviderProfile actual)
    {
        Assert.Equal(expected.SelectionTags, actual.SelectionTags);
        Assert.Equal(expected.Manifest.ProtocolVersion, actual.Manifest.ProtocolVersion);
        Assert.Equal(expected.Manifest.Capabilities, actual.Manifest.Capabilities);
        Assert.Equal(expected.Manifest.InteractionSupport, actual.Manifest.InteractionSupport);
        Assert.Equal(expected.Manifest.UiSurfaces, actual.Manifest.UiSurfaces);
        Assert.Equal(expected.Manifest.Limits, actual.Manifest.Limits);
        Assert.Equal(
            expected.Manifest.Extensions.Values["provider.vendor.customSettings"].GetRawText(),
            actual.Manifest.Extensions.Values["provider.vendor.customSettings"].GetRawText());
    }

    public static void AssertExtension(MemoryProviderProfile profile, string key, string value)
    {
        Assert.True(profile.Manifest.Extensions.Values.TryGetValue(key, out var element));
        Assert.Equal(value, element.GetString());
    }

    public static string SerializeExtensions(MemoryProviderProfile profile) =>
        JsonSerializer.Serialize(profile.Manifest.Extensions.Values);

    public static JsonElement String(string value) => JsonSerializer.SerializeToElement(value);

    public static JsonElement Number(int value) => JsonSerializer.SerializeToElement(value);

    public static JsonElement Json<T>(T value) => JsonSerializer.SerializeToElement(value);
}
