using System.Xml.Linq;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowExecutors.Plugins;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.SharedKernel.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

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
    public void PluginDescriptorSourceProjectsGrantAvailabilityAndSourceMetadata()
    {
        var plugin = new TestPlugin(CreatePluginDescriptor(
            sourceKind: PluginSourceKind.Bundled,
            trustLevel: PluginTrustLevel.Bundled,
            capabilities: PluginCapabilityKind.WorkflowExecutor | PluginCapabilityKind.OAuth2,
            oauth2: new PluginOAuth2Descriptor(
                new PluginConnectionKey("oauth"),
                new Uri("https://example.test/authorize"),
                new Uri("https://example.test/token"),
                ["mail.read"])));
        var grantEvaluator = new StaticGrantEvaluator(
            PluginGrantDecision.Allow(plugin.Descriptor.Id, PluginCapabilityKind.WorkflowExecutor),
            PluginGrantDecision.Deny(
                plugin.Descriptor.Id,
                PluginCapabilityKind.OAuth2,
                PluginGrantDecisionKind.GrantMissing,
                "OAuth grant is missing."));
        var source = new PluginWorkflowExecutorDescriptorSource([plugin], grantEvaluator);

        var descriptor = Assert.Single(source.ListExecutorDescriptors());

        Assert.Equal(plugin.Descriptor.WorkflowExecutors[0].ExecutorId, descriptor.Id);
        Assert.Equal(WorkflowExecutorSourceKind.BundledPlugin, descriptor.Source.Kind);
        Assert.Equal(plugin.Descriptor.Id.Value, descriptor.Source.PluginId);
        Assert.Equal(WorkflowExecutorTrustLevel.BundledPlugin, descriptor.Source.TrustLevel);
        Assert.Equal(WorkflowExecutorAvailabilityKind.Unavailable, descriptor.Availability.Kind);
        Assert.Equal(nameof(PluginGrantDecisionKind.GrantMissing), descriptor.Availability.ReasonCode);
        Assert.False(descriptor.CanExecute);
        Assert.Equal(plugin.Descriptor.WorkflowExecutors[0].SideEffects, descriptor.SideEffects);
        Assert.Equal(plugin.Descriptor.WorkflowExecutors[0].PermissionPolicy, descriptor.PermissionPolicy);
        Assert.True(descriptor.DeterministicTestMode.IsSupported);
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
        var executor = Assert.Single(provider.GetRequiredService<IEnumerable<IWorkflowExecutor>>());
        var descriptorSource = Assert.Single(provider.GetRequiredService<IEnumerable<IWorkflowExecutorDescriptorSource>>());
        var descriptor = Assert.Single(descriptorSource.ListExecutorDescriptors());

        Assert.IsType<RuntimePackageWorkflowExecutor>(executor);
        Assert.Equal(RuntimeFixtureWorkflowExecutor.ExecutorId, descriptor.Id);
        Assert.Equal(WorkflowExecutorSourceKind.LocalPackage, descriptor.Source.Kind);
        Assert.Equal(plugin.Id.Value, descriptor.Source.PluginId);
        Assert.Equal(plugin.Package!.PackageId.Value, descriptor.Source.PackageId);
        Assert.Equal(WorkflowExecutorTrustLevel.LocalPackage, descriptor.Source.TrustLevel);
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
        var exception = Assert.Throws<PluginWorkflowExecutorActivationException>(() =>
            provider.GetRequiredService<IEnumerable<IWorkflowExecutor>>().ToArray());

        Assert.Equal(plugin.Id, exception.PluginId);
        Assert.Equal(plugin.Package!.PackageId, exception.PackageId);
        Assert.Equal(typeof(MissingDependencyRuntimeExecutor).FullName, exception.ExecutorTypeName);
        Assert.Equal("runtime-package-activation", exception.Operation);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public void PluginModuleComposesPluginExecutorBoundaryInsteadOfOwningDescriptorSource()
    {
        var moduleSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Modules",
            "CanDoItAll.Modules.Plugins",
            "Services",
            "PluginsModuleServiceCollectionExtensions.cs"));
        var oldDescriptorSourcePath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Modules",
            "CanDoItAll.Modules.Plugins",
            "Catalog",
            "PluginWorkflowExecutorDescriptorSource.cs");

        Assert.Contains("AddPluginWorkflowExecutorBoundary()", moduleSource, StringComparison.Ordinal);
        Assert.Contains("IPluginWorkflowExecutorGrantEvaluator", moduleSource, StringComparison.Ordinal);
        Assert.False(File.Exists(oldDescriptorSourcePath));
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
            "Executor used by plugin executor boundary tests.",
            WorkflowExecutorCategoryKind.Utility,
            new PluginRendererKey("runtime.fixture"),
            ConfigurationSchema.Empty(),
            WorkflowValueShape.Text,
            new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"),
            WorkflowExecutorExecutionPolicy.Default)
        {
            PermissionPolicy = new WorkflowExecutorPermissionPolicy(
                WorkflowExecutorCapabilityFlags.UsesNetwork |
                WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
                WorkflowExecutorApprovalRequirement.NotRequired),
            SideEffects = WorkflowExecutorSideEffectDescriptor.None,
            DeterministicTestMode = WorkflowExecutorDeterministicTestModeDescriptor.Supported("Boundary test preview.")
        };

    private static ProjectReferenceSnapshot ReadProjectReferences(string relativeProjectPath)
    {
        var projectPath = Path.Combine(FindRepositoryRoot(), relativeProjectPath.Replace('/', Path.DirectorySeparatorChar));
        var project = XDocument.Load(projectPath);
        var projectReferences = project
            .Descendants("ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension((string?)element.Attribute("Include") ?? string.Empty))
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

    private sealed class TestPlugin(PluginDescriptor descriptor) : ICanDoItAllPlugin
    {
        public PluginDescriptor Descriptor { get; } = descriptor;
    }

    private sealed class StaticGrantEvaluator(params PluginGrantDecision[] decisions) : IPluginWorkflowExecutorGrantEvaluator
    {
        public PluginGrantDecision Evaluate(
            PluginId pluginId,
            PluginCapabilityKind capability,
            PluginHostToolRecipeId? recipeId = null)
            => decisions.FirstOrDefault(decision =>
                   decision.PluginId == pluginId &&
                   decision.Capability == capability &&
                   decision.RecipeId == recipeId)
               ?? PluginGrantDecision.Allow(pluginId, capability, recipeId);
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

    private sealed class MissingDependency
    {
        public string Name => "Missing";
    }

    private sealed record ProjectReferenceSnapshot(
        IReadOnlyList<string> ProjectReferences,
        IReadOnlyList<string> PackageReferences);
}
