namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowAdoptionHardeningCheckpointTests
{
    private static readonly string[] AdoptionSourceFiles =
    [
        @"src\App\CanDoItAll.Web\Api\WorkflowsApi.cs",
        @"src\Modules\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor",
        @"src\Modules\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor.cs",
        @"src\Modules\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor",
        @"src\Modules\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor.cs",
        @"src\Modules\CanDoItAll.Modules.Workbench\CanDoItAll.Modules.Workbench.csproj",
        @"src\Modules\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureAgentContracts.cs",
        @"src\Modules\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureWorkflowNodeService.cs",
        @"src\Modules\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.WorkflowNodes.cs",
        @"src\Modules\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.SelectionPanel.cs"
    ];

    [Fact]
    public void ApiUiWorkbenchAdoptionDoesNotReferenceMafInternalsOrOldExecutorAliases()
    {
        var source = ReadCombinedSource(AdoptionSourceFiles);
        var forbiddenSnippets = new[]
        {
            "MafWorkflowCompiler",
            "MafInProcessWorkflowExecutionBackend",
            "MafWorkflowEventNormalizer",
            "MafWorkflowLlmComponentInvoker",
            "AddBuiltInWorkflowExecutors",
            "Microsoft.Agents.AI.Workflows"
        };

        foreach (var forbiddenSnippet in forbiddenSnippets)
        {
            Assert.DoesNotContain(forbiddenSnippet, source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void WorkflowUiAndWorkbenchAdoptionUseTypedFailureDisplayBoundary()
    {
        var root = FindRepositoryRoot();
        var formatterSource = File.ReadAllText(TestRepositoryPath.Resolve(
            root,
            @"src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.Core\WorkflowFailureDisplayFormatter.cs"));
        var workflowsPageCode = File.ReadAllText(TestRepositoryPath.Resolve(
            root,
            @"src\Modules\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor.cs"));
        var workflowNodeService = File.ReadAllText(TestRepositoryPath.Resolve(
            root,
            @"src\Modules\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureWorkflowNodeService.cs"));
        var workbenchContracts = File.ReadAllText(TestRepositoryPath.Resolve(
            root,
            @"src\Modules\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureAgentContracts.cs"));

        Assert.Contains("ToUserMessage(WorkflowEventRecord workflowEvent)", formatterSource, StringComparison.Ordinal);
        Assert.Contains("TryResolveDiagnosticTechnicalDetail", formatterSource, StringComparison.Ordinal);
        Assert.Contains("WorkflowEventPayloadEnvelope", formatterSource, StringComparison.Ordinal);
        Assert.Contains("ResolveEventDisplayMessage(WorkflowEventRecord workflowEvent)", workflowsPageCode, StringComparison.Ordinal);
        Assert.Contains("WorkflowFailureDisplayFormatter.ToUserMessage(workflowEvent)", workflowsPageCode, StringComparison.Ordinal);
        Assert.Contains("WorkflowFailureDisplayFormatter.TryResolveDiagnosticTechnicalDetail", workflowsPageCode, StringComparison.Ordinal);
        Assert.Contains("WorkflowFailureDisplayFormatter.ToUserMessage(workflowEvent)", workflowNodeService, StringComparison.Ordinal);
        Assert.Contains(".Select(WorkflowFailureDisplayFormatter.ToUserMessage)", workflowNodeService, StringComparison.Ordinal);
        Assert.Contains("string PayloadJson = \"\"", workbenchContracts, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkflowUiAndWorkbenchDoNotUseRawEventMessageDisplayOrMessageOnlyStatus()
    {
        var root = FindRepositoryRoot();
        var workflowsPageMarkup = File.ReadAllText(TestRepositoryPath.Resolve(
            root,
            @"src\Modules\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor"));
        var workflowsPageCode = File.ReadAllText(TestRepositoryPath.Resolve(
            root,
            @"src\Modules\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor.cs"));
        var workflowNodeService = File.ReadAllText(TestRepositoryPath.Resolve(
            root,
            @"src\Modules\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureWorkflowNodeService.cs"));

        Assert.DoesNotContain("@workflowEvent.Message", workflowsPageMarkup, StringComparison.Ordinal);
        Assert.DoesNotContain("Truncate(workflowEvent.Message", workflowsPageCode, StringComparison.Ordinal);
        Assert.DoesNotContain("ToUserMessage(workflowEvent.Message)", workflowsPageCode, StringComparison.Ordinal);
        Assert.DoesNotContain("Message = message", workflowNodeService, StringComparison.Ordinal);
        Assert.DoesNotContain("Summary = message", workflowNodeService, StringComparison.Ordinal);
    }

    [Fact]
    public void TypedDiagnosticDeserializationStaysOutOfUiAndWorkbenchAdoptionCode()
    {
        var adoptionSource = ReadCombinedSource(AdoptionSourceFiles);

        Assert.DoesNotContain("WorkflowFailureDiagnosticEnvelope", adoptionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer.Deserialize<WorkflowEventPayloadEnvelope>", adoptionSource, StringComparison.Ordinal);
    }

    [Fact]
    public void AdoptionSourceHasNoStubMarkersOrGenericWorkflowErrors()
    {
        var source = ReadCombinedSource(AdoptionSourceFiles);
        var forbiddenSnippets = new[]
        {
            "TODO",
            "NotImplemented",
            "throw new NotImplementedException",
            "generic error",
            "unknown error",
            "something went wrong",
            "Catalog\\WorkflowTemplatePackLoader"
        };

        foreach (var forbiddenSnippet in forbiddenSnippets)
        {
            Assert.DoesNotContain(forbiddenSnippet, source, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string ReadCombinedSource(IEnumerable<string> relativePaths)
    {
        var root = FindRepositoryRoot();
        return string.Join(
            Environment.NewLine,
            relativePaths.Select(relativePath => File.ReadAllText(TestRepositoryPath.Resolve(root, relativePath))));
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
