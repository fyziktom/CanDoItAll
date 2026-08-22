using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class MafNativeHitlArchitectureTests
{
    private const string AdapterRelativeRoot =
        @"src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.MafAdapter";

    private static readonly string[] FrameworkNeutralRelativeRoots =
    [
        @"src\MAF\Common\CanDoItAll.AgentFramework.Models",
        @"src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.Abstractions",
        @"src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.Core",
        @"src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.Runtime",
        @"src\MAF\WorkflowExecutors\CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions",
        @"src\MAF\WorkflowExecutors\CanDoItAll.AgentFramework.WorkflowExecutors.Core",
        @"src\App\CanDoItAll.Web"
    ];

    private static readonly string[] ForbiddenMafTokens =
    [
        "Microsoft.Agents.AI",
        "CanDoItAll.AgentFramework.Maf",
        "CanDoItAll.AgentFramework.Workflows.MafAdapter"
    ];

    private static readonly string[] NativeHitlFileNames =
    [
        "MafHumanInputCheckpointCorrelator.cs",
        "MafJsonCheckpointStoreAdapter.cs",
        "MafWorkflowCompiler.cs",
        "MafWorkflowExternalRequestMapper.cs",
        "MafWorkflowExternalResponseDriver.cs",
        "MafWorkflowHitlBindingCompiler.cs",
        "MafWorkflowNativeStartDriver.cs",
        "MafWorkflowNodeExecutionBindingFactory.cs",
        "MafWorkflowRehydrationVerifier.cs",
        "MafWorkflowStreamingRunDriver.cs",
        "MafWorkflowTurnResultMapper.cs",
        "MafWorkflowTopologyFingerprintFactory.cs"
    ];

    [Fact]
    public void Framework_neutral_and_web_projects_contain_no_MAF_namespace_or_package_reference()
    {
        var root = FindRepositoryRoot();
        var violations = FrameworkNeutralRelativeRoots
            .Select(relativeRoot => TestRepositoryPath.Resolve(root, relativeRoot))
            .SelectMany(EnumerateBoundaryFiles)
            .SelectMany(path => ForbiddenMafTokens
                .Where(token => File.ReadAllText(path).Contains(token, StringComparison.OrdinalIgnoreCase))
                .Select(token => $"{Path.GetRelativePath(root, path)} contains '{token}'"))
            .OrderBy(violation => violation, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Checkpoint_adapter_implements_the_exact_MAF_json_store_contract()
    {
        var contract = typeof(ICheckpointStore<JsonElement>);
        var adapter = typeof(MafJsonCheckpointStoreAdapter);
        var contractMethods = contract.GetMethods();

        Assert.True(contract.IsAssignableFrom(adapter));
        Assert.Equal(3, contractMethods.Length);
        AssertContractMethod(
            contractMethods,
            nameof(ICheckpointStore<JsonElement>.RetrieveIndexAsync),
            typeof(ValueTask<IEnumerable<CheckpointInfo>>),
            [typeof(string), typeof(CheckpointInfo)],
            optionalParameterIndexes: [1]);
        AssertContractMethod(
            contractMethods,
            nameof(ICheckpointStore<JsonElement>.CreateCheckpointAsync),
            typeof(ValueTask<CheckpointInfo>),
            [typeof(string), typeof(JsonElement), typeof(CheckpointInfo)],
            optionalParameterIndexes: [2]);
        AssertContractMethod(
            contractMethods,
            nameof(ICheckpointStore<JsonElement>.RetrieveCheckpointAsync),
            typeof(ValueTask<JsonElement>),
            [typeof(string), typeof(CheckpointInfo)],
            optionalParameterIndexes: []);

        var interfaceMap = adapter.GetInterfaceMap(contract);
        Assert.Equal(contractMethods.Length, interfaceMap.InterfaceMethods.Length);
        Assert.All(
            interfaceMap.TargetMethods,
            method => Assert.Equal(adapter, method.DeclaringType));
        Assert.DoesNotContain(
            interfaceMap.TargetMethods.SelectMany(method => method.GetParameters()),
            parameter => parameter.ParameterType == typeof(CancellationToken));
    }

    [Fact]
    public void Backend_is_non_durable_and_advertises_resume_only_with_store_and_catalog()
    {
        var compiler = new MafWorkflowCompiler(new WorkflowDefinitionValidator());
        var components = Array.Empty<LlmCallComponent>();
        var store = new InMemoryWorkflowBackendCheckpointPayloadStore(TimeProvider.System);
        var catalog = new InMemoryWorkflowCatalogService(new WorkflowDefinitionValidator());
        var withoutDependencies = new MafInProcessWorkflowExecutionBackend(compiler, components);
        var withStoreOnly = new MafInProcessWorkflowExecutionBackend(
            compiler,
            components,
            checkpointPayloadStore: store);
        var withCatalogOnly = new MafInProcessWorkflowExecutionBackend(
            compiler,
            components,
            catalog: catalog);
        var withStoreAndCatalog = new MafInProcessWorkflowExecutionBackend(
            compiler,
            components,
            checkpointPayloadStore: store,
            catalog: catalog);
        var backends = new[]
        {
            withoutDependencies,
            withStoreOnly,
            withCatalogOnly,
            withStoreAndCatalog
        };

        Assert.True(typeof(IWorkflowExternalResponseBackend).IsAssignableFrom(typeof(MafInProcessWorkflowExecutionBackend)));
        Assert.All(backends, backend => Assert.False(backend.Descriptor.IsDurable));
        Assert.False(withoutDependencies.Descriptor.SupportsExternalResponseResume);
        Assert.False(withStoreOnly.Descriptor.SupportsExternalResponseResume);
        Assert.False(withCatalogOnly.Descriptor.SupportsExternalResponseResume);
        Assert.True(withStoreAndCatalog.Descriptor.SupportsExternalResponseResume);
    }

    [Fact]
    public void Legacy_driver_is_the_only_adapter_source_that_uses_exception_as_pause()
    {
        var adapterRoot = TestRepositoryPath.Resolve(FindRepositoryRoot(), AdapterRelativeRoot);
        var owners = Directory
            .EnumerateFiles(adapterRoot, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(path => File.ReadAllText(path).Contains(
                "WorkflowExternalRequestPendingException",
                StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path) ?? throw new InvalidOperationException(
                $"Adapter source path '{path}' has no file name."))
            .OrderBy(fileName => fileName, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["MafLegacyWorkflowExecutionDriver.cs"], owners);
    }

    [Theory]
    [InlineData("MafWorkflowCompiler.cs", 350, "MafWorkflowHitlBindingCompiler")]
    [InlineData("MafInProcessWorkflowExecutionBackend.cs", 300, "MafWorkflowExternalResponseDriver")]
    [InlineData("MafWorkflowExternalResponseDriver.cs", 180, "MafWorkflowTurnResultMapper")]
    [InlineData("MafWorkflowNativeStartDriver.cs", 150, "MafWorkflowTurnResultMapper")]
    public void Compiler_and_backend_are_non_partial_delegating_facades_with_line_ceiling(
        string fileName,
        int maximumLines,
        string delegatedCollaborator)
    {
        var path = AdapterPath(fileName);
        var source = File.ReadAllText(path);

        Assert.InRange(File.ReadLines(path).Count(), 1, maximumLines);
        Assert.False(
            Regex.IsMatch(source, @"\bpartial\s+(?:class|record|struct)\b", RegexOptions.CultureInvariant),
            $"{fileName} must not hide retained responsibilities behind a partial type.");
        Assert.Contains(delegatedCollaborator, source, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_HITL_collaborators_are_top_level_production_types()
    {
        var collaboratorTypes = new[]
        {
            typeof(InMemoryWorkflowBackendCheckpointPayloadStore),
            typeof(MafHumanInputCheckpointCorrelator),
            typeof(MafJsonCheckpointStoreAdapter),
            typeof(MafWorkflowExternalRequestMapper),
            typeof(MafWorkflowExternalResponseDriver),
            typeof(MafWorkflowHitlBindingCompiler),
            typeof(MafWorkflowNativeStartDriver),
            typeof(MafWorkflowNodeExecutionBindingFactory),
            typeof(MafWorkflowRehydrationVerifier),
            typeof(MafWorkflowStreamingRunDriver),
            typeof(MafWorkflowTurnResultMapper),
            typeof(MafWorkflowTopologyFingerprintFactory),
            typeof(WorkflowBackendCheckpointPayload),
            typeof(WorkflowBackendCheckpointSession)
        };

        Assert.All(
            collaboratorTypes,
            type => Assert.Null(type.DeclaringType));
    }

    [Fact]
    public void Native_HITL_driver_and_compiler_sources_do_not_use_service_location()
    {
        var forbiddenTokens = new[]
        {
            "IServiceProvider",
            "BuildServiceProvider"
        };
        var violations = NativeHitlFileNames
            .Select(AdapterPath)
            .SelectMany(path => forbiddenTokens
                .Where(token => File.ReadAllText(path).Contains(token, StringComparison.Ordinal))
                .Select(token => $"{Path.GetFileName(path)} contains '{token}'"))
            .OrderBy(violation => violation, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    private static IEnumerable<string> EnumerateBoundaryFiles(string root)
    {
        Assert.True(Directory.Exists(root), $"Missing architecture boundary root: {root}");
        return EnumerateBoundaryFilesCore(root);
    }

    private static IEnumerable<string> EnumerateBoundaryFilesCore(string directory)
    {
        foreach (var path in Directory.EnumerateFiles(directory))
        {
            if (Path.GetExtension(path) is ".cs" or ".razor" or ".csproj")
            {
                yield return path;
            }
        }

        foreach (var childDirectory in Directory.EnumerateDirectories(directory))
        {
            if (IsBuildArtifactDirectory(childDirectory))
            {
                continue;
            }

            foreach (var path in EnumerateBoundaryFilesCore(childDirectory))
            {
                yield return path;
            }
        }
    }

    private static bool IsBuildArtifactDirectory(string directory)
        => Path.GetFileName(directory) is { } name &&
           (name.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("artifacts", StringComparison.OrdinalIgnoreCase));

    private static void AssertContractMethod(
        IReadOnlyList<System.Reflection.MethodInfo> methods,
        string name,
        Type returnType,
        IReadOnlyList<Type> parameterTypes,
        IReadOnlyCollection<int> optionalParameterIndexes)
    {
        var method = Assert.Single(methods, candidate => candidate.Name == name);
        var parameters = method.GetParameters();

        Assert.Equal(returnType, method.ReturnType);
        Assert.Equal(parameterTypes, parameters.Select(parameter => parameter.ParameterType));
        for (var index = 0; index < parameters.Length; index++)
        {
            Assert.Equal(optionalParameterIndexes.Contains(index), parameters[index].IsOptional);
        }
    }

    private static string AdapterPath(string fileName)
        => TestRepositoryPath.Resolve(
            FindRepositoryRoot(),
            $@"{AdapterRelativeRoot}\{fileName}");

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

        throw new InvalidOperationException("Unable to locate repository root.");
    }
}
