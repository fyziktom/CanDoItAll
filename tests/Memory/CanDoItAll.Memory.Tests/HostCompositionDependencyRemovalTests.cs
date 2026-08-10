using CanDoItAll.Composition;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Drivers.CognitiveMemory;
using CanDoItAll.Memory.Http;
using CanDoItAll.Memory.Mcp;
using CanDoItAll.Memory.Mock;
using CanDoItAll.Memory.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Memory.Tests;

public sealed class HostCompositionDependencyRemovalTests
{
    private static readonly string[] ForbiddenBaseHostPatterns =
    [
        "CanDoItAll.Modules.CognitiveMemory",
        "AddCognitiveMemoryModule",
        "CognitiveMemoryModuleAssemblyMarker",
        "AddConfiguredQdrantRagDriver",
        "CanDoItAll.AgentFramework.Rag.Qdrant",
        "CanDoItAll.AgentFramework.SemanticCompletion.Driver",
        "Rag:Qdrant",
        "Qdrant"
    ];

    [Fact]
    public void CP001_Base_host_source_has_no_direct_native_memory_or_qdrant_references()
    {
        var violations = EnumerateBaseHostGuardFiles()
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new
                {
                    Path = path,
                    LineNumber = index + 1,
                    Line = line
                }))
            .SelectMany(candidate => ForbiddenBaseHostPatterns
                .Where(pattern => candidate.Line.Contains(pattern, StringComparison.Ordinal))
                .Select(pattern => $"{Path.GetRelativePath(RepoRoot, candidate.Path)}:{candidate.LineNumber} contains {pattern}"))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void CP002_Base_module_assembly_discovery_excludes_native_cognitive_memory()
    {
        var assemblyNames = ModuleAssemblies.All
            .Select(assembly => assembly.GetName().Name)
            .ToArray();

        Assert.DoesNotContain("CanDoItAll.Modules.CognitiveMemory", assemblyNames);
        Assert.Contains("CanDoItAll.Modules.Memory", assemblyNames);
    }

    [Fact]
    public void CP003_Zero_provider_runtime_registration_has_no_implicit_provider_drivers()
    {
        var services = new ServiceCollection();
        services.AddCanDoItAllRuntimeModules(
            CreateConfiguration(new Dictionary<string, string?>()),
            MemoryTestHostEnvironment.Instance);

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IMemoryProviderDriver));
        Assert.DoesNotContain(services, descriptor => DescriptorContainsType(descriptor, typeof(DeterministicMockMemoryProviderDriver)));
        Assert.DoesNotContain(services, descriptor => DescriptorContainsType(descriptor, typeof(HttpMemoryProviderDriver)));
        Assert.DoesNotContain(services, descriptor => DescriptorContainsType(descriptor, typeof(McpMemoryProviderDriver)));
        Assert.DoesNotContain(services, descriptor => DescriptorContainsType(descriptor, typeof(NativeRemoteMemoryProviderDriver)));
        Assert.DoesNotContain(services, descriptor => DescriptorMentions(descriptor, "Qdrant"));
        Assert.DoesNotContain(services, descriptor => DescriptorMentions(descriptor, "CanDoItAll.Modules.CognitiveMemory"));
    }

    [Fact]
    public void CP004_Explicit_provider_driver_configuration_registers_only_requested_drivers()
    {
        var services = new ServiceCollection();
        services.AddCanDoItAllRuntimeModules(CreateConfiguration(new Dictionary<string, string?>
        {
            ["Memory:Providers:DeterministicMock:Enabled"] = "true",
            ["Memory:Providers:Http:Enabled"] = "true",
            ["Memory:Providers:Mcp:Enabled"] = "true",
            ["Memory:Providers:NativeRemote:Enabled"] = "true",
            ["Memory:Providers:Http:ClientName"] = "test-memory-http",
            ["Memory:Providers:NativeRemote:ClientName"] = "test-memory-native-remote"
        }), MemoryTestHostEnvironment.Instance);

        using var provider = services.BuildServiceProvider(validateScopes: false);
        var driverKinds = provider.GetServices<IMemoryProviderDriver>()
            .Select(driver => driver.DriverKind)
            .Order()
            .ToArray();

        Assert.Equal(
            [MemoryProviderDriverKind.Http, MemoryProviderDriverKind.Mcp, MemoryProviderDriverKind.NativeRemote, MemoryProviderDriverKind.Mock],
            driverKinds);
    }

    [Fact]
    public void CP005_Mock_driver_and_memory_composition_have_explicit_owners()
    {
        Assert.Equal(
            "CanDoItAll.Memory.Mock",
            typeof(DeterministicMockMemoryProviderDriver).Assembly.GetName().Name);

        var persistenceProject = File.ReadAllText(Path.Combine(
            RepoRoot,
            "src",
            "Memory",
            "CanDoItAll.Memory.Persistence",
            "CanDoItAll.Memory.Persistence.csproj"));
        Assert.DoesNotContain("CanDoItAll.Memory.Mock", persistenceProject, StringComparison.Ordinal);

        var runtimeHost = File.ReadAllText(Path.Combine(
            RepoRoot,
            "src",
            "App",
            "CanDoItAll.Composition",
            "RuntimeHostServiceCollectionExtensions.cs"));
        Assert.Contains("AddCanDoItAllMemory(configuration)", runtimeHost, StringComparison.Ordinal);
        Assert.DoesNotContain("Memory:Providers:", runtimeHost, StringComparison.Ordinal);

        Assert.True(File.Exists(Path.Combine(
            RepoRoot,
            "src",
            "App",
            "CanDoItAll.Composition",
            "Memory",
            "MemoryRuntimeServiceCollectionExtensions.cs")));
    }

    [Fact]
    public void CP006_Cognitive_memory_remote_driver_is_an_outer_adapter()
    {
        Assert.Equal(
            "CanDoItAll.Memory.Drivers.CognitiveMemory",
            typeof(NativeRemoteMemoryProviderDriver).Assembly.GetName().Name);

        var genericHttpProject = File.ReadAllText(Path.Combine(
            RepoRoot,
            "src",
            "Memory",
            "CanDoItAll.Memory.Http",
            "CanDoItAll.Memory.Http.csproj"));
        Assert.DoesNotContain(
            "CanDoItAll.Memory.Drivers.CognitiveMemory",
            genericHttpProject,
            StringComparison.Ordinal);

        var genericMemoryModuleProject = File.ReadAllText(Path.Combine(
            RepoRoot,
            "src",
            "Modules",
            "CanDoItAll.Modules.Memory",
            "CanDoItAll.Modules.Memory.csproj"));
        Assert.DoesNotContain(
            "CanDoItAll.Memory.Drivers.CognitiveMemory",
            genericMemoryModuleProject,
            StringComparison.Ordinal);
    }

    private static IConfiguration CreateConfiguration(IReadOnlyDictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static bool DescriptorContainsType(ServiceDescriptor descriptor, Type type)
    {
        return descriptor.ImplementationType == type ||
               descriptor.ImplementationInstance?.GetType() == type;
    }

    private static bool DescriptorMentions(ServiceDescriptor descriptor, string value)
    {
        return DescriptorTypeNames(descriptor)
            .Any(name => name.Contains(value, StringComparison.Ordinal));
    }

    private static IEnumerable<string> DescriptorTypeNames(ServiceDescriptor descriptor)
    {
        yield return descriptor.ServiceType.FullName ?? descriptor.ServiceType.Name;

        if (descriptor.ImplementationType is not null)
        {
            yield return descriptor.ImplementationType.FullName ?? descriptor.ImplementationType.Name;
        }

        if (descriptor.ImplementationInstance is not null)
        {
            yield return descriptor.ImplementationInstance.GetType().FullName ?? descriptor.ImplementationInstance.GetType().Name;
        }
    }

    private static IEnumerable<string> EnumerateBaseHostGuardFiles()
    {
        foreach (var root in new[]
        {
            Path.Combine(RepoRoot, "src", "App"),
            Path.Combine(RepoRoot, "src", "MAF"),
            Path.Combine(RepoRoot, "src", "Memory")
        })
        {
            foreach (var file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                         .Where(IsGuardedSourceFile)
                         .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                         .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
            {
                yield return file;
            }
        }

        yield return Path.Combine(RepoRoot, "CanDoItAll.slnx");
    }

    private static bool IsGuardedSourceFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension is ".cs" or ".csproj" or ".json" or ".slnx";
    }

    private static string RepoRoot => FindRepoRoot();

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

        throw new DirectoryNotFoundException("Could not locate repository root containing CanDoItAll.slnx.");
    }
}
