using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.AgentFramework;

// Stage C of the process scratch hardening: .NET output of a read-only external target goes to the
// workspace's persistent .build/<key> folder, and nothing else changes.
public sealed class WorkspaceReadOnlyBuildOutputTests : IDisposable
{
    private const string ProductRootId = "0123456789abcdef01234567";
    private const string OtherRootId = "89abcdef0123456789abcdef";

    private readonly string rootPath = Path.Combine(
        Path.GetTempPath(),
        "CanDoItAll.WorkspaceReadOnlyBuildOutputTests",
        Guid.NewGuid().ToString("N"));
    private readonly ExternalTargetPathRegistry externalTargets = new();

    // T-C1: the redirect decision.

    [Fact]
    public void A_read_only_external_target_is_redirected_to_a_deterministic_build_folder()
    {
        var root = ExternalTargetAliasCodec.BuildAlias(ProductRootId, ["product"]);
        var access = new EffectiveExternalTargetAccessScope([], [root]);
        var project = $"{root}/src/App/App.csproj";

        var first = WorkspaceReadOnlyBuildOutput.ResolveRelativePath(access, project, workingDirectory: null);
        var second = WorkspaceReadOnlyBuildOutput.ResolveRelativePath(access, project, workingDirectory: null);

        Assert.NotNull(first);
        Assert.Equal(first, second);
        Assert.Matches("^\\.build/[0-9a-f]{12}$", first);
        Assert.Equal($".build/{WorkspaceReadOnlyBuildOutput.ComputeKey(root)}", first);
    }

    [Fact]
    public void A_solution_and_a_project_under_the_same_read_only_root_share_one_build_folder()
    {
        var root = ExternalTargetAliasCodec.BuildAlias(ProductRootId, ["product"]);
        var access = new EffectiveExternalTargetAccessScope([], [root]);

        var solution = WorkspaceReadOnlyBuildOutput.ResolveRelativePath(access, $"{root}/Product.sln", null);
        var project = WorkspaceReadOnlyBuildOutput.ResolveRelativePath(access, $"{root}/src/App/App.csproj", null);
        var folder = WorkspaceReadOnlyBuildOutput.ResolveRelativePath(access, null, $"{root}/src/App");

        Assert.NotNull(solution);
        Assert.Equal(solution, project);
        Assert.Equal(solution, folder);
    }

    [Fact]
    public void Different_read_only_roots_get_different_build_folders()
    {
        var product = ExternalTargetAliasCodec.BuildAlias(ProductRootId, ["product"]);
        var other = ExternalTargetAliasCodec.BuildAlias(OtherRootId, ["product"]);
        var access = new EffectiveExternalTargetAccessScope([], [product, other]);

        var first = WorkspaceReadOnlyBuildOutput.ResolveRelativePath(access, $"{product}/App.csproj", null);
        var second = WorkspaceReadOnlyBuildOutput.ResolveRelativePath(access, $"{other}/App.csproj", null);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void The_most_specific_read_only_root_names_the_build_folder()
    {
        var outer = ExternalTargetAliasCodec.BuildAlias(ProductRootId, ["repo"]);
        var inner = ExternalTargetAliasCodec.BuildAlias(ProductRootId, ["repo", "product"]);
        var access = new EffectiveExternalTargetAccessScope([], [outer, inner]);

        var result = WorkspaceReadOnlyBuildOutput.ResolveRelativePath(access, $"{inner}/App.csproj", null);

        Assert.Equal($".build/{WorkspaceReadOnlyBuildOutput.ComputeKey(inner)}", result);
    }

    [Fact]
    public void Writable_workspace_and_unknown_targets_and_a_disabled_switch_are_not_redirected()
    {
        var readOnly = ExternalTargetAliasCodec.BuildAlias(ProductRootId, ["product"]);
        var writableInside = ExternalTargetAliasCodec.BuildAlias(ProductRootId, ["product", "sandbox"]);
        var writable = ExternalTargetAliasCodec.BuildAlias(OtherRootId, ["work"]);
        var access = new EffectiveExternalTargetAccessScope([writable, writableInside], [readOnly]);

        Assert.Null(WorkspaceReadOnlyBuildOutput.ResolveRelativePath(access, $"{writable}/App.csproj", null));
        Assert.Null(WorkspaceReadOnlyBuildOutput.ResolveRelativePath(access, $"{writableInside}/App.csproj", null));
        Assert.Null(WorkspaceReadOnlyBuildOutput.ResolveRelativePath(access, "src/App/App.csproj", null));
        Assert.Null(WorkspaceReadOnlyBuildOutput.ResolveRelativePath(access, null, null));
        Assert.Null(WorkspaceReadOnlyBuildOutput.ResolveRelativePath(
            EffectiveExternalTargetAccessScope.Empty, $"{readOnly}/App.csproj", null));
        Assert.Null(WorkspaceReadOnlyBuildOutput.ResolveRelativePath(access, $"{readOnly}/App.csproj", null, enabled: false));
        Assert.NotNull(WorkspaceReadOnlyBuildOutput.ResolveRelativePath(access, $"{readOnly}/App.csproj", null));
    }

    [Fact]
    public void The_target_decides_over_the_working_directory()
    {
        var readOnly = ExternalTargetAliasCodec.BuildAlias(ProductRootId, ["product"]);
        var access = new EffectiveExternalTargetAccessScope([], [readOnly]);

        Assert.Null(WorkspaceReadOnlyBuildOutput.ResolveRelativePath(access, "src/App/App.csproj", readOnly));
        Assert.NotNull(WorkspaceReadOnlyBuildOutput.ResolveRelativePath(access, $"{readOnly}/App.csproj", "src"));
    }

    [Fact]
    public void The_redirect_switch_is_on_by_default_and_follows_the_operator_option()
    {
        Assert.True(WorkspaceProcessEnvironmentSettings.Default.RedirectReadOnlyDotnetOutput);
        Assert.True(WorkspaceProcessEnvironmentSettings.FromOptions(null, null, out _).RedirectReadOnlyDotnetOutput);
        Assert.False(WorkspaceProcessEnvironmentSettings.FromOptions(
            new ProcessEnvironmentOptions { RedirectReadOnlyDotnetOutput = false }, null, out _).RedirectReadOnlyDotnetOutput);
    }

    // T-C2: every redirected dotnet command carries --artifacts-path exactly once; others never.

    [Fact]
    public void Every_dotnet_plan_adds_the_artifacts_path_once_when_redirected()
    {
        var (builder, workspaceRoot, projectAlias, _) = CreateExternalProject();
        const string buildFolder = ".build/0123456789ab";
        var expected = Path.GetFullPath(Path.Combine(workspaceRoot, ".build", "0123456789ab"));
        var published = new WorkspacePublishedOutputPlanBuilder(
            TestWorkspaceServices.CreatePathPolicy(workspaceRoot, externalTargetRegistry: externalTargets), builder);

        WorkspaceCommandPlan[] plans =
        [
            builder.BuildDotnetRestore(projectAlias, artifactsPath: buildFolder),
            builder.BuildDotnetBuild(projectAlias, artifactsPath: buildFolder),
            builder.BuildDotnetTest(projectAlias, noBuild: true, filter: "Category=Fast", artifactsPath: buildFolder),
            builder.BuildDotnetRun(projectAlias, waitForHttp: false, artifactsPath: buildFolder),
            builder.BuildDotnetRun(projectAlias, url: "http://127.0.0.1:5987", waitForHttp: true, artifactsPath: buildFolder),
            published.Publish(projectAlias, "Release", noRestore: false, workingDirectory: null, timeoutSeconds: 600, artifactsPath: buildFolder).Command
        ];

        foreach (var plan in plans)
        {
            var arguments = plan.Arguments.ToList();
            Assert.Single(arguments, argument => argument == "--artifacts-path");
            var index = arguments.IndexOf("--artifacts-path");
            Assert.Equal(expected, arguments[index + 1], ignoreCase: OperatingSystem.IsWindows());
            var separator = arguments.IndexOf("--");
            Assert.True(separator < 0 || index < separator, $"{plan.Decision.RecipeId}: --artifacts-path must precede the application arguments.");
            Assert.Contains(buildFolder, plan.TargetPaths);
        }
    }

    [Fact]
    public void No_dotnet_plan_carries_an_artifacts_path_without_a_redirect()
    {
        var (builder, workspaceRoot, projectAlias, _) = CreateExternalProject();
        var published = new WorkspacePublishedOutputPlanBuilder(
            TestWorkspaceServices.CreatePathPolicy(workspaceRoot, externalTargetRegistry: externalTargets), builder);

        WorkspaceCommandPlan[] plans =
        [
            builder.BuildDotnetRestore(projectAlias),
            builder.BuildDotnetBuild(projectAlias),
            builder.BuildDotnetTest(projectAlias),
            builder.BuildDotnetRun(projectAlias, waitForHttp: false),
            builder.BuildDotnetRun(projectAlias, url: "http://127.0.0.1:5987", waitForHttp: true),
            published.Publish(projectAlias, "Release", noRestore: false, workingDirectory: null, timeoutSeconds: 600).Command
        ];

        foreach (var plan in plans)
        {
            Assert.DoesNotContain("--artifacts-path", plan.Arguments);
            Assert.DoesNotContain(plan.TargetPaths, path => path.StartsWith(".build", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void An_artifacts_path_outside_the_workspace_is_rejected()
    {
        var (builder, _, projectAlias, externalFolder) = CreateExternalProject();
        var externalAlias = BuildExternalTargetAlias(externalFolder);

        Assert.ThrowsAny<InvalidOperationException>(() => builder.BuildDotnetBuild(projectAlias, artifactsPath: externalAlias));
        Assert.ThrowsAny<InvalidOperationException>(() => builder.BuildDotnetBuild(projectAlias, artifactsPath: "../outside"));
    }

    // T-C3: the result names the folder, and the receipt lists it as a target.

    [Fact]
    public async Task A_redirected_build_says_where_its_output_is_and_records_it_in_the_receipt()
    {
        var workspaceRoot = CreateDirectory("workspace-service");
        var projectAlias = CreateExternalProjectAlias("external-service");
        var processHost = new RecordingWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(
            workspaceRoot, processHost, externalTargetRegistry: externalTargets);

        var redirected = await service.DotnetBuild(projectAlias, artifactsPath: ".build/0123456789ab");
        var plain = await service.DotnetBuild(projectAlias);

        Assert.True(redirected.Succeeded, redirected.Message);
        Assert.EndsWith(
            "Build output for this read-only target is kept in '.build/0123456789ab'; the target folder is not modified.",
            redirected.Message,
            StringComparison.Ordinal);
        Assert.Contains(".build/0123456789ab", redirected.Receipt.TargetPaths);
        Assert.DoesNotContain("read-only target", plain.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("--artifacts-path", processHost.Requests[^1].Arguments);
        Assert.Contains("--artifacts-path", processHost.Requests[^2].Arguments);
    }

    // T-C1 at the plugin: the agent's current access decides, and the agent cannot choose the folder.

    [Fact]
    public async Task The_plugin_redirects_builds_of_read_only_targets_only()
    {
        var workspaceRoot = CreateDirectory("workspace-plugin");
        var productFolder = CreateDirectory("product");
        await File.WriteAllTextAsync(Path.Combine(productFolder, "App.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var productRoot = BuildExternalTargetAlias(productFolder);
        var projectAlias = $"{productRoot}/App.csproj";
        var processHost = new RecordingWorkspaceProcessHost();
        var plugin = CreatePlugin(workspaceRoot, processHost);

        using (WorkspaceExecutionAuditContext.BeginScope(CreateRun(readOnlyAliases: [productRoot])))
        {
            var readOnly = await plugin.DotnetWorkspaceBuild(projectAlias);
            Assert.True(readOnly.Succeeded, readOnly.Message);
        }

        using (WorkspaceExecutionAuditContext.BeginScope(CreateRun(writableAliases: [productRoot])))
        {
            var writable = await plugin.DotnetWorkspaceBuild(projectAlias);
            Assert.True(writable.Succeeded, writable.Message);
        }

        // A long workspace root runs through a temporary drive alias on Windows, so compare the folder's tail.
        var expectedTail = Path.Combine(".build", WorkspaceReadOnlyBuildOutput.ComputeKey(productRoot));
        var readOnlyArguments = processHost.Requests[0].Arguments.ToList();
        Assert.EndsWith(expectedTail, readOnlyArguments[readOnlyArguments.IndexOf("--artifacts-path") + 1], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--artifacts-path", processHost.Requests[1].Arguments);
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

    private (WorkspaceCommandPlanBuilder Builder, string WorkspaceRoot, string ProjectAlias, string ExternalFolder) CreateExternalProject()
    {
        var workspaceRoot = CreateDirectory($"workspace-{Guid.NewGuid():N}");
        var externalFolder = CreateDirectory($"external-{Guid.NewGuid():N}");
        File.WriteAllText(Path.Combine(externalFolder, "App.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        var builder = new WorkspaceCommandPlanBuilder(
            TestWorkspaceServices.CreatePathPolicy(workspaceRoot, externalTargetRegistry: externalTargets));
        return (builder, workspaceRoot, $"{BuildExternalTargetAlias(externalFolder)}/App.csproj", externalFolder);
    }

    // Product roots are bound as folders; the project is a path inside the bound root.
    private string CreateExternalProjectAlias(string folderName)
    {
        var externalFolder = CreateDirectory(folderName);
        File.WriteAllText(Path.Combine(externalFolder, "App.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        return $"{BuildExternalTargetAlias(externalFolder)}/App.csproj";
    }

    private WorkspaceRuntimePlugin CreatePlugin(string workspaceRoot, IWorkspaceProcessHost processHost)
    {
        var accessSettings = AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.SoftwareDevelopment);
        var commandService = TestWorkspaceServices.CreateCommandExecutionService(
            workspaceRoot, processHost, externalTargetRegistry: externalTargets);
        return new WorkspaceRuntimePlugin(
            commandService,
            null!,
            workspaceRoot,
            TestWorkspaceServices.PhysicalPathPolicyFactory,
            WorkspaceScopeDescriptor.Sandbox,
            accessSettings,
            CreateProvider(),
            "test-model",
            null!);
    }

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
            Title: "Read-only build output test",
            SourceKind: "test",
            SourceId: "read-only-build-output",
            CorrelationId: Guid.NewGuid().ToString("D"),
            CausationId: string.Empty,
            RequestedBy: "unit-test",
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

    private string CreateDirectory(string name)
    {
        var path = Path.Combine(rootPath, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private string BuildExternalTargetAlias(string fullPath)
        => externalTargets.TryCreateAlias(fullPath, out var alias)
            ? alias
            : throw new InvalidOperationException($"Could not create an external-target alias for '{fullPath}'.");

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

    private sealed class RecordingWorkspaceProcessHost : IWorkspaceProcessHost
    {
        public List<WorkspaceProcessExecutionRequest> Requests { get; } = [];

        public ExecutionBoundaryDescriptor DescribeBoundary()
            => new(
                Mode: "Test",
                FilesystemScope: "Workspace",
                NetworkScope: "None",
                CredentialScope: "None",
                HostLabel: "Fake",
                IsEnforcedByHost: false,
                Notes: "Unit test host.");

        public Task<WorkspaceProcessExecutionResult> ExecuteAsync(
            WorkspaceProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!string.Equals(request.ToolName, "workspace_path_alias", StringComparison.Ordinal))
            {
                Requests.Add(request);
            }

            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new WorkspaceProcessExecutionResult(
                Started: true,
                ExitCode: 0,
                Stdout: "ok",
                Stderr: string.Empty,
                StdoutTruncated: false,
                StderrTruncated: false,
                StartedAtUtc: now,
                CompletedAtUtc: now,
                TimedOut: false,
                Boundary: DescribeBoundary(),
                FailureMessage: string.Empty));
        }
    }
}
