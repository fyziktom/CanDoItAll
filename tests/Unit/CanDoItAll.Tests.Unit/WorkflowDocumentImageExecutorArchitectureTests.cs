using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Documents;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Media;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowDocumentImageExecutorArchitectureTests
{
    [Theory]
    [InlineData("DocumentToMarkdown", "document.to-markdown")]
    [InlineData("ImageInspect", "image.inspect")]
    [InlineData("ImageAnalyze", "image.analyze")]
    public void Workflow_executor_ids_expose_stable_typed_members(string propertyName, string expectedId)
    {
        var property = typeof(WorkflowExecutorIds).GetProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal(expectedId, Assert.IsType<WorkflowExecutorId>(property.GetValue(null)).Value);
    }

    [Theory]
    [InlineData("WorkflowDocumentToMarkdownExecutorSettings")]
    [InlineData("WorkflowImageInspectExecutorSettings")]
    [InlineData("WorkflowImageAnalyzeExecutorSettings")]
    public void Workflow_executor_settings_are_strongly_typed_models(string typeName)
    {
        var type = typeof(WorkflowExecutorIds).Assembly.GetType($"CanDoItAll.AgentFramework.Models.{typeName}");

        Assert.NotNull(type);
    }

    [Theory]
    [InlineData("DocumentToMarkdown", "document.to-markdown")]
    [InlineData("ImageInspect", "image.inspect")]
    [InlineData("ImageAnalyze", "image.analyze")]
    public void Built_in_descriptors_expose_document_and_image_nodes(string propertyName, string expectedId)
    {
        var property = typeof(BuiltInWorkflowExecutorDescriptors).GetProperty(propertyName);

        Assert.NotNull(property);
        var descriptor = Assert.IsType<WorkflowExecutorDescriptor>(property.GetValue(null));
        Assert.Equal(expectedId, descriptor.Id.Value);
        Assert.True(descriptor.CanExecute);
    }

    [Theory]
    [InlineData("DocumentToMarkdownWorkflowExecutor", "document.to-markdown", true)]
    [InlineData("ImageInspectWorkflowExecutor", "image.inspect", false)]
    [InlineData("ImageAnalyzeWorkflowExecutor", "image.analyze", false)]
    public void Standard_assemblies_expose_runnable_contribution_types(
        string typeName,
        string expectedId,
        bool isDocumentExecutor)
    {
        var assembly = isDocumentExecutor
            ? typeof(SpreadsheetWorkflowExecutor).Assembly
            : typeof(ImageGenerationWorkflowExecutor).Assembly;
        var executorNamespace = isDocumentExecutor
            ? "CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Documents"
            : "CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Media";
        var type = assembly.GetType($"{executorNamespace}.{typeName}");

        Assert.NotNull(type);
        Assert.Contains(typeof(IWorkflowExecutor), type.GetInterfaces());
        Assert.Equal(expectedId, type.GetProperty("Descriptor")?.PropertyType == typeof(WorkflowExecutorDescriptor)
            ? expectedId
            : string.Empty);
    }

    [Fact]
    public void Built_in_catalog_contains_all_document_and_image_nodes()
    {
        var executorIds = BuiltInWorkflowExecutorDescriptors.All
            .Select(descriptor => descriptor.Id.Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("document.to-markdown", executorIds);
        Assert.Contains("image.inspect", executorIds);
        Assert.Contains("image.analyze", executorIds);
    }

    [Fact]
    public void Descriptor_policies_match_document_and_image_runtime_effects()
    {
        var document = BuiltInWorkflowExecutorDescriptors.DocumentToMarkdown;
        var inspect = BuiltInWorkflowExecutorDescriptors.ImageInspect;
        var analyze = BuiltInWorkflowExecutorDescriptors.ImageAnalyze;

        Assert.True(document.PermissionPolicy.RequiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.ReadsWorkspace));
        Assert.True(document.PermissionPolicy.RequiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.WritesWorkspace));
        Assert.True(document.PermissionPolicy.RequiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.EmitsArtifacts));
        Assert.True(document.DeterministicTestMode.IsSupported);
        Assert.True(document.Simulation.SupportsPreviewSimulation);

        Assert.True(inspect.PermissionPolicy.RequiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.ReadsWorkspace));
        Assert.False(inspect.PermissionPolicy.RequiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.UsesNetwork));
        Assert.True(inspect.DeterministicTestMode.IsSupported);
        Assert.True(inspect.Simulation.SupportsPreviewSimulation);

        var expectedAnalysisCapabilities = WorkflowExecutorCapabilityFlags.ReadsWorkspace |
                                           WorkflowExecutorCapabilityFlags.ReadsExternalData |
                                           WorkflowExecutorCapabilityFlags.UsesNetwork |
                                           WorkflowExecutorCapabilityFlags.UsesSecrets;
        Assert.Equal(expectedAnalysisCapabilities, analyze.PermissionPolicy.RequiredCapabilities);
        Assert.Equal(WorkflowExecutorApprovalRequirement.RequiredForExternalEffect, analyze.PermissionPolicy.ApprovalRequirement);
        Assert.False(analyze.DeterministicTestMode.IsSupported);
        Assert.False(analyze.Simulation.SupportsPreviewSimulation);
    }

    [Fact]
    public void Executors_delegate_operations_without_duplicating_file_provider_or_json_path_logic()
    {
        var root = FindRepositoryRoot();
        var documentSource = File.ReadAllText(Path.Combine(
            root,
            "src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Documents/DocumentToMarkdownWorkflowExecutor.cs"));
        var inspectSource = File.ReadAllText(Path.Combine(
            root,
            "src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Media/ImageInspectWorkflowExecutor.cs"));
        var analyzeSource = File.ReadAllText(Path.Combine(
            root,
            "src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Media/ImageAnalyzeWorkflowExecutor.cs"));
        var artifactOperationSource = File.ReadAllText(Path.Combine(
            root,
            "src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Tools/WorkspaceArtifactToolService.cs"));

        Assert.Contains("IWorkspaceArtifactToolService", documentSource, StringComparison.Ordinal);
        Assert.Contains("ConvertDocumentToMarkdown", documentSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IWorkspaceDocumentMarkdownConverter", documentSource, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", documentSource, StringComparison.Ordinal);

        Assert.Contains("IWorkspaceImageOperationService", inspectSource, StringComparison.Ordinal);
        Assert.Contains("IWorkspaceImageOperationService", analyzeSource, StringComparison.Ordinal);
        Assert.Contains("IAgentImageAnalysisService", analyzeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RunProviderImageChatAsync", analyzeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", inspectSource, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", analyzeSource, StringComparison.Ordinal);

        Assert.Contains("WorkflowInputJsonStringResolver.ResolveRequired", documentSource, StringComparison.Ordinal);
        Assert.Contains("WorkflowInputJsonStringResolver.ResolveRequired", inspectSource, StringComparison.Ordinal);
        Assert.Contains("WorkflowInputJsonStringResolver.ResolveRequired", analyzeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonDocument", documentSource, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonDocument", inspectSource, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonDocument", analyzeSource, StringComparison.Ordinal);
        Assert.Contains("File.WriteAllTextAsync(temporaryPath, markdown, cancellationToken)", artifactOperationSource, StringComparison.Ordinal);
        Assert.Contains("settings.PreviewCharacters,\n                cancellationToken", documentSource.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
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

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
