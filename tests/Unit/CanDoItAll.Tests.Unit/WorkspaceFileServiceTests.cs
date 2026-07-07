using System.IO.Compression;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceFileServiceTests
{
    [Fact]
    public async Task AgentChatAttachmentStagingService_stage_image_writes_scoped_artifact_and_returns_logical_path()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.ChatAttachmentTests.{Guid.NewGuid():N}");
        var scope = WorkspaceScopeDescriptor.Organization("test-org");
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            var pathResolution = new WorkspacePathResolutionService(workspaceRoot, scope);
            var service = new AgentChatAttachmentStagingService(pathResolution);
            var bytes = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };

            var result = await service.StageImageAsync(
                "qa screenshot.PNG",
                "image/png",
                bytes.Length,
                new MemoryStream(bytes));

            Assert.StartsWith("artifacts/chat-attachments/", result.RelativePath, StringComparison.Ordinal);
            Assert.EndsWith(".png", result.RelativePath, StringComparison.Ordinal);
            Assert.Equal("image/png", result.ContentType);

            var resolved = pathResolution.ResolveFilePath(result.RelativePath, allowMissing: false);
            Assert.True(File.Exists(resolved.FullPath));
            Assert.Contains(
                Path.Combine("artifacts", "scopes", "organization", "test-org", "chat-attachments"),
                resolved.FullPath,
                StringComparison.OrdinalIgnoreCase);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(resolved.FullPath));
        }
        finally
        {
            try
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task AgentChatAttachmentStagingService_stage_image_rejects_non_image_extension()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.ChatAttachmentTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            var service = new AgentChatAttachmentStagingService(new WorkspacePathResolutionService(workspaceRoot));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StageImageAsync(
                "notes.txt",
                "text/plain",
                4,
                new MemoryStream([1, 2, 3, 4])));

            Assert.Contains("Only PNG, JPEG, GIF, and WebP", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            try
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void WriteTextFile_registers_showcase_deliverable_as_execution_artifact()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceFileServiceTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            var service = new WorkspaceFileService(workspaceRoot);

            var result = service.WriteTextFile(
                "showcases/blazor-ssr-workflow/app/SimpleWorkflowApp/Program.cs",
                "var builder = WebApplication.CreateBuilder(args);");

            Assert.True(result.Succeeded);
            Assert.Contains(
                result.Receipt.ArtifactReferences,
                item =>
                    string.Equals(item.Zone, "generated-output", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        item.RelativePath,
                        "showcases/blazor-ssr-workflow/app/SimpleWorkflowApp/Program.cs",
                        StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            try
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void WriteTextFile_tags_audit_receipt_with_runtime_tool_provider_ownership()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceFileServiceTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);
        var run = CreateRun();

        try
        {
            var service = new WorkspaceFileService(workspaceRoot);
            using (WorkspaceExecutionAuditContext.BeginScope(run))
            using (AgentRuntimeToolOwnershipContext.BeginScope(new AgentRuntimeToolOwnership(
                       "processes.runtime-tools",
                       "Processes runtime tools",
                       "workspace_write_file")))
            {
                var result = service.WriteTextFile("artifacts/process-runs/run-001/provider-proof.md", "proof");

                Assert.True(result.Succeeded);
            }

            var receiptPath = Assert.Single(Directory.GetFiles(
                Path.Combine(workspaceRoot, "data", "execution", "runs", run.Id.ToString("N"), "audit", "receipts"),
                "*.json"));
            var receipt = JsonSerializer.Deserialize<ToolExecutionReceiptRecord>(
                              File.ReadAllText(receiptPath),
                              new JsonSerializerOptions(JsonSerializerDefaults.Web))
                          ?? throw new InvalidOperationException("Receipt JSON did not deserialize.");

            Assert.Equal("workspace_write_file", receipt.ToolName);
            Assert.Equal("processes.runtime-tools", receipt.RuntimeToolProviderKey);
            Assert.Equal("Processes runtime tools", receipt.RuntimeToolProviderName);
        }
        finally
        {
            try
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void ToolExecutionReceiptRecord_deserializes_legacy_receipts_with_empty_runtime_provider_ownership()
    {
        var receiptId = Guid.NewGuid();
        var executionRunId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;
        var json = $$"""
            {
              "id": "{{receiptId}}",
              "executionRunId": "{{executionRunId}}",
              "toolFamily": "workspace-file",
              "toolName": "workspace_read_file",
              "riskClass": "ReadOnlyWorkspace",
              "approvalMode": "NotRequired",
              "isolationGuarantee": "Workspace file service.",
              "requestSummary": "README.md",
              "workingDirectory": ".",
              "exitSummary": "Succeeded",
              "startedAtUtc": "{{timestamp:O}}",
              "completedAtUtc": "{{timestamp:O}}"
            }
            """;

        var receipt = JsonSerializer.Deserialize<ToolExecutionReceiptRecord>(
                          json,
                          new JsonSerializerOptions(JsonSerializerDefaults.Web))
                      ?? throw new InvalidOperationException("Receipt JSON did not deserialize.");

        Assert.Equal(receiptId, receipt.Id);
        Assert.Equal(executionRunId, receipt.ExecutionRunId);
        Assert.Equal(string.Empty, receipt.RuntimeToolProviderKey);
        Assert.Equal(string.Empty, receipt.RuntimeToolProviderName);
    }

    [Fact]
    public void ZipPath_does_not_delete_existing_destination_when_source_validation_fails()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceFileServiceTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "archives"));
        var existingArchivePath = Path.Combine(workspaceRoot, "archives", "docs.zip");
        File.WriteAllText(existingArchivePath, "existing archive");

        try
        {
            var service = new WorkspaceFileService(workspaceRoot);

            var result = service.ZipPath("missing", "archives/docs.zip", overwrite: true);

            Assert.False(result.Succeeded);
            Assert.Equal("existing archive", File.ReadAllText(existingArchivePath));
        }
        finally
        {
            try
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void UnzipArchive_does_not_extract_partial_files_when_overwrite_conflict_exists()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceFileServiceTests.{Guid.NewGuid():N}");
        var sourceDirectory = Path.Combine(workspaceRoot, "source");
        var destinationDirectory = Path.Combine(workspaceRoot, "expanded");
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(destinationDirectory);
        File.WriteAllText(Path.Combine(sourceDirectory, "a.txt"), "new a");
        File.WriteAllText(Path.Combine(sourceDirectory, "b.txt"), "new b");
        File.WriteAllText(Path.Combine(destinationDirectory, "b.txt"), "existing b");
        ZipFile.CreateFromDirectory(sourceDirectory, Path.Combine(workspaceRoot, "archive.zip"));

        try
        {
            var service = new WorkspaceFileService(workspaceRoot);

            var result = service.UnzipArchive("archive.zip", "expanded", overwrite: false);

            Assert.False(result.Succeeded);
            Assert.False(File.Exists(Path.Combine(destinationDirectory, "a.txt")));
            Assert.Equal("existing b", File.ReadAllText(Path.Combine(destinationDirectory, "b.txt")));
        }
        finally
        {
            try
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static ExecutionRunRecord CreateRun()
    {
        var now = DateTimeOffset.UtcNow;
        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            ChatSessionId: null,
            Title: "Receipt provider ownership",
            SourceKind: "manual",
            SourceId: "receipt-provider-ownership",
            CorrelationId: "corr-001",
            CausationId: string.Empty,
            RequestedBy: "unit-test",
            RequestedByKind: "system",
            MetadataJson: "{}",
            InputSummary: "Input",
            ResultSummary: string.Empty,
            ProviderName: "Provider",
            Model: "model",
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
}
