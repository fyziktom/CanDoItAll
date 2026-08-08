using CanDoItAll.AgentFramework.Models;

using CanDoItAll.AgentFramework.Runtime.Abstractions;
namespace CanDoItAll.AgentFramework.Core;

internal sealed partial class AgentFrameworkWorkspaceExecutionService
{
    internal static IReadOnlyList<AgentToolInvocationTrace> ResolveFailureToolInvocationTraces(
        AgentRuntimeResponse? lastRuntimeResponse,
        Exception exception)
    {
        var priorTraces = lastRuntimeResponse?.ToolInvocationTraces ?? [];
        var exceptionTraces = exception is AgentRuntimeUsageException usageException
            ? usageException.ToolInvocationTraces
            : [];
        var seen = new HashSet<AgentToolInvocationTrace>();
        var traces = new List<AgentToolInvocationTrace>(priorTraces.Count + exceptionTraces.Count);
        foreach (var trace in priorTraces.Concat(exceptionTraces))
        {
            if (!seen.Add(trace))
            {
                continue;
            }

            traces.Add(trace with
            {
                Sequence = traces.Count + 1
            });
        }

        return traces;
    }

    internal static IReadOnlyList<ToolExecutionReceiptRecord> CreateToolInvocationTraceReceipts(
        ExecutionRunRecord run,
        AgentRuntimeResponse response)
        => CreateToolInvocationTraceReceipts(run, response.ToolInvocationTraces);

    internal static IReadOnlyList<ToolExecutionReceiptRecord> CreateToolInvocationTraceReceipts(
        ExecutionRunRecord run,
        IReadOnlyList<AgentToolInvocationTrace> toolInvocationTraces)
    {
        if (toolInvocationTraces.Count == 0)
        {
            return [];
        }

        var receipts = new List<ToolExecutionReceiptRecord>();
        foreach (var trace in toolInvocationTraces)
        {
            if (ShouldCreateRuntimeProviderToolReceipt(trace))
            {
                receipts.Add(CreateRuntimeProviderToolReceipt(run, trace));
            }
            else if (ShouldCreateFallbackTraceReceipt(run.Id, trace))
            {
                receipts.Add(CreateFallbackTraceReceipt(run, trace));
            }
        }

        return receipts
            .OrderByDescending(item => item.CompletedAtUtc)
            .ThenByDescending(item => item.StartedAtUtc)
            .ToArray();
    }

    private static ToolExecutionReceiptRecord CreateRuntimeProviderToolReceipt(
        ExecutionRunRecord run,
        AgentToolInvocationTrace trace)
    {
        return new ToolExecutionReceiptRecord(
            Id: CreateDeterministicGuid($"{run.Id:N}|runtime-provider-tool|{trace.Sequence}|{trace.StartedAtUtc:O}|{trace.ToolName}|{trace.RuntimeToolProviderKey}|{trace.Signature}"),
            ExecutionRunId: run.Id,
            ToolFamily: "runtime-provider",
            ToolName: trace.ToolName,
            RiskClass: ResolveRuntimeProviderReceiptRiskClass(trace.Classification),
            ApprovalMode: "PolicyEnforced",
            IsolationGuarantee: "RuntimeProviderPolicy",
            RequestSummary: ResolveTraceRequestSummary(trace),
            WorkingDirectory: string.Empty,
            ExitSummary: ResolveTraceExitSummary(trace),
            StartedAtUtc: trace.StartedAtUtc,
            CompletedAtUtc: trace.CompletedAtUtc ?? trace.StartedAtUtc)
        {
            RuntimeToolProviderKey = trace.RuntimeToolProviderKey.Trim(),
            RuntimeToolProviderName = trace.RuntimeToolProviderName.Trim()
        };
    }

    private static ToolExecutionReceiptRecord CreateFallbackTraceReceipt(
        ExecutionRunRecord run,
        AgentToolInvocationTrace trace)
    {
        return new ToolExecutionReceiptRecord(
            Id: CreateDeterministicGuid($"{run.Id:N}|agent-tool-trace|{trace.Sequence}|{trace.StartedAtUtc:O}|{trace.ToolName}|{trace.Signature}"),
            ExecutionRunId: run.Id,
            ToolFamily: "agent-tool-trace",
            ToolName: trace.ToolName,
            RiskClass: $"ToolInvocation:{trace.Classification}",
            ApprovalMode: "PolicyEnforced",
            IsolationGuarantee: "ToolInvocationPolicy",
            RequestSummary: ResolveTraceRequestSummary(trace),
            WorkingDirectory: string.Empty,
            ExitSummary: ResolveTraceExitSummary(trace),
            StartedAtUtc: trace.StartedAtUtc,
            CompletedAtUtc: trace.CompletedAtUtc ?? trace.StartedAtUtc);
    }

    private static bool ShouldCreateRuntimeProviderToolReceipt(AgentToolInvocationTrace trace)
    {
        if (string.IsNullOrWhiteSpace(trace.ToolName) ||
            string.IsNullOrWhiteSpace(trace.RuntimeToolProviderKey) ||
            trace.CompletedAtUtc is null)
        {
            return false;
        }

        return trace.Classification is ToolInvocationClassification.Mutation
            or ToolInvocationClassification.Validation
            or ToolInvocationClassification.Read;
    }

    private static bool ShouldCreateFallbackTraceReceipt(
        Guid executionRunId,
        AgentToolInvocationTrace trace)
    {
        if (string.IsNullOrWhiteSpace(trace.ToolName) ||
            !string.IsNullOrWhiteSpace(trace.RuntimeToolProviderKey) ||
            trace.CompletedAtUtc is null ||
            trace.DirectReceiptExecutionRunId == executionRunId)
        {
            return false;
        }

        return trace.Classification is ToolInvocationClassification.Mutation
            or ToolInvocationClassification.Validation
            or ToolInvocationClassification.Read;
    }

    private static string ResolveTraceRequestSummary(AgentToolInvocationTrace trace)
    {
        return string.IsNullOrWhiteSpace(trace.Signature)
            ? $"sequence={trace.Sequence}"
            : trace.Signature.Trim();
    }

    private static string ResolveTraceExitSummary(AgentToolInvocationTrace trace)
    {
        return trace.Succeeded
            ? "Succeeded"
            : string.IsNullOrWhiteSpace(trace.FailureMessage)
                ? "Failed"
                : $"Failed: {trace.FailureMessage.Trim()}";
    }

    private static string ResolveRuntimeProviderReceiptRiskClass(ToolInvocationClassification classification)
    {
        return classification switch
        {
            ToolInvocationClassification.Mutation => "RuntimeProvider:Mutation",
            ToolInvocationClassification.Validation => "RuntimeProvider:Validation",
            ToolInvocationClassification.Read => "RuntimeProvider:Read",
            ToolInvocationClassification.LocalMcp => "RuntimeProvider:LocalMcp",
            ToolInvocationClassification.HostedMcp => "RuntimeProvider:HostedMcp",
            ToolInvocationClassification.HostedProviderNative => "RuntimeProvider:HostedProviderNative",
            _ => "RuntimeProvider:Unknown"
        };
    }
}
