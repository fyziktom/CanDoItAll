using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Workflows.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class MafWorkflowAdapterIsolationTests
{
    [Fact]
    public void Maf_workflow_adapter_types_live_in_adapter_assembly()
    {
        Assert.Equal(
            "CanDoItAll.AgentFramework.Workflows.MafAdapter",
            typeof(MafWorkflowCompiler).Assembly.GetName().Name);
        Assert.Equal(
            "CanDoItAll.AgentFramework.Workflows.MafAdapter",
            typeof(MafInProcessWorkflowExecutionBackend).Assembly.GetName().Name);
        Assert.Equal(
            "CanDoItAll.AgentFramework.Workflows.MafAdapter",
            typeof(MafWorkflowEventNormalizer).Assembly.GetName().Name);
        // The neutral workflow LLM invoker moved out of the MAF adapter into
        // the provider-neutral Workflows runtime.
        Assert.Equal(
            "CanDoItAll.AgentFramework.Workflows.Runtime",
            typeof(WorkflowLlmComponentInvoker).Assembly.GetName().Name);

        Assert.Equal(
            "CanDoItAll.AgentFramework.Maf",
            typeof(MafAgentRuntime).Assembly.GetName().Name);
    }

    [Fact]
    public void Workflow_owned_projects_do_not_reference_maf_adapter_or_maf_project()
    {
        var root = FindRepoRoot();
        var workflowProjectFiles = new[]
        {
            @"src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.Abstractions\CanDoItAll.AgentFramework.Workflows.Abstractions.csproj",
            @"src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.Builder\CanDoItAll.AgentFramework.Workflows.Builder.csproj",
            @"src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.Core\CanDoItAll.AgentFramework.Workflows.Core.csproj",
            @"src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.Runtime\CanDoItAll.AgentFramework.Workflows.Runtime.csproj",
            @"src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.Templates\CanDoItAll.AgentFramework.Workflows.Templates.csproj",
            @"src\MAF\WorkflowExecutors\CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions\CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions.csproj",
            @"src\MAF\WorkflowExecutors\CanDoItAll.AgentFramework.WorkflowExecutors.Core\CanDoItAll.AgentFramework.WorkflowExecutors.Core.csproj",
            @"src\MAF\WorkflowExecutors\Standard\CanDoItAll.AgentFramework.WorkflowExecutors.Standard\CanDoItAll.AgentFramework.WorkflowExecutors.Standard.csproj",
            @"src\MAF\WorkflowExecutors\CanDoItAll.AgentFramework.WorkflowExecutors.Plugins\CanDoItAll.AgentFramework.WorkflowExecutors.Plugins.csproj"
        };

        foreach (var relativePath in workflowProjectFiles)
        {
            var text = File.ReadAllText(TestRepositoryPath.Resolve(root, relativePath));
            Assert.DoesNotContain("CanDoItAll.AgentFramework.Maf", text, StringComparison.Ordinal);
            Assert.DoesNotContain("CanDoItAll.AgentFramework.Workflows.MafAdapter", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Host_and_module_delegate_maf_workflow_registration_to_adapter_extension()
    {
        var root = FindRepoRoot();
        var hostRegistration = File.ReadAllText(TestRepositoryPath.Resolve(
            root,
            @"src\MAF\Common\CanDoItAll.AgentFramework.Hosting\AgentFrameworkServiceCollectionExtensions.cs"));
        var moduleRegistration = File.ReadAllText(TestRepositoryPath.Resolve(
            root,
            @"src\Modules\CanDoItAll.Modules.AgentFramework\Services\AgentFrameworkModuleServiceCollectionExtensions.cs"));

        Assert.Contains("AddMafWorkflowAdapterServices(ServiceLifetime.Scoped)", hostRegistration, StringComparison.Ordinal);
        Assert.Contains("AddMafWorkflowAdapterServices(ServiceLifetime.Scoped)", moduleRegistration, StringComparison.Ordinal);

        foreach (var registration in new[] { hostRegistration, moduleRegistration })
        {
            Assert.DoesNotContain("TryAddScoped<MafWorkflowCompiler>", registration, StringComparison.Ordinal);
            Assert.DoesNotContain("MafInProcessWorkflowExecutionBackend>", registration, StringComparison.Ordinal);
            Assert.DoesNotContain("AddStandardWorkflowExecutors(", registration, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Adapter_backend_source_is_split_by_responsibility()
    {
        var root = FindRepoRoot();
        var adapterRoot = Path.Combine(root, "src", "MAF", "Workflows", "CanDoItAll.AgentFramework.Workflows.MafAdapter");
        var expectedFiles = new[]
        {
            "MafInProcessWorkflowExecutionBackend.cs",
            "MafWorkflowCompiler.cs",
            "MafWorkflowEventNormalizer.cs",
            "MafConfiguredFileArtifactResolver.cs",
            "WorkflowBackendExternalRequestCapture.cs",
            "WorkflowBackendProgressEventObserver.cs",
            "MafWorkflowAdapterServiceCollectionExtensions.cs"
        };

        foreach (var fileName in expectedFiles)
        {
            Assert.True(File.Exists(Path.Combine(adapterRoot, fileName)), $"Missing adapter file: {fileName}");
        }

        var handoffFactoryPath = Path.Combine(
            root,
            "src",
            "MAF",
            "Common",
            "CanDoItAll.AgentFramework.Maf",
            "Runtime",
            "Handoffs",
            "MafHandoffWorkflowFactory.cs");
        Assert.True(
            File.Exists(handoffFactoryPath),
            "MafHandoffWorkflowFactory.cs moved out of the workflow adapter into the MAF runtime and must live at Runtime/Handoffs.");

        var mafProjectText = File.ReadAllText(TestRepositoryPath.Resolve(
            root,
            @"src\MAF\Common\CanDoItAll.AgentFramework.Maf\CanDoItAll.AgentFramework.Maf.csproj"));
        Assert.DoesNotContain("Workflows.MafAdapter", mafProjectText, StringComparison.Ordinal);

        var mafWorkflowDirectory = Path.Combine(root, "src", "MAF", "Common", "CanDoItAll.AgentFramework.Maf", "Runtime", "Workflows");
        var remainingMafWorkflowFiles = Directory.Exists(mafWorkflowDirectory)
            ? Directory.GetFiles(mafWorkflowDirectory, "*.cs", SearchOption.AllDirectories)
            : [];
        Assert.Empty(remainingMafWorkflowFiles);

        Assert.InRange(CountLines(Path.Combine(adapterRoot, "MafInProcessWorkflowExecutionBackend.cs")), 1, 500);
    }

    private static int CountLines(string path)
        => File.ReadLines(path).Count();

    private static string FindRepoRoot()
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

        throw new InvalidOperationException("Unable to locate repository root.");
    }
}
