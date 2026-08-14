using System.Xml.Linq;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Control;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Documents;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Media;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Network;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Transforms;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowExecutorCategoryIsolationTests
{
    private static readonly string[] ConcreteExecutorFileNames =
    [
        "ControlWorkflowExecutors.cs",
        "DocumentToMarkdownWorkflowExecutor.cs",
        "HttpFetchWorkflowExecutor.cs",
        "ImageAnalyzeWorkflowExecutor.cs",
        "ImageGenerationWorkflowExecutor.cs",
        "ImageInspectWorkflowExecutor.cs",
        "JsonTransformWorkflowExecutor.cs",
        "MarkdownRenderWorkflowExecutor.cs",
        "PlannedWorkflowExecutor.cs",
        "ProjectStructureWorkflowExecutor.cs",
        "SourceIngestionWorkflowExecutor.cs",
        "SpreadsheetWorkflowExecutor.cs",
        "WorkspaceFileWorkflowExecutor.cs"
    ];

    [Fact]
    public void DefaultExecutorImplementationsMovedOutOfMafWorkflowFolder()
    {
        var root = FindRepositoryRoot();
        var mafWorkflowFolder = Path.Combine(root, "src", "MAF", "Common", "CanDoItAll.AgentFramework.Maf", "Runtime", "Workflows");
        foreach (var fileName in ConcreteExecutorFileNames)
        {
            Assert.False(
                File.Exists(Path.Combine(mafWorkflowFolder, fileName)),
                $"{fileName} must be owned by a standard executor category project, not MAF.");
        }

        Assert.True(File.Exists(Path.Combine(root, "src", "MAF", "WorkflowExecutors", "Standard", "CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Control", "ControlWorkflowExecutors.cs")));
        Assert.True(File.Exists(Path.Combine(root, "src", "MAF", "WorkflowExecutors", "Standard", "CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Transforms", "JsonTransformWorkflowExecutor.cs")));
        Assert.True(File.Exists(Path.Combine(root, "src", "MAF", "WorkflowExecutors", "Standard", "CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace", "WorkspaceFileWorkflowExecutor.cs")));
        Assert.True(File.Exists(Path.Combine(root, "src", "MAF", "WorkflowExecutors", "Standard", "CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Network", "HttpFetchWorkflowExecutor.cs")));
        Assert.True(File.Exists(Path.Combine(root, "src", "MAF", "WorkflowExecutors", "Standard", "CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Documents", "SpreadsheetWorkflowExecutor.cs")));
        Assert.True(File.Exists(Path.Combine(root, "src", "MAF", "WorkflowExecutors", "Standard", "CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Media", "ImageGenerationWorkflowExecutor.cs")));
        Assert.True(File.Exists(Path.Combine(root, "src", "MAF", "WorkflowExecutors", "Standard", "CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure", "ProjectStructureWorkflowExecutor.cs")));
    }

    [Fact]
    public void CategoryRegistrationsPartitionBuiltInContributions()
    {
        var services = new ServiceCollection();
        services.AddStandardControlWorkflowExecutors(ServiceLifetime.Singleton);
        services.AddStandardTransformWorkflowExecutors(ServiceLifetime.Singleton);
        services.AddStandardWorkspaceWorkflowExecutors(ServiceLifetime.Singleton);
        services.AddStandardNetworkWorkflowExecutors(ServiceLifetime.Singleton);
        services.AddStandardDocumentWorkflowExecutors(ServiceLifetime.Singleton);
        services.AddStandardMediaWorkflowExecutors(ServiceLifetime.Singleton);
        services.AddStandardProjectStructureWorkflowExecutors(ServiceLifetime.Singleton);

        Type[] expectedImplementationTypes =
        [
            typeof(DelayWorkflowExecutor),
            typeof(HumanApprovalWorkflowExecutor),
            typeof(JsonTransformWorkflowExecutor),
            typeof(MarkdownRenderWorkflowExecutor),
            typeof(WorkspaceFileWorkflowExecutor),
            typeof(SourceIngestionWorkflowExecutor),
            typeof(HttpFetchWorkflowExecutor),
            typeof(DocumentToMarkdownWorkflowExecutor),
            typeof(SpreadsheetWorkflowExecutor),
            typeof(ImageAnalyzeWorkflowExecutor),
            typeof(ImageGenerationWorkflowExecutor),
            typeof(ImageInspectWorkflowExecutor),
            typeof(ProjectStructureWorkflowExecutor)
        ];
        var contributionDescriptors = services
            .Where(descriptor => descriptor.ServiceType == typeof(IWorkflowExecutorContribution))
            .ToArray();
        var implementationTypes = services
            .Where(descriptor => descriptor.ServiceType == typeof(IWorkflowExecutor))
            .Select(descriptor => descriptor.ImplementationType)
            .OfType<Type>()
            .Select(type => type.GetGenericArguments().Single())
            .ToArray();
        var plannedDescriptors = contributionDescriptors
            .Select(descriptor => descriptor.ImplementationInstance)
            .OfType<IWorkflowExecutorContribution>()
            .Select(contribution => contribution.Descriptor)
            .Where(descriptor => !descriptor.CanExecute)
            .ToArray();

        Assert.Equal(expectedImplementationTypes.OrderBy(type => type.FullName), implementationTypes.OrderBy(type => type.FullName));
        Assert.Equal(BuiltInWorkflowExecutorDescriptors.Planned, plannedDescriptors);
        Assert.Equal(expectedImplementationTypes.Length + plannedDescriptors.Length, contributionDescriptors.Length);
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IWorkflowExecutorDescriptorSource));
    }

    [Fact]
    public void BuiltInRegistrationDelegatesToCategoryRegistrations()
    {
        var root = FindRepositoryRoot();
        var mafRegistration = File.ReadAllText(Path.Combine(root, "src", "MAF", "Workflows", "CanDoItAll.AgentFramework.Workflows.MafAdapter", "MafWorkflowAdapterServiceCollectionExtensions.cs"));
        var moduleRegistration = File.ReadAllText(Path.Combine(root, "src", "Modules", "CanDoItAll.Modules.AgentFramework", "Services", "AgentFrameworkModuleServiceCollectionExtensions.cs"));

        Assert.Contains("AddStandardWorkflowExecutors(executorLifetime)", mafRegistration, StringComparison.Ordinal);
        Assert.Contains("AddMafWorkflowAdapterServices(ServiceLifetime.Scoped)", moduleRegistration, StringComparison.Ordinal);
        foreach (var executorTypeName in ConcreteExecutorFileNames.Select(Path.GetFileNameWithoutExtension))
        {
            Assert.DoesNotContain($"ServiceDescriptor.Singleton<IWorkflowExecutor, {executorTypeName}>", mafRegistration, StringComparison.Ordinal);
            Assert.DoesNotContain($"ServiceDescriptor.Scoped<IWorkflowExecutor, {executorTypeName}>", moduleRegistration, StringComparison.Ordinal);
        }

        var services = new ServiceCollection();
        services.AddStandardWorkflowExecutors();

        var executorDescriptors = services.Where(descriptor => descriptor.ServiceType == typeof(IWorkflowExecutor)).ToArray();
        var contributionDescriptors = services.Where(descriptor => descriptor.ServiceType == typeof(IWorkflowExecutorContribution)).ToArray();

        Assert.Equal(BuiltInWorkflowExecutorDescriptors.Implemented.Count, executorDescriptors.Length);
        Assert.Equal(BuiltInWorkflowExecutorDescriptors.All.Count, contributionDescriptors.Length);
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IWorkflowExecutorDescriptorSource));
    }

    [Fact]
    public void StandardCategoryProjectsUseExpectedDependencyBoundaries()
    {
        AssertProjectReferences(
            "src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Control/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Control.csproj",
            ["CanDoItAll.AgentFramework.Models", "CanDoItAll.AgentFramework.WorkflowExecutors.Core", "CanDoItAll.AgentFramework.Workflows.Runtime"],
            ["Microsoft.Extensions.DependencyInjection.Abstractions"]);
        AssertProjectReferences(
            "src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Transforms/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Transforms.csproj",
            ["CanDoItAll.AgentFramework.Core", "CanDoItAll.AgentFramework.Models", "CanDoItAll.AgentFramework.WorkflowExecutors.Core", "CanDoItAll.AgentFramework.Workflows.Core"],
            ["Microsoft.Extensions.DependencyInjection.Abstractions"]);
        AssertProjectReferences(
            "src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace.csproj",
            ["CanDoItAll.AgentFramework.Core", "CanDoItAll.AgentFramework.Models", "CanDoItAll.AgentFramework.WorkflowExecutors.Core", "CanDoItAll.Infrastructure.Abstractions", "CanDoItAll.SharedKernel"],
            ["ExcelDataReader", "Microsoft.Extensions.DependencyInjection.Abstractions"]);
        AssertProjectReferences(
            "src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Network/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Network.csproj",
            ["CanDoItAll.AgentFramework.Core", "CanDoItAll.AgentFramework.Models", "CanDoItAll.AgentFramework.WorkflowExecutors.Core", "CanDoItAll.AgentFramework.Workflows.Core", "CanDoItAll.Security.Abstractions", "CanDoItAll.SharedKernel"],
            ["Microsoft.Extensions.DependencyInjection.Abstractions"]);
        AssertProjectReferences(
            "src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Documents/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Documents.csproj",
            ["CanDoItAll.AgentFramework.Core", "CanDoItAll.AgentFramework.Models", "CanDoItAll.AgentFramework.WorkflowExecutors.Core", "CanDoItAll.Tools.Documents"],
            ["Microsoft.Extensions.DependencyInjection.Abstractions"]);
        AssertProjectReferences(
            "src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Media/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Media.csproj",
            ["CanDoItAll.AgentFramework.Core", "CanDoItAll.AgentFramework.Models", "CanDoItAll.AgentFramework.WorkflowExecutors.Core"],
            ["Microsoft.Extensions.DependencyInjection.Abstractions"]);
        AssertProjectReferences(
            "src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure.csproj",
            ["CanDoItAll.AgentFramework.Core", "CanDoItAll.AgentFramework.Models", "CanDoItAll.AgentFramework.WorkflowExecutors.Core", "CanDoItAll.AgentFramework.Workflows.Core", "CanDoItAll.SharedKernel"],
            ["Microsoft.Extensions.DependencyInjection.Abstractions"]);
    }

    [Fact]
    public void LargeExecutorCategoriesAreSplitByResponsibility()
    {
        var root = FindRepositoryRoot();
        var workspaceDirectory = Path.Combine(root, "src", "MAF", "WorkflowExecutors", "Standard", "CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace");
        AssertComposedExecutor(
            workspaceDirectory,
            "SourceIngestionWorkflowExecutor.cs",
            [
                ("WorkflowSourceCandidateCollector.cs", "WorkflowSourceCandidateCollector"),
                ("WorkflowSourceFileResolver.cs", "WorkflowSourceFileResolver"),
                ("WorkflowSourceDocumentReader.cs", "WorkflowSourceDocumentReader")
            ]);
        Assert.False(File.Exists(Path.Combine(workspaceDirectory, "SourceIngestionWorkflowReader.cs")));
        Assert.False(File.Exists(Path.Combine(workspaceDirectory, "SourceIngestionWorkflowPaths.cs")));
        Assert.False(File.Exists(Path.Combine(workspaceDirectory, "SourceIngestionWorkflowCandidates.cs")));
        AssertSplit(
            Path.Combine(root, "src", "MAF", "WorkflowExecutors", "Standard", "CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure"),
            "ProjectStructureWorkflowExecutor.cs",
            [
                "ProjectStructureWorkflowTaskNodes.cs",
                "ProjectStructureWorkflowInputResolution.cs",
                "ProjectStructureWorkflowSupport.cs"
            ]);
    }

    private static void AssertComposedExecutor(
        string categoryDirectory,
        string mainFileName,
        IReadOnlyList<(string FileName, string TypeName)> collaborators)
    {
        var mainFile = Path.Combine(categoryDirectory, mainFileName);
        Assert.True(File.Exists(mainFile), $"{mainFileName} is missing.");
        Assert.DoesNotContain("partial class", File.ReadAllText(mainFile), StringComparison.Ordinal);
        Assert.InRange(File.ReadAllLines(mainFile).Length, 1, 240);

        foreach (var (fileName, typeName) in collaborators)
        {
            var collaboratorFile = Path.Combine(categoryDirectory, fileName);
            Assert.True(File.Exists(collaboratorFile), $"{fileName} is missing.");
            var source = File.ReadAllText(collaboratorFile);
            Assert.Contains($"sealed class {typeName}", source, StringComparison.Ordinal);
            Assert.DoesNotContain($"partial class {typeName}", source, StringComparison.Ordinal);
            Assert.InRange(File.ReadAllLines(collaboratorFile).Length, 1, 340);
        }
    }

    private static void AssertProjectReferences(
        string relativeProjectPath,
        IReadOnlyList<string> expectedProjectReferences,
        IReadOnlyList<string> expectedPackageReferences)
    {
        var root = FindRepositoryRoot();
        var document = XDocument.Load(Path.Combine(root, relativeProjectPath));
        var projectReferences = document.Descendants("ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension(element.Attribute("Include")?.Value.Replace('\\', Path.DirectorySeparatorChar)))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var packageReferences = document.Descendants("PackageReference")
            .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
            .Where(value => value.Length > 0)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedProjectReferences.Order(StringComparer.Ordinal), projectReferences);
        Assert.Equal(expectedPackageReferences.Order(StringComparer.Ordinal), packageReferences);
        Assert.DoesNotContain(projectReferences, reference => reference == "CanDoItAll.AgentFramework.Maf");
        Assert.DoesNotContain(projectReferences, reference => reference == "CanDoItAll.Web");
        Assert.DoesNotContain(projectReferences, reference => reference == "CanDoItAll.Modules.AgentFramework");
    }

    private static void AssertSplit(string categoryDirectory, string mainFileName, IReadOnlyList<string> helperFileNames)
    {
        var mainFile = Path.Combine(categoryDirectory, mainFileName);
        Assert.True(File.Exists(mainFile), $"{mainFileName} is missing.");
        Assert.Contains("partial class", File.ReadAllText(mainFile), StringComparison.Ordinal);
        Assert.InRange(File.ReadAllLines(mainFile).Length, 1, 140);

        foreach (var helperFileName in helperFileNames)
        {
            var helperFile = Path.Combine(categoryDirectory, helperFileName);
            Assert.True(File.Exists(helperFile), $"{helperFileName} is missing.");
            Assert.Contains("partial class", File.ReadAllText(helperFile), StringComparison.Ordinal);
            Assert.InRange(File.ReadAllLines(helperFile).Length, 1, 340);
        }
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

        throw new InvalidOperationException("Could not find repository root.");
    }
}
