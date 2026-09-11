using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed record MafNativeToolContract(string Name, AgentToolSemanticDigest Digest);

internal static class MafNativeToolContracts {
    internal static IReadOnlyList<MafNativeToolContract> Capture(IEnumerable<AITool> tools)
        => tools.Where(tool => tool is not AIFunctionDeclaration).Select(Capture)
            .OrderBy(contract => contract.Name, StringComparer.Ordinal).ToArray();

    internal static MafNativeToolContract Capture(AITool tool) {
        object contract = tool switch {
            HostedMcpServerTool mcp => new {
                Kind = NativeKind.Mcp, mcp.Name, mcp.Description, mcp.ServerName, mcp.ServerAddress,
                mcp.ServerDescription, mcp.AllowedTools,
                Approval = Approval(mcp.ApprovalMode), mcp.AdditionalProperties,
                HeaderDigest = MafToolProtocolCodec.Digest(mcp.Headers)
            },
            HostedCodeInterpreterTool code => new {
                Kind = NativeKind.CodeInterpreter, code.Name, code.Description, code.Inputs, code.AdditionalProperties
            },
            HostedFileSearchTool files => new {
                Kind = NativeKind.FileSearch, files.Name, files.Description, files.Inputs,
                files.MaximumResultCount, files.AdditionalProperties
            },
            HostedWebSearchTool web => new {
                Kind = NativeKind.WebSearch, web.Name, web.Description, web.AdditionalProperties
            },
            _ => throw Unsupported("The configured native tool has no supported immutable recovery contract.")
        };
        return new(tool.Name, MafToolProtocolCodec.Digest(contract));
    }

    internal static AgentToolPreparedCall PrepareApproval(ToolApprovalRequestContent request,
        IEnumerable<AITool> tools) {
        if (request.ToolCall is not McpServerToolCallContent call) {
            throw Unsupported("The native approval type has no supported durable provider-call binding.");
        }

        var matches = tools.OfType<HostedMcpServerTool>().Where(tool =>
            string.Equals(tool.ServerName, call.ServerName, StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1 || matches[0].AllowedTools is { } allowed &&
                !allowed.Contains(call.Name, StringComparer.Ordinal)) {
            throw Unsupported("The native approval is outside the current configured server and allowed tool set.");
        }

        var contract = Capture(matches[0]);
        var arguments = JsonSerializer.SerializeToElement(call.Arguments ?? new Dictionary<string, object?>(),
            MafToolProtocolCodec.SerializationOptions);
        var payload = new AgentToolPreparedPayload(call.Name, 1,
            MafToolProtocolCodec.Digest(new { call.Name, call.ServerName, Contract = contract.Digest, Arguments = arguments }),
            MafToolProtocolCodec.Canonicalize(arguments), AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.ReconcileBeforeRetry);
        return new(call.CallId, payload, true, new(contract.Name, contract.Digest, MafToolProtocolCodec.Encode(request)));
    }

    internal static ToolApprovalRequestContent RestoreApproval(AgentToolProposalRecord proposal) {
        var binding = proposal.ProviderCall ?? throw Unsupported("The native approval has no saved provider call.");
        var request = MafToolProtocolCodec.Decode<ToolApprovalRequestContent>(binding.ProtocolCall);
        if (request.ToolCall is not McpServerToolCallContent call || call.CallId != proposal.CallId ||
            call.Name != proposal.Payload.ToolName || proposal.ApprovalId is { } id && request.RequestId != id) {
            throw Unsupported("The native protocol identity differs from its admitted approval.");
        }

        return request;
    }

    private static object Approval(HostedMcpServerToolApprovalMode? mode) => mode switch {
        null => new { Kind = ApprovalKind.ProviderDefault },
        HostedMcpServerToolAlwaysRequireApprovalMode => new { Kind = ApprovalKind.Always },
        HostedMcpServerToolNeverRequireApprovalMode => new { Kind = ApprovalKind.Never },
        HostedMcpServerToolRequireSpecificApprovalMode specific => new {
            Kind = ApprovalKind.Specific, specific.AlwaysRequireApprovalToolNames, specific.NeverRequireApprovalToolNames
        },
        _ => throw Unsupported("The configured native approval mode has no supported recovery contract.")
    };

    private static AgentToolAdmissionException Unsupported(string message)
        => new("tool-admission.unsupported-native-protocol", message);

    private enum NativeKind { Mcp, CodeInterpreter, FileSearch, WebSearch }
    private enum ApprovalKind { ProviderDefault, Always, Never, Specific }
}
