using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowExecutors.Plugins;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.SharedKernel.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowExecutorHardeningCheckpointTests
{
    [Fact]
    public void CombinedExecutorDescriptorsKeepStableIdsAndSourceContext()
    {
        var descriptors = CollectCombinedDescriptors();
        var duplicateIds = descriptors
            .GroupBy(descriptor => descriptor.Id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key.Value)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(duplicateIds);
        Assert.Contains(descriptors, descriptor => descriptor.Id == WorkflowExecutorIds.Delay);
        Assert.Contains(descriptors, descriptor => descriptor.Id == RuntimePackageExecutorId);
        Assert.Contains(descriptors, descriptor => descriptor.Source.Kind == WorkflowExecutorSourceKind.BundledPlugin && descriptor.Source.PluginId == BundledPluginId.Value);
        Assert.Contains(descriptors, descriptor => descriptor.Source.Kind == WorkflowExecutorSourceKind.LocalPackage && descriptor.Source.PackageId == RuntimePackageId.Value);

        foreach (var descriptor in descriptors)
        {
            Assert.False(string.IsNullOrWhiteSpace(descriptor.Id.Value));
            Assert.False(string.IsNullOrWhiteSpace(descriptor.Name));
            Assert.False(string.IsNullOrWhiteSpace(descriptor.SetupRendererKey));
            Assert.False(string.IsNullOrWhiteSpace(descriptor.DefaultSettingsJson));
            Assert.False(string.IsNullOrWhiteSpace(descriptor.Source.SourceId));
            Assert.NotEqual(WorkflowExecutorTrustLevel.Untrusted, descriptor.Source.TrustLevel);
        }
    }

    [Fact]
    public async Task PluginExecutorFailureDiagnosticsPreserveContextRepairHintRetryabilityAndRedaction()
    {
        var descriptor = CreateRuntimePackageDescriptor("plugin.hardening.failure") with
        {
            Source = WorkflowExecutorSourceDescriptor.Package(
                WorkflowExecutorSourceKind.LocalPackage,
                BundledPluginId.Value,
                RuntimePackageId.Value,
                "1.0.0",
                WorkflowExecutorTrustLevel.LocalPackage,
                "Runtime package",
                UiIconDescriptor.MaterialIcon("extension", "Runtime package"))
        };
        var executor = new ThrowingExecutor(
            descriptor,
            new InvalidOperationException("Provider failed token=raw-token-value Authorization: Bearer sk-test-secret-value."));
        var node = CreateNode(descriptor.Id);
        var invoker = new WorkflowExecutorInvoker(new WorkflowExecutorCatalog([executor]), [executor]);

        var exception = await Assert.ThrowsAsync<WorkflowExecutorInvocationException>(() =>
            invoker.ExecuteAsync(CreateDefinition(node), node, new WorkflowNodeInput("{}")).AsTask());
        var diagnostic = Assert.Single(WorkflowExecutorFailureDiagnosticMapper.GetDiagnostics(exception));

        Assert.Equal(WorkflowFailureKind.Executor, diagnostic.Kind);
        Assert.Equal(WorkflowFailureRetryability.RetryableAfterRepair, diagnostic.Retryability);
        Assert.Equal(node.Id, diagnostic.NodeId);
        Assert.Equal(descriptor.Id, diagnostic.ExecutorId);
        Assert.Equal(BundledPluginId.Value, diagnostic.Source.PluginId);
        Assert.Equal(RuntimePackageId.Value, diagnostic.Source.PackageId);
        Assert.Contains("Fix the executor settings", diagnostic.RepairHint, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", diagnostic.RedactedTechnicalDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-token-value", diagnostic.RedactedTechnicalDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-test-secret-value", diagnostic.RedactedTechnicalDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-token-value", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void PluginActivationDiagnosticsExposeRetryabilityRepairHintAndRedactedTechnicalDetail()
    {
        var plugin = CreatePluginDescriptor(
            PluginSourceKind.LocalPackage,
            PluginTrustLevel.LocalPackage,
            RuntimePackageId);
        var innerException = new InvalidOperationException(
            "Missing constructor dependency token=raw-token-value Authorization: Bearer sk-test-secret-value.");

        var exception = PluginWorkflowExecutorActivationException.ActivationFailed(
            plugin,
            typeof(RuntimePackageExecutor),
            innerException);

        Assert.Equal(plugin.Id, exception.PluginId);
        Assert.Equal(RuntimePackageId, exception.PackageId);
        Assert.Equal(typeof(RuntimePackageExecutor).FullName, exception.ExecutorTypeName);
        Assert.Equal("runtime-package-activation", exception.Operation);
        Assert.Equal(PluginWorkflowExecutorActivationFailureKind.ActivationFailed, exception.FailureKind);
        Assert.Equal(PluginWorkflowExecutorActivationRetryability.RetryableAfterRepair, exception.Retryability);
        Assert.Contains("Register missing constructor dependencies", exception.RepairHint, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", exception.RedactedTechnicalDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-token-value", exception.RedactedTechnicalDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-test-secret-value", exception.RedactedTechnicalDetail, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutorOwnershipAuditHasNoMafFallbackOrCategoryMonolith()
    {
        var root = FindRepositoryRoot();
        var mafWorkflowDirectory = Path.Combine(
            root,
            "src",
            "MAF",
            "Common",
            "CanDoItAll.AgentFramework.Maf",
            "Runtime",
            "Workflows");

        Assert.False(Directory.Exists(mafWorkflowDirectory), $"{mafWorkflowDirectory} must not own workflow executors.");

        var executorFiles = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => path.Contains("WorkflowExecutors.Standard", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToArray();

        foreach (var file in executorFiles)
        {
            Assert.InRange(File.ReadAllLines(file).Length, 1, 450);
        }
    }

    [Fact]
    public void BundledPluginWorkflowExecutorsShareSerializerOptions()
    {
        var root = FindRepositoryRoot();
        var gmailSource = File.ReadAllText(Path.Combine(root, "src", "plugins", "Implementations", "CanDoItAll.Plugin.Gmail", "GmailWorkflowExecutor.cs"));
        var office365Source = File.ReadAllText(Path.Combine(root, "src", "plugins", "Implementations", "CanDoItAll.Plugin.Office365", "Office365WorkflowExecutor.cs"));

        Assert.Contains("GmailWorkflowJson.Options", gmailSource, StringComparison.Ordinal);
        Assert.Contains("Office365WorkflowJson.Options", office365Source, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(gmailSource, "new JsonSerializerOptions(JsonSerializerDefaults.Web)"));
        Assert.Equal(1, CountOccurrences(office365Source, "new JsonSerializerOptions(JsonSerializerDefaults.Web)"));
    }

    private static IReadOnlyList<WorkflowExecutorDescriptor> CollectCombinedDescriptors()
    {
        List<WorkflowExecutorDescriptor> descriptors = [];
        descriptors.AddRange(BuiltInWorkflowExecutorDescriptors.All);

        descriptors.Add(CreateRuntimePackageDescriptor("plugin.hardening.bundled") with
        {
            Source = WorkflowExecutorSourceDescriptor.BundledPlugin(
                BundledPluginId.Value,
                "1.0.0",
                "Hardening plugin",
                UiIconDescriptor.MaterialIcon("extension", "Hardening plugin"))
        });

        var runtimePackage = CreatePluginDescriptor(
            PluginSourceKind.LocalPackage,
            PluginTrustLevel.LocalPackage,
            RuntimePackageId);
        var services = new ServiceCollection();
        PluginWorkflowExecutorRuntimeRegistration.RegisterWorkflowExecutors(
            services,
            [typeof(RuntimePackageExecutor)],
            runtimePackage);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        descriptors.AddRange(scope.ServiceProvider
            .GetRequiredService<IEnumerable<IWorkflowExecutorContribution>>()
            .Select(contribution => contribution.Descriptor));

        return descriptors;
    }

    private static PluginDescriptor CreatePluginDescriptor(
        PluginSourceKind sourceKind,
        PluginTrustLevel trustLevel,
        PluginPackageId? packageId)
        => new(
            BundledPluginId,
            "Hardening plugin",
            "Plugin used by executor hardening checkpoint tests.",
            "1.0.0",
            "CanDoItAll",
            sourceKind,
            trustLevel,
            "1.0.0",
            PluginCapabilityKind.WorkflowExecutor,
            [CreatePluginExecutor(sourceKind == PluginSourceKind.Bundled
                ? new WorkflowExecutorId("plugin.hardening.bundled")
                : RuntimePackageExecutorId)],
            PluginSettingsDescriptor.Empty,
            [],
            packageId is null
                ? null
                : new PluginPackageDescriptor(
                    packageId.Value,
                    "1.0.0",
                    "1.0.0",
                    "sha256",
                    "signature"),
            OAuth2: null,
            UiIconDescriptor.MaterialIcon("extension", "Hardening plugin"));

    private static PluginWorkflowExecutorDescriptor CreatePluginExecutor(WorkflowExecutorId executorId)
        => PluginWorkflowExecutorDescriptor.FromWorkflowExecutorDescriptor(
            CreateRuntimePackageDescriptor(executorId.Value));

    private static WorkflowExecutorDescriptor CreateRuntimePackageDescriptor(string id)
        => new(
            new WorkflowExecutorId(id),
            "Runtime package executor",
            "Runtime package executor used by hardening checkpoint tests.",
            WorkflowExecutorCategoryKind.Utility,
            "extension",
            "plugin.hardening",
            WorkflowValueShape.Text,
            new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"),
            "{}",
            "{}",
            WorkflowExecutorExecutionPolicy.Default,
            IsImplemented: true);

    private static WorkflowNode CreateNode(WorkflowExecutorId executorId)
        => new(
            new WorkflowNodeId("plugin-hardening-node"),
            WorkflowNodeKind.Executor,
            "Plugin hardening node",
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: WorkflowValueShape.Text,
                ResultShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"))
            {
                ExecutorId = executorId,
                ExecutorSettingsJson = "{}",
                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
            });

    private static WorkflowDefinition CreateDefinition(WorkflowNode node)
        => new(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Executor hardening workflow",
            "Executor hardening workflow.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(node.Id, [node], []),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
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

    private static readonly PluginId BundledPluginId = new("plugin.hardening");

    private static readonly PluginPackageId RuntimePackageId = new("plugin.hardening.runtime");

    private static readonly WorkflowExecutorId RuntimePackageExecutorId = new("plugin.hardening.runtime-executor");

    private sealed class RuntimePackageExecutor : IWorkflowExecutor
    {
        public WorkflowExecutorDescriptor Descriptor { get; } = CreateRuntimePackageDescriptor(RuntimePackageExecutorId.Value);

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new WorkflowNodeExecutionResult(
                context.Node.Id,
                "{}",
                context.Descriptor.ResultShape));
    }

    private sealed class ThrowingExecutor(
        WorkflowExecutorDescriptor descriptor,
        Exception exception) : IWorkflowExecutor
    {
        public WorkflowExecutorDescriptor Descriptor { get; } = descriptor;

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
            => throw exception;
    }
}
