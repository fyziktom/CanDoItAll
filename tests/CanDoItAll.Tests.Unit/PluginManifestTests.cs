using System.Reflection;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Tests.Unit;

public sealed class PluginManifestTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void PluginAbstractions_ids_normalize_and_serialize_as_scalars()
    {
        var pluginId = new PluginId("  Vendor.Sample_Plugin  ");
        var packageId = new PluginPackageId(" Vendor.Sample.Package ");
        var connectionId = new PluginConnectionId(Guid.Parse("45f5e2db-f7be-4f24-8a16-52ddab19d6d1"));
        var connectionKey = new PluginConnectionKey(" Api-Key ");
        var rendererKey = new PluginRendererKey(" Settings.Renderer ");

        var json = JsonSerializer.Serialize(new IdentifierEnvelope(
            pluginId,
            packageId,
            connectionId,
            connectionKey,
            rendererKey), JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<IdentifierEnvelope>(json, JsonOptions);

        Assert.Contains("\"pluginId\":\"vendor.sample_plugin\"", json, StringComparison.Ordinal);
        Assert.Contains("\"packageId\":\"vendor.sample.package\"", json, StringComparison.Ordinal);
        Assert.Contains("\"connectionId\":\"45f5e2db-f7be-4f24-8a16-52ddab19d6d1\"", json, StringComparison.Ordinal);
        Assert.NotNull(roundTrip);
        Assert.Equal(pluginId, new PluginId("vendor.sample_plugin"));
        Assert.Equal(pluginId, roundTrip.PluginId);
        Assert.Equal(packageId, roundTrip.PackageId);
        Assert.Equal(connectionId, roundTrip.ConnectionId);
        Assert.Equal(connectionKey, roundTrip.ConnectionKey);
        Assert.Equal(rendererKey, roundTrip.RendererKey);
    }

    [Fact]
    public void PluginManifest_validator_rejects_duplicate_executor_renderer_and_connection_keys()
    {
        var descriptor = CreateDescriptor(
            workflowExecutors:
            [
                CreateExecutor("sample.exec", "settings.renderer"),
                CreateExecutor("SAMPLE.EXEC", "settings.renderer")
            ],
            settings: new PluginSettingsDescriptor(ConfigurationSchema.Empty(),
            [
                new PluginSettingsRendererDescriptor(new PluginRendererKey("settings.renderer"), "Settings", "SampleSettingsRenderer", PluginRendererTrustLevel.Bundled),
                new PluginSettingsRendererDescriptor(new PluginRendererKey("SETTINGS.RENDERER"), "Settings copy", "SampleSettingsRenderer", PluginRendererTrustLevel.Bundled)
            ]),
            connections:
            [
                CreateConnection("api"),
                CreateConnection("API")
            ],
            capabilities: PluginCapabilityKind.WorkflowExecutor | PluginCapabilityKind.SettingsRenderer | PluginCapabilityKind.SecretReference);

        var result = PluginManifestValidator.Validate(descriptor);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Issues, issue => issue.Code == PluginManifestValidationIssueCode.DuplicateWorkflowExecutorId);
        Assert.Contains(result.Issues, issue => issue.Code == PluginManifestValidationIssueCode.DuplicateRendererKey);
        Assert.Contains(result.Issues, issue => issue.Code == PluginManifestValidationIssueCode.DuplicateConnectionKey);
    }

    [Fact]
    public void PluginManifest_validator_requires_declared_capabilities()
    {
        var descriptor = CreateDescriptor(
            workflowExecutors: [CreateExecutor("sample.exec", "settings.renderer")],
            settings: new PluginSettingsDescriptor(ConfigurationSchema.Empty(),
            [
                new PluginSettingsRendererDescriptor(new PluginRendererKey("settings.renderer"), "Settings", "SampleSettingsRenderer", PluginRendererTrustLevel.Bundled)
            ]),
            connections: [CreateConnection("api")],
            oauth2: new PluginOAuth2Descriptor(
                new PluginConnectionKey("oauth"),
                new Uri("https://example.test/oauth/authorize"),
                new Uri("https://example.test/oauth/token"),
                ["files.read"]),
            capabilities: PluginCapabilityKind.None);

        var result = PluginManifestValidator.Validate(descriptor);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Issues, issue => issue.Code == PluginManifestValidationIssueCode.MissingCapability && issue.Message.Contains("workflow executors", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Issues, issue => issue.Code == PluginManifestValidationIssueCode.MissingCapability && issue.Message.Contains("settings renderers", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Issues, issue => issue.Code == PluginManifestValidationIssueCode.MissingCapability && issue.Message.Contains("OAuth2", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Issues, issue => issue.Code == PluginManifestValidationIssueCode.MissingCapability && issue.Message.Contains("secret-backed connections", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PluginManifest_validator_rejects_duplicate_catalog_ids_and_unsupported_capabilities()
    {
        var unsupportedCapability = (PluginCapabilityKind)(1 << 20);
        var descriptors = new[]
        {
            CreateDescriptor(
                id: "sample.plugin",
                packageId: "sample.package",
                capabilities: unsupportedCapability),
            CreateDescriptor(
                id: "SAMPLE.PLUGIN",
                packageId: "SAMPLE.PACKAGE",
                capabilities: PluginCapabilityKind.None)
        };

        var result = PluginManifestValidator.ValidateCatalog(descriptors);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Issues, issue => issue.Code == PluginManifestValidationIssueCode.UnsupportedCapability);
        Assert.Contains(result.Issues, issue => issue.Code == PluginManifestValidationIssueCode.DuplicatePluginId);
        Assert.Contains(result.Issues, issue => issue.Code == PluginManifestValidationIssueCode.DuplicatePackageId);
    }

    [Fact]
    public void PluginManifest_descriptor_round_trips_with_scalar_identifiers()
    {
        var descriptor = CreateDescriptor(
            workflowExecutors: [CreateExecutor("sample.exec", "settings.renderer")],
            settings: new PluginSettingsDescriptor(ConfigurationSchema.Empty(),
            [
                new PluginSettingsRendererDescriptor(new PluginRendererKey("settings.renderer"), "Settings", "SampleSettingsRenderer", PluginRendererTrustLevel.Bundled)
            ]),
            connections: [CreateConnection("api")],
            capabilities: PluginCapabilityKind.WorkflowExecutor | PluginCapabilityKind.SettingsRenderer | PluginCapabilityKind.SecretReference);

        var json = JsonSerializer.Serialize(descriptor, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<PluginDescriptor>(json, JsonOptions);

        Assert.Contains("\"id\":\"sample.plugin\"", json, StringComparison.Ordinal);
        Assert.Contains("\"packageId\":\"sample.package\"", json, StringComparison.Ordinal);
        Assert.NotNull(roundTrip);
        Assert.Equal(descriptor.Id, roundTrip.Id);
        Assert.Equal(descriptor.Package!.PackageId, roundTrip.Package!.PackageId);
        Assert.Equal(descriptor.WorkflowExecutors[0].ExecutorId, roundTrip.WorkflowExecutors[0].ExecutorId);
        Assert.Equal(descriptor.Settings.Renderers[0].RendererKey, roundTrip.Settings.Renderers[0].RendererKey);
        Assert.Equal(descriptor.Connections[0].Key, roundTrip.Connections[0].Key);
    }

    [Fact]
    public void PluginAbstractions_public_contracts_do_not_reference_IServiceProvider_or_implementation_modules()
    {
        var assembly = typeof(PluginDescriptor).Assembly;
        var forbiddenPublicReferences = assembly.GetExportedTypes()
            .SelectMany(GetPublicMemberTypes)
            .Where(ContainsServiceProvider)
            .ToList();
        var referencedAssemblyNames = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToList();

        Assert.Empty(forbiddenPublicReferences);
        Assert.DoesNotContain(referencedAssemblyNames, name => name.StartsWith("CanDoItAll.Modules.", StringComparison.Ordinal));
        Assert.DoesNotContain("CanDoItAll.AgentFramework.Core", referencedAssemblyNames);
        Assert.DoesNotContain("CanDoItAll.AgentFramework.Maf", referencedAssemblyNames);
        Assert.DoesNotContain("CanDoItAll.Infrastructure", referencedAssemblyNames);
    }

    private static PluginDescriptor CreateDescriptor(
        string id = "sample.plugin",
        string packageId = "sample.package",
        IReadOnlyList<PluginWorkflowExecutorDescriptor>? workflowExecutors = null,
        PluginSettingsDescriptor? settings = null,
        IReadOnlyList<PluginConnectionDescriptor>? connections = null,
        PluginOAuth2Descriptor? oauth2 = null,
        PluginCapabilityKind capabilities = PluginCapabilityKind.None)
        => new(
            new PluginId(id),
            "Sample plugin",
            "Sample plugin for contract tests.",
            "1.0.0",
            "CanDoItAll",
            PluginSourceKind.Bundled,
            PluginTrustLevel.Bundled,
            "1.0.0",
            capabilities,
            workflowExecutors ?? [],
            settings ?? PluginSettingsDescriptor.Empty,
            connections ?? [],
            new PluginPackageDescriptor(
                new PluginPackageId(packageId),
                "1.0.0",
                "1.0.0",
                "sha256",
                "signature"),
            oauth2);

    private static PluginWorkflowExecutorDescriptor CreateExecutor(
        string executorId,
        string rendererKey)
        => new(
            new WorkflowExecutorId(executorId),
            "Sample executor",
            "Sample executor for contract tests.",
            WorkflowExecutorCategoryKind.Utility,
            new PluginRendererKey(rendererKey),
            ConfigurationSchema.Empty(),
            WorkflowValueShape.Text,
            new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"),
            WorkflowExecutorExecutionPolicy.Default);

    private static PluginConnectionDescriptor CreateConnection(string key)
        => new(
            new PluginConnectionKey(key),
            "API",
            "API connection.",
            PluginConnectionAuthKind.ApiKey,
            ConfigurationSchema.Empty());

    private static IEnumerable<Type> GetPublicMemberTypes(Type type)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            yield return property.PropertyType;
        }

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            yield return method.ReturnType;
            foreach (var parameter in method.GetParameters())
            {
                yield return parameter.ParameterType;
            }
        }
    }

    private static bool ContainsServiceProvider(Type type)
    {
        if (type == typeof(IServiceProvider))
        {
            return true;
        }

        return type.IsGenericType && type.GetGenericArguments().Any(ContainsServiceProvider);
    }

    public sealed record IdentifierEnvelope(
        PluginId PluginId,
        PluginPackageId PackageId,
        PluginConnectionId ConnectionId,
        PluginConnectionKey ConnectionKey,
        PluginRendererKey RendererKey);
}
