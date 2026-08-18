using System.Xml.Linq;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.AgentFramework.Workflows.Builder;
using Microsoft.Extensions.DependencyInjection;
using WorkflowCatalogContract = CanDoItAll.AgentFramework.Workflows.Abstractions.IWorkflowCatalogService;
using WorkflowDefinitionValidatorContract = CanDoItAll.AgentFramework.Workflows.Abstractions.IWorkflowDefinitionValidator;
using WorkflowRuntimeBackendCatalogContract = CanDoItAll.AgentFramework.Workflows.Abstractions.IWorkflowRuntimeBackendCatalog;
using WorkflowRuntimeManagerContract = CanDoItAll.AgentFramework.Workflows.Abstractions.IWorkflowRuntimeManager;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowCoreExtractionTests
{
    [Fact]
    public void WorkflowCoreProjectDoesNotReferenceForbiddenImplementationProjects()
    {
        var root = FindRepositoryRoot();
        var projectPath = Path.Combine(
            root,
            "src",
            "MAF",
            "Workflows",
            "CanDoItAll.AgentFramework.Workflows.Core",
            "CanDoItAll.AgentFramework.Workflows.Core.csproj");
        var forbiddenReferences = new[]
        {
            "CanDoItAll.AgentFramework.Maf",
            "CanDoItAll.Modules.AgentFramework",
            "CanDoItAll.Modules.Plugins",
            "CanDoItAll.Plugins.Abstractions",
            "CanDoItAll.AgentFramework.Persistence",
            "CanDoItAll.AgentFramework.Workflows.Runtime",
            "CanDoItAll.Web"
        };

        var project = XDocument.Load(projectPath);
        var references = project
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
            .Concat(project
                .Descendants("PackageReference")
                .Select(element => element.Attribute("Include")?.Value ?? string.Empty))
            .ToArray();

        foreach (var forbiddenReference in forbiddenReferences)
        {
            Assert.DoesNotContain(
                references,
                reference => reference.Contains(forbiddenReference, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void WorkflowContractsAreOwnedOnlyByWorkflowAbstractions()
    {
        var root = FindRepositoryRoot();
        var legacyContractFiles = new[]
        {
            Path.Combine(
                root,
                "src",
                "MAF",
                "Common",
                "CanDoItAll.AgentFramework.Core",
                "Workflows",
                "WorkflowCatalogContracts.cs"),
            Path.Combine(
                root,
                "src",
                "MAF",
                "Workflows",
                "CanDoItAll.AgentFramework.Workflows.Runtime",
                "WorkflowContracts.cs")
        };

        Assert.All(legacyContractFiles, contractFile => Assert.False(File.Exists(contractFile), contractFile));
        var contractAssembly = typeof(WorkflowCatalogContract).Assembly;
        var contractTypes = new[]
        {
            typeof(IWorkflowSettingsService),
            typeof(IWorkflowComponentLibraryService),
            typeof(IWorkflowTestRunner),
            typeof(WorkflowDefinitionValidatorContract),
            typeof(WorkflowRuntimeBackendCatalogContract),
            typeof(WorkflowRuntimeManagerContract),
            typeof(IWorkflowExecutionBackend),
            typeof(IWorkflowRunStore),
            typeof(IWorkflowArtifactContentStore),
            typeof(WorkflowRunPageRequest),
            typeof(WorkflowBackendStartResult)
        };

        Assert.Equal("CanDoItAll.AgentFramework.Workflows.Abstractions", typeof(WorkflowCatalogContract).Namespace);
        Assert.All(contractTypes, contractType => Assert.Equal(contractAssembly, contractType.Assembly));
        Assert.All(
            contractTypes,
            contractType => Assert.Equal("CanDoItAll.AgentFramework.Workflows.Abstractions", contractType.Namespace));
    }

    [Fact]
    public void WorkflowCoreImplementationFilesMovedOutOfAgentFrameworkCoreProject()
    {
        var root = FindRepositoryRoot();
        var movedFiles = new[]
        {
            "WorkflowDefinitionValidator.cs",
            "WorkflowCatalogServices.cs",
            "WorkflowRoutingCompiler.cs",
            "WorkflowPreviewSimulationRenderer.cs",
            "WorkflowPayloadPolicyService.cs",
            "WorkflowFailureDisplayFormatter.cs"
        };

        foreach (var movedFile in movedFiles)
        {
            Assert.False(
                File.Exists(Path.Combine(root, "src", "MAF", "Common", "CanDoItAll.AgentFramework.Core", "Workflows", movedFile)),
                $"{movedFile} must not remain in AgentFramework.Core.");
            Assert.True(
                File.Exists(Path.Combine(root, "src", "MAF", "Workflows", "CanDoItAll.AgentFramework.Workflows.Core", movedFile)),
                $"{movedFile} must exist in Workflows.Core.");
        }
    }

    [Fact]
    public void WorkflowCoreRegistrationExtensionOwnsCoreServiceRegistrations()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWorkflowExecutorCatalog>(WorkflowExecutorCatalog.FromDescriptors([]));
        services.AddWorkflowCoreServices();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(WorkflowDefinitionValidatorContract));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(WorkflowRuntimeBackendCatalogContract));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IWorkflowPayloadPolicyService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IWorkflowTestRunner));

        using var provider = services.BuildServiceProvider();
        Assert.IsType<WorkflowDefinitionValidator>(provider.GetRequiredService<WorkflowDefinitionValidatorContract>());
        Assert.IsType<WorkflowRuntimeBackendCatalog>(provider.GetRequiredService<WorkflowRuntimeBackendCatalogContract>());
        Assert.IsType<WorkflowPayloadPolicyService>(provider.GetRequiredService<IWorkflowPayloadPolicyService>());
    }

    [Fact]
    public void DirectRuntimeProcessBridgeWasRemovedInFavorOfGovernedModuleDriver()
    {
        var root = FindRepositoryRoot();
        var bridgePath = Path.Combine(
            root,
            "src",
            "MAF",
            "Workflows",
            "CanDoItAll.AgentFramework.Workflows.Core",
            "WorkflowProcessExecutorBridge.cs");
        var contractSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MAF",
            "Workflows",
            "CanDoItAll.AgentFramework.Workflows.Abstractions",
            "WorkflowRuntimeContracts.cs"));
        var registrationSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MAF",
            "Workflows",
            "CanDoItAll.AgentFramework.Workflows.Core",
            "WorkflowCoreServiceCollectionExtensions.cs"));

        Assert.False(File.Exists(bridgePath));
        Assert.DoesNotContain("IWorkflowProcessExecutorBridge", contractSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkflowProcessExecutorBridge", registrationSource, StringComparison.Ordinal);
    }

    [Fact]
    public void HostAndModuleRegistrationUseWorkflowCoreExtension()
    {
        var root = FindRepositoryRoot();
        var registrationFiles = new[]
        {
            Path.Combine(root, "src", "MAF", "Common", "CanDoItAll.AgentFramework.Hosting", "AgentFrameworkServiceCollectionExtensions.cs"),
            Path.Combine(root, "src", "Modules", "CanDoItAll.Modules.AgentFramework", "Services", "AgentFrameworkModuleServiceCollectionExtensions.cs")
        };

        foreach (var registrationFile in registrationFiles)
        {
            var source = File.ReadAllText(registrationFile);

            Assert.Contains("AddMafWorkflowAdapterServices", source, StringComparison.Ordinal);
            Assert.DoesNotContain("TryAddScoped<IWorkflowDefinitionValidator>", source, StringComparison.Ordinal);
            Assert.DoesNotContain("TryAddScoped<IWorkflowPayloadPolicyService, WorkflowPayloadPolicyService>", source, StringComparison.Ordinal);
            Assert.DoesNotContain("TryAddScoped<IWorkflowProcessExecutorBridge, WorkflowProcessExecutorBridge>", source, StringComparison.Ordinal);
            Assert.DoesNotContain("TryAddScoped<IWorkflowTestRunner, WorkflowTestRunner>", source, StringComparison.Ordinal);
        }

        var adapterRegistrationFile = Path.Combine(
            root,
            "src",
            "MAF",
            "Workflows",
            "CanDoItAll.AgentFramework.Workflows.MafAdapter",
            "MafWorkflowAdapterServiceCollectionExtensions.cs");
        var adapterSource = File.ReadAllText(adapterRegistrationFile);

        Assert.Contains("AddWorkflowCoreServices()", adapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryAddScoped<IWorkflowDefinitionValidator>", adapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryAddScoped<IWorkflowPayloadPolicyService, WorkflowPayloadPolicyService>", adapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryAddScoped<IWorkflowProcessExecutorBridge, WorkflowProcessExecutorBridge>", adapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryAddScoped<IWorkflowTestRunner, WorkflowTestRunner>", adapterSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CatalogValidationFailureCarriesTypedRepairableDiagnostics()
    {
        var catalog = new InMemoryWorkflowCatalogService(
            new InMemoryWorkflowCatalogStore(),
            new WorkflowDefinitionValidator());
        var invalidDefinition = WorkflowFixtureFactory.CreateInvalidMissingStartWorkflow();
        var request = new WorkflowDefinitionSaveRequest(
            Id: null,
            ExpectedVersionId: null,
            invalidDefinition.Name,
            invalidDefinition.Description,
            invalidDefinition.Status,
            invalidDefinition.Graph,
            invalidDefinition.RuntimePolicy);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => catalog.SaveDefinitionAsync(request));
        var diagnostics = WorkflowFailureDiagnosticMapper.GetDiagnostics(exception);
        var diagnostic = Assert.Single(
            diagnostics,
            item => item.Kind == WorkflowFailureKind.Validation &&
                    item.NodeId == new WorkflowNodeId("__missing-start__"));

        Assert.Equal(WorkflowFailureRetryability.RetryableAfterRepair, diagnostic.Retryability);
        Assert.Equal(WorkflowFailureSourceKind.Workflow, diagnostic.Source.Kind);
        Assert.Contains("start node", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("start node", diagnostic.RepairHint, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connectionString", diagnostic.RedactedTechnicalDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TypedFailureDisplayUsesDiagnosticContext()
    {
        var diagnostic = WorkflowFixtureFactory.CreateExecutorFailureDiagnostic(
            new WorkflowNodeId("read-project"),
            WorkflowExecutorIds.ProjectStructure,
            "corr-typed-display");

        var message = WorkflowFailureDisplayFormatter.ToUserMessage(diagnostic);

        Assert.Contains("read-project", message, StringComparison.Ordinal);
        Assert.Contains(WorkflowExecutorIds.ProjectStructure.Value, message, StringComparison.Ordinal);
        Assert.Contains("Fix the executor settings JSON", message, StringComparison.Ordinal);
    }

    [Fact]
    public void TypedFailureDisplayUsesEventPayloadDiagnostic()
    {
        var diagnostic = WorkflowFixtureFactory.CreateExecutorFailureDiagnostic(
            new WorkflowNodeId("store-project"),
            WorkflowExecutorIds.ProjectStructure,
            "corr-event-display");
        var payloadJson = WorkflowEventPayloads.Serialize(
            WorkflowEventPayloadSource.Runtime,
            "WorkflowExecutorFailed",
            nodeId: new WorkflowNodeId("store-project"),
            executorId: WorkflowExecutorIds.ProjectStructure,
            inlineJson: WorkflowRuntimeFailureDiagnosticMapper.Serialize(diagnostic));
        var workflowEvent = new WorkflowEventRecord(
            Guid.NewGuid(),
            WorkflowRunId.New(),
            WorkflowEventKind.ExecutorFailed,
            new WorkflowNodeId("store-project"),
            "Workflow executor failed with token raw-token-value.",
            payloadJson,
            DateTimeOffset.UtcNow);

        var message = WorkflowFailureDisplayFormatter.ToUserMessage(workflowEvent);

        Assert.Contains("store-project", message, StringComparison.Ordinal);
        Assert.Contains(WorkflowExecutorIds.ProjectStructure.Value, message, StringComparison.Ordinal);
        Assert.Contains("Fix the executor settings JSON", message, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-token-value", message, StringComparison.Ordinal);
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
}
