using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using System.Diagnostics;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkspaceExecutionAuditTrailWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static void PersistReceipt(
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope,
        WorkspaceToolReceipt receipt,
        string toolFamily,
        string toolName,
        string riskClass,
        string approvalMode,
        string isolationGuarantee,
        string requestSummary,
        string workingDirectory,
        string exitSummary)
    {
        var scope = WorkspaceExecutionAuditContext.Current;
        if (scope is null || receipt.ExecutionRunId is null)
        {
            return;
        }

        var executionRunId = receipt.ExecutionRunId.Value;
        var runtimeToolOwnership = ResolveRuntimeToolOwnership(toolName);
        var receiptRecord = new ToolExecutionReceiptRecord(
            Id: CreateDeterministicGuid($"{executionRunId:N}|receipt|{receipt.ReceiptRelativePath}|{toolName}|{receipt.StartedAtUtc:O}"),
            ExecutionRunId: executionRunId,
            ToolFamily: toolFamily,
            ToolName: toolName,
            RiskClass: riskClass,
            ApprovalMode: approvalMode,
            IsolationGuarantee: isolationGuarantee,
            RequestSummary: requestSummary,
            WorkingDirectory: workingDirectory,
            ExitSummary: exitSummary,
            StartedAtUtc: receipt.StartedAtUtc,
            CompletedAtUtc: receipt.CompletedAtUtc)
        {
            RuntimeToolProviderKey = runtimeToolOwnership?.ProviderKey ?? string.Empty,
            RuntimeToolProviderName = runtimeToolOwnership?.ProviderName ?? string.Empty
        };

        using (var receiptActivity = AgentFrameworkTelemetry.ActivitySource.StartActivity("tool.receipt", ActivityKind.Internal))
        {
            AgentFrameworkTelemetry.ApplyCurrentAuditScope(receiptActivity);
            receiptActivity?.SetTag("agentframework.receipt_id", receiptRecord.Id.ToString("N"));
            receiptActivity?.SetTag("agentframework.tool_family", toolFamily);
            receiptActivity?.SetTag("agentframework.tool_name", toolName);
            receiptActivity?.SetTag("agentframework.risk_class", riskClass);
            receiptActivity?.SetTag("agentframework.approval_mode", approvalMode);
            if (runtimeToolOwnership is not null)
            {
                receiptActivity?.SetTag("agentframework.runtime_tool_provider_key", runtimeToolOwnership.ProviderKey);
                receiptActivity?.SetTag("agentframework.runtime_tool_provider_name", runtimeToolOwnership.ProviderName);
            }

            AgentFrameworkTelemetry.RecordToolExecution(toolFamily, toolName, riskClass);
        }

        PersistRecord(
            workspaceRoot,
            Path.Combine(GetRunAuditRoot(workspaceRoot, workspaceScope, executionRunId), "receipts", $"{receiptRecord.Id:N}.json"),
            receiptRecord);

        foreach (var artifact in receipt.ArtifactReferences.Where(item => !string.Equals(item.Zone, "tool-receipt", StringComparison.OrdinalIgnoreCase)))
        {
            var artifactRecord = new ExecutionArtifactRecord(
                Id: CreateDeterministicGuid($"{executionRunId:N}|artifact|{receipt.ReceiptRelativePath}|{artifact.RelativePath}|{artifact.Zone}"),
                ExecutionRunId: executionRunId,
                ArtifactKind: artifact.Zone,
                DisplayName: artifact.DisplayName,
                RelativePath: NormalizeRelativePath(artifact.RelativePath),
                ContentType: artifact.ContentType,
                ProducedBy: toolName,
                Summary: artifact.Summary,
                CreatedAtUtc: receipt.CompletedAtUtc);

            using (var artifactActivity = AgentFrameworkTelemetry.ActivitySource.StartActivity("artifact.register", ActivityKind.Internal))
            {
                AgentFrameworkTelemetry.ApplyCurrentAuditScope(artifactActivity);
                artifactActivity?.SetTag("agentframework.artifact_id", artifactRecord.Id.ToString("N"));
                artifactActivity?.SetTag("agentframework.artifact_kind", artifactRecord.ArtifactKind);
                artifactActivity?.SetTag("agentframework.relative_path", artifactRecord.RelativePath);
                AgentFrameworkTelemetry.RecordArtifactRegistration(artifactRecord.ArtifactKind, artifactRecord.ContentType);
            }

            PersistRecord(
                workspaceRoot,
                Path.Combine(GetRunAuditRoot(workspaceRoot, workspaceScope, executionRunId), "artifacts", $"{artifactRecord.Id:N}.json"),
                artifactRecord);
        }
    }

    public static string GetRunAuditRoot(string workspaceRoot, WorkspaceScopeDescriptor workspaceScope, Guid executionRunId)
    {
        return Path.Combine(workspaceScope.ResolveDataRoot(workspaceRoot), "execution", "runs", executionRunId.ToString("N"), "audit");
    }

    private static void PersistRecord<T>(string workspaceRoot, string fullPath, T payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        WriteJsonAtomically(fullPath, payload);
    }

    private static void WriteJsonAtomically<T>(string fullPath, T payload)
    {
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(directory, $"{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(tempPath, JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8);
            if (File.Exists(fullPath))
            {
                File.Replace(tempPath, fullPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, fullPath);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static Guid CreateDeterministicGuid(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        Span<byte> buffer = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(buffer);
        buffer[6] = (byte)((buffer[6] & 0x0F) | 0x50);
        buffer[8] = (byte)((buffer[8] & 0x3F) | 0x80);
        return new Guid(buffer);
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }

    private static AgentRuntimeToolOwnership? ResolveRuntimeToolOwnership(string toolName)
    {
        var ownership = AgentRuntimeToolOwnershipContext.Current;
        if (ownership is null ||
            !string.Equals(ownership.ToolName, toolName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return ownership;
    }
}
