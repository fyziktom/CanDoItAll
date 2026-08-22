using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkflowExecutorInvocationKeyFactory
{
    public static WorkflowExecutorInvocationIdentity Create(
        WorkflowRunId runId,
        WorkflowVersionId workflowVersionId,
        WorkflowNodeId nodeId,
        WorkflowExecutorId executorId,
        WorkflowExecutorContractVersion executorContractVersion,
        WorkflowExternalRequestId causationRequestId,
        WorkflowExternalRequestVersion causationRequestVersion,
        WorkflowExternalResponseOperationId causationOperationId,
        WorkflowExecutorInvocationGeneration logicalGeneration,
        WorkflowNodeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var inputHash = WorkflowExecutorInputHash.Compute(input);
        var scopeHash = HashParts(
            runId.ToString(),
            workflowVersionId.ToString(),
            nodeId.Value,
            executorId.Value,
            executorContractVersion.Value,
            causationRequestId.ToString(),
            causationRequestVersion.ToString(),
            causationOperationId.ToString(),
            logicalGeneration.ToString());
        var keyHash = HashParts(scopeHash, inputHash.Value);
        var idempotencyKey = HashParts("workflow-executor-invocation", keyHash);
        return new WorkflowExecutorInvocationIdentity(
            new WorkflowExecutorInvocationScopeKey(scopeHash),
            new WorkflowExecutorInvocationKey(keyHash),
            new WorkflowExecutorInvocationIdempotencyKey(idempotencyKey),
            runId,
            workflowVersionId,
            nodeId,
            executorId,
            executorContractVersion,
            causationRequestId,
            causationRequestVersion,
            causationOperationId,
            logicalGeneration,
            inputHash);
    }

    private static string HashParts(params string[] parts)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var length = new byte[sizeof(int)];
        foreach (var part in parts)
        {
            var bytes = Encoding.UTF8.GetBytes(part);
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
