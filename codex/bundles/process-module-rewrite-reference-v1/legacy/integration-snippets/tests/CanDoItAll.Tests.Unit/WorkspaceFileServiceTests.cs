using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceFileServiceTests
{
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
