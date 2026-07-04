using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed partial class AgentFrameworkWorkspaceExecutionService
{
    internal static IReadOnlyList<ToolExecutionReceiptRecord> CreateRuntimeProviderToolReceipts(
        ExecutionRunRecord run,
        AgentRuntimeResponse response)
    {
        if (response.ToolInvocationTraces.Count == 0)
        {
            return [];
        }

        var receipts = new List<ToolExecutionReceiptRecord>();
        foreach (var trace in response.ToolInvocationTraces)
        {
            if (!ShouldCreateRuntimeProviderToolReceipt(trace))
            {
                continue;
            }

            var completedAtUtc = trace.CompletedAtUtc ?? trace.StartedAtUtc;
            receipts.Add(
                new ToolExecutionReceiptRecord(
                    Id: CreateDeterministicGuid($"{run.Id:N}|runtime-provider-tool|{trace.Sequence}|{trace.ToolName}|{trace.RuntimeToolProviderKey}|{trace.Signature}"),
                    ExecutionRunId: run.Id,
                    ToolFamily: "runtime-provider",
                    ToolName: trace.ToolName,
                    RiskClass: ResolveRuntimeProviderReceiptRiskClass(trace.Classification),
                    ApprovalMode: "PolicyEnforced",
                    IsolationGuarantee: "RuntimeProviderPolicy",
                    RequestSummary: string.IsNullOrWhiteSpace(trace.Signature)
                        ? $"sequence={trace.Sequence}"
                        : trace.Signature.Trim(),
                    WorkingDirectory: string.Empty,
                    ExitSummary: trace.Succeeded
                        ? "Succeeded"
                        : $"Failed: {trace.FailureMessage}".Trim(),
                    StartedAtUtc: trace.StartedAtUtc,
                    CompletedAtUtc: completedAtUtc)
                {
                    RuntimeToolProviderKey = trace.RuntimeToolProviderKey.Trim(),
                    RuntimeToolProviderName = trace.RuntimeToolProviderName.Trim()
                });
        }

        return receipts
            .OrderByDescending(item => item.CompletedAtUtc)
            .ThenByDescending(item => item.StartedAtUtc)
            .ToArray();
    }

    private static bool ShouldCreateRuntimeProviderToolReceipt(AgentToolInvocationTrace trace)
    {
        if (string.IsNullOrWhiteSpace(trace.ToolName) ||
            string.IsNullOrWhiteSpace(trace.RuntimeToolProviderKey) ||
            trace.CompletedAtUtc is null)
        {
            return false;
        }

        return trace.Classification is ToolInvocationClassification.Mutation or ToolInvocationClassification.Validation;
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
