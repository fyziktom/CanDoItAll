using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Integration.Runtime;

// Stage C of the process scratch hardening on a real dotnet SDK: an agent that may only read a
// product folder can build, test and run it, and the product folder stays byte-for-byte unchanged.
// The package versions match this test project, so restore is served from the local package cache.
[Trait("Category", "HostPlatform")]
public sealed class WorkspaceDotnetReadOnlyTargetTests : IDisposable
{
    private const string AppProject = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net10.0</TargetFramework>
            <Nullable>enable</Nullable>
          </PropertyGroup>
        </Project>
        """;

    private const string AppProgram = """
        namespace App;

        public static class Greeter
        {
            public const string Message = "hello from the read-only product";
        }

        public static class Program
        {
            public static void Main() => System.Console.WriteLine(Greeter.Message);
        }
        """;

    private const string TestProject = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <IsPackable>false</IsPackable>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
            <PackageReference Include="xunit" Version="2.9.3" />
            <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
          </ItemGroup>
          <ItemGroup>
            <ProjectReference Include="../App/App.csproj" />
          </ItemGroup>
        </Project>
        """;

    private const string TestSource = """
        namespace App.Tests;

        public sealed class GreeterTests
        {
            [Xunit.Fact]
            public void Greets() => Xunit.Assert.StartsWith("hello", App.Greeter.Message);
        }
        """;

    private readonly string rootPath = Path.Combine(Path.GetTempPath(), $"cdia-ro-{Guid.NewGuid():N}"[..16]);
    private readonly ExternalTargetPathRegistry externalTargets = new();
    private readonly PhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory = new();

    [Fact]
    public async Task A_read_only_product_builds_tests_and_runs_without_changing_a_single_file()
    {
        var workspaceRoot = CreateDirectory("ws");
        var productRoot = CreateProduct("product");
        var productAlias = BindFolder(productRoot);
        var before = HashTree(productRoot);

        using (WorkspaceExecutionAuditContext.BeginScope(CreateRun(readOnlyAliases: [productAlias])))
        {
            var plugin = CreatePlugin(workspaceRoot);

            var build = await plugin.DotnetWorkspaceBuild($"{productAlias}/App.Tests/App.Tests.csproj", timeoutSeconds: 300);
            Assert.True(build.Succeeded, Describe(build));
            Assert.Contains("is kept in '.build/", build.Message, StringComparison.Ordinal);

            var test = await plugin.DotnetWorkspaceTest($"{productAlias}/App.Tests/App.Tests.csproj", noBuild: true, timeoutSeconds: 300);
            Assert.True(test.Succeeded, Describe(test));

            var run = await plugin.DotnetWorkspaceRun($"{productAlias}/App/App.csproj", noBuild: true, waitForHttp: false, timeoutSeconds: 120);
            Assert.True(run.Succeeded, Describe(run));
            Assert.Contains("hello from the read-only product", run.StdoutPreview, StringComparison.Ordinal);
        }

        Assert.Equal(before, HashTree(productRoot));
        Assert.False(Directory.Exists(Path.Combine(productRoot, "App", "bin")));
        Assert.False(Directory.Exists(Path.Combine(productRoot, "App", "obj")));
        Assert.False(Directory.Exists(Path.Combine(productRoot, "App.Tests", "obj")));
        var buildFolder = Path.Combine(workspaceRoot, ".build", WorkspaceReadOnlyBuildOutput.ComputeKey(productAlias));
        Assert.True(Directory.Exists(Path.Combine(buildFolder, "bin")), "The redirected build output is missing.");

        // T-C6: a later execution reuses the persistent output instead of rebuilding into the product.
        using (WorkspaceExecutionAuditContext.BeginScope(CreateRun(readOnlyAliases: [productAlias])))
        {
            var laterTest = await CreatePlugin(workspaceRoot)
                .DotnetWorkspaceTest($"{productAlias}/App.Tests/App.Tests.csproj", noBuild: true, timeoutSeconds: 300);
            Assert.True(laterTest.Succeeded, Describe(laterTest));
        }

        Assert.Equal(before, HashTree(productRoot));
    }

    [Fact]
    public async Task A_writable_product_keeps_the_ordinary_bin_and_obj_layout()
    {
        var workspaceRoot = CreateDirectory("ws-writable");
        var productRoot = CreateProduct("product-writable");
        var productAlias = BindFolder(productRoot);

        using (WorkspaceExecutionAuditContext.BeginScope(CreateRun(writableAliases: [productAlias])))
        {
            var build = await CreatePlugin(workspaceRoot)
                .DotnetWorkspaceBuild($"{productAlias}/App/App.csproj", timeoutSeconds: 300);
            Assert.True(build.Succeeded, Describe(build));
            Assert.DoesNotContain(".build/", build.Message, StringComparison.Ordinal);
        }

        Assert.True(Directory.Exists(Path.Combine(productRoot, "App", "bin")));
        Assert.True(Directory.Exists(Path.Combine(productRoot, "App", "obj")));
        Assert.False(Directory.Exists(Path.Combine(workspaceRoot, ".build")));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
        catch
        {
        }
    }

    private string CreateProduct(string name)
    {
        var productRoot = CreateDirectory(name);
        Directory.CreateDirectory(Path.Combine(productRoot, "App"));
        Directory.CreateDirectory(Path.Combine(productRoot, "App.Tests"));
        File.WriteAllText(Path.Combine(productRoot, "App", "App.csproj"), AppProject);
        File.WriteAllText(Path.Combine(productRoot, "App", "Program.cs"), AppProgram);
        File.WriteAllText(Path.Combine(productRoot, "App.Tests", "App.Tests.csproj"), TestProject);
        File.WriteAllText(Path.Combine(productRoot, "App.Tests", "GreeterTests.cs"), TestSource);
        return productRoot;
    }

    private string BindFolder(string folder)
        => externalTargets.TryCreateAlias(folder, out var alias)
            ? alias
            : throw new InvalidOperationException($"Could not bind '{folder}' as an external target.");

    private WorkspaceRuntimePlugin CreatePlugin(string workspaceRoot)
    {
        var commands = new WorkspaceCommandExecutionService(
            workspaceRoot,
            new LocalWorkspaceProcessHost(),
            physicalPathPolicyFactory,
            WorkspaceScopeDescriptor.Sandbox,
            externalTargetRegistry: externalTargets);
        return new WorkspaceRuntimePlugin(
            commands,
            null!,
            workspaceRoot,
            physicalPathPolicyFactory,
            WorkspaceScopeDescriptor.Sandbox,
            AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.SoftwareDevelopment),
            CreateProvider(),
            "test-model",
            null!);
    }

    // Relative path and content of every file, so an added, removed or changed file changes the hash.
    private static string HashTree(string root)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(root, file).Replace('\\', '/')));
            hash.AppendData(File.ReadAllBytes(file));
        }

        foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(root, directory).Replace('\\', '/') + "/"));
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string Describe(WorkspaceCommandExecutionResult result)
        => $"{result.Message}{Environment.NewLine}{result.StdoutPreview}{Environment.NewLine}{result.StderrPreview}";

    private static ExecutionRunRecord CreateRun(
        IReadOnlyList<string>? readOnlyAliases = null,
        IReadOnlyList<string>? writableAliases = null)
    {
        var now = DateTimeOffset.UtcNow;
        var metadata = new Dictionary<string, IReadOnlyList<string>>();
        if (readOnlyAliases is not null)
        {
            metadata[ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey] = readOnlyAliases;
        }

        if (writableAliases is not null)
        {
            metadata[ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey] = writableAliases;
        }

        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            ChatSessionId: null,
            Title: "Read-only dotnet target",
            SourceKind: "test",
            SourceId: "read-only-dotnet-target",
            CorrelationId: Guid.NewGuid().ToString("D"),
            CausationId: string.Empty,
            RequestedBy: "integration-test",
            RequestedByKind: "system",
            MetadataJson: JsonSerializer.Serialize(metadata),
            InputSummary: string.Empty,
            ResultSummary: string.Empty,
            ProviderName: "test",
            Model: "test",
            State: ExecutionState.Running,
            Outcome: null,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            StartedAtUtc: now,
            CompletedAtUtc: null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: []);
    }

    private static ProviderProfile CreateProvider()
        => new(
            Guid.NewGuid(),
            "Test Provider",
            ProviderKind.OpenAi,
            "https://provider.example.test",
            "PROVIDER_API_KEY",
            "test-model",
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: false,
            SupportsTools: false,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: ["test-model"],
            Purpose: ProviderProfilePurpose.Chat);

    private string CreateDirectory(string name)
    {
        var path = Path.Combine(rootPath, name);
        Directory.CreateDirectory(path);
        return path;
    }
}
