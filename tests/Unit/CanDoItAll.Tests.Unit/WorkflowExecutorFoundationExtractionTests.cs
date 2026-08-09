using System.Xml.Linq;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.SharedKernel.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowExecutorFoundationExtractionTests
{
    [Fact]
    public void WorkflowExecutorFoundationProjectsHaveBoundedDependencies()
    {
        var abstractionReferences = ReadProjectReferences("src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions/CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions.csproj");
        Assert.Equal(
            ["CanDoItAll.AgentFramework.Models"],
            abstractionReferences.ProjectReferences);
        Assert.Empty(abstractionReferences.PackageReferences);

        var coreReferences = ReadProjectReferences("src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Core/CanDoItAll.AgentFramework.WorkflowExecutors.Core.csproj");
        Assert.Equal(
            [
                "CanDoItAll.AgentFramework.Models",
                "CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions",
                "CanDoItAll.AgentFramework.Workflows.Abstractions",
                "CanDoItAll.SharedKernel"
            ],
            coreReferences.ProjectReferences);
        Assert.Equal(
            ["Microsoft.Extensions.DependencyInjection.Abstractions"],
            coreReferences.PackageReferences);

        var forbiddenReferences = new[]
        {
            "CanDoItAll.AgentFramework.Core",
            "CanDoItAll.AgentFramework.Maf",
            "CanDoItAll.Modules.AgentFramework",
            "CanDoItAll.Modules.Plugins",
            "CanDoItAll.Modules.CognitiveMemory",
            "CanDoItAll.Plugins.Abstractions",
            "CanDoItAll.Web"
        };
        foreach (var forbiddenReference in forbiddenReferences)
        {
            Assert.DoesNotContain(coreReferences.ProjectReferences, reference => reference == forbiddenReference);
            Assert.DoesNotContain(abstractionReferences.ProjectReferences, reference => reference == forbiddenReference);
        }
    }

    [Fact]
    public void ExecutorContractsAndSharedHelpersMovedOutOfOldOwners()
    {
        var root = FindRepositoryRoot();
        var oldCoreFiles = new[]
        {
            Path.Combine(root, "src", "MAF", "Common", "CanDoItAll.AgentFramework.Core", "Workflows", "WorkflowExecutorContracts.cs"),
            Path.Combine(root, "src", "MAF", "Common", "CanDoItAll.AgentFramework.Core", "Workflows", "WorkflowExecutorObservability.cs")
        };

        foreach (var oldCoreFile in oldCoreFiles)
        {
            Assert.False(File.Exists(oldCoreFile), $"{oldCoreFile} must not remain in AgentFramework.Core.");
        }

        Assert.True(File.Exists(Path.Combine(root, "src", "MAF", "WorkflowExecutors", "CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions", "WorkflowExecutorContracts.cs")));
        Assert.True(File.Exists(Path.Combine(root, "src", "MAF", "WorkflowExecutors", "CanDoItAll.AgentFramework.WorkflowExecutors.Core", "WorkflowExecutorInvoker.cs")));
        Assert.True(File.Exists(Path.Combine(root, "src", "MAF", "WorkflowExecutors", "CanDoItAll.AgentFramework.WorkflowExecutors.Core", "WorkflowExecutorJson.cs")));
        Assert.False(File.Exists(Path.Combine(root, "src", "MAF", "Common", "CanDoItAll.AgentFramework.Maf", "Runtime", "Workflows", "WorkflowExecutorJson.cs")));

        var builtInDescriptorSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MAF",
            "WorkflowExecutors",
            "CanDoItAll.AgentFramework.WorkflowExecutors.Core",
            "BuiltInWorkflowExecutorDescriptors.cs"));
        Assert.Contains("WorkflowExecutorDescriptorFactory.CreateImplemented", builtInDescriptorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BindingFlags", builtInDescriptorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateSettingsConfigurationSchema", builtInDescriptorSource, StringComparison.Ordinal);
    }

    [Fact]
    public void HostAndModuleRegistrationUseExecutorCoreExtension()
    {
        var root = FindRepositoryRoot();
        var registrationFiles = new[]
        {
            Path.Combine(root, "src", "MAF", "Common", "CanDoItAll.AgentFramework.Hosting", "AgentFrameworkServiceCollectionExtensions.cs"),
            Path.Combine(root, "src", "Modules", "CanDoItAll.Modules.AgentFramework", "Services", "AgentFrameworkModuleServiceCollectionExtensions.cs")
        };
        var adapterSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MAF",
            "Workflows",
            "CanDoItAll.AgentFramework.Workflows.MafAdapter",
            "MafWorkflowAdapterServiceCollectionExtensions.cs"));

        foreach (var registrationFile in registrationFiles)
        {
            var source = File.ReadAllText(registrationFile);

            Assert.Contains("AddMafWorkflowAdapterServices", source, StringComparison.Ordinal);
            Assert.DoesNotContain("TryAddScoped<IWorkflowExecutorCatalog>", source, StringComparison.Ordinal);
            Assert.DoesNotContain("TryAddScoped<IWorkflowExecutorExecutionObserver", source, StringComparison.Ordinal);
            Assert.DoesNotContain("TryAddScoped<IWorkflowExecutorInvoker", source, StringComparison.Ordinal);
        }

        Assert.Contains("AddWorkflowExecutorCoreServices()", adapterSource, StringComparison.Ordinal);
    }

    [Fact]
    public void DescriptorFactoryCreatesTypedSchemaAndSharedJson()
    {
        var descriptor = WorkflowExecutorDescriptorFactory.CreateImplemented(
            new WorkflowExecutorId("test.executor.factory"),
            "Factory executor",
            "Factory executor for tests.",
            WorkflowExecutorCategoryKind.Utility,
            "settings",
            "test.factory",
            new FactorySettings
            {
                Enabled = true,
                Limit = 7,
                ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Mode = FactoryMode.Strict,
                Filter = new FactoryFilter { Query = "open" }
            },
            WorkflowExecutorSourceDescriptor.BuiltIn());

        Assert.Equal(WorkflowExecutorDescriptorFactory.SettingsSchemaVersion, descriptor.SettingsSchema.Version);
        Assert.Equal(WorkflowExecutorDescriptorFactory.DefaultObjectJsonSchema, descriptor.SettingsSchema.SchemaJson);
        Assert.Contains("\"enabled\":true", descriptor.DefaultSettingsJson, StringComparison.Ordinal);
        Assert.Contains("\"mode\":\"Strict\"", descriptor.DefaultSettingsJson, StringComparison.Ordinal);

        var fields = descriptor.ConfigurationSchema.Fields.ToDictionary(field => field.Key);
        Assert.Equal(ConfigurationFieldType.Boolean, fields["enabled"].FieldType);
        Assert.Equal(ConfigurationFieldType.Number, fields["limit"].FieldType);
        Assert.Equal(ConfigurationNumberKind.Int32, fields["limit"].NumberKind);
        Assert.Equal(ConfigurationFieldType.Guid, fields["projectId"].FieldType);
        Assert.Equal(ConfigurationFieldType.Select, fields["mode"].FieldType);
        Assert.Equal(ConfigurationFieldType.Json, fields["filter"].FieldType);
        Assert.Contains(fields["mode"].Options, option => option.Value == nameof(FactoryMode.Strict));

        var settings = WorkflowExecutorJson.Deserialize<FactorySettings>(descriptor.DefaultSettingsJson);
        Assert.True(settings.Enabled);
        Assert.Equal(FactoryMode.Strict, settings.Mode);
    }

    [Fact]
    public void BuiltInDescriptorsUseSharedFactoryOutput()
    {
        var delay = BuiltInWorkflowExecutorDescriptors.Delay;
        Assert.Equal(WorkflowExecutorDescriptorFactory.SettingsSchemaVersion, delay.ConfigurationSchema.Version);
        Assert.Contains(delay.ConfigurationSchema.Fields, field => field.Key == "delayMilliseconds" && field.FieldType == ConfigurationFieldType.Number);
        Assert.Contains("\"delayMilliseconds\":1000", delay.DefaultSettingsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingExecutorIdFailureCarriesTypedRepairableDiagnostics()
    {
        var node = CreateNode(executorId: null);
        var definition = CreateDefinition(node);
        var invoker = new WorkflowExecutorInvoker(WorkflowExecutorCatalog.FromDescriptors([]), []);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            invoker.ExecuteAsync(definition, node, new WorkflowNodeInput("{}")).AsTask());
        var diagnostic = Assert.Single(WorkflowExecutorFailureDiagnosticMapper.GetDiagnostics(exception));

        Assert.Equal(WorkflowFailureKind.Executor, diagnostic.Kind);
        Assert.Equal(WorkflowFailureRetryability.RetryableAfterRepair, diagnostic.Retryability);
        Assert.Equal(node.Id, diagnostic.NodeId);
        Assert.Null(diagnostic.ExecutorId);
        Assert.Equal(WorkflowFailureSourceKind.Node, diagnostic.Source.Kind);
        Assert.Contains("Select a registered workflow executor", diagnostic.RepairHint, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecutorInvocationFailureCarriesRedactedDiagnostics()
    {
        var descriptor = CreateDescriptor("test.executor.diagnostics");
        var executor = new ThrowingExecutor(
            descriptor,
            new InvalidOperationException("Remote API rejected token=raw-token-value and Authorization: Bearer sk-test-secret-value."));
        var node = CreateNode(descriptor.Id);
        var invoker = new WorkflowExecutorInvoker(new WorkflowExecutorCatalog([executor]), [executor]);

        var exception = await Assert.ThrowsAsync<WorkflowExecutorInvocationException>(() =>
            invoker.ExecuteAsync(CreateDefinition(node), node, new WorkflowNodeInput("{}")).AsTask());
        var diagnostic = Assert.Single(WorkflowExecutorFailureDiagnosticMapper.GetDiagnostics(exception));

        Assert.Equal(WorkflowFailureKind.Executor, diagnostic.Kind);
        Assert.Equal(WorkflowFailureRetryability.RetryableAfterRepair, diagnostic.Retryability);
        Assert.Equal(descriptor.Id, diagnostic.ExecutorId);
        Assert.Equal(node.Id, diagnostic.NodeId);
        Assert.Contains("[REDACTED]", diagnostic.RedactedTechnicalDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-token-value", diagnostic.RedactedTechnicalDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-test-secret-value", diagnostic.RedactedTechnicalDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-token-value", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApprovalDeniedFailureCarriesApprovalDiagnosticsBeforeExecution()
    {
        var descriptor = CreateDescriptor("test.executor.approval") with
        {
            PermissionPolicy = new WorkflowExecutorPermissionPolicy(
                WorkflowExecutorCapabilityFlags.WritesExternalData | WorkflowExecutorCapabilityFlags.UsesSecrets,
                WorkflowExecutorApprovalRequirement.AlwaysRequired)
        };
        var executor = new RecordingExecutor(descriptor);
        var node = CreateNode(descriptor.Id);
        var invoker = new WorkflowExecutorInvoker(
            new WorkflowExecutorCatalog([executor]),
            [executor],
            approvalGate: new DenyingApprovalGate("Denied Authorization: Bearer sk-test-secret-value."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            invoker.ExecuteAsync(CreateDefinition(node), node, new WorkflowNodeInput("{}")).AsTask());
        var diagnostic = Assert.Single(WorkflowExecutorFailureDiagnosticMapper.GetDiagnostics(exception));

        Assert.Equal(WorkflowFailureKind.Approval, diagnostic.Kind);
        Assert.Equal(WorkflowFailureRetryability.RetryableAfterRepair, diagnostic.Retryability);
        Assert.Equal(descriptor.Id, diagnostic.ExecutorId);
        Assert.Equal(0, executor.InvocationCount);
        Assert.Contains("[REDACTED]", diagnostic.RedactedTechnicalDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-test-secret-value", diagnostic.RedactedTechnicalDetail, StringComparison.Ordinal);
    }

    [Fact]
    public void FeatureModulesAndPluginsReferenceExecutorFoundationProjects()
    {
        AssertProjectReferences(
            "src/Modules/CanDoItAll.Modules.Plugins/CanDoItAll.Modules.Plugins.csproj",
            "CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions");
        AssertProjectReferences(
            "src/plugins/Implementations/CanDoItAll.Plugin.Gmail/CanDoItAll.Plugin.Gmail.csproj",
            "CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions");
        AssertProjectReferences(
            "src/plugins/Implementations/CanDoItAll.Plugin.Gmail/CanDoItAll.Plugin.Gmail.csproj",
            "CanDoItAll.AgentFramework.WorkflowExecutors.Core");
        AssertProjectReferences(
            "src/plugins/Implementations/CanDoItAll.Plugin.Office365/CanDoItAll.Plugin.Office365.csproj",
            "CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions");
        AssertProjectReferences(
            "src/plugins/Implementations/CanDoItAll.Plugin.Office365/CanDoItAll.Plugin.Office365.csproj",
            "CanDoItAll.AgentFramework.WorkflowExecutors.Core");
        AssertProjectReferences(
            "src/plugins/Implementations/CanDoItAll.Plugin.Docker/CanDoItAll.Plugin.Docker.csproj",
            "CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions");
        AssertProjectReferences(
            "src/plugins/Implementations/CanDoItAll.Plugin.Docker/CanDoItAll.Plugin.Docker.csproj",
            "CanDoItAll.AgentFramework.WorkflowExecutors.Core");
    }

    private static WorkflowExecutorDescriptor CreateDescriptor(string id)
        => new(
            new WorkflowExecutorId(id),
            "Test executor",
            "Test executor for diagnostics.",
            WorkflowExecutorCategoryKind.Utility,
            "settings",
            "test.executor",
            WorkflowValueShape.Text,
            WorkflowExecutorDescriptorFactory.JsonShape,
            WorkflowExecutorDescriptorFactory.DefaultObjectJsonSchema,
            "{}",
            WorkflowExecutorExecutionPolicy.Default,
            IsImplemented: true);

    private static WorkflowNode CreateNode(WorkflowExecutorId? executorId)
        => new(
            new WorkflowNodeId("executor-node"),
            WorkflowNodeKind.Executor,
            "Executor node",
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: WorkflowValueShape.Text,
                ResultShape: WorkflowExecutorDescriptorFactory.JsonShape)
            {
                ExecutorId = executorId,
                ExecutorSettingsJson = """{"token":"raw-token-value"}""",
                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
            });

    private static WorkflowDefinition CreateDefinition(WorkflowNode node)
        => new(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Executor diagnostics workflow",
            "Executor diagnostics workflow.",
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

    private static void AssertProjectReferences(string relativeProjectPath, string expectedProjectName)
    {
        var references = ReadProjectReferences(relativeProjectPath);
        Assert.Contains(expectedProjectName, references.ProjectReferences);
    }

    private static ProjectReferenceSnapshot ReadProjectReferences(string relativeProjectPath)
    {
        var projectPath = Path.Combine(FindRepositoryRoot(), relativeProjectPath.Replace('/', Path.DirectorySeparatorChar));
        var project = XDocument.Load(projectPath);
        var projectReferences = project
            .Descendants("ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension(
                (element.Attribute("Include")?.Value ?? string.Empty).Replace('\\', '/')))
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .ToArray();
        var packageReferences = project
            .Descendants("PackageReference")
            .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .ToArray();
        return new ProjectReferenceSnapshot(projectReferences, packageReferences);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }

    private sealed record ProjectReferenceSnapshot(
        IReadOnlyList<string> ProjectReferences,
        IReadOnlyList<string> PackageReferences);

    private enum FactoryMode
    {
        Relaxed,
        Strict
    }

    private sealed record FactorySettings
    {
        public bool Enabled { get; init; }

        public int Limit { get; init; }

        public Guid ProjectId { get; init; }

        public FactoryMode Mode { get; init; }

        public FactoryFilter Filter { get; init; } = new();
    }

    private sealed record FactoryFilter
    {
        public string Query { get; init; } = string.Empty;
    }

    private class RecordingExecutor(WorkflowExecutorDescriptor descriptor) : IWorkflowExecutor
    {
        public WorkflowExecutorDescriptor Descriptor { get; } = descriptor;

        public int InvocationCount { get; private set; }

        public virtual ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
                context.Node.Id,
                "{}",
                context.Descriptor.ResultShape));
        }
    }

    private sealed class ThrowingExecutor(
        WorkflowExecutorDescriptor descriptor,
        Exception exception) : RecordingExecutor(descriptor)
    {
        public override ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
            => throw exception;
    }

    private sealed class DenyingApprovalGate(string message) : IWorkflowExecutorApprovalGate
    {
        public ValueTask<WorkflowExecutorApprovalDecision> RequestApprovalAsync(
            WorkflowExecutorApprovalRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new WorkflowExecutorApprovalDecision(false, message));
        }
    }
}
