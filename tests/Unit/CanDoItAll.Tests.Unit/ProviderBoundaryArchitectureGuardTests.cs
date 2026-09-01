using System.Xml.Linq;

namespace CanDoItAll.Tests.Unit;

public sealed class ProviderBoundaryArchitectureGuardTests
{
    private static readonly string[] ExpectedProviderTables =
    [
        "Workspace_ProviderProfiles",
        "Workspace_ProviderSharePublications",
        "Workspace_SharedProviderServiceIdentity",
        "Workspace_SharedProviderSources",
        "Workspace_SharedProviderInvocations",
        "Workspace_SharedProviderImports"
    ];

    private static readonly string[] LegacyRuntimeDeclarations =
    [
        "interface IProviderAdapter",
        "class ProviderRegistry",
        "class ProviderExecutionService",
        "record ProviderExecutionRequest",
        "class ProviderExecutionRequest",
        "record ProviderExecutionResponse",
        "class ProviderExecutionResponse",
        "class OpenAiProviderAdapter",
        "class OllamaProviderAdapter",
        "class ComfyUiProviderAdapter",
        "class LegacyProviderRuntimeGateway"
    ];

    [Fact]
    public void Provider_management_is_an_inner_boundary_in_the_canonical_solution()
    {
        var projectPath = Absolute(
            "src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/CanDoItAll.Modules.AgentFramework.ProviderManagement.csproj");
        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        var projectReferences = XDocument
            .Load(projectPath)
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(Path.GetFileName)
            .ToArray();
        var providerSource = ReadSources(projectDirectory, "*.cs");

        Assert.Contains(
            "CanDoItAll.Modules.AgentFramework.ProviderManagement.csproj",
            Read("CanDoItAll.slnx"),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            projectReferences,
            reference => string.Equals(
                reference,
                "CanDoItAll.Modules.Workspace.csproj",
                StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("CanDoItAll.Modules.Workspace", providerSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Provider_specific_agent_framework_source_is_workspace_free()
    {
        var providerDirectory = Absolute("src/Modules/CanDoItAll.Modules.AgentFramework/Providers");
        var componentDirectory = Absolute("src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components");
        var providerFiles = EnumerateSourceFiles(providerDirectory)
            .Concat(EnumerateSourceFiles(componentDirectory)
                .Where(path => Path.GetFileName(path).Contains("Provider", StringComparison.Ordinal)))
            .ToArray();

        Assert.NotEmpty(providerFiles);
        Assert.All(providerFiles, path =>
        {
            var source = File.ReadAllText(path);
            Assert.DoesNotContain("CanDoItAll.Modules.Workspace", source, StringComparison.Ordinal);
            Assert.DoesNotContain("IAgentFrameworkWorkspaceService", source, StringComparison.Ordinal);
            Assert.DoesNotContain("IWorkspaceProviderCatalog", source, StringComparison.Ordinal);
            Assert.DoesNotContain("WorkspaceProviderOption", source, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Workspace_retains_only_the_opaque_default_provider_preference()
    {
        var workspaceDirectory = Absolute("src/Modules/CanDoItAll.Modules.Workspace");
        var sharedProviderDirectory = Path.Combine(workspaceDirectory, "SharedProviders");
        var providerDirectory = Path.Combine(workspaceDirectory, "Providers");
        var providerFiles = Directory.Exists(providerDirectory)
            ? EnumerateSourceFiles(providerDirectory).Select(path => Path.GetFileName(path)!).ToArray()
            : [];
        var providerCatalog = Read(
            "src/Modules/CanDoItAll.Modules.Workspace/Providers/WorkspaceProviderCatalog.cs");
        var workspaceRegistration = Read(
            "src/Modules/CanDoItAll.Modules.Workspace/Services/WorkspaceModuleServiceCollectionExtensions.cs");
        var preferenceTransfer = Read(
            "src/Modules/CanDoItAll.Modules.Workspace/DatabaseTransfer/WorkspaceDefaultProviderDatabaseTransferHandler.cs");
        string[] forbiddenOwnershipTokens =
        [
            "IProviderAdapter",
            "ProviderRegistry",
            "ProviderExecutionService",
            "IProviderRuntimeGateway",
            "IProviderAdministration",
            "ProviderSharePublication",
            "SharedProviderSource",
            "SharedProviderImport",
            "SharedProviderInvocation",
            "AddAgentFrameworkProviderManagement"
        ];

        Assert.False(Directory.Exists(sharedProviderDirectory));
        Assert.Equal(["WorkspaceProviderCatalog.cs"], providerFiles);
        Assert.Contains("public sealed record WorkspaceProviderOption", providerCatalog, StringComparison.Ordinal);
        Assert.Contains("public interface IWorkspaceProviderCatalog", providerCatalog, StringComparison.Ordinal);
        Assert.DoesNotContain(
            forbiddenOwnershipTokens,
            token => workspaceRegistration.Contains(token, StringComparison.Ordinal));
        Assert.Contains("DefaultProviderProfileId", preferenceTransfer, StringComparison.Ordinal);
        Assert.DoesNotContain("dbContext.Set<ProviderProfile>", preferenceTransfer, StringComparison.Ordinal);
        Assert.DoesNotContain("SharedProvider", preferenceTransfer, StringComparison.Ordinal);
        Assert.DoesNotContain("Secret", preferenceTransfer, StringComparison.Ordinal);
    }

    [Fact]
    public void Web_shared_provider_endpoints_are_workspace_free()
    {
        var apiDirectory = Absolute("src/App/CanDoItAll.Web/Api");
        var sharedProviderFiles = EnumerateSourceFiles(apiDirectory)
            .Where(path => Path.GetFileName(path).Contains("SharedProvider", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(sharedProviderFiles);
        Assert.All(sharedProviderFiles, path =>
        {
            var source = File.ReadAllText(path);
            Assert.DoesNotContain("CanDoItAll.Modules.Workspace", source, StringComparison.Ordinal);
            Assert.DoesNotContain("IAgentFrameworkWorkspaceService", source, StringComparison.Ordinal);
            Assert.DoesNotContain("IWorkspaceProviderCatalog", source, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Agent_provider_editor_uses_only_provider_management_ports()
    {
        var componentDirectory = Absolute(
            "src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components");
        var componentSource = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(
                    componentDirectory,
                    "AgentProviderProfilesPanel.*",
                    SearchOption.TopDirectoryOnly)
                .Select(File.ReadAllText));

        Assert.Contains("IProviderAdministrationService", componentSource, StringComparison.Ordinal);
        Assert.Contains("IProviderRuntimeAdministrationService", componentSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Modules.Workspace", componentSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IAgentFrameworkWorkspaceService", componentSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IWorkspaceProviderCatalog", componentSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkspaceProviderOption", componentSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Workbench_has_no_legacy_provider_execution_stack()
    {
        var workbenchSource = ReadSources(
            Absolute("src/Modules/CanDoItAll.Modules.Workbench"),
            "*.cs",
            "*.razor");
        string[] forbiddenTokens =
        [
            "ProviderExecutionService",
            "ProviderExecutionRequest",
            "ProviderExecutionResponse"
        ];

        Assert.DoesNotContain(
            forbiddenTokens,
            token => workbenchSource.Contains(token, StringComparison.Ordinal));
    }

    [Fact]
    public void Production_has_no_legacy_direct_inference_declaration()
    {
        var productionSource = string.Join(
            Environment.NewLine,
            new[]
            {
                "src/Modules",
                "src/App",
                "src/MAF"
            }.Select(path => ReadSources(Absolute(path), "*.cs", "*.razor")));

        Assert.DoesNotContain(
            LegacyRuntimeDeclarations,
            declaration => productionSource.Contains(declaration, StringComparison.Ordinal));
    }

    [Fact]
    public void Provider_ef_types_are_configured_only_from_provider_management()
    {
        var providerSource = ReadSources(
            Absolute("src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement"),
            "*.cs");
        var workspaceSource = ReadSources(
            Absolute("src/Modules/CanDoItAll.Modules.Workspace"),
            "*.cs");
        string[] configurationContracts =
        [
            "IEntityTypeConfiguration<ProviderProfile>",
            "IEntityTypeConfiguration<ProviderSharePublication>",
            "IEntityTypeConfiguration<SharedProviderServiceIdentity>",
            "IEntityTypeConfiguration<SharedProviderSource>",
            "IEntityTypeConfiguration<SharedProviderInvocationRecord>",
            "IEntityTypeConfiguration<SharedProviderImport>"
        ];

        Assert.All(
            configurationContracts,
            contract => Assert.Contains(contract, providerSource, StringComparison.Ordinal));
        Assert.DoesNotContain(
            configurationContracts,
            contract => workspaceSource.Contains(contract, StringComparison.Ordinal));
        Assert.Contains(
            "typeof(ProviderManagementModuleAssemblyMarker).Assembly",
            Read("src/App/CanDoItAll.Composition/ModuleAssemblies.cs"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Provider_physical_table_names_are_frozen()
    {
        var providerSource = ReadSources(
            Absolute("src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement"),
            "*.cs");
        string[] forbiddenReplacementTables =
        [
            "AgentFramework_ProviderProfiles",
            "ProviderManagement_ProviderProfiles",
            "AgentFramework_SharedProviderSources",
            "ProviderManagement_SharedProviderSources"
        ];

        Assert.All(
            ExpectedProviderTables,
            table => Assert.Contains(table, providerSource, StringComparison.Ordinal));
        Assert.DoesNotContain(
            forbiddenReplacementTables,
            table => providerSource.Contains(table, StringComparison.Ordinal));
    }

    [Fact]
    public void Production_host_registers_provider_management_exactly_once()
    {
        var productionSource = ReadSources(Absolute("src"), "*.cs", "*.razor");

        Assert.Equal(
            1,
            CountOccurrences(productionSource, "services.AddAgentFrameworkProviderManagement();"));
        Assert.Contains(
            "services.AddAgentFrameworkProviderManagement();",
            Read("src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void User_facing_source_has_no_workspace_provider_ownership_language()
    {
        var userFacingSource = string.Join(
            Environment.NewLine,
            ReadSources(Absolute("src/Modules"), "*.razor"),
            ReadSources(Absolute("src/App/CanDoItAll.Web/Api"), "*.cs"));
        var normalizedSource = userFacingSource.ToLowerInvariant();
        string[] forbiddenTerms =
        [
            "workspace-backed provider",
            "workspace-owned provider",
            "workspace backed provider",
            "workspace owned provider"
        ];

        Assert.DoesNotContain(
            forbiddenTerms,
            term => normalizedSource.Contains(term, StringComparison.Ordinal));
    }

    private static IEnumerable<string> EnumerateSourceFiles(string directory)
    {
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(directory);

        while (pendingDirectories.TryPop(out var currentDirectory))
        {
            foreach (var path in Directory.EnumerateFiles(currentDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                if (Path.GetExtension(path) is ".cs" or ".razor")
                {
                    yield return path;
                }
            }

            foreach (var childDirectory in Directory.EnumerateDirectories(
                         currentDirectory,
                         "*",
                         SearchOption.TopDirectoryOnly))
            {
                if (Path.GetFileName(childDirectory) is not ("bin" or "obj" or ".artifacts" or "artifacts"))
                {
                    pendingDirectories.Push(childDirectory);
                }
            }
        }
    }

    private static string ReadSources(string directory, params string[] searchPatterns)
    {
        var extensions = searchPatterns
            .Select(Path.GetExtension)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return string.Join(
            Environment.NewLine,
            EnumerateSourceFiles(directory)
                .Where(path => extensions.Contains(Path.GetExtension(path)))
                .Select(File.ReadAllText));
    }

    private static int CountOccurrences(string value, string term)
    {
        return value.Split(term, StringSplitOptions.None).Length - 1;
    }

    private static string Read(string relativePath)
    {
        return File.ReadAllText(Absolute(relativePath));
    }

    private static string Absolute(string relativePath)
    {
        return Path.Combine(FindRepositoryRoot(), relativePath);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
