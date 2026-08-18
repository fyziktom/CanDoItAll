using System.Xml.Linq;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowExecutors.Plugins;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.SharedKernel.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit.Plugins;

public sealed class PluginWorkflowExecutorBoundaryTests
{
    [Fact]
    public void PluginExecutorBoundaryProjectHasBoundedDependencies()
    {
        var references = ReadProjectReferences(
            "src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Plugins/CanDoItAll.AgentFramework.WorkflowExecutors.Plugins.csproj");

        Assert.Equal(
            new[]
            {
                "CanDoItAll.AgentFramework.Models",
                "CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions",
                "CanDoItAll.Plugins.Abstractions"
            },
            references.ProjectReferences.Order(StringComparer.Ordinal));
        Assert.Equal(
            new[] { "Microsoft.Extensions.DependencyInjection.Abstractions" },
            references.PackageReferences.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void PluginManifestProjectionPreservesStableContributionMetadata()
    {
        var descriptor = new RuntimeFixtureWorkflowExecutor().Descriptor with
        {
            SettingsPresentationMode = WorkflowExecutorSettingsPresentationMode.CustomRenderer
        };

        var projected = PluginWorkflowExecutorDescriptor.FromWorkflowExecutorDescriptor(descriptor);

        Assert.Equal(descriptor.Id, projected.ExecutorId);
        Assert.Equal(descriptor.Name, projected.Name);
        Assert.Equal(descriptor.Description, projected.Description);
        Assert.Equal(descriptor.Category, projected.Category);
        Assert.Equal(descriptor.SetupRendererKey, projected.SettingsRendererKey.Value);
        Assert.Equal(descriptor.ConfigurationSchema, projected.SettingsSchema);
        Assert.Equal(descriptor.InputShape, projected.InputShape);
        Assert.Equal(descriptor.ResultShape, projected.ResultShape);
        Assert.Equal(descriptor.DefaultPolicy, projected.DefaultPolicy);
        Assert.Equal(descriptor.DefaultSettingsJson, projected.DefaultSettingsJson);
        Assert.Equal(descriptor.SettingsPresentationMode, projected.SettingsPresentationMode);
        Assert.Equal(descriptor.Simulation, projected.Simulation);
        Assert.Equal(descriptor.SideEffects, projected.SideEffects);
        Assert.Equal(descriptor.PermissionPolicy, projected.PermissionPolicy);
        Assert.Equal(descriptor.DeterministicTestMode, projected.DeterministicTestMode);
    }

    [Fact]
    public void RuntimePackageRegistrationWrapsExecutorWithPackageSourceMetadata()
    {
        var plugin = CreatePluginDescriptor(
            sourceKind: PluginSourceKind.LocalPackage,
            trustLevel: PluginTrustLevel.LocalPackage,
            packageId: "runtime.package");
        var services = new ServiceCollection();

        PluginWorkflowExecutorRuntimeRegistration.RegisterWorkflowExecutors(
            services,
            [typeof(RuntimeFixtureWorkflowExecutor)],
            plugin);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var contribution = Assert.Single(
            scope.ServiceProvider.GetRequiredService<IEnumerable<IWorkflowExecutorContribution>>());
        var executor = Assert.IsType<RuntimePackageWorkflowExecutor>(Assert.Single(
            scope.ServiceProvider.GetRequiredService<IEnumerable<IWorkflowExecutor>>()));
        var descriptor = contribution.Descriptor;
        var implementationDescriptor = new RuntimeFixtureWorkflowExecutor().Descriptor;

        Assert.Equal(RuntimeFixtureWorkflowExecutor.ExecutorId, descriptor.Id);
        Assert.Equal(WorkflowExecutorSourceKind.LocalPackage, descriptor.Source.Kind);
        Assert.Equal(plugin.Id.Value, descriptor.Source.PluginId);
        Assert.Equal(plugin.Package!.PackageId.Value, descriptor.Source.PackageId);
        Assert.Equal(WorkflowExecutorTrustLevel.LocalPackage, descriptor.Source.TrustLevel);
        Assert.Equal(implementationDescriptor.DefaultSettingsJson, descriptor.DefaultSettingsJson);
        Assert.Equal(implementationDescriptor.ConfigurationSchema.Version, descriptor.ConfigurationSchema.Version);
        Assert.Equal(
            implementationDescriptor.ConfigurationSchema.Fields.Select(field => (field.Key, field.FieldType, field.IsRequired)),
            descriptor.ConfigurationSchema.Fields.Select(field => (field.Key, field.FieldType, field.IsRequired)));
        Assert.Equal(implementationDescriptor.Simulation, descriptor.Simulation);
        Assert.Equal(implementationDescriptor.PermissionPolicy, descriptor.PermissionPolicy);
        Assert.Equal(implementationDescriptor.SideEffects, descriptor.SideEffects);
        Assert.Equal(implementationDescriptor.DeterministicTestMode, descriptor.DeterministicTestMode);
        Assert.Equal(descriptor, executor.Descriptor);
    }

    [Fact]
    public void RuntimePackageActivationFailureIncludesPluginPackageAndTypeContext()
    {
        var plugin = CreatePluginDescriptor(
            sourceKind: PluginSourceKind.LocalPackage,
            trustLevel: PluginTrustLevel.LocalPackage,
            packageId: "runtime.failure.package");
        var services = new ServiceCollection();

        PluginWorkflowExecutorRuntimeRegistration.RegisterWorkflowExecutors(
            services,
            [typeof(MissingDependencyRuntimeExecutor)],
            plugin);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var exception = Assert.Throws<PluginWorkflowExecutorActivationException>(() =>
            scope.ServiceProvider.GetRequiredService<IEnumerable<IWorkflowExecutor>>().ToArray());

        Assert.Equal(plugin.Id, exception.PluginId);
        Assert.Equal(plugin.Package!.PackageId, exception.PackageId);
        Assert.Equal(typeof(MissingDependencyRuntimeExecutor).FullName, exception.ExecutorTypeName);
        Assert.Equal("runtime-package-activation", exception.Operation);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public void RuntimePackageRegistrationRejectsManifestImplementationCountMismatch()
    {
        var plugin = CreatePluginDescriptor(
            sourceKind: PluginSourceKind.LocalPackage,
            trustLevel: PluginTrustLevel.LocalPackage,
            packageId: "runtime.count-mismatch.package");
        var services = new ServiceCollection();

        var exception = Assert.Throws<PluginWorkflowExecutorActivationException>(() =>
            PluginWorkflowExecutorRuntimeRegistration.RegisterWorkflowExecutors(
                services,
                [],
                plugin));

        Assert.Equal(PluginWorkflowExecutorActivationFailureKind.ManifestRuntimeMismatch, exception.FailureKind);
        Assert.Equal("runtime-package-manifest-validation", exception.Operation);
        Assert.Contains(RuntimeFixtureWorkflowExecutor.ExecutorId.Value, exception.Message, StringComparison.Ordinal);
        Assert.Contains("none", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RuntimePackageContributionRejectsManifestRuntimeIdMismatch()
    {
        var plugin = CreatePluginDescriptor(
            sourceKind: PluginSourceKind.LocalPackage,
            trustLevel: PluginTrustLevel.LocalPackage,
            packageId: "runtime.id-mismatch.package");
        var services = new ServiceCollection();
        PluginWorkflowExecutorRuntimeRegistration.RegisterWorkflowExecutors(
            services,
            [typeof(MismatchedRuntimeExecutor)],
            plugin);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var exception = Assert.Throws<PluginWorkflowExecutorActivationException>(() =>
            scope.ServiceProvider.GetRequiredService<IEnumerable<IWorkflowExecutor>>().ToArray());

        Assert.Equal(PluginWorkflowExecutorActivationFailureKind.ManifestRuntimeMismatch, exception.FailureKind);
        Assert.Contains(RuntimeFixtureWorkflowExecutor.ExecutorId.Value, exception.Message, StringComparison.Ordinal);
        Assert.Contains(MismatchedRuntimeExecutor.ExecutorId.Value, exception.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(MismatchedRuntimeExecutor).FullName!, exception.ExecutorTypeName, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimePackageContributionRejectsManifestRuntimeMetadataDrift()
    {
        var plugin = CreatePluginDescriptor(
            sourceKind: PluginSourceKind.LocalPackage,
            trustLevel: PluginTrustLevel.LocalPackage,
            packageId: "runtime.metadata-mismatch.package") with
        {
            WorkflowExecutors =
            [
                CreatePluginExecutor() with
                {
                    DefaultSettingsJson = "{\"enabled\":false}"
                }
            ]
        };
        var services = new ServiceCollection();
        PluginWorkflowExecutorRuntimeRegistration.RegisterWorkflowExecutors(
            services,
            [typeof(RuntimeFixtureWorkflowExecutor)],
            plugin);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var exception = Assert.Throws<PluginWorkflowExecutorActivationException>(() =>
            scope.ServiceProvider.GetRequiredService<IEnumerable<IWorkflowExecutor>>().ToArray());

        Assert.Equal(PluginWorkflowExecutorActivationFailureKind.ManifestRuntimeMismatch, exception.FailureKind);
        Assert.Contains("defaultSettingsJson", exception.Message, StringComparison.Ordinal);
        Assert.Contains(RuntimeFixtureWorkflowExecutor.ExecutorId.Value, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimePackageContributionRejectsSettingsPresentationModeDrift()
    {
        var plugin = CreatePluginDescriptor(
            sourceKind: PluginSourceKind.LocalPackage,
            trustLevel: PluginTrustLevel.LocalPackage,
            packageId: "runtime.renderer-mode-mismatch.package") with
        {
            WorkflowExecutors =
            [
                CreatePluginExecutor() with
                {
                    SettingsPresentationMode = WorkflowExecutorSettingsPresentationMode.CustomRenderer
                }
            ]
        };
        var services = new ServiceCollection();
        PluginWorkflowExecutorRuntimeRegistration.RegisterWorkflowExecutors(
            services,
            [typeof(RuntimeFixtureWorkflowExecutor)],
            plugin);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var exception = Assert.Throws<PluginWorkflowExecutorActivationException>(() =>
            scope.ServiceProvider.GetRequiredService<IEnumerable<IWorkflowExecutor>>().ToArray());

        Assert.Equal(PluginWorkflowExecutorActivationFailureKind.ManifestRuntimeMismatch, exception.FailureKind);
        Assert.Contains("settingsPresentationMode", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PluginModuleDoesNotRegisterLegacyWorkflowExecutorDescriptorSource()
    {
        var moduleSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Modules",
            "CanDoItAll.Modules.Plugins",
            "Services",
            "PluginsModuleServiceCollectionExtensions.cs"));
        var pluginBoundaryRoot = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "MAF",
            "WorkflowExecutors",
            "CanDoItAll.AgentFramework.WorkflowExecutors.Plugins");

        Assert.DoesNotContain("AddPluginWorkflowExecutorBoundary()", moduleSource, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(pluginBoundaryRoot, "PluginWorkflowExecutorDescriptorSource.cs")));
        Assert.False(File.Exists(Path.Combine(pluginBoundaryRoot, "RuntimePackageWorkflowExecutorDescriptorSource.cs")));
    }

    private static PluginDescriptor CreatePluginDescriptor(
        PluginSourceKind sourceKind = PluginSourceKind.Bundled,
        PluginTrustLevel trustLevel = PluginTrustLevel.Bundled,
        PluginCapabilityKind capabilities = PluginCapabilityKind.WorkflowExecutor,
        string id = "test.plugin",
        string packageId = "test.package",
        PluginOAuth2Descriptor? oauth2 = null)
        => new(
            new PluginId(id),
            "Test plugin",
            "Plugin used by plugin executor boundary tests.",
            "1.0.0",
            "CanDoItAll",
            sourceKind,
            trustLevel,
            "1.0.0",
            capabilities,
            [CreatePluginExecutor()],
            PluginSettingsDescriptor.Empty,
            [],
            new PluginPackageDescriptor(
                new PluginPackageId(packageId),
                "1.0.0",
                "1.0.0",
                "sha256",
                "signature"),
            oauth2,
            UiIconDescriptor.MaterialIcon("extension", "Plugin"));

    private static PluginWorkflowExecutorDescriptor CreatePluginExecutor()
        => new(
            RuntimeFixtureWorkflowExecutor.ExecutorId,
            "Runtime fixture executor",
            "Runtime package executor used by tests.",
            WorkflowExecutorCategoryKind.Utility,
            new PluginRendererKey("runtime.fixture"),
            new ConfigurationSchema(
                "1.0",
                [new ConfigurationFieldDescriptor("enabled", "Enabled", ConfigurationFieldType.Boolean, IsRequired: false, "Enable fixture execution.")]),
            WorkflowValueShape.Text,
            new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"),
            WorkflowExecutorExecutionPolicy.Default)
        {
            DefaultSettingsJson = "{\"enabled\":true}",
            Simulation = WorkflowExecutorSimulationDescriptor.JsonTemplate(
                "{\"simulated\":true}",
                "Simulate runtime package execution."),
            PermissionPolicy = new WorkflowExecutorPermissionPolicy(
                WorkflowExecutorCapabilityFlags.UsesNetwork |
                WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
                WorkflowExecutorApprovalRequirement.NotRequired),
            SideEffects = WorkflowExecutorSideEffectDescriptor.ExternalRead("runtime-fixture-read/v1"),
            DeterministicTestMode = WorkflowExecutorDeterministicTestModeDescriptor.Supported("Runtime fixture preview.")
        };

    private static ProjectReferenceSnapshot ReadProjectReferences(string relativeProjectPath)
    {
        var projectPath = Path.Combine(FindRepositoryRoot(), relativeProjectPath.Replace('/', Path.DirectorySeparatorChar));
        var project = XDocument.Load(projectPath);
        var projectReferences = project
            .Descendants("ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension(
                ((string?)element.Attribute("Include") ?? string.Empty).Replace('\\', '/')))
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var packageReferences = project
            .Descendants("PackageReference")
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty)
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Order(StringComparer.Ordinal)
            .ToArray();
        return new ProjectReferenceSnapshot(projectReferences, packageReferences);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root could not be found.");
    }

    private sealed class RuntimeFixtureWorkflowExecutor : IWorkflowExecutor
    {
        public static WorkflowExecutorId ExecutorId { get; } = new("runtime.fixture.executor");

        public WorkflowExecutorDescriptor Descriptor { get; } = new(
            ExecutorId,
            "Runtime fixture executor",
            "Runtime package executor used by tests.",
            WorkflowExecutorCategoryKind.Utility,
            "extension",
            "runtime.fixture",
            WorkflowValueShape.Text,
            new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"),
            "{\"type\":\"object\"}",
            "{\"enabled\":true}",
            WorkflowExecutorExecutionPolicy.Default,
            IsImplemented: true)
        {
            ConfigurationSchema = new ConfigurationSchema(
                "1.0",
                [new ConfigurationFieldDescriptor("enabled", "Enabled", ConfigurationFieldType.Boolean, IsRequired: false, "Enable fixture execution.")]),
            Simulation = WorkflowExecutorSimulationDescriptor.JsonTemplate(
                "{\"simulated\":true}",
                "Simulate runtime package execution."),
            PermissionPolicy = new WorkflowExecutorPermissionPolicy(
                WorkflowExecutorCapabilityFlags.UsesNetwork |
                WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
                WorkflowExecutorApprovalRequirement.NotRequired),
            SideEffects = WorkflowExecutorSideEffectDescriptor.ExternalRead("runtime-fixture-read/v1"),
            DeterministicTestMode = WorkflowExecutorDeterministicTestModeDescriptor.Supported("Runtime fixture preview.")
        };

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new WorkflowNodeExecutionResult(
                context.Node.Id,
                "{}",
                context.Descriptor.ResultShape));
    }

    private sealed class MissingDependencyRuntimeExecutor(MissingDependency dependency) : IWorkflowExecutor
    {
        public WorkflowExecutorDescriptor Descriptor => new(
            new WorkflowExecutorId("runtime.missing-dependency"),
            dependency.Name,
            "Runtime package executor with a missing dependency.",
            WorkflowExecutorCategoryKind.Utility,
            "extension",
            "runtime.missing",
            WorkflowValueShape.Text,
            new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"),
            "{}",
            "{}",
            WorkflowExecutorExecutionPolicy.Default,
            IsImplemented: true);

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new WorkflowNodeExecutionResult(
                context.Node.Id,
                "{}",
                context.Descriptor.ResultShape));
    }

    private sealed class MismatchedRuntimeExecutor : IWorkflowExecutor
    {
        public static WorkflowExecutorId ExecutorId { get; } = new("runtime.fixture.mismatched");

        public WorkflowExecutorDescriptor Descriptor => new(
            ExecutorId,
            "Mismatched runtime executor",
            "Runtime executor whose id is not declared in the manifest.",
            WorkflowExecutorCategoryKind.Utility,
            "extension",
            "runtime.fixture",
            WorkflowValueShape.Text,
            WorkflowValueShape.Text,
            "{\"type\":\"object\"}",
            "{}",
            WorkflowExecutorExecutionPolicy.Default,
            IsImplemented: true);

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class MissingDependency
    {
        public string Name => "Missing";
    }

    private sealed record ProjectReferenceSnapshot(
        IReadOnlyList<string> ProjectReferences,
        IReadOnlyList<string> PackageReferences);
}
