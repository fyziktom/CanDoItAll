using System.Net;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Tools.StaticHost;

namespace CanDoItAll.Tests.Unit.AgentFramework;

[Collection(nameof(LocalWorkspaceProcessHostTestCollection))]
public sealed class WorkspacePublishedOutputTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Published_output_is_served_with_owned_stop_or_terminal_cleanup(bool terminalCleanup, bool longWebRoot) {
        var directory = Directory.CreateTempSubdirectory("cdia-published-output-");
        var root = directory.FullName;
        var host = new LocalWorkspaceProcessHost();
        var commands = new WorkspaceCommandExecutionService(root, host, TestWorkspaceServices.PhysicalPathPolicyFactory);
        var now = DateTimeOffset.UtcNow;
        var run = new ExecutionRunRecord(Guid.NewGuid(), Guid.NewGuid(), null, "Static proof", "process-step", "qa",
            Guid.NewGuid().ToString("N"), string.Empty, "unit-test", "system", "{}", string.Empty, string.Empty,
            "test", "test", ExecutionState.Running, null, now, now, now, null, string.Empty, null, [],
            ProcessRunId: Guid.NewGuid().ToString(), ProcessStepId: "qa");
        using var audit = WorkspaceExecutionAuditContext.BeginScope(run);
        try {
            Directory.CreateDirectory(Path.Combine(root, "site"));
            await File.WriteAllTextAsync(Path.Combine(root, "Sample.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
                  <ItemGroup><Content Include="site/**" Link="wwwroot/%(RecursiveDir)%(Filename)%(Extension)" CopyToPublishDirectory="Always" /></ItemGroup>
                </Project>
                """);
            await File.WriteAllTextAsync(Path.Combine(root, "site", "index.html"), "<h1>Published fixture</h1>");
            await File.WriteAllBytesAsync(Path.Combine(root, "site", "sample.wasm"), [0, 97, 115, 109]);
            await File.WriteAllTextAsync(Path.Combine(root, "site", "service-worker.js"), "self.addEventListener('install', () => {});");
            var publish = await commands.DotnetPublish("Sample.csproj");
            Assert.True(publish.Succeeded, publish.StderrPreview + publish.StdoutPreview + publish.Message);
            Assert.Contains("publish", publish.ArgumentsSummary, StringComparison.Ordinal);
            var output = Assert.Single(publish.Receipt.TargetPaths, path => path.Contains("published-output/", StringComparison.Ordinal));
            Assert.StartsWith($"artifacts/process-runs/{run.ProcessRunId}/published-output/", output, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(root, output, "Sample.dll")));
            Assert.True(File.Exists(Path.Combine(root, output, "wwwroot", "index.html")));
            var webRoot = Path.GetFullPath(Path.Combine(root, output, "wwwroot"));
            if (longWebRoot) {
                var nestedWebRoot = Path.GetFullPath(Path.Combine(root, output, new string('x', 128), "wwwroot"));
                Directory.CreateDirectory(nestedWebRoot);
                foreach (var file in Directory.EnumerateFiles(webRoot)) {
                    File.Copy(file, Path.Combine(nestedWebRoot, Path.GetFileName(file)));
                }
                webRoot = nestedWebRoot;
                Assert.True(webRoot.Length > 260);
            }
            var link = Path.Combine(webRoot, "escape");
            Directory.CreateSymbolicLink(link, root);
            var served = await commands.ServeStaticFiles(typeof(StaticFileHost).Assembly.Location, webRoot);
            Assert.True(served.Succeeded, served.Message + served.StdoutPreview + served.StderrPreview);
            var startupPath = Assert.Single(served.Receipt.TargetPaths, path => path.EndsWith("/startup.json", StringComparison.Ordinal));
            using var startup = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, startupPath)));
            var url = startup.RootElement.GetProperty("probeUrl").GetString()!;
            using var client = new HttpClient { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(5) };
            foreach (var path in new[] { "/", "/route/nested", "/service-worker.js" }) {
                using var response = await client.GetAsync(path);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.True(response.Headers.CacheControl!.NoCache);
                Assert.True(response.Headers.CacheControl.MustRevalidate);
                Assert.Equal(TimeSpan.Zero, response.Headers.CacheControl.MaxAge);
                if (path != "/service-worker.js") {
                    Assert.Equal("<h1>Published fixture</h1>", await response.Content.ReadAsStringAsync());
                }
            }
            using var wasm = await client.GetAsync("/sample.wasm");
            Assert.Equal("application/wasm", wasm.Content.Headers.ContentType!.MediaType);
            Assert.Equal(new byte[] { 0, 97, 115, 109 }, await wasm.Content.ReadAsByteArrayAsync());
            using var escaped = await client.GetAsync("/escape/site/index.html");
            Assert.Equal(HttpStatusCode.NotFound, escaped.StatusCode);
            Directory.Delete(link);
            using var missing = await client.GetAsync("/missing.js");
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            using var traversal = await client.GetAsync("/%2e%2e%5cSample.csproj");
            Assert.Equal(HttpStatusCode.NotFound, traversal.StatusCode);
            using var post = await client.PostAsync("/", new StringContent("denied"));
            Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
            if (terminalCleanup) {
                var cleanup = await ((IWorkspaceExecutionRunProcessLeaseCleanupExecutor)commands).CleanupAsync(run.Id);
                Assert.Empty(cleanup.Failures);
                Assert.Contains(startupPath, cleanup.CleanedStartupReceiptPaths);
            } else {
                var stopped = await commands.DotnetStop(startupPath);
                Assert.True(stopped.Succeeded, stopped.Message);
            }
            Assert.Empty(new WorkspaceExecutionRunProcessLeaseStore(root, WorkspaceScopeDescriptor.Sandbox).Load(run.Id).Leases);
            await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("/"));
        } finally {
            var cleanup = await ((IWorkspaceExecutionRunProcessLeaseCleanupExecutor)commands).CleanupAsync(run.Id);
            Assert.Empty(cleanup.Failures);
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Reported_web_root_serves_from_the_run_managed_root_when_context_and_execution_scopes_differ() {
        var directory = Directory.CreateTempSubdirectory("cdia-published-scopes-");
        var root = directory.FullName;
        var project = new WorkspaceScopeDescriptor(WorkspaceScopeKind.Project, Guid.NewGuid().ToString("D"));
        var organization = new WorkspaceScopeDescriptor(WorkspaceScopeKind.Organization, Guid.NewGuid().ToString("N"));
        var commands = new WorkspaceCommandExecutionService(root, new LocalWorkspaceProcessHost(),
            TestWorkspaceServices.PhysicalPathPolicyFactory, project);
        var now = DateTimeOffset.UtcNow;
        var run = new ExecutionRunRecord(Guid.NewGuid(), Guid.NewGuid(), null, "Static proof", "process-step", "qa",
            Guid.NewGuid().ToString("N"), string.Empty, "unit-test", "system", "{}", string.Empty, string.Empty,
            "test", "test", ExecutionState.Running, null, now, now, now, null, string.Empty, null, [],
            ProcessRunId: Guid.NewGuid().ToString(), ProcessStepId: "qa");
        using var audit = WorkspaceExecutionAuditContext.BeginScope(run, project, organization);
        try {
            Directory.CreateDirectory(Path.Combine(root, "site"));
            await File.WriteAllTextAsync(Path.Combine(root, "Sample.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
                  <ItemGroup><Content Include="site/**" Link="wwwroot/%(RecursiveDir)%(Filename)%(Extension)" CopyToPublishDirectory="Always" /></ItemGroup>
                </Project>
                """);
            await File.WriteAllTextAsync(Path.Combine(root, "site", "index.html"), "<h1>Scoped fixture</h1>");

            var publish = await commands.DotnetPublish("Sample.csproj");

            Assert.True(publish.Succeeded, publish.StderrPreview + publish.StdoutPreview + publish.Message);
            const string webRootLabel = "Static web root: ";
            var webRoot = publish.Message[(publish.Message.IndexOf(webRootLabel, StringComparison.Ordinal) + webRootLabel.Length)..].TrimEnd('.');
            var output = webRoot[..^"/wwwroot".Length];
            var prefix = $"artifacts/process-runs/{run.ProcessRunId}/published-output/";
            Assert.StartsWith(prefix, output, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(root, organization.CombineArtifactPath(
                "process-runs", run.ProcessRunId, "published-output", output[prefix.Length..], "wwwroot", "index.html"))));

            using (var capture = AgentToolInvocationEffectScope.Begin()) {
                var outputRoot = await commands.ServeStaticFiles(typeof(StaticFileHost).Assembly.Location, output);
                Assert.False(outputRoot.Succeeded);
                Assert.Contains($"'{webRoot}'", outputRoot.Message, StringComparison.Ordinal);
                Assert.True(capture.RejectedBeforeEffect);
            }

            var served = await commands.ServeStaticFiles(typeof(StaticFileHost).Assembly.Location, webRoot);
            Assert.True(served.Succeeded, served.Message + served.StdoutPreview + served.StderrPreview);
            var startupPath = Assert.Single(served.Receipt.TargetPaths, path => path.EndsWith("/startup.json", StringComparison.Ordinal));
            using var startup = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, startupPath)));
            using var client = new HttpClient { BaseAddress = new Uri(startup.RootElement.GetProperty("probeUrl").GetString()!) };
            Assert.Equal("<h1>Scoped fixture</h1>", await client.GetStringAsync("/"));
        } finally {
            var cleanup = await ((IWorkspaceExecutionRunProcessLeaseCleanupExecutor)commands).CleanupAsync(run.Id);
            Assert.Empty(cleanup.Failures);
            directory.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("../outside.csproj")]
    [InlineData("Sample.sln")]
    [InlineData("--help")]
    public async Task Publish_rejects_unbounded_or_nonproject_targets(string targetPath) {
        var directory = Directory.CreateTempSubdirectory("cdia-publish-rejected-");
        try {
            var commands = new WorkspaceCommandExecutionService(directory.FullName, new LocalWorkspaceProcessHost(), TestWorkspaceServices.PhysicalPathPolicyFactory);
            var result = await commands.DotnetPublish(targetPath);
            Assert.False(result.Succeeded);
            Assert.Equal("Denied", result.Receipt.Outcome);
        } finally {
            directory.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("http://0.0.0.0:5000")]
    [InlineData("http://example.com:5000")]
    [InlineData("https://127.0.0.1:5000")]
    public void Static_host_rejects_nonloopback_or_unsupported_endpoints(string url) {
        Assert.Throws<ArgumentException>(() => StaticFileHost.Create(Path.GetTempPath(), url, true));
    }

    [Theory]
    [InlineData(false, true, 0)]
    [InlineData(true, false, 0)]
    [InlineData(true, true, 2)]
    public void Registered_validation_policy_requires_both_workspace_permission_and_context(bool permission, bool enabled, int count) {
        var policy = new WorkspaceValidationToolProvider().GetConfiguredWorkspacePolicy(
            new AgentWorkspaceToolAccessSettings { CanRunValidationCommands = permission },
            AgentRuntimeContextIntent.Empty with { WorkspaceToolsEnabled = enabled });
        Assert.Equal(count, policy.Capabilities.Count);
        if (count > 0) {
            Assert.Contains(policy.Capabilities, capability => capability.RuntimeToolName?.Value == ToolContractCatalog.WorkspaceDotNetPublish);
            Assert.Contains(policy.Capabilities, capability => capability.RuntimeToolName?.Value == ToolContractCatalog.WorkspaceStaticServe);
        }
        Assert.True(ToolCapabilityRegistry.TryResolve(ToolContractCatalog.WorkspaceStaticServe, out var metadata));
        Assert.False(metadata.CanMutateProduct);
        Assert.Equal(["LaunchRuntime"], Assert.Single(metadata.OperationRequirements).AnyOf);
    }
}
