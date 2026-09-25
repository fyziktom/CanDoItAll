using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class WorkspacePublishedOutputPlanBuilder(WorkspacePathPolicy pathPolicy, WorkspaceCommandPlanBuilder commands) {
    internal const string EntryDocument = "index.html";
    internal const string WebRootName = "wwwroot";

    public WorkspacePublishedOutputPlan Publish(string targetPath, string configuration, bool noRestore, string? workingDirectory, int timeoutSeconds) {
        if (string.IsNullOrWhiteSpace(targetPath) || Path.GetExtension(targetPath).ToLowerInvariant() is not (".csproj" or ".fsproj" or ".vbproj")) {
            throw WorkspaceCommandInputException.Create("Publish requires one project file.", "Publish requires one .csproj, .fsproj or .vbproj path.");
        }
        var build = commands.BuildDotnetBuild(targetPath, configuration, noRestore, workingDirectory, timeoutSeconds);
        // A process run addresses its evidence through the run's managed artifact reference. Resolving that reference
        // stores the output in the run's managed root, where the reported reference and later tools find it again.
        var output = Guid.TryParse(WorkspaceExecutionAuditContext.Current?.ProcessRunId, out var processRunId)
            ? WorkspaceScopeDescriptor.Sandbox.CombineArtifactPath("process-runs", processRunId.ToString("D"), "published-output", Guid.NewGuid().ToString("N"))
            : pathPolicy.WorkspaceScope.CombineArtifactPath("tool-runs", "published-output", Guid.NewGuid().ToString("N"));
        if (!pathPolicy.TryResolveWorkspacePath(output, false, out var resolved, out var validation)) {
            throw WorkspaceCommandInputException.Create(validation, validation);
        }
        return new(build with {
            Decision = build.Decision with {
                ToolName = ToolContractCatalog.WorkspaceDotNetPublish,
                RecipeId = "dotnet_publish",
                Reason = "Publish project and generated output passed workspace policy checks."
            },
            Arguments = ["publish", .. build.Arguments.Skip(1), "--output", resolved.FullPath],
            TargetPaths = [.. build.TargetPaths, resolved.RelativePath],
            MutatesWorkspace = true
        }, output, resolved.FullPath);
    }

    public WorkspaceCommandPlan Serve(string hostAssemblyPath, string directoryPath, string? url, bool spaFallback, int startupTimeoutSeconds) {
        if (!Path.IsPathFullyQualified(hostAssemblyPath) || !File.Exists(hostAssemblyPath)) {
            throw new InvalidOperationException("The registered static validation host assembly is unavailable.");
        }
        var directory = pathPolicy.ResolveExistingPath(directoryPath, allowFiles: false, allowDirectories: true);
        // The host proves readiness by serving the entry document, so a directory without one could only time out.
        if (!File.Exists(Path.Combine(directory.FullPath, EntryDocument))) {
            var webRoot = File.Exists(Path.Combine(directory.FullPath, WebRootName, EntryDocument))
                ? $" Its web root '{WorkspaceScopeDescriptor.NormalizeRelativePath(directoryPath)}/{WebRootName}' has one; serve that directory instead."
                : string.Empty;
            throw WorkspaceCommandInputException.Create(
                $"Static validation directory '{directory.RelativePath}' has no {EntryDocument}.",
                $"The directory has no {EntryDocument} entry document to serve.{webRoot}");
        }
        var urls = WorkspaceCommandPlanBuilder.ResolveManagedDotnetRunUrls(WorkspaceCommandPlanBuilder.ResolveDotnetRunUrls(url));
        var endpoint = new Uri(urls.ListenUrl!, UriKind.Absolute);
        if (endpoint.Scheme != Uri.UriSchemeHttp) {
            throw WorkspaceCommandInputException.Create("Static validation host only supports HTTP.", "Use an http:// loopback URL for the static validation host.");
        }
        var listenUrl = new UriBuilder(endpoint) { Host = endpoint.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ? "127.0.0.1" : endpoint.Host }.Uri.GetLeftPart(UriPartial.Authority);
        var artifacts = commands.BuildDotnetRunArtifactPaths();
        var startupTimeout = Math.Clamp(startupTimeoutSeconds, 1, 600);
        return commands.CreatePlan(
            ToolContractCatalog.WorkspaceStaticServe, "static_serve", "LocalExecution", false, true, true,
            [directory.RelativePath, .. artifacts.TargetPaths], ".", pathPolicy.WorkspaceRoot,
            ["dotnet"], [hostAssemblyPath, directory.FullPath, listenUrl, spaFallback.ToString()],
            startupTimeout + 10, 128 * 1024, 128 * 1024,
            dotnetRunLifecycle: new WorkspaceDotnetRunLifecyclePlan(
                listenUrl, listenUrl, startupTimeout, true, WorkspaceProcessLifetimeScope.ExecutionRun,
                artifacts.StdoutLogFullPath, artifacts.StdoutLogRelativePath,
                artifacts.StderrLogFullPath, artifacts.StderrLogRelativePath,
                artifacts.StartupReceiptFullPath, artifacts.StartupReceiptRelativePath,
                artifacts.CleanupReceiptFullPath, artifacts.CleanupReceiptRelativePath));
    }
}

internal sealed record WorkspacePublishedOutputPlan(WorkspaceCommandPlan Command, string OutputReference, string OutputFullPath) {
    // A published web project keeps its static site under the web root; naming it spares a guess before serving it.
    public string Describe()
        => File.Exists(Path.Combine(OutputFullPath, WorkspacePublishedOutputPlanBuilder.WebRootName, WorkspacePublishedOutputPlanBuilder.EntryDocument))
            ? $"Published output directory: {OutputReference}. Static web root: {OutputReference}/{WorkspacePublishedOutputPlanBuilder.WebRootName}."
            : $"Published output directory: {OutputReference}.";
}
