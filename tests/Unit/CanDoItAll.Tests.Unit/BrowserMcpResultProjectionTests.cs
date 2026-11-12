using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Protocol;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class BrowserMcpResultProjectionTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Requested_filename_without_produced_artifact_does_not_claim_saved(bool isError) {
        var canonical = BrowserMcpResultProjector.Capture(new CallToolResult {
            IsError = isError, Content = [new TextContentBlock { Text = "capture reply" }]
        });
        var result = BrowserMcpResultProjector.Project(ToolContractCatalog.BrowserTakeScreenshot,
            "artifacts/missing.png", canonical, new([]));
        Assert.DoesNotContain("Saved artifact", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Artifacts);
        Assert.Equal(AgentToolInvocationOutcome.Failed, result.Outcome);
        Assert.False(MafRuntimeToolInvocationResultClassifier.Assess(ToolContractCatalog.BrowserTakeScreenshot,
            ToolInvocationClassification.Read, result).Succeeded);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Function_preserves_mcp_status_and_only_imports_a_successful_fresh_capture(bool isError, bool produceFile) {
        var root = TestFileSystem.CreateTemporaryRoot("browser-mcp-results");
        try {
            var now = DateTimeOffset.UtcNow;
            var run = new ExecutionRunRecord(Guid.NewGuid(), Guid.NewGuid(), null, "Browser evidence", "process-step", "qa",
                "proof", "proof", "unit-test", "system", "{}", "Capture", "", "fixture", "fixture",
                ExecutionState.Running, null, now, now, now, null, "", null, [], ProcessRunId: Guid.NewGuid().ToString("D"));
            using var audit = WorkspaceExecutionAuditContext.BeginScope(run, WorkspaceScopeDescriptor.Sandbox,
                WorkspaceScopeDescriptor.Organization("execution-owner"));
            const string fileName = ".playwright-mcp/current.png";
            var arguments = new AIFunctionArguments { ["filename"] = fileName, ["fullPage"] = false };
            var original = JsonSerializer.Serialize(arguments);
            var native = AIFunctionFactory.Create((AIFunctionArguments received) => {
                Assert.Equal(original, JsonSerializer.Serialize(received));
                if (produceFile) {
                    File.WriteAllBytes(Path.Combine(root, fileName), [137, 80, 78, 71, 13, 10, 26, 10]);
                }
                return new CallToolResult { IsError = isError, Content = [ImageContentBlock.FromBytes(Convert.FromBase64String("iVBORw0KGgo="), "image/png")] };
            }, ToolContractCatalog.BrowserTakeScreenshot);
            var function = new BrowserMcpFunction(native, root, WorkspaceScopeDescriptor.Sandbox, []);
            var result = Assert.IsType<BrowserMcpToolResult>(await function.InvokeAsync(arguments));
            Assert.Equal(!isError && produceFile, result.Succeeded);
            Assert.Equal(original, JsonSerializer.Serialize(arguments));
            Assert.DoesNotContain("iVBORw0KGgo=", JsonSerializer.Serialize(result), StringComparison.Ordinal);
            Assert.Equal("iVBORw0KGgo=", result.CanonicalResult.GetProperty("content")[0].GetProperty("data").GetString());
            if (result.Succeeded) {
                var auditRoot = WorkspaceExecutionAuditTrailWriter.GetRunAuditRoot(root,
                    WorkspaceScopeDescriptor.Organization("execution-owner"), run.Id);
                var artifact = JsonSerializer.Deserialize<ExecutionArtifactRecord>(File.ReadAllText(Assert.Single(
                    Directory.GetFiles(Path.Combine(auditRoot, "artifacts"), "*.json"))), JsonSerializerOptions.Web)!;
                Assert.Equal(run.Id, artifact.ExecutionRunId);
                var reference = Assert.Single(result.Artifacts);
                Assert.StartsWith($"artifacts/process-runs/{run.ProcessRunId}/browser/", reference, StringComparison.Ordinal);
                Assert.Equal($"{WorkspaceScopeDescriptor.Organization("execution-owner").ArtifactRootRelativePath}/{reference["artifacts/".Length..]}", artifact.RelativePath);
                Assert.True(File.Exists(Path.Combine(root, artifact.RelativePath.Replace('/', Path.DirectorySeparatorChar))));
                Assert.DoesNotContain(artifact.RelativePath, JsonSerializer.Serialize(result), StringComparison.Ordinal);
                Assert.Equal("image/png", artifact.ContentType);
            } else {
                Assert.Empty(result.Artifacts);
                Assert.Empty(Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories));
            }
        } finally {
            TestFileSystem.DeleteDirectoryWithRetry(root);
        }
    }

    [Fact]
    public void Previous_invocation_file_is_not_fresh_evidence() {
        var root = TestFileSystem.CreateTemporaryRoot("browser-mcp-results");
        try {
            const string fileName = "artifacts/stale.png";
            var scope = WorkspaceScopeDescriptor.Sandbox;
            BrowserMcpArtifactPathService.EnsureWritableArtifactDirectories(root, scope, fileName);
            File.WriteAllText(Path.Combine(root, fileName), "previous capture");
            var before = BrowserMcpArtifactPathService.ObserveBeforeInvocation(root, scope, fileName);
            var result = BrowserMcpArtifactPathService.TryImportAfterInvocation(root, scope, fileName, null, before);
            Assert.False(result.Imported);
        } finally {
            TestFileSystem.DeleteDirectoryWithRetry(root);
        }
    }

    [Theory]
    [InlineData("artifacts/../outside.png")]
    [InlineData("artifacts/scopes/organization/another/capture.png")]
    [InlineData("../outside.png")]
    public void Out_of_scope_filename_cannot_produce_an_import_receipt(string fileName) {
        var root = TestFileSystem.CreateTemporaryRoot("browser-mcp-results");
        try {
            var scope = WorkspaceScopeDescriptor.Organization("current");
            var before = BrowserMcpArtifactPathService.ObserveBeforeInvocation(root, scope, fileName);
            Assert.Null(before.SourcePath);
            Assert.False(BrowserMcpArtifactPathService.TryImportAfterInvocation(root, scope, fileName, null, before).Imported);
        } finally {
            TestFileSystem.DeleteDirectoryWithRetry(root);
        }
    }

    [Fact]
    public void Snapshot_projection_is_bounded_while_canonical_evidence_is_retained() {
        var text = new string('x', 24000);
        var canonical = BrowserMcpResultProjector.Capture(new CallToolResult {
            Content = [new TextContentBlock { Text = text }]
        });
        var result = BrowserMcpResultProjector.Project(ToolContractCatalog.BrowserSnapshot, null, canonical, new([]));
        Assert.True(result.Succeeded);
        Assert.InRange(result.Message.Length, 1, 12000);
        Assert.Contains("[truncated]", result.Message);
        Assert.Contains(text, result.CanonicalResult.GetRawText());
        Assert.DoesNotContain(text, JsonSerializer.Serialize(result));
    }

    [Fact]
    public void Unstructured_mcp_reply_has_unknown_outcome() {
        var result = BrowserMcpResultProjector.Project(ToolContractCatalog.BrowserSnapshot, null,
            BrowserMcpResultProjector.Capture("completed"), new([]));
        Assert.Equal(AgentToolInvocationOutcome.Unknown, result.Outcome);
        Assert.False(result.Succeeded);
    }
}
